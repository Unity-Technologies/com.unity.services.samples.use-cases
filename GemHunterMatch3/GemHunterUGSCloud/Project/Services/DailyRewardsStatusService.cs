using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using System.Threading.Tasks;
using GemHunterUGSCloud.Models;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Model;
namespace GemHunterUGSCloud.Services;

/// <summary>
/// DailyRewardsStatusService - Manages daily rewards status tracking and state calculations.
/// 
/// Note: For simplicity, initialization is separate from PlayerData and PlayerEconomy.
/// 
/// Core Responsibilities:
/// - Daily rewards status retrieval and state management
/// - Remote Config integration for reward configuration
/// - Player progress tracking and timing calculations
/// - New player initialization for daily rewards events
/// - Claim eligibility validation and cooldown management
/// 
/// Key Cloud Code Functions:
/// - GetDailyRewardsStatus: Retrieves current daily rewards state for players
/// 
/// </summary>
public class DailyRewardsStatusService
{
    private readonly ILogger<DailyRewardsStatusService> m_Logger;
    private readonly IGameApiClient m_GameApiClient;
    private readonly PlayerDataService m_PlayerDataService;
    
    // Remote Config Key
    private const string k_DailyRewardsConfigKey = "DAILY_REWARDS_CONFIG";
    
    // Cloud Save Key
    public const string k_PlayerStatusKey = "DAILY_REWARDS_STATUS";

    public DailyRewardsStatusService(ILogger<DailyRewardsStatusService> logger, IGameApiClient gameApiClient, PlayerDataService playerDataService)
    {
        m_Logger = logger;
        m_GameApiClient = gameApiClient;
        m_PlayerDataService = playerDataService;
    }
    
