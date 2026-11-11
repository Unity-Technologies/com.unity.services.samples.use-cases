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

public class CommandBatchingServiceTests
{
    const string k_Red = "COMMANDBATCH_DEFEAT_RED_ENEMY";
    const string k_Blue = "COMMANDBATCH_DEFEAT_BLUE_ENEMY";
    const string k_Chest = "COMMANDBATCH_OPEN_CHEST";
    const string k_Bonus = "COMMANDBATCH_ACHIEVE_BONUS_GOAL";
    const string k_GameOver = "COMMANDBATCH_GAME_OVER";

    readonly Mock<ICloudSaveDataApi> m_CloudSave = new();
    readonly Mock<IRemoteConfigSettingsApi> m_RemoteConfig = new();
    readonly Mock<IExecutionContext> m_Context = new();
    readonly CommandBatchingService m_Service;
    readonly Dictionary<string, object> m_Stored = new();
    readonly List<SetItemBody> m_Writes = new();
    readonly List<SetItemBatchBody> m_BatchWrites = new();

    public CommandBatchingServiceTests()
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
        m_CloudSave
            .Setup(c => c.SetProtectedItemBatchAsync(It.IsAny<IExecutionContext>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<SetItemBatchBody>(), It.IsAny<CancellationToken>()))
            .Callback((IExecutionContext _, string _, string _, string _, SetItemBatchBody body, CancellationToken _) =>
                m_BatchWrites.Add(body))
            .ReturnsAsync(Response(new SetItemBatchResponse(new List<SetItemBatchResponseResultsInner>())));

        var store = new PlayerDataStore(gameApiClient.Object);
        var config = new RemoteConfigReader(gameApiClient.Object);
        var economy = new EconomyModuleService(NullLogger<EconomyModuleService>.Instance, store, config);
        m_Service = new CommandBatchingService(economy, store, config);
    }

    void GivenRewardConfig()
    {
        var settings = new Dictionary<string, object>
        {
            { k_Red, Rewards(("currency", "COIN", 5), ("cloudSave", "COMMAND_BATCH_XP", 50)) },
            { k_Blue, Rewards(("currency", "GEM", 5), ("cloudSave", "COMMAND_BATCH_XP", 50)) },
            { k_Chest, Rewards(("currency", "COIN", 25), ("currency", "GEM", 25), ("cloudSave", "COMMAND_BATCH_XP", 100)) },
            { k_Bonus, Rewards(("cloudSave", "COMMAND_BATCH_XP", 150), ("cloudSave", "COMMAND_BATCH_GOALS_ACHIEVED", 1)) },
            { k_GameOver, Rewards(("cloudSave", "COMMAND_BATCH_XP", 100)) },
        };
        m_RemoteConfig
            .Setup(r => r.AssignSettingsGetAsync(It.IsAny<IExecutionContext>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(),
                It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response(new SettingsDeliveryResponse(new Configs(settings))));
    }

    static JObject Rewards(params (string service, string id, long amount)[] rewards)
    {
        return new JObject
        {
            ["rewards"] = new JArray(rewards.Select(r => new JObject
            {
                ["service"] = r.service, ["id"] = r.id, ["amount"] = r.amount,
            })),
        };
    }

    static JObject ParseError(ModuleError error) => JObject.Parse(error.Message);

    // ApiResponse<T>.Data has a non-public setter, so responses are built via reflection.
    static ApiResponse<T> Response<T>(T data)
    {
        var response = new ApiResponse<T>();
        typeof(ApiResponse<T>).GetProperty(nameof(ApiResponse<T>.Data))!.SetValue(response, data);
        return response;
    }

    [Theory]
    [InlineData(2, "Too many commands in batch.", k_Red, k_Red, k_Red, k_Red, k_Red, k_Red, k_Red, k_GameOver)]
    [InlineData(2, "Not enough commands in batch.", k_Red, k_Red, k_GameOver)]
    [InlineData(2, "Last command must be Game Over.", k_Red, k_Red, k_Red, k_Red, k_Red, k_Red, k_Red)]
    [InlineData(2, "There can be no commands after Game Over.", k_Red, k_Red, k_Red, k_Red, k_Red, k_GameOver, k_GameOver)]
    [InlineData(3, "Chests can only be opened immediately after a red or blue enemy has been defeated.",
        k_Chest, k_Red, k_Red, k_Red, k_Red, k_Red, k_GameOver)]
    [InlineData(3, "Bonus goals can only be achieved once a chest has been opened.",
        k_Red, k_Bonus, k_Red, k_Red, k_Red, k_Red, k_GameOver)]
    [InlineData(3, "Chests can only be opened immediately after a red or blue enemy has been defeated.",
        k_Red, k_Chest, k_Bonus, k_Chest, k_Red, k_Red, k_GameOver)]
    public async Task ProcessBatch_InvalidBatch_ThrowsBeforeAnyWrite(int status, string message, params string[] commands)
    {
        var error = await Assert.ThrowsAsync<ModuleError>(
            () => m_Service.ProcessBatch(m_Context.Object, commands.ToList()));

        var parsed = ParseError(error);
        Assert.Equal(status, parsed.Value<int>("status"));
        Assert.Equal(message, parsed.Value<string>("message"));
        Assert.Empty(m_Writes);
        Assert.Empty(m_BatchWrites);
    }

    [Fact]
    public async Task ProcessBatch_ValidBatch_SumsRewardsIntoOneEconomyWriteAndOneCloudSaveBatch()
    {
        GivenRewardConfig();
        m_Stored["COMMAND_BATCH_XP"] = 10L;
        var commands = new List<string> { k_Red, k_Blue, k_Chest, k_Bonus, k_Red, k_Chest, k_GameOver };

        await m_Service.ProcessBatch(m_Context.Object, commands);

        var economyWrite = Assert.Single(m_Writes);
        Assert.Equal(EconomyModuleService.PlayerEconomyKey, economyWrite.Key);
        var wallet = JObject.FromObject(economyWrite.Value)["wallet"]!;
        Assert.Equal(60, wallet.Value<long>("COIN"));
        Assert.Equal(55, wallet.Value<long>("GEM"));

        var batch = Assert.Single(m_BatchWrites).Data.ToDictionary(d => d.Key, d => Convert.ToInt64(d.Value));
        Assert.Equal(610, batch["COMMAND_BATCH_XP"]);
        Assert.Equal(1, batch["COMMAND_BATCH_GOALS_ACHIEVED"]);
    }
}
