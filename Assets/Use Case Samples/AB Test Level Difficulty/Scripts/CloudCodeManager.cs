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

            // Cloud Code SDK status codes from Client
            const int k_CloudCodeRateLimitExceptionStatusCode = 50;
            const int k_CloudCodeMissingScriptExceptionStatusCode = 9002;
            const int k_CloudCodeUnprocessableEntityExceptionStatusCode = 9009;

            // HTTP REST API status codes
            const int k_HttpBadRequestStatusCode = 400;
            const int k_HttpTooManyRequestsStatusCode = 429;

            // Custom status codes
            const int k_UnexpectedFormatCustomStatusCode = int.MinValue;
            const int k_UntypedCustomScriptStatusCode = 0;
            const int k_GenericCloudCodeScriptStatusCode = 1;

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
                    // trim the text that's in front of the valid JSON
                    var trimmedExceptionMessage = Regex.Replace(
                        e.Message, @"^[^\{]*", "", RegexOptions.IgnorePatternWhitespace);

                    // Convert the message string ultimately into the Cloud Code Custom Error object which has a
                    // standard structure for all errors.
                    var parsedMessage = JsonUtility.FromJson<CloudCodeExceptionParsedMessage>(trimmedExceptionMessage);
                    return JsonUtility.FromJson<CloudCodeCustomError>(parsedMessage.message);
                }
                catch (Exception exception)
                {
                    return new CloudCodeCustomError("Failed to Parse Error", k_UnexpectedFormatCustomStatusCode,
                        "Cloud Code Unprocessable Entity exception is in an unexpected format and " +
                        $"couldn't be parsed: {exception.Message}", e);
                }
            }

            // This method does whatever handling is appropriate given the specific error.
            void HandleCloudCodeScriptError(CloudCodeCustomError cloudCodeCustomError)
            {
                switch (cloudCodeCustomError.status)
                {
                    case k_HttpBadRequestStatusCode:
                        Debug.Log("Validation error during Cloud Code script execution: " +
                                  $"{cloudCodeCustomError.name}: {cloudCodeCustomError.message} : " +
                                  $"{cloudCodeCustomError.details[0]}");
                        break;

                    case k_HttpTooManyRequestsStatusCode:
                        Debug.Log($"Rate Limit has been exceeded. Wait {cloudCodeCustomError.retryAfter} " +
                                  "seconds and try again.");
                        break;

                    case k_GenericCloudCodeScriptStatusCode:
                        Debug.Log("A problem occured while trying to save xp increase: "
                                  + cloudCodeCustomError.message);
                        break;

                    case k_UntypedCustomScriptStatusCode:
                        Debug.Log($"Cloud code returned error: {cloudCodeCustomError.status}: " +
                                  $"{cloudCodeCustomError.name}: {cloudCodeCustomError.message}");
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
