using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using GemHunterUGSCloud.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Model;

namespace GemHunterUGSCloud.Services;

/// <summary>
/// Manages all player economy operations including currencies, inventory, and special items.
///
/// The whole economy of a player lives in one Cloud Save key (<see cref="k_CloudSaveKey"/>) as a
/// <see cref="PlayerEconomyRecord"/>. Every change is a read, an in-memory edit and a conditional write
/// guarded by the Cloud Save write lock, retried on conflict, so concurrent calls for the same player
/// never lose an update. The key is in the protected access class: the player can read it, and only this
/// module, with the service token, can write it. Clients read their economy through <c>GetPlayerEconomyData</c>.
///
/// Core Responsibilities:
/// - Currency operations (coins) with conditional increment/decrement
/// - Inventory management (power-ups, consumables)
/// - New player economy initialization with starter currencies and items
/// - Infinite heart activation with time-based expiry
/// - Free pack claiming and real-money purchase grants, deduplicated by transaction id
///
/// Key Cloud Code Functions:
/// - GetPlayerEconomyData: Retrieves complete economy state
///
/// - Cloud Save docs: https://docs.unity3d.com/Packages/com.unity.services.apis@1.1/api/Unity.Services.Apis.CloudSave.html
/// </summary>
public class PlayerEconomyService
{
    public const string k_CloudSaveKey = "PLAYER_ECONOMY";
    const int k_MaxWriteAttempts = 3;

    // 6 minutes infinite heart for testing...
    readonly long m_InfiniteHeartDurationSeconds = 6 * 60;

    readonly IGameApiClient m_GameApiClient;
    readonly ILogger<PlayerEconomyService> m_Logger;

    public PlayerEconomyService(ILogger<PlayerEconomyService> logger, IGameApiClient gameApiClient)
    {
        m_Logger = logger;
        m_GameApiClient = gameApiClient;
    }

    #region Player Lifecycle Management

    /// <summary>Writes the starter economy of a player who signs in for the first time.</summary>
    public async Task InitializeNewPlayerEconomy(IExecutionContext context)
    {
        var record = new PlayerEconomyRecord();
        // The former Economy catalog opened every wallet with 100 coins and sign-in added a 100 coin bonus on top.
        record.Currencies[EconomyConstants.Currencies.k_Coin] = 200;
        record.Items[EconomyConstants.Items.k_LargeBomb] = 1;
        record.Items[EconomyConstants.Items.k_SmallBomb] = 3;
        record.Items[EconomyConstants.Items.k_HorizontalRocket] = 1;
        record.Items[EconomyConstants.Items.k_VerticalRocket] = 1;
        record.Items[EconomyConstants.Items.k_ColorBonus] = 1;

        await WriteAsync(context, record, writeLock: null);
        m_Logger.LogInformation("Initialized starter economy for new player {PlayerId}", context.PlayerId);
    }

    /// <summary>
    /// Ensures all required inventory items exist for the player.
    /// Adds a zero-quantity slot for every required item the player's record lacks, so all
    /// players have consistent inventory slots even when new items are added to the game
    /// after their account creation. Returns true when something was added.
    /// </summary>
    public async Task<bool> EnsurePlayerHasRequiredInventoryItems(IExecutionContext context, PlayerEconomyData currentData)
    {
        var missingItems = PlayerEconomyRecord.RequiredItemIds
            .Where(id => !currentData.ItemInventory.ContainsKey(id))
            .ToList();
        if (missingItems.Count == 0)
        {
            return false;
        }

        await MutateAsync(context, record =>
        {
            foreach (var itemId in missingItems)
            {
                record.Items.TryAdd(itemId, 0);
            }
        });
        m_Logger.LogInformation("Added missing inventory items {Items} for player {PlayerId}", missingItems, context.PlayerId);
        return true;
    }

    #endregion

    #region Data Access

    [CloudCodeFunction("GetPlayerEconomyData")]
    public async Task<PlayerEconomyData?> GetPlayerEconomyData(IExecutionContext context)
    {
        if (context.PlayerId == null)
        {
            m_Logger.LogInformation("Player data is null");
            return null;
        }

        try
        {
            var (record, _) = await ReadAsync(context);
            return ToEconomyData(record);
        }
        catch (Exception e)
        {
            m_Logger.LogError(e, "Failed to get player economy data.");
            throw;
        }
    }

    PlayerEconomyData ToEconomyData(PlayerEconomyRecord record)
    {
        var infiniteHeartExpiry = record.InfiniteHeartActivatedAt + m_InfiniteHeartDurationSeconds;
        return new PlayerEconomyData
        {
            Currencies = new Dictionary<string, int>(record.Currencies),
            ItemInventory = new Dictionary<string, int>(record.Items),
            HasPurchasedFreeCoinPack = record.FreeCoinPackClaimedAt > 0,
            InfiniteHeartsExpiryTimestamp = infiniteHeartExpiry > Now() ? infiniteHeartExpiry : 0,
        };
    }

    #endregion

    #region Currency Operations

    /// <summary>Adds <paramref name="amount"/> (negative to spend) and returns the new balance. Spending below zero throws.</summary>
    public async Task<int> UpdatePlayerCurrency(IExecutionContext context, string currencyId, int amount)
    {
        m_Logger.LogInformation("Attempting currency update: PlayerId: {PlayerId}, Currency: {CurrencyId}, Amount: {Amount}",
            context.PlayerId, currencyId, amount);

        var record = await MutateAsync(context, r => AddCurrency(r, currencyId, amount));
        var newBalance = record.Currencies[currencyId];
        m_Logger.LogInformation("Currency {CurrencyId} updated. New balance: {NewBalance}", currencyId, newBalance);
        return newBalance;
    }

