using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using SampleGameModule.Infrastructure;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Api;
using Unity.Services.CloudSave.Model;
using Xunit;

namespace SampleGameModule.Tests;

/// <summary>
/// The store keeps module-owned keys in the protected access class, written with the service token, so the
/// player can read them and cannot change them. The Cloud Save mock is strict: any other call fails the test.
/// </summary>
public class PlayerDataStoreTests
{
    const string k_ServiceToken = "service-token";
    const string k_PlayerToken = "player-token";

    readonly Mock<ICloudSaveDataApi> m_CloudSave = new(MockBehavior.Strict);
    readonly Mock<IExecutionContext> m_Context = new();
    readonly PlayerDataStore m_Store;

    public PlayerDataStoreTests()
    {
        m_Context.SetupGet(c => c.ProjectId).Returns("project");
        m_Context.SetupGet(c => c.PlayerId).Returns("player");
        m_Context.SetupGet(c => c.AccessToken).Returns(k_PlayerToken);
        m_Context.SetupGet(c => c.ServiceToken).Returns(k_ServiceToken);

        var gameApiClient = new Mock<IGameApiClient>();
        gameApiClient.SetupGet(c => c.CloudSaveData).Returns(m_CloudSave.Object);
        m_Store = new PlayerDataStore(gameApiClient.Object);
    }

    [Fact]
    public async Task GetMany_ReadsProtectedWithTheServiceToken()
    {
        m_CloudSave
            .Setup(c => c.GetProtectedItemsAsync(m_Context.Object, k_ServiceToken, "project", "player",
                new List<string> { "a", "b" }, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response(new GetItemsResponse(
                new List<Item> { new("a", 1, "lockA", new ModifiedMetadata(DateTime.UtcNow), new ModifiedMetadata(DateTime.UtcNow)) },
                new GetItemsResponseLinks(""))));

        var items = await m_Store.GetManyAsync(m_Context.Object, new[] { "a", "b" });

        Assert.Equal(1, items["a"].As<int>());
        Assert.Equal("lockA", items["a"].WriteLock);
        Assert.False(items["b"].Exists);
    }

    [Fact]
    public async Task Set_WritesProtectedWithTheServiceTokenAndTheWriteLock()
    {
        m_CloudSave
            .Setup(c => c.SetProtectedItemAsync(m_Context.Object, k_ServiceToken, "project", "player",
                It.Is<SetItemBody>(b => b.Key == "a" && (int)b.Value == 1 && b.WriteLock == "lockA"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response(new SetItemResponse("lockB")));

        await m_Store.SetAsync(m_Context.Object, "a", 1, "lockA");

        m_CloudSave.VerifyAll();
    }

    [Fact]
    public async Task SetMany_WritesProtectedWithTheServiceToken()
    {
        m_CloudSave
            .Setup(c => c.SetProtectedItemBatchAsync(m_Context.Object, k_ServiceToken, "project", "player",
                It.Is<SetItemBatchBody>(b => b.Data.Count == 2), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response(new SetItemBatchResponse(new List<SetItemBatchResponseResultsInner>())));

        await m_Store.SetManyAsync(m_Context.Object, new Dictionary<string, object> { { "a", 1 }, { "b", 2 } });

        m_CloudSave.VerifyAll();
    }

    [Fact]
    public async Task Delete_DeletesProtectedWithTheServiceToken()
    {
        m_CloudSave
            .Setup(c => c.DeleteProtectedItemAsync(m_Context.Object, k_ServiceToken, "a", "project", "player",
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse());

        await m_Store.DeleteAsync(m_Context.Object, "a");

        m_CloudSave.VerifyAll();
    }

    [Fact]
    public async Task PlayerWritten_ReadsAndWritesTheDefaultClassWithThePlayerToken()
    {
        m_CloudSave
            .Setup(c => c.GetItemsAsync(m_Context.Object, k_PlayerToken, "project", "player",
                new List<string> { "inbox" }, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response(new GetItemsResponse(new List<Item>(), new GetItemsResponseLinks(""))));
        m_CloudSave
            .Setup(c => c.SetItemAsync(m_Context.Object, k_PlayerToken, "project", "player",
                It.Is<SetItemBody>(b => b.Key == "inbox"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response(new SetItemResponse("lock")));

        Assert.False((await m_Store.GetPlayerWrittenAsync(m_Context.Object, "inbox")).Exists);
        await m_Store.SetPlayerWrittenAsync(m_Context.Object, "inbox", "{}");

        m_CloudSave.VerifyAll();
    }

    // ApiResponse<T>.Data has a non-public setter, so responses are built via reflection.
    static ApiResponse<T> Response<T>(T data)
    {
        var response = new ApiResponse<T>();
        typeof(ApiResponse<T>).GetProperty(nameof(ApiResponse<T>.Data))!.SetValue(response, data);
        return response;
    }
}
