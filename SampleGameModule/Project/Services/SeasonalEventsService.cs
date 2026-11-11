using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SampleGameModule.Infrastructure;
using SampleGameModule.Models;
using Unity.Services.CloudCode.Core;

namespace SampleGameModule.Services;

/// <summary>
/// Seasonal Events sample: the active season comes from Remote Config Game Overrides keyed on the
/// current minute, and each season's challenge reward can be claimed once per iteration.
/// Used by: Seasonal Events, CloudCodeManager.CallGetServerEpochTimeEndpoint and CallGrantEventRewardEndpoint.
/// </summary>
public class SeasonalEventsService
{
    const string k_LastCompletedEventKey = "SEASONAL_EVENTS_LAST_COMPLETED_EVENT";
    const string k_LastCompletedEventTimestampKey = "SEASONAL_EVENTS_LAST_COMPLETED_EVENT_TIMESTAMP";
    const string k_EventKeyConfigKey = "EVENT_KEY";
    const string k_EventDurationConfigKey = "EVENT_TOTAL_DURATION_MINUTES";
    const string k_ChallengeRewardConfigKey = "SEASONAL_EVENTS_CHALLENGE_REWARD";
    const long k_MillisecondsInAMinute = 60000;

    readonly EconomyModuleService m_Economy;
    readonly PlayerDataStore m_Store;
    readonly RemoteConfigReader m_Config;

    public SeasonalEventsService(EconomyModuleService economy, PlayerDataStore store, RemoteConfigReader config)
    {
        m_Economy = economy;
        m_Store = store;
        m_Config = config;
    }

    public class RewardDetail
    {
        [JsonProperty("id")] public string? Id { get; set; }
        [JsonProperty("quantity")] public long? Quantity { get; set; }
        [JsonProperty("spriteAddress")] public string? SpriteAddress { get; set; }
    }

    public class ChallengeReward
    {
        [JsonProperty("rewards")] public List<RewardDetail>? Rewards { get; set; }
    }

    public class GrantEventRewardResult
    {
        [JsonProperty("grantedRewards")] public List<RewardDetail> GrantedRewards { get; set; } = new();
        [JsonProperty("eventKey")] public string EventKey { get; set; } = "";
        [JsonProperty("timestamp")] public long Timestamp { get; set; }
        [JsonProperty("timestampMinutes")] public int TimestampMinutes { get; set; }
    }

    /// <summary>
    /// Used by: Seasonal Events sample, CloudCodeManager.CallGetServerEpochTimeEndpoint.
    /// Returns the server's current Unix time in milliseconds.
    /// </summary>
    [CloudCodeFunction("SeasonalEvents_GetServerTime")]
    public Task<long> GetServerTime(IExecutionContext context)
    {
        return Task.FromResult(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    /// <summary>
    /// Used by: Seasonal Events sample, CloudCodeManager.CallGrantEventRewardEndpoint.
    /// Grants the active season's challenge rewards once per season iteration and records the claim.
    /// </summary>
    [CloudCodeFunction("SeasonalEvents_GrantEventReward")]
    public async Task<GrantEventRewardResult> GrantEventReward(IExecutionContext context)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var timestampMinutes = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).Minute.ToString("00");

        var config = await m_Config.GetAsync(context,
            new[] { k_EventKeyConfigKey, k_EventDurationConfigKey, k_ChallengeRewardConfigKey },
            new Dictionary<string, string> { { "timestampMinutes", timestampMinutes } });
        var saved = await m_Store.GetManyAsync(context, new[] { k_LastCompletedEventKey, k_LastCompletedEventTimestampKey });

        var activeEventKey = config.GetString(k_EventKeyConfigKey);
        var activeEventDurationMinutes = config.GetLong(k_EventDurationConfigKey);
        var lastCompletedEvent = saved[k_LastCompletedEventKey].As<string>() ?? "";
        var lastCompletedEventTimestamp = saved[k_LastCompletedEventTimestampKey].Exists
            ? saved[k_LastCompletedEventTimestampKey].As<long>()
            : 0;

        ThrowIfRewardDistributionNotAllowed(activeEventKey, activeEventDurationMinutes,
            lastCompletedEvent, lastCompletedEventTimestamp, timestamp);

        var grantedRewards = await GrantRewards(context, config);

        await m_Store.SetManyAsync(context, new Dictionary<string, object>
        {
            { k_LastCompletedEventKey, activeEventKey },
            { k_LastCompletedEventTimestampKey, timestamp },
        });

        return new GrantEventRewardResult
        {
            GrantedRewards = grantedRewards,
            EventKey = activeEventKey,
            Timestamp = timestamp,
            TimestampMinutes = int.Parse(timestampMinutes),
        };
    }

    static void ThrowIfRewardDistributionNotAllowed(string activeEventKey, long activeEventDurationMinutes,
        string lastCompletedEvent, long lastCompletedEventTimestamp, long currentTime)
    {
        // Because the seasonal events repeat and do not have unique keys for each iteration, we first check whether the
        // current season's key is the same as the key of the season that was active the last time the event was completed.
        if (activeEventKey != lastCompletedEvent)
            return;

        // Because the key of the season that was active the last time the event was completed is the same as the
        // current season's key, we now need to check whether the timestamp of the last time the event was completed
        // is so old that it couldn't possibly be from the current iteration of this season.
        //
        // We do these cyclical seasons for ease of demonstration in the sample project, however in a real world
        // implementation (where seasonal events last longer than a few minutes) you would likely create a new
        // override in remote config each time an event period was starting.
        var eventDurationMilliseconds = activeEventDurationMinutes * k_MillisecondsInAMinute;
        var earliestPotentialStartForActiveEvent = currentTime - eventDurationMilliseconds;

        // Greater than ( > ) when talking about timestamps means more recent
        if (lastCompletedEventTimestamp >= earliestPotentialStartForActiveEvent)
        {
            throw new ModuleError(2, "InvalidRewardDistributionAttemptError",
                "The rewards for this season have already been claimed.");
        }
    }

    /// <summary>Grants every reward in one economy write and reports each currency's post-grant balance under "quantity".</summary>
    async Task<List<RewardDetail>> GrantRewards(IExecutionContext context, ConfigValues config)
    {
        var rewards = config.GetJson(k_ChallengeRewardConfigKey)?.ToObject<ChallengeReward>()?.Rewards
            ?? new List<RewardDetail>();
        var grantable = rewards.Where(r => r.Id != null && r.Quantity != null).ToList();

        var snapshot = await m_Economy.GrantAsync(context,
            grantable.Select(r => new KeyValuePair<string, long>(r.Id!, r.Quantity!.Value)));

        return grantable.Select(r => new RewardDetail
        {
            Id = r.Id,
            Quantity = snapshot.Wallet[r.Id!],
            SpriteAddress = r.SpriteAddress,
        }).ToList();
    }
}
