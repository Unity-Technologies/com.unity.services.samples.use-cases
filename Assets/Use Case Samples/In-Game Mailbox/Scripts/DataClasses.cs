using System;
using System.Globalization;

namespace UnityGamingServicesUseCases
{
    namespace InGameMailbox
    {
        [Serializable]
        public class InboxMessage
        {
            // The messageId, which also serves as the Remote Config key for a given message.
            public string messageId;

            // MessageInfo is all the universal message data stored in Remote Config
            public MessageInfo messageInfo;

            // MessageMetadata is all the player-specific instance data for a given message, stored in Cloud Save.
            public MessageMetadata metadata;
            
            public InboxMessage(string messageId = "", MessageInfo messageInfo = null, MessageMetadata metadata = null)
            {
                this.messageId = messageId;
                this.messageInfo = messageInfo;
                this.metadata = metadata;
            }
        }

        [Serializable]
        public class MessageInfo
        {
            public string title;
            public string content;
            public string attachment;
            public string expirationPeriod;
        }

        [Serializable]
        public class MessageMetadata
        {
            public string expirationDate;
            public bool isRead;
            public bool hasUnclaimedAttachment;

            public MessageMetadata(TimeSpan expiration, bool hasUnclaimedAttachment = false)
            {
                var expirationDateTime = DateTime.Now.Add(expiration);
                // "s" format for DateTime results in a string like "2008-10-31T17:04:32"
                expirationDate = expirationDateTime.ToString("s", CultureInfo.GetCultureInfo("en-US"));
                this.hasUnclaimedAttachment = hasUnclaimedAttachment;
                isRead = false;
            }
        }

        public class ItemAndAmountSpec
        {
            public string id;
            public int amount;

            public ItemAndAmountSpec(string id, int amount)
            {
                this.id = id;
                this.amount = amount;
            }

            public override string ToString()
            {
                return $"{id}:{amount}";
            }
        }

    }
}
