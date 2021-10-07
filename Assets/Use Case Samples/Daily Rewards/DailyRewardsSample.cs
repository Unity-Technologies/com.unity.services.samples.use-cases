using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;
using Unity.Services.Economy;
using Unity.Services.Economy.Model;
using UnityEngine;
using UnityEngine.UI;

namespace GameOperationsSamples
{
    public class DailyRewardsSample : MonoBehaviour
    {
        public string cloudCodeCooldownScriptName = "GrantTimedRandomRewardCooldown";

        [SerializeField]
        public string cloudCodeGrantScriptName = "GrantTimedRandomReward";

        [SerializeField]
        public Button grantRandomRewardButton;

        [SerializeField] 
        TextMeshProUGUI grantRandomRewardButtonText;

        [SerializeField]
        public CurrencyHudView[] currencyHudViews;

        [SerializeField]
        public InventoryHudView inventoryHudView;

        [SerializeField]
        public CanvasGroup signinScreen;

        int defaultCooldownSeconds;

        int cooldownSeconds;

        bool currencyBalancesUpdatedFlag = false;
        bool inventoryItemsUpdatedFlag = false;

        bool didDestroyFlag = false;


        async void Start()
        {
            Debug.Log("Initializing Unity Services...");

            await UnityServices.InitializeAsync();

            if (didDestroyFlag) return;

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

            if (didDestroyFlag) return;

            await UpdateEconomy();

            if (didDestroyFlag) return;

            await UpdateCooldownStatusFromCloudCode();

            if (didDestroyFlag) return;

            ShowCooldownStatus();

            SampleInitialized();

            await WaitForCooldown();
        }

        void SampleInitialized()
        {
            Debug.Log("Initialization and signin complete.");

            signinScreen.gameObject.SetActive(false);
        }

        async Task UpdateCooldownStatusFromCloudCode()
        {
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                Debug.LogError("Cloud Code can't be called because you're not logged in.");
                return;
            }

            var grantCooldownResult = await CloudCode.CallEndpointAsync<GrantCooldownResult>(
                cloudCodeCooldownScriptName, new object());

            if (didDestroyFlag) return;

            Debug.Log($"Retrieved cooldown flag:{grantCooldownResult.canGrantFlag} time:{grantCooldownResult.grantCooldown} default:{grantCooldownResult.defaultCooldown}");

            defaultCooldownSeconds = grantCooldownResult.defaultCooldown;
            cooldownSeconds = grantCooldownResult.grantCooldown;
        }

        async Task WaitForCooldown()
        {
            if (didDestroyFlag) return;

            while (cooldownSeconds > 0)
            {
                ShowCooldownStatus();

                await Task.Delay(1000);

                if (didDestroyFlag) return;

                --cooldownSeconds;
            }

            ShowCooldownStatus();
        }

        void ShowCooldownStatus()
        {
            if (cooldownSeconds > 0)
            {
                grantRandomRewardButton.interactable = false;
                if (cooldownSeconds > 1)
                {
                    grantRandomRewardButtonText.text = $"... ready in {cooldownSeconds} seconds.";
                }
                else
                {
                    grantRandomRewardButtonText.text = $"... ready in 1 second.";
                }
            }
            else
            {
                grantRandomRewardButton.interactable = true;
                grantRandomRewardButtonText.text = "Claim Daily Reward";
            }
        }

        public async void GrantTimedRandomReward()
        {
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                Debug.LogError("Cloud Code can't be called to grant the Daily Reward because you're not logged in.");
                return;
            }

            Debug.Log("---------------------------------------------------------------------");
            Debug.Log($"Calling Cloud Code {cloudCodeGrantScriptName} to grant the Daily Reward now.");

            grantRandomRewardButton.interactable = false;
            grantRandomRewardButtonText.text = "Claiming Daily Reward";

            var grantResult = await CloudCode.CallEndpointAsync<GrantResult>(
                cloudCodeGrantScriptName, new object());

            if (didDestroyFlag) return;

            Debug.Log("CloudCode script rewarded: " + GetGrantInfoString(grantResult));

            await UpdateEconomy();

            if (didDestroyFlag) return;

            cooldownSeconds = defaultCooldownSeconds;

