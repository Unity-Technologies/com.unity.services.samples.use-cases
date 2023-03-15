using System;
using System.Globalization;

namespace Unity.Services.Samples.ProjectInbox
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
        public string expirationPeriod;
        public string expirationDate;
        public string addressablesAddress;
    }

    [Serializable]
    public class MessageMetadata
    {
        public string expiresAtDateTime;
        public bool isRead;

        public MessageMetadata()
        {
            expiresAtDateTime = "";
            isRead = false;
        }

        public MessageMetadata(TimeSpan expirationPeriod)
        {
            var expirationDateTime = DateTime.Now.Add(expirationPeriod);

            // "s" format for DateTime results in a string like "2008-10-31T17:04:32"
            expiresAtDateTime = expirationDateTime.ToString("s", CultureInfo.GetCultureInfo("en-US"));
            isRead = false;
        }
    }
}
