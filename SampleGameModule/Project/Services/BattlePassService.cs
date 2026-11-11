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
/// Battle Pass sample: season XP, ten tiers that unlock every 100 XP, free and premium
/// tier rewards, and a purchasable pass for the current season. The season comes from
/// Remote Config with the timestamp-minutes Game Override applied; progress lives in six
/// Cloud Save keys that every function reads, resets when the season changed, and writes back.
/// Used by: Battle Pass sample, CloudCodeManager.
/// </summary>
public class BattlePassService
{
    const string k_SeasonXpKey = "BATTLE_PASS_SEASON_XP";
    const string k_SeasonTierStatesKey = "BATTLE_PASS_SEASON_TIER_STATES";
    const string k_LatestSeasonActivityTimestampKey = "BATTLE_PASS_LATEST_SEASON_ACTIVITY_TIMESTAMP";
    const string k_LatestSeasonActivityEventKeyKey = "BATTLE_PASS_LATEST_SEASON_ACTIVITY_EVENT_KEY";
    const string k_PurchasedTimestampKey = "BATTLE_PASS_PURCHASED_TIMESTAMP";
    const string k_PurchasedSeasonKey = "BATTLE_PASS_PURCHASED_SEASON";

    const string k_EventKeyConfigKey = "EVENT_KEY";
    const string k_EventNameConfigKey = "EVENT_NAME";
    const string k_EventEndTimeConfigKey = "EVENT_END_TIME";
    const string k_EventTotalDurationMinutesConfigKey = "EVENT_TOTAL_DURATION_MINUTES";
    const string k_RewardsFreeConfigKey = "BATTLE_PASS_REWARDS_FREE";
    const string k_RewardsPremiumConfigKey = "BATTLE_PASS_REWARDS_PREMIUM";
    const string k_SeasonXpPerTierConfigKey = "BATTLE_PASS_SEASON_XP_PER_TIER";

    const string k_BattlePassPurchaseId = "BATTLE_PASS_PURCHASE";
    const int k_TierCount = 10;

    // Port note: the scripts hard-code 100 XP per tier; BATTLE_PASS_SEASON_XP_PER_TIER exists in
    // Remote Config (and is returned to the client) but is ignored here too, matching the scripts.
    const int k_SeasonXpNeededToUnlockTier = 100;

    const int k_TierLocked = 0;
    const int k_TierUnlocked = 1;
    const int k_TierClaimed = 2;

    const long k_MillisecondsPerMinute = 60000;

    static readonly string[] k_CloudSaveKeys =
    {
        k_SeasonXpKey, k_SeasonTierStatesKey, k_LatestSeasonActivityTimestampKey,
        k_LatestSeasonActivityEventKeyKey, k_PurchasedTimestampKey, k_PurchasedSeasonKey,
    };

    static readonly string[] k_ConfigKeys =
    {
        k_EventKeyConfigKey, k_EventNameConfigKey, k_EventEndTimeConfigKey, k_EventTotalDurationMinutesConfigKey,
        k_RewardsFreeConfigKey, k_RewardsPremiumConfigKey, k_SeasonXpPerTierConfigKey,
    };

    readonly EconomyModuleService m_Economy;
    readonly PlayerDataStore m_Store;
    readonly RemoteConfigReader m_Config;
    readonly ILogger<BattlePassService> m_Logger;

    public BattlePassService(EconomyModuleService economy, PlayerDataStore store, RemoteConfigReader config,
        ILogger<BattlePassService> logger)
    {
        m_Economy = economy;
        m_Store = store;
        m_Config = config;
        m_Logger = logger;
    }

    /// <summary>One tier reward as stored in BATTLE_PASS_REWARDS_FREE / BATTLE_PASS_REWARDS_PREMIUM.</summary>
    public class TierReward
    {
        [JsonProperty("service")] public string Service { get; set; } = "";
        [JsonProperty("id")] public string Id { get; set; } = "";
        [JsonProperty("quantity")] public long Quantity { get; set; }
        [JsonProperty("spriteAddress")] public string? SpriteAddress { get; set; }
    }

