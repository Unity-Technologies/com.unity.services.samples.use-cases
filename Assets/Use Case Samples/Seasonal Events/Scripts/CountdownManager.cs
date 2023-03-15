using System;
using Unity.Services.Core;
using UnityEngine;

namespace Unity.Services.Samples.SeasonalEvents
{
    public class CountdownManager : MonoBehaviour
    {
        public SeasonalEventsSampleView sceneView;

        int m_CurrentTimeMinutes;
        int m_CurrentTimeSeconds;

        void Start()
        {
            UpdateCountdownIfReadyAndNeeded();
        }

        void LateUpdate()
        {
            UpdateCountdownIfReadyAndNeeded();
        }

        void UpdateCountdownIfReadyAndNeeded()
        {
            if (!IsRemoteConfigReady())
            {
                SetBlankCountdownText();
                return;
            }

            // Determine the approximate time on the server.
            // Note: We need to use the server time to ensure we are showing/claiming the correct season
            //       in case the client's clock is off for any reason.
            var serverTime = ServerTimeHelper.UtcNow;
            var newMinutes = serverTime.Minute;
            var newSeconds = serverTime.Second;

            if (IsUpdateNeeded(newMinutes, newSeconds))
            {
                UpdateCountdown(newMinutes, newSeconds);
            }
        }

        bool IsRemoteConfigReady()
        {
            // 0 is used as the default value for activeEventEndTime in RemoteConfigManager.
            // Note that no actual game overrides have an end time of 0.
            return UnityServices.State == ServicesInitializationState.Initialized &&
                RemoteConfigManager.instance.activeEventEndTime != 0;
        }

        void SetBlankCountdownText()
        {
            sceneView.UpdateCountdownText("");
        }

        bool IsUpdateNeeded(int newMinutes, int newSeconds)
        {
            return newMinutes != m_CurrentTimeMinutes || newSeconds != m_CurrentTimeSeconds;
        }

        void UpdateCountdown(int newMinutes, int newSeconds)
        {
            m_CurrentTimeMinutes = newMinutes;
            m_CurrentTimeSeconds = newSeconds;

            CalculateAndDisplayCurrentCountdownValue();
        }

        void CalculateAndDisplayCurrentCountdownValue()
        {
            var countdownMinutes = RemoteConfigManager.instance.activeEventEndTime - m_CurrentTimeMinutes % 10;
            if (countdownMinutes < 0 || countdownMinutes > 3)
            {
                // This can occur when for a brief moment the current time has rolled over into the next event, but
                // Remote Config hasn't finished updating with the new game override's data
                countdownMinutes = 0;
            }

            var countdownSeconds = 59 - m_CurrentTimeSeconds;
            SetCountdownText(countdownMinutes, countdownSeconds);
        }

        void SetCountdownText(int minutes, int seconds)
        {
            var counter = "00:" + minutes.ToString("D2") + ":" + seconds.ToString("D2");
            sceneView.UpdateCountdownText(counter);
        }

        public void StartCountdownFromNow()
        {
            var serverTime = ServerTimeHelper.UtcNow;
            UpdateCountdown(serverTime.Minute, serverTime.Second);
        }
    }
}
