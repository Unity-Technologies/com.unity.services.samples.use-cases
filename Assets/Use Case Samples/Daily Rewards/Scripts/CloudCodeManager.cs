using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using UnityEngine;

namespace UnityGamingServicesUseCases
{
    namespace DailyRewards
    {
        public class CloudCodeManager : MonoBehaviour
        {
            // Cloud Code SDK exceptions.
            const int k_CloudCodeUnprocessableEntityExceptionErrorCode = 9009;
            const int k_CloudCodeRateLimitExceptionErrorCode = 50;
            const int k_CloudCodeMissingScriptExceptionErrorCode = 9002;

            // Cloud Code script errors.
            const int k_UntypedCustomScriptError = 0;
            const int k_ValidationScriptError = 400;
            const int k_RateLimitScriptError = 429;

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

            public async Task CallResetEventEndpoint()
            {
                ThrowIfNotSignedIn();

                try
                {
                    await CloudCode.CallEndpointAsync("DailyRewards_ResetEvent", new object());
                }
                catch (CloudCodeException e)
                {
                    HandleCloudCodeException(e);

                    throw new CloudCodeResultUnavailableException(e,
                        "Handled exception in 'CallResetEventEndpoint.'");
                }
            }

            public async Task<DailyRewardsEventManager.GetStatusResult> CallGetStatusEndpoint()
            {
                ThrowIfNotSignedIn();

                try
                {
                    return await CloudCode.CallEndpointAsync<DailyRewardsEventManager.GetStatusResult>(
                        "DailyRewards_GetStatus", new object());
                }
                catch (CloudCodeException e)
                {
                    HandleCloudCodeException(e);

                    throw new CloudCodeResultUnavailableException(e,
                        "Handled exception in 'CallGetStatusEndpoint.'");
                }
            }

            public async Task<DailyRewardsEventManager.ClaimResult> CallClaimEndpoint()
            {
                ThrowIfNotSignedIn();

                try
                {
                    return await CloudCode.CallEndpointAsync<DailyRewardsEventManager.ClaimResult>(
                        "DailyRewards_Claim", new object());
                }
                catch (CloudCodeException e)
                {
                    HandleCloudCodeException(e);

                    throw new CloudCodeResultUnavailableException(e,
                        "Handled exception in 'CallClaimEndpoint.'");
                }
            }

            void ThrowIfNotSignedIn()
            {
                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    Debug.LogError("Cloud Code can't be called because you're not signed in.");

                    throw new CloudCodeResultUnavailableException(null,
                        "Not signed in to authentication in 'CloudCodeManager.'");
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
                    return new CloudCodeCustomError("Could not parse CloudCodeException.");
                }

                // Convert the message string ultimately into the Cloud Code Custom Error object which has a
                // standard structure for all errors.
                var parsedMessage = JsonUtility.FromJson<CloudCodeExceptionParsedMessage>(trimmedExceptionMessage);
                return JsonUtility.FromJson<CloudCodeCustomError>(parsedMessage.message);
            }

            // This method does whatever handling is appropriate given the specific error. So for example for an invalid
            // play in the Cloud AI Mini Game, it shows a popup in the scene to explain the error.
            void HandleCloudCodeScriptError(CloudCodeCustomError cloudCodeCustomError)
            {
                switch (cloudCodeCustomError.status)
                {
                    case k_UntypedCustomScriptError:
                        Debug.Log($"Cloud code returned error: {cloudCodeCustomError.status}: " +
                                  $"{cloudCodeCustomError.title}: {cloudCodeCustomError.message}");
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
