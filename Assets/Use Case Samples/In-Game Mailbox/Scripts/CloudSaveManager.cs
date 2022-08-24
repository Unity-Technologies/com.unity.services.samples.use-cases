using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using UnityEngine;

namespace UnityGamingServicesUseCases
{
    namespace InGameMailbox
    {
        public class CloudSaveManager : MonoBehaviour
        {
            public static CloudSaveManager instance { get; private set; }

            public InGameMailboxSampleView sceneView;
            public List<InboxMessage> inboxMessages { get; private set; } = new List<InboxMessage>();

            string m_LastMessageDownloadedId;

            const string k_InboxStateKey = "MESSAGES_INBOX_STATE";
            const string k_LastMessageDownloadedKey = "MESSAGES_LAST_MESSAGE_DOWNLOADED_ID";

            void Awake()
            {
                if (instance != null && instance != this)
                {
                    Destroy(this);
                }
                else
                {
                    instance = this;
                }
            }

            public async Task FetchPlayerInbox()
            {
                try
                {
                    var cloudSaveData = await CloudSaveService.Instance.Data.LoadAllAsync();

                    // Check that scene has not been unloaded while processing async wait to prevent throw.
                    if (this == null) return;

                    if (cloudSaveData.ContainsKey(k_InboxStateKey))
                    {
                        var inbox = JsonUtility.FromJson<InboxState>(cloudSaveData[k_InboxStateKey]);
                        inboxMessages = inbox.messages;
                    }

                    m_LastMessageDownloadedId = cloudSaveData.ContainsKey(k_LastMessageDownloadedKey)
                        ? cloudSaveData[k_LastMessageDownloadedKey]
                        : "";
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            public int DeleteExpiredMessages()
            {
                var messagesDeletedCount = 0;
                var currentDateTime = DateTime.Now;

                for (var i = inboxMessages.Count - 1; i >= 0; i--)
                {
                    if (DateTime.TryParse(inboxMessages[i].metadata.expirationDate, out var expirationDateTime))
                    {
                        if (IsMessageExpired(expirationDateTime, currentDateTime))
                        {
                            inboxMessages.RemoveAt(i);
                            messagesDeletedCount++;
                        }
                    }
                }

                return messagesDeletedCount;
            }

            bool IsMessageExpired(DateTime expirationDateTime, DateTime currentDateTime)
            {
                // Could much more simply compare if (expirationDateTime <= currentDateTime), however we want the
                // messages to expire at the top of the minute, instead of at the correct second. i.e. if expiration
                // time is 2:43:35, and current time is 2:43:00 we want the message to be treated as expired.

                if (expirationDateTime.Date < currentDateTime.Date)
                {
                    return true;
                }

                if (expirationDateTime.Date == currentDateTime.Date)
                {
                    if (expirationDateTime.Hour < currentDateTime.Hour)
                    {
                        return true;
                    }

                    if (expirationDateTime.Hour == currentDateTime.Hour)
                    {
                        if (expirationDateTime.Minute <= currentDateTime.Minute)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            public void CheckForNewMessages()
            {
                var spaceForNewMessages = InGameMailboxSceneManager.maxInboxSize - inboxMessages.Count;
                if (spaceForNewMessages <= 0)
                {
                    return;
                }

                sceneView.StartFetchingNewMessagesSpinner();
                var newMessages =
                    RemoteConfigManager.instance.GetNextMessages(spaceForNewMessages, m_LastMessageDownloadedId);

                if (newMessages == null || newMessages.Count == 0)
                {
                    sceneView.StopFetchingNewMessagesSpinner();
                    return;
                }

                foreach (var inboxMessage in newMessages)
                {
                    var expirationPeriod = TimeSpan.Parse(inboxMessage.messageInfo.expirationPeriod);
                    var hasUnclaimedAttachment = !string.IsNullOrEmpty(inboxMessage.messageInfo.attachment);

                    inboxMessage.metadata = new MessageMetadata(expirationPeriod, hasUnclaimedAttachment);

                    inboxMessages.Add(inboxMessage);
                }

                m_LastMessageDownloadedId = inboxMessages[inboxMessages.Count - 1].messageId;
                sceneView.StopFetchingNewMessagesSpinner();
            }

            public async Task SavePlayerInboxInCloudSave()
            {
                try
                {
                    var inboxState = new InboxState
                    {
                        messages = inboxMessages
                    };
                    var inboxStateJson = JsonUtility.ToJson(inboxState);

                    var dataToSave = new Dictionary<string, object>
                    {
                        { k_LastMessageDownloadedKey, m_LastMessageDownloadedId },
                        { k_InboxStateKey, inboxStateJson }
                    };

                    await CloudSaveService.Instance.Data.ForceSaveAsync(dataToSave);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            public void MarkMessageAsRead(string messageId)
            {
                foreach (var message in inboxMessages)
                {
                    if (string.Equals(message.messageId, messageId))
                    {
                        message.metadata.isRead = true;
                        break;
                    }
                }
            }

            public void DeleteMessage(string messageId)
            {
                var oldMessageCount = inboxMessages.Count;

                foreach (var message in inboxMessages)
                {
                    if (string.Equals(message.messageId, messageId))
                    {
                        inboxMessages.Remove(message);
                        break;
                    }
                }

                if (oldMessageCount == InGameMailboxSceneManager.maxInboxSize)
                {
                    CheckForNewMessages();
                }
            }

            public int DeleteAllReadAndClaimedMessages()
            {
                var messagesDeletedCount = 0;
                var oldMessageCount = inboxMessages.Count;

                for (var i = inboxMessages.Count - 1; i >= 0; i--)
                {
                    var message = inboxMessages[i];
                    if (message.metadata.isRead && !message.metadata.hasUnclaimedAttachment)
                    {
                        inboxMessages.RemoveAt(i);
                        messagesDeletedCount++;
                    }
                }

                if (oldMessageCount == InGameMailboxSceneManager.maxInboxSize)
                {
                    CheckForNewMessages();
                }

                return messagesDeletedCount;
            }

            public async Task ResetCloudSaveData()
            {
                try
                {
                    m_LastMessageDownloadedId = "";
                    inboxMessages.Clear();

                    await Task.WhenAll(
                        CloudSaveService.Instance.Data.ForceDeleteAsync(k_LastMessageDownloadedKey),
                        CloudSaveService.Instance.Data.ForceDeleteAsync(k_InboxStateKey)
                    );
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            void OnDestroy()
            {
                if (instance == this)
                {
                    instance = null;
                }
            }

            [Serializable]
            struct InboxState
            {
                public List<InboxMessage> messages;
            }
        }
    }
}
