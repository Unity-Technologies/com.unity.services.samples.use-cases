using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Economy.Model;
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
        public class StarterPackSceneManager : MonoBehaviour
        {
            public static event Action<bool> StarterPackStatusChecked;
            public static event Action<string, long> CurrencyBalanceChanged;

            public CurrencyHudView[] currencyHudViews;
            public InventoryHudView inventoryHudView;

            void OnEnable()
            {
                foreach (var currencyHudView in currencyHudViews)
                {
                    CurrencyBalanceChanged += currencyHudView.UpdateBalanceField;
                }
            }

            void OnDisable()
            {
                foreach (var currencyHudView in currencyHudViews)
                {
                    CurrencyBalanceChanged -= currencyHudView.UpdateBalanceField;
                }
            }

            async void Start()
            {
                await UnityServices.InitializeAsync();

                // Check that scene has not been unloaded while processing async wait to prevent throw.
                if (this == null) return;

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                    if (this == null) return;
                }

                Debug.Log($"Player id: {AuthenticationService.Instance.PlayerId}");

                await Task.WhenAll(RefreshCurrencies(),
                    RefreshInventory(),
                    RefreshStarterPackStatus());
                if (this == null) return;

                StarterPackSampleView.instance.Enable();
            }

            async Task RefreshCurrencies()
            {
                try
                {
                    var balancesOptions = new PlayerBalances.GetBalancesOptions { ItemsPerFetch = 100 };
                    var getBalancesResult = await Economy.PlayerBalances.GetBalancesAsync(balancesOptions);
                    if (this == null) return;

                    foreach (var balance in getBalancesResult.Balances)
                    {
                        CurrencyBalanceChanged?.Invoke(balance.CurrencyId, balance.Balance);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            async Task RefreshInventory()
            {
                try
                {
                    var getInventoryResponse = await Economy.PlayerInventory.GetInventoryAsync();
                    if (this == null) return;

                    inventoryHudView.Refresh(getInventoryResponse.PlayersInventoryItems);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            public async void OnBuyButtonPressed()
            {
                StarterPackSampleView.instance.Disable();

                // We normally use the Economy.Purchase.MakeVirtualPurchaseAsync method to make a virtual purchase.
                // In this case, we also want to track if a player has purchased a Starter Pack or not by using a flag.
                // While that flag is set, this player cannot make the same purchase again.
                // This flag could be removed so that the player could purchase it again.
                try
                {
                    await CloudCode.CallEndpointAsync<MakeVirtualPurchaseResult>("PurchaseStarterPack", "");
                    if (this == null) return;

                    await Task.WhenAll(RefreshCurrencies(),
                        RefreshInventory(),
                        RefreshStarterPackStatus());
                    if (this == null) return;
                }
                catch (Exception e)
                {
                    Debug.LogError("Something went wrong! Make sure you can afford the Starter Pack.");
                    Debug.LogException(e);
                }

                StarterPackSampleView.instance.Enable();

                await SaveData.LoadAsync(new HashSet<string> { "STARTER_PACK_STATUS" });
                if (this == null) return;

                await RefreshStarterPackStatus();
            }

            public async void OnGiveTenGemsButtonPressed()
            {
                StarterPackSampleView.instance.Disable();

                var balanceResponse = await Economy.PlayerBalances.IncrementBalanceAsync("GEM", 10);
                if (this == null) return;

                CurrencyBalanceChanged?.Invoke("GEM", balanceResponse.Balance);

                StarterPackSampleView.instance.Enable();
            }

            public async void OnResetPlayerDataButtonPressed()
            {
                StarterPackSampleView.instance.Disable();

                try
                {
                    await CloudCode.CallEndpointAsync("ResetStarterPackFlag", "");
                    if (this == null) return;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                await RefreshStarterPackStatus();
                if (this == null) return;

                StarterPackSampleView.instance.Enable();
            }

            static async Task RefreshStarterPackStatus()
            {
                var starterPackIsClaimed = false;

                try
                {
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
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                StarterPackStatusChecked?.Invoke(starterPackIsClaimed);
            }
        }
    }
}
