using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using Unity.Services.Economy;
using UnityEngine;

namespace Unity.Services.Samples.StarterPack
{
    public class StarterPackSceneManager : MonoBehaviour
    {
        public static event Action<bool> starterPackStatusChecked;

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

                await EconomyManager.instance.RefreshEconomyConfiguration();
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
                    StarterPackSampleView.instance.SetInteractable();
                }
            }
        }

        public async void OnBuyButtonPressed()
        {
            try
            {
                StarterPackSampleView.instance.SetInteractable(false);

                await CloudCodeManager.instance.CallPurchaseStarterPackEndpoint();
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
                    StarterPackSampleView.instance.SetInteractable();
                }
            }
        }

        public async void OnGiveTenGemsButtonPressed()
        {
            try
            {
                StarterPackSampleView.instance.SetInteractable(false);

                var balanceResponse = await EconomyService.Instance.PlayerBalances.IncrementBalanceAsync("GEM", 10);
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
                    StarterPackSampleView.instance.SetInteractable();
                }
            }
        }

        public async void OnResetPlayerDataButtonPressed()
        {
            try
            {
                StarterPackSampleView.instance.SetInteractable(false);

                // Delete the Starter-Pack-purchased key ("STARTER_PACK_STATUS") from Cloud Save so
                // Starter Pack can be purchased again. This is used for testing to permit repurchasing
                // this one-time-only product.
                await CloudSaveService.Instance.Data.ForceDeleteAsync(k_StarterPackCloudSaveKey);
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
                    StarterPackSampleView.instance.SetInteractable();
                }
            }
        }

        static async Task RefreshStarterPackStatus()
        {
            var starterPackIsClaimed = false;

            try
            {
                // Read the "STARTER_PACK_STATUS" key from Cloud Save
                var starterPackStatusCloudSaveResult = await CloudSaveService.Instance.Data.LoadAsync(
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

            starterPackStatusChecked?.Invoke(starterPackIsClaimed);
        }
    }
}
