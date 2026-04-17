using GemHunterUGSCloud.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
namespace GemHunterUGSCloud.Services;

/// <summary>
/// Manages area-based upgradable items and player progression within game areas.
/// 
/// Note these are game area items in the player hub, NOT inventory items (inventory items are boosters used in game)
/// 
/// Core Responsibilities:
/// - (Area Upgradable) Item unlocking system using star requirements
/// - Item upgrade system using coin currency
/// - Area progression tracking based on total upgrade levels
/// - Dynamic item addition when areas are expanded
/// - Item validation and unlock/upgrade eligibility checks
/// 
/// Key Cloud Code Functions:
/// - HandleUnlock: Unlocks new items in an area (costs stars)
/// - HandleUpgrade: Upgrades existing items (costs coins)
/// 
/// Game Mechanics:
/// - Items must be unlocked before they can be upgraded
/// - Each area has a maximum number of upgradable item slots
/// - Area progress calculated as sum of all item upgrade levels
/// - Items are added dynamically when targeted for unlock
/// - Supports predefined item types (Shark, Seahorse, Submarine, Treasure, Turtle)
/// - Graceful validation with early returns for invalid requests
/// 
/// </summary>
public class UpgradableAreaItemsService
{
    private readonly ILogger<UpgradableAreaItemsService> m_Logger;
    private readonly IGameApiClient m_GameApiClient;
    private readonly PlayerDataService m_PlayerDataService;
    private readonly PlayerEconomyService m_PlayerEconomyService;

    public UpgradableAreaItemsService(
        ILogger<UpgradableAreaItemsService> logger,
        IGameApiClient gameApiClient,
        PlayerDataService playerDataService,
        PlayerEconomyService playerEconomyService)
    {
        m_Logger = logger;
        m_GameApiClient = gameApiClient;
        m_PlayerDataService = playerDataService;
        m_PlayerEconomyService = playerEconomyService;
    }
    
    [CloudCodeFunction("HandleUnlock")]
    public async Task<PlayerData> HandleUnlock(IExecutionContext context, int areaId, int itemId)
    {
        try
        {
            var playerData = await m_PlayerDataService.GetPlayerData(context);
            if (!ValidateAndGetItemForUnlock(playerData, areaId, itemId, context.PlayerId, out var areaData, out var item))
            {
                return playerData;
            }

            if (item.IsUnlocked)
            {
                m_Logger.LogWarning("Attempted to unlock already unlocked item {ItemId} for player {PlayerId}", itemId, context.PlayerId);
                return playerData;
            }

            if (playerData.Stars < areaData.UnlockRequirement_Stars)
            {
                m_Logger.LogWarning("Player {PlayerId} attempted unlock with insufficient stars: need {Required}, have {Current}", context.PlayerId, areaData.UnlockRequirement_Stars, playerData.Stars);
                return playerData; // Return current state, don't throw
            }

            UnlockAreaItem(playerData, areaData, item);
            
            playerData.CurrentArea = areaData;
            UpdateCurrentAreaProgress(areaData);
            
            await m_PlayerDataService.SaveAllPlayerData(context, playerData);
            return playerData;
        }
        catch (Exception e)
        {
            m_Logger.LogError(e, $"Error in HandleUnlock: {e.Message}");
            throw;
        }
    }

    private bool ValidateAndGetItemForUnlock(PlayerData playerData, int areaId, int itemId, string playerId,
    out AreaData areaData, out UpgradableAreaItem item)
    {
        areaData = null;
        item = null;

        if (playerData == null)
        {
            m_Logger.LogError("Player data not found for player {PlayerId}", playerId);
            throw new InvalidOperationException("Player data not found");
        }

        if (!TryGetAreaData(playerData, areaId, out areaData))
        {
            m_Logger.LogWarning("Player {PlayerId} attempted to access invalid area {AreaId}", playerId, areaId);
            return false;
        }

        EnsureUpgradableAreaItemExists(areaData, itemId);

        if (!TryGetUpgradableItem(areaData, itemId, out item))
        {
            m_Logger.LogWarning("Player {PlayerId} attempted to unlock invalid item {ItemId} in area {AreaId}",
                playerId, itemId, areaId);
            return false;
        }

        return true;
    }

    private void UnlockAreaItem(PlayerData playerData, AreaData areaData, UpgradableAreaItem item)
    {
        item.IsUnlocked = true;
        playerData.Stars -= areaData.UnlockRequirement_Stars;
    }

