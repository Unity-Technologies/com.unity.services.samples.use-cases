using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using SampleGameModule.Infrastructure;
using SampleGameModule.Models;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;

namespace SampleGameModule.Services;

/// <summary>
/// The samples' economy: wallet and inventory in one Cloud Save key ("player_economy",
/// see PlayerEconomySnapshot), catalog in Remote Config under ECONOMY_CURRENCIES /
/// ECONOMY_ITEMS / ECONOMY_VIRTUAL_PURCHASES.
///
/// Every mutation is a read → validate → modify → conditional-write transaction against
/// the key's Cloud Save write lock, retried on conflict. The other sample services call
/// the in-process methods (GrantAsync, MakeVirtualPurchasesAsync, MutateAsync) so a whole
/// request lands in a single write; the Economy_* functions are the client-facing entry
/// points Virtual Shop and Starter Pack's debug grant use directly.
/// </summary>
public class EconomyModuleService
{
    public const string PlayerEconomyKey = "player_economy";
    const string k_CurrenciesConfigKey = "ECONOMY_CURRENCIES";
    const string k_VirtualPurchasesConfigKey = "ECONOMY_VIRTUAL_PURCHASES";
    const int k_WriteAttempts = 3;
    static readonly Random s_Random = new();

    readonly ILogger<EconomyModuleService> m_Logger;
    readonly PlayerDataStore m_Store;
    readonly RemoteConfigReader m_Config;

    public EconomyModuleService(ILogger<EconomyModuleService> logger, PlayerDataStore store, RemoteConfigReader config)
    {
        m_Logger = logger;
        m_Store = store;
        m_Config = config;
    }

    /// <summary>Used by: Virtual Shop and Starter Pack debug grants (client), and every sample service in-process.</summary>
    [CloudCodeFunction("Economy_ModifyBalance")]
    public Task<PlayerEconomySnapshot> ModifyBalance(IExecutionContext context, string currencyId, long delta)
    {
        RequireId(currencyId, "currencyId");
        return MutateAsync(context, snapshot => AdjustBalance(snapshot, currencyId, delta));
    }

    /// <summary>Used by: Idle Clicker Game (resets WATER when a board is created) and the integration tests.</summary>
    [CloudCodeFunction("Economy_SetBalance")]
    public Task<PlayerEconomySnapshot> SetBalance(IExecutionContext context, string currencyId, long balance)
    {
        RequireId(currencyId, "currencyId");
        if (balance < 0)
            throw EconomyError.NegativeBalance(currencyId);
        return MutateAsync(context, snapshot => snapshot.Wallet[currencyId] = balance);
    }

    /// <summary>Used by: Battle Pass and Loot Boxes With Cooldown item grants.</summary>
    [CloudCodeFunction("Economy_ModifyInventoryItem")]
    public Task<PlayerEconomySnapshot> ModifyInventoryItem(IExecutionContext context, string itemId, long delta)
    {
        RequireId(itemId, "itemId");
        return MutateAsync(context, snapshot => AdjustCount(snapshot, itemId, delta));
    }

    /// <summary>Used by: Virtual Shop (client), Starter Pack, Battle Pass, Idle Clicker Game, In-Game Mailbox.</summary>
    [CloudCodeFunction("Economy_MakeVirtualPurchase")]
    public Task<PlayerEconomySnapshot> MakeVirtualPurchase(IExecutionContext context, string purchaseId)
    {
        RequireId(purchaseId, "purchaseId");
        return MakeVirtualPurchasesAsync(context, new[] { purchaseId });
    }

    public async Task<PlayerEconomySnapshot> ReadAsync(IExecutionContext context)
    {
        var (snapshot, _) = await LoadAsync(context);
        return snapshot;
    }

    /// <summary>
    /// Applies several wallet and inventory deltas in one write. Rejects the whole batch
    /// if any balance or count would go negative.
    /// </summary>
    public Task<PlayerEconomySnapshot> GrantAsync(IExecutionContext context,
        IEnumerable<KeyValuePair<string, long>>? currencyDeltas,
        IEnumerable<KeyValuePair<string, long>>? itemDeltas = null)
    {
        var currencies = currencyDeltas?.ToList() ?? new List<KeyValuePair<string, long>>();
        var items = itemDeltas?.ToList() ?? new List<KeyValuePair<string, long>>();
        if (currencies.Count == 0 && items.Count == 0)
            return ReadAsync(context);

        return MutateAsync(context, snapshot =>
        {
            foreach (var (id, delta) in currencies)
                AdjustBalance(snapshot, id, delta);
            foreach (var (id, delta) in items)
                AdjustCount(snapshot, id, delta);
        });
    }

