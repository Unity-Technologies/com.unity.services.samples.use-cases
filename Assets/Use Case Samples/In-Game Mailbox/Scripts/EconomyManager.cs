using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Economy;
using Unity.Services.Economy.Model;
using UnityEngine;

namespace UnityGamingServicesUseCases
{
    namespace InGameMailbox
    {
        public class EconomyManager : MonoBehaviour
        {
            public CurrencyHudView currencyHudView;
            public InventoryHudView inventoryHudView;

            public static EconomyManager instance { get; private set; }

            public Dictionary<string, string> economySpriteAddresses { get; private set; } =
                new Dictionary<string, string>();

            // Dictionary of all Virtual Purchase transactions ids to lists of rewards.
            public Dictionary<string, List<ItemAndAmountSpec>> virtualPurchaseTransactions { get; private set; } =
                new Dictionary<string, List<ItemAndAmountSpec>>();

            List<CurrencyDefinition> m_CurrencyDefinitions;
            List<InventoryItemDefinition> m_InventoryItemDefinitions;
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

                m_CurrencyDefinitions = getCurrenciesTask.Result;
                m_InventoryItemDefinitions = getInventoryItemsTask.Result;
                m_VirtualPurchaseDefinitions = getVirtualPurchasesTask.Result;
            }

            public void InitializeEconomyLookups()
            {
                InitializeEconomySpriteAddressLookup(m_CurrencyDefinitions, m_InventoryItemDefinitions);
                InitializeVirtualPurchaseLookup();
            }

            void InitializeEconomySpriteAddressLookup(List<CurrencyDefinition> currencyDefinitions,
                List<InventoryItemDefinition> inventoryItemDefinitions)
            {
                economySpriteAddresses.Clear();

                if (currencyDefinitions != null)
                {
                    foreach (var currencyDefinition in currencyDefinitions)
                    {
                        if (currencyDefinition.CustomData != null
                            && currencyDefinition.CustomData.TryGetValue("spriteAddress", out var spriteAddressObject)
                            && spriteAddressObject is string spriteAddress)
                        {
                            economySpriteAddresses.Add(currencyDefinition.Id, spriteAddress);
                        }
                    }
                }

                if (inventoryItemDefinitions != null)
                {
                    foreach (var inventoryItemDefinition in inventoryItemDefinitions)
                    {
                        if (inventoryItemDefinition.CustomData != null
                            && inventoryItemDefinition.CustomData.TryGetValue("spriteAddress", out var spriteAddressObject)
                            && spriteAddressObject is string spriteAddress)
                        {
                            economySpriteAddresses.Add(inventoryItemDefinition.Id, spriteAddress);
                        }
                    }
                }
            }
            
            void InitializeVirtualPurchaseLookup()
            {
                virtualPurchaseTransactions.Clear();

                if (m_VirtualPurchaseDefinitions == null)
                {
                    return;
                }

                foreach (var virtualPurchaseDefinition in m_VirtualPurchaseDefinitions)
                {
                    var rewards = ParseEconomyItems(virtualPurchaseDefinition.Rewards);
                    virtualPurchaseTransactions[virtualPurchaseDefinition.Id] = rewards;
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
                    Debug.Log("Problem getting Economy currency balances: " + e.Message);
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
                    Debug.Log("Problem getting Economy inventory items: " + e.Message);
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
