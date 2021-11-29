using System;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;
using UnityEngine;

namespace GameOperationsSamples
{
    namespace LootBoxes
    {
        public class LootBoxesSceneManager : MonoBehaviour
        {
            public LootBoxesSampleView sceneView;

            public string cloudCodeScriptName = "GrantRandomCurrency";


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

                    await EconomyManager.instance.RefreshCurrencyBalances();
                    if (this == null) return;

                    sceneView.Enable();

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
                    Debug.Log($"Calling Cloud Code {cloudCodeScriptName} to grant random reward now.");

                    if (!AuthenticationService.Instance.IsSignedIn)
                    {
                        Debug.LogError("Cloud Code can't be called to grant random currency because you're not logged in.");
                        return;
                    }

                    sceneView.Disable();

                    // Call Cloud Code js script and wait for return values
                    var grantResult = await CloudCode.CallEndpointAsync<GrantRandomCurrencyResult>(
                        cloudCodeScriptName, new object());
                    if (this == null) return;

                    Debug.Log($"CloudCode script rewarded currency id: {grantResult.currencyId} amount: {grantResult.amount}");

                    await EconomyManager.instance.RefreshCurrencyBalances();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
                finally
                {
                    if (this != null)
                    {
                        sceneView.Enable();
                    }
                }
            }

            // Struct used to receive result from Cloud Code.
            public struct GrantRandomCurrencyResult
            {
                public string currencyId;
                public int amount;
            }
        }
    }
}
