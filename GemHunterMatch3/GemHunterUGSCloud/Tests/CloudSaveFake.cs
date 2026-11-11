using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using GemHunterUGSCloud.Models;
using GemHunterUGSCloud.Services;
using Moq;
using Newtonsoft.Json.Linq;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Api;
using Unity.Services.CloudSave.Model;

namespace GemHunterUGSCloud.Tests;

/// <summary>
/// An in-memory stand-in for the player's protected Cloud Save keys, with the write-lock rules
/// the real service applies: every stored item carries a lock, a conditional write with a stale lock is a 409.
/// The mock is strict and answers only protected calls made with the service token, so a default-class call
/// or a call with the player's token fails the test.
/// </summary>
public class CloudSaveFake
{
    const string k_ServiceToken = "service-token";

    readonly Dictionary<string, (JToken Value, string WriteLock)> m_Stored = new();
    int m_LockCounter;

    public Mock<ICloudSaveDataApi> Api { get; } = new(MockBehavior.Strict);
    public Mock<IExecutionContext> Context { get; } = new();
    public List<SetItemBody> Writes { get; } = new();

    /// <summary>How many of the next writes answer 409 regardless of their lock.</summary>
    public int ConflictsToInject { get; set; }

    /// <summary>Runs right before each write lands, after the service has read; lets a test slip a concurrent change in.</summary>
    public Action? BeforeWrite { get; set; }

    public CloudSaveFake()
    {
        Context.SetupGet(c => c.ProjectId).Returns("project");
        Context.SetupGet(c => c.PlayerId).Returns("player");
        Context.SetupGet(c => c.EnvironmentId).Returns("environment");
        Context.SetupGet(c => c.AccessToken).Returns("player-token");
        Context.SetupGet(c => c.ServiceToken).Returns(k_ServiceToken);

        Api.Setup(c => c.GetProtectedItemsAsync(It.IsAny<IExecutionContext>(), k_ServiceToken, It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((IExecutionContext _, string _, string _, string _, List<string> keys, string _, CancellationToken _) =>
            {
                var results = keys.Where(m_Stored.ContainsKey)
                    .Select(k => new Item(k, m_Stored[k].Value.DeepClone(), m_Stored[k].WriteLock,
                        new ModifiedMetadata(DateTime.UtcNow), new ModifiedMetadata(DateTime.UtcNow)))
                    .ToList();
                return Task.FromResult(Response(new GetItemsResponse(results, new GetItemsResponseLinks(""))));
            });

        Api.Setup(c => c.SetProtectedItemAsync(It.IsAny<IExecutionContext>(), k_ServiceToken, It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<SetItemBody>(), It.IsAny<CancellationToken>()))
            .Returns((IExecutionContext _, string _, string _, string _, SetItemBody body, CancellationToken _) => Write(body));
    }

    Task<ApiResponse<SetItemResponse>> Write(SetItemBody body)
    {
        BeforeWrite?.Invoke();
        if (ConflictsToInject > 0)
        {
            ConflictsToInject--;
            throw MakeConflictException();
        }

        if (m_Stored.TryGetValue(body.Key, out var current) && body.WriteLock != null && body.WriteLock != current.WriteLock)
        {
            throw MakeConflictException();
        }

        var newLock = $"lock{++m_LockCounter}";
        m_Stored[body.Key] = (JToken.FromObject(body.Value), newLock);
        Writes.Add(body);
        return Task.FromResult(Response(new SetItemResponse(newLock)));
    }

    public IGameApiClient GameApiClient
    {
        get
        {
            var client = new Mock<IGameApiClient>();
            client.SetupGet(c => c.CloudSaveData).Returns(Api.Object);
            return client.Object;
        }
    }

    public PlayerEconomyRecord? StoredRecord =>
        m_Stored.TryGetValue(PlayerEconomyService.k_CloudSaveKey, out var item) ? item.Value.ToObject<PlayerEconomyRecord>() : null;

    public void Store(PlayerEconomyRecord record)
    {
        m_Stored[PlayerEconomyService.k_CloudSaveKey] = (JToken.FromObject(record), $"lock{++m_LockCounter}");
    }

    /// <summary>Overwrites the stored record behind the service's back, as another concurrent call would.</summary>
    public void StoreConcurrently(Action<PlayerEconomyRecord> change)
    {
        var record = StoredRecord ?? new PlayerEconomyRecord();
        change(record);
        Store(record);
    }

    // ApiResponse<T>.Data has a non-public setter, so responses are built via reflection.
    static ApiResponse<T> Response<T>(T data)
    {
        var response = new ApiResponse<T>();
        typeof(ApiResponse<T>).GetProperty(nameof(ApiResponse<T>.Data))!.SetValue(response, data);
        return response;
    }

    static ApiException MakeConflictException()
    {
        var response = new Mock<IApiResponse>();
        response.SetupGet(r => r.StatusCode).Returns(HttpStatusCode.Conflict);
        return new ApiException(ApiExceptionType.Http, "conflict", response.Object);
    }
}
