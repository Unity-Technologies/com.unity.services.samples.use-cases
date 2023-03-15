using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Services.Samples.IdleClickerGame
{
    public class UnlockManager : MonoBehaviour
    {
        public const int unlockCountRequired = 4;

        public static UnlockManager instance { get; private set; }

        // Unlock counters based on key values.
        // Note that we use strings as keys so the Unlock Manager could support any arbitrary accomplishments.
        Dictionary<string, int> m_UnlockCounters;

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

        public void SetUnlockCounters(Dictionary<string, int> unlockCounters)
        {
            if (unlockCounters is null)
            {
                throw new ArgumentNullException("SetUnlockCounters called with null dictionary.");
            }

            m_UnlockCounters = unlockCounters;
        }

        public bool IsUnlocked(string key)
        {
            var count = GetUnlockedCount(key);
            return count >= unlockCountRequired;
        }

        public int GetCountNeeded(string key)
        {
            var count = GetUnlockedCount(key);
            return unlockCountRequired - count;
        }

        public int GetUnlockedCount(string key)
        {
            if (m_UnlockCounters is null)
            {
                throw new InvalidOperationException("Unlock Manager not ready. Be sure to call SetUnlockCounters before checking unlock status.");
            }

            if (!m_UnlockCounters.TryGetValue(key, out int count))
            {
                throw new KeyNotFoundException($"Unlock Manager does not have a value for '{key}'");
            }

            return count;
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
