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

public class InGameMailboxServiceTests
{
    const string k_InboxKey = "MESSAGES_INBOX_STATE";
    const string k_PlayerEconomyKey = "player_economy";
    const string k_ClaimedKey = "MESSAGES_CLAIMED";

    readonly Mock<ICloudSaveDataApi> m_CloudSave = new();
    readonly Mock<IRemoteConfigSettingsApi> m_RemoteConfig = new();
    readonly Mock<IExecutionContext> m_Context = new();
    // The inbox is player-written (default access class); claims and the economy are protected.
    readonly Dictionary<string, object> m_PlayerWritten = new();
    readonly Dictionary<string, object> m_Protected = new();
    readonly List<SetItemBody> m_Writes = new();
    readonly Dictionary<string, object> m_Settings = new();
    readonly List<SettingsDeliveryRequest> m_ConfigRequests = new();
    readonly InGameMailboxService m_Service;

    public InGameMailboxServiceTests()
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
            .Setup(c => c.GetItemsAsync(It.IsAny<IExecutionContext>(), "token", It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((IExecutionContext _, string _, string _, string _, List<string> keys, string _, CancellationToken _) =>
                Task.FromResult(Read(m_PlayerWritten, keys)));
        m_CloudSave
            .Setup(c => c.SetItemAsync(It.IsAny<IExecutionContext>(), "token", It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<SetItemBody>(), It.IsAny<CancellationToken>()))
            .Returns((IExecutionContext _, string _, string _, string _, SetItemBody body, CancellationToken _) =>
                Task.FromResult(Write(m_PlayerWritten, body)));

        m_CloudSave
            .Setup(c => c.GetProtectedItemsAsync(It.IsAny<IExecutionContext>(), "service-token", It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((IExecutionContext _, string _, string _, string _, List<string> keys, string _, CancellationToken _) =>
                Task.FromResult(Read(m_Protected, keys)));
        m_CloudSave
            .Setup(c => c.SetProtectedItemAsync(It.IsAny<IExecutionContext>(), "service-token", It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<SetItemBody>(), It.IsAny<CancellationToken>()))
            .Returns((IExecutionContext _, string _, string _, string _, SetItemBody body, CancellationToken _) =>
                Task.FromResult(Write(m_Protected, body)));
        m_CloudSave
            .Setup(c => c.DeleteProtectedItemAsync(It.IsAny<IExecutionContext>(), "service-token", It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((IExecutionContext _, string _, string key, string _, string _, string _, CancellationToken _) =>
                m_Protected.Remove(key))
            .ReturnsAsync(new ApiResponse());

        GivenCatalog(
            @"{ ""COIN"": { ""name"": ""Coin"" } }",
            @"{ ""GIFT_A"": { ""costs"": [], ""rewards"": [ { ""id"": ""COIN"", ""amount"": 10 } ] },
                ""GIFT_B"": { ""costs"": [], ""rewards"": [ { ""id"": ""SWORD"", ""amount"": 1 } ] } }");

        // The economy catalog read (no user attributes) and the message read (with the audience) share one settings map.
        m_RemoteConfig
            .Setup(r => r.AssignSettingsAsync(It.IsAny<IExecutionContext>(), It.IsAny<string>(),
                It.IsAny<SettingsDeliveryRequest>(), It.IsAny<CancellationToken>()))
            .Callback((IExecutionContext _, string _, SettingsDeliveryRequest request, CancellationToken _) =>
                m_ConfigRequests.Add(request))
            .ReturnsAsync(() => Response(new SettingsDeliveryResponse(new Configs(m_Settings))));

        var store = new PlayerDataStore(gameApiClient.Object);
        var config = new RemoteConfigReader(gameApiClient.Object);
        var economy = new EconomyModuleService(NullLogger<EconomyModuleService>.Instance, store, config);
        m_Service = new InGameMailboxService(economy, store, config);
    }

    void GivenCatalog(string currenciesJson, string purchasesJson)
    {
        m_Settings["ECONOMY_CURRENCIES"] = JObject.Parse(currenciesJson);
        m_Settings["ECONOMY_VIRTUAL_PURCHASES"] = JObject.Parse(purchasesJson);
        m_RemoteConfig
            .Setup(r => r.AssignSettingsGetAsync(It.IsAny<IExecutionContext>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(),
                It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Response(new SettingsDeliveryResponse(new Configs(m_Settings))));
    }

    /// <summary>The messages Remote Config serves: MESSAGES_ALL lists them, each entry carries its attachment.</summary>
    void GivenRemoteConfigMessages(params (string id, string attachment)[] messages)
    {
        m_Settings["MESSAGES_ALL"] = new JObject { ["messageList"] = new JArray(messages.Select(m => m.id)) };
        foreach (var (id, attachment) in messages)
            m_Settings[id] = new JObject { ["title"] = "t", ["content"] = "c", ["attachment"] = attachment };
    }

    void GivenClaimed(params string[] messageIds) => m_Protected[k_ClaimedKey] = new JArray(messageIds);

    List<string> StoredClaimed() => JArray.FromObject(m_Protected[k_ClaimedKey]).ToObject<List<string>>()!;

    static ApiResponse<GetItemsResponse> Read(Dictionary<string, object> store, List<string> keys)
    {
        var results = keys.Where(store.ContainsKey)
            .Select(k => new Item(k, store[k], "lock1",
                new ModifiedMetadata(DateTime.UtcNow), new ModifiedMetadata(DateTime.UtcNow)))
            .ToList();
        return Response(new GetItemsResponse(results, new GetItemsResponseLinks("")));
    }

    ApiResponse<SetItemResponse> Write(Dictionary<string, object> store, SetItemBody body)
    {
        m_Writes.Add(body);
        store[body.Key] = body.Value;
        return Response(new SetItemResponse("newLock"));
    }

    // The inbox is stored the way the client writes it: a JSON string.
    void GivenInbox(params JObject[] messages)
    {
        m_PlayerWritten[k_InboxKey] = new JObject { ["messages"] = new JArray(messages) }.ToString();
    }

    static JObject Message(string id, string attachment, bool hasUnclaimedAttachment)
    {
        return new JObject
        {
            ["messageId"] = id,
            ["messageInfo"] = new JObject
            {
                ["title"] = "t", ["content"] = "c", ["attachment"] = attachment, ["expirationPeriod"] = "1.00:00:00",
            },
            ["metadata"] = new JObject
            {
                ["expirationDate"] = "2099-01-01T00:00:00", ["isRead"] = false,
                ["hasUnclaimedAttachment"] = hasUnclaimedAttachment,
            },
        };
    }

    JObject StoredInbox() => JObject.Parse((string)m_PlayerWritten[k_InboxKey]);

    JToken StoredMessage(string id) => StoredInbox()["messages"]!.First(m => m.Value<string>("messageId") == id);

    static JObject ParseError(ModuleError error) => JObject.Parse(error.Message);

    // ApiResponse<T>.Data has a non-public setter, so responses are built via reflection.
    static ApiResponse<T> Response<T>(T data)
    {
        var response = new ApiResponse<T>();
        typeof(ApiResponse<T>).GetProperty(nameof(ApiResponse<T>.Data))!.SetValue(response, data);
        return response;
    }

    [Fact]
    public async Task ClaimAttachment_MissingInbox_Throws3()
    {
        var error = await Assert.ThrowsAsync<ModuleError>(
            () => m_Service.ClaimAttachment(m_Context.Object, "m1"));

        Assert.Equal(3, ParseError(error).Value<int>("status"));
        Assert.Equal("MissingCloudSaveDataError", ParseError(error).Value<string>("name"));
    }

    [Fact]
    public async Task ClaimAttachment_AlreadyClaimed_Throws5AndDoesNotWrite()
    {
        GivenRemoteConfigMessages(("m1", "GIFT_A"));
        GivenInbox(Message("m1", "GIFT_A", hasUnclaimedAttachment: true));
        GivenClaimed("m1");

        var error = await Assert.ThrowsAsync<ModuleError>(
            () => m_Service.ClaimAttachment(m_Context.Object, "m1"));

        Assert.Equal(5, ParseError(error).Value<int>("status"));
        Assert.Equal("AttachmentAlreadyClaimedError", ParseError(error).Value<string>("name"));
        Assert.Empty(m_Writes);
    }

    [Fact]
    public async Task ClaimAttachment_UnknownMessage_Throws4()
    {
        GivenRemoteConfigMessages(("m1", "GIFT_A"));
        GivenInbox(Message("m1", "GIFT_A", hasUnclaimedAttachment: true));

        var error = await Assert.ThrowsAsync<ModuleError>(
            () => m_Service.ClaimAttachment(m_Context.Object, "nope"));

        Assert.Equal(4, ParseError(error).Value<int>("status"));
    }

    [Fact]
    public async Task ClaimAttachment_Unclaimed_PurchasesAndMarksClaimed()
    {
        GivenRemoteConfigMessages(("m1", "GIFT_A"));
        GivenInbox(Message("m1", "GIFT_A", hasUnclaimedAttachment: true));

        await m_Service.ClaimAttachment(m_Context.Object, "m1");

        var economy = Assert.Single(m_Writes, w => w.Key == k_PlayerEconomyKey);
        Assert.Equal(10, JObject.FromObject(economy.Value)["wallet"]!.Value<long>("COIN"));
        Assert.Equal(new[] { "m1" }, StoredClaimed());
        Assert.IsType<string>(Assert.Single(m_Writes, w => w.Key == k_InboxKey).Value);
        Assert.False(StoredMessage("m1")["metadata"]!.Value<bool>("hasUnclaimedAttachment"));
        Assert.False(StoredMessage("m1")["metadata"]!.Value<bool>("isRead"));
    }

    [Fact]
    public async Task ClaimAttachment_ForgedAttachment_PaysTheRemoteConfigAttachment()
    {
        GivenRemoteConfigMessages(("m1", "GIFT_A"));
        GivenInbox(Message("m1", "GIFT_B", hasUnclaimedAttachment: true));

        await m_Service.ClaimAttachment(m_Context.Object, "m1");

        var snapshot = JObject.FromObject(Assert.Single(m_Writes, w => w.Key == k_PlayerEconomyKey).Value);
        Assert.Equal(10, snapshot["wallet"]!.Value<long>("COIN"));
        Assert.Null(snapshot["inventory"]!["SWORD"]);
    }

    [Fact]
    public async Task ClaimAttachment_MessageNotInRemoteConfig_Throws4AndPaysNothing()
    {
        GivenRemoteConfigMessages(("m1", "GIFT_A"));
        GivenInbox(Message("forged", "GIFT_A", hasUnclaimedAttachment: true));

        var error = await Assert.ThrowsAsync<ModuleError>(
            () => m_Service.ClaimAttachment(m_Context.Object, "forged"));

        Assert.Equal(4, ParseError(error).Value<int>("status"));
        Assert.Empty(m_Writes);
    }

    [Fact]
    public async Task ClaimAttachment_MessageWithoutRemoteConfigAttachment_Throws4AndPaysNothing()
    {
        GivenRemoteConfigMessages(("m1", ""));
        GivenInbox(Message("m1", "GIFT_A", hasUnclaimedAttachment: true));

        var error = await Assert.ThrowsAsync<ModuleError>(
            () => m_Service.ClaimAttachment(m_Context.Object, "m1"));

        Assert.Equal(4, ParseError(error).Value<int>("status"));
        Assert.Empty(m_Writes);
    }

    [Fact]
    public async Task ClaimAttachment_SameMessageTwice_PaysOnce()
    {
        GivenRemoteConfigMessages(("m1", "GIFT_A"));
        GivenInbox(Message("m1", "GIFT_A", hasUnclaimedAttachment: true));
        await m_Service.ClaimAttachment(m_Context.Object, "m1");

        // The client rewrites its inbox as unclaimed; the claim record still refuses.
        GivenInbox(Message("m1", "GIFT_A", hasUnclaimedAttachment: true));
        var error = await Assert.ThrowsAsync<ModuleError>(
            () => m_Service.ClaimAttachment(m_Context.Object, "m1"));

        Assert.Equal(5, ParseError(error).Value<int>("status"));
        Assert.Single(m_Writes, w => w.Key == k_PlayerEconomyKey);
    }

    [Fact]
    public async Task ClaimAttachment_SendsTheAudienceToRemoteConfig()
    {
        GivenRemoteConfigMessages(("m1", "GIFT_A"));
        GivenInbox(Message("m1", "GIFT_A", hasUnclaimedAttachment: true));

        await m_Service.ClaimAttachment(m_Context.Object, "m1", "NewPlayers");

        Assert.Equal("NewPlayers", Assert.Single(m_ConfigRequests).Attributes.User["audience"]);
    }

    [Fact]
    public async Task ClaimAttachment_NoAudience_AsksForTheDefaultAudience()
    {
        GivenRemoteConfigMessages(("m1", "GIFT_A"));
        GivenInbox(Message("m1", "GIFT_A", hasUnclaimedAttachment: true));

        await m_Service.ClaimAttachment(m_Context.Object, "m1");

        Assert.Equal("Default", Assert.Single(m_ConfigRequests).Attributes.User["audience"]);
    }

    [Fact]
    public async Task ClaimAttachment_PaymentFails_ReleasesTheClaim()
    {
        GivenRemoteConfigMessages(("m1", "NOT_A_PURCHASE"));
        GivenInbox(Message("m1", "NOT_A_PURCHASE", hasUnclaimedAttachment: true));

        await Assert.ThrowsAsync<EconomyError>(() => m_Service.ClaimAttachment(m_Context.Object, "m1"));

        Assert.Empty(StoredClaimed());
        Assert.DoesNotContain(m_Writes, w => w.Key == k_PlayerEconomyKey);
    }

    [Fact]
    public async Task ResetForDebug_ForgetsEveryClaim()
    {
        GivenClaimed("m1");

        await m_Service.ResetForDebug(m_Context.Object);

        Assert.False(m_Protected.ContainsKey(k_ClaimedKey));
    }

    [Fact]
    public async Task ClaimAllAttachments_NothingToClaim_Throws6()
    {
        GivenRemoteConfigMessages(("m1", "GIFT_A"));
        GivenInbox(Message("m1", "GIFT_A", hasUnclaimedAttachment: true));
        GivenClaimed("m1");

        var error = await Assert.ThrowsAsync<ModuleError>(
            () => m_Service.ClaimAllAttachments(m_Context.Object));

        Assert.Equal(6, ParseError(error).Value<int>("status"));
        Assert.Equal("NoClaimableAttachmentsError", ParseError(error).Value<string>("name"));
    }

    [Fact]
    public async Task ClaimAllAttachments_ClaimsEveryUnclaimedInOneEconomyWrite()
    {
        GivenRemoteConfigMessages(("m1", "GIFT_A"), ("m2", "GIFT_B"), ("m3", "GIFT_B"));
        GivenInbox(
            Message("m1", "GIFT_A", hasUnclaimedAttachment: true),
            Message("m2", "GIFT_B", hasUnclaimedAttachment: false),
            Message("m3", "GIFT_B", hasUnclaimedAttachment: true));
        GivenClaimed("m2");

        var result = await m_Service.ClaimAllAttachments(m_Context.Object);

        Assert.Equal(new[] { "GIFT_A", "GIFT_B" }, result.ProcessedTransactions);
        var economy = Assert.Single(m_Writes, w => w.Key == k_PlayerEconomyKey);
        var snapshot = JObject.FromObject(economy.Value);
        Assert.Equal(10, snapshot["wallet"]!.Value<long>("COIN"));
        Assert.Equal(1, snapshot["inventory"]!.Value<long>("SWORD"));
        foreach (var id in new[] { "m1", "m3" })
        {
            Assert.False(StoredMessage(id)["metadata"]!.Value<bool>("hasUnclaimedAttachment"));
            Assert.True(StoredMessage(id)["metadata"]!.Value<bool>("isRead"));
        }

        Assert.False(StoredMessage("m2")["metadata"]!.Value<bool>("isRead"));
        Assert.Equal("t", StoredMessage("m2")["messageInfo"]!.Value<string>("title"));
        Assert.Equal(new[] { "m2", "m1", "m3" }, StoredClaimed());
    }

    [Fact]
    public async Task ClaimAllAttachments_ForgedMessages_PayOnlyRemoteConfigAttachments()
    {
        GivenRemoteConfigMessages(("m1", "GIFT_A"));
        GivenInbox(
            Message("m1", "GIFT_B", hasUnclaimedAttachment: true),
            Message("forged", "GIFT_B", hasUnclaimedAttachment: true));

        var result = await m_Service.ClaimAllAttachments(m_Context.Object);

        Assert.Equal(new[] { "GIFT_A" }, result.ProcessedTransactions);
        Assert.Equal(new[] { "m1" }, StoredClaimed());
    }
}
