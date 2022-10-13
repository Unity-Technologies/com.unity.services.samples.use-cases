using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode;
using UnityEngine;

namespace UnityGamingServicesUseCases
{
    namespace RewardedAds
    {
        public class CloudCodeManager : MonoBehaviour
        {
            public static CloudCodeManager instance { get; private set; }

            public RewardedAdsSampleView sceneView;
            public RewardedAdsSceneManager sceneManager;

            // Cloud Code SDK status codes from Client
            const int k_CloudCodeRateLimitExceptionStatusCode = 50;
            const int k_CloudCodeMissingScriptExceptionStatusCode = 9002;
            const int k_CloudCodeUnprocessableEntityExceptionStatusCode = 9009;

            // HTTP REST API status codes
            const int k_HttpBadRequestStatusCode = 400;
            const int k_HttpTooManyRequestsStatusCode = 429;

            // Custom status codes
            const int k_UnexpectedFormatCustomStatusCode = int.MinValue;
            const int k_InvalidRewardGrantScriptStatusCode = 2;

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

            public async Task CallGrantLevelEndRewardsEndpoint(bool waitForSecondRewardDistribution, int? multiplier = null)
            {
                try
                {
                    sceneView.SetCompleteLevelButtonInteractable(false);
                    GrantLevelEndRewardsResult result;

                    // Our cloud code script knows that if a multiplier is not passed in, that means that it is
                    // processing a base reward grant, whereas if a multiplier is passed in, that means it is processing
                    // bonus rewards.
                    if (multiplier is null)
                    {
                        Debug.Log("Distributing Level End Base Rewards via Cloud Code...");

                        result = await CloudCodeService.Instance.CallEndpointAsync<GrantLevelEndRewardsResult>(
                            "RewardedAds_GrantLevelEndRewards",
                            new Dictionary<string, object>());
                    }
                    else
                    {
                        Debug.Log("Distributing Level End Booster Rewards via Cloud Code...");

                        result = await CloudCodeService.Instance.CallEndpointAsync<GrantLevelEndRewardsResult>(
                            "RewardedAds_GrantLevelEndRewards",
                            new Dictionary<string, object>{{ "multiplier", (int) multiplier }});
                    }

                    // Check that scene has not been unloaded while processing async wait to prevent throw.
                    if (this == null) return;

                    sceneManager.UpdateEconomyHudWhenAppropriate(waitForSecondRewardDistribution, result.rewardCurrencyId,
                        result.rewardCurrencyBalance);
                }
                catch (CloudCodeException e)
                {
                    HandleCloudCodeException(e);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
                finally
                {
                    sceneView.SetCompleteLevelButtonInteractable(true);
                }
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
                        Debug.Log("Cloud Code exceeded its Rate Limit. Try Again.");
                        break;

                    case k_CloudCodeMissingScriptExceptionStatusCode:
                        Debug.Log("Couldn't find requested Cloud Code Script.");
                        break;

                    default:
                        // Handle other native client errors
                        Debug.Log("Error Code: " + e.ErrorCode);
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

            void HandleCloudCodeScriptError(CloudCodeCustomError cloudCodeCustomError)
            {
                switch (cloudCodeCustomError.status)
                {
                    case k_InvalidRewardGrantScriptStatusCode:
                        Debug.Log("Reward not granted due to an invalid operation: " +
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

            public struct GrantLevelEndRewardsResult
            {
                public string rewardCurrencyId;
                public int rewardCurrencyBalance;
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
