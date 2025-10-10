using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GemHunterUGSCloud.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.Economy.Model;
namespace GemHunterUGSCloud.Services;

/// <summary>
/// Manages all player economy operations including currencies, inventory, and special items.
/// 
/// Core Responsibilities:
/// - Currency operations (coins, gems, etc.) with atomic increment/decrement
/// - Inventory management (power-ups, consumables, special items)
/// - New player economy initialization with starter currencies and items
/// - Infinite heart system with time-based expiration tracking
/// - Free pack claiming and validation
/// - Economy data synchronization and cleanup operations
/// 
/// Key Cloud Code Functions:
/// - GetPlayerEconomyData: Retrieves complete economy state
/// 
/// Special Features:
/// - Automatic missing item detection and addition for existing players
/// - Time-based infinite heart expiration management
/// - Zero-amount inventory item cleanup
/// - Atomic currency operations with write-lock protection
/// 
/// - Economy docs: https://docs.unity3d.com/Packages/com.unity.services.apis@1.1/api/Unity.Services.Apis.Economy.html
/// </summary>
public class PlayerEconomyService
{
    // 6 minutes infinite heart for testing...
    private readonly float m_InfiniteHeartDuration = 0.1f * 3600f; 
    
    private readonly IGameApiClient m_GameApiClient;
    private readonly ILogger<PlayerEconomyService> m_Logger;
    
    public PlayerEconomyService(ILogger<PlayerEconomyService> logger, IGameApiClient gameApiClient)
    {
        m_Logger = logger;
        m_GameApiClient = gameApiClient;
    }
    
    #region Player Lifecycle Management
    
    public async Task AddBonusNewPlayerCurrencies(IExecutionContext context)
    {
        var initialCurrencies = new Dictionary<string, int>
        {
            { EconomyConstants.Currencies.k_Coin, 100 },
            // Add new currencies here that all new players should have
        };
        
        foreach (var currency in initialCurrencies)
        {
            try
            {
                var newBalance = await UpdatePlayerCurrency(context, currency.Key, currency.Value);
                m_Logger.LogInformation("Initialized currency {CurrencyId} with {Amount} for new player {PlayerId}. New balance: {NewBalance}", 
                    currency.Key, currency.Value, context.PlayerId, newBalance);
            }
            catch (Exception ex)
            {
                m_Logger.LogError(ex, "Failed to initialize currency {CurrencyId} for new player {PlayerId}", 
                    currency.Key, context.PlayerId);
            }
        }
    }
    
