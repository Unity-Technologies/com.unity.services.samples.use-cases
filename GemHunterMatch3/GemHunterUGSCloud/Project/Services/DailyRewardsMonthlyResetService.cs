using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudSave.Model;

namespace GemHunterUGSCloud.Services
{
    /// <summary>
    /// *** Not using this class *** but you might want to implement a reset of daily rewards (e.g. monthly)
    /// 
    /// Core Responsibilities:
    /// - Reset daily rewards event start time to current timestamp
    /// - Clear individual player progress and claim status
    /// - Parallel execution of reset operations for efficiency
    /// - Graceful handling of missing player data during cleanup
    /// 
    /// </summary>
    public class DailyRewardsMonthlyResetService
    {
        private readonly ILogger<DailyRewardsMonthlyResetService> m_Logger;
        private string k_DailyRewardsMonthStartKey = "DAILY_REWARDS_START_EPOCH_TIME";
        private readonly IGameApiClient m_GameApiClient;

        public DailyRewardsMonthlyResetService(
            ILogger<DailyRewardsMonthlyResetService> logger,
            IGameApiClient gameApiClient)
        {
            m_Logger = logger;
            m_GameApiClient = gameApiClient;
        }

        // Leaving this commented for now
        // [CloudCodeFunction("ResetDailyRewards")]
        public async Task ResetDailyRewards(IExecutionContext context)
        {
            try
            {
                var epochTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                m_Logger.LogInformation($"Current epochTime: {epochTime}");

                // Using Task.WhenAll for parallel execution
                await Task.WhenAll(
                    SetEventStartEpochTime(context, epochTime),
                    ClearPlayerStatus(context)
                );

                m_Logger.LogInformation("Successfully reset Daily Rewards event.");
            }
            catch (Exception error)
            {
                m_Logger.LogError($"Failed to reset Daily Rewards: {error.Message}");
            }
        }

        private async Task SetEventStartEpochTime(IExecutionContext context, long epochTime)
        {
            try
            {
                await m_GameApiClient.CloudSaveData.SetItemAsync(
                    context,
                    context.AccessToken,
                    context.ProjectId, 
                    context.PlayerId,
                    new SetItemBody
                    { 
                        Key = k_DailyRewardsMonthStartKey,
                        Value = epochTime.ToString()
                    });
            }
            catch (Exception e)
            {
                m_Logger.LogError($"Failed to set event start time: {e.Message}");
                throw;
            }
        }

        private async Task ClearPlayerStatus(IExecutionContext context)
        {
            try
            {
                await m_GameApiClient.CloudSaveData.DeleteItemAsync(
                    context,
                    context.AccessToken,
                    DailyRewardsStatusService.k_PlayerStatusKey,
                    context.ProjectId,
                    context.PlayerId);
            }
            catch (Exception ex)
            {
                // If the error contains "not found" in the message, it's okay
                if (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                {
                    m_Logger.LogInformation("Player record did not exist so it did not need to be deleted.");
                    return;
                }
                
                m_Logger.LogError($"Failed to clear player status: {ex.Message}");
                throw;
            }
        }
    }
}