    /// <summary>
    /// Cloud Code function that retrieves the current state of daily rewards for a player.
    /// Creates initial state for new players or those starting a new rewards period.
    /// </summary>
    /// <param name="context">The execution context containing player and project information</param>
    /// <returns>Current state of daily rewards including claim status and available rewards</returns>
    [CloudCodeFunction("GetDailyRewardsStatus")]
    public async Task<DailyRewardsResult> GetDailyRewardsStatus(IExecutionContext context)
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
                    Success = true,
                    FirstVisit = false,
                    DaysRemaining = 0,
                    SecondsTillClaimable = 0,
                    SecondsTillNextDay = 0
                }
            };

            await LoadPlayerStateAndConfig(context, eventState);
            CalculateRewardStatus(eventState);

            eventState.Result.DaysClaimed = eventState.PlayerStatus.DaysClaimed;
            
            m_Logger.LogInformation($"Result: {JsonConvert.SerializeObject(eventState.Result)}");
            return eventState.Result;
        }
        catch (Exception error)
        {
            m_Logger.LogError($"Failed to get daily rewards status: {error.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// Loads all required state data from cloud services and initializes state for a new rewards period if needed.
    /// Combines config data, start time, and player progress into a unified state object.
    /// </summary>
    public async Task LoadPlayerStateAndConfig(IExecutionContext context, RewardsClaimingState state)
    {
        var configData = await GetRemoteConfigData(context);
        var playerStatus = await GetPlayerStatus(context);

        state.Result.ConfigData = configData;
        state.PlayerStatus = playerStatus;

        // Handle first visit or new month
        if (state.PlayerStatus == null)
        {
            state.PlayerStatus = await StartEventForPlayer(context, state.EpochTime);
            state.Result.FirstVisit = true;
        }
    }

    /// <summary>
    /// Updates the claim status and timing calculations based on player's claim history and current time.
    /// Determines if rewards can be claimed and calculates time remaining until next claim.
    /// </summary>
    public void CalculateRewardStatus(RewardsClaimingState state)
    {
        if (state.Result.ConfigData == null)
        {
            m_Logger.LogError("ConfigData is null in UpdateState");
            throw new InvalidOperationException("ConfigData is null");
        }
        
        // Calculate time elapsed since last claim
        long secondsElapsedSinceLastClaim = 0;
        if (state.PlayerStatus.LastClaimTime > 0)
        {
            secondsElapsedSinceLastClaim = (state.EpochTime - state.PlayerStatus.LastClaimTime) / 1000;
        }
        
        // Set claim eligibility and timing
        if (CanClaimReward(state, secondsElapsedSinceLastClaim))
        {
            state.Result.SecondsTillClaimable = 0;
            state.Result.IsStarted = true;
            state.Result.IsEnded = false;
        }
        else
        {
            state.Result.SecondsTillClaimable = state.Result.ConfigData.SecondsPerDay - secondsElapsedSinceLastClaim;
            state.Result.IsStarted = true;
            state.Result.IsEnded = false;
        }

        // Update progress counters
        state.Result.DaysClaimed = state.PlayerStatus.DaysClaimed;
        state.Result.SecondsTillNextDay = state.Result.SecondsTillClaimable;
        state.Result.DaysRemaining = state.Result.ConfigData.TotalDays - state.PlayerStatus.DaysClaimed;
        
        // Check if all daily rewards are complete
        if (state.Result.DaysRemaining <= 0)
        {
            state.Result.IsEnded = true;
        }

        m_Logger.LogInformation(
            $"Reward status: Current day: {state.PlayerStatus.DaysClaimed} " +
            $"Claimable in {state.Result.SecondsTillClaimable} seconds, " +
            $"Days claimed: {state.Result.DaysClaimed}, " +
            $"Days remaining: {state.Result.DaysRemaining}");
    }
    
    private bool CanClaimReward(RewardsClaimingState rewardsClaimingState, long secondsSinceLastClaim)
    {
        bool isFirstClaim = rewardsClaimingState.PlayerStatus.LastClaimTime == 0;
        bool hasWaitedLongEnough = secondsSinceLastClaim >= rewardsClaimingState.Result.ConfigData.SecondsPerDay;
        bool hasMoreRewardsAvailable = rewardsClaimingState.PlayerStatus.DaysClaimed < rewardsClaimingState.Result.ConfigData.TotalDays;

        return isFirstClaim || (hasWaitedLongEnough && hasMoreRewardsAvailable);
    }
    
    /// <summary>
    /// Retrieves the daily rewards configuration from Remote Config.
    /// Contains reward schedule, cooldown periods, and total duration.
    /// </summary>
    private async Task<ConfigData> GetRemoteConfigData(IExecutionContext context)
    {
        var response = await m_GameApiClient.RemoteConfigSettings.AssignSettingsGetAsync(
            context,
            context.AccessToken,
            context.ProjectId,
            context.EnvironmentId,
            null,
            new List<string> { k_DailyRewardsConfigKey });

        if (response?.Data?.Configs?.Settings != null && 
            response.Data.Configs.Settings.ContainsKey(k_DailyRewardsConfigKey))
        {
            var rawJson = response.Data.Configs.Settings[k_DailyRewardsConfigKey].ToString();
            
            m_Logger.LogInformation($"Raw config JSON: {rawJson}");
            
            var config = JsonConvert.DeserializeObject<ConfigData>(rawJson);
            
            m_Logger.LogInformation($"Deserialized config: First reward: {config.DailyRewards?[0]}");
            
            return config;
        }

        m_Logger.LogError("Failed to get DAILY_REWARDS_CONFIG from Remote Config");
        throw new InvalidOperationException("Failed to get DAILY_REWARDS_CONFIG");
    }
    
    private async Task<DailyRewardsPlayerStatus?> GetPlayerStatus(IExecutionContext context)
    {
        var (success, playerStatus) = await m_PlayerDataService.TryGetProtectedData<DailyRewardsPlayerStatus>(context, k_PlayerStatusKey);
        return success ? playerStatus : null;
    }
    
    public async Task<DailyRewardsPlayerStatus> StartEventForPlayer(IExecutionContext context, long currentEpochTime)
    {
        var playerStatus = new DailyRewardsPlayerStatus
        {
            StartEpochTime = currentEpochTime,
            DaysClaimed = 0,
            LastClaimTime = 0
        };

        var setItemBody = new SetItemBody(
            k_PlayerStatusKey,
            playerStatus);

        await m_GameApiClient.CloudSaveData.SetItemAsync(
            context,
            context.AccessToken,
            context.ProjectId,
            context.PlayerId,
            setItemBody);

        m_Logger.LogInformation($"New player status: {JsonConvert.SerializeObject(playerStatus)}");
        return playerStatus;
    }
}