            await WaitForCooldown();
        }

        public async Task UpdateEconomy()
        {
            Debug.Log($"Updating Economy balances and items...");

            currencyBalancesUpdatedFlag = false;
            inventoryItemsUpdatedFlag = false;

            var delayMs = 100;
            do
            {
                if (didDestroyFlag) return;

                try
                {
                    await Task.WhenAll(
                        UpdateCurrencyBalances(),
                        UpdateInventoryItems());
                }
                catch (Unity.Services.Economy.EconomyException e)
                when (e.Reason == EconomyExceptionReason.RateLimited)
                {
                    // If the rate-limited exception occurs, use exponential back-off when retrying
                    await Task.Delay(delayMs);

                    Debug.Log($"Retrying UpdateEconomyData due to rate-limit exception after {delayMs}ms delay.");

                    delayMs *= 2;
                }
            }
            while (!currencyBalancesUpdatedFlag || !inventoryItemsUpdatedFlag);

            Debug.Log($"Economy update complete...");
        }

        async Task UpdateCurrencyBalances()
        {
            if (currencyBalancesUpdatedFlag) return;

            long totalCurrencyQuantity = 0;

            var balancesOptions = new PlayerBalances.GetBalancesOptions { ItemsPerFetch = 100 };
            var getBalancesResult = await Economy.PlayerBalances.GetBalancesAsync(balancesOptions);

            if (didDestroyFlag) return;

            foreach (var balance in getBalancesResult.Balances)
            {
                totalCurrencyQuantity += balance.Balance;

                foreach (var currencyHudView in currencyHudViews)
                {
                    currencyHudView.UpdateBalanceField(balance.CurrencyId, balance.Balance);
                }
            }

            currencyBalancesUpdatedFlag = true;

            Debug.Log($"Currency balances retrieved and updated. Total quantity of all Currencies: {totalCurrencyQuantity}");
        }

        async Task UpdateInventoryItems()
        {
            if (inventoryItemsUpdatedFlag) return;

            var options = new PlayerInventory.GetInventoryOptions { ItemsPerFetch = 100 };
            var response = await Economy.PlayerInventory.GetInventoryAsync(options);

            if (didDestroyFlag) return;

            inventoryHudView.Refresh(response.PlayersInventoryItems);

            inventoryItemsUpdatedFlag = true;

            var totalInventoryItems = response.PlayersInventoryItems.Count;
            Debug.Log($"Inventory items retrieved and updated. Total inventory item count: {totalInventoryItems}");
        }

        string GetGrantInfoString(GrantResult grantResult)
        {
            string grantResultString = "";

            int currencyCount = grantResult.currencyId.Count;
            int inventoryCount = grantResult.inventoryItemId.Count;
            for (int i = 0; i < currencyCount; ++i)
            {
                if (i == 0)
                {
                    grantResultString += $"{grantResult.currencyQuantity[i]} {grantResult.currencyId[i]}(s)";
                }
                else
                {
                    grantResultString += $", {grantResult.currencyQuantity[i]} {grantResult.currencyId[i]}(s)";
                }
            }

            for (int i = 0; i < inventoryCount; ++i)
            {
                if (i < inventoryCount - 1)
                {
                    grantResultString += $", {grantResult.inventoryItemQuantity[i]} {grantResult.inventoryItemId[i]}(s)";
                }
                else
                {
                    grantResultString += $" and {grantResult.inventoryItemQuantity[i]} {grantResult.inventoryItemId[i]}(s)";
                }
            }

            return grantResultString;
        }

        // Remember this object has been destroyed, so avoid using it to cause an exception
        public void OnDestroy()
        {
            didDestroyFlag = true;
        }

        // Class matches response from the Cloud Code grant call to receive the list of currencies and inventory items granted
        class GrantCooldownResult
        {
            #pragma warning disable CS0649
            public bool canGrantFlag;

            public int grantCooldown;

            public int defaultCooldown;
            #pragma warning restore CS0649
        }

        // Class used to receive the result of the Daily Reward from Cloud Code
        class GrantResult
        {
            #pragma warning disable CS0649
            public List<string> currencyId;

            public List<int> currencyQuantity;

            public List<string> inventoryItemId;

            public List<int> inventoryItemQuantity;
            #pragma warning restore CS0649
        }
    }
}