    /// <summary>
    /// Executes virtual purchases from ECONOMY_VIRTUAL_PURCHASES in one write: validates every
    /// cost against the wallet and inventory, then applies all costs and rewards. Throws status
    /// 10504 when a purchase id is unknown or its costs are not met; nothing is written then.
    /// </summary>
    public async Task<PlayerEconomySnapshot> MakeVirtualPurchasesAsync(IExecutionContext context, IEnumerable<string> purchaseIds)
    {
        var ids = purchaseIds.ToList();
        var catalog = await GetCatalogAsync(context);
        var purchases = ids.Select(id => (id, definition: catalog.Purchase(id))).ToList();

        return await MutateAsync(context, snapshot =>
        {
            foreach (var (id, definition) in purchases)
            {
                foreach (var cost in definition.Costs)
                {
                    if (Owned(snapshot, catalog, cost.Id) < cost.Amount)
                        throw EconomyError.PurchaseCostsNotMet(id);
                }

                foreach (var cost in definition.Costs)
                    Apply(snapshot, catalog, cost.Id, -cost.Amount);
                foreach (var reward in definition.Rewards)
                    Apply(snapshot, catalog, reward.Id, reward.Amount);
            }
        });
    }

    /// <summary>Runs the read → mutate → conditional-write loop, retrying on write-lock conflict.</summary>
    public async Task<PlayerEconomySnapshot> MutateAsync(IExecutionContext context, Action<PlayerEconomySnapshot> mutate)
    {
        for (var attempt = 1; attempt <= k_WriteAttempts; attempt++)
        {
            var (snapshot, writeLock) = await LoadAsync(context);
            mutate(snapshot);
            try
            {
                await m_Store.SetAsync(context, PlayerEconomyKey, JObject.FromObject(snapshot), writeLock);
                return snapshot;
            }
            catch (ApiException e) when (PlayerDataStore.IsWriteConflict(e))
            {
                if (attempt == k_WriteAttempts)
                    throw EconomyError.WriteConflict();
                var delayMs = 50 * attempt + s_Random.Next(100);
                m_Logger.LogWarning(
                    "Write lock conflict on {Key} for player {PlayerId} (attempt {Attempt}); retrying in {DelayMs} ms",
                    PlayerEconomyKey, context.PlayerId, attempt, delayMs);
                await Task.Delay(delayMs);
            }
        }

        throw EconomyError.WriteConflict();
    }

    public async Task<EconomyCatalog> GetCatalogAsync(IExecutionContext context)
    {
        var config = await m_Config.GetAsync(context, new[] { k_CurrenciesConfigKey, k_VirtualPurchasesConfigKey });
        var currencies = config.GetJson(k_CurrenciesConfigKey) as JObject
            ?? throw new EconomyError(EconomyError.ValidationStatus, "EconomyConfigMissing",
                "Failed to load the economy catalog from Remote Config.");
        var purchases = config.GetJson(k_VirtualPurchasesConfigKey) as JObject ?? new JObject();
        return new EconomyCatalog(
            currencies.Properties().Select(p => p.Name).ToHashSet(),
            purchases);
    }

    async Task<(PlayerEconomySnapshot snapshot, string? writeLock)> LoadAsync(IExecutionContext context)
    {
        var item = await m_Store.GetAsync(context, PlayerEconomyKey);
        return (item.As<PlayerEconomySnapshot>() ?? new PlayerEconomySnapshot(), item.WriteLock);
    }

    static void AdjustBalance(PlayerEconomySnapshot snapshot, string currencyId, long delta)
    {
        var balance = snapshot.Wallet.GetValueOrDefault(currencyId) + delta;
        if (balance < 0)
            throw EconomyError.NegativeBalance(currencyId);
        snapshot.Wallet[currencyId] = balance;
    }

    static void AdjustCount(PlayerEconomySnapshot snapshot, string itemId, long delta)
    {
        var count = snapshot.Inventory.GetValueOrDefault(itemId) + delta;
        if (count < 0)
            throw EconomyError.NegativeCount(itemId);
        snapshot.Inventory[itemId] = count;
    }

    static long Owned(PlayerEconomySnapshot snapshot, EconomyCatalog catalog, string id)
        => catalog.IsCurrency(id) ? snapshot.Wallet.GetValueOrDefault(id) : snapshot.Inventory.GetValueOrDefault(id);

    static void Apply(PlayerEconomySnapshot snapshot, EconomyCatalog catalog, string id, long delta)
    {
        var target = catalog.IsCurrency(id) ? snapshot.Wallet : snapshot.Inventory;
        target[id] = target.GetValueOrDefault(id) + delta;
    }

    static void RequireId(string? value, string name)
    {
        if (string.IsNullOrEmpty(value))
            throw new EconomyError(EconomyError.ValidationStatus, "EconomyValidation", $"{name} is required.");
    }
}

/// <summary>The parts of the Remote Config catalog the module needs: which ids are currencies, and the purchases.</summary>
public class EconomyCatalog
{
    readonly HashSet<string> m_CurrencyIds;
    readonly JObject m_Purchases;

    public EconomyCatalog(HashSet<string> currencyIds, JObject purchases)
    {
        m_CurrencyIds = currencyIds;
        m_Purchases = purchases;
    }

    public bool IsCurrency(string id) => m_CurrencyIds.Contains(id);

    public VirtualPurchaseDefinition Purchase(string purchaseId)
    {
        if (m_Purchases[purchaseId] is not JObject json)
            throw EconomyError.UnknownPurchase(purchaseId);
        return json.ToObject<VirtualPurchaseDefinition>() ?? throw EconomyError.UnknownPurchase(purchaseId);
    }
}
