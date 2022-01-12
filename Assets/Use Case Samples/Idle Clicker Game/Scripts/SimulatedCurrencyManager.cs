using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameOperationsSamples
{
    namespace IdleClickerGame
    {
        public class SimulatedCurrencyManager : MonoBehaviour
        {
            public const string k_FactoryGrantCurrency = "WATER";
            public const long k_MillisecondsPerSecond = 1000;
            public const long k_FactoryGrantFrequencySeconds = 1;
            public const long k_FactoryGrantQuantity = 1;
            public const long k_FactoryGrantFrequency = k_FactoryGrantFrequencySeconds * k_MillisecondsPerSecond;

            public CurrencyHudView currencyHudView;

            public IdleClickerGameSampleView sampleView;

            public static SimulatedCurrencyManager instance { get; private set; }

            // We track the timestamp offset between local time and server time so we can simulate ticking
            // water at the same time as the server would actually grant if updated so client and server
            // can always be synced.
            public long serverTimestampOffset { get; private set; }

            List<FactoryInfo> m_Factories;

            long m_NextFactoryProduceTime = long.MaxValue;


            void Awake()
            {
                if (instance != null && instance != this)
                {
                    Destroy(gameObject);
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

            public void StartRefreshingCurrencyBalances(List<FactoryInfo> factories)
            {
                m_Factories = factories;

                m_NextFactoryProduceTime = CalculateNextFactoryProduceTimestamp();
            }

            public void StopRefreshingCurrencyBalances()
            {
                m_Factories = null;

                m_NextFactoryProduceTime = long.MaxValue;
            }

            void Update()
            {
                var serverTimestamp = GetApproxServerTimestamp();

                if (serverTimestamp >= m_NextFactoryProduceTime)
                { 
                    UpdateAllFactoriesForTimestamp(serverTimestamp);

                    m_NextFactoryProduceTime = CalculateNextFactoryProduceTimestamp();
                }
            }

            void UpdateAllFactoriesForTimestamp(long timestamp)
            {
                long currencyProduced = 0;

                for (int i = 0; i < m_Factories.Count; i++)
                {
                    var factory = m_Factories[i];

                    var elapsed = timestamp - factory.timestamp;
                    var grantCycles = elapsed / k_FactoryGrantFrequency;
                    if (grantCycles > 0)
                    {
                        currencyProduced += grantCycles * k_FactoryGrantQuantity;

                        factory.timestamp += grantCycles * k_FactoryGrantFrequency;
                        m_Factories[i] = factory;

                        sampleView.ShowGenerateAnimation(new Vector2(factory.x, factory.y));
                    }
                }

                EconomyManager.instance.IncrementCurrencyBalance(k_FactoryGrantCurrency, currencyProduced);
            }

            long CalculateNextFactoryProduceTimestamp()
            {
                var oldestTime = FindOldestFactoryTimestamp();
                return oldestTime + k_FactoryGrantFrequency;
            }

            long FindOldestFactoryTimestamp()
            {
                var oldestTime = long.MaxValue;
                foreach (var factory in m_Factories)
                {
                    if (factory.timestamp < oldestTime)
                    {
                        oldestTime = factory.timestamp;
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
                instance = null;
            }
        }
    }
}
