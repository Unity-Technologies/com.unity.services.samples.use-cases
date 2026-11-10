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

public class DailyRewardsServiceTests
{
    const string k_StartEpochTimeKey = "DAILY_REWARDS_START_EPOCH_TIME";
    const string k_StatusKey = "DAILY_REWARDS_STATUS";
    const long k_SecondsPerDay = 10;

    readonly Mock<ICloudSaveDataApi> m_CloudSave = new();
    readonly Mock<IRemoteConfigSettingsApi> m_RemoteConfig = new();
    readonly Mock<IExecutionContext> m_Context = new();
    readonly DailyRewardsService m_Service;
    readonly Dictionary<string, object> m_Stored = new();
    readonly List<SetItemBody> m_Writes = new();
    readonly long m_Now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public DailyRewardsServiceTests()
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
                Task.FromResult(Response(new GetItemsResponse(
                    keys.Where(m_Stored.ContainsKey).Select(k => new Item(k, m_Stored[k], "lock1",
                        new ModifiedMetadata(DateTime.UtcNow), new ModifiedMetadata(DateTime.UtcNow))).ToList(),
                    new GetItemsResponseLinks("")))));
        m_CloudSave
            .Setup(c => c.SetProtectedItemAsync(It.IsAny<IExecutionContext>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<SetItemBody>(), It.IsAny<CancellationToken>()))
            .Callback((IExecutionContext _, string _, string _, string _, SetItemBody body, CancellationToken _) =>
                m_Writes.Add(body))
            .ReturnsAsync(Response(new SetItemResponse("newLock")));

        GivenConfig(totalDays: 3);

        var store = new PlayerDataStore(gameApiClient.Object);
        var config = new RemoteConfigReader(gameApiClient.Object);
        var economy = new EconomyModuleService(NullLogger<EconomyModuleService>.Instance, store, config);
        m_Service = new DailyRewardsService(economy, store, config, NullLogger<DailyRewardsService>.Instance);
    }

    void GivenConfig(long totalDays)
    {
        var config = JObject.Parse($@"{{
            ""totalDays"": {totalDays},
            ""secondsPerDay"": {k_SecondsPerDay},
            ""dailyRewards"": [ [ {{ ""id"": ""COIN"", ""quantity"": 10 }} ],
                                [ {{ ""id"": ""GEM"", ""quantity"": 1 }} ],
                                [ {{ ""id"": ""COIN"", ""quantity"": 50 }} ] ],
            ""bonusReward"": [ {{ ""id"": ""STAR"", ""quantity"": 1 }} ]
        }}");
        var settings = new Dictionary<string, object> { { "DAILY_REWARDS_CONFIG", config } };
        m_RemoteConfig
            .Setup(r => r.AssignSettingsGetAsync(It.IsAny<IExecutionContext>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(),
                It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response(new SettingsDeliveryResponse(new Configs(settings))));
    }

    /// <summary>Event started <paramref name="secondsAgo"/> s ago; the player's last claim fell on <paramref name="lastClaimDay"/> (-1: never).</summary>
    void GivenEvent(long secondsAgo, long daysClaimed, long lastClaimDay)
    {
        var start = m_Now - secondsAgo * 1000;
        m_Stored[k_StartEpochTimeKey] = start;
        m_Stored[k_StatusKey] = JsonConvert.SerializeObject(new
        {
            startEpochTime = start,
            daysClaimed,
            lastClaimTime = lastClaimDay < 0 ? 0 : start + lastClaimDay * k_SecondsPerDay * 1000 + 500,
        });
    }

    static JObject ParseError(ModuleError error) => JObject.Parse(error.Message);

    // ApiResponse<T>.Data has a non-public setter, so responses are built via reflection.
    static ApiResponse<T> Response<T>(T data)
    {
        var response = new ApiResponse<T>();
        typeof(ApiResponse<T>).GetProperty(nameof(ApiResponse<T>.Data))!.SetValue(response, data);
        return response;
    }

    [Fact]
    public async Task GetStatus_FirstVisit_StartsEventAndClaimableToday()
    {
        var status = await m_Service.GetStatus(m_Context.Object);

        Assert.True(status.Success);
        Assert.True(status.FirstVisit);
        Assert.True(status.IsStarted);
        Assert.False(status.IsEnded);
        Assert.Equal(0, status.DaysClaimed);
        Assert.Equal(0, status.SecondsTillClaimable);
        Assert.Equal(3, status.DaysRemaining);
        Assert.Equal(3, status.TotalDays);
        Assert.Equal(k_SecondsPerDay, status.SecondsPerDay);
        Assert.Equal(3, status.DailyRewards.Count);
        Assert.Equal("STAR", Assert.Single(status.BonusReward).Id);

        Assert.Equal(2, m_Writes.Count);
        Assert.Equal(k_StartEpochTimeKey, m_Writes[0].Key);
        Assert.Equal(k_StatusKey, m_Writes[1].Key);
        var stored = JObject.Parse(Assert.IsType<string>(m_Writes[1].Value));
        Assert.Equal(Convert.ToInt64(m_Writes[0].Value), stored.Value<long>("startEpochTime"));
        Assert.Equal(0, stored.Value<long>("daysClaimed"));
    }

    [Fact]
    public async Task GetStatus_ClaimedToday_WaitsForNextDay()
    {
        GivenEvent(secondsAgo: 15, daysClaimed: 2, lastClaimDay: 1);

        var status = await m_Service.GetStatus(m_Context.Object);

        Assert.False(status.FirstVisit);
        Assert.Equal(2, status.DaysClaimed);
        Assert.InRange(status.SecondsTillClaimable, 4, 5);
        Assert.Equal(status.SecondsTillNextDay, status.SecondsTillClaimable);
        Assert.Equal(1, status.DaysRemaining);
        Assert.False(status.IsEnded);
        Assert.Empty(m_Writes);
    }

    [Fact]
    public async Task GetStatus_NewDay_IsClaimable()
    {
        GivenEvent(secondsAgo: 15, daysClaimed: 1, lastClaimDay: 0);

        var status = await m_Service.GetStatus(m_Context.Object);

        Assert.Equal(0, status.SecondsTillClaimable);
        Assert.Equal(2, status.DaysRemaining);
        Assert.False(status.IsEnded);
    }

    [Fact]
    public async Task GetStatus_StatusFromOlderEvent_RestartsPlayer()
    {
        GivenEvent(secondsAgo: 15, daysClaimed: 2, lastClaimDay: 1);
        m_Stored[k_StartEpochTimeKey] = m_Now - 5000;

        var status = await m_Service.GetStatus(m_Context.Object);

        Assert.True(status.FirstVisit);
        Assert.Equal(0, status.DaysClaimed);
        Assert.Equal(k_StatusKey, Assert.Single(m_Writes).Key);
    }

    [Fact]
    public async Task Claim_AlreadyClaimedToday_ThrowsStatus4()
    {
        GivenEvent(secondsAgo: 15, daysClaimed: 2, lastClaimDay: 1);

        var error = await Assert.ThrowsAsync<ModuleError>(() => m_Service.Claim(m_Context.Object));

        var parsed = ParseError(error);
        Assert.Equal(4, parsed.Value<int>("status"));
        Assert.Equal("AlreadyClaimedError", parsed.Value<string>("name"));
        Assert.Equal("Daily Rewards already claimed for today.", parsed.Value<string>("message"));
        Assert.Empty(m_Writes);
    }

    [Fact]
    public async Task Claim_EventOver_ThrowsStatus3()
    {
        GivenEvent(secondsAgo: 3 * k_SecondsPerDay + 1, daysClaimed: 3, lastClaimDay: 2);

        var error = await Assert.ThrowsAsync<ModuleError>(() => m_Service.Claim(m_Context.Object));

        var parsed = ParseError(error);
        Assert.Equal(3, parsed.Value<int>("status"));
        Assert.Equal("EventNotActiveError", parsed.Value<string>("name"));
        Assert.Equal("Daily Rewards not active when claim attempt made.", parsed.Value<string>("message"));
    }

    [Fact]
    public async Task Claim_NewDay_GrantsDayRewardAndRecordsClaim()
    {
        GivenEvent(secondsAgo: 15, daysClaimed: 1, lastClaimDay: 0);

        var result = await m_Service.Claim(m_Context.Object);

        Assert.True(result.Success);
        Assert.Equal(2, result.DaysClaimed);
        Assert.Equal(1, result.DaysRemaining);
        Assert.False(result.IsEnded);
        var granted = Assert.Single(result.RewardsGranted);
        Assert.Equal("GEM", granted.Id);
        Assert.Equal(1, granted.Quantity);

        Assert.Equal(2, m_Writes.Count);
        Assert.Equal(EconomyModuleService.PlayerEconomyKey, m_Writes[0].Key);
        Assert.Equal(1, JObject.FromObject(m_Writes[0].Value)["wallet"]!.Value<long>("GEM"));
        Assert.Equal(k_StatusKey, m_Writes[1].Key);
        var stored = JObject.Parse(Assert.IsType<string>(m_Writes[1].Value));
        Assert.Equal(2, stored.Value<long>("daysClaimed"));
        Assert.InRange(stored.Value<long>("lastClaimTime"), m_Now, m_Now + 60_000);
    }

    [Fact]
    public async Task Claim_LastDay_EndsEvent()
    {
        GivenEvent(secondsAgo: 25, daysClaimed: 2, lastClaimDay: 1);

        var result = await m_Service.Claim(m_Context.Object);

        Assert.True(result.Success);
        Assert.Equal(0, result.DaysRemaining);
        Assert.True(result.IsEnded);
        Assert.Equal("COIN", Assert.Single(result.RewardsGranted).Id);
    }

    [Fact]
    public async Task Claim_PastCalendarDays_GrantsBonusReward()
    {
        GivenConfig(totalDays: 5);
        GivenEvent(secondsAgo: 35, daysClaimed: 3, lastClaimDay: 2);

        var result = await m_Service.Claim(m_Context.Object);

        Assert.Equal("STAR", Assert.Single(result.RewardsGranted).Id);
        Assert.Equal(4, result.DaysClaimed);
    }

    [Fact]
    public async Task ResetEvent_WritesStartTimeAndDeletesStatus()
    {
        await m_Service.ResetEvent(m_Context.Object);

        var write = Assert.Single(m_Writes);
        Assert.Equal(k_StartEpochTimeKey, write.Key);
        Assert.InRange(Convert.ToInt64(write.Value), m_Now, m_Now + 60_000);
        m_CloudSave.Verify(c => c.DeleteProtectedItemAsync(It.IsAny<IExecutionContext>(), It.IsAny<string>(), k_StatusKey,
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
