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
using Xunit;

namespace SampleGameModule.Tests;

public class LootBoxesWithCooldownServiceTests
{
    const string k_GrantRewardTimeKey = "LOOT_BOX_COOLDOWN_GRANT_REWARD_TIME";
    static readonly string[] k_CurrencyIds = { "COIN", "GEM", "PEARL", "STAR" };
    static readonly string[] k_InventoryItemIds = { "SWORD", "SHIELD" };

    readonly Mock<ICloudSaveDataApi> m_CloudSave = new();
    readonly Mock<IExecutionContext> m_Context = new();
    readonly LootBoxesWithCooldownService m_Service;
    readonly Dictionary<string, object> m_Stored = new();
    readonly List<SetItemBody> m_Writes = new();

    public LootBoxesWithCooldownServiceTests()
    {
        m_Context.SetupGet(c => c.ProjectId).Returns("project");
        m_Context.SetupGet(c => c.PlayerId).Returns("player");
        m_Context.SetupGet(c => c.EnvironmentId).Returns("environment");
        m_Context.SetupGet(c => c.AccessToken).Returns("token");
        m_Context.SetupGet(c => c.ServiceToken).Returns("service-token");

        var gameApiClient = new Mock<IGameApiClient>();
        gameApiClient.SetupGet(c => c.CloudSaveData).Returns(m_CloudSave.Object);

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

        var store = new PlayerDataStore(gameApiClient.Object);
        var economy = new EconomyModuleService(NullLogger<EconomyModuleService>.Instance, store,
            new RemoteConfigReader(gameApiClient.Object));
        m_Service = new LootBoxesWithCooldownService(economy, store, NullLogger<LootBoxesWithCooldownService>.Instance);
    }

    void GivenLastGrantSecondsAgo(long secondsAgo)
    {
        m_Stored[k_GrantRewardTimeKey] = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - secondsAgo;
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
    public async Task GetStatus_NeverClaimed_CanGrant()
    {
        var status = await m_Service.GetStatus(m_Context.Object);

        Assert.True(status.CanGrantFlag);
        Assert.Equal(0, status.GrantCooldown);
        Assert.Equal(60, status.DefaultCooldown);
    }

    [Fact]
    public async Task GetStatus_WithinCooldown_ReportsRemainingSeconds()
    {
        GivenLastGrantSecondsAgo(10);

        var status = await m_Service.GetStatus(m_Context.Object);

        Assert.False(status.CanGrantFlag);
        Assert.InRange(status.GrantCooldown, 49, 50);
    }

    [Fact]
    public async Task GetStatus_CooldownExpired_CanGrant()
    {
        GivenLastGrantSecondsAgo(120);

        var status = await m_Service.GetStatus(m_Context.Object);

        Assert.True(status.CanGrantFlag);
        Assert.Equal(0, status.GrantCooldown);
    }

    [Fact]
    public async Task Claim_WithinCooldown_ThrowsStatus1AndWritesNothing()
    {
        GivenLastGrantSecondsAgo(10);

        var error = await Assert.ThrowsAsync<ModuleError>(() => m_Service.Claim(m_Context.Object));

        var parsed = ParseError(error);
        Assert.Equal(1, parsed.Value<int>("status"));
        Assert.Equal("CloudCodeCustomError", parsed.Value<string>("name"));
        Assert.Equal("The player tried to claim a Loot Box before the cooldown timer expired.", parsed.Value<string>("message"));
        Assert.Empty(m_Writes);
    }

    [Fact]
    public async Task Claim_CooldownExpired_WritesTimestampThenOneEconomyGrant()
    {
        GivenLastGrantSecondsAgo(61);

        var result = await m_Service.Claim(m_Context.Object);

        Assert.Equal(2, m_Writes.Count);
        Assert.Equal(k_GrantRewardTimeKey, m_Writes[0].Key);
        Assert.InRange(Convert.ToInt64(m_Writes[0].Value), DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 5,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        Assert.Equal(EconomyModuleService.PlayerEconomyKey, m_Writes[1].Key);

        Assert.Equal(2, result.CurrencyId.Count);
        Assert.NotEqual(result.CurrencyId[0], result.CurrencyId[1]);
        Assert.All(result.CurrencyId, id => Assert.Contains(id, k_CurrencyIds));
        Assert.All(result.CurrencyQuantity, q => Assert.InRange(q, 1, 5));
        Assert.Contains(Assert.Single(result.InventoryItemId), k_InventoryItemIds);
        Assert.InRange(Assert.Single(result.InventoryItemQuantity), 1, 2);

        var snapshot = JObject.FromObject(m_Writes[1].Value);
        Assert.Equal(result.CurrencyQuantity[0], snapshot["wallet"]!.Value<long>(result.CurrencyId[0]));
        Assert.Equal(result.InventoryItemQuantity[0], snapshot["inventory"]!.Value<long>(result.InventoryItemId[0]));
    }
}
