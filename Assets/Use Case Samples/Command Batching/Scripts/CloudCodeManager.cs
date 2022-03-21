using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.Services.CloudCode;
using UnityEngine;

namespace UnityGamingServicesUseCases
{
    namespace CommandBatching
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
            const int k_InvalidArgumentScriptError = 2;
            const int k_InvalidGameplayScriptError = 3;
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

            public async Task CallProcessBatchEndpoint(string[] commands)
            {
                if (commands is null || commands.Length <= 0)
                {
                    return;
                }

                try
                {
                    Debug.Log("Processing command batch via Cloud Code...");

                    // Cloud Code API will convert ProcessBatchRequest into a JSON structure like
                    // { batch: { "commands": ["COMMANDBATCH_DEFEAT_RED_ENEMY", "COMMANDBATCH_OPEN_CHEST", etc] }}
                    await CloudCode.CallEndpointAsync("CommandBatch_ProcessBatch",
                        new ProcessBatchRequest(commands));

                    Debug.Log("Cloud Code successfully processed batch.");
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
                    case k_InvalidArgumentScriptError:
                        Debug.Log("Cloud Code could not process batch because it was missing or " +
                                  "misconfigured: " + cloudCodeCustomError.message);
                        break;

                    case k_InvalidGameplayScriptError:
                        Debug.Log("Cloud Code could not process the batch because of invalid gameplay: "
                                  + cloudCodeCustomError.message);
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
                        Debug.Log("A problem occured while trying to process batch: "
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

            struct Batch
            {
                public string[] commands;

                public Batch(string[] commands)
                {
                    this.commands = commands;
                }
            }

            struct ProcessBatchRequest
            {
                public Batch batch;

                public ProcessBatchRequest(string[] commands)
                {
                    batch = new Batch(commands);
                }
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
