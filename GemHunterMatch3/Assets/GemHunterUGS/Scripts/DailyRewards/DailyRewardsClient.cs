using System;
using System.Threading.Tasks;
using GemHunterUGS.Scripts.Core;
using GemHunterUGS.Scripts.PlayerDataManagement;
using GemHunterUGS.Scripts.PlayerEconomyManagement;
using Unity.Services.CloudCode.GeneratedBindings.GemHunterUGSCloud.Models;
using UnityEngine;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;

namespace GemHunterUGS.Scripts.DailyRewards
{
    /// <summary>
    /// Handles cloud communication for daily rewards system, managing reward status checks
    /// and claim validations through Cloud Code.
    /// </summary>
    public class DailyRewardsClient : MonoBehaviour
    {
        private PlayerDataManagerClient m_PlayerDataManagerClient;
        private PlayerEconomyManagerClient m_PlayerEconomyManagerClient;
        private CloudBindingsProvider m_BindingsProvider;
        private DailyRewardsManager m_DailyRewardsManager;
        
        private DailyRewardsUIController m_DailyRewardsUIController;

        /// <summary>
        /// Fired when new daily rewards status is received from Cloud Code
        /// </summary>
        public event Action<DailyRewardsResult> FetchedDailyRewardsStatus;
        
        private void Start()
        {
            InitializeDependencies();
            SetupEventHandlers();
            
            // Check if we can fetch status immediately
            if (m_PlayerDataManagerClient.IsPlayerInitializedInCloud)
            {
                StartGetStatus();
            }
        }

        private void InitializeDependencies()
        {
            m_PlayerDataManagerClient = GameSystemLocator.Get<PlayerDataManagerClient>();
            m_PlayerEconomyManagerClient = GameSystemLocator.Get<PlayerEconomyManagerClient>();
            m_BindingsProvider = GameSystemLocator.Get<CloudBindingsProvider>();
            
            m_DailyRewardsManager = m_DailyRewardsManager ?? GetComponent<DailyRewardsManager>();
            m_DailyRewardsUIController = m_DailyRewardsUIController ?? GetComponent<DailyRewardsUIController>();
        }

        private void SetupEventHandlers()
        {
            m_PlayerDataManagerClient.PlayerInitialized += StartGetStatus;
            m_DailyRewardsUIController.OpeningDailyRewardMenu += StartGetStatus;
            m_DailyRewardsManager.ClaimedDailyReward += HandleClaimDailyRewardCloud;
        }
        
        private async void StartGetStatus()
        {
            await GetDailyRewardsStatus();
        }

        private async Task GetDailyRewardsStatus()
        {
            try
            {
                var status = await m_BindingsProvider.GemHunterBindings.GetDailyRewardsStatus();
                FetchedDailyRewardsStatus?.Invoke(status);
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to get daily rewards status: {e.Message}");
            }
        }

        private async void HandleClaimDailyRewardCloud(DailyRewardClaimEventArgs args)
        {
            try 
            {
                var newStatus = await m_BindingsProvider.GemHunterBindings.ClaimDailyReward();
                FetchedDailyRewardsStatus?.Invoke(newStatus);
                m_PlayerEconomyManagerClient.SyncEconomyData();
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to claim daily reward: {e.Message}");
            }
        }

        private void OnDisable()
        {
            // Prevents unnecessary errors if PlayerHub scene is loaded first
            if (m_PlayerDataManagerClient == null)
            {
                return;
            }
            
            m_DailyRewardsUIController.OpeningDailyRewardMenu -= StartGetStatus;
            m_PlayerDataManagerClient.PlayerInitialized -= StartGetStatus;
            m_DailyRewardsManager.ClaimedDailyReward -= HandleClaimDailyRewardCloud;
        }
    }
}
