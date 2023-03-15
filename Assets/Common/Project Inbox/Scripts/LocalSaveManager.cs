using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Services.Samples.ProjectInbox
{
    public static class LocalSaveManager
    {
        public static List<string> playerIdsLocalCache { get; private set; }

        public static ProjectInboxState savedProjectInboxState
        {
            get => s_SavedProjectInboxState;
            private set => s_SavedProjectInboxState = value;
        }

        static ProjectInboxState s_SavedProjectInboxState;

        const string k_InboxStateKey = "ProjectInboxState";
        const string k_PastPlayerIdsKey = "ProjectInboxPastPlayerIds";

        public static void Initialize()
        {
            InitializeSavedProjectInboxState();
            InitializePlayerIdsLocalCache();
        }

        static void InitializeSavedProjectInboxState()
        {
            var inboxStateJson = PlayerPrefs.GetString(k_InboxStateKey, null);

            if (!string.IsNullOrEmpty(inboxStateJson))
            {
                savedProjectInboxState = JsonUtility.FromJson<ProjectInboxState>(inboxStateJson);
            }
            else
            {
                savedProjectInboxState = new ProjectInboxState
                {
                    messages = new List<InboxMessage>(),
                    lastMessageDownloadedId = ""
                };
            }
        }

        static void InitializePlayerIdsLocalCache()
        {
            var playerIdsJson = PlayerPrefs.GetString(k_PastPlayerIdsKey, null);

            if (!string.IsNullOrEmpty(playerIdsJson))
            {
                var pastPlayerIds = JsonUtility.FromJson<PastPlayerIds>(playerIdsJson);
                playerIdsLocalCache = pastPlayerIds.playerIds;
            }
            else
            {
                playerIdsLocalCache = new List<string>();
            }
        }

        public static void SaveProjectInboxData(List<InboxMessage> inboxMessages, string lastMessageDownloadedId)
        {
            s_SavedProjectInboxState.messages = inboxMessages;
            s_SavedProjectInboxState.lastMessageDownloadedId = lastMessageDownloadedId;

            var inboxStateJson = JsonUtility.ToJson(s_SavedProjectInboxState);
            PlayerPrefs.SetString(k_InboxStateKey, inboxStateJson);
            PlayerPrefs.Save();
        }

        public static void AddNewPlayerId(string playerId)
        {
            if (!playerIdsLocalCache.Contains(playerId))
            {
                playerIdsLocalCache.Add(playerId);
            }

            var pastPlayerIds = new PastPlayerIds
            {
                playerIds = playerIdsLocalCache
            };

            var playerIdsJson = JsonUtility.ToJson(pastPlayerIds);
            PlayerPrefs.SetString(k_PastPlayerIdsKey, playerIdsJson);
            PlayerPrefs.Save();
        }

        public struct ProjectInboxState
        {
            public List<InboxMessage> messages;
            public string lastMessageDownloadedId;
        }

        [Serializable]
        struct PastPlayerIds
        {
            public List<string> playerIds;
        }
    }
}
