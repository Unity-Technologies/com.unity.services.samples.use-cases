using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SampleGameModule.Infrastructure;
using SampleGameModule.Models;
using Unity.Services.CloudCode.Core;

namespace SampleGameModule.Services;

/// <summary>
/// AB Test Level Difficulty sample: XP gain and level-up, with the XP-per-gain and
/// XP-needed-per-level coming from the Remote Config A/B test.
/// Used by: AB Test Level Difficulty, CloudCodeManager.CallGainXPAndLevelIfReadyEndpoint.
/// </summary>
public class ABTestLevelDifficultyService
{
    const string k_LevelKey = "AB_TEST_PLAYER_LEVEL";
    const string k_XpKey = "AB_TEST_PLAYER_XP";
    const string k_XpIncreaseConfigKey = "AB_TEST_XP_INCREASE";
    const string k_XpNeededConfigKey = "AB_TEST_LEVEL_UP_XP_NEEDED";
    const string k_RewardCurrencyId = "COIN";
    const long k_RewardAmount = 100;

    readonly EconomyModuleService m_Economy;
    readonly PlayerDataStore m_Store;
    readonly RemoteConfigReader m_Config;

    public ABTestLevelDifficultyService(EconomyModuleService economy, PlayerDataStore store, RemoteConfigReader config)
    {
        m_Economy = economy;
        m_Store = store;
        m_Config = config;
    }

    /// <summary>Serializes as {} when the player did not level up, as the script returned.</summary>
    public class LevelUpRewards
    {
        [JsonProperty("currencyId", NullValueHandling = NullValueHandling.Ignore)] public string? CurrencyId { get; set; }
        [JsonProperty("rewardAmount", NullValueHandling = NullValueHandling.Ignore)] public long? RewardAmount { get; set; }
        [JsonProperty("balance", NullValueHandling = NullValueHandling.Ignore)] public long? Balance { get; set; }
    }

    public class GainXPAndLevelResult
    {
        [JsonProperty("playerLevel")] public long PlayerLevel { get; set; }
        [JsonProperty("playerXP")] public long PlayerXP { get; set; }
        [JsonProperty("playerXPUpdateAmount")] public long PlayerXPUpdateAmount { get; set; }
        [JsonProperty("didLevelUp")] public bool DidLevelUp { get; set; }
        [JsonProperty("levelUpRewards")] public LevelUpRewards LevelUpRewards { get; set; } = new();
    }

    /// <summary>
    /// Used by: AB Test Level Difficulty sample, CloudCodeManager.CallGainXPAndLevelIfReadyEndpoint.
    /// Adds the A/B-tested XP amount; on reaching the threshold, levels up, resets XP and grants 100 COIN.
    /// </summary>
    [CloudCodeFunction("ABTestLevelDifficulty_GainXPAndLevelIfReady")]
    public async Task<GainXPAndLevelResult> GainXPAndLevelIfReady(IExecutionContext context)
    {
        var saved = await m_Store.GetManyAsync(context, new[] { k_LevelKey, k_XpKey });
        var currentLevel = saved[k_LevelKey].Exists ? saved[k_LevelKey].As<long>() : 1;
        var currentXP = saved[k_XpKey].Exists ? saved[k_XpKey].As<long>() : 0;

        var config = await m_Config.GetAsync(context, new[] { k_XpIncreaseConfigKey, k_XpNeededConfigKey });
        if (!config.Has(k_XpIncreaseConfigKey) || !config.Has(k_XpNeededConfigKey))
        {
            throw new ModuleError(1, "CloudCodeCustomError",
                "Failed to get AB_TEST_XP_INCREASE or AB_TEST_LEVEL_UP_XP_NEEDED data from Remote Config.");
        }

        var xpIncreaseAmount = config.GetLong(k_XpIncreaseConfigKey);
        var xpNeededForNextLevel = config.GetLong(k_XpNeededConfigKey);

        var updatedPlayerLevel = currentLevel;
        var updatedPlayerXP = currentXP + xpIncreaseAmount;
        var shouldPlayerLevelUp = updatedPlayerXP >= xpNeededForNextLevel;

        if (shouldPlayerLevelUp)
        {
            updatedPlayerLevel = currentLevel + 1;
            updatedPlayerXP = 0;
        }

        var levelUpRewards = new LevelUpRewards();
        if (shouldPlayerLevelUp)
        {
            var snapshot = await m_Economy.GrantAsync(context,
                new[] { new KeyValuePair<string, long>(k_RewardCurrencyId, k_RewardAmount) });
            levelUpRewards.CurrencyId = k_RewardCurrencyId;
            levelUpRewards.RewardAmount = k_RewardAmount;
            levelUpRewards.Balance = snapshot.Wallet[k_RewardCurrencyId];
        }

        await m_Store.SetManyAsync(context, new Dictionary<string, object>
        {
            { k_XpKey, updatedPlayerXP },
            { k_LevelKey, updatedPlayerLevel },
        });

        return new GainXPAndLevelResult
        {
            PlayerLevel = updatedPlayerLevel,
            PlayerXP = updatedPlayerXP,
            PlayerXPUpdateAmount = xpIncreaseAmount,
            DidLevelUp = shouldPlayerLevelUp,
            LevelUpRewards = levelUpRewards,
        };
    }
}
