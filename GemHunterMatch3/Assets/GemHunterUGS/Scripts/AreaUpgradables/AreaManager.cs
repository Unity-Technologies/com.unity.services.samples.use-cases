using System;
using System.Linq;
using GemHunterUGS.Scripts.PlayerDataManagement;
using GemHunterUGS.Scripts.PlayerEconomyManagement;
using Unity.Services.CloudCode.GeneratedBindings.GemHunterUGSCloud.Models;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;
namespace GemHunterUGS.Scripts.AreaUpgradables
{
    /// <summary>
    /// Manages upgradable items within game areas, handling unlocking, upgrading,
    /// and progression tracking of area-specific items.
    /// </summary>
    public class AreaManager : IDisposable
    {
        private readonly PlayerDataManager m_PlayerDataManager;
        private readonly PlayerEconomyManager m_PlayerEconomyManager;

        private AreaData m_CurrentAreaData { get; set; }
        public const int k_DefaultMaxLevel = 5;
        
        public event Action AreaCompleteLocal;
        public event Action<string> AreaActionFailed;

        private bool m_IsAreaComplete = false;
        
        public AreaManager(PlayerDataManager playerDataManager, PlayerEconomyManager playerEconomyManager)
        {
            m_PlayerDataManager = playerDataManager;
            m_PlayerEconomyManager = playerEconomyManager;
            
            m_PlayerDataManager.LocalPlayerDataUpdated += UpdateAreaData;
        }

        private void UpdateAreaData(PlayerData playerData)
        {
            if (!ValidatePlayerData(playerData)) return;
            
            int currentLevel = playerData.CurrentArea.AreaLevel;
            
            if (!ValidateAreaLevel(currentLevel, playerData.GameAreasData.Count)) return;
            
            m_CurrentAreaData = playerData.CurrentArea;
            CheckForAreaCompletion(playerData);
        }
        
        public bool IsItemUnlocked(int upgradableId)
        {
            var playerData = m_PlayerDataManager.PlayerDataLocal;
            var area = playerData.CurrentArea;
            var item = area.UpgradableAreaItems.FirstOrDefault(item => item.UpgradableId == upgradableId);
            
            if (item == null)
            {
                // Items have not been created in the cloud yet, so a null item is technically unlocked
                return false;
            }
            
            return item.IsUnlocked;
        }
        
        public bool CanPlayerAffordUpgrade(int upgradableId)
        {
            var item = GetUpgradableItem(upgradableId);
            if (item == null || !item.IsUnlocked || item.CurrentLevel >= item.MaxLevel)
            {
                return false;
            }
    
            int upgradeCost = item.PerLevelCoinUpgradeRequirement;
            int playerCoins = m_PlayerEconomyManager.GetCurrencyBalance(PlayerEconomyManager.k_Coin);
    
            return playerCoins >= upgradeCost;
        }

        public bool CanPlayerAffordUnlock()
        {
            var playerData = m_PlayerDataManager.PlayerDataLocal;
            
            if (playerData == null || m_CurrentAreaData == null)
            {
                Logger.LogWarning("Cannot check unlock affordability - missing data");
                return false;
            }
            
            return playerData.Stars >= m_CurrentAreaData.UnlockRequirement_Stars;
        }
        
        private UpgradableAreaItem GetUpgradableItem(int upgradableId)
        {
            var playerData = m_PlayerDataManager.PlayerDataLocal;
            return playerData.CurrentArea.UpgradableAreaItems
                .FirstOrDefault(item => item.UpgradableId == upgradableId);
        }

        private void DeductStarsLocal(int amount)
        {
            m_PlayerDataManager.ModifyStars(-amount);
        }
        
        #region Item Actions
        
        public bool UnlockItem(int upgradableId)
        {
            if (!CanPlayerAffordUnlock())
            {
                AreaActionFailed?.Invoke($"Not enough stars for item {upgradableId}! Complete more levels.");
                return false;
            }
    
            DeductStarsLocal(m_CurrentAreaData.UnlockRequirement_Stars);
            return true;
        }
        
