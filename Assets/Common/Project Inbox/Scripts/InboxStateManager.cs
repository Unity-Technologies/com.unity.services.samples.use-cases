using System;
using System.Collections.Generic;
using System.Globalization;

namespace Unity.Services.Samples.ProjectInbox
{
    public static class InboxStateManager
    {
        public static List<InboxMessage> inboxMessages { get; private set; }
        public static int unreadMessageCount { get; private set; }

        static string s_LastMessageDownloadedId;

        public static void InitializeInboxState()
        {
            inboxMessages = LocalSaveManager.savedProjectInboxState.messages;
            s_LastMessageDownloadedId = LocalSaveManager.savedProjectInboxState.lastMessageDownloadedId;
            SetUnreadMessageCount();

            DownloadAllMessagesImages();
        }

        static void SetUnreadMessageCount()
        {
            unreadMessageCount = 0;

            foreach (var inboxMessage in inboxMessages)
            {
                if (!inboxMessage.metadata.isRead)
                {
                    unreadMessageCount++;
                }
            }
        }

        static void DownloadAllMessagesImages()
        {
            foreach (var inboxMessage in inboxMessages)
            {
                DownloadMessageImage(inboxMessage);
            }
        }

        static void DownloadMessageImage(InboxMessage inboxMessage)
        {
            AddressablesManager.instance.LoadImageForMessage(inboxMessage.messageInfo.addressablesAddress,
                inboxMessage.messageId);
        }

        public static bool UpdateInboxState()
        {
            var messageDeletedCount = DeleteExpiredMessages();
            var newMessageCount = CheckForNewMessages();
            var inboxStateChanged = messageDeletedCount > 0 || newMessageCount > 0;

            if (inboxStateChanged)
            {
                SetUnreadMessageCount();
                SaveInboxState();
            }

            return inboxStateChanged;
        }

        static int DeleteExpiredMessages()
        {
            var messagesDeletedCount = 0;
            var currentDateTime = DateTime.Now;

            for (var i = inboxMessages.Count - 1; i >= 0; i--)
            {
                var message = inboxMessages[i];
                if (DateTime.TryParse(message.metadata.expiresAtDateTime, out var expirationDateTime))
                {
                    if (IsMessageExpired(expirationDateTime, currentDateTime))
                    {
                        RemoveMessageAtLocation(i);
                        messagesDeletedCount++;
                    }
                }
            }

            return messagesDeletedCount;
        }

        static bool IsMessageExpired(DateTime expirationDateTime, DateTime currentDateTime)
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

        static void RemoveMessageAtLocation(int location)
        {
            var message = inboxMessages[location];
            inboxMessages.RemoveAt(location);
            AddressablesManager.instance.TryReleaseHandle(message.messageId);
        }

        static int CheckForNewMessages()
        {
            var numberOfMessagesToRequest = GetInboxSpaceRemaining();
            var startLocation = RemoteConfigManager.GetNextMessageLocation(s_LastMessageDownloadedId);
            var newMessages = GetNextMessages(startLocation, numberOfMessagesToRequest);

            if (newMessages == null || newMessages.Count == 0)
            {
                return 0;
            }

            foreach (var inboxMessage in newMessages)
            {
                inboxMessage.metadata =
                    TimeSpan.TryParse(inboxMessage.messageInfo.expirationPeriod, out var expirationTimeSpan)
                        ? new MessageMetadata(expirationTimeSpan)
                        : new MessageMetadata();

                DownloadMessageImage(inboxMessage);
                inboxMessages.Add(inboxMessage);
#if UNITY_EDITOR
                SamplesEditorAnalytics.SendProjectInboxMessageReceivedEvent(inboxMessage.messageId,
                    LocalSaveManager.playerIdsLocalCache);
#endif
            }

            s_LastMessageDownloadedId = inboxMessages[inboxMessages.Count - 1].messageId;

            return newMessages.Count;
        }

        static int GetInboxSpaceRemaining()
        {
            // If numberOfMessagesToRequest is -1, that indicates an infinite number of messages can be received.
            var numberOfMessagesToRequest = -1;

            if (ProjectInboxManager.maxInboxSize > 0)
            {
                var spaceForNewMessages = ProjectInboxManager.maxInboxSize - inboxMessages.Count;
                numberOfMessagesToRequest = spaceForNewMessages <= 0 ? 0 : spaceForNewMessages;
            }

            return numberOfMessagesToRequest;
        }

        static List<InboxMessage> GetNextMessages(int startLocation, int numberOfMessagesRequested)
        {
            var newMessages = new List<InboxMessage>();
            var currentLookupLocation = startLocation;

            // A 0 for numberOfMessagesRequested indicates an infinite number of messages can be returned, but while
            // loop will only proceed if Remote Config Manager has new messages available.
            while ((numberOfMessagesRequested == -1 || newMessages.Count < numberOfMessagesRequested) &&
                   RemoteConfigManager.TryFetchMessage(currentLookupLocation, out var message))
            {
                if (MessageIsValid(message))
                {
                    newMessages.Add(message);
                }

                currentLookupLocation++;
            }

            return newMessages;
        }

        static bool MessageIsValid(InboxMessage inboxMessage)
        {
            var messageInfo = inboxMessage?.messageInfo;

            if (messageInfo == null || string.IsNullOrEmpty(inboxMessage.messageId) ||
                string.IsNullOrEmpty(messageInfo.title) || string.IsNullOrEmpty(messageInfo.content) ||
                !IsMessageExpirationPeriodValid(messageInfo) || IsMessageOutOfDate(messageInfo))
            {
                return false;
            }

            return true;
        }

        static bool IsMessageExpirationPeriodValid(MessageInfo messageInfo)
        {
            // Valid message expirationPeriods include a blank string (i.e. no expirationPeriod) or a valid TimeSpan.
            if (string.IsNullOrEmpty(messageInfo.expirationPeriod) ||
                TimeSpan.TryParse(messageInfo.expirationPeriod, new CultureInfo("en-US"), out var timespan))
            {
                return true;
            }

            return false;
        }

        static bool IsMessageOutOfDate(MessageInfo messageInfo)
        {
            if (DateTime.TryParse(messageInfo.expirationDate, out var expirationDate))
            {
                return expirationDate <= DateTime.Now;
            }

            // If no expirationDate is provided, then the message should never be considered out of date.
            return false;
        }

        static void SaveInboxState()
        {
            LocalSaveManager.SaveProjectInboxData(inboxMessages, s_LastMessageDownloadedId);
        }

        public static void MarkMessageAsRead(string messageId)
        {
            var messageWasChanged = false;

            foreach (var message in inboxMessages)
            {
                if (!string.Equals(message.messageId, messageId))
                {
                    continue;
                }

                if (!message.metadata.isRead)
                {
                    message.metadata.isRead = true;
                    messageWasChanged = true;
                }

                break;
            }

            if (messageWasChanged)
            {
                SetUnreadMessageCount();
                SaveInboxState();
            }
        }

        public static void DeleteMessage(string messageId)
        {
            var messageWasDeleted = false;

            for (var i = inboxMessages.Count - 1; i >= 0; i--)
            {
                var message = inboxMessages[i];
                if (string.Equals(message.messageId, messageId))
                {
                    RemoveMessageAtLocation(i);
                    messageWasDeleted = true;
                    break;
                }
            }

            if (messageWasDeleted)
            {
                SaveInboxState();
            }
        }
    }
}
