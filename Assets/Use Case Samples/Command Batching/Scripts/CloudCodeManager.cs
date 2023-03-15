using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode;
using UnityEngine;

namespace Unity.Services.Samples.CommandBatching
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
        const int k_InvalidArgumentScriptStatusCode = 2;
        const int k_InvalidGameplayScriptStatusCode = 3;

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

                await CloudCodeService.Instance.CallEndpointAsync(
                    "CommandBatching_ProcessBatch",
                    new Dictionary<string, object> { { "commands", commands } });

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
                case k_InvalidArgumentScriptStatusCode:
                    Debug.Log("Cloud Code could not process batch because it was missing or " +
                        "misconfigured: " + cloudCodeCustomError.message);
                    break;

                case k_InvalidGameplayScriptStatusCode:
                    Debug.Log("Cloud Code could not process the batch because of invalid gameplay: "
                        + cloudCodeCustomError.message);
                    break;

                case k_HttpBadRequestStatusCode:
                    Debug.Log("A bad server request occurred during Cloud Code script execution: " +
                        $"{cloudCodeCustomError.name}: {cloudCodeCustomError.message} : " +
                        $"{cloudCodeCustomError.details[0]}");
                    break;

                case k_HttpTooManyRequestsStatusCode:
                    Debug.Log($"Rate Limit has been exceeded. Wait {cloudCodeCustomError.retryAfter} " +
                        "seconds and try again.");
                    break;

                case k_GenericCloudCodeScriptStatusCode:
                    Debug.Log("A problem occured while trying to process batch: "
                        + cloudCodeCustomError.message);
                    break;

                case k_UnexpectedFormatCustomStatusCode:
                    Debug.Log("Cloud Code returned an Unprocessable Entity exception, " +
                        $"but it could not be parsed: {cloudCodeCustomError.message}. " +
                        $"Original error: {cloudCodeCustomError.InnerException?.Message}");
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

        class CloudCodeCustomError : Exception
        {
            public int status;
            public string name;
            public string message;
            public string retryAfter;
            public string[] details;

            public CloudCodeCustomError(string name, int status, string message = null,
                Exception innerException = null)
                : base(message, innerException)
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