        public bool UpgradeItem(int upgradableId)
        {
            var playerData = m_PlayerDataManager.PlayerDataLocal;
            var itemToUpgrade = playerData.CurrentArea.UpgradableAreaItems.FirstOrDefault(item => item.UpgradableId == upgradableId);
            
            if (itemToUpgrade == null || !itemToUpgrade.IsUnlocked)
            {
                Logger.LogWarning($"Cannot upgrade item. Item null: {itemToUpgrade == null}, Is Unlocked: {itemToUpgrade?.IsUnlocked}");
                return false;
            }
            
            Logger.LogVerbose($"Trying to update {itemToUpgrade.UpgradableName} to {itemToUpgrade.CurrentLevel+1}");

            if (itemToUpgrade.CurrentLevel >= itemToUpgrade.MaxLevel)
            {
                Logger.LogWarning($"Item {itemToUpgrade.UpgradableName} is already at max level.");
                return false;
            }
            
            int upgradeCost = itemToUpgrade.PerLevelCoinUpgradeRequirement;
            if (!m_PlayerEconomyManager.TryDeductCurrencyLocal(PlayerEconomyManager.k_Coin, upgradeCost))
            {
                AreaActionFailed?.Invoke("Not enough coins to upgrade!");
                return false;
            }
            
            // Note: we are waiting for Cloud Code to apply the upgrade, but upgrades could be applied locally
            // itemToUpgrade.CurrentLevel++;
            
            UpdateCurrentAreaProgress(playerData);
            UpdateGameAreasCollection(playerData);
            
            return true;
        }
        
        #endregion
        
        private void UpdateCurrentAreaProgress(PlayerData playerData)
        {
            var area = playerData.CurrentArea;
            area.CurrentProgress = area.UpgradableAreaItems.Sum(item => item.IsUnlocked ? item.CurrentLevel : 0);
            CheckForAreaCompletion(playerData);
        }
        
        private void CheckForAreaCompletion(PlayerData playerData)
        {
            Utilities.Logger.LogVerbose($"Checking for area completion for current progress {playerData.CurrentArea.CurrentProgress}");
            if (m_IsAreaComplete)
            {
                Logger.LogWarning("Area is already complete!");
                return;
            }
            var area = playerData.CurrentArea;
            bool allMaxLevel = area.CurrentProgress == area.MaxProgress;
            
            if (allMaxLevel)
            {
                m_IsAreaComplete = true;
                Logger.LogDemo("Completed Area locally!");
                AreaCompleteLocal?.Invoke();
            }
        }
        
        private void UpdateGameAreasCollection(PlayerData playerData)
        {
            if (!ValidatePlayerData(playerData)) return;
            var currentArea = playerData.CurrentArea;

            int areaIndex = currentArea.AreaLevel - 1;
            if (areaIndex < 0 || areaIndex >= playerData.GameAreasData.Count)
            {
                Logger.LogError($"Cannot update game areas data: invalid index {areaIndex}");
                return;
            }

            playerData.GameAreasData[areaIndex] = currentArea;
        }
        
        private bool ValidatePlayerData(PlayerData playerData)
        {
            if (playerData == null)
            {
                Logger.LogError("PlayerData is null in UpdateAreaData");
                return false;
            }

            if (playerData.GameAreasData == null || playerData.GameAreasData.Count == 0)
            {
                Logger.LogError($"PlayerData.GameAreasData is invalid. Null: {playerData.GameAreasData == null}, Count: {playerData.GameAreasData?.Count}");
                return false;
            }
            
            Logger.LogVerbose($"Updating area data. PlayerData: {playerData.DisplayName}, Areas: {playerData.GameAreasData?.Count ?? 0}");
            return true;
        }
        
        private bool ValidateAreaLevel(int currentLevel, int areasCount)
        {
            if (currentLevel < 0 || currentLevel > areasCount)
            {
                Logger.LogError($"Area index {currentLevel} is out of range for GameAreasData (Count: {areasCount})");
                return false;
            }
            return true;
        }
        
        public void Dispose()
        {
            m_PlayerDataManager.LocalPlayerDataUpdated -= UpdateAreaData;
        }
    }
}