using System;
using System.Collections.Generic;
using System.Linq;
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

            // Cloud Code SDK status codes from Client
            const int k_CloudCodeRateLimitExceptionStatusCode = 50;
            const int k_CloudCodeMissingScriptExceptionStatusCode = 9002;
            const int k_CloudCodeUnprocessableEntityExceptionStatusCode = 9009;

            // HTTP REST API status codes
            const int k_HttpBadRequestStatusCode = 400;
            const int k_HttpTooManyRequestsStatusCode = 429;

            // Custom status codes
            const int k_UnexpectedFormatCustomStatusCode = int.MinValue;
            const int k_GenericCloudCodeScriptStatusCode = 1;
            const int k_InvalidRewardDistributionAttemptScriptStatusCode = 2;

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
                    var updatedRewardBalances = await CloudCodeService.Instance.CallEndpointAsync<GrantEventRewardResult>(
                        "SeasonalEvents_GrantEventReward",
                        new Dictionary<string, object>());

                    // Check that scene has not been unloaded while processing async wait to prevent throw.
                    if (this == null) return;

                    Debug.Log($"Collect reward result: {updatedRewardBalances}");

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

            public async Task<long> CallGetServerEpochTimeEndpoint()
            {
                try
                {
                    return await CloudCodeService.Instance.CallEndpointAsync<long>(
                        "SeasonalEvents_GetServerTime", 
                        new Dictionary<string, object>());
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

                return default;
            }

            void HandleCloudCodeException(CloudCodeException e)
            {
                if (e is CloudCodeRateLimitedException cloudCodeRateLimitedException)
                {
                    Debug.Log("Cloud Code rate limit has been exceeded. " +
                              $"Wait {cloudCodeRateLimitedException.RetryAfter} seconds and try again.");
                    return;
                }

                switch (e.ErrorCode)
                {
                    case k_CloudCodeUnprocessableEntityExceptionStatusCode:
                        var cloudCodeCustomError = ConvertToActionableError(e);
                        HandleCloudCodeScriptError(cloudCodeCustomError);
                        break;

                    case k_CloudCodeRateLimitExceptionStatusCode:
                        Debug.Log("Rate Limit Exceeded. Try Again.");
                        break;

                    case k_CloudCodeMissingScriptExceptionStatusCode:
                        Debug.Log("Couldn't find requested Cloud Code Script");
                        break;

                    default:
                        Debug.Log(e);
                        break;
                }
            }

            static CloudCodeCustomError ConvertToActionableError(CloudCodeException e)
            {
                try
                {
                    // extract the JSON part of the exception message
                    var trimmedMessage = e.Message;
                    trimmedMessage = trimmedMessage.Substring(trimmedMessage.IndexOf('{'));
                    trimmedMessage = trimmedMessage.Substring(0, trimmedMessage.LastIndexOf('}') + 1);

                    // Convert the message string ultimately into the Cloud Code Custom Error object which has a
                    // standard structure for all errors.
                    return JsonUtility.FromJson<CloudCodeCustomError>(trimmedMessage);
                }
                catch (Exception exception)
                {
                    return new CloudCodeCustomError("Failed to Parse Error", k_UnexpectedFormatCustomStatusCode,
                        "Cloud Code Unprocessable Entity exception is in an unexpected format and " +
                        $"couldn't be parsed: {exception.Message}", e);
                }
            }

            // This method does whatever handling is appropriate given the specific errors this use case may trigger.
            void HandleCloudCodeScriptError(CloudCodeCustomError cloudCodeCustomError)
            {
                switch (cloudCodeCustomError.status)
                {
                    case k_InvalidRewardDistributionAttemptScriptStatusCode:
                        Debug.Log("No rewards were granted for completing the challenge: " +
                                  $"{cloudCodeCustomError.message}");
                        break;

                    case k_HttpBadRequestStatusCode:
                        Debug.Log("A bad server request occurred during Cloud Code script execution: " + 
                                  $"{cloudCodeCustomError.name}: {cloudCodeCustomError.message} : " +
                                  $"{cloudCodeCustomError.details[0]}");
                        break;

                    case k_HttpTooManyRequestsStatusCode:
                        Debug.Log($"Rate Limit has been exceeded. Wait {cloudCodeCustomError.retryAfter} " +
                                  $"seconds and try again.");
                        break;

                    case k_GenericCloudCodeScriptStatusCode:
                        Debug.Log("A problem occured while trying to grant seasonal rewards: "
                                  + cloudCodeCustomError.message);
                        break;

                    case k_UnexpectedFormatCustomStatusCode:
                        Debug.Log($"Cloud Code returned an Unprocessable Entity exception, " +
                                  $"but it could not be parsed: { cloudCodeCustomError.message }. " +
                                  $"Original error: { cloudCodeCustomError.InnerException?.Message }");
                        break;

                    default:
                        Debug.Log($"Cloud code returned error: {cloudCodeCustomError.status}: " +
                                  $"{cloudCodeCustomError.name}: {cloudCodeCustomError.message}");
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

            public struct GrantEventRewardResult
            {
                public RewardDetail[] grantedRewards;
                public string eventKey;
                public long timestamp;
                public int timestampMinutes;

                public override string ToString()
                {
                    return $"Updated Balances:{string.Join(",", grantedRewards.Select(reward => reward.ToString()).ToArray())}, " +
                        $"Season:{eventKey}, Timestamp:{timestamp} (minutes:{timestampMinutes})";
                }
            }

            struct CloudCodeExceptionParsedMessage
            {
                public string message;
            }

            class CloudCodeCustomError : Exception
            {
                public int status;
                public string name;
                public string message;
                public string retryAfter;
                public string[] details;

                public CloudCodeCustomError(string name, int status, string message = null, 
                    Exception innerException = null) : base(message, innerException)
                {
                    this.name = name;
                    this.status = status;
                    this.message = message;
                    retryAfter = null;
                    details = new string[] { };
                }
            }
        }
    }
}
