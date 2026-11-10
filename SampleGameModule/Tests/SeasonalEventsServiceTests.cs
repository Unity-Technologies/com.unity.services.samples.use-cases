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

public class SeasonalEventsServiceTests
{
    const string k_LastCompletedEventKey = "SEASONAL_EVENTS_LAST_COMPLETED_EVENT";
    const string k_LastCompletedEventTimestampKey = "SEASONAL_EVENTS_LAST_COMPLETED_EVENT_TIMESTAMP";
    const long k_MinuteMs = 60000;

    readonly Mock<ICloudSaveDataApi> m_CloudSave = new();
    readonly Mock<IRemoteConfigSettingsApi> m_RemoteConfig = new();
    readonly Mock<IExecutionContext> m_Context = new();
    readonly Dictionary<string, object> m_Stored = new();
    readonly Dictionary<string, object> m_Settings = new();
    readonly List<SetItemBody> m_EconomyWrites = new();
    readonly List<SetItemBody> m_BatchWrites = new();
    readonly List<SettingsDeliveryRequest> m_ConfigRequests = new();
    readonly SeasonalEventsService m_Service;

    public SeasonalEventsServiceTests()
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

        // The economy catalog read (no user attributes) and the season read (with attributes) share one settings map.
        m_Settings["ECONOMY_CURRENCIES"] = JObject.Parse(@"{ ""COIN"": { ""name"": ""Coin"" }, ""GEM"": { ""name"": ""Gem"" } }");
        m_Settings["EVENT_KEY"] = "Fall";
        m_Settings["EVENT_TOTAL_DURATION_MINUTES"] = 10L;
        m_Settings["SEASONAL_EVENTS_CHALLENGE_REWARD"] = JObject.Parse(
            @"{ ""rewards"": [ { ""id"": ""COIN"", ""quantity"": 5, ""spriteAddress"": ""Sprites/Coin"" },
                               { ""id"": ""GEM"", ""quantity"": 2, ""spriteAddress"": ""Sprites/Gem"" } ] }");
        m_RemoteConfig
            .Setup(r => r.AssignSettingsGetAsync(It.IsAny<IExecutionContext>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(),
                It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Response(new SettingsDeliveryResponse(new Configs(m_Settings))));
        m_RemoteConfig
            .Setup(r => r.AssignSettingsAsync(It.IsAny<IExecutionContext>(), It.IsAny<string>(),
                It.IsAny<SettingsDeliveryRequest>(), It.IsAny<CancellationToken>()))
            .Callback((IExecutionContext _, string _, SettingsDeliveryRequest request, CancellationToken _) =>
                m_ConfigRequests.Add(request))
            .ReturnsAsync(() => Response(new SettingsDeliveryResponse(new Configs(m_Settings))));

        var store = new PlayerDataStore(gameApiClient.Object);
        var config = new RemoteConfigReader(gameApiClient.Object);
        var economy = new EconomyModuleService(NullLogger<EconomyModuleService>.Instance, store, config);
        m_Service = new SeasonalEventsService(economy, store, config);
    }

    static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    // ApiResponse<T>.Data has a non-public setter, so responses are built via reflection.
    static ApiResponse<T> Response<T>(T data)
    {
        var response = new ApiResponse<T>();
        typeof(ApiResponse<T>).GetProperty(nameof(ApiResponse<T>.Data))!.SetValue(response, data);
        return response;
    }

    [Fact]
    public async Task GetServerTime_ReturnsCurrentUnixMilliseconds()
    {
        var before = NowMs();
        var time = await m_Service.GetServerTime(m_Context.Object);
        Assert.InRange(time, before, NowMs());
    }

    [Fact]
    public async Task SameSeasonClaimedWithinDuration_ThrowsStatus2AndWritesNothing()
    {
        m_Stored[k_LastCompletedEventKey] = "Fall";
        m_Stored[k_LastCompletedEventTimestampKey] = NowMs() - 1 * k_MinuteMs;

        var error = await Assert.ThrowsAsync<ModuleError>(() => m_Service.GrantEventReward(m_Context.Object));

        var parsed = JObject.Parse(error.Message);
        Assert.Equal(2, parsed.Value<int>("status"));
        Assert.Equal("InvalidRewardDistributionAttemptError", parsed.Value<string>("name"));
        Assert.Equal("The rewards for this season have already been claimed.", parsed.Value<string>("message"));
        Assert.Empty(m_EconomyWrites);
        Assert.Empty(m_BatchWrites);
    }

    [Fact]
    public async Task SameSeasonClaimedInAnEarlierIteration_Grants()
    {
        m_Stored[k_LastCompletedEventKey] = "Fall";
        m_Stored[k_LastCompletedEventTimestampKey] = NowMs() - 20 * k_MinuteMs;

        var result = await m_Service.GrantEventReward(m_Context.Object);

        Assert.Equal(2, result.GrantedRewards.Count);
        Assert.Single(m_EconomyWrites);
    }

    [Fact]
    public async Task DifferentSeason_GrantsAllRewardsInOneWriteAndRecordsTheClaim()
    {
        m_Stored[k_LastCompletedEventKey] = "Summer";
        m_Stored[k_LastCompletedEventTimestampKey] = NowMs() - 1 * k_MinuteMs;
        m_Stored[EconomyModuleService.PlayerEconomyKey] = JObject.FromObject(new PlayerEconomySnapshot { Wallet = { ["COIN"] = 10 } });
        var before = NowMs();

        var result = await m_Service.GrantEventReward(m_Context.Object);

        // "quantity" carries the post-grant balance, as the script returned it.
        var coin = result.GrantedRewards.Single(r => r.Id == "COIN");
        Assert.Equal(15, coin.Quantity);
        Assert.Equal("Sprites/Coin", coin.SpriteAddress);
        Assert.Equal(2, result.GrantedRewards.Single(r => r.Id == "GEM").Quantity);
        Assert.Equal("Fall", result.EventKey);
        Assert.InRange(result.Timestamp, before, NowMs());
        Assert.InRange(result.TimestampMinutes, 0, 59);
        Assert.Single(m_EconomyWrites);
        Assert.Equal("Fall", m_BatchWrites.Single(w => w.Key == k_LastCompletedEventKey).Value);
        Assert.Equal(result.Timestamp, Convert.ToInt64(m_BatchWrites.Single(w => w.Key == k_LastCompletedEventTimestampKey).Value));
    }

    [Fact]
    public async Task SendsTwoDigitTimestampMinutesAsUserAttribute()
    {
        await m_Service.GrantEventReward(m_Context.Object);

        var minutes = Assert.Single(m_ConfigRequests).Attributes.User["timestampMinutes"];
        Assert.Equal(2, minutes.Length);
        Assert.InRange(int.Parse(minutes), 0, 59);
    }
}
