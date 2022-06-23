using System;
using System.Threading.Tasks;
using Unity.Services.Economy;
using UnityEngine;

namespace UnityGamingServicesUseCases
{
    namespace BattlePass
    {
        public class EconomyManager : MonoBehaviour
        {
            public CurrencyHudView currencyHudView;
            public InventoryHudView inventoryHudView;

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
                    Debug.Log("Problem calling cloud code endpoint: " + e.Message);
                    Debug.LogException(e);
                }
            }

            public async Task RefreshInventory()
            {
                // empty the inventory view first
                inventoryHudView.Refresh(default);

                try
                {
                    var options = new GetInventoryOptions { ItemsPerFetch = 100 };
                    var getInventoryTask = EconomyService.Instance.PlayerInventory.GetInventoryAsync(options);
                    var getInventoryResult = await Utils.ProcessEconomyTaskWithRetry(getInventoryTask);

                    if (this == null) return;

                    inventoryHudView.Refresh(getInventoryResult);
                }
                catch (Exception e)
                {
                    Debug.Log("Problem calling cloud code endpoint: " + e.Message);
                    Debug.LogException(e);
                }
            }

            public async Task GainCurrency(string currencyId, int amount)
            {
                try
                { 
                    var balance = await EconomyService.Instance.PlayerBalances.IncrementBalanceAsync(currencyId, amount);

                    currencyHudView.SetBalance(balance.CurrencyId, balance.Balance);
                }
                catch (Exception e)
                {
                    Debug.Log("Problem calling cloud code endpoint: " + e.Message);
                    Debug.LogException(e);
                }
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
