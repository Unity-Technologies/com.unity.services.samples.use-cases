using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SampleGameModule.Infrastructure;
using SampleGameModule.Models;
using Unity.Services.CloudCode.Core;

namespace SampleGameModule.Services;

/// <summary>
/// Daily Rewards sample: a calendar of one claim per "day" (DAILY_REWARDS_CONFIG decides how
/// long a day is), with bonus rewards once every calendar day has been claimed.
/// Used by: Daily Rewards, CloudCodeManager.CallResetEventEndpoint / CallGetStatusEndpoint / CallClaimEndpoint.
/// </summary>
public class DailyRewardsService
{
    const string k_ConfigKey = "DAILY_REWARDS_CONFIG";

    // The event start time would NORMALLY be stored in Remote Config for all players, set to the start of the
    // month. To facilitate testing it lives in Cloud Save so each player can start the event when they first
    // enter this Use Case Sample.
    const string k_StartEpochTimeKey = "DAILY_REWARDS_START_EPOCH_TIME";
    const string k_StatusKey = "DAILY_REWARDS_STATUS";

    readonly EconomyModuleService m_Economy;
    readonly PlayerDataStore m_Store;
    readonly RemoteConfigReader m_Config;
    readonly ILogger<DailyRewardsService> m_Logger;

    public DailyRewardsService(EconomyModuleService economy, PlayerDataStore store, RemoteConfigReader config,
        ILogger<DailyRewardsService> logger)
    {
        m_Economy = economy;
        m_Store = store;
        m_Config = config;
        m_Logger = logger;
    }

    public class DailyReward
    {
        [JsonProperty("id")] public string Id { get; set; } = "";
        [JsonProperty("quantity")] public long Quantity { get; set; }
    }

    /// <summary>The DAILY_REWARDS_CONFIG Remote Config value.</summary>
    public class EventConfig
    {
        [JsonProperty("totalDays")] public long TotalDays { get; set; }
        [JsonProperty("secondsPerDay")] public long SecondsPerDay { get; set; }
        [JsonProperty("dailyRewards")] public List<List<DailyReward>> DailyRewards { get; set; } = new();
        [JsonProperty("bonusReward")] public List<DailyReward> BonusReward { get; set; } = new();
    }

    /// <summary>The DAILY_REWARDS_STATUS Cloud Save value, stored as a JSON string.</summary>
    public class PlayerStatus
    {
        [JsonProperty("startEpochTime")] public long StartEpochTime { get; set; }
        [JsonProperty("daysClaimed")] public long DaysClaimed { get; set; }
        [JsonProperty("lastClaimTime")] public long LastClaimTime { get; set; }
    }

    public class StatusResult
    {
        [JsonProperty("success")] public bool Success { get; set; }
        [JsonProperty("firstVisit")] public bool FirstVisit { get; set; }
        [JsonProperty("daysRemaining")] public long DaysRemaining { get; set; }
        [JsonProperty("secondsTillClaimable")] public double SecondsTillClaimable { get; set; }
        [JsonProperty("secondsTillNextDay")] public double SecondsTillNextDay { get; set; }
        [JsonProperty("isStarted")] public bool IsStarted { get; set; }
        [JsonProperty("isEnded")] public bool IsEnded { get; set; }
        [JsonProperty("daysClaimed")] public long DaysClaimed { get; set; }

        // The config fields ride along with the status (the script merged DAILY_REWARDS_CONFIG into its result).
        [JsonProperty("totalDays")] public long TotalDays { get; set; }
        [JsonProperty("secondsPerDay")] public long SecondsPerDay { get; set; }
        [JsonProperty("dailyRewards")] public List<List<DailyReward>> DailyRewards { get; set; } = new();
        [JsonProperty("bonusReward")] public List<DailyReward> BonusReward { get; set; } = new();
    }

    public class ClaimResult : StatusResult
    {
        [JsonProperty("rewardsGranted")] public List<DailyReward> RewardsGranted { get; set; } = new();
    }

    class EventState
    {
        public long EpochTime;
        public EventConfig Config = new();
        public long StartEpochTime;
        public PlayerStatus Status = new();
        public long EventDay;
        public long LastDayClaimed;
    }

    /// <summary>
    /// Used by: Daily Rewards sample, CloudCodeManager.CallResetEventEndpoint.
    /// Restarts the event now and forgets the player's claims, simulating a first visit to a new month.
    /// </summary>
    [CloudCodeFunction("DailyRewards_ResetEvent")]
    public async Task ResetEvent(IExecutionContext context)
    {
        var epochTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await m_Store.SetAsync(context, k_StartEpochTimeKey, epochTime);
        await m_Store.DeleteAsync(context, k_StatusKey);
        m_Logger.LogInformation("Successfully reset Daily Rewards event.");
    }

    /// <summary>
    /// Used by: Daily Rewards sample, CloudCodeManager.CallGetStatusEndpoint.
    /// Starts the event for a first-time visitor and reports where the player stands in the calendar.
    /// </summary>
    [CloudCodeFunction("DailyRewards_GetStatus")]
    public async Task<StatusResult> GetStatus(IExecutionContext context)
    {
        var result = new StatusResult { Success = true };
        var state = await ReadInitialStateAsync(context, result);
        UpdateState(state, result);

        if (result.IsStarted && !result.IsEnded)
        {
            if (state.EventDay > state.LastDayClaimed)
            {
                result.SecondsTillClaimable = 0;
                result.DaysRemaining = state.Config.TotalDays - state.EventDay;
            }

            // If the player has claimed the last day of the month, end the event now to signal that further claims are not possible.
            if (result.DaysRemaining <= 0)
                result.IsEnded = true;
        }

        result.DaysClaimed = state.Status.DaysClaimed;
        return result;
    }

