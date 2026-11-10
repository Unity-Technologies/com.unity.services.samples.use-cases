using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Samples.Economy;
using UnityEngine;

namespace Unity.Services.Samples.InGameMailbox
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

        public void InitializeEconomyLookups()
        {
            var currencyDefinitions = EconomyCatalog.Instance.GetCurrencies();
            var inventoryItemDefinitions = EconomyCatalog.Instance.GetInventoryItems();
            InitializeEconomySpriteAddressLookup(currencyDefinitions, inventoryItemDefinitions);
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
                    if (currencyDefinition.CustomDataDeserializable.GetAs<Dictionary<string, string>>() is { } customData
                        && customData.TryGetValue("spriteAddress", out var spriteAddress))
                    {
                        economySpriteAddresses.Add(currencyDefinition.Id, spriteAddress);
                    }
                }
            }

            if (inventoryItemDefinitions != null)
            {
                foreach (var inventoryItemDefinition in inventoryItemDefinitions)
                {
                    if (inventoryItemDefinition.CustomDataDeserializable.GetAs<Dictionary<string, string>>() is { } customData
                        && customData.TryGetValue("spriteAddress", out var spriteAddress))
                    {
                        economySpriteAddresses.Add(inventoryItemDefinition.Id, spriteAddress);
                    }
                }
            }
        }

        void InitializeVirtualPurchaseLookup()
        {
            virtualPurchaseTransactions.Clear();

            var virtualPurchaseDefinitions = EconomyCatalog.Instance.GetVirtualPurchases();

            if (virtualPurchaseDefinitions == null)
            {
                return;
            }

            foreach (var virtualPurchaseDefinition in virtualPurchaseDefinitions)
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
            if (this == null) return;

            currencyHudView.SetBalances(balanceResult);
        }

        static Task<GetBalancesResult> GetEconomyBalances()
        {
            var options = new GetBalancesOptions { ItemsPerFetch = 100 };
            return PlayerEconomy.Instance.GetBalancesAsync(options);
        }

        public async Task RefreshInventory()
        {
            GetInventoryResult inventoryResult = null;

            // empty the inventory view first
            inventoryHudView.Refresh(default);

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

            if (this == null) return;

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
