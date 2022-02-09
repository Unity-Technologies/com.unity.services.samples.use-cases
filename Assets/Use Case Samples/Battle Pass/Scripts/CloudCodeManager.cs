using System;
using System.Threading.Tasks;
using Unity.Services.CloudCode;
using UnityEngine;

namespace GameOperationsSamples
{
    namespace BattlePass
    {
        public class CloudCodeManager : MonoBehaviour
        {
            public static CloudCodeManager instance { get; private set; }

            void Awake()
            {
                if (instance != null && instance != this)
                {
                    Destroy(this);
                }
                else
                {
                    instance = this;
                }
            }

            void OnDestroy()
            {
                if (instance == this)
                {
                    instance = null;
                }
            }

            public async Task<GetProgressResult> CallGetProgressEndpoint()
            {
                try
                {
                    Debug.Log("Getting current Battle Pass progress via Cloud Code...");

                    // The CallEndpointAsync method requires two objects to be passed in: the name of the script being
                    // called, and a struct for any arguments that need to be passed to the script. In this sample,
                    // we didn't need to pass any additional arguments, so we're passing an empty string. You could
                    // pass an empty struct. See CallGainSeasonXpEndpoint for an example with non-empty args.
                    var result = await CloudCode.CallEndpointAsync<GetProgressResult>
                        ("BattlePass_GetProgress", "");

                    // Check that scene has not been unloaded while processing async wait to prevent throw.
                    return this == null ? default : result;
                }
                catch (Exception e)
                {
                    Debug.Log("Problem calling cloud code endpoint: " + e.Message);
                    Debug.LogException(e);
                }

                return default;
            }

            public async Task<GainSeasonXpResult> CallGainSeasonXpEndpoint(int xpToGain)
            {
                try
                {
                    Debug.Log("Gaining Season XP via Cloud Code...");

                    var request = new GainSeasonXpRequest { amount = xpToGain };

                    var result = await CloudCode.CallEndpointAsync<GainSeasonXpResult>("BattlePass_GainSeasonXP", request);

                    // Check that scene has not been unloaded while processing async wait to prevent throw.
                    return this == null ? default : result;
                }
                catch (Exception e)
                {
                    Debug.Log("Problem calling cloud code endpoint: " + e.Message);
                    Debug.LogException(e);
                }

                return default;
            }

            public async Task<PurchaseBattlePassResult> CallPurchaseBattlePassEndpoint()
            {
                try
                {
                    Debug.Log("Purchasing the current Battle Pass via Cloud Code...");

                    var result = await CloudCode.CallEndpointAsync<PurchaseBattlePassResult>("BattlePass_PurchaseBattlePass", "");

                    // Check that scene has not been unloaded while processing async wait to prevent throw.
                    return this == null ? default : result;
                }
                catch (Exception e)
                {
                    Debug.Log("Problem calling cloud code endpoint: " + e.Message);
                    Debug.LogException(e);
                }

                return default;
            }

            public async Task<ClaimTierResult> CallClaimTierEndpoint(int tierIndexToClaim)
            {
                try
                {
                    Debug.Log($"Claiming tier {tierIndexToClaim + 1} via Cloud Code...");

                    var request = new ClaimTierRequest { tierIndex = tierIndexToClaim };

                    var result = await CloudCode.CallEndpointAsync<ClaimTierResult>("BattlePass_ClaimTier", request);

                    // Check that scene has not been unloaded while processing async wait to prevent throw.
                    return this == null ? default : result;
                }
                catch (Exception e)
                {
                    Debug.Log("Problem calling cloud code endpoint: " + e.Message);
                    Debug.LogException(e);
                }

                return default;
            }
            
            public struct ResultReward
            {
                public string service;
                public string id;
                public int quantity;
                public string spriteAddress;
            }

            public struct GetProgressResult
            {
                public int seasonXp;
                public bool ownsBattlePass;
                public int[] seasonTierStates;
            }

            public struct GainSeasonXpRequest
            {
                public int amount;
            }

            public struct GainSeasonXpResult
            {
                public int seasonXp;
                public int unlockedNewTier;
                public string validationResult;
                public int[] seasonTierStates;
            }

            public struct PurchaseBattlePassResult
            {
                public string purchaseResult;
                public ResultReward[] grantedRewards;
                public int[] seasonTierStates;
            }

            public struct ClaimTierRequest
            {
                public int tierIndex;
            }

            public struct ClaimTierResult
            {
                public string validationResult;
                public ResultReward[] grantedRewards;
                public int[] seasonTierStates;
            }
        }
    }
}