    static void AddCurrency(PlayerEconomyRecord record, string currencyId, int amount)
    {
        var balance = record.Currencies.GetValueOrDefault(currencyId);
        if (balance + amount < 0)
        {
            throw new InvalidOperationException($"Insufficient {currencyId}: balance {balance}, requested {amount}");
        }

        record.Currencies[currencyId] = balance + amount;
    }

    #endregion

    #region Inventory Operations

    /// <summary>Adds <paramref name="amount"/> of an item to the player's inventory.</summary>
    public async Task UpdateInventoryItem(IExecutionContext context, string itemId, int amount)
    {
        m_Logger.LogInformation("UpdateInventoryItem called with itemId: {ItemId}, amount: {Amount}", itemId, amount);

        if (string.IsNullOrEmpty(itemId))
        {
            m_Logger.LogError("Item ID is null or empty");
            throw new ArgumentException("Item ID cannot be null or empty.", nameof(itemId));
        }

        if (amount < 0)
        {
            m_Logger.LogError("Invalid amount: {Amount}", amount);
            throw new ArgumentException("Quantity must be zero or greater.", nameof(amount));
        }

        var record = await MutateAsync(context, r => AddItem(r, itemId, amount));
        m_Logger.LogInformation("Successfully added/updated inventory item: {ItemId}, New amount: {NewAmount}", itemId, record.Items[itemId]);
    }

    static void AddItem(PlayerEconomyRecord record, string itemId, int amount)
    {
        record.Items[itemId] = record.Items.GetValueOrDefault(itemId) + amount;
    }

    #endregion

    #region Purchase Operations

    /// <summary>
    /// Grants a real-money purchase in one write and remembers its transaction id.
    /// Returns false, granting nothing, when the transaction was already redeemed.
    /// </summary>
    public async Task<bool> GrantPurchase(IExecutionContext context, string transactionId,
        IReadOnlyDictionary<string, int> currencies, IReadOnlyDictionary<string, int> items, bool infiniteHeart)
    {
        var granted = false;
        await MutateAsync(context, record =>
        {
            granted = !record.RedeemedTransactionIds.Contains(transactionId);
            if (!granted)
            {
                return false;
            }

            foreach (var (currencyId, amount) in currencies)
            {
                AddCurrency(record, currencyId, amount);
            }

            foreach (var (itemId, amount) in items)
            {
                AddItem(record, itemId, amount);
            }

            if (infiniteHeart)
            {
                record.InfiniteHeartActivatedAt = Now();
            }

            record.RedeemedTransactionIds.Add(transactionId);
            return true;
        });

        return granted;
    }

    public async Task<bool> HasPlayerClaimedFreePack(IExecutionContext context)
    {
        var (record, _) = await ReadAsync(context);
        return record.FreeCoinPackClaimedAt > 0;
    }

    public async Task MarkFreePackAsClaimed(IExecutionContext context)
    {
        await MutateAsync(context, record => record.FreeCoinPackClaimedAt = Now());
        m_Logger.LogInformation("Marked free pack as claimed for player {PlayerId}", context.PlayerId);
    }

    #endregion

    #region Cloud Save Access

    async Task<(PlayerEconomyRecord Record, string? WriteLock)> ReadAsync(IExecutionContext context)
    {
        var response = await m_GameApiClient.CloudSaveData.GetProtectedItemsAsync(context, context.ServiceToken,
            context.ProjectId, context.PlayerId!, new List<string> { k_CloudSaveKey });
        var item = response.Data?.Results?.FirstOrDefault();
        if (item?.Value == null)
        {
            return (new PlayerEconomyRecord(), null);
        }

        var record = JToken.FromObject(item.Value).ToObject<PlayerEconomyRecord>() ?? new PlayerEconomyRecord();
        return (record, item.WriteLock);
    }

    Task WriteAsync(IExecutionContext context, PlayerEconomyRecord record, string? writeLock)
    {
        return m_GameApiClient.CloudSaveData.SetProtectedItemAsync(context, context.ServiceToken, context.ProjectId,
            context.PlayerId!, new SetItemBody(k_CloudSaveKey, record, writeLock!));
    }

    Task<PlayerEconomyRecord> MutateAsync(IExecutionContext context, Action<PlayerEconomyRecord> apply)
    {
        return MutateAsync(context, record =>
        {
            apply(record);
            return true;
        });
    }

    /// <summary>
    /// Reads the record, applies <paramref name="apply"/> and, when it returns true, writes the record back under
    /// the write lock of the read. When another call wrote in between, Cloud Save answers 409 and the whole step
    /// runs again on fresh data.
    /// </summary>
    async Task<PlayerEconomyRecord> MutateAsync(IExecutionContext context, Func<PlayerEconomyRecord, bool> apply)
    {
        if (context.PlayerId == null)
        {
            throw new InvalidOperationException("PlayerId cannot be null");
        }

        for (var attempt = 1;; attempt++)
        {
            var (record, writeLock) = await ReadAsync(context);
            if (!apply(record))
            {
                return record;
            }

            try
            {
                await WriteAsync(context, record, writeLock);
                return record;
            }
            catch (ApiException e) when (e.Response?.StatusCode == HttpStatusCode.Conflict && attempt < k_MaxWriteAttempts)
            {
                m_Logger.LogWarning("Economy write conflict for player {PlayerId}, attempt {Attempt} of {Max}",
                    context.PlayerId, attempt, k_MaxWriteAttempts);
            }
        }
    }

    static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    #endregion
}