    /// <summary>The season's Remote Config values under the camelCased keys the scripts sent the client.</summary>
    public class RemoteConfigs
    {
        [JsonProperty("battlePassRewardsFree")] public List<TierReward?> BattlePassRewardsFree { get; set; } = new();
        [JsonProperty("battlePassRewardsPremium")] public List<TierReward?> BattlePassRewardsPremium { get; set; } = new();
        [JsonProperty("battlePassSeasonXpPerTier")] public long BattlePassSeasonXpPerTier { get; set; }
        [JsonProperty("eventKey")] public string EventKey { get; set; } = "";
        [JsonProperty("eventName")] public string EventName { get; set; } = "";
        [JsonProperty("eventEndTime")] public long EventEndTime { get; set; }
        [JsonProperty("eventTotalDurationMinutes")] public long EventTotalDurationMinutes { get; set; }
    }

    public class GetStateResult
    {
        [JsonProperty("seasonXp")] public int SeasonXp { get; set; }
        [JsonProperty("seasonTierStates")] public int[] SeasonTierStates { get; set; } = Array.Empty<int>();
        [JsonProperty("ownsBattlePass")] public bool OwnsBattlePass { get; set; }
        [JsonProperty("eventSecondsRemaining")] public long EventSecondsRemaining { get; set; }
        [JsonProperty("remoteConfigs")] public RemoteConfigs RemoteConfigs { get; set; } = new();
    }

    public class GainSeasonXpResult
    {
        [JsonProperty("unlockedNewTier")] public int UnlockedNewTier { get; set; } = -1;
        [JsonProperty("seasonXp")] public int SeasonXp { get; set; }
        [JsonProperty("seasonTierStates")] public int[] SeasonTierStates { get; set; } = Array.Empty<int>();
    }

    public class ClaimTierResult
    {
        [JsonProperty("grantedRewards")] public List<TierReward> GrantedRewards { get; set; } = new();
        [JsonProperty("seasonTierStates")] public int[] SeasonTierStates { get; set; } = Array.Empty<int>();
    }

    public class PurchaseBattlePassResult
    {
        [JsonProperty("grantedRewards")] public List<TierReward> GrantedRewards { get; set; } = new();
        [JsonProperty("seasonTierStates")] public int[] SeasonTierStates { get; set; } = Array.Empty<int>();
    }

    /// <summary>
    /// Used by: Battle Pass sample, CloudCodeManager.CallGetProgressEndpoint.
    /// Returns the player's season progress (reset if the season changed) together with the
    /// season configuration and how many seconds the current event has left.
    /// </summary>
    [CloudCodeFunction("BattlePass_GetState")]
    public async Task<GetStateResult> BattlePass_GetState(IExecutionContext context)
    {
        var (config, state, timestamp) = await LoadSeasonAsync(context);
        await SaveAsync(context, config, state, timestamp);
        return new GetStateResult
        {
            SeasonXp = state.SeasonXp,
            SeasonTierStates = state.SeasonTierStates,
            OwnsBattlePass = OwnsBattlePass(config, state),
            EventSecondsRemaining = GetEventSecondsRemaining(config, timestamp),
            RemoteConfigs = MakeClientFriendlyRemoteConfigs(config),
        };
    }

    /// <summary>
    /// Used by: Battle Pass sample, CloudCodeManager.CallGainSeasonXpEndpoint.
    /// Adds season XP and unlocks every tier the new total reaches; unlockedNewTier is the
    /// highest tier index reached, or -1 when nothing new was unlocked.
    /// </summary>
    [CloudCodeFunction("BattlePass_GainSeasonXP")]
    public async Task<GainSeasonXpResult> BattlePass_GainSeasonXP(IExecutionContext context, int amount)
    {
        var (config, state, timestamp) = await LoadSeasonAsync(context);

        if (amount < 1)
            throw new ModuleError(0, "Error", "XP increase amount cannot be less than 1.");

        state.SeasonXp += amount;
        var unlockedNewTier = UnlockNewTiersIfEligible(state);

        await SaveAsync(context, config, state, timestamp);
        return new GainSeasonXpResult
        {
            UnlockedNewTier = unlockedNewTier,
            SeasonXp = state.SeasonXp,
            SeasonTierStates = state.SeasonTierStates,
        };
    }