    [CloudCodeFunction("HandleUpgrade")]
    public async Task<PlayerData> HandleUpgrade(IExecutionContext context, int areaId, int itemId)
    {
        try
        {
            var economyData = await m_PlayerEconomyService.GetPlayerEconomyData(context);
            var playerData = await m_PlayerDataService.GetPlayerData(context);

            if (!ValidateAndGetItemForUpgrade(playerData, economyData, areaId, itemId, context.PlayerId, out var areaData, out var item))
            {
                return playerData;
            }

            // Validate business rules
            if (!item.IsUnlocked)
            {
                m_Logger.LogWarning("Player {PlayerId} attempted to upgrade locked item {ItemId}",
                    context.PlayerId, itemId);
                return playerData;
            }

            if (item.CurrentLevel >= item.MaxLevel)
            {
                m_Logger.LogWarning("Player {PlayerId} attempted to upgrade max level item {ItemId}",
                    context.PlayerId, itemId);
                return playerData;
            }

            int upgradeCost = item.PerLevelCoinUpgradeRequirement;
            if (!CanPlayerAffordUpgrade(economyData, upgradeCost))
            {
                m_Logger.LogWarning("Player {PlayerId} attempted upgrade with insufficient coins: need {Cost}, have {Current}",
                    context.PlayerId, upgradeCost,
                    economyData.Currencies.GetValueOrDefault(EconomyConstants.Currencies.k_Coin, 0));
                return playerData;
            }

            await UpgradeAreaItem(context, economyData, item, upgradeCost);
            
            playerData.CurrentArea = areaData;
            UpdateCurrentAreaProgress(areaData);
            
            await m_PlayerDataService.SaveAllPlayerData(context, playerData);
            return playerData;
        }
        catch (Exception e)
        {
            m_Logger.LogError(e, $"Error in HandleUpgrade: {e.Message}");
            throw;
        }
    }

    private bool ValidateAndGetItemForUpgrade(PlayerData playerData, PlayerEconomyData economyData,
    int areaId, int itemId, string playerId, out AreaData areaData, out UpgradableAreaItem item)
    {
        areaData = null;
        item = null;

        if (playerData == null)
        {
            m_Logger.LogError("Player data not found for player {PlayerId}", playerId);
            throw new InvalidOperationException("Player data not found");
        }

        if (economyData == null)
        {
            m_Logger.LogError("Economy data not found for player {PlayerId}", playerId);
            throw new InvalidOperationException("Economy data not found");
        }

        if (!TryGetAreaData(playerData, areaId, out areaData))
        {
            m_Logger.LogWarning("Player {PlayerId} attempted to access invalid area {AreaId}", playerId, areaId);
            return false;
        }

        if (!TryGetUpgradableItem(areaData, itemId, out item))
        {
            m_Logger.LogWarning("Player {PlayerId} attempted to upgrade invalid item {ItemId} in area {AreaId}",
                playerId, itemId, areaId);
            return false;
        }

        return true;
    }

    private bool TryGetAreaData(PlayerData playerData, int areaId, out AreaData areaData)
    {
        areaData = playerData.GameAreasData.Find(area => area.AreaLevel == areaId);
        return areaData != null;
    }

    private bool TryGetUpgradableItem(AreaData areaData, int itemId, out UpgradableAreaItem item)
    {
        item = areaData.UpgradableAreaItems.Find(i => i.UpgradableId == itemId);
        return item != null;
    }

    private bool CanPlayerAffordUpgrade(PlayerEconomyData economyData, int cost)
    {
        return economyData.Currencies.ContainsKey(EconomyConstants.Currencies.k_Coin) &&
               economyData.Currencies[EconomyConstants.Currencies.k_Coin] >= cost;
    }

    private async Task UpgradeAreaItem(IExecutionContext context, PlayerEconomyData economyData, UpgradableAreaItem item, int upgradeCost)
    {
        item.CurrentLevel++;
        await m_PlayerEconomyService.UpdatePlayerCurrency(context, EconomyConstants.Currencies.k_Coin, -upgradeCost);

        m_Logger.LogInformation("Successfully upgraded {ItemName} to level {Level} for player {PlayerId}",
    item.UpgradableName, item.CurrentLevel, context.PlayerId);
    }

    private void UpdateCurrentAreaProgress(AreaData areaData)
    {
        var newProgress = areaData.UpgradableAreaItems.Sum(i => i.IsUnlocked ? i.CurrentLevel : 0);
        areaData.CurrentProgress = newProgress;
    }

    private void EnsureUpgradableAreaItemExists(AreaData areaData, int targetItemId)
    {
        if (areaData.UpgradableAreaItems.Count >= areaData.TotalUpgradableSlots)
        {
            m_Logger.LogInformation("Max upgradable items reached");
            return;
        }
        
        var existingItem = areaData.UpgradableAreaItems
            .FirstOrDefault(i => i.UpgradableId == targetItemId);
        
        if (existingItem == null)
        {
            AddUpgradableAreaItem(areaData, targetItemId);
            
            // order the list by ID for consistency
            areaData.UpgradableAreaItems = areaData.UpgradableAreaItems
                .OrderBy(i => i.UpgradableId)
                .ToList();
        }
    }
    
    private void AddUpgradableAreaItem(AreaData areaData, int targetItemId)
    {
        var itemData = targetItemId switch
        {
            1 => (name: "Shark", cost: 20),
            2 => (name: "Seahorse", cost: 40),
            3 => (name: "Submarine", cost: 60),
            4 => (name: "Treasure", cost: 80),
            5 => (name: "Turtle", cost: 100),
            _ => (name: null, cost: 30)
        };
        
        if (itemData.name != null)
        {
            var newItem = new UpgradableAreaItem
            {
                UpgradableName = itemData.name,
                UpgradableId = targetItemId,
                IsUnlocked = false,
                CurrentLevel = 0,
                MaxLevel = 5,
                PerLevelCoinUpgradeRequirement = itemData.cost
            };
            areaData.UpgradableAreaItems.Add(newItem);
        }
    }
}