    public async Task InitializeNewPlayerInventoryItems(IExecutionContext context)
    {
        var initialItems = new Dictionary<string, int>
        {
            { EconomyConstants.Items.k_LargeBomb, 1 },
            { EconomyConstants.Items.k_SmallBomb, 3 },
            { EconomyConstants.Items.k_HorizontalRocket, 1 },
            { EconomyConstants.Items.k_VerticalRocket, 1 },
            { EconomyConstants.Items.k_ColorBonus, 1 }
        };
        
        var tasks = initialItems.Select(async item =>
        {
            try
            {
                await AddInventoryItem(context, item.Key, item.Value);
                m_Logger.LogInformation("Granted initial inventory item {ItemId} x{Amount} for new player {PlayerId}", 
                    item.Key, item.Value, context.PlayerId);
            }
            catch (Exception ex)
            {
                m_Logger.LogError(ex, "Failed to grant initial inventory item {ItemId} for new player {PlayerId}", 
                    item.Key, context.PlayerId);
            }
        });
    
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Ensures all required inventory items exist for the player.
    /// Checks for missing inventory items that should be available to all players
    /// and adds them with zero quantity if they don't exist. This ensures all
    /// players have consistent inventory slots even when new items are added
    /// to the game after their account creation.
    /// </summary>
    public async Task<bool> EnsurePlayerHasRequiredInventoryItems(IExecutionContext context, PlayerEconomyData currentData)
    {
        var requiredItems = GetRequiredInventoryItems();
    
        var missingItems = requiredItems.Where(item => !currentData.ItemInventory.ContainsKey(item.Key)).ToList();
    
        if (missingItems.Any())
        {
            var tasks = missingItems.Select(item => {
                m_Logger.LogInformation("Adding missing inventory item: {ItemId} for player {PlayerId}", 
                    item.Key, context.PlayerId);
                return AddInventoryItem(context, item.Key, item.Value);
            });
        
            try
            {
                await Task.WhenAll(tasks);
                m_Logger.LogInformation("Successfully initialized all missing items for player {PlayerId}", context.PlayerId);
            }
            catch (Exception ex)
            {
                m_Logger.LogError(ex, "Failed to initialize some missing items for player {PlayerId}", context.PlayerId);
            }
        }
        return missingItems.Any();
    }
    
    private Dictionary<string, int> GetRequiredInventoryItems()
    {
        return new Dictionary<string, int>
        {
            // Core gameplay items
            { EconomyConstants.Items.k_LargeBomb, 0 },
            { EconomyConstants.Items.k_SmallBomb, 0 },
            { EconomyConstants.Items.k_HorizontalRocket, 0 },
            { EconomyConstants.Items.k_VerticalRocket, 0 },
            { EconomyConstants.Items.k_ColorBonus, 0 },
            // If you add new items later, add them here
            // { "NEW_POWERUP", 0 },
        };
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
            var economyData = new PlayerEconomyData
            {
                Currencies = new Dictionary<string, int>(),
                ItemInventory = new Dictionary<string, int>()  // Initialize empty dictionaries
            };
        
            var currencyTask = GetPlayerCurrencies(context, economyData);
            var inventoryTask = GetPlayerItemInventory(context, economyData);
            
            await Task.WhenAll(currencyTask, inventoryTask);
            
            // Check infinite heart status
            var infiniteHeartItem = await GetExistingInventoryItem(context, EconomyConstants.Items.k_InfiniteHeart);
            if (infiniteHeartItem != null)
            {
                var isActive = await CheckInfiniteHeartExpiration(context, infiniteHeartItem);
                if (isActive)
                {
                    var instanceData = ParseInfiniteHeartInstanceData(infiniteHeartItem.InstanceData);
                    if (instanceData != null)
                    {
                        economyData.InfiniteHeartsExpiryTimestamp = instanceData.ActivatedAt + Convert.ToInt64(m_InfiniteHeartDuration);
                    }
                }
            }

            var hasClaimed = await HasPlayerClaimedFreePack(context);
            economyData.HasPurchasedFreeCoinPack = hasClaimed;
            
            return economyData;
        }
        
        catch (Exception e)
        {
            m_Logger.LogError(e, "Failed to get player economy data.");
            throw;
        }
    }
    
    private async Task GetPlayerCurrencies(IExecutionContext context, PlayerEconomyData economyData)
    {
        try
        {
            var balancesResponse = await m_GameApiClient.EconomyCurrencies.GetPlayerCurrenciesAsync(
                context,
                context.AccessToken,
                context.ProjectId,
                context.PlayerId!
            );
            
            if (balancesResponse?.Data?.Results != null)
            {
                foreach (var balance in balancesResponse.Data.Results)
                {
                    if (!string.IsNullOrEmpty(balance.CurrencyId))
                    {
                        economyData.Currencies[balance.CurrencyId] = (int)balance.Balance;
                    }
                }
            }
            else
            {
                m_Logger.LogWarning("No currency data returned from Economy service");
            }
        }
        catch (Exception e)
        {
            m_Logger.LogError(e, "Failed to get player currencies");
            throw;
        }
    }
    
    private async Task GetPlayerItemInventory(IExecutionContext context, PlayerEconomyData economyData)
    {
        if (economyData?.ItemInventory == null)
        {
            m_Logger.LogInformation("Player data item inventory is null");
            return;
        }
        
        try
        {
            var inventoryResponse = await m_GameApiClient.EconomyInventory.GetPlayerInventoryAsync(
                context,
                context.AccessToken,
                context.ProjectId,
                context.PlayerId!
            );

            if (inventoryResponse?.Data?.Results != null)
            {
                UpdateInventoryFromResponse(economyData, inventoryResponse.Data.Results);
            }
            else
            {
                m_Logger.LogWarning("No inventory data returned from Economy service");
            }
        }
        catch (Exception e)
        {
            m_Logger.LogError(e, "Failed to update inventory from Economy service");
            throw;
        }
    }
    
    private void UpdateInventoryFromResponse(PlayerEconomyData economyData, List<InventoryResponse> inventoryItems)
    {
        foreach (var item in inventoryItems)
        {
            if (!string.IsNullOrEmpty(item.InventoryItemId))
            {
                int amount = ParseInventoryItemAmount(item.InstanceData);
                economyData.ItemInventory[item.InventoryItemId] = amount;
            }
            else
            {
                m_Logger.LogWarning("Received an inventory item with null or empty InventoryItemId");
            }
        }
    }
    
    private int ParseInventoryItemAmount(object? instanceData)
    {
        if (instanceData == null) return 0;

        try
        {
            var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(instanceData.ToString() ?? string.Empty);
            if (data != null && data.TryGetValue("amount", out var amountObj))
            {
                return int.TryParse(amountObj.ToString(), out int amount) ? amount : 0;
            }
        }
        catch (JsonException)
        {
            m_Logger.LogWarning($"Failed to parse InstanceData as JSON");
        }

        return 0;
    }
    
    #endregion
    
    #region Currency Operations
    
    public async Task<int> UpdatePlayerCurrency(IExecutionContext context, string currencyId, int amount)
    {
        m_Logger.LogInformation("Attempting currency update: PlayerId: {PlayerId}, Currency: {CurrencyId}, Amount: {Amount}", 
            context.PlayerId, currencyId, amount);
        
        if (context.PlayerId == null)
        {
            throw new InvalidOperationException("PlayerId cannot be null");
        }
        
        try
        {
            // Get current balance and write lock
            var balanceResponse = await m_GameApiClient.EconomyCurrencies.GetPlayerCurrenciesAsync(
                context,
                context.AccessToken,
                context.ProjectId,
                context.PlayerId
            );
            
            // Find the current currency and its write lock
            var currentCurrency = balanceResponse.Data.Results.FirstOrDefault(c => c.CurrencyId == currencyId);
            if (currentCurrency == null)
            {
                throw new InvalidOperationException($"Currency {currencyId} not found in player balances for player {context.PlayerId}");
            }
            
            var currencyModifyBalanceRequest = new CurrencyModifyBalanceRequest(
                currencyId: currencyId,         
                amount: Math.Abs((long)amount),
                writeLock: currentCurrency.WriteLock
            );

            var response = amount >= 0 
                ? await m_GameApiClient.EconomyCurrencies.IncrementPlayerCurrencyBalanceAsync(
                    context,
                    context.AccessToken,
                    context.ProjectId,
                    context.PlayerId ?? throw new InvalidOperationException(),
                    currencyId,
                    currencyModifyBalanceRequest)
                : await m_GameApiClient.EconomyCurrencies.DecrementPlayerCurrencyBalanceAsync(
                    context,
                    context.AccessToken,
                    context.ProjectId,
                    context.PlayerId ?? throw new InvalidOperationException(),
                    currencyId,
                    currencyModifyBalanceRequest);
            
            int newBalance = (int)response.Data.Balance;
            m_Logger.LogInformation($"Currency {currencyId} updated in Economy service. New balance: {newBalance}");
            
            return newBalance;
        }
        catch (Exception e)
        {
            m_Logger.LogError(e, $"Failed to update currency {currencyId}. Amount: {amount}, Exception details: {e.GetType()}, Message: {e.Message}, Inner Exception: {e.InnerException?.Message}");
            throw new Exception($"Failed to update currency: {e.Message}");
        }
    }
    
    #endregion
    
    #region Inventory Operations
    
    public async Task UpdateInventoryItem(IExecutionContext context, string itemId, int amount)
    {
        m_Logger.LogInformation($"UpdateInventoryItem called with itemId: {itemId}, amount: {amount}");

        ValidateInput();

        void ValidateInput()
        {
            if (string.IsNullOrEmpty(itemId))
            {
                m_Logger.LogError("Item ID is null or empty");
                throw new ArgumentException("Item ID cannot be null or empty.", nameof(itemId));
            }

            if (amount < 0)
            {
                m_Logger.LogError($"Invalid amount: {amount}");
                throw new ArgumentException("Quantity must be zero or greater.", nameof(amount));
            }
        }

        try
        {
            var existingItem = await GetExistingInventoryItem(context, itemId);

            if (existingItem != null)
            {
                var (currentAmount, playersInventoryItemId) = ExtractItemDetails(existingItem);
                int newAmount = currentAmount + amount;
                await UpdateItemInventory(context, itemId, playersInventoryItemId, newAmount);
                m_Logger.LogInformation($"Successfully added/updated inventory item: {itemId}, New amount: {newAmount}");
            }
            else
            {
                await AddInventoryItem(context, itemId, amount);
                m_Logger.LogInformation($"Successfully added new inventory item: {itemId}, Amount: {amount}");
            }
        }
        catch (Exception e)
        {
            m_Logger.LogError(e, $"Failed to add inventory item: {itemId}. Error: {e.Message}");
            throw;
        }
    }
    
