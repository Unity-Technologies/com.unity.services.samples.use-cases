using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;
using Unity.Services.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace GameOperationsSamples
{
    namespace LootBoxes
    {
        public class LootBoxesSceneManager : MonoBehaviour
        {
            public string cloudCodeScriptName = "GrantRandomCurrency";

            public Button grantRandomRewardButton;

            public CurrencyHudView[] currencyHudViews;


            async void Start()
            {
                Debug.Log("Initializing Unity Services...");

                await UnityServices.InitializeAsync();

                // Check that scene has not been unloaded while processing async wait to prevent throw.
                if (this == null) return;

                SignIn();
            }

            async void SignIn()
            {
                AuthenticationService.Instance.SignedIn += SignedIn;

                Debug.Log("Signing in...");

                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            async void SignedIn()
            {
                AuthenticationService.Instance.SignedIn -= SignedIn;

                // Check that scene has not been unloaded while processing SignInAnonymouslyAsync.
                if (this == null) return;

                Debug.Log($"Player id:{AuthenticationService.Instance.PlayerId}");

                await UpdateBalancesView();
                if (this == null) return;

                grantRandomRewardButton.interactable = true;

                Debug.Log("Initialization and signin complete.");
            }

            async Task UpdateBalancesView()
            {
                Debug.Log("Retrieving currency balances...");

                var balancesOptions = new PlayerBalances.GetBalancesOptions { ItemsPerFetch = 100 };
                var getBalancesResult = await Economy.PlayerBalances.GetBalancesAsync(balancesOptions);
                if (this == null) return;

                foreach (var balance in getBalancesResult.Balances)
                {
                    foreach (var currencyHudView in currencyHudViews)
                    {
                        currencyHudView.UpdateBalanceField(balance.CurrencyId, balance.Balance);
                    }
                }
            }

            public async void GrantRandomCurrency()
            {
                Debug.Log($"Calling Cloud Code {cloudCodeScriptName} to grant random reward now.");

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    Debug.LogError("Cloud Code can't be called to grant random currency because you're not logged in.");
                    return;
                }

                grantRandomRewardButton.interactable = false;

                try
                {
                    // Call Cloud Code js script and wait for return values
                    var grantResult = await CloudCode.CallEndpointAsync<GrantRandomCurrencyResult>(
                        cloudCodeScriptName, new object());
                    if (this == null) return;

                    Debug.Log($"CloudCode script rewarded currency id: {grantResult.currencyId} amount: {grantResult.amount}");
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                await UpdateBalancesView();
                if (this == null) return;

                grantRandomRewardButton.interactable = true;
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
