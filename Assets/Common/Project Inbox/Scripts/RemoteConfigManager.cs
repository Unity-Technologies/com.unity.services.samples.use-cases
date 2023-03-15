using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.RemoteConfig;
using UnityEngine;

namespace Unity.Services.Samples.ProjectInbox
{
    public static class RemoteConfigManager
    {
        public static int maxInboxCount { get; private set; }

        public static string ccdContentUrl { get; private set; }

        static List<string> s_OrderedMessageIds;

        const string k_MaxInboxCountKey = "PROJECT_INBOX_MAX_INBOX_COUNT";
        const string k_MessagesListKey = "PROJECT_INBOX_MESSAGES_LIST";
        const string k_CloudContentDeliveryUrlKey = "PROJECT_INBOX_CCD_CONTENT_URL";

        public static async Task FetchConfigs()
        {
            try
            {
                var appAttributes = new AppAttributes { versionNumber = Application.version };

                await RemoteConfigService.Instance.FetchConfigsAsync(new UserAttributes(), appAttributes);

                CacheConfigValues();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        static void CacheConfigValues()
        {
            maxInboxCount = RemoteConfigService.Instance.appConfig.GetInt(k_MaxInboxCountKey);

            ccdContentUrl = RemoteConfigService.Instance.appConfig.GetString(k_CloudContentDeliveryUrlKey);

            var json = RemoteConfigService.Instance.appConfig.GetJson(k_MessagesListKey, "");

            if (string.IsNullOrEmpty(json))
            {
                Debug.LogError($"Remote config key {k_MessagesListKey} cannot be found.");
                return;
            }

            var messageIds = JsonUtility.FromJson<MessageIds>(json);
            s_OrderedMessageIds = messageIds.messageList;
        }

        public static int GetNextMessageLocation(string lastMessageId = "")
        {
            if (s_OrderedMessageIds is null)
            {
                // A return value of -1 indicates there are no additional messages to fetch.
                return -1;
            }

            if (string.IsNullOrEmpty(lastMessageId))
            {
                return 0;
            }

            var startLocation = 0;
            for (var i = 0; i < s_OrderedMessageIds.Count; i++)
            {
                if (string.Equals(s_OrderedMessageIds[i], lastMessageId))
                {
                    startLocation = i + 1;
                    break;
                }
            }

            if (startLocation >= s_OrderedMessageIds.Count)
            {
                return -1;
            }

            return startLocation;
        }

        public static bool TryFetchMessage(int lookupLocation, out InboxMessage message)
        {
            message = null;

            if (lookupLocation < 0 || lookupLocation >= s_OrderedMessageIds.Count)
            {
                return false;
            }

            var messageId = s_OrderedMessageIds[lookupLocation];

            var json = RemoteConfigService.Instance.appConfig.GetJson(messageId, "");

            if (string.IsNullOrEmpty(json))
            {
                Debug.LogError($"Remote config key {messageId} cannot be found.");
                return false;
            }

            var messageInfo = JsonUtility.FromJson<MessageInfo>(json);

            if (messageInfo == null)
            {
                Debug.LogError($"Message format for Remote Config key {messageId} cannot be parsed.");
                return false;
            }

            message = new InboxMessage(messageId, messageInfo);
            return true;
        }

        struct UserAttributes { }

        struct AppAttributes
        {
            public string versionNumber;
        }

        [Serializable]
        struct MessageIds
        {
            public List<string> messageList;
        }
    }
}
