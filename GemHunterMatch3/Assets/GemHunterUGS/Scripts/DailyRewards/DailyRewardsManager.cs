using System;
using System.Collections.Generic;
using GemHunterUGS.Scripts.Core;
using GemHunterUGS.Scripts.PlayerEconomyManagement;
using Unity.Services.CloudCode.GeneratedBindings.GemHunterUGSCloud.Models;
using UnityEngine;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;
namespace GemHunterUGS.Scripts.DailyRewards
{
    /// <summary>
    /// Encapsulates event data for when a daily reward is claimed.
    /// </summary>
    public class DailyRewardClaimEventArgs : EventArgs
    {
        public int RewardAmount { get; }
        public int DayIndex { get; }

        public DailyRewardClaimEventArgs(int rewardAmount, int dayIndex)
        {
            RewardAmount = rewardAmount;
            DayIndex = dayIndex;
        }
    }
    
    /// <summary>
    ///  Manages the daily rewards system in the game, handling both UI and business logic.
    /// It coordinates between the cloud service client, UI controller, and player economy system
    /// to fetch reward status, display available rewards, and process reward claims.
    /// to fetch reward status, display available rewards, and process reward claims.
    /// Key responsibilities:
    /// - Maintains local state of daily rewards
    /// - Coordinates between UI and cloud service
    /// - Processes reward claims
    /// - Updates player economy when rewards are claimed
    /// </summary>
    public class DailyRewardsManager : MonoBehaviour
    {
        [SerializeField]
        private DailyRewardsClient m_DailyRewardsClient;
        [SerializeField]
        private DailyRewardsUIController m_DailyRewardsUIController;

        public DailyRewardsResult DailyRewardsResultLocal { get; private set; }
        private PlayerEconomyManager m_PlayerEconomyManager;

        /// <summary>
        /// Fired when a daily reward is successfully claimed
        /// </summary>
        public event Action<DailyRewardClaimEventArgs> ClaimedDailyReward;
        public event Action<DailyRewardsResult> DailyRewardsResultUpdated;
        
        private void Start()
        {
            m_DailyRewardsUIController.Initialize();
            
            m_PlayerEconomyManager = GameSystemLocator.Get<PlayerEconomyManager>();

            SetupEventHandlers();

            if (DailyRewardsResultLocal != null)
            {
                DailyRewardsResultUpdated?.Invoke(DailyRewardsResultLocal);
            }
        }
        
        private void SetupEventHandlers()
        {
            m_DailyRewardsClient.FetchedDailyRewardsStatus += UpdateDailyRewardsResult;
            m_DailyRewardsUIController.ClaimDailyRewardRequested += ProcessRewardClaim;
        }

        private void UpdateDailyRewardsResult(DailyRewardsResult result)
        {
            DailyRewardsResultLocal = result;
            DailyRewardsResultUpdated?.Invoke(result);
        }
        
        public void ProcessRewardClaim()
        {
            if (DailyRewardsResultLocal?.ConfigData?.DailyRewards == null) 
            {
                Logger.LogWarning("Cannot claim reward: reward data not available");
                return;
            }

            var currentDayIndex = DailyRewardsResultLocal.DaysClaimed;
            if (currentDayIndex >= DailyRewardsResultLocal.ConfigData.DailyRewards.Count) return;

            var rewardToGrant = DailyRewardsResultLocal.ConfigData.DailyRewards[currentDayIndex];
        
            // Update local state
            ClaimRewardLocal(rewardToGrant.Quantity);
            
            ClaimedDailyReward?.Invoke(new DailyRewardClaimEventArgs(rewardToGrant.Quantity, currentDayIndex));
        }

        private void ClaimRewardLocal(int rewardAmount)
        {
            var currencies = new Dictionary<string, int>
            {
                { PlayerEconomyManager.k_Coin, rewardAmount }
            };
            
            m_PlayerEconomyManager.ApplyLocalRewards(currencies);
        }

        private void OnDisable()
        {
            m_DailyRewardsUIController.ClaimDailyRewardRequested -= ProcessRewardClaim;
            m_DailyRewardsClient.FetchedDailyRewardsStatus -= UpdateDailyRewardsResult;
        }
    }
}
