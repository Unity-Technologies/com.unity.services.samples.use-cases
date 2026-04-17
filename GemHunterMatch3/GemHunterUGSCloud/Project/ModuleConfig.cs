using GemHunterUGSCloud.Services;
using Microsoft.Extensions.DependencyInjection;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
namespace GemHunterUGSCloud
{
public class ModuleConfig : ICloudCodeSetup
{
    public void Setup(ICloudCodeConfig config)
    {
        config.Dependencies.AddSingleton(GameApiClient.Create());

        config.Dependencies.AddSingleton<GiftHeartCooldownService>();
        
        config.Dependencies.AddSingleton<PlayerEconomyService>();
        config.Dependencies.AddSingleton<PlayerDataService>();
        
        config.Dependencies.AddSingleton<LootBoxCooldownService>();
        
        // Needs PlayerEconomyService
        config.Dependencies.AddSingleton<LootBoxService>();
        // Needs PlayerEconomyService
        config.Dependencies.AddSingleton<CommandBatchingService_AreaUpgradables>();
        
        config.Dependencies.AddSingleton<DailyRewardsStatusService>();
        config.Dependencies.AddSingleton<DailyRewardsMonthlyResetService>();
        config.Dependencies.AddSingleton<DailyRewardsClaimService>();

        config.Dependencies.AddSingleton<AllPlayersService>();
        config.Dependencies.AddSingleton<FriendsService>();
        config.Dependencies.AddSingleton<PlayerGiftsService>();
        config.Dependencies.AddSingleton<AdRewardsService>();
        
        config.Dependencies.AddSingleton<StoreService>();
    }
}
}
