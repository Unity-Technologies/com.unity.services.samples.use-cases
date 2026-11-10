using System;
using System.Threading.Tasks;
using Unity.Services.Samples.Economy;
using UnityEngine;

namespace Unity.Services.Samples.StarterPack
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
            // Calling FetchAsync(), will update the cached catalog (the lists of Currency, Inventory Item, and
            // Purchase definitions) with the latest values published to the ECONOMY_CURRENCIES, ECONOMY_ITEMS and
            // ECONOMY_VIRTUAL_PURCHASES Remote Config keys, including any changes made by Game Overrides. It also
            // ensures that services like Cloud Code are working with the same configuration that has been cached.
            await EconomyCatalog.Instance.FetchAsync();
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
            return PlayerEconomy.Instance.GetBalancesAsync(options);
        }

        public void SetCurrencyBalance(string currencyId, long balance)
        {
            currencyHudView.SetBalance(currencyId, balance);
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
            return PlayerEconomy.Instance.GetInventoryAsync(options);
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
