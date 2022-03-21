using System;
using System.Text.RegularExpressions;
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

            // Cloud Code SDK exceptions.
            const int k_CloudCodeUnprocessableEntityExceptionErrorCode = 9009;
            const int k_CloudCodeRateLimitExceptionErrorCode = 50;
            const int k_CloudCodeMissingScriptExceptionErrorCode = 9002;

            // Cloud Code script errors.
            const int k_InvalidRewardGrantScriptError = 2;
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

            public async Task CallGrantLevelEndRewardsEndpoint(int? multiplier = null)
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

                        result = await CloudCode.CallEndpointAsync<GrantLevelEndRewardsResult>
                            ("RewardedAds_GrantLevelEndRewards", "");
                    }
                    else
                    {
                        Debug.Log("Distributing Level End Booster Rewards via Cloud Code...");

                        result = await CloudCode.CallEndpointAsync<GrantLevelEndRewardsResult>
                            ("RewardedAds_GrantLevelEndRewards", new GrantLevelEndRewardsRequest((int) multiplier));
                    }

                    // Check that scene has not been unloaded while processing async wait to prevent throw.
                    if (this == null) return;

                    EconomyManager.instance.SetCurrencyBalance(result.rewardCurrencyId, result.rewardCurrencyBalance);
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
                switch (e.ErrorCode)
                {
                    case k_CloudCodeUnprocessableEntityExceptionErrorCode:
                        var cloudCodeCustomError = ConvertToActionableError(e);
                        HandleCloudCodeScriptError(cloudCodeCustomError);
                        break;

                    case k_CloudCodeRateLimitExceptionErrorCode:
                        Debug.Log("Cloud Code exceeded its Rate Limit. Try Again.");
                        break;

                    case k_CloudCodeMissingScriptExceptionErrorCode:
                        Debug.Log("Couldn't find requested Cloud Code Script.");
                        break;

                    default:
                        // Handle other native client errors
                        Debug.Log("Error Code: " + e.ErrorCode);
                        Debug.Log(e);
                        break;
                }
            }

            CloudCodeCustomError ConvertToActionableError(CloudCodeException e)
            {
                // get rid of the text that's in front of the valid JSON
                var trimmedExceptionMessage = Regex.Replace(
                    e.Message, @"^[^\{]*", "", RegexOptions.IgnorePatternWhitespace);

                if (string.IsNullOrEmpty(trimmedExceptionMessage))
                {
                    return new CloudCodeCustomError("Could not parse CloudCodeException.");
                }

                // Convert the message string ultimately into the Cloud Code Custom Error object we've defined
                // which has a standard structure for all errors.
                var parsedMessage = JsonUtility.FromJson<CloudCodeExceptionParsedMessage>(trimmedExceptionMessage);
                return JsonUtility.FromJson<CloudCodeCustomError>(parsedMessage.message);
            }

            void HandleCloudCodeScriptError(CloudCodeCustomError cloudCodeCustomError)
            {
                switch (cloudCodeCustomError.status)
                {
                    case k_InvalidRewardGrantScriptError:
                        Debug.Log("Reward not granted due to an invalid operation: " +
                                  $"{cloudCodeCustomError.message}");
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

            void OnDestroy()
            {
                if (instance == this)
                {
                    instance = null;
                }
            }

            struct GrantLevelEndRewardsRequest
            {
                public int multiplier;

                public GrantLevelEndRewardsRequest(int multiplier)
                {
                    this.multiplier = multiplier;
                }
            }

            struct GrantLevelEndRewardsResult
            {
                public string rewardCurrencyId;
                public int rewardCurrencyBalance;
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
