using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.Services.CloudCode;
using UnityEngine;

namespace UnityGamingServicesUseCases
{
    namespace SeasonalEvents
    {
        public class CloudCodeManager : MonoBehaviour
        {
            public static CloudCodeManager instance { get; private set; }

            // Cloud Code SDK exceptions.
            const int k_CloudCodeUnprocessableEntityExceptionErrorCode = 9009;
            const int k_CloudCodeRateLimitExceptionErrorCode = 50;
            const int k_CloudCodeMissingScriptExceptionErrorCode = 9002;

            // Cloud Code script errors.
            const int k_UntypedCustomScriptError = 0;
            const int k_GenericCloudCodeScriptError = 1;
            const int k_InvalidRewardDistributionAttemptScriptError = 2;
            const int k_ValidationScriptError = 400;
            const int k_RateLimitScriptError = 429;

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
                        ("SeasonalEvents_GrantEventReward", new GrantEventRewardRequest());

                    // Check that scene has not been unloaded while processing async wait to prevent throw.
                    if (this == null) return;

                    // The GrantEventReward script returns the total balance for each of the currencies that are
                    // distributed as part of the event reward. These total balances ultimately come from the
                    // Economy API which returns them to the Cloud Code script as part of the reward distribution,
                    // so we will update our currency HUD displays directly with these new balances.
                    foreach (var reward in updatedRewardBalances.grantedRewards)
                    {
                        EconomyManager.instance.SetCurrencyBalance(reward.id, reward.quantity);
                    }
                }
                catch (CloudCodeException e)
                {
                    HandleCloudCodeException(e);
                }
                catch (Exception e)
                {
                    Debug.Log("Problem calling cloud code endpoint: " + e.Message);
                    Debug.LogException(e);
                }
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
                    return new CloudCodeCustomError("Cloud Code Unprocessable Entity exception is in an " +
                                                    "unexpected format and couldn't be parsed.");
                }

                // Convert the message string ultimately into the Cloud Code Custom Error object which has a
                // standard structure for all errors.
                var parsedMessage = JsonUtility.FromJson<CloudCodeExceptionParsedMessage>(trimmedExceptionMessage);
                return JsonUtility.FromJson<CloudCodeCustomError>(parsedMessage.message);
            }

            // This method does whatever handling is appropriate given the specific errors this use case may trigger.
            void HandleCloudCodeScriptError(CloudCodeCustomError cloudCodeCustomError)
            {
                switch (cloudCodeCustomError.status)
                {
                    case k_InvalidRewardDistributionAttemptScriptError:
                        Debug.Log("No rewards were granted for completing the challenge: " +
                                  $"{cloudCodeCustomError.message}");
                        break;

                    case k_ValidationScriptError:
                        Debug.Log("Validation error during Cloud Code script execution:");
                        Debug.Log($"{cloudCodeCustomError.title}: {cloudCodeCustomError.message} : " +
                                  $"{cloudCodeCustomError.additionalDetails[0]}");
                        break;

                    case k_RateLimitScriptError:
                        Debug.Log($"Rate Limit has been exceeded. Wait {cloudCodeCustomError.retryAfter} " +
                                  $"seconds and try again.");
                        break;

                    case k_GenericCloudCodeScriptError:
                        Debug.Log("A problem occured while trying to grant seasonal rewards: "
                                  + cloudCodeCustomError.message);
                        break;

                    case k_UntypedCustomScriptError:
                        Debug.Log($"Cloud code returned error: {cloudCodeCustomError.status}: " +
                                  $"{cloudCodeCustomError.title}: {cloudCodeCustomError.message}");
                        break;

                    default:
                        Debug.Log($"Cloud code returned error: {cloudCodeCustomError.status}: " +
                                  $"{cloudCodeCustomError.title}: {cloudCodeCustomError.message}");
                        break;
                }
            }

            void OnDestroy()
            {
                if (instance == this)
                {
                    instance = null;
                }
            }

            public struct GrantEventRewardRequest
            {
            }

            public struct GrantEventRewardResult
            {
                public RewardDetail[] grantedRewards;
            }

            public struct CloudCodeExceptionParsedMessage
            {
                public string message;
            }

            public struct CloudCodeCustomError
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
