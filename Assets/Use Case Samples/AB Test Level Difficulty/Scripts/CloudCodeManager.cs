using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.Services.CloudCode;
using UnityEngine;

namespace UnityGamingServicesUseCases
{
    namespace ABTestLevelDifficulty
    {
        public class CloudCodeManager : MonoBehaviour
        {
            public static CloudCodeManager instance { get; private set; }
            public static event Action<string, long> LeveledUp;
            public static event Action<int> XPIncreased;

            // Cloud Code SDK exceptions.
            const int k_CloudCodeUnprocessableEntityExceptionErrorCode = 9009;
            const int k_CloudCodeRateLimitExceptionErrorCode = 50;
            const int k_CloudCodeMissingScriptExceptionErrorCode = 9002;

            // Cloud Code script errors.
            const int k_UntypedCustomScriptError = 0;
            const int k_GenericCloudCodeScriptError = 1;
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

            public async Task CallGainXPAndLevelIfReadyEndpoint()
            {
                try
                {
                    var gainXPAndLevelResults = await CloudCode.CallEndpointAsync<GainXPAndLevelResult>("ABTest_GainXPAndLevelIfReady", "");

                    // Check that scene has not been unloaded while processing async wait to prevent throw.
                    if (this == null) return;

                    if (gainXPAndLevelResults.playerXPUpdateAmount > 0)
                    {
                        XPIncreased?.Invoke(gainXPAndLevelResults.playerXPUpdateAmount);
                    }

                    if (gainXPAndLevelResults.didLevelUp)
                    {
                        CompleteLevelUpUpdates(gainXPAndLevelResults);
                    }

                    CloudSaveManager.instance.UpdateCachedPlayerXP(gainXPAndLevelResults.playerXP);
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

            void CompleteLevelUpUpdates(GainXPAndLevelResult levelUpResults)
            {
                var rewardCurrencyId = levelUpResults.levelUpRewards.currencyId;
                LeveledUp?.Invoke(rewardCurrencyId, levelUpResults.levelUpRewards.rewardAmount);
                EconomyManager.instance.SetCurrencyBalance(rewardCurrencyId,
                    levelUpResults.levelUpRewards.balance);

                CloudSaveManager.instance.UpdateCachedPlayerLevel(levelUpResults.playerLevel);
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

            // This method does whatever handling is appropriate given the specific error.
            void HandleCloudCodeScriptError(CloudCodeCustomError cloudCodeCustomError)
            {
                switch (cloudCodeCustomError.status)
                {
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
                        Debug.Log("A problem occured while trying to save xp increase: "
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

            public struct LevelUpRewards
            {
                public string currencyId;
                public int rewardAmount;
                public int balance;
            }

            public struct GainXPAndLevelResult
            {
                public int playerLevel;
                public int playerXP;
                public int playerXPUpdateAmount;
                public bool didLevelUp;
                public LevelUpRewards levelUpRewards;
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
