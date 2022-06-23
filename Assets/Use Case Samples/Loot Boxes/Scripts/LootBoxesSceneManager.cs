using System;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;
using UnityEngine;

namespace UnityGamingServicesUseCases
{
    namespace LootBoxes
    {
        public class LootBoxesSceneManager : MonoBehaviour
        {
            public LootBoxesSampleView sceneView;


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

                    Debug.Log($"Player id:{AuthenticationService.Instance.PlayerId}");

                    // Economy configuration should be refreshed every time the app initializes.
                    // Doing so updates the cached configuration data and initializes for this player any items or
                    // currencies that were recently published.
                    // 
                    // It's important to do this update before making any other calls to the Economy or Remote Config
                    // APIs as both use the cached data list. (Though it wouldn't be necessary to do if only using Remote
                    // Config in your project and not Economy.)
                    await EconomyManager.instance.RefreshEconomyConfiguration();
                    if (this == null) return;

                    await EconomyManager.instance.RefreshCurrencyBalances();
                    if (this == null) return;

                    sceneView.SetInteractable();

                    Debug.Log("Initialization and signin complete.");
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            public async void GrantRandomCurrency()
            {
                try
                {
                    sceneView.SetInteractable(false);

                    // Call Cloud Code js script and wait for grant to complete.
                    await CloudCodeManager.instance.CallGrantRandomCurrencyEndpoint();
                    if (this == null) return;

                    await EconomyManager.instance.RefreshCurrencyBalances();
                }
                catch (CloudCodeResultUnavailableException)
                {
                    // Exception already handled by CloudCodeManager
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
                finally
                {
                    if (this != null)
                    {
                        sceneView.SetInteractable();
                    }
                }
            }
        }
    }
}
