using System;
using System.Collections.Generic;
using System.Net;
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

public class EconomyModuleServiceTests
{
    const string k_PlayerEconomyKey = "player_economy";

    readonly Mock<ICloudSaveDataApi> m_CloudSave = new();
    readonly Mock<IRemoteConfigSettingsApi> m_RemoteConfig = new();
    readonly Mock<IExecutionContext> m_Context = new();
    readonly EconomyModuleService m_Service;
    readonly List<SetItemBody> m_Writes = new();

    public EconomyModuleServiceTests()
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
            .Setup(c => c.SetProtectedItemAsync(It.IsAny<IExecutionContext>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<SetItemBody>(), It.IsAny<CancellationToken>()))
            .Callback((IExecutionContext _, string _, string _, string _, SetItemBody body, CancellationToken _) =>
                m_Writes.Add(body))
            .ReturnsAsync(Response(new SetItemResponse("newLock")));

        m_Service = new EconomyModuleService(NullLogger<EconomyModuleService>.Instance,
            new PlayerDataStore(gameApiClient.Object), new RemoteConfigReader(gameApiClient.Object));
    }

    void GivenStoredSnapshot(PlayerEconomySnapshot snapshot, string writeLock = "lock1")
    {
        var results = new List<Item>
        {
            new Item(k_PlayerEconomyKey, JObject.FromObject(snapshot), writeLock,
                new ModifiedMetadata(DateTime.UtcNow), new ModifiedMetadata(DateTime.UtcNow)),
        };
        GivenGetItemsResponse(new GetItemsResponse(results, new GetItemsResponseLinks("")));
    }

    void GivenNoStoredSnapshot()
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

    void GivenCatalog(string currenciesJson, string purchasesJson)
    {
        var settings = new Dictionary<string, object>
        {
            { "ECONOMY_CURRENCIES", JObject.Parse(currenciesJson) },
            { "ECONOMY_VIRTUAL_PURCHASES", JObject.Parse(purchasesJson) },
        };
        m_RemoteConfig
            .Setup(r => r.AssignSettingsGetAsync(It.IsAny<IExecutionContext>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(),
                It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response(new SettingsDeliveryResponse(new Configs(settings))));
    }

    static JObject ParseError(EconomyError error)
    {
        return JObject.Parse(error.Message);
    }

    // ApiResponse<T>.Data has a non-public setter, so responses are built via reflection.
    static ApiResponse<T> Response<T>(T data)
    {
        var response = new ApiResponse<T>();
        typeof(ApiResponse<T>).GetProperty(nameof(ApiResponse<T>.Data))!.SetValue(response, data);
        return response;
    }

    [Fact]
    public async Task ModifyBalance_MissingKey_CreatesWalletEntry()
    {
        GivenNoStoredSnapshot();

        var snapshot = await m_Service.ModifyBalance(m_Context.Object, "COIN", 25);

        Assert.Equal(25, snapshot.Wallet["COIN"]);
        var written = Assert.Single(m_Writes);
        Assert.Equal(k_PlayerEconomyKey, written.Key);
        Assert.Null(written.WriteLock);
    }

    [Fact]
    public async Task ModifyBalance_PassesStoredWriteLock()
    {
        GivenStoredSnapshot(new PlayerEconomySnapshot { Wallet = { ["COIN"] = 5 } }, "lock42");

        var snapshot = await m_Service.ModifyBalance(m_Context.Object, "COIN", -5);

        Assert.Equal(0, snapshot.Wallet["COIN"]);
        Assert.Equal("lock42", Assert.Single(m_Writes).WriteLock);
    }

    [Fact]
    public async Task ModifyBalance_NegativeResult_Throws1007AndDoesNotWrite()
    {
        GivenStoredSnapshot(new PlayerEconomySnapshot { Wallet = { ["COIN"] = 5 } });

        var error = await Assert.ThrowsAsync<EconomyError>(
            () => m_Service.ModifyBalance(m_Context.Object, "COIN", -6));

        Assert.Equal(EconomyError.ValidationStatus, ParseError(error).Value<int>("status"));
        Assert.Empty(m_Writes);
    }

    [Fact]
    public async Task ModifyInventoryItem_NegativeResult_Throws1007()
    {
        GivenNoStoredSnapshot();

        var error = await Assert.ThrowsAsync<EconomyError>(
            () => m_Service.ModifyInventoryItem(m_Context.Object, "SWORD", -1));

        Assert.Equal(EconomyError.ValidationStatus, ParseError(error).Value<int>("status"));
    }

    [Fact]
    public async Task SetBalance_OverwritesValue()
    {
        GivenStoredSnapshot(new PlayerEconomySnapshot { Wallet = { ["WATER"] = 123 } });

        var snapshot = await m_Service.SetBalance(m_Context.Object, "WATER", 1000);

        Assert.Equal(1000, snapshot.Wallet["WATER"]);
    }

    [Fact]
    public async Task MakeVirtualPurchase_UnknownId_Throws10504()
    {
        GivenCatalog("{}", "{}");
        GivenNoStoredSnapshot();

        var error = await Assert.ThrowsAsync<EconomyError>(
            () => m_Service.MakeVirtualPurchase(m_Context.Object, "NOT_A_PURCHASE"));

        Assert.Equal(EconomyError.PurchaseCostsNotMetStatus, ParseError(error).Value<int>("status"));
    }

    [Fact]
    public async Task MakeVirtualPurchase_CostsNotMet_Throws10504AndDoesNotWrite()
    {
        GivenCatalog(
            @"{ ""COIN"": { ""name"": ""Coin"" } }",
            @"{ ""BUY_SWORD"": { ""costs"": [ { ""id"": ""COIN"", ""amount"": 15 } ],
                                 ""rewards"": [ { ""id"": ""SWORD"", ""amount"": 1 } ] } }");
        GivenStoredSnapshot(new PlayerEconomySnapshot { Wallet = { ["COIN"] = 14 } });

        var error = await Assert.ThrowsAsync<EconomyError>(
            () => m_Service.MakeVirtualPurchase(m_Context.Object, "BUY_SWORD"));

        Assert.Equal(EconomyError.PurchaseCostsNotMetStatus, ParseError(error).Value<int>("status"));
        Assert.Empty(m_Writes);
    }

    [Fact]
    public async Task MakeVirtualPurchase_AppliesCostsAndRewardsAcrossWalletAndInventory()
    {
        GivenCatalog(
            @"{ ""COIN"": { ""name"": ""Coin"" }, ""PEARL"": { ""name"": ""Pearl"" } }",
            @"{ ""TRADE"": { ""costs"": [ { ""id"": ""COIN"", ""amount"": 10 }, { ""id"": ""SWORD"", ""amount"": 1 } ],
                             ""rewards"": [ { ""id"": ""PEARL"", ""amount"": 3 }, { ""id"": ""SHIELD"", ""amount"": 2 } ] } }");
        GivenStoredSnapshot(new PlayerEconomySnapshot
        {
            Wallet = { ["COIN"] = 10 },
            Inventory = { ["SWORD"] = 1 },
        });

        var snapshot = await m_Service.MakeVirtualPurchase(m_Context.Object, "TRADE");

        Assert.Equal(0, snapshot.Wallet["COIN"]);
        Assert.Equal(3, snapshot.Wallet["PEARL"]);
        Assert.Equal(0, snapshot.Inventory["SWORD"]);
        Assert.Equal(2, snapshot.Inventory["SHIELD"]);
        Assert.Single(m_Writes);
    }

    [Fact]
    public async Task WriteConflict_RetriesThenSucceeds()
    {
        GivenStoredSnapshot(new PlayerEconomySnapshot());

        var attempts = 0;
        m_CloudSave
            .Setup(c => c.SetProtectedItemAsync(It.IsAny<IExecutionContext>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<SetItemBody>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                attempts++;
                if (attempts < 3)
                    throw MakeConflictException();

                return Task.FromResult(Response(new SetItemResponse("newLock")));
            });

        var snapshot = await m_Service.ModifyBalance(m_Context.Object, "COIN", 1);

        Assert.Equal(3, attempts);
        Assert.Equal(1, snapshot.Wallet["COIN"]);
    }

    [Fact]
    public async Task WriteConflict_ExhaustsRetries_Throws409()
    {
        GivenStoredSnapshot(new PlayerEconomySnapshot());

        m_CloudSave
            .Setup(c => c.SetProtectedItemAsync(It.IsAny<IExecutionContext>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<SetItemBody>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(MakeConflictException());

        var error = await Assert.ThrowsAsync<EconomyError>(
            () => m_Service.ModifyBalance(m_Context.Object, "COIN", 1));

        Assert.Equal(EconomyError.ConflictStatus, ParseError(error).Value<int>("status"));
    }

    [Fact]
    public void EconomyError_MessageIsTheSharedStructuredJsonShape()
    {
        var error = ParseError(EconomyError.PurchaseCostsNotMet("BUY_SWORD"));

        Assert.Equal(10504, error.Value<int>("status"));
        Assert.Equal("EconomyPurchaseCostsNotMet", error.Value<string>("name"));
        Assert.Contains("BUY_SWORD", error.Value<string>("message"));
        Assert.True(error.ContainsKey("retryAfter"));
        Assert.True(error.ContainsKey("details"));
    }

    static ApiException MakeConflictException()
    {
        var response = new Mock<IApiResponse>();
        response.SetupGet(r => r.StatusCode).Returns(HttpStatusCode.Conflict);
        return new ApiException(ApiExceptionType.Http, "conflict", response.Object);
    }
}
