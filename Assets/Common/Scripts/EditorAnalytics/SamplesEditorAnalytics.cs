#if UNITY_EDITOR

// NOTE:
// You can disable all editor analytics in Unity > Preferences.
// The EditorAnalytics API is for Unity to collect usage data for this samples project in
// order to improve our products. The EditorAnalytics API won't be useful in your own project
// because it only works in the Editor and the data is only sent to Unity. To see how you
// could implement analytics in your own project, have a look at
// the AB Test Level Difficulty sample or the Seasonal Events sample.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Unity.Services.Samples
{
    [InitializeOnLoad]
    public static class SamplesEditorAnalytics
    {
        [Serializable]
        struct SceneOpenedData
        {
            public string sceneName;
        }

        [Serializable]
        struct PlayModeButtonPressedData
        {
            public string buttonName;
        }

        [Serializable]
        struct SceneTotalSessionLengthData
        {
            public string sceneName;
            public int sessionLengthSeconds;
        }

        [Serializable]
        struct ProjectInboxMessageData
        {
            public string messageId;
            public List<string> playerIds;
        }

        const int k_WaitForEnabledMilliseconds = 500;

        const int k_MaxEventsPerHour = 3600;
        const int k_MaxItems = 10;
        const string k_VendorKey = "unity.gamingservicessamples";

        const string k_Prefix = "gameOperationsSamples";
        const string k_SceneOpenedEvent = k_Prefix + "SceneOpened";
        const int k_SceneOpenedEventVersion = 2;
        const string k_ButtonPressedInPlayModeEvent = k_Prefix + "ButtonPressedInPlayMode";
        const int k_ButtonPressedInPlayModeEventVersion = 2;
        const string k_SceneTotalSessionLengthEvent = k_Prefix + "SceneTotalSessionLength";
        const int k_SceneTotalSessionLengthEventVersion = 2;
        const string k_ProjectInboxMessageReceivedEvent = k_Prefix + "InboxMessageReceived";
        const int k_ProjectInboxMessageReceivedEventVersion = 2;
        const string k_ProjectInboxMessageOpenedEvent = k_Prefix + "InboxMessageOpened";
        const int k_ProjectInboxMessageOpenedEventVersion = 1;

        static string m_CurrentSceneName;
        static DateTime m_CurrentSceneSessionCheckpoint;

        static SamplesEditorAnalytics()
        {
            m_CurrentSceneName = SceneManager.GetActiveScene().name;
            m_CurrentSceneSessionCheckpoint = DateTime.Now;

            SceneManager.sceneLoaded += OnSceneLoaded;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.wantsToQuit += OnEditorWantsToQuit;

// disable the warning that we aren't awaiting this call
#pragma warning disable 4014

            RegisterEvents();

#pragma warning restore 4014
        }

        static async Task RegisterEvents()
        {
            var timeout = 0;

            while (!EditorAnalytics.enabled)
            {
                await Task.Delay(k_WaitForEnabledMilliseconds);
                timeout += k_WaitForEnabledMilliseconds;

                if (timeout >= 30000)
                {
                    // let's stop trying after 30 seconds because the editor user might have disabled EditorAnalytics
                    return;
                }
            }

            EditorAnalytics.RegisterEventWithLimit(
                k_ButtonPressedInPlayModeEvent,
                k_MaxEventsPerHour,
                k_MaxItems,
                k_VendorKey,
                k_ButtonPressedInPlayModeEventVersion);

            EditorAnalytics.RegisterEventWithLimit(
                k_SceneOpenedEvent,
                k_MaxEventsPerHour,
                k_MaxItems,
                k_VendorKey,
                k_SceneOpenedEventVersion);

            EditorAnalytics.RegisterEventWithLimit(
                k_SceneTotalSessionLengthEvent,
                k_MaxEventsPerHour,
                k_MaxItems,
                k_VendorKey,
                k_SceneTotalSessionLengthEventVersion);

            EditorAnalytics.RegisterEventWithLimit(
                k_ProjectInboxMessageReceivedEvent,
                k_MaxEventsPerHour,
                k_MaxItems,
                k_VendorKey,
                k_ProjectInboxMessageReceivedEventVersion);

            EditorAnalytics.RegisterEventWithLimit(
                k_ProjectInboxMessageOpenedEvent,
                k_MaxEventsPerHour,
                k_MaxItems,
                k_VendorKey,
                k_ProjectInboxMessageOpenedEventVersion);
        }

        public static void SendButtonPressedInPlayModeEvent(string buttonName)
        {
            EditorAnalytics.SendEventWithLimit(
                k_ButtonPressedInPlayModeEvent,
                new PlayModeButtonPressedData { buttonName = buttonName },
                k_ButtonPressedInPlayModeEventVersion);
        }

        public static void SendProjectInboxMessageReceivedEvent(string messageId, List<string> playerIds)
        {
            EditorAnalytics.SendEventWithLimit(
                k_ProjectInboxMessageReceivedEvent,
                new ProjectInboxMessageData { messageId = messageId, playerIds = playerIds },
                k_ProjectInboxMessageReceivedEventVersion);
        }

        public static void SendProjectInboxMessageOpenedEvent(string messageId, List<string> playerIds)
        {
            EditorAnalytics.SendEventWithLimit(
                k_ProjectInboxMessageOpenedEvent,
                new ProjectInboxMessageData { messageId = messageId, playerIds = playerIds },
                k_ProjectInboxMessageOpenedEventVersion);
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
        {
            SendSceneSessionLengthEvent();
            SendSceneOpenedEvent(scene);
            m_CurrentSceneName = scene.name;
        }

        static void OnSceneOpened(Scene scene, OpenSceneMode openSceneMode)
        {
            SendSceneSessionLengthEvent();
            SendSceneOpenedEvent(scene);
            m_CurrentSceneName = scene.name;
        }

        static void SendSceneOpenedEvent(Scene scene)
        {
            EditorAnalytics.SendEventWithLimit(
                k_SceneOpenedEvent,
                new SceneOpenedData { sceneName = scene.name },
                k_SceneOpenedEventVersion);
        }

        // All static fields will be reset if something triggers a recompile (or reimport, etc).
        // Therefore, we can't store a running total of session length.
        // We should send the current value before the recompile starts.
        static void OnBeforeAssemblyReload()
        {
            SendSceneSessionLengthEvent();
        }

        // All static fields will be reset when entering play mode.
        // Therefore, we can't store a running total of session length.
        // We should send the current value while exiting edit mode.
        static void OnPlayModeStateChanged(PlayModeStateChange playModeStateChange)
        {
            if (playModeStateChange == PlayModeStateChange.ExitingEditMode)
            {
                SendSceneSessionLengthEvent();
            }
        }

        static bool OnEditorWantsToQuit()
        {
            SendSceneSessionLengthEvent();

            return true;
        }

        static void SendSceneSessionLengthEvent()
        {
            if (string.IsNullOrEmpty(m_CurrentSceneName))
            {
                return;
            }

            var elapsedSeconds = Convert.ToInt32(DateTime.Now.Subtract(m_CurrentSceneSessionCheckpoint).TotalSeconds);

            if (elapsedSeconds <= 0)
            {
                return;
            }

            m_CurrentSceneSessionCheckpoint = DateTime.Now;

            EditorAnalytics.SendEventWithLimit(
                k_SceneTotalSessionLengthEvent,
                new SceneTotalSessionLengthData
                {
                    sceneName = m_CurrentSceneName,
                    sessionLengthSeconds = elapsedSeconds
                },
                k_SceneTotalSessionLengthEventVersion);
        }
    }
}

#endif
