using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Economy;
using Unity.Services.Economy.Model;
using UnityEngine;

namespace UnityGamingServicesUseCases
{
    namespace VirtualShop
    {
        public class EconomyManager : MonoBehaviour
        {
            const int k_EconomyPurchaseCostsNotMetStatusCode = 10504;

            public CurrencyHudView currencyHudView;
            public InventoryHudView inventoryHudView;

            public static EconomyManager instance { get; private set; }

            // Dictionary of all Virtual Purchase transactions ids to lists of costs & rewards.
            public Dictionary<string, (List<ItemAndAmountSpec> costs, List<ItemAndAmountSpec> rewards)>
                virtualPurchaseTransactions { get; private set; }

            public List<CurrencyDefinition> currencyDefinitions { get; private set; }
            public List<InventoryItemDefinition> inventoryItemDefinitions { get; private set; }

            List<VirtualPurchaseDefinition> m_VirtualPurchaseDefinitions;

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

            public async Task RefreshEconomyConfiguration()
            {
                // Calling GetCurrenciesAsync (or GetInventoryItemsAsync, or GetVirtualPurchasesAsync, etc), in addition
                // to returning the appropriate Economy configurations, will update the cached configuration list,
                // including any new Currency, Inventory Item, or Purchases that have been published since the last
                // time the player's configuration was cached.
                // 
                // This is important to do before hitting the Economy or Remote Config services for any other calls as
                // both use the cached data list.
                var getCurrenciesTask = EconomyService.Instance.Configuration.GetCurrenciesAsync();
                var getInventoryItemsTask = EconomyService.Instance.Configuration.GetInventoryItemsAsync();
                var getVirtualPurchasesTask = EconomyService.Instance.Configuration.GetVirtualPurchasesAsync();

                await Task.WhenAll(getCurrenciesTask, getInventoryItemsTask, getVirtualPurchasesTask);

                // Check that scene has not been unloaded while processing async wait to prevent throw.
                if (this == null) return;

                currencyDefinitions = getCurrenciesTask.Result;
                inventoryItemDefinitions = getInventoryItemsTask.Result;
                m_VirtualPurchaseDefinitions = getVirtualPurchasesTask.Result;
            }

            public async Task RefreshCurrencyBalances()
            {
                try
                {
                    var options = new GetBalancesOptions { ItemsPerFetch = 100 };
                    var getBalancesTask = EconomyService.Instance.PlayerBalances.GetBalancesAsync(options);
                    var balances = await Utils.ProcessEconomyTaskWithRetry(getBalancesTask);

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

            public void InitializeVirtualPurchaseLookup()
            {
                if (m_VirtualPurchaseDefinitions == null)
                {
                    return;
                }

                virtualPurchaseTransactions = new Dictionary<string, 
                    (List<ItemAndAmountSpec> costs, List<ItemAndAmountSpec> rewards)>();

                foreach (var virtualPurchaseDefinition in m_VirtualPurchaseDefinitions)
                {
                    var costs = ParseEconomyItems(virtualPurchaseDefinition.Costs);
                    var rewards = ParseEconomyItems(virtualPurchaseDefinition.Rewards);

                    virtualPurchaseTransactions[virtualPurchaseDefinition.Id] = (costs, rewards);
                }
            }

            List<ItemAndAmountSpec> ParseEconomyItems(List<PurchaseItemQuantity> itemQuantities)
            {
                var itemsAndAmountsSpec = new List<ItemAndAmountSpec>();

                foreach (var itemQuantity in itemQuantities)
                {
                    var id = itemQuantity.Item.GetReferencedConfigurationItem().Id;
                    itemsAndAmountsSpec.Add(new ItemAndAmountSpec(id, itemQuantity.Amount));
                }

                return itemsAndAmountsSpec;
            }

            public async Task<MakeVirtualPurchaseResult> MakeVirtualPurchaseAsync(string virtualPurchaseId)
            {
                try
                {
                    return await EconomyService.Instance.Purchases.MakeVirtualPurchaseAsync(virtualPurchaseId);
                }
                catch (EconomyException e)
                when (e.ErrorCode == k_EconomyPurchaseCostsNotMetStatusCode)
                {
                    // Rethrow purchase-cost-not-met exception to be handled by shops manager.
                    throw;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    return default;
                }
            }

            // This method is used to help test this Use Case sample by giving some currency to permit
            // transactions to be completed.
            public async Task GrantDebugCurrency(string currencyId, int amount)
            {
                try
                {
                    await EconomyService.Instance.PlayerBalances.IncrementBalanceAsync(currencyId, amount);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }
    }
}
