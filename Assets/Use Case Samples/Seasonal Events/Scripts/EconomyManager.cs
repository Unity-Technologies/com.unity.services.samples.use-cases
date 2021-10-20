using System;
using Unity.Services.Economy;
using UnityEngine;

namespace GameOperationsSamples
{
    namespace SeasonalEvents
    {
        public class EconomyManager : MonoBehaviour
        {
            public static EconomyManager instance { get; private set; }
            public static event Action<string, long> CurrencyBalanceUpdated;

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

            public async void GetUpdatedBalances()
            {
                try
                {
                    var balancesOptions = new PlayerBalances.GetBalancesOptions { ItemsPerFetch = 100 };
                    var getBalancesResult = await Economy.PlayerBalances.GetBalancesAsync(balancesOptions);

                    // Check that scene has not been unloaded while processing async wait to prevent throw.
                    if (this == null) return;

                    foreach (var balance in getBalancesResult.Balances)
                    {
                        CurrencyBalanceUpdated?.Invoke(balance.CurrencyId, balance.Balance);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }
    }
}
