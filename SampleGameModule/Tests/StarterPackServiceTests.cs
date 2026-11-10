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

public class StarterPackServiceTests
{
    const string k_StatusKey = "STARTER_PACK_STATUS";

    readonly Mock<ICloudSaveDataApi> m_CloudSave = new();
    readonly Mock<IRemoteConfigSettingsApi> m_RemoteConfig = new();
    readonly Mock<IExecutionContext> m_Context = new();
    readonly Dictionary<string, object> m_Stored = new();
    readonly List<SetItemBody> m_Writes = new();
    readonly StarterPackService m_Service;

    public StarterPackServiceTests()
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
                m_Writes.Add(body))
            .ReturnsAsync(Response(new SetItemResponse("newLock")));

        var settings = new Dictionary<string, object>
        {
            { "ECONOMY_CURRENCIES", JObject.Parse(@"{ ""COIN"": { ""name"": ""Coin"" } }") },
            {
                "ECONOMY_VIRTUAL_PURCHASES", JObject.Parse(
                    @"{ ""STARTER_PACK_PURCHASE"": { ""costs"": [ { ""id"": ""COIN"", ""amount"": 10 } ],
                                                   ""rewards"": [ { ""id"": ""SWORD"", ""amount"": 1 } ] } }")
            },
        };
        m_RemoteConfig
            .Setup(r => r.AssignSettingsGetAsync(It.IsAny<IExecutionContext>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(),
                It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response(new SettingsDeliveryResponse(new Configs(settings))));

        var store = new PlayerDataStore(gameApiClient.Object);
        var config = new RemoteConfigReader(gameApiClient.Object);
        var economy = new EconomyModuleService(NullLogger<EconomyModuleService>.Instance, store, config);
        m_Service = new StarterPackService(economy, store, NullLogger<StarterPackService>.Instance);
    }

    // ApiResponse<T>.Data has a non-public setter, so responses are built via reflection.
    static ApiResponse<T> Response<T>(T data)
    {
        var response = new ApiResponse<T>();
        typeof(ApiResponse<T>).GetProperty(nameof(ApiResponse<T>.Data))!.SetValue(response, data);
        return response;
    }

    [Fact]
    public async Task AlreadyClaimed_ThrowsStatus0ErrorAndWritesNothing()
    {
        m_Stored[k_StatusKey] = JObject.Parse(@"{ ""claimed"": true }");
        m_Stored[EconomyModuleService.PlayerEconomyKey] = JObject.FromObject(new PlayerEconomySnapshot { Wallet = { ["COIN"] = 10 } });

        var error = await Assert.ThrowsAsync<ModuleError>(() => m_Service.PurchaseStarterPack(m_Context.Object));

        var parsed = JObject.Parse(error.Message);
        Assert.Equal(0, parsed.Value<int>("status"));
        Assert.Equal("Error", parsed.Value<string>("name"));
        Assert.Equal("The Starter Pack has already been claimed by this player.", parsed.Value<string>("message"));
        Assert.Empty(m_Writes);
    }

    [Fact]
    public async Task NotClaimed_PurchasesThenMarksClaimed()
    {
        m_Stored[EconomyModuleService.PlayerEconomyKey] = JObject.FromObject(new PlayerEconomySnapshot { Wallet = { ["COIN"] = 10 } });

        var snapshot = await m_Service.PurchaseStarterPack(m_Context.Object);

        Assert.Equal(0, snapshot.Wallet["COIN"]);
        Assert.Equal(1, snapshot.Inventory["SWORD"]);
        Assert.Equal(2, m_Writes.Count);
        Assert.Equal(EconomyModuleService.PlayerEconomyKey, m_Writes[0].Key);
        Assert.Equal(k_StatusKey, m_Writes[1].Key);
        Assert.True(JObject.FromObject(m_Writes[1].Value).Value<bool>("claimed"));
    }

    [Fact]
    public async Task CannotAfford_Throws10504AndDoesNotMarkClaimed()
    {
        m_Stored[EconomyModuleService.PlayerEconomyKey] = JObject.FromObject(new PlayerEconomySnapshot { Wallet = { ["COIN"] = 9 } });

        var error = await Assert.ThrowsAsync<EconomyError>(() => m_Service.PurchaseStarterPack(m_Context.Object));

        Assert.Equal(EconomyError.PurchaseCostsNotMetStatus, JObject.Parse(error.Message).Value<int>("status"));
        Assert.Empty(m_Writes);
    }

    [Fact]
    public async Task ResetForDebug_DeletesTheStatus()
    {
        await m_Service.ResetForDebug(m_Context.Object);

        m_CloudSave.Verify(c => c.DeleteProtectedItemAsync(It.IsAny<IExecutionContext>(), It.IsAny<string>(), k_StatusKey,
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
