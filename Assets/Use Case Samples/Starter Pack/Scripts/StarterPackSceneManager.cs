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

            const string k_StarterPackCloudSaveKey = "STARTER_PACK_STATUS";


            async void Start()
            {
                try
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

                    await Task.WhenAll(EconomyManager.instance.RefreshCurrencyBalances(),
                        EconomyManager.instance.RefreshInventory(),
                        RefreshStarterPackStatus());
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
                finally
                {
                    if (this != null)
                    {
                        StarterPackSampleView.instance.Enable();
                    }
                }
            }

            public async void OnBuyButtonPressed()
            {
                try
                { 
                    StarterPackSampleView.instance.Disable();

                    await ProcessStarterPackPurchaseRequest();
                    if (this == null) return;

                    await Task.WhenAll(EconomyManager.instance.RefreshCurrencyBalances(),
                        EconomyManager.instance.RefreshInventory(),
                        RefreshStarterPackStatus());
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
                finally
                {
                    if (this != null)
                    {
                        StarterPackSampleView.instance.Enable();
                    }
                }
            }

            public async Task ProcessStarterPackPurchaseRequest()
            {
                // We normally use the Economy.Purchase.MakeVirtualPurchaseAsync method to make a virtual purchase.
                // In this case, we also want to track if a player has purchased a Starter Pack or not by using a flag.
                // While that flag is set, this player cannot make the same purchase again.
                // This flag could be removed so that the player could purchase it again.
                try
                {
                    await CloudCode.CallEndpointAsync<MakeVirtualPurchaseResult>(
                        "PurchaseStarterPack", new object());
                }
                catch (Exception e)
                {
                    Debug.LogError("Something went wrong! Make sure you can afford the Starter Pack.");
                    Debug.LogException(e);
                }
            }

            public async void OnGiveTenGemsButtonPressed()
            {
                try
                { 
                    StarterPackSampleView.instance.Disable();

                    var balanceResponse = await Economy.PlayerBalances.IncrementBalanceAsync("GEM", 10);
                    if (this == null) return;

                    EconomyManager.instance.SetCurrencyBalance(balanceResponse.CurrencyId, balanceResponse.Balance);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
                finally
                {
                    if (this != null)
                    {
                        StarterPackSampleView.instance.Enable();
                    }
                }
            }

            public async void OnResetPlayerDataButtonPressed()
            {
                try
                {
                    StarterPackSampleView.instance.Disable();

                    // Delete the Starter-Pack-purchased key ("STARTER_PACK_STATUS") from Cloud Save so
                    // Starter Pack can be purchased again. This is used for testing to permit repurchasing
                    // this one-time-only product.
                    await SaveData.ForceDeleteAsync(k_StarterPackCloudSaveKey);
                    if (this == null) return;

                    await RefreshStarterPackStatus();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
                finally
                {
                    if (this != null)
                    {
                        StarterPackSampleView.instance.Enable();
                    }
                }
            }

            static async Task RefreshStarterPackStatus()
            {
                var starterPackIsClaimed = false;

                try
                {
                    // Read the "STARTER_PACK_STATUS" key from Cloud Save
                    var starterPackStatusCloudSaveResult = await SaveData.LoadAsync(
                        new HashSet<string> { k_StarterPackCloudSaveKey });

                    // If key is found, mark it as purchased if it it contains:  "claimed":true
                    if (starterPackStatusCloudSaveResult.TryGetValue(k_StarterPackCloudSaveKey, out var result))
                    {
                        Debug.Log($"{k_StarterPackCloudSaveKey} value: {result}");

                        if (result.Contains("\"claimed\":true"))
                        {
                            starterPackIsClaimed = true;
                        }
                    }
                    else
                    {
                        Debug.Log($"{k_StarterPackCloudSaveKey} key not set");
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
