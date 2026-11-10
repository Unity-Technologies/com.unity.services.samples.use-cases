using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SampleGameModule.Infrastructure;
using SampleGameModule.Models;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;

namespace SampleGameModule.Services;

/// <summary>
/// In-Game Mailbox sample: claims message attachments as virtual purchases.
/// The inbox lives in the client-written Cloud Save key MESSAGES_INBOX_STATE as a JSON string, so the module
/// does not trust it for what to pay: a claim must name a message listed in the Remote Config MESSAGES_ALL
/// key, the attachment comes from that message's Remote Config entry for the player's audience, and claimed
/// message ids are recorded in MESSAGES_CLAIMED, which only this module writes. The inbox is updated for
/// display only; its message list is round-tripped as JObject so fields this service does not touch survive.
/// </summary>
public class InGameMailboxService
{
    const string k_InboxStateKey = "MESSAGES_INBOX_STATE";
    const string k_ClaimedKey = "MESSAGES_CLAIMED";
    const string k_MessageListConfigKey = "MESSAGES_ALL";
    const string k_DefaultAudience = "Default";

    readonly EconomyModuleService m_Economy;
    readonly PlayerDataStore m_Store;
    readonly RemoteConfigReader m_Config;

    public InGameMailboxService(EconomyModuleService economy, PlayerDataStore store, RemoteConfigReader config)
    {
        m_Economy = economy;
        m_Store = store;
        m_Config = config;
    }

    public class ClaimAllResult
    {
        [JsonProperty("processedTransactions")] public List<string> ProcessedTransactions { get; set; } = new();
    }

    /// <summary>
    /// Used by: In-Game Mailbox sample, CloudCodeManager.CallClaimMessageAttachmentEndpoint.
    /// Claims one message's attachment (a virtual purchase id) and marks it claimed in the inbox.
    /// The audience is the one the client fetched its messages for, so Game Overrides resolve the same way.
    /// </summary>
    [CloudCodeFunction("InGameMailbox_ClaimAttachment")]
    public async Task ClaimAttachment(IExecutionContext context, string messageId, string? audience = null)
    {
        var inbox = await ReadInboxAsync(context);
        var message = Messages(inbox).FirstOrDefault(m => m.Value<string>("messageId") == messageId)
            ?? throw new ModuleError(4, "InvalidArgumentError",
                "The message whose attachment is attempting to be claimed can't be found.");

        var attachments = await GetAttachmentsAsync(context, new[] { messageId }, audience);
        if (!attachments.TryGetValue(messageId, out var attachment))
            throw new ModuleError(4, "InvalidArgumentError", "Message does not have an attachment to claim.");

        var (claimed, writeLock) = await ReadClaimedAsync(context);
        if (claimed.Contains(messageId))
            throw AlreadyClaimed();

        await ClaimOnceAsync(context, claimed, writeLock, new[] { messageId }, new[] { attachment });

        message["metadata"]!["hasUnclaimedAttachment"] = false;
        await SaveInboxAsync(context, inbox);
    }

    /// <summary>
    /// Used by: In-Game Mailbox sample, CloudCodeManager.CallClaimAllMessageAttachmentsEndpoint.
    /// Claims every unclaimed attachment in one economy write, then marks those messages claimed and read.
    /// </summary>
    [CloudCodeFunction("InGameMailbox_ClaimAllAttachments")]
    public async Task<ClaimAllResult> ClaimAllAttachments(IExecutionContext context, string? audience = null)
    {
        var inbox = await ReadInboxAsync(context);
        var inboxIds = Messages(inbox).Select(m => m.Value<string>("messageId") ?? "").ToList();
        var attachments = await GetAttachmentsAsync(context, inboxIds, audience);
        var (claimed, writeLock) = await ReadClaimedAsync(context);

        var messageIdsToClaim = inboxIds.Where(id => attachments.ContainsKey(id) && !claimed.Contains(id)).ToList();
        if (messageIdsToClaim.Count == 0)
            throw new ModuleError(6, "NoClaimableAttachmentsError",
                "There are no messages with unclaimed attachments in the inbox.");

        // Port note: the script purchased one attachment per economy write; all-or-nothing in one write here.
        var processedTransactions = messageIdsToClaim.Select(id => attachments[id]).ToList();
        await ClaimOnceAsync(context, claimed, writeLock, messageIdsToClaim, processedTransactions);

        foreach (var message in Messages(inbox).Where(m => messageIdsToClaim.Contains(m.Value<string>("messageId") ?? "")))
        {
            message["metadata"]!["hasUnclaimedAttachment"] = false;
            message["metadata"]!["isRead"] = true;
        }

        await SaveInboxAsync(context, inbox);
        return new ClaimAllResult { ProcessedTransactions = processedTransactions };
    }

