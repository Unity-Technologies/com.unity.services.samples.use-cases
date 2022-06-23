using System;
using System.Threading.Tasks;
using Unity.Services.Economy;
using UnityEngine;

namespace UnityGamingServicesUseCases
{
    namespace ABTestLevelDifficulty
    {
        public class EconomyManager : MonoBehaviour
        {
            public CurrencyHudView currencyHudView;

            public static EconomyManager instance { get; private set; }

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
            
            public async Task RefreshEconomyConfiguration()
            {
                // Calling GetCurrenciesAsync (or GetInventoryItemsAsync), in addition to returning the appropriate
                // Economy configurations, will update the cached configuration list, including any new Currency, 
                // Inventory Item, or Purchases that have been published since the last time the player's configuration
                // was cached.
                // 
                // This is important to do before hitting the Economy or Remote Config services for any other calls as
                // both use the cached data list.
                await EconomyService.Instance.Configuration.GetCurrenciesAsync();
            }

            public async Task RefreshCurrencyBalances()
            {
                try
                {
                    var options = new GetBalancesOptions { ItemsPerFetch = 100 };
                    var getBalancesTask = EconomyService.Instance.PlayerBalances.GetBalancesAsync(options);
                    var balances = await Utils.ProcessEconomyTaskWithRetry(getBalancesTask);

                    // Check that scene has not been unloaded while processing async wait to prevent throw.
                    if (this == null) return;

                    currencyHudView.SetBalances(balances);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            public void SetCurrencyBalance(string currencyId, long balance)
            {
                currencyHudView.SetBalance(currencyId, balance);
            }

            public void ClearCurrencyBalances()
            {
                currencyHudView.ClearBalances();
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
