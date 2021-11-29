using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;
using UnityEngine;

namespace GameOperationsSamples
{
    namespace DailyRewards
    {
        public class DailyRewardsSceneManager : MonoBehaviour
        {
            public DailyRewardsSampleView sceneView;

            public string cloudCodeCooldownScriptName = "GrantTimedRandomRewardCooldown";

            public string cloudCodeGrantScriptName = "GrantTimedRandomReward";

            int m_DefaultCooldownSeconds;

            int m_CooldownSeconds;


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

                    await UpdateEconomy();
                    if (this == null) return;

                    await UpdateCooldownStatusFromCloudCode();
                    if (this == null) return;

                    Debug.Log("Initialization and signin complete.");

                    await WaitForCooldown();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
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
                    sceneView.UpdateCooldown(m_CooldownSeconds);

                    await Task.Delay(1000);
                    if (this == null) return;

                    m_CooldownSeconds--;
                }

                sceneView.UpdateCooldown(m_CooldownSeconds);
            }

            public async void GrantTimedRandomReward()
            {
                try
                {
                    if (!AuthenticationService.Instance.IsSignedIn)
                    {
                        Debug.LogError("Cloud Code can't be called to grant the Daily Reward because you're not logged in.");
                        return;
                    }

                    Debug.Log($"Calling Cloud Code {cloudCodeGrantScriptName} to grant the Daily Reward now.");

                    sceneView.OnClaimingDailyReward();

                    var grantResult = await CloudCode.CallEndpointAsync<GrantResult>(
                        cloudCodeGrantScriptName, new object());
                    if (this == null) return;

                    Debug.Log($"CloudCode script rewarded: {grantResult}");

                    await UpdateEconomy();
                    if (this == null) return;

                    m_CooldownSeconds = m_DefaultCooldownSeconds;

                    await WaitForCooldown();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            async Task UpdateEconomy()
            {
                await Task.WhenAll(EconomyManager.instance.RefreshCurrencyBalances(),
                    EconomyManager.instance.RefreshInventory());
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

                public override string ToString()
                {
                    var grantResultString = new StringBuilder(64);

                    int currencyCount = currencyId.Count;
                    int inventoryCount = inventoryItemId.Count;
                    for (int i = 0; i < currencyCount; i++)
                    {
                        if (i == 0)
                        {
                            grantResultString.Append($"{currencyQuantity[i]} {currencyId[i]}(s)");
                        }
                        else
                        {
                            grantResultString.Append($", {currencyQuantity[i]} {currencyId[i]}(s)");
                        }
                    }

                    for (int i = 0; i < inventoryCount; i++)
                    {
                        if (i < inventoryCount - 1)
                        {
                            grantResultString.Append($", {inventoryItemQuantity[i]} {inventoryItemId[i]}(s)");
                        }
                        else
                        {
                            grantResultString.Append($" and {inventoryItemQuantity[i]} {inventoryItemId[i]}(s)");
                        }
                    }

                    return grantResultString.ToString();
                }
            }
        }
    }
}
