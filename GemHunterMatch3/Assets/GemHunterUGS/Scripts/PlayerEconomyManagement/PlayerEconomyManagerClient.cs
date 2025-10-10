using System;
using System.Threading.Tasks;
using Unity.Services.CloudCode.GeneratedBindings.GemHunterUGSCloud.Models;
using UnityEngine;
using GemHunterUGS.Scripts.Core;
using GemHunterUGS.Scripts.PlayerDataManagement;
using GemHunterUGS.Scripts.Utilities;
using Unity.Services.CloudCode;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;

namespace GemHunterUGS.Scripts.PlayerEconomyManagement
{
    /// <summary>
    /// Handles all cloud communication related to the player's economy system. This client manages initialization,
    /// synchronization, and updates of player currency and inventory data between the local game and Cloud Code functions.
    /// </summary>
    /// <remarks>
    /// Key responsibilities:
    /// - Synchronizes local economy state with cloud data
    /// - Handles cloud data updates from various game systems (store, ad rewards, etc.)
    /// - Manages economy-related event notifications
    /// </remarks>
    public class PlayerEconomyManagerClient : IDisposable
    {
        private PlayerEconomyData m_CloudPlayerEconomyData;
        private readonly PlayerDataManagerClient m_PlayerDataManagerClient;
        private readonly CloudBindingsProvider m_BindingsProvider;
        
        public bool IsInitialized { get; private set; }
        
        public event Action<PlayerEconomyData> EconomyDataUpdated;
        public event Action PlayerEconomyInitialized;
        
        public PlayerEconomyManagerClient(PlayerDataManagerClient playerDataManagerClient, CloudBindingsProvider bindingsProvider)
        {
            m_PlayerDataManagerClient = playerDataManagerClient;
            m_BindingsProvider = bindingsProvider;
            
            m_PlayerDataManagerClient.PlayerDataInitialized += HandleEconomyInitialization;
        }
        
        private void HandleEconomyInitialization(PlayerData playerData, PlayerEconomyData economyData)
        {
            if (economyData == null)
            {
                Logger.LogError("Received null economy data during initialization");
                return;
            }
            
            m_CloudPlayerEconomyData = economyData;
            IsInitialized = true;
            
            Logger.LogDemo("\u2601 \u26A1 Economy data initialized");
            
            EconomyDataUpdated?.Invoke(m_CloudPlayerEconomyData);
            PlayerEconomyInitialized?.Invoke();
        }

        public void SyncEconomyData()
        {
            FetchEconomyDataAsync();
        }
        
        private async void FetchEconomyDataAsync()
        {
            try
            {
                Logger.LogDemo("Syncing economy data...");
                
                var economyData = await m_BindingsProvider.GemHunterBindings.GetPlayerEconomyData();
                
                if (economyData == null)
                {
                    Logger.LogWarning("Received null economy data from cloud sync");
                    return;
                }
                
                Logger.LogDemo("\u2601 \u26A1 EconomyDataFetchedFromCloud");
                EconomyDataUpdated?.Invoke(economyData);
            }
            catch (CloudCodeException ex)
            {
                Logger.LogError($"CloudCode error syncing economy data: {ex.Message}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Unexpected error syncing economy data: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Other systems retrieve economy data from Cloud Code (store, ad rewards, etc.). Use this method to update the local cloud data and fire off an event with the update.
        /// </summary>
        /// <param name="updatedEconomyData"></param>
        public void HandleEconomyUpdate(PlayerEconomyData updatedEconomyData)
        {
            if (updatedEconomyData == null)
            {
                Logger.LogWarning("Received null economy data in HandleEconomyUpdate");
                return;
            }
            
            m_CloudPlayerEconomyData = updatedEconomyData;
            EconomyDataUpdated?.Invoke(m_CloudPlayerEconomyData);
        }
        
        public void Dispose()
        {
            if (m_PlayerDataManagerClient != null)
            {
                m_PlayerDataManagerClient.PlayerDataInitialized -= HandleEconomyInitialization;
            }
            
            m_CloudPlayerEconomyData = null;
        }
    }
}