    public async Task AddInventoryItem(IExecutionContext context, string itemId, int amount)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            m_Logger.LogError($"Attempted to add inventory item with null or empty itemId. Amount: {amount}");
            throw new ArgumentNullException(nameof(itemId), "itemId is required and cannot be null or empty");
        }
        
        var instanceData = new Dictionary<string, object>
        {
            { "amount", amount }
        };

        var inventoryRequest = new AddInventoryRequest(itemId, instanceData: instanceData);
        
        try
        {
            await m_GameApiClient.EconomyInventory.AddInventoryItemAsync(
                context,
                context.AccessToken,
                context.ProjectId,
                context.PlayerId ?? throw new InvalidOperationException(),
                inventoryRequest
            );
        }
        catch (Exception e)
        {
            m_Logger.LogError(e, $"Exception in AddInventoryItemAsync. ItemId: '{itemId}', Amount: {amount}");
            throw;
        }
    }

    /// <summary>
    /// For GetPlayerInventory 
    /// <see href="<https://docs.unity3d.com/Packages/com.unity.services.apis@1.1/api/Unity.Services.Apis.Economy.EconomyInventoryApi.html#Unity_Services_Apis_Economy_EconomyInventoryApi_GetPlayerInventory_System_String_System_String_System_String_System_String_System_String_System_String_System_Nullable_System_Int32__System_Collections_Generic_List_System_String__System_Collections_Generic_List_System_String__System_Threading_CancellationToken_>"/>
    /// </summary>
    /// <param name="context"></param>
    /// <param name="itemId"></param>
    /// <returns></returns>
    private async Task<InventoryResponse?> GetExistingInventoryItem(IExecutionContext context, string itemId)
    {
        try
        {
            var existingInventory = await m_GameApiClient.EconomyInventory.GetPlayerInventoryAsync(
                context,
                context.AccessToken,
                context.ProjectId,
                context.PlayerId ?? throw new InvalidOperationException(),
                inventoryItemIds: new List<string> { itemId }
            );

            m_Logger.LogInformation($"GetPlayerInventoryAsync response: {JsonConvert.SerializeObject(existingInventory)}");

            if (existingInventory?.Data?.Results != null && existingInventory.Data.Results.Count > 0)
            {
                return existingInventory.Data.Results[0];
            }

            m_Logger.LogInformation($"No existing inventory item found for itemId: {itemId}");
            return null;
        }
        catch (Exception e)
        {
            m_Logger.LogError(e, $"Error retrieving existing inventory item for itemId: {itemId}");
            throw;
        }
    }

    private async Task UpdateItemInventory(IExecutionContext context, string itemId, string playersInventoryItemId, int newAmount)
    {
        if (context.PlayerId == null)
        {
            throw new InvalidOperationException("PlayerId cannot be null");
        }
        
        var instanceData = new Dictionary<string, object>
        {
            { "amount", newAmount }
        };

        var inventoryUpdateRequest = new InventoryRequestUpdate(instanceData: instanceData);

        m_Logger.LogInformation($"Created AddInventoryRequest. Full request details: {JsonConvert.SerializeObject(inventoryUpdateRequest)}");

        try
        {
            var response = await m_GameApiClient.EconomyInventory.UpdateInventoryItemAsync(
                context,
                context.AccessToken,
                context.ProjectId,
                context.PlayerId,
                playersInventoryItemId,
                inventoryUpdateRequest
            );

            m_Logger.LogInformation("Successfully updated item inventory for item {ItemId}, player {PlayerId}. New amount: {NewAmount}", 
                itemId, context.PlayerId, newAmount);
        }
        catch (Exception e)
        {
            m_Logger.LogError(e, $"Exception thrown in AddInventoryItemAsync. Request details: {JsonConvert.SerializeObject(inventoryUpdateRequest)}");
            throw;
        }
    }

    private (int currentAmount, string playersInventoryItemId) ExtractItemDetails(InventoryResponse existingItem)
    {
        string playersInventoryItemId = existingItem.PlayersInventoryItemId;
        var currentAmount = ParseInstanceData(existingItem.InstanceData) ?? 0;
        
        return (currentAmount, playersInventoryItemId);
    }

    private int? ParseInstanceData(object? instanceData)
    {
        if (instanceData == null) return null;
        
        try
        {
            var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(instanceData.ToString() ?? string.Empty);
            if (data != null && data.TryGetValue("amount", out var amountObj))
            {
                return int.TryParse(amountObj.ToString(), out int amount) ? amount : 0;
            }
        }
        catch (JsonException)
        {
            m_Logger.LogWarning($"Failed to parse InstanceData as JSON");
        }
        
        return null;
    }
    
    #endregion
    
    #region Infinite Heart Operations
    
    public async Task AddInfiniteHeart(IExecutionContext context, string infiniteHeartId)
    {
        try
        {
            var inventoryRequest = new AddInventoryRequest(
                infiniteHeartId, 
                instanceData: new Dictionary<string, object>
                {
                    { "active", true },
                    { "activatedAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
                }
            );
        
            await m_GameApiClient.EconomyInventory.AddInventoryItemAsync(
                context,
                context.AccessToken,
                context.ProjectId,
                context.PlayerId ?? throw new InvalidOperationException(),
                inventoryRequest
            );
        }
        catch (Exception e)
        {
            m_Logger.LogError(e, "Failed to add Infinite Heart");
            throw;
        }
    }
    
    private async Task<bool> CheckInfiniteHeartExpiration(IExecutionContext context, InventoryResponse infiniteHeartItem)
    {
        try
        {
            var instanceData = ParseInfiniteHeartInstanceData(infiniteHeartItem.InstanceData);
            if (instanceData == null) return false;

            long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long expiryTime = instanceData.ActivatedAt + Convert.ToInt64(m_InfiniteHeartDuration);

            // If expired and still marked as active, update to inactive
            if (currentTime > expiryTime && instanceData.Active)
            {
                await UpdateInfiniteHeartStatus(context, infiniteHeartItem.PlayersInventoryItemId, instanceData.ActivatedAt, false);
                m_Logger.LogInformation("Marked expired infinite heart as inactive for player {PlayerId}. Expiry time: {ExpiryTime}, Current time: {CurrentTime}", 
                    context.PlayerId, expiryTime, currentTime);
                return false;
            }

            return instanceData.Active;
        }
        catch (Exception e)
        {
            m_Logger.LogError(e, "Failed to check infinite heart expiration");
            throw;
        }
    }
    
    private async Task UpdateInfiniteHeartStatus(IExecutionContext context, string playersInventoryItemId, long activatedAt, bool active)
    {
        if (context.PlayerId == null)
        {
            throw new InvalidOperationException("PlayerId cannot be null");
        }
        
        var updateRequest = new InventoryRequestUpdate(
            instanceData: new Dictionary<string, object>
            {
                { "active", active },
                { "activatedAt", activatedAt }
            }
        );

        await m_GameApiClient.EconomyInventory.UpdateInventoryItemAsync(
            context,
            context.AccessToken,
            context.ProjectId,
            context.PlayerId,
            playersInventoryItemId,
            updateRequest
        );
        
        m_Logger.LogInformation("Updated infinite heart status to {Active} for player {PlayerId}", 
            active, context.PlayerId);
    }
    
    private InfiniteHeartData? ParseInfiniteHeartInstanceData(object? instanceData)
    {
        if (instanceData == null) return null;

        try
        {
            var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(instanceData.ToString() ?? string.Empty);
            if (data != null && 
                data.TryGetValue("active", out var activeObj) &&
                data.TryGetValue("activatedAt", out var activatedAtObj))
            {
                return new InfiniteHeartData
                {
                    Active = Convert.ToBoolean(activeObj),
                    ActivatedAt = Convert.ToInt64(activatedAtObj)
                };
            }
        }
        catch (JsonException e)
        {
            m_Logger.LogWarning(e, "Failed to parse infinite heart instance data");
        }
        
        return null;
    }

    private class InfiniteHeartData
    {
        public bool Active { get; set; }
        public long ActivatedAt { get; set; }
    }
    
    #endregion
    
    #region Free Pack Operations
    
    public async Task<bool> HasPlayerClaimedFreePack(IExecutionContext context)
    {
        if (context.PlayerId == null)
        {
            throw new InvalidOperationException("PlayerId cannot be null");
        }
        
        try
        {
            var inventoryResponse = await m_GameApiClient.EconomyInventory.GetPlayerInventoryAsync(
                context,
                context.AccessToken,
                context.ProjectId,
                context.PlayerId,
                inventoryItemIds: new List<string> { EconomyConstants.Products.k_CoinPackFree }
            );

            return inventoryResponse?.Data?.Results?.Any(item => item.InventoryItemId == EconomyConstants.Products.k_CoinPackFree) ?? false;
        }
        catch (Exception e)
        {
            m_Logger.LogError(e, "Failed to check free pack status");
            throw;
        }
    }
    
    public async Task MarkFreePackAsClaimed(IExecutionContext context)
    {
        try
        {
            var instanceData = new Dictionary<string, object>
            {
                { "claimedAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
            };

            var inventoryRequest = new AddInventoryRequest(EconomyConstants.Products.k_CoinPackFree, instanceData: instanceData);
        
            await m_GameApiClient.EconomyInventory.AddInventoryItemAsync(
                context,
                context.AccessToken,
                context.ProjectId,
                context.PlayerId!,
                inventoryRequest
            );
        
            m_Logger.LogInformation($"Marked free pack as claimed for player {context.PlayerId}");
        }
        catch (Exception e)
        {
            m_Logger.LogError(e, "Failed to mark free pack as claimed");
            throw;
        }
    }
    
    #endregion
    
    #region Maintenance Operations
    
    public async Task CleanupZeroAmountItems(IExecutionContext context)
    {
        if (context.PlayerId == null)
        {
            m_Logger.LogError("Cannot cleanup zero amount items: PlayerId is null");
            return;
        }
        
        try
        {
            var inventoryResponse = await m_GameApiClient.EconomyInventory.GetPlayerInventoryAsync(
                context,
                context.AccessToken,
                context.ProjectId,
                context.PlayerId!
            );

            if (inventoryResponse?.Data?.Results == null) return; // No items to clean up
            
            int deletedCount = 0;
            foreach (var item in inventoryResponse.Data.Results)
            {
                if (string.IsNullOrEmpty(item.PlayersInventoryItemId)) continue;
                
                // Skip infinite heart item
                if (item.InventoryItemId == EconomyConstants.Items.k_InfiniteHeart) continue;
                
                var amount = ParseInstanceData(item.InstanceData);
                
                if (amount.HasValue && amount.Value <= 0)
                {
                    var deleteSuccess = await DeleteInventoryItem(context, item.PlayersInventoryItemId, item.InventoryItemId);
                    if (deleteSuccess)
                    {
                        deletedCount++;
                    }
                }
            }
            
            if (deletedCount > 0)
            {
                m_Logger.LogInformation("Cleaned up {DeletedCount} zero-amount items for player {PlayerId}", 
                    deletedCount, context.PlayerId);
            }
        }
        catch (Exception e)
        {
            m_Logger.LogError(e, "Failed to cleanup zero-amount items");
            throw;
        }
    }
    
    private async Task<bool> DeleteInventoryItem(IExecutionContext context, string playersInventoryItemId, string itemId)
    {
        try
        {
            await m_GameApiClient.EconomyInventory.DeleteInventoryItemAsync(
                context,
                context.AccessToken,
                context.ProjectId,
                context.PlayerId!,
                playersInventoryItemId
            );
            
            m_Logger.LogInformation("Deleted zero-amount item: {ItemId} for player {PlayerId}", 
                itemId, context.PlayerId);
            return true;
        }
        catch (Exception e)
        {
            m_Logger.LogError(e, "Failed to delete inventory item {ItemId} for player {PlayerId}", 
                itemId, context.PlayerId);
            return false;
        }
    }
    
    #endregion
    
    #region Helper Methods
    
    // In case you want to ever remove special or temporary items
    private void RemoveStaleInventoryItems(PlayerEconomyData economyData, List<InventoryResponse> serverItems)
    {
        var serverItemIds = serverItems.Select(i => i.InventoryItemId).ToHashSet();
        var staleItems = economyData.ItemInventory.Keys.Where(k => !serverItemIds.Contains(k)).ToList();
            
        foreach (var staleItem in staleItems)
        {
            economyData.ItemInventory.Remove(staleItem);
            m_Logger.LogInformation($"Removed stale inventory item: {staleItem}");
        }
    }
    
    #endregion
}