using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using UnityEngine;

namespace Unity.Services.Samples.CommandBatching
{
    public class CloudSaveManager : MonoBehaviour
    {
        public static CloudSaveManager instance { get; private set; }

        public const string xpKey = "COMMAND_BATCH_XP";
        public const string goalsAchievedKey = "COMMAND_BATCH_GOALS_ACHIEVED";

        Dictionary<string, string> m_CachedCloudData;

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
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public int GetCachedXP()
        {
            var xp = 0;

            if (m_CachedCloudData != null && m_CachedCloudData.ContainsKey(xpKey))
            {
                int.TryParse(m_CachedCloudData[xpKey], out xp);
            }

            return xp;
        }

        public int GetCachedGoalsAchieved()
        {
            var goalsAchieved = 0;

            if (m_CachedCloudData != null && m_CachedCloudData.ContainsKey(goalsAchievedKey))
            {
                int.TryParse(m_CachedCloudData[goalsAchievedKey], out goalsAchieved);
            }

            return goalsAchieved;
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
