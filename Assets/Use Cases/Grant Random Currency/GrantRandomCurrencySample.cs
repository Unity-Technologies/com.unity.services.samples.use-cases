using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;
using Unity.Services.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace GameOperationsSamples
{
    public class GrantRandomCurrencySample : MonoBehaviour
    {
        [SerializeField]
        public string cloudCodeScriptName = "GrantRandomCurrency";

        [SerializeField]
        public Button grantRandomRewardButton;

        [SerializeField]
        public CurrencyHudView[] currencyHudViews;

        [SerializeField]
        public CanvasGroup signinScreen;

        bool didDestroyFlag = false;


        static event Action<string, long> OnCurrencyBalanceChange;


        async void Start()
        {
            Debug.Log("Initializing Unity Services...");

            await UnityServices.InitializeAsync();

            foreach (var currencyHudView in currencyHudViews)
            {
                OnCurrencyBalanceChange += currencyHudView.UpdateBalanceField;
            }

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

            Debug.Log($"Player id:{AuthenticationService.Instance.PlayerId}");

            await UpdateBalancesView();

            SampleInitialized();
        }

        async Task UpdateBalancesView()
        {
            if (didDestroyFlag) return;

            Debug.Log("Retrieving currency balances...");

            var balancesOptions = new PlayerBalances.GetBalancesOptions { ItemsPerFetch = 100 };
            var getBalancesResult = await Economy.PlayerBalances.GetBalancesAsync(balancesOptions);

            if (didDestroyFlag) return;

            foreach (var balance in getBalancesResult.Balances)
            {
                OnCurrencyBalanceChange?.Invoke(balance.CurrencyId, balance.Balance);
            }
        }

        void SampleInitialized()
        {
            Debug.Log("Initialization and signin complete.");

            signinScreen.gameObject.SetActive(false);
        }

        // Class used to receive result from Cloud Code.
        class GrantRandomCurrencyResult
        {
            public string currencyId = "";
            public int amount = 0;
        }

        public async void GrantRandomCurrency()
        {
            Debug.Log("---------------------------------------------------------------------");
            Debug.Log($"Calling Cloud Code {cloudCodeScriptName} to grant random reward now.");

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                Debug.LogError("Cloud Code can't be called to grant random currency because you're not logged in.");
                return;
            }

            grantRandomRewardButton.interactable = false;

            // Call Cloud Code js script and wait for return values
            var grantResult = await CloudCode.CallEndpointAsync<GrantRandomCurrencyResult>(
                cloudCodeScriptName, new object());

            if (didDestroyFlag) return;

            Debug.Log($"CloudCode script rewarded currency id: {grantResult.currencyId} amount: {grantResult.amount}");

            await UpdateBalancesView();

            if (didDestroyFlag) return;

            grantRandomRewardButton.interactable = true;
        }

        // Set flag so UI is not touched after app terminates.
        public void OnDestroy()
        {
            didDestroyFlag = true;

            foreach (var currencyHudView in currencyHudViews)
            {
                OnCurrencyBalanceChange -= currencyHudView.UpdateBalanceField;
            }
        }
    }
}
