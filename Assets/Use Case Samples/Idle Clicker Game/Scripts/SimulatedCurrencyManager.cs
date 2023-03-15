using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Services.Samples.IdleClickerGame
{
    public class SimulatedCurrencyManager : MonoBehaviour
    {
        public const string k_WellGrantCurrency = "WATER";
        public const long k_MillisecondsPerSecond = 1000;
        public const long k_WellGrantFrequencySeconds = 1;
        public const long k_WellGrantQuantity = 1;
        public const long k_WellGrantFrequency = k_WellGrantFrequencySeconds * k_MillisecondsPerSecond;

        public CurrencyHudView currencyHudView;

        public IdleClickerGameSampleView sampleView;

        public static SimulatedCurrencyManager instance { get; private set; }

        // We track the timestamp offset between local time and server time so we can simulate ticking
        // water at the same time as the server would actually grant if updated so client and server
        // can always be synced.
        public long serverTimestampOffset { get; private set; }

        List<WellInfo>[] m_AllWells;

        long m_NextWellProduceTime = long.MaxValue;

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

        public void UpdateServerTimestampOffset(long serverTimestamp)
        {
            var localTimestamp = GetLocalTimestamp();
            serverTimestampOffset = serverTimestamp - localTimestamp;
        }

        public void StartRefreshingCurrencyBalances(List<WellInfo>[] allWells)
        {
            UpdateWellsLastProduceTime(allWells);

            m_AllWells = allWells;

            m_NextWellProduceTime = CalculateNextWellProduceTimestamp();
        }

        void UpdateWellsLastProduceTime(List<WellInfo>[] allWells)
        {
            foreach (var wellsByLevel in allWells)
            {
                var serverTime = GetApproxServerTimestamp();
                for (int i = 0; i < wellsByLevel.Count; i++)
                {
                    var well = wellsByLevel[i];
                    var elapsed = serverTime - well.timestamp;
                    if (elapsed > 0)
                    {
                        var lastProducedTimestamp = well.timestamp +
                            ((elapsed / k_MillisecondsPerSecond) * k_MillisecondsPerSecond);
                        well.timestamp = lastProducedTimestamp;
                        wellsByLevel[i] = well;
                    }
                }
            }
        }

        public void StopRefreshingCurrencyBalances()
        {
            m_AllWells = null;

            m_NextWellProduceTime = long.MaxValue;
        }

        void Update()
        {
            var serverTimestamp = GetApproxServerTimestamp();

            if (serverTimestamp >= m_NextWellProduceTime)
            {
                UpdateAllWellsForTimestamp(serverTimestamp);

                m_NextWellProduceTime = CalculateNextWellProduceTimestamp();
            }
        }

        void UpdateAllWellsForTimestamp(long timestamp)
        {
            long currencyProduced = 0;

            for (int wellLevelOn = 0;
                 wellLevelOn < IdleClickerGameSceneManager.k_NumWellLevels;
                 wellLevelOn++)
            {
                var wellsByLevel = m_AllWells[wellLevelOn];
                for (var i = 0; i < wellsByLevel.Count; i++)
                {
                    var well = wellsByLevel[i];

                    var elapsed = timestamp - well.timestamp;
                    var grantCycles = elapsed / k_WellGrantFrequency;
                    if (grantCycles > 0)
                    {
                        currencyProduced += grantCycles * k_WellGrantQuantity * (wellLevelOn + 1);

                        well.timestamp += grantCycles * k_WellGrantFrequency;
                        wellsByLevel[i] = well;

                        sampleView.ShowGenerateAnimation(new Vector2(well.x, well.y));
                    }
                }
            }

            EconomyManager.instance.IncrementCurrencyBalance(k_WellGrantCurrency, currencyProduced);
        }

        long CalculateNextWellProduceTimestamp()
        {
            var oldestTime = FindOldestWellTimestamp();
            return oldestTime + k_WellGrantFrequency;
        }

        long FindOldestWellTimestamp()
        {
            var oldestTime = long.MaxValue;

            foreach (var wellsByLevel in m_AllWells)
            {
                foreach (var well in wellsByLevel)
                {
                    if (well.timestamp < oldestTime)
                    {
                        oldestTime = well.timestamp;
                    }
                }
            }

            return oldestTime;
        }

        public long GetApproxServerTimestamp()
        {
            return GetLocalTimestamp() + serverTimestampOffset;
        }

        long GetLocalTimestamp()
        {
            return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;
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
