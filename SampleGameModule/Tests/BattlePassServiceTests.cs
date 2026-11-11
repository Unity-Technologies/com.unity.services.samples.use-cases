using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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

public class BattlePassServiceTests
{
    static readonly int[] k_DefaultTierStates = { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

    readonly Mock<ICloudSaveDataApi> m_CloudSave = new();
    readonly Mock<IRemoteConfigSettingsApi> m_RemoteConfig = new();
    readonly Mock<IExecutionContext> m_Context = new();
    readonly BattlePassService m_Service;
    readonly List<SetItemBatchBody> m_BatchWrites = new();

    public BattlePassServiceTests()
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
            .Setup(c => c.SetProtectedItemBatchAsync(It.IsAny<IExecutionContext>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<SetItemBatchBody>(), It.IsAny<CancellationToken>()))
            .Callback((IExecutionContext _, string _, string _, string _, SetItemBatchBody body, CancellationToken _) =>
                m_BatchWrites.Add(body))
            .ReturnsAsync(Response(new SetItemBatchResponse(new List<SetItemBatchResponseResultsInner>())));

        var store = new PlayerDataStore(gameApiClient.Object);
        var config = new RemoteConfigReader(gameApiClient.Object);
        var economy = new EconomyModuleService(NullLogger<EconomyModuleService>.Instance, store, config);
        m_Service = new BattlePassService(economy, store, config, NullLogger<BattlePassService>.Instance);
    }

    void GivenSeason(string eventKey, long durationMinutes = 3)
    {
        var settings = new Dictionary<string, object>
        {
            { "EVENT_KEY", eventKey },
            { "EVENT_NAME", $"{eventKey} Event" },
            { "EVENT_END_TIME", 2L },
            { "EVENT_TOTAL_DURATION_MINUTES", durationMinutes },
            { "BATTLE_PASS_REWARDS_FREE", JArray.Parse(@"[{ ""service"": ""currency"", ""id"": ""COIN"", ""quantity"": 50 }]") },
            { "BATTLE_PASS_REWARDS_PREMIUM", JArray.Parse(@"[{ ""service"": ""inventory"", ""id"": ""SWORD"", ""quantity"": 1 }]") },
            { "BATTLE_PASS_SEASON_XP_PER_TIER", 100L },
        };
        m_RemoteConfig
            .Setup(r => r.AssignSettingsAsync(It.IsAny<IExecutionContext>(), It.IsAny<string>(),
                It.IsAny<SettingsDeliveryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response(new SettingsDeliveryResponse(new Configs(settings))));
    }

    void GivenStoredProgress(int seasonXp, int[] tierStates, string eventKey, long activityTimestamp)
    {
        var values = new Dictionary<string, object>
        {
            { "BATTLE_PASS_SEASON_XP", seasonXp },
            { "BATTLE_PASS_SEASON_TIER_STATES", JArray.FromObject(tierStates) },
            { "BATTLE_PASS_LATEST_SEASON_ACTIVITY_TIMESTAMP", activityTimestamp },
            { "BATTLE_PASS_LATEST_SEASON_ACTIVITY_EVENT_KEY", eventKey },
            { "BATTLE_PASS_PURCHASED_TIMESTAMP", 0L },
            { "BATTLE_PASS_PURCHASED_SEASON", "" },
        };
        var results = values.Select(kv => new Item(kv.Key, kv.Value, "lock1",
            new ModifiedMetadata(DateTime.UtcNow), new ModifiedMetadata(DateTime.UtcNow))).ToList();
        GivenGetItemsResponse(new GetItemsResponse(results, new GetItemsResponseLinks("")));
    }

    void GivenNoStoredProgress()
    {
        GivenGetItemsResponse(new GetItemsResponse(new List<Item>(), new GetItemsResponseLinks("")));
    }

    void GivenGetItemsResponse(GetItemsResponse response)
    {
        m_CloudSave
            .Setup(c => c.GetProtectedItemsAsync(It.IsAny<IExecutionContext>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response(response));
    }

    static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    static JObject ParseError(ModuleError error) => JObject.Parse(error.Message);

    // ApiResponse<T>.Data has a non-public setter, so responses are built via reflection.
    static ApiResponse<T> Response<T>(T data)
    {
        var response = new ApiResponse<T>();
        typeof(ApiResponse<T>).GetProperty(nameof(ApiResponse<T>.Data))!.SetValue(response, data);
        return response;
    }

    [Fact]
    public async Task GainSeasonXP_100Xp_UnlocksTier1()
    {
        GivenSeason("Fall");
        GivenNoStoredProgress();

        var result = await m_Service.BattlePass_GainSeasonXP(m_Context.Object, 100);

        Assert.Equal(1, result.UnlockedNewTier);
        Assert.Equal(100, result.SeasonXp);
        Assert.Equal(new[] { 1, 1, 0, 0, 0, 0, 0, 0, 0, 0 }, result.SeasonTierStates);
        var write = Assert.Single(m_BatchWrites);
        Assert.Equal(6, write.Data.Count);
        Assert.Equal("Fall", (string)write.Data.Single(d => d.Key == "BATTLE_PASS_LATEST_SEASON_ACTIVITY_EVENT_KEY").Value);
    }

    [Fact]
    public async Task ClaimTier_LockedTier_ThrowsAndDoesNotWrite()
    {
        GivenSeason("Fall");
        GivenStoredProgress(0, k_DefaultTierStates, "Fall", Now());

        var error = await Assert.ThrowsAsync<ModuleError>(
            () => m_Service.BattlePass_ClaimTier(m_Context.Object, 1));

        var parsed = ParseError(error);
        Assert.Equal(0, parsed.Value<int>("status"));
        Assert.Equal("Error", parsed.Value<string>("name"));
        Assert.Equal("The requested tier cannot be claimed because it's locked.", parsed.Value<string>("message"));
        Assert.Empty(m_BatchWrites);
    }

    [Fact]
    public async Task ClaimTier_AlreadyClaimed_Throws()
    {
        GivenSeason("Fall");
        GivenStoredProgress(0, new[] { 2, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, "Fall", Now());

        var error = await Assert.ThrowsAsync<ModuleError>(
            () => m_Service.BattlePass_ClaimTier(m_Context.Object, 0));

        Assert.Equal("Player has already claimed this tier.", ParseError(error).Value<string>("message"));
    }

    [Fact]
    public async Task GetState_ChangedEventKey_ResetsProgress()
    {
        GivenSeason("Fall");
        GivenStoredProgress(500, new[] { 2, 2, 1, 1, 1, 1, 0, 0, 0, 0 }, "Winter", Now());

        var result = await m_Service.BattlePass_GetState(m_Context.Object);

        Assert.Equal(0, result.SeasonXp);
        Assert.Equal(k_DefaultTierStates, result.SeasonTierStates);
        Assert.False(result.OwnsBattlePass);
        Assert.Equal("Fall", result.RemoteConfigs.EventKey);
        Assert.Single(m_BatchWrites);
    }
}
