using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;
using Unity.Services.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace GameOperationsSamples
{
    namespace DailyRewards
    {
        public class DailyRewardsSceneManager : MonoBehaviour
        {
            public string cloudCodeCooldownScriptName = "GrantTimedRandomRewardCooldown";

            public string cloudCodeGrantScriptName = "GrantTimedRandomReward";

            public Button grantRandomRewardButton;

            public TextMeshProUGUI grantRandomRewardButtonText;

            public CurrencyHudView[] currencyHudViews;

            public InventoryHudView inventoryHudView;

            int m_DefaultCooldownSeconds;

            int m_CooldownSeconds;

            bool m_CurrencyBalancesUpdatedFlag = false;
            bool m_InventoryItemsUpdatedFlag = false;


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
                Debug.Log("Signing in...");

                AuthenticationService.Instance.SignedIn += SignedIn;
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            async void SignedIn()
            {
                AuthenticationService.Instance.SignedIn -= SignedIn;

                // Check that scene has not been unloaded while processing SignInAnonymouslyAsync.
                if (this == null) return;

                Debug.Log($"Player id:{AuthenticationService.Instance.PlayerId}");

                await UpdateEconomy();
                if (this == null) return;

                await UpdateCooldownStatusFromCloudCode();
                if (this == null) return;

                ShowCooldownStatus();

                Debug.Log("Initialization and signin complete.");

                await WaitForCooldown();
            }

            async Task UpdateCooldownStatusFromCloudCode()
            {
                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    Debug.LogError("Cloud Code can't be called because you're not logged in.");
                    return;
                }

                try
                {
                    var grantCooldownResult = await CloudCode.CallEndpointAsync<GrantCooldownResult>(
                        cloudCodeCooldownScriptName, new object());
                    if (this == null) return;

                    Debug.Log($"Retrieved cooldown flag:{grantCooldownResult.canGrantFlag} time:{grantCooldownResult.grantCooldown} default:{grantCooldownResult.defaultCooldown}");

                    m_DefaultCooldownSeconds = grantCooldownResult.defaultCooldown;
                    m_CooldownSeconds = grantCooldownResult.grantCooldown;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            async Task WaitForCooldown()
            {
                while (m_CooldownSeconds > 0)
                {
                    ShowCooldownStatus();

                    await Task.Delay(1000);
                    if (this == null) return;

                    m_CooldownSeconds--;
                }

                ShowCooldownStatus();
            }

            void ShowCooldownStatus()
            {
                if (m_CooldownSeconds > 0)
                {
                    grantRandomRewardButton.interactable = false;
                    grantRandomRewardButtonText.text = m_CooldownSeconds > 1
                        ? $"... ready in {m_CooldownSeconds} seconds."
                        : "... ready in 1 second.";
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

                Debug.Log($"Calling Cloud Code {cloudCodeGrantScriptName} to grant the Daily Reward now.");

                grantRandomRewardButton.interactable = false;
                grantRandomRewardButtonText.text = "Claiming Daily Reward";

                try
                {
                    var grantResult = await CloudCode.CallEndpointAsync<GrantResult>(
                        cloudCodeGrantScriptName, new object());
                    if (this == null) return;

                    Debug.Log("CloudCode script rewarded: " + GetGrantInfoString(grantResult));
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                await UpdateEconomy();
                if (this == null) return;

                m_CooldownSeconds = m_DefaultCooldownSeconds;

                await WaitForCooldown();
            }

            async Task UpdateEconomy()
            {
                Debug.Log("Updating Economy balances and items...");

                m_CurrencyBalancesUpdatedFlag = false;
                m_InventoryItemsUpdatedFlag = false;

                var delayMs = 100;
                do
                {
                    try
                    {
                        await Task.WhenAll(UpdateCurrencyBalances(), 
                            UpdateInventoryItems());
                        if (this == null) return;
                    }
                    catch (EconomyException e)
                    when (e.Reason == EconomyExceptionReason.RateLimited)
                    {
                        // If the rate-limited exception occurs, use exponential back-off when retrying
                        await Task.Delay(delayMs);
                        if (this == null) return;

                        Debug.Log($"Retrying UpdateEconomyData due to rate-limit exception after {delayMs}ms delay.");

                        delayMs *= 2;
                    }
                }
                while (!m_CurrencyBalancesUpdatedFlag || !m_InventoryItemsUpdatedFlag);

                Debug.Log("Economy update complete...");
            }

            async Task UpdateCurrencyBalances()
            {
                if (m_CurrencyBalancesUpdatedFlag) return;

                long totalCurrencyQuantity = 0;

                var balancesOptions = new PlayerBalances.GetBalancesOptions { ItemsPerFetch = 100 };
                var getBalancesResult = await Economy.PlayerBalances.GetBalancesAsync(balancesOptions);
                if (this == null) return;

                foreach (var balance in getBalancesResult.Balances)
                {
                    totalCurrencyQuantity += balance.Balance;

                    foreach (var currencyHudView in currencyHudViews)
                    {
                        currencyHudView.UpdateBalanceField(balance.CurrencyId, balance.Balance);
                    }
                }

                m_CurrencyBalancesUpdatedFlag = true;

                Debug.Log($"Currency balances retrieved and updated. Total quantity of all Currencies: {totalCurrencyQuantity}");
            }

            async Task UpdateInventoryItems()
            {
                if (m_InventoryItemsUpdatedFlag) return;

                var options = new PlayerInventory.GetInventoryOptions { ItemsPerFetch = 100 };
                var response = await Economy.PlayerInventory.GetInventoryAsync(options);
                if (this == null) return;

                inventoryHudView.Refresh(response.PlayersInventoryItems);

                m_InventoryItemsUpdatedFlag = true;

                var totalInventoryItems = response.PlayersInventoryItems.Count;
                Debug.Log($"Inventory items retrieved and updated. Total inventory item count: {totalInventoryItems}");
            }

            string GetGrantInfoString(GrantResult grantResult)
            {
                string grantResultString = "";

                int currencyCount = grantResult.currencyId.Count;
                int inventoryCount = grantResult.inventoryItemId.Count;
                for (int i = 0; i < currencyCount; i++)
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

                for (int i = 0; i < inventoryCount; i++)
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

            // Struct matches response from the Cloud Code grant call to receive the list of currencies and inventory items granted
            public struct GrantCooldownResult
            {
                public bool canGrantFlag;

                public int grantCooldown;

                public int defaultCooldown;
            }

            // Struct used to receive the result of the Daily Reward from Cloud Code
            public struct GrantResult
            {
                public List<string> currencyId;

                public List<int> currencyQuantity;

                public List<string> inventoryItemId;

                public List<int> inventoryItemQuantity;
            }
        }
    }
}
