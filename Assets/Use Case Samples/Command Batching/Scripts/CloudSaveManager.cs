using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using UnityEngine;

namespace UnityGamingServicesUseCases
{
    namespace CommandBatching
    {
        public class CloudSaveManager : MonoBehaviour
        {
            public static CloudSaveManager instance { get; private set; }

            const string k_XPKey = "COMMANDBATCH_XP";
            const string k_GoalsAchievedKey = "COMMANDBATCH_GOALSACHIEVED";

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
                    m_CachedCloudData = await SaveData.LoadAllAsync();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            public int GetCachedXP()
            {
                var xp = 0;

                if (m_CachedCloudData != null && m_CachedCloudData.ContainsKey(k_XPKey))
                {
                    int.TryParse(m_CachedCloudData[k_XPKey], out xp);
                }

                return xp;
            }
            
            public int GetCachedGoalsAchieved()
            {
                var goalsAchieved = 0;

                if (m_CachedCloudData != null && m_CachedCloudData.ContainsKey(k_GoalsAchievedKey))
                {
                    int.TryParse(m_CachedCloudData[k_GoalsAchievedKey], out goalsAchieved);
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
}
