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
using Xunit;

namespace SampleGameModule.Tests;

public class IdleClickerGameServiceTests
{
    const string k_StateKey = "IDLE_CLICKER_GAME_STATE";

    readonly Mock<ICloudSaveDataApi> m_CloudSave = new();
    readonly Mock<IExecutionContext> m_Context = new();
    readonly Dictionary<string, Item> m_Stored = new();
    readonly List<SetItemBody> m_Writes = new();
    readonly IdleClickerGameService m_Service;

    public IdleClickerGameServiceTests()
    {
        m_Context.SetupGet(c => c.ProjectId).Returns("project");
        m_Context.SetupGet(c => c.PlayerId).Returns("player");
        m_Context.SetupGet(c => c.EnvironmentId).Returns("environment");
        m_Context.SetupGet(c => c.AccessToken).Returns("token");
        m_Context.SetupGet(c => c.ServiceToken).Returns("service-token");

        var gameApiClient = new Mock<IGameApiClient>();
        gameApiClient.SetupGet(c => c.CloudSaveData).Returns(m_CloudSave.Object);
        gameApiClient.SetupGet(c => c.RemoteConfigSettings).Returns(new Mock<IRemoteConfigSettingsApi>().Object);

        // Each read asks for one key; answer from m_Stored so the economy key and the state key are served independently.
        m_CloudSave
            .Setup(c => c.GetProtectedItemsAsync(It.IsAny<IExecutionContext>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IExecutionContext _, string _, string _, string _, List<string> keys, string _, CancellationToken _) =>
                Response(new GetItemsResponse(
                    keys.Where(m_Stored.ContainsKey).Select(k => m_Stored[k]).ToList(),
                    new GetItemsResponseLinks(""))));

        m_CloudSave
            .Setup(c => c.SetProtectedItemAsync(It.IsAny<IExecutionContext>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<SetItemBody>(), It.IsAny<CancellationToken>()))
            .Callback((IExecutionContext _, string _, string _, string _, SetItemBody body, CancellationToken _) =>
                m_Writes.Add(body))
            .ReturnsAsync(Response(new SetItemResponse("newLock")));

        var store = new PlayerDataStore(gameApiClient.Object);
        var economy = new EconomyModuleService(NullLogger<EconomyModuleService>.Instance, store,
            new RemoteConfigReader(gameApiClient.Object));
        m_Service = new IdleClickerGameService(economy, store, NullLogger<IdleClickerGameService>.Instance);
    }

    void GivenState(IdleClickerState state)
    {
        m_Stored[k_StateKey] = new Item(k_StateKey, JObject.FromObject(state), "lock1",
            new ModifiedMetadata(DateTime.UtcNow), new ModifiedMetadata(DateTime.UtcNow));
    }

    static IdleClickerState BoardAt(long timestamp)
    {
        return new IdleClickerState
        {
            Timestamp = timestamp,
            Obstacles = { Coord(0, 0) },
            UnlockCounters =
            {
                ["Well_Level1"] = 4,
                ["Well_Level2"] = 4,
                ["Well_Level3"] = 0,
                ["Well_Level4"] = 0,
            },
        };
    }

    static IdleClickerCoord Coord(int x, int y) => new() { X = x, Y = y };

    static IdleClickerWell Well(int x, int y, long timestamp) => new() { X = x, Y = y, Timestamp = timestamp };

    static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    // ApiResponse<T>.Data has a non-public setter, so responses are built via reflection.
    static ApiResponse<T> Response<T>(T data)
    {
        var response = new ApiResponse<T>();
        typeof(ApiResponse<T>).GetProperty(nameof(ApiResponse<T>.Data))!.SetValue(response, data);
        return response;
    }

    [Fact]
    public async Task PlaceWell_OnObstacle_ThrowsSpaceOccupied()
    {
        GivenState(BoardAt(Now()));

        var error = await Assert.ThrowsAsync<ModuleError>(
            () => m_Service.PlaceWell(m_Context.Object, Coord(0, 0)));

        Assert.Equal(3, error.Status);
        Assert.Equal("SpaceOccupiedError", error.Name);
    }

    [Fact]
    public async Task MergeWells_DifferentLevels_ThrowsWellsDifferentLevel()
    {
        var now = Now();
        var state = BoardAt(now);
        state.WellsLevel1.Add(Well(1, 0, now));
        state.WellsLevel2.Add(Well(2, 0, now));
        GivenState(state);

        var error = await Assert.ThrowsAsync<ModuleError>(
            () => m_Service.MergeWells(m_Context.Object, Coord(1, 0), Coord(2, 0)));

        Assert.Equal(7, error.Status);
        Assert.Equal("WellsDifferentLevelError", error.Name);
    }

    [Fact]
    public async Task MergeWells_IntoLockedLevel_ThrowsWellLevelLocked()
    {
        var now = Now();
        var state = BoardAt(now);
        state.WellsLevel2.Add(Well(1, 0, now));
        state.WellsLevel2.Add(Well(2, 0, now));
        GivenState(state);

        var error = await Assert.ThrowsAsync<ModuleError>(
            () => m_Service.MergeWells(m_Context.Object, Coord(1, 0), Coord(2, 0)));

        Assert.Equal(10, error.Status);
        Assert.Equal("WellLevelLockedError", error.Name);
    }

    [Fact]
    public async Task GetUpdatedState_GrantsWaterProducedSinceLastTimestamp()
    {
        // 10.5 s ago: a level-1 well yields floor(10.5) = 10, a level-3 well floor(31.5) = 31.
        var then = Now() - 10_500;
        var state = BoardAt(then);
        state.WellsLevel1.Add(Well(1, 0, then));
        state.WellsLevel3.Add(Well(2, 0, then));
        GivenState(state);

        var result = await m_Service.GetUpdatedState(m_Context.Object);

        Assert.Equal(41, result.CurrencyBalance);
        var economyWrite = Assert.Single(m_Writes, w => w.Key == EconomyModuleService.PlayerEconomyKey);
        Assert.Equal(41, ((JObject)economyWrite.Value)["wallet"]!.Value<long>("WATER"));
        var stateWrite = Assert.Single(m_Writes, w => w.Key == k_StateKey);
        Assert.True(((JObject)stateWrite.Value).Value<long>("timestamp") > then);
    }
}
