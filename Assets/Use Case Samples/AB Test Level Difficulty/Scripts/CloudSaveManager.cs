using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using UnityEngine;

namespace GameOperationsSamples
{
    namespace ABTestLevelDifficulty
    {
        public class CloudSaveManager : MonoBehaviour
        {
            public static CloudSaveManager instance { get; private set; }

            public int playerLevel => m_CachedCloudData[k_PlayerLevelKey];
            public int playerXP => m_CachedCloudData[k_PlayerXPKey];

            Dictionary<string, int> m_CachedCloudData = new Dictionary<string, int>();
            const int k_NewPlayerLevel = 1;
            const int k_NewPlayerXP = 0;
            const string k_PlayerLevelKey = "PLAYER_LEVEL";
            const string k_PlayerXPKey = "PLAYER_XP";

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

                InitializeDefaultCachedData();
            }

            void InitializeDefaultCachedData()
            {
                m_CachedCloudData.Add(k_PlayerLevelKey, 0);
                m_CachedCloudData.Add(k_PlayerXPKey, 0);
            }

            public async Task LoadAndCacheData()
            {
                try
                {
                    var savedData = await SaveData.LoadAllAsync();

                    // Check that scene has not been unloaded while processing async wait to prevent throw.
                    if (this == null) return;

                    if (savedData.Keys.Count == 0)
                    {
                        await InitializeNewPlayerData();
                        return;
                    }

                    if (savedData.ContainsKey(k_PlayerLevelKey))
                    {
                        m_CachedCloudData[k_PlayerLevelKey] = int.Parse(savedData[k_PlayerLevelKey]);
                    }
                    else
                    {
                        Debug.Log($"Cloud Code was expected to have {k_PlayerLevelKey} data, but did not.");
                    }

                    if (savedData.ContainsKey(k_PlayerXPKey))
                    {
                        m_CachedCloudData[k_PlayerXPKey] = int.Parse(savedData[k_PlayerXPKey]);
                    }
                    else
                    {
                        Debug.Log($"Cloud Code was expected to have {k_PlayerXPKey} data, but did not.");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            async Task InitializeNewPlayerData()
            {
                try
                {
                    var newPlayerData = new Dictionary<string, object>
                    {
                        { k_PlayerLevelKey, k_NewPlayerLevel },
                        { k_PlayerXPKey, k_NewPlayerXP }
                    };

                    await SaveData.ForceSaveAsync(newPlayerData);
                    if (this == null) return;

                    if (m_CachedCloudData.ContainsKey(k_PlayerLevelKey))
                    {
                        m_CachedCloudData[k_PlayerLevelKey] = k_NewPlayerLevel;
                    }
                    else
                    {
                        m_CachedCloudData.Add(k_PlayerLevelKey, k_NewPlayerLevel);
                    }
                    
                    if (m_CachedCloudData.ContainsKey(k_PlayerXPKey))
                    {
                        m_CachedCloudData[k_PlayerXPKey] = k_NewPlayerXP;
                    }
                    else
                    {
                        m_CachedCloudData.Add(k_PlayerXPKey, k_NewPlayerXP);
                    }
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
                m_CachedCloudData.Clear();
                InitializeDefaultCachedData();
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
