using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models.Data.Player;
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
                // SampleGameModule keeps level and XP in the protected access class: the client reads them,
                // only the module writes them.
                var savedData = await CloudSaveService.Instance.Data.Player.LoadAllAsync(
                    new LoadAllOptions(new ProtectedReadAccessClassOptions()));

                // Check that scene has not been unloaded while processing async wait to prevent throw.
                if (this == null) return;

                // Only SampleGameModule writes these keys. A new player has neither yet, and the module
                // treats missing keys as level 1 with 0 XP, so the client shows the same defaults.
                m_CachedCloudData[k_PlayerLevelKey] = savedData.TryGetValue(k_PlayerLevelKey, out var level)
                    ? level.Value.GetAs<int>()
                    : k_NewPlayerLevel;
                m_CachedCloudData[k_PlayerXPKey] = savedData.TryGetValue(k_PlayerXPKey, out var xp)
                    ? xp.Value.GetAs<int>()
                    : k_NewPlayerXP;
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
