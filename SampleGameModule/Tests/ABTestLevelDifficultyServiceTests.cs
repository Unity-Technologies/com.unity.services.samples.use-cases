using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SampleGameModule.Infrastructure;
using SampleGameModule.Models;
using SampleGameModule.Services;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Api;
using Unity.Services.CloudSave.Model;
using Unity.Services.RemoteConfig.Api;
using Unity.Services.RemoteConfig.Model;
using Xunit;

namespace SampleGameModule.Tests;

public class ABTestLevelDifficultyServiceTests
{
    const string k_LevelKey = "AB_TEST_PLAYER_LEVEL";
    const string k_XpKey = "AB_TEST_PLAYER_XP";

    readonly Mock<ICloudSaveDataApi> m_CloudSave = new();
    readonly Mock<IRemoteConfigSettingsApi> m_RemoteConfig = new();
    readonly Mock<IExecutionContext> m_Context = new();
    readonly Dictionary<string, object> m_Stored = new();
    readonly Dictionary<string, object> m_Settings = new();
    readonly List<SetItemBody> m_EconomyWrites = new();
    readonly List<SetItemBody> m_BatchWrites = new();
    readonly ABTestLevelDifficultyService m_Service;

    public ABTestLevelDifficultyServiceTests()
    {
        m_Context.SetupGet(c => c.ProjectId).Returns("project");
        m_Context.SetupGet(c => c.PlayerId).Returns("player");
        m_Context.SetupGet(c => c.EnvironmentId).Returns("environment");
        m_Context.SetupGet(c => c.AccessToken).Returns("token");
        m_Context.SetupGet(c => c.ServiceToken).Returns("service-token");

        var gameApiClient = new Mock<IGameApiClient>();
        gameApiClient.SetupGet(c => c.CloudSaveData).Returns(m_CloudSave.Object);
        gameApiClient.SetupGet(c => c.RemoteConfigSettings).Returns(m_RemoteConfig.Object);

        m_CloudSave
            .Setup(c => c.GetProtectedItemsAsync(It.IsAny<IExecutionContext>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((IExecutionContext _, string _, string _, string _, List<string> keys, string _, CancellationToken _) =>
            {
                var results = keys.Where(m_Stored.ContainsKey)
                    .Select(k => new Item(k, m_Stored[k], "lock1",
                        new ModifiedMetadata(DateTime.UtcNow), new ModifiedMetadata(DateTime.UtcNow)))
                    .ToList();
                return Task.FromResult(Response(new GetItemsResponse(results, new GetItemsResponseLinks(""))));
            });
        m_CloudSave
            .Setup(c => c.SetProtectedItemAsync(It.IsAny<IExecutionContext>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<SetItemBody>(), It.IsAny<CancellationToken>()))
            .Callback((IExecutionContext _, string _, string _, string _, SetItemBody body, CancellationToken _) =>
                m_EconomyWrites.Add(body))
            .ReturnsAsync(Response(new SetItemResponse("newLock")));
        m_CloudSave
            .Setup(c => c.SetProtectedItemBatchAsync(It.IsAny<IExecutionContext>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<SetItemBatchBody>(), It.IsAny<CancellationToken>()))
            .Callback((IExecutionContext _, string _, string _, string _, SetItemBatchBody body, CancellationToken _) =>
                m_BatchWrites.AddRange(body.Data))
            .ReturnsAsync(Response(new SetItemBatchResponse(new List<SetItemBatchResponseResultsInner>())));

        m_Settings["ECONOMY_CURRENCIES"] = JObject.Parse(@"{ ""COIN"": { ""name"": ""Coin"" } }");
        m_RemoteConfig
            .Setup(r => r.AssignSettingsGetAsync(It.IsAny<IExecutionContext>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(),
                It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Response(new SettingsDeliveryResponse(new Configs(m_Settings))));

        var store = new PlayerDataStore(gameApiClient.Object);
        var config = new RemoteConfigReader(gameApiClient.Object);
        var economy = new EconomyModuleService(NullLogger<EconomyModuleService>.Instance, store, config);
        m_Service = new ABTestLevelDifficultyService(economy, store, config);
    }

    void GivenAbTestConfig(long xpIncrease, long xpNeeded)
    {
        m_Settings["AB_TEST_XP_INCREASE"] = xpIncrease;
        m_Settings["AB_TEST_LEVEL_UP_XP_NEEDED"] = xpNeeded;
    }

    // ApiResponse<T>.Data has a non-public setter, so responses are built via reflection.
    static ApiResponse<T> Response<T>(T data)
    {
        var response = new ApiResponse<T>();
        typeof(ApiResponse<T>).GetProperty(nameof(ApiResponse<T>.Data))!.SetValue(response, data);
        return response;
    }

    [Fact]
    public async Task BelowThreshold_AddsXpWithoutLevelUp()
    {
        GivenAbTestConfig(5, 100);
        m_Stored[k_LevelKey] = 2L;
        m_Stored[k_XpKey] = 10L;

        var result = await m_Service.GainXPAndLevelIfReady(m_Context.Object);

        Assert.Equal(2, result.PlayerLevel);
        Assert.Equal(15, result.PlayerXP);
        Assert.Equal(5, result.PlayerXPUpdateAmount);
        Assert.False(result.DidLevelUp);
        Assert.Equal("{}", JsonConvert.SerializeObject(result.LevelUpRewards));
        Assert.Empty(m_EconomyWrites);
        Assert.Equal(15L, Convert.ToInt64(m_BatchWrites.Single(w => w.Key == k_XpKey).Value));
        Assert.Equal(2L, Convert.ToInt64(m_BatchWrites.Single(w => w.Key == k_LevelKey).Value));
    }

    [Fact]
    public async Task ReachesThreshold_LevelsUpResetsXpAndGrants100Coin()
    {
        GivenAbTestConfig(5, 100);
        m_Stored[k_LevelKey] = 1L;
        m_Stored[k_XpKey] = 95L;
        m_Stored[EconomyModuleService.PlayerEconomyKey] = JObject.FromObject(new PlayerEconomySnapshot { Wallet = { ["COIN"] = 7 } });

        var result = await m_Service.GainXPAndLevelIfReady(m_Context.Object);

        Assert.Equal(2, result.PlayerLevel);
        Assert.Equal(0, result.PlayerXP);
        Assert.True(result.DidLevelUp);
        Assert.Equal("COIN", result.LevelUpRewards.CurrencyId);
        Assert.Equal(100, result.LevelUpRewards.RewardAmount);
        Assert.Equal(107, result.LevelUpRewards.Balance);
        Assert.Single(m_EconomyWrites);
        Assert.Equal(0L, Convert.ToInt64(m_BatchWrites.Single(w => w.Key == k_XpKey).Value));
    }

    [Fact]
    public async Task NoSavedProgress_StartsAtLevel1WithZeroXp()
    {
        GivenAbTestConfig(5, 100);

        var result = await m_Service.GainXPAndLevelIfReady(m_Context.Object);

        Assert.Equal(1, result.PlayerLevel);
        Assert.Equal(5, result.PlayerXP);
    }

    [Fact]
    public async Task MissingRemoteConfig_ThrowsStatus1CustomError()
    {
        m_Settings["AB_TEST_XP_INCREASE"] = 5L;

        var error = await Assert.ThrowsAsync<ModuleError>(() => m_Service.GainXPAndLevelIfReady(m_Context.Object));

        var parsed = JObject.Parse(error.Message);
        Assert.Equal(1, parsed.Value<int>("status"));
        Assert.Equal("CloudCodeCustomError", parsed.Value<string>("name"));
        Assert.Empty(m_BatchWrites);
    }
}
