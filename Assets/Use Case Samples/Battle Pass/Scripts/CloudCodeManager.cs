using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.Services.CloudCode;
using UnityEngine;

namespace UnityGamingServicesUseCases
{
    namespace BattlePass
    {
        public class CloudCodeManager : MonoBehaviour
        {
            // Unity Gaming Services error codes
            const int k_CloudCodeUnprocessableEntityExceptionErrorCode = 9009;
            const int k_CloudCodeRateLimitExceptionErrorCode = 50;
            const int k_CloudCodeMissingScriptExceptionErrorCode = 9002;

            // HTTP REST API error codes
            const int k_ValidationScriptError = 400;
            const int k_RateLimitScriptError = 429;

            // Custom error codes
            private const int k_CantAffordBattlePassError = 3;

            public static CloudCodeManager instance { get; private set; }

            public BattlePassSampleView sceneView;

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
                    return await CloudCode.CallEndpointAsync<GetProgressResult>("BattlePass_GetProgress", "");
                }
                catch (CloudCodeException e)
                {
                    HandleCloudCodeException(e);

                    throw new CloudCodeResultUnavailableException(
                        e, $"Handled exception in {nameof(CallGetProgressEndpoint)}.");
                }
                catch (Exception e)
                {
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

                    return await CloudCode.CallEndpointAsync<GainSeasonXpResult>("BattlePass_GainSeasonXP", request);
                }
                catch (CloudCodeException e)
                {
                    HandleCloudCodeException(e);

                    throw new CloudCodeResultUnavailableException(
                        e, $"Handled exception in {nameof(CallGainSeasonXpEndpoint)}.");
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                return default;
            }

            public async Task<PurchaseBattlePassResult> CallPurchaseBattlePassEndpoint()
            {
                try
                {
                    Debug.Log("Purchasing the current Battle Pass via Cloud Code...");

                    return await CloudCode.CallEndpointAsync<PurchaseBattlePassResult>("BattlePass_PurchaseBattlePass", "");
                }
                catch (CloudCodeException e)
                {
                    HandleCloudCodeException(e);

                    throw new CloudCodeResultUnavailableException(
                        e, $"Handled exception in {nameof(CallPurchaseBattlePassEndpoint)}.");
                }
                catch (Exception e)
                {
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

                    return await CloudCode.CallEndpointAsync<ClaimTierResult>("BattlePass_ClaimTier", request);
                }
                catch (CloudCodeException e)
                {
                    HandleCloudCodeException(e);

                    throw new CloudCodeResultUnavailableException(
                        e, $"Handled exception in {nameof(CallClaimTierEndpoint)}.");
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                return default;
            }

            void HandleCloudCodeException(CloudCodeException e)
            {
                switch (e.ErrorCode)
                {
                    case k_CloudCodeUnprocessableEntityExceptionErrorCode:
                        var cloudCodeCustomError = ConvertToActionableError(e);
                        HandleCloudCodeScriptError(cloudCodeCustomError);
                        break;

                    case k_CloudCodeRateLimitExceptionErrorCode:
                        Debug.Log("Rate Limit Exceeded. Try Again.");
                        break;

                    case k_CloudCodeMissingScriptExceptionErrorCode:
                        Debug.Log("Couldn't find requested Cloud Code Script");
                        break;

                    default:
                        Debug.Log(e);
                        break;
                }
            }

            CloudCodeCustomError ConvertToActionableError(CloudCodeException e)
            {
                // trim the text that's in front of the valid JSON
                var trimmedExceptionMessage = Regex.Replace(
                    e.Message, @"^[^\{]*", "", RegexOptions.IgnorePatternWhitespace);

                if (string.IsNullOrEmpty(trimmedExceptionMessage))
                {
                    return new CloudCodeCustomError("Could not parse CloudCodeException.");
                }

                // Convert the message string ultimately into the Cloud Code Custom Error object which has a
                // standard structure for all errors.
                var parsedMessage = JsonUtility.FromJson<CloudCodeExceptionParsedMessage>(trimmedExceptionMessage);
                return JsonUtility.FromJson<CloudCodeCustomError>(parsedMessage.message);
            }

            void HandleCloudCodeScriptError(CloudCodeCustomError cloudCodeCustomError)
            {
                switch (cloudCodeCustomError.status)
                {
                    case k_CantAffordBattlePassError:
                        sceneView.ShowCantAffordBattlePassPopup();
                        break;

                    case k_ValidationScriptError:
                        Debug.Log($"{cloudCodeCustomError.title}: {cloudCodeCustomError.message} : " +
                                  $"{cloudCodeCustomError.additionalDetails[0]}");
                        break;

                    case k_RateLimitScriptError:
                        Debug.Log($"Rate Limit has been exceeded. Wait {cloudCodeCustomError.retryAfter} " +
                                  $"seconds and try again.");
                        break;

                    default:
                        Debug.Log($"Cloud code returned error: {cloudCodeCustomError.status}: " +
                                  $"{cloudCodeCustomError.title}: {cloudCodeCustomError.message}");
                        break;
                }
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

            struct CloudCodeExceptionParsedMessage
            {
                public string message;
            }

            struct CloudCodeCustomError
            {
                public int status;
                public string title;
                public string message;
                public string retryAfter;
                public string[] additionalDetails;

                public CloudCodeCustomError(string title)
                {
                    this.title = title;
                    status = 0;
                    message = null;
                    retryAfter = null;
                    additionalDetails = new string[] { };
                }
            }
        }
    }
}