    /// <summary>
    /// Used by: Daily Rewards sample, CloudCodeManager.CallClaimEndpoint.
    /// Grants today's rewards (or the bonus rewards once the calendar is exhausted) and records the claim.
    /// </summary>
    [CloudCodeFunction("DailyRewards_Claim")]
    public async Task<ClaimResult> Claim(IExecutionContext context)
    {
        var result = new ClaimResult { Success = false };
        var state = await ReadInitialStateAsync(context, result);
        UpdateState(state, result);

        if (!result.IsStarted || result.IsEnded)
            throw new ModuleError(3, "EventNotActiveError", "Daily Rewards not active when claim attempt made.");
        if (state.EventDay <= state.LastDayClaimed)
            throw new ModuleError(4, "AlreadyClaimedError", "Daily Rewards already claimed for today.");

        // Check if still rewarding normal calendar days. Once all calendar days have been claimed, the player
        // begins claiming the 'bonus rewards' for the remaining days.
        var claimDayIndex = (int)state.Status.DaysClaimed;
        result.RewardsGranted = claimDayIndex < state.Config.DailyRewards.Count
            ? state.Config.DailyRewards[claimDayIndex]
            : state.Config.BonusReward;
        m_Logger.LogInformation("Claiming day {Day} reward(s): {Rewards}", claimDayIndex + 1,
            JsonConvert.SerializeObject(result.RewardsGranted));

        await m_Economy.GrantAsync(context,
            result.RewardsGranted.Select(r => new KeyValuePair<string, long>(r.Id, r.Quantity)));

        // Update player status to reflect that today's reward has been claimed
        state.Status.DaysClaimed++;
        state.Status.LastClaimTime = state.EpochTime;

        // If the player has just claimed the last day of the month, end the event now to signal that further claims are not possible.
        if (result.DaysRemaining <= 0)
            result.IsEnded = true;

        await m_Store.SetAsync(context, k_StatusKey, JsonConvert.SerializeObject(state.Status));

        result.Success = true;
        result.DaysClaimed = state.Status.DaysClaimed;
        return result;
    }

    /// <summary>
    /// Loads config, event start and player status; starts the event (and the player's status) when either
    /// is missing or the player's status belongs to an earlier event.
    /// </summary>
    async Task<EventState> ReadInitialStateAsync(IExecutionContext context, StatusResult result)
    {
        var state = new EventState { EpochTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };

        var config = await m_Config.GetAsync(context, new[] { k_ConfigKey });
        state.Config = config.GetJson<EventConfig>(k_ConfigKey, "Failed to get DAILY_REWARDS_CONFIG.");

        var items = await m_Store.GetManyAsync(context, new[] { k_StartEpochTimeKey, k_StatusKey });
        var startItem = items[k_StartEpochTimeKey];
        if (startItem.Exists)
        {
            state.StartEpochTime = startItem.As<long>();
        }
        else
        {
            m_Logger.LogInformation("Setting start event time in Cloud Save. This would normally be set in Remote Config to the first day of the month.");
            state.StartEpochTime = state.EpochTime;
            await m_Store.SetAsync(context, k_StartEpochTimeKey, state.StartEpochTime);
        }

        var statusJson = items[k_StatusKey].As<string>();
        var status = string.IsNullOrEmpty(statusJson) ? null : JsonConvert.DeserializeObject<PlayerStatus>(statusJson);

        // Detect that this player has NOT visited the Daily Rewards feature yet OR that the event has started a new month
        if (status is null || status.StartEpochTime != state.StartEpochTime)
        {
            status = new PlayerStatus { StartEpochTime = state.StartEpochTime, DaysClaimed = 0, LastClaimTime = 0 };
            await m_Store.SetAsync(context, k_StatusKey, JsonConvert.SerializeObject(status));
            result.FirstVisit = true;
        }

        state.Status = status;
        result.TotalDays = state.Config.TotalDays;
        result.SecondsPerDay = state.Config.SecondsPerDay;
        result.DailyRewards = state.Config.DailyRewards;
        result.BonusReward = state.Config.BonusReward;
        return state;
    }

    static void UpdateState(EventState state, StatusResult result)
    {
        var config = state.Config;

        // Total event length in seconds, to check whether the event is already over.
        var eventTotalSeconds = config.TotalDays * config.SecondsPerDay;

        // Epoch times are in milliseconds; the event math is in seconds.
        var eventSecondsPassed = (state.EpochTime - state.StartEpochTime) / 1000.0;
        result.IsStarted = eventSecondsPassed >= 0;
        result.IsEnded = eventSecondsPassed >= eventTotalSeconds;

        if (!result.IsStarted || result.IsEnded)
            return;

        state.EventDay = DayIndex(state.EpochTime, state);
        state.LastDayClaimed = DayIndex(state.Status.LastClaimTime, state);

        // Seconds remaining in the current day: the next day's offset minus the seconds passed in the event so far.
        result.SecondsTillNextDay = (state.EventDay + 1) * config.SecondsPerDay - eventSecondsPassed;
        result.SecondsTillClaimable = result.SecondsTillNextDay;
        result.DaysRemaining = config.TotalDays - state.EventDay - 1;
    }

    static long DayIndex(long epochTime, EventState state)
        => (long)Math.Floor((epochTime - state.StartEpochTime) / (double)state.Config.SecondsPerDay / 1000);
}
