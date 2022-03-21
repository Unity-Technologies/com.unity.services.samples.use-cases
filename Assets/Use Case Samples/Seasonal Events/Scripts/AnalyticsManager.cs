using System;
using System.Collections.Generic;
using Unity.Services.Analytics;
using UnityEngine;

namespace UnityGamingServicesUseCases
{
    namespace SeasonalEvents
    {
        public class AnalyticsManager : MonoBehaviour
        {
            public static AnalyticsManager instance { get; private set; }

            const string k_SceneName = "SeasonalEventsSample";
            DateTime m_SessionStartTime;

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

            void Start()
            {
                m_SessionStartTime = DateTime.Now;
            }

            // Analytics events must be sent after UnityServices.Initialize() is finished. This is why instead of
            // including it in a method that's automatically called like Start(), we send this event in a method we
            // can call when we're ready.
            public void SendSceneOpenedEvent()
            {
                Dictionary<string, object> sceneOpenedParameters = new Dictionary<string, object>
                {
                    { "sceneName", k_SceneName }
                };
                Events.CustomData("SceneOpened", sceneOpenedParameters);
                Debug.Log("Sending Scene opened event.");
            }

            public void SendActionButtonPressedEvent(string buttonName)
            {
                // The ActionButtonPressed event is used to see what buttons are pressed how often and under what conditions.
                // In Data Explorer on the Unity Dashboard, filter to any individual parameter, including different
                // combinations of the same data (like buttonNameBySceneName, buttonNameByABGroup, and
                // buttonNameBySceneNameAndABGroup) so that you can view these combinations in Data Explorer at a glance.
                // Alternatively, you can include the single item parameters (i.e. buttonName, sceneName, and abGroup)
                // and do advanced analysis on them using Data Export.
                Dictionary<string, object> actionButtonPressedParameters = new Dictionary<string, object>
                {
                    { "buttonName", buttonName },
                    { "sceneName", k_SceneName },
                    { "remoteConfigActiveEvent", RemoteConfigManager.instance.activeEventKey },
                    { "buttonNameBySceneName", $"{buttonName} - {k_SceneName}" },
                    { "buttonNameByRemoteConfigEvent", $"{buttonName} - {RemoteConfigManager.instance.activeEventKey}" },
                    { "buttonNameBySceneNameAndRemoteConfigEvent", $"{buttonName} - {k_SceneName} - {RemoteConfigManager.instance.activeEventKey}" }
                };

                Events.CustomData("ActionButtonPressed", actionButtonPressedParameters);
            }

            // The session length is sent when the Back button in the scene is clicked.
            public void SendSessionLengthEvent()
            {
                var timeRange = Utils.GetElapsedTimeRange(m_SessionStartTime);

                Dictionary<string, object> sceneSessionLengthParameters = new Dictionary<string, object>
                {
                    { "timeRange", timeRange },
                    { "sceneName", k_SceneName },
                    { "remoteConfigActiveEvent", RemoteConfigManager.instance.activeEventKey },
                    { "timeRangeBySceneName", $"{timeRange} - {k_SceneName}" },
                    { "timeRangeByRemoteConfigEvent", $"{timeRange} - {RemoteConfigManager.instance.activeEventKey}" },
                    { "timeRangeBySceneNameAndRemoteConfigEvent", $"{timeRange} - {k_SceneName} - {RemoteConfigManager.instance.activeEventKey}" }
                };
                
                Events.CustomData("SceneSessionLength", sceneSessionLengthParameters);
                Debug.Log("Sending SceneSessionLength event: " + timeRange);
            }

            void OnDestroy()
            {
                if (instance == this)
                {
                    instance = null;
                }
            }
        }
    }
}
