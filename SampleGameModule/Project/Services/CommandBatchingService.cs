using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SampleGameModule.Infrastructure;
using SampleGameModule.Models;
using Unity.Services.CloudCode.Core;

namespace SampleGameModule.Services;

/// <summary>
/// Command Batching sample: validates a whole game's worth of commands server-side,
/// then pays out their rewards in one go.
/// Used by: Command Batching, CloudCodeManager.CallProcessBatchEndpoint.
/// </summary>
public class CommandBatchingService
{
    const string k_DefeatRedEnemy = "COMMANDBATCH_DEFEAT_RED_ENEMY";
    const string k_DefeatBlueEnemy = "COMMANDBATCH_DEFEAT_BLUE_ENEMY";
    const string k_OpenChest = "COMMANDBATCH_OPEN_CHEST";
    const string k_AchieveBonusGoal = "COMMANDBATCH_ACHIEVE_BONUS_GOAL";
    const string k_GameOver = "COMMANDBATCH_GAME_OVER";

    // The correct number of commands is 7 exactly (6 turns + 1 game over)
    const int k_CommandsPerBatch = 7;

    const string k_CurrencyService = "currency";
    const string k_CloudSaveService = "cloudSave";

    static readonly string[] k_CommandKeys =
    {
        k_DefeatRedEnemy, k_DefeatBlueEnemy, k_OpenChest, k_AchieveBonusGoal, k_GameOver,
    };

    readonly EconomyModuleService m_Economy;
    readonly PlayerDataStore m_Store;
    readonly RemoteConfigReader m_Config;

    public CommandBatchingService(EconomyModuleService economy, PlayerDataStore store, RemoteConfigReader config)
    {
        m_Economy = economy;
        m_Store = store;
        m_Config = config;
    }

    /// <summary>One reward of a command, as stored in the COMMANDBATCH_* Remote Config keys.</summary>
    public class CommandReward
    {
        [JsonProperty("service")] public string Service { get; set; } = "";
        [JsonProperty("id")] public string Id { get; set; } = "";
        [JsonProperty("amount")] public long Amount { get; set; }
    }

    public class CommandDefinition
    {
        [JsonProperty("rewards")] public List<CommandReward> Rewards { get; set; } = new();
    }

    /// <summary>
    /// Used by: Command Batching sample, CloudCodeManager.CallProcessBatchEndpoint.
    /// Validates the batch, then grants the summed currency rewards in one economy write
    /// and increments the Cloud Save counters (COMMAND_BATCH_XP, COMMAND_BATCH_GOALS_ACHIEVED).
    /// </summary>
    [CloudCodeFunction("CommandBatching_ProcessBatch")]
    public async Task ProcessBatch(IExecutionContext context, List<string> commands)
    {
        ValidateCommands(commands);

        var rewardsByCommand = await GetCommandRewardsAsync(context);
        var currencyRewards = new Dictionary<string, long>();
        var cloudSaveRewards = new Dictionary<string, long>();
        foreach (var reward in commands.SelectMany(command => rewardsByCommand[command]))
        {
            var group = reward.Service == k_CurrencyService ? currencyRewards : cloudSaveRewards;
            group[reward.Id] = group.GetValueOrDefault(reward.Id) + reward.Amount;
        }

        await m_Economy.GrantAsync(context, currencyRewards);
        await DistributeCloudSaveRewardsAsync(context, cloudSaveRewards);
    }

    public static void ValidateCommands(IReadOnlyList<string> commands)
    {
        if (commands.Count > k_CommandsPerBatch)
            throw new ModuleError(2, "InvalidArgumentError", "Too many commands in batch.");
        if (commands.Count < k_CommandsPerBatch)
            throw new ModuleError(2, "InvalidArgumentError", "Not enough commands in batch.");
        if (commands[^1] != k_GameOver)
            throw new ModuleError(2, "InvalidArgumentError", "Last command must be Game Over.");

        ValidateCommandsAreInLegalOrder(commands);
    }

    static void ValidateCommandsAreInLegalOrder(IReadOnlyList<string> commands)
    {
        // any open chest command always follows defeat enemy command
        // achieveBonusGoal only appears in the list after openChest has appeared
        // no commands occur after gameOver
        var achieveBonusGoalValidMove = false;
        var openChestValidMove = false;
        var gameOverSeen = false;

        foreach (var command in commands)
        {
            if (gameOverSeen)
                throw new ModuleError(2, "InvalidArgumentError", "There can be no commands after Game Over.");

            switch (command)
            {
                case k_DefeatRedEnemy:
                case k_DefeatBlueEnemy:
                    openChestValidMove = true;
                    break;

                case k_OpenChest:
                    if (!openChestValidMove)
                        throw new ModuleError(3, "InvalidGameplayError",
                            "Chests can only be opened immediately after a red or blue enemy has been defeated.");
                    achieveBonusGoalValidMove = true;
                    break;

                case k_AchieveBonusGoal:
                    if (!achieveBonusGoalValidMove)
                        throw new ModuleError(3, "InvalidGameplayError",
                            "Bonus goals can only be achieved once a chest has been opened.");
                    openChestValidMove = false;
                    break;

                case k_GameOver:
                    gameOverSeen = true;
                    break;

                default:
                    // Port note: the script crashed with a TypeError on a command it had no rewards for; reject it cleanly.
                    throw new ModuleError(2, "InvalidArgumentError", $"Unknown command '{command}'.");
            }
        }
    }

    /// <summary>Every command's rewards; a command missing from Remote Config has none.</summary>
    async Task<Dictionary<string, List<CommandReward>>> GetCommandRewardsAsync(IExecutionContext context)
    {
        var config = await m_Config.GetAsync(context, k_CommandKeys);
        if (!k_CommandKeys.Any(config.Has))
            throw new ModuleError(1, "CloudCodeCustomError",
                "There was a problem getting command reward data from Remote Config.");

        return k_CommandKeys.ToDictionary(key => key,
            key => config.GetJson(key)?.ToObject<CommandDefinition>()?.Rewards ?? new List<CommandReward>());
    }

    async Task DistributeCloudSaveRewardsAsync(IExecutionContext context, Dictionary<string, long> rewards)
    {
        if (rewards.Count == 0)
            return;

        var current = await m_Store.GetManyAsync(context, rewards.Keys);
        var updated = rewards.ToDictionary(kv => kv.Key, kv => (object)(current[kv.Key].As<long>() + kv.Value));
        await m_Store.SetManyAsync(context, updated);
    }
}
