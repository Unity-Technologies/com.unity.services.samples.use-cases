using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using UnityEngine;

namespace Unity.Services.Samples.ABTestLevelDifficulty
{
    public class CloudSaveManager : MonoBehaviour
    {
        public static CloudSaveManager instance { get; private set; }

        public int playerLevel => m_CachedCloudData[k_PlayerLevelKey];
        public int playerXP => m_CachedCloudData[k_PlayerXPKey];

        const int k_NewPlayerLevel = 1;
        const int k_NewPlayerXP = 0;
        const string k_PlayerLevelKey = "AB_TEST_PLAYER_LEVEL";
        const string k_PlayerXPKey = "AB_TEST_PLAYER_XP";

        // The scene view needs to be able to display some value for level and xp even when no player
        // is signed in. For this reason we always keep playerLevel and playerXP keys in the
        // m_CachedCloudData dictionary.
        Dictionary<string, int> m_CachedCloudData = new Dictionary<string, int>
        {
            { k_PlayerLevelKey, 0 },
            { k_PlayerXPKey, 0 }
        };

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
                var savedData = await CloudSaveService.Instance.Data.LoadAllAsync();

                // Check that scene has not been unloaded while processing async wait to prevent throw.
                if (this == null) return;

                var missingData = new Dictionary<string, object>();
                if (savedData.ContainsKey(k_PlayerLevelKey))
                {
                    m_CachedCloudData[k_PlayerLevelKey] = int.Parse(savedData[k_PlayerLevelKey]);
                }
                else
                {
                    missingData.Add(k_PlayerLevelKey, k_NewPlayerLevel);
                    m_CachedCloudData[k_PlayerLevelKey] = k_NewPlayerLevel;
                }

                if (savedData.ContainsKey(k_PlayerXPKey))
                {
                    m_CachedCloudData[k_PlayerXPKey] = int.Parse(savedData[k_PlayerXPKey]);
                }
                else
                {
                    missingData.Add(k_PlayerXPKey, k_NewPlayerXP);
                    m_CachedCloudData[k_PlayerXPKey] = k_NewPlayerXP;
                }

                if (missingData.Count > 0)
                {
                    await SaveUpdatedData(missingData);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        async Task SaveUpdatedData(Dictionary<string, object> data)
        {
            try
            {
                await CloudSaveService.Instance.Data.ForceSaveAsync(data);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public void UpdateCachedPlayerLevel(int newLevel)
        {
            m_CachedCloudData[k_PlayerLevelKey] = newLevel;
        }

        public void UpdateCachedPlayerXP(int newXP)
        {
            m_CachedCloudData[k_PlayerXPKey] = newXP;
        }

        public void ClearCachedData()
        {
            // The scene view needs to be able to display some value for level and xp even when no player
            // is signed in. For this reason we always keep playerLevel and playerXP keys in the
            // m_CachedCloudData dictionary.
            m_CachedCloudData[k_PlayerLevelKey] = 0;
            m_CachedCloudData[k_PlayerXPKey] = 0;
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
