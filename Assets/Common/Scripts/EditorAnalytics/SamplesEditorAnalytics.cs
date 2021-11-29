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
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace GameOperationsSamples
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
        struct TotalUnitySessionLengthData
        {
            public int sessionLengthSeconds;
        }

        const int k_MaxEventsPerHour = 100;
        const int k_MaxNumberOfElements = 10;
        const string k_VendorKey = "unity.gamingservicessamples";

        const string k_Prefix = "gameOperationsSamples";
        const string k_SceneOpenedEvent = k_Prefix + "SceneOpened";
        const string k_ButtonPressedInPlayModeEvent = k_Prefix + "ButtonPressedInPlayMode";
        const string k_SceneTotalSessionLengthEvent = k_Prefix + "SceneTotalSessionLength";
        const string k_TotalEditorSessionLengthEvent = k_Prefix + "TotalEditorSessionLength";

        static string m_CurrentSceneName;
        static DateTime m_CurrentSceneOpenedDateTime;
        static Dictionary<string, int> m_SceneTotalSessionLengths = new Dictionary<string, int>();

        static SamplesEditorAnalytics()
        {
            EditorSceneManager.sceneOpened += OnEditorSceneOpened;
            EditorSceneManager.sceneClosed += OnEditorSceneClosed;
            EditorApplication.wantsToQuit += OnEditorWantsToQuit;

            if (EditorAnalytics.enabled)
            {
                EditorAnalytics.RegisterEventWithLimit(
                    k_ButtonPressedInPlayModeEvent, k_MaxEventsPerHour, k_MaxNumberOfElements, k_VendorKey);

                EditorAnalytics.RegisterEventWithLimit(
                    k_SceneOpenedEvent, k_MaxEventsPerHour, k_MaxNumberOfElements, k_VendorKey);

                EditorAnalytics.RegisterEventWithLimit(
                    k_SceneTotalSessionLengthEvent, k_MaxEventsPerHour, k_MaxNumberOfElements, k_VendorKey);

                EditorAnalytics.RegisterEventWithLimit(
                    k_TotalEditorSessionLengthEvent, k_MaxEventsPerHour, k_MaxNumberOfElements, k_VendorKey);
            }
        }

        public static void SendButtonPressedInPlayModeEvent(string buttonName)
        {
            if (EditorAnalytics.enabled)
            {
                EditorAnalytics.SendEventWithLimit(
                    k_ButtonPressedInPlayModeEvent, new PlayModeButtonPressedData
                    {
                        buttonName = buttonName
                    });
            }
        }

        static void OnEditorSceneOpened(Scene scene, OpenSceneMode openSceneMode)
        {
            if (!m_CurrentSceneName.Equals(scene.name))
            {
                m_CurrentSceneName = scene.name;
                m_CurrentSceneOpenedDateTime = DateTime.Now;

                if (!m_SceneTotalSessionLengths.ContainsKey(m_CurrentSceneName))
                {
                    m_SceneTotalSessionLengths.Add(m_CurrentSceneName, 0);
                }
            }

            if (EditorAnalytics.enabled)
            {
                EditorAnalytics.SendEventWithLimit(
                    k_SceneOpenedEvent, new SceneOpenedData
                    {
                        sceneName = scene.name
                    });
            }
        }

        static void OnEditorSceneClosed(Scene scene)
        {
            AddThisSceneSessionLengthToTotals();

            m_CurrentSceneName = "";
        }

        static void AddThisSceneSessionLengthToTotals()
        {
            if (!string.IsNullOrEmpty(m_CurrentSceneName)
                && m_SceneTotalSessionLengths.ContainsKey(m_CurrentSceneName))
            {
                var elapsedSeconds = Convert.ToInt32(DateTime.Now.Subtract(m_CurrentSceneOpenedDateTime).TotalSeconds);

                m_SceneTotalSessionLengths[m_CurrentSceneName] += elapsedSeconds;
            }
        }

        static bool OnEditorWantsToQuit()
        {
            AddThisSceneSessionLengthToTotals();

            if (EditorAnalytics.enabled)
            {
                var totalUnitySessionLengthSeconds = 0;

                foreach (var kvp in m_SceneTotalSessionLengths)
                {
                    totalUnitySessionLengthSeconds += kvp.Value;

                    EditorAnalytics.SendEventWithLimit(
                        k_SceneTotalSessionLengthEvent,
                        new SceneTotalSessionLengthData
                        {
                            sceneName = kvp.Key,
                            sessionLengthSeconds = kvp.Value
                        });
                }

                EditorAnalytics.SendEventWithLimit(
                    k_TotalEditorSessionLengthEvent,
                    new TotalUnitySessionLengthData { sessionLengthSeconds = totalUnitySessionLengthSeconds });
            }

            return true;
        }
    }
}

#endif