    /// <summary>
    /// Used by: In-Game Mailbox sample, CloudSaveManager.ResetCloudSaveData.
    /// Forgets every claim, so a reset inbox can claim its gifts again. A testing aid, like the Economy_* grants:
    /// remove it from a real game.
    /// </summary>
    [CloudCodeFunction("InGameMailbox_ResetForDebug")]
    public Task ResetForDebug(IExecutionContext context) => m_Store.DeleteAsync(context, k_ClaimedKey);

    /// <summary>
    /// The attachment of each given message that MESSAGES_ALL lists and that has one, as Remote Config
    /// serves it to the audience. Messages outside the list, or without an attachment, are left out.
    /// </summary>
    async Task<Dictionary<string, string>> GetAttachmentsAsync(IExecutionContext context,
        IReadOnlyCollection<string> messageIds, string? audience)
    {
        var userAttributes = new Dictionary<string, string>
        {
            { "audience", string.IsNullOrEmpty(audience) ? k_DefaultAudience : audience },
        };
        var config = await m_Config.GetAsync(context, messageIds.Append(k_MessageListConfigKey), userAttributes);
        var listed = config.GetJson(k_MessageListConfigKey)?["messageList"]?.ToObject<List<string>>()
            ?? throw new ModuleError(2, "RemoteConfigKeyMissingError", "Remote Config key MESSAGES_ALL is missing.");

        var attachments = new Dictionary<string, string>();
        foreach (var id in messageIds.Where(listed.Contains))
        {
            var attachment = config.GetJson(id)?.Value<string>("attachment") ?? "";
            if (attachment != "")
                attachments[id] = attachment;
        }

        return attachments;
    }

    async Task<(List<string> Claimed, string? WriteLock)> ReadClaimedAsync(IExecutionContext context)
    {
        var item = await m_Store.GetAsync(context, k_ClaimedKey);
        return (item.As<List<string>>() ?? new List<string>(), item.WriteLock);
    }

    /// <summary>
    /// Records the messages as claimed under the write lock of the read, then pays. Recording first means two
    /// concurrent claims of one message cannot both pay: the second write fails on the lock. When the payment
    /// fails, the record is put back so the player can retry.
    /// </summary>
    async Task ClaimOnceAsync(IExecutionContext context, List<string> claimed, string? writeLock,
        IReadOnlyCollection<string> messageIds, IEnumerable<string> purchaseIds)
    {
        try
        {
            await m_Store.SetAsync(context, k_ClaimedKey, claimed.Concat(messageIds).ToList(), writeLock);
        }
        catch (ApiException e) when (PlayerDataStore.IsWriteConflict(e))
        {
            throw AlreadyClaimed();
        }

        try
        {
            await m_Economy.MakeVirtualPurchasesAsync(context, purchaseIds);
        }
        catch
        {
            await m_Store.SetAsync(context, k_ClaimedKey, claimed);
            throw;
        }
    }

    static ModuleError AlreadyClaimed()
        => new(5, "AttachmentAlreadyClaimedError", "This message's attachment has already been claimed.");

    async Task<JObject> ReadInboxAsync(IExecutionContext context)
    {
        var item = await m_Store.GetPlayerWrittenAsync(context, k_InboxStateKey);
        if (!item.Exists)
            throw new ModuleError(3, "MissingCloudSaveDataError", "Cloud Save couldn't find the key \"MESSAGES_INBOX_STATE\".");
        return JObject.Parse(item.As<string>()!);
    }

    Task SaveInboxAsync(IExecutionContext context, JObject inbox)
    {
        return m_Store.SetPlayerWrittenAsync(context, k_InboxStateKey, inbox.ToString(Formatting.None));
    }

    static IEnumerable<JToken> Messages(JObject inbox) => inbox["messages"] as JArray ?? new JArray();
}
