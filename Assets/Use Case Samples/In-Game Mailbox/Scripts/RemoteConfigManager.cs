using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Unity.Services.RemoteConfig;
using UnityEngine;

namespace UnityGamingServicesUseCases
{
    namespace InGameMailbox
    {
        public class RemoteConfigManager : MonoBehaviour
        {
            public static RemoteConfigManager instance { get; private set; }

            SampleAudience m_CurrentAudience = SampleAudience.Default;
            List<string> m_OrderedMessageIds;

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

            public async Task FetchConfigs()
            {
                try
                {
                    var userAttribute = new UserAttributes { audience = m_CurrentAudience.ToString() };

                    await RemoteConfigService.Instance.FetchConfigsAsync(userAttribute, new AppAttributes());

                    // Check that scene has not been unloaded while processing async wait to prevent throw.
                    if (this == null) return;

                    CacheConfigValues();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            void CacheConfigValues()
            {
                var json = RemoteConfigService.Instance.appConfig.GetJson("MESSAGES_ALL", "");

                if (string.IsNullOrEmpty(json))
                {
                    Debug.LogError("Remote config key \"MESSAGES_ALL\" cannot be found.");
                    return;
                }

                var messageIds = JsonUtility.FromJson<MessageIds>(json);
                m_OrderedMessageIds = messageIds.messageList;
            }

            public void UpdateAudienceType(SampleAudience newAudience)
            {
                m_CurrentAudience = newAudience;
            }

            public List<InboxMessage> GetNextMessages(int numberOfMessages, string lastMessageId = "")
            {
                if (m_OrderedMessageIds is null)
                {
                    return null;
                }

                if (string.IsNullOrEmpty(lastMessageId))
                {
                    return GetNextMessagesFromStartLocation(0, numberOfMessages);
                }

                for (var i = 0; i < m_OrderedMessageIds.Count; i++)
                {
                    if (string.Equals(m_OrderedMessageIds[i], lastMessageId) && i + 1 < m_OrderedMessageIds.Count)
                    {
                        return GetNextMessagesFromStartLocation(i + 1, numberOfMessages);
                    }
                }

                return null;
            }

            List<InboxMessage> GetNextMessagesFromStartLocation(int startLocation, int numberOfMessages)
            {
                var newMessages = new List<InboxMessage>();
                
                for (var i = startLocation; i < m_OrderedMessageIds.Count; i++)
                {
                    if (numberOfMessages > 0)
                    {
                        var message = FetchMessage(m_OrderedMessageIds[i]);
                        
                        // Some message values will be blank if the player does not fall into a targeted audience.
                        // We want to filter those messages out when downloading a specific number of messages.
                        if (MessageIsValid(message))
                        {
                            newMessages.Add(message);
                            numberOfMessages--;
                        }
                    }

                    if (numberOfMessages == 0)
                    {
                        break;
                    }
                }

                return newMessages;
            }

            InboxMessage FetchMessage(string messageId)
            {
                var json = RemoteConfigService.Instance.appConfig.GetJson(messageId, "");

                if (string.IsNullOrEmpty(json))
                {
                    Debug.LogError($"Remote config key {messageId} cannot be found.");
                    return new InboxMessage();
                }

                var message = JsonUtility.FromJson<MessageInfo>(json);

                return message == null
                    ? new InboxMessage()
                    : new InboxMessage(messageId, message);
            }
            
            bool MessageIsValid(InboxMessage inboxMessage)
            {
                var message = inboxMessage.messageInfo;

                if (string.IsNullOrEmpty(inboxMessage.messageId) || message == null ||
                    string.IsNullOrEmpty(message.title) || string.IsNullOrEmpty(message.content) ||
                    string.IsNullOrEmpty(message.expirationPeriod) || !TimeSpan.TryParse(message.expirationPeriod,
                        new CultureInfo("en-US"), out var timespan))
                {
                    return false;
                }

                return true;
            }

            void OnDestroy()
            {
                if (instance == this)
                {
                    instance = null;
                }
            }

            // Any values can be added to UserAttributes. These values can be used for Game Overrides
            // that use JEXL targeting.  Candidates for what you may want to pass in the UserAttributes
            // struct could be things like device type, however it is completely customizable.
            struct UserAttributes
            {
                public string audience;
            }

            // Even if there are no values you want to add to these fields, a non-nullable object must still be
            // passed to Remote Config's FetchConfigs call. Candidates for what you can pass in the AppAttributes
            // struct could be things like what level the player is on, or what version of the app is installed.
            // The candidates are completely customizable.
            struct AppAttributes
            {
            }

            public enum SampleAudience
            {
                Default,
                AllSpenders,
                UnengagedPlayers,
                FrenchSpeakers,
                NewPlayers
            }

            [Serializable]
            struct MessageIds
            {
                public List<string> messageList;
            }
        }
    }
}