    /// <summary>
    /// Used by: Battle Pass sample, CloudCodeManager.CallClaimTierEndpoint.
    /// Grants the tier's free reward, plus its premium reward when the player owns the pass,
    /// and marks the tier claimed.
    /// </summary>
    [CloudCodeFunction("BattlePass_ClaimTier")]
    public async Task<ClaimTierResult> BattlePass_ClaimTier(IExecutionContext context, int tierIndex)
    {
        var (config, state, timestamp) = await LoadSeasonAsync(context);

        ValidateClaim(state, tierIndex);
        var rewards = GetTierRewards(config, state, tierIndex);
        await GrantRewardsAsync(context, rewards);
        state.SeasonTierStates[tierIndex] = k_TierClaimed;

        await SaveAsync(context, config, state, timestamp);
        return new ClaimTierResult { GrantedRewards = rewards, SeasonTierStates = state.SeasonTierStates };
    }

    /// <summary>
    /// Used by: Battle Pass sample, CloudCodeManager.CallPurchaseBattlePassEndpoint.
    /// Buys the pass for the current season through the BATTLE_PASS_PURCHASE virtual purchase,
    /// then grants the premium rewards of every tier the player already claimed.
    /// </summary>
    [CloudCodeFunction("BattlePass_PurchaseBattlePass")]
    public async Task<PurchaseBattlePassResult> BattlePass_PurchaseBattlePass(IExecutionContext context)
    {
        var (config, state, timestamp) = await LoadSeasonAsync(context);

        if (OwnsBattlePass(config, state))
            throw new ModuleError(0, "Error", "Cannot purchase Battle Pass because you already own it for the current season.");

        // An unaffordable purchase throws the EconomyError untouched; nothing below runs.
        await m_Economy.MakeVirtualPurchasesAsync(context, new[] { k_BattlePassPurchaseId });
        state.PurchasedTimestamp = timestamp;
        state.PurchasedSeason = config.GetString(k_EventKeyConfigKey);

        var rewards = GetPastClaimedPremiumRewards(config, state);
        await GrantRewardsAsync(context, rewards);

        // This should only be executed if the purchase was a success.
        await SaveAsync(context, config, state, timestamp);
        return new PurchaseBattlePassResult { GrantedRewards = rewards, SeasonTierStates = state.SeasonTierStates };
    }

    class PlayerState
    {
        public int SeasonXp;
        public int[] SeasonTierStates = DefaultTierStates();
        public long LatestSeasonActivityTimestamp;
        public string LatestSeasonActivityEventKey = "";
        public long PurchasedTimestamp;
        public string PurchasedSeason = "";

        // Port note: the scripts shared one mutable default array across resets; a reset here
        // always gets a fresh copy.
        public static int[] DefaultTierStates()
        {
            var states = new int[k_TierCount];
            states[0] = k_TierUnlocked;
            return states;
        }
    }

    async Task<(ConfigValues config, PlayerState state, long timestamp)> LoadSeasonAsync(IExecutionContext context)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var config = await GetSeasonConfigAsync(context, timestamp);
        var state = await GetPlayerStateAsync(context);

        if (ShouldResetBattlePassProgress(config, state, timestamp))
        {
            m_Logger.LogInformation("Resetting Battle Pass progress for player {PlayerId}: season is now {EventKey}",
                context.PlayerId, config.GetString(k_EventKeyConfigKey));
            state = new PlayerState();
        }

