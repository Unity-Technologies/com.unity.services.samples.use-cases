using System;
using System.Threading.Tasks;
using Unity.Services.CloudCode;
using UnityEngine;

namespace GameOperationsSamples
{
    namespace SeasonalEvents
    {
        public class CloudCodeManager : MonoBehaviour
        {
            public static CloudCodeManager instance { get; private set; }

            void Awake()
            {
                if (instance != null && instance != this)
                {
                    Destroy(gameObject);
                }
                else
                {
                    instance = this;
                }
            }

            public async Task CallGrantEventRewardEndpoint()
            {
                try
                {
                    Debug.Log("Collecting event rewards via Cloud Code...");

                    // The CallEndpointAsync method requires two objects to be passed in: the name of the script being
                    // called, and a struct for any arguments that need to be passed to the script. In this sample,
                    // we didn't need to pass any additional arguments, so we're passing an empty struct. Alternatively,
                    // you could pass an empty string.
                    var updatedRewardBalances = await CloudCode.CallEndpointAsync<GrantEventRewardResult>
                        ("GrantEventReward", new GrantEventRewardRequest());

                    // Check that scene has not been unloaded while processing async wait to prevent throw.
                    if (this == null) return;

                    // The GrantEventReward script returns an empty array when no rewards are granted due to the
                    // current season cycle's challenge already having been completed and rewards distributed.
                    if (updatedRewardBalances.grantedRewards is null ||
                        updatedRewardBalances.grantedRewards.Length <= 0)
                    {
                        Debug.Log("No rewards were granted for completing the challenge.");
                    }
                    else
                    {
                        // The GrantEventReward script returns the total balance for each of the currencies that are distributed
                        // as part of the event reward. These total balances ultimately come from the Economy API which returns
                        // them to the Cloud Code script as part of the reward distribution, so we will update our currency HUD
                        // displays directly with these new balances.
                        foreach (var reward in updatedRewardBalances.grantedRewards)
                        {
                            EconomyManager.instance.SetCurrencyBalance(reward.id, reward.quantity);
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.Log("Problem calling cloud code endpoint: " + e.Message);
                    Debug.LogException(e);
                }
            }

            void OnDestroy()
            {
                instance = null;
            }

            public struct GrantEventRewardRequest
            {
            }

            public struct GrantEventRewardResult
            {
                public RewardDetail[] grantedRewards;
            }
        }
    }
}
