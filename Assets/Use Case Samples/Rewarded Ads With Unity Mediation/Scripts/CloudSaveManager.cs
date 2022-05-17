using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using UnityEngine;

namespace UnityGamingServicesUseCases
{
    namespace RewardedAds
    {
        public class CloudSaveManager : MonoBehaviour
        {
            public static CloudSaveManager instance { get; private set; }

            const string k_LevelCountKey = "REWARDED_ADS_LEVEL_END_COUNT";

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

            public int GetCachedLevelEndCount()
            {
                var levelCount = 0;

                if (m_CachedCloudData != null && m_CachedCloudData.ContainsKey(k_LevelCountKey))
                {
                    int.TryParse(m_CachedCloudData[k_LevelCountKey], out levelCount);
                }

                return levelCount;
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
