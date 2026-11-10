using Microsoft.Extensions.DependencyInjection;
using SampleGameModule.Infrastructure;
using SampleGameModule.Services;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace SampleGameModule;

public class ModuleConfig : ICloudCodeSetup
{
    public void Setup(ICloudCodeConfig config)
    {
        config.Dependencies.AddSingleton(GameApiClient.Create());
        config.Dependencies.AddSingleton<PlayerDataStore>();
        config.Dependencies.AddSingleton<RemoteConfigReader>();

        // Shared substrate: every sample service writes the player's wallet and inventory through it.
        config.Dependencies.AddSingleton<EconomyModuleService>();

        // One service per sample; function names match the scripts they replace.
        config.Dependencies.AddSingleton<LootBoxesService>();
        config.Dependencies.AddSingleton<LootBoxesWithCooldownService>();
        config.Dependencies.AddSingleton<StarterPackService>();
        config.Dependencies.AddSingleton<ABTestLevelDifficultyService>();
        config.Dependencies.AddSingleton<SeasonalEventsService>();
        config.Dependencies.AddSingleton<CommandBatchingService>();
        config.Dependencies.AddSingleton<DailyRewardsService>();
        config.Dependencies.AddSingleton<InGameMailboxService>();
        config.Dependencies.AddSingleton<CloudAiMiniGameService>();
        config.Dependencies.AddSingleton<BattlePassService>();
        config.Dependencies.AddSingleton<IdleClickerGameService>();
    }
}