        return (config, state, timestamp);
    }

    /// <summary>
    /// The current season configuration. The current timestamp's minutes are sent as a user
    /// attribute so Remote Config applies the matching season Game Override; the returned
    /// configuration contains all the tier rewards for the current season.
    /// </summary>
    Task<ConfigValues> GetSeasonConfigAsync(IExecutionContext context, long timestamp)
    {
        var timestampMinutes = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).Minute.ToString("00");
        return m_Config.GetAsync(context, k_ConfigKeys,
            new Dictionary<string, string> { { "timestampMinutes", timestampMinutes } });
    }

    async Task<PlayerState> GetPlayerStateAsync(IExecutionContext context)
    {
        var items = await m_Store.GetManyAsync(context, k_CloudSaveKeys);
        var state = new PlayerState();
        if (items[k_SeasonXpKey].Exists)
            state.SeasonXp = items[k_SeasonXpKey].As<int>();
        if (items[k_SeasonTierStatesKey].Exists)
            state.SeasonTierStates = items[k_SeasonTierStatesKey].As<int[]>() ?? PlayerState.DefaultTierStates();
        if (items[k_LatestSeasonActivityTimestampKey].Exists)
            state.LatestSeasonActivityTimestamp = items[k_LatestSeasonActivityTimestampKey].As<long>();
        if (items[k_LatestSeasonActivityEventKeyKey].Exists)
            state.LatestSeasonActivityEventKey = items[k_LatestSeasonActivityEventKeyKey].As<string>() ?? "";
        if (items[k_PurchasedTimestampKey].Exists)
            state.PurchasedTimestamp = items[k_PurchasedTimestampKey].As<long>();
        if (items[k_PurchasedSeasonKey].Exists)
            state.PurchasedSeason = items[k_PurchasedSeasonKey].As<string>() ?? "";
        return state;
    }

    static bool ShouldResetBattlePassProgress(ConfigValues config, PlayerState state, long timestamp)
    {
        // Because the seasonal events repeat and do not have unique keys for each iteration, we first check whether the
        // current season's key is the same as the key of the season that was active the last time the event was completed.
        if (config.GetString(k_EventKeyConfigKey) != state.LatestSeasonActivityEventKey)
            return true;

        // Because the key of the season that was active the last time the event was completed is the same as the
        // current season's key, we now need to check whether the timestamp of the last time the event was completed
        // is so old that it couldn't possibly be from the current iteration of this season.
        //
        // We do these cyclical seasons for ease of demonstration in the sample project, however in a real world
        // implementation (where seasonal events last longer than a few minutes) you would likely create a new
        // override in remote config each time an event period was starting.
        //
        // Port note: ClaimTier and PurchaseBattlePass never requested EVENT_TOTAL_DURATION_MINUTES from Remote
        // Config, so this time-based reset never fired there; every function reads it here.
        var eventDurationMilliseconds = config.GetLong(k_EventTotalDurationMinutesConfigKey) * k_MillisecondsPerMinute;
        var currentSeasonEarliestPotentialStartTimestamp = timestamp - eventDurationMilliseconds;
        return state.LatestSeasonActivityTimestamp < currentSeasonEarliestPotentialStartTimestamp;
    }

    Task SaveAsync(IExecutionContext context, ConfigValues config, PlayerState state, long timestamp)
    {
        return m_Store.SetManyAsync(context, new Dictionary<string, object>
        {
            { k_SeasonXpKey, state.SeasonXp },
            { k_SeasonTierStatesKey, state.SeasonTierStates },
            { k_LatestSeasonActivityTimestampKey, timestamp },
            { k_LatestSeasonActivityEventKeyKey, config.GetString(k_EventKeyConfigKey) },
            { k_PurchasedTimestampKey, state.PurchasedTimestamp },
            { k_PurchasedSeasonKey, state.PurchasedSeason },
        });
    }

    static bool OwnsBattlePass(ConfigValues config, PlayerState state)
        => state.PurchasedSeason == config.GetString(k_EventKeyConfigKey);

    /// <summary>
    /// Seconds until the current event ends. EVENT_END_TIME is the last digit of the minute the
    /// event ends on, within the current ten-minute cycle.
    /// </summary>
    static long GetEventSecondsRemaining(ConfigValues config, long timestamp)
    {
        var now = DateTimeOffset.FromUnixTimeMilliseconds(timestamp);
        var currentMinutesFirstDigit = now.Minute / 10;
        var eventMinutesLastDigit = config.GetLong(k_EventEndTimeConfigKey);
        var eventEndMinute = currentMinutesFirstDigit * 10 + eventMinutesLastDigit;

        // Add 1 minute because EVENT_END_TIME is inclusive (the event doesn't end until after the specified minute).
        return (eventEndMinute + 1) * 60 - (now.Minute * 60 + now.Second);
    }

    static RemoteConfigs MakeClientFriendlyRemoteConfigs(ConfigValues config)
    {
        return new RemoteConfigs
        {
            BattlePassRewardsFree = GetRewardList(config, k_RewardsFreeConfigKey),
            BattlePassRewardsPremium = GetRewardList(config, k_RewardsPremiumConfigKey),
            BattlePassSeasonXpPerTier = config.GetLong(k_SeasonXpPerTierConfigKey),
            EventKey = config.GetString(k_EventKeyConfigKey),
            EventName = config.GetString(k_EventNameConfigKey),
            EventEndTime = config.GetLong(k_EventEndTimeConfigKey),
            EventTotalDurationMinutes = config.GetLong(k_EventTotalDurationMinutesConfigKey),
        };
    }

    static List<TierReward?> GetRewardList(ConfigValues config, string key)
        => config.GetJson<List<TierReward?>>(key, $"Remote Config key {key} is missing for the current season.");

    static int UnlockNewTiersIfEligible(PlayerState state)
    {
        var oldHighestUnlockedTierIndex = GetHighestNonLockedTier(state);
        var newHighestUnlockedTierIndex = state.SeasonXp / k_SeasonXpNeededToUnlockTier;
        if (newHighestUnlockedTierIndex <= oldHighestUnlockedTierIndex)
            return -1; // Nothing new was unlocked.

        // The current operation unlocked at least one new tier.
        // Loop to make sure we don't skip any tiers, in case the XP increase was huge.
        // Port note: the loop starts at the last tier when the XP total points past the tier list
        // (the scripts' out-of-range writes were silent no-ops); the returned index is uncapped, as before.
        for (var i = Math.Min(newHighestUnlockedTierIndex, k_TierCount - 1); i >= 0; i--)
        {
            if (state.SeasonTierStates[i] == k_TierLocked)
                state.SeasonTierStates[i] = k_TierUnlocked;
        }

        return newHighestUnlockedTierIndex;
    }

    static int GetHighestNonLockedTier(PlayerState state)
    {
        // Loop backwards through the tier states to find the highest one that's not locked.
        for (var i = state.SeasonTierStates.Length - 1; i >= 0; i--)
        {
            if (state.SeasonTierStates[i] != k_TierLocked)
                return i;
        }

        // There are no tiers unlocked. In our current design, we don't expect this,
        // but this function should be ready for any possible future design change.
        return -1;
    }

    static void ValidateClaim(PlayerState state, int tierIndex)
    {
        if (tierIndex < 0 || tierIndex >= k_TierCount)
            throw new ModuleError(0, "Error", "The given index doesn't fall within the tier list's bounds.");
        if (state.SeasonTierStates[tierIndex] == k_TierClaimed)
            throw new ModuleError(0, "Error", "Player has already claimed this tier.");
        if (state.SeasonTierStates[tierIndex] == k_TierLocked)
            throw new ModuleError(0, "Error", "The requested tier cannot be claimed because it's locked.");
    }

    static List<TierReward> GetTierRewards(ConfigValues config, PlayerState state, int tierIndex)
    {
        var rewards = new List<TierReward>();
        var freeReward = GetRewardList(config, k_RewardsFreeConfigKey).ElementAtOrDefault(tierIndex);
        if (freeReward is not null)
            rewards.Add(freeReward);

        var premiumReward = GetRewardList(config, k_RewardsPremiumConfigKey).ElementAtOrDefault(tierIndex);
        if (OwnsBattlePass(config, state) && premiumReward is not null)
            rewards.Add(premiumReward);

        return rewards;
    }

    static List<TierReward> GetPastClaimedPremiumRewards(ConfigValues config, PlayerState state)
    {
        var premiumRewards = GetRewardList(config, k_RewardsPremiumConfigKey);
        var rewards = new List<TierReward>();
        for (var i = 0; i < state.SeasonTierStates.Length; i++)
        {
            if (state.SeasonTierStates[i] != k_TierClaimed)
                continue;
            var premiumReward = premiumRewards.ElementAtOrDefault(i);
            if (premiumReward is not null)
                rewards.Add(premiumReward);
        }

        return rewards;
    }

    /// <summary>All rewards of one call land in a single economy write.</summary>
    Task GrantRewardsAsync(IExecutionContext context, List<TierReward> rewards)
    {
        var currencies = rewards.Where(r => r.Service == "currency")
            .Select(r => new KeyValuePair<string, long>(r.Id, r.Quantity));
        var items = rewards.Where(r => r.Service == "inventory")
            .Select(r => new KeyValuePair<string, long>(r.Id, r.Quantity));
        return m_Economy.GrantAsync(context, currencies, items);
    }
}
