using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Economy;
using Unity.Services.Economy.Model;
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
            // Calling SyncConfigurationAsync(), will update the cached configuration list (the lists of Currency,
            // Inventory Item, and Purchase definitions) with any definitions that have been published or changed by
            // Economy or overriden by Game Overrides since the last time the player's configuration was cached. It also
            // ensures that other services like Cloud Code are working with the same configuration that has been cached.
            await EconomyService.Instance.Configuration.SyncConfigurationAsync();
        }

        public void InitializeEconomyLookups()
        {
            var currencyDefinitions = EconomyService.Instance.Configuration.GetCurrencies();
            var inventoryItemDefinitions = EconomyService.Instance.Configuration.GetInventoryItems();
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

            var virtualPurchaseDefinitions = EconomyService.Instance.Configuration.GetVirtualPurchases();

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
            return EconomyService.Instance.PlayerBalances.GetBalancesAsync(options);
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
