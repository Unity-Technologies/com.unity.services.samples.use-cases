using System;
using System.Threading.Tasks;
using Unity.Services.Economy;
using UnityEngine;

namespace GameOperationsSamples
{
    namespace ABTestLevelDifficulty
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

            public async Task GetUpdatedBalances()
            {
                var balancesOptions = new PlayerBalances.GetBalancesOptions {ItemsPerFetch = 100};
                var getBalancesResult = await Economy.PlayerBalances.GetBalancesAsync(balancesOptions);

                // Check that scene has not been unloaded while processing async wait to prevent throw.
                if (this == null) return;

                foreach (var balance in getBalancesResult.Balances)
                {
                    CurrencyBalanceUpdated?.Invoke(balance.CurrencyId, balance.Balance);
                }
            }

            public async Task ClearCachedView()
            {
                var currencyDefinitions = await Economy.Configuration.GetCurrenciesAsync();
                if (this == null) return;

                foreach (var currencyDefinition in currencyDefinitions)
                {
                    CurrencyBalanceUpdated?.Invoke(currencyDefinition.Id, 0);
                }
            }
        }
    }

}
