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

            void OnDestroy()
            {
                if (instance == this)
                {
                    instance = null;
                }
            }

            public async Task RefreshCurrencyBalances()
            {
                try
                {
                    var options = new PlayerBalances.GetBalancesOptions { ItemsPerFetch = 100 };
                    var getBalancesTask = Economy.PlayerBalances.GetBalancesAsync(options);
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
                    var options = new PlayerInventory.GetInventoryOptions { ItemsPerFetch = 100 };
                    var getInventoryTask = Economy.PlayerInventory.GetInventoryAsync(options);
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
                    var balance = await Economy.PlayerBalances.IncrementBalanceAsync(currencyId, amount);

                    currencyHudView.SetBalance(balance.CurrencyId, balance.Balance);
                }
                catch (Exception e)
                {
                    Debug.Log("Problem calling cloud code endpoint: " + e.Message);
                    Debug.LogException(e);
                }
            }
        }
    }
}
