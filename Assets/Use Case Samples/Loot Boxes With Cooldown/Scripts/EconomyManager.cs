using System;
using System.Threading.Tasks;
using Unity.Services.Economy;
using Unity.Services.Economy.Model;
using UnityEngine;

namespace Unity.Services.Samples.LootBoxesWithCooldown
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
            GetBalancesResult balanceResult = null;

            try
            {
                balanceResult = await GetEconomyBalances();
            }
            catch (EconomyRateLimitedException e)
            {
                balanceResult = await Utils.RetryEconomyFunction(GetEconomyBalances, e.RetryAfter);
            }
            catch (Exception e)
            {
                Debug.Log("Problem getting Economy currency balances:");
                Debug.LogException(e);
            }

            // Check that scene has not been unloaded while processing async wait to prevent throw.
            if (this == null)
                return;

            currencyHudView.SetBalances(balanceResult);
        }

        static Task<GetBalancesResult> GetEconomyBalances()
        {
            var options = new GetBalancesOptions { ItemsPerFetch = 100 };
            return EconomyService.Instance.PlayerBalances.GetBalancesAsync(options);
        }

        public async Task RefreshInventory()
        {
            GetInventoryResult inventoryResult = null;

            try
            {
                inventoryResult = await GetEconomyPlayerInventory();
            }
            catch (EconomyRateLimitedException e)
            {
                inventoryResult = await Utils.RetryEconomyFunction(GetEconomyPlayerInventory, e.RetryAfter);
            }
            catch (Exception e)
            {
                Debug.Log("Problem getting Economy inventory items:");
                Debug.LogException(e);
            }

            if (this == null)
                return;

            inventoryHudView.Refresh(inventoryResult.PlayersInventoryItems);
        }

        static Task<GetInventoryResult> GetEconomyPlayerInventory()
        {
            var options = new GetInventoryOptions { ItemsPerFetch = 100 };
            return EconomyService.Instance.PlayerInventory.GetInventoryAsync(options);
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
