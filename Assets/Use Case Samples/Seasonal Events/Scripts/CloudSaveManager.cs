using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using UnityEngine;

namespace Unity.Services.Samples.SeasonalEvents
{
    public class CloudSaveManager : MonoBehaviour
    {
        public static CloudSaveManager instance { get; private set; }

        const string k_LastCompletedEventKey = "SEASONAL_EVENTS_LAST_COMPLETED_EVENT";
        const string k_LastCompletedEventTimestampKey = "SEASONAL_EVENTS_LAST_COMPLETED_EVENT_TIMESTAMP";

        const string k_DefaultCompletedEvent = "";
        DateTime k_DefaultCompletedEventTimestamp = DateTime.MinValue;

        Dictionary<string, string> m_CachedCloudData = new Dictionary<string, string>();

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

        public async Task LoadAndCacheData()
        {
            try
            {
                m_CachedCloudData = await CloudSaveService.Instance.Data.LoadAllAsync();

                // Check that scene has not been unloaded while processing async wait to prevent throw.
                if (this == null) return;

                if (m_CachedCloudData == null)
                {
                    m_CachedCloudData = new Dictionary<string, string>();
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public string GetLastCompletedActiveEvent()
        {
            if (m_CachedCloudData.ContainsKey(k_LastCompletedEventKey))
            {
                var eventKey = m_CachedCloudData[k_LastCompletedEventKey];

                // When a string value is saved in Cloud Save, quotes are added around the value.
                // These quotes don't exist in the value stored in Remote Config which we'll be comparing with, so
                // we will remove the quotes from the value that we return here.
                var trimmedEventKey = eventKey.Replace("\"", string.Empty);
                return trimmedEventKey;
            }

            return k_DefaultCompletedEvent;
        }

        public DateTime GetLastCompletedEventTimestamp()
        {
            if (m_CachedCloudData.ContainsKey(k_LastCompletedEventTimestampKey))
            {
                var eventTimestampMilliseconds = m_CachedCloudData[k_LastCompletedEventTimestampKey];

                // The event timestamp is being saved in Cloud Code, which uses lodash's .now() function to get the
                // timestamp. Lodash calculates milliseconds from the unix epoch (1 January 1970 00:00:00 UTC).
                // DateTime's default initialization is to 0001-01-01 00:00:00.
                var unixEpoch = new DateTime(1970, 1, 1);
                var timestamp = unixEpoch.AddMilliseconds(double.Parse(eventTimestampMilliseconds));
                return timestamp;
            }

            return k_DefaultCompletedEventTimestamp;
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
