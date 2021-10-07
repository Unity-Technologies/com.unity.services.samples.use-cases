using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.GameBackend.Economy.Models;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using Unity.Services.Economy;
using UnityEngine;

namespace GameOperationsSamples
{
    namespace StarterPack
    {
        public class StarterPackSample : MonoBehaviour
        {
            public static event Action<bool> StarterPackStatusChecked;
            public static event Action<string, long> CurrencyBalanceChanged;

            public CurrencyHudView[] currencyHudViews;
            public InventoryHudView inventoryHudView;

            private void OnEnable()
            {
                foreach (var currencyHudView in currencyHudViews)
                {
                    CurrencyBalanceChanged += currencyHudView.UpdateBalanceField;
                }
            }

            private void OnDisable()
            {
                foreach (var currencyHudView in currencyHudViews)
                {
                    CurrencyBalanceChanged -= currencyHudView.UpdateBalanceField;
                }
            }

            async void Start()
            {
                await UnityServices.InitializeAsync();

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }

                Debug.Log($"Player id: {AuthenticationService.Instance.PlayerId}");

                await RefreshCurrencies();
                await RefreshInventory();
                await RefreshStarterPackStatus();

                StarterPackSampleView.instance.Enable();
            }

            async Task RefreshCurrencies()
            {
                var balancesOptions = new PlayerBalances.GetBalancesOptions { ItemsPerFetch = 100 };
                var getBalancesResult = await Economy.PlayerBalances.GetBalancesAsync(balancesOptions);

                foreach (var balance in getBalancesResult.Balances)
                {
                    CurrencyBalanceChanged?.Invoke(balance.CurrencyId, balance.Balance);
                }
            }

            async Task RefreshInventory()
            {
                var getInventoryResponse = await Economy.PlayerInventory.GetInventoryAsync();

                inventoryHudView.Refresh(getInventoryResponse.PlayersInventoryItems);
            }

            public async void OnBuyButtonPressed()
            {
                StarterPackSampleView.instance.Disable();

                /*
                 * We normally use the Economy.Purchase.MakeVirtualPurchaseAsync method to make a virtual purchase.
                 * In this case, we also want to track if a player has purchased a Starter Pack or not by using a flag.
                 * While that flag is set, this player cannot make the same purchase again.
                 * This flag could be removed so that the player could purchase it again.
                 */

                try
                {
                    await CloudCode.CallEndpointAsync<PlayerPurchaseVirtualResponse>("PurchaseStarterPack", "");

                    await RefreshCurrencies();
                    await RefreshInventory();
                    await RefreshStarterPackStatus();
                }
                catch
                {
                    Debug.LogError("Something went wrong! Make sure you can afford the Starter Pack.");
                }

                StarterPackSampleView.instance.Enable();

                await SaveData.LoadAsync(new HashSet<string> { "STARTER_PACK_STATUS" });

                await RefreshStarterPackStatus();
            }

            public async void OnGiveTenGemsButtonPressed()
            {
                StarterPackSampleView.instance.Disable();

                var balanceResponse = await Economy.PlayerBalances.IncrementBalanceAsync("GEM", 10);

                CurrencyBalanceChanged?.Invoke("GEM", balanceResponse.Balance);

                StarterPackSampleView.instance.Enable();
            }

            public async void OnResetPlayerDataButtonPressed()
            {
                StarterPackSampleView.instance.Disable();

                await CloudCode.CallEndpointAsync("ResetStarterPackFlag", "");

                await RefreshStarterPackStatus();

                StarterPackSampleView.instance.Enable();
            }

            static async Task RefreshStarterPackStatus()
            {
                var starterPackIsClaimed = false;

                var starterPackStatusCloudSaveResult = await SaveData.LoadAsync(new HashSet<string> { "STARTER_PACK_STATUS" });

                if (starterPackStatusCloudSaveResult.ContainsKey("STARTER_PACK_STATUS"))
                {
                    if (starterPackStatusCloudSaveResult["STARTER_PACK_STATUS"].Contains("\"claimed\":true"))
                    {
                        starterPackIsClaimed = true;
                    }
                }
                else
                {
                    Debug.Log("STARTER_PACK_STATUS key not set");
                }

                StarterPackStatusChecked?.Invoke(starterPackIsClaimed);
            }
        }
    }
}
