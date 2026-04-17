using GemHunterUGSCloud.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Model;

namespace GemHunterUGSCloud.Services
{
    /// <summary>
    /// DailyRewardsClaimService - Handles the claiming and distribution of daily login rewards.
    /// 
    /// Core Responsibilities:
    /// - Daily reward claim validation and processing
    /// - Player economy integration for reward distribution
    /// - Claim timing validation with grace period support
    /// - Player status persistence after successful claims
    /// 
    /// Key Cloud Code Functions:
    /// - ClaimDailyReward: Validates eligibility and processes reward claims
    /// 
    /// Claim Validation:
    /// - Grace period support (5 seconds) for network timing issues
    /// - Daily event active period validation
    /// - Sequential day progression tracking
    /// - Duplicate claim prevention
    /// - Automatic event completion detection
    /// 
    /// </summary>
    public class DailyRewardsClaimService
    {
        private const int k_ClaimGracePeriodSeconds = 5;
        
        private readonly ILogger<DailyRewardsClaimService> m_Logger;
        private readonly IGameApiClient m_GameApiClient;
        private readonly DailyRewardsStatusService m_DailyRewardsStatus;
        private readonly PlayerEconomyService m_PlayerEconomyService;

        public DailyRewardsClaimService(
            ILogger<DailyRewardsClaimService> logger,
            IGameApiClient gameApiClient,
            DailyRewardsStatusService dailyRewardsStatusService,
            PlayerEconomyService playerEconomyService)
        {
            m_Logger = logger;
            m_GameApiClient = gameApiClient;
            m_DailyRewardsStatus = dailyRewardsStatusService;
            m_PlayerEconomyService = playerEconomyService;
        }

        /// <summary>
        /// Cloud Code function that handles the daily reward claim process.
        /// Validates the claim eligibility, grants rewards, and updates player state.
        /// </summary>
        /// <returns>Updated DailyRewardsResult containing new state and claimed rewards</returns>
        [CloudCodeFunction("ClaimDailyReward")]
        public async Task<DailyRewardsResult> ClaimDailyReward(IExecutionContext context)
        {
            try
            {
                var epochTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                m_Logger.LogInformation($"Current epochTime: {epochTime}");
                
                var eventState = new RewardsClaimingState
                {
                    EpochTime = epochTime,
                    Result = new DailyRewardsResult
                    {
                        Success = false,
                        FirstVisit = false,
                        DaysRemaining = 0,
                        SecondsTillClaimable = 0,
                        SecondsTillNextDay = 0,
                        ConfigData = new ConfigData()
                    }
                };

                await m_DailyRewardsStatus.LoadPlayerStateAndConfig(context, eventState);
                m_DailyRewardsStatus.CalculateRewardStatus(eventState);

                if (eventState.Result.IsStarted && !eventState.Result.IsEnded)
                {
                    if (eventState.Result.SecondsTillClaimable <= k_ClaimGracePeriodSeconds)
                    {
                        await ClaimRewards(context, eventState);
                        await SaveUpdatedState(context, eventState);
                        return await m_DailyRewardsStatus.GetDailyRewardsStatus(context);
                    }
                    m_Logger.LogError("Daily Rewards already claimed for today.");
                    throw new InvalidOperationException("Daily Rewards already claimed for today.");
                }
                m_Logger.LogError("Daily Rewards not active when claim attempt made.");
                throw new InvalidOperationException("Daily Rewards not active when claim attempt made.");
            }
            catch (Exception error)
            {
                m_Logger.LogError($"Failed to claim daily reward: {error.Message}");
                throw;
            }
        }

        /// <summary>
        /// Processes the reward claim for the current day, updates player economy,
        /// and updates the player's claim status.
        /// </summary>
        /// <param name="context">The execution context containing player and project information</param>
        /// <param name="rewardsClaimingState">The current event state containing reward configuration and player status</param>
        private async Task ClaimRewards(IExecutionContext context, RewardsClaimingState rewardsClaimingState)
        {
            var claimDayIndex = rewardsClaimingState.PlayerStatus.DaysClaimed;

            DailyReward rewardToGrant;
            if (claimDayIndex < rewardsClaimingState.Result.ConfigData.DailyRewards.Count)
            {
                rewardToGrant = rewardsClaimingState.Result.ConfigData.DailyRewards[claimDayIndex];
                m_Logger.LogInformation($"Claiming day {claimDayIndex + 1} rewards: {JsonConvert.SerializeObject(rewardToGrant)}");
            }
            else
            {
                m_Logger.LogError("No reward available to claim");
                throw new InvalidOperationException("No reward available to claim");
            }

            try
            {
                await m_PlayerEconomyService.UpdatePlayerCurrency(
                    context,
                    rewardToGrant.Id,
                    rewardToGrant.Quantity);

                m_Logger.LogInformation($"Successfully granted {rewardToGrant.Quantity} of {rewardToGrant.Id}");

                rewardsClaimingState.PlayerStatus.DaysClaimed++;
                rewardsClaimingState.PlayerStatus.LastClaimTime = rewardsClaimingState.EpochTime;

                if (rewardsClaimingState.Result.DaysRemaining <= 0)
                {
                    rewardsClaimingState.Result.IsEnded = true;
                }

                // Store the granted reward in the result
                rewardsClaimingState.Result.RewardsGranted = new List<DailyReward> { rewardToGrant };
            }
            catch (Exception e)
            {
                m_Logger.LogError(e, $"Failed to grant reward {rewardToGrant.Id}. Amount: {rewardToGrant.Quantity}");
                throw;
            }
        }

        /// <summary>
        /// Persists the updated player status to Cloud Save after a successful reward claim.
        /// </summary>
        /// <param name="context">The execution context containing player and project information</param>
        /// <param name="rewardsClaimingState">The current event state containing updated player status</param>
        private async Task SaveUpdatedState(IExecutionContext context, RewardsClaimingState rewardsClaimingState)
        {
            m_Logger.LogInformation($"Saving updated state: {JsonConvert.SerializeObject(rewardsClaimingState.PlayerStatus)}");

            var setItemBody = new SetItemBody(
                DailyRewardsStatusService.k_PlayerStatusKey,
                rewardsClaimingState.PlayerStatus);

            await m_GameApiClient.CloudSaveData.SetProtectedItemAsync(
                context,
                context.ServiceToken,
                context.ProjectId,
                context.PlayerId,
                setItemBody);
        }
    }
}