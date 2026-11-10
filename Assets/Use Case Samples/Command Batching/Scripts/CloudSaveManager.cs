using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models.Data.Player;
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
                // SampleGameModule keeps this sample's keys in the protected access class: the client reads
                // them, only the module writes them.
                var savedData = await CloudSaveService.Instance.Data.Player.LoadAllAsync(
                    new LoadAllOptions(new ProtectedReadAccessClassOptions()));
                m_CachedCloudData = savedData.ToDictionary(item => item.Key, item => item.Value.Value.GetAsString());
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
