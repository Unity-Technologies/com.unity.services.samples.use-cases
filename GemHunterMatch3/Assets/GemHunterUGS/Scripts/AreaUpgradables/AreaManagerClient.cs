using System;
using GemHunterUGS.Scripts.Core;
using GemHunterUGS.Scripts.PlayerDataManagement;
using Unity.Services.CloudCode.GeneratedBindings.GemHunterUGSCloud.Models;
using UnityEngine;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;
namespace GemHunterUGS.Scripts.AreaUpgradables
{
    /// <summary>
    /// Handles cloud communication for area upgrades and unlocks,
    /// synchronizing area state between local and cloud data.
    /// </summary>
    public class AreaManagerClient : IDisposable
    {
        private AreaData m_CurrentAreaDataCloud;
        private readonly PlayerDataManagerClient m_PlayerDataManagerClient;
        private AreaUpgradablesUIController m_AreaUIController;
        private readonly CloudBindingsProvider m_BindingsProvider;
        
        public AreaManagerClient(PlayerDataManagerClient playerDataManagerClient, CloudBindingsProvider bindingsProvider)
        {
            m_PlayerDataManagerClient = playerDataManagerClient;
            m_BindingsProvider = bindingsProvider;
            m_PlayerDataManagerClient.PlayerDataUpdated += UpdateAreaData;
        }
        
        public void SetupEventHandlers(AreaUpgradablesUIController areaUIController)
        {
            if (m_AreaUIController != null)
            {
                // Clean up old subscriptions if they exist
                m_AreaUIController.OnUnlockClicked -= HandleAreaItemUnlock;
                m_AreaUIController.OnUpgradeClicked -= HandleAreaItemUpgrade;
            }
            m_AreaUIController = areaUIController;
            m_AreaUIController.OnUnlockClicked += HandleAreaItemUnlock;
            m_AreaUIController.OnUpgradeClicked += HandleAreaItemUpgrade;
        }
        
        private void UpdateAreaData(PlayerData playerData)
        {
            if (!ValidatePlayerAreaData(playerData))
            {
                return;
            }

            int currentLevel = playerData.CurrentArea.AreaLevel;

            if (!ValidateAreaLevel(currentLevel, playerData.GameAreasData.Count))
            {
                return;
            }

            m_CurrentAreaDataCloud = playerData.CurrentArea;
        }
        
        private bool ValidatePlayerAreaData(PlayerData playerData)
        {
            if (playerData == null)
            {
                Logger.LogWarning("PlayerData is null in UpdateAreaData");
                return false;
            }

            if (playerData.GameAreasData == null || playerData.GameAreasData.Count == 0)
            {
                Logger.LogError($"PlayerData.GameAreasData is invalid. Null: {playerData.GameAreasData == null}, Count: {playerData.GameAreasData?.Count}");
                return false;
            }

            if (playerData.CurrentArea == null)
            {
                Logger.LogError($"PlayerData.CurrentArea is null");
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
        
        private async void HandleAreaItemUnlock(int itemId)
        {
            if (m_CurrentAreaDataCloud == null)
            {
                Logger.LogError("m_CurrentAreaDataCloud is null");
                return;
            }
            try
            {
                int areaId = m_CurrentAreaDataCloud.AreaLevel;
                var updatedPlayerData = await m_BindingsProvider.GemHunterBindings.HandleUnlock(areaId, itemId);
                m_PlayerDataManagerClient.HandleCloudDataUpdate(updatedPlayerData);
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to handle area item unlock: {e.Message}");
            }
        }

        private async void HandleAreaItemUpgrade(int itemId)
        {
            if (m_CurrentAreaDataCloud == null)
            {
                Logger.LogError("m_CurrentAreaDataCloud is null");
                return;
            }
            try
            {
                Logger.LogVerbose($"Handling upgradable for areaId {m_CurrentAreaDataCloud.AreaLevel} and itemId {itemId}");
                
                int areaId = m_CurrentAreaDataCloud.AreaLevel;
                var updatedPlayerData = await m_BindingsProvider.GemHunterBindings.HandleUpgrade(areaId, itemId);
                m_PlayerDataManagerClient.HandleCloudDataUpdate(updatedPlayerData);
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to handle area item upgrade: {e.Message}");
            }
        }
        
        public void Dispose()
        {
            if (m_AreaUIController != null)
            {
                m_AreaUIController.OnUnlockClicked -= HandleAreaItemUnlock;
                m_AreaUIController.OnUpgradeClicked -= HandleAreaItemUpgrade;
            }
            
            m_PlayerDataManagerClient.PlayerDataUpdated -= UpdateAreaData;
        }
    }
}
