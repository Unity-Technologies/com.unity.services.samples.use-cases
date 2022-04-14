using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.Services.CloudCode;
using UnityEngine;

namespace UnityGamingServicesUseCases
{
    namespace IdleClickerGame
    {
        public class CloudCodeManager : MonoBehaviour
        {
            // Cloud Code SDK status codes from Client
            const int k_CloudCodeRateLimitExceptionStatusCode = 50;
            const int k_CloudCodeMissingScriptExceptionStatusCode = 9002;
            const int k_CloudCodeUnprocessableEntityExceptionStatusCode = 9009;

            // HTTP REST API status codes
            const int k_HttpBadRequestStatusCode = 400;
            const int k_HttpTooManyRequestsStatusCode = 429;

            // Custom status codes
            const int k_UnexpectedFormatCustomStatusCode = int.MinValue;
            const int k_SpaceOccupiedScriptStatusCode = 2;
            const int k_VirtualPurchaseFailedStatusCode = 3;

            // Unity Gaming Services status codes via Cloud Code
            const int k_EconomyPurchaseCostsNotMetStatusCode = 10504;
            const int k_EconomyValidationExceptionStatusCode = 1007;
            const int k_RateLimitExceptionStatusCode = 50;

            public IdleClickerGameSampleView sceneView;

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

            public async Task<UpdatedState> CallGetUpdatedStateEndpoint()
            {
                try
                {
                    var updatedState = await CloudCode.CallEndpointAsync<UpdatedState>(
                        "IdleClicker_GetUpdatedState", new object());

                    return updatedState;
                }
                catch (CloudCodeException e)
                {
                    HandleCloudCodeException(e);
                    throw new CloudCodeResultUnavailableException(e,
                        "Handled exception in CallGetUpdatedStateEndpoint.");
                }
            }

            public async Task<PlacePieceResult> CallPlaceWellEndpoint(Vector2 coord)
            {
                try
                {
                    var placePieceResult = await CloudCode.CallEndpointAsync<PlacePieceResult>(
                        "IdleClicker_PlaceWell", new CoordParam { coord = { x = (int)coord.x, y = (int)coord.y } });

                    return placePieceResult;
                }
                catch (CloudCodeException e)
                {
                    HandleCloudCodeException(e);
                    throw new CloudCodeResultUnavailableException(e,
                        "Handled exception in CallPlaceWellEndpoint.");
                }
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

            // This method does whatever handling is appropriate given the specific error. So for example for an invalid
            // play in the Cloud Ai Mini Game, it shows a popup in the scene to explain the error.
            void HandleCloudCodeScriptError(CloudCodeCustomError cloudCodeCustomError)
            {
                switch (cloudCodeCustomError.status)
                {
                    case k_SpaceOccupiedScriptStatusCode:
                        sceneView.ShowSpaceOccupiedErrorPopup();
                        break;

                    case k_EconomyValidationExceptionStatusCode:
                    case k_HttpBadRequestStatusCode:
                        Debug.Log("A bad server request occurred during Cloud Code script execution: " + 
                                  $"{cloudCodeCustomError.name}: {cloudCodeCustomError.message} : " +
                                  $"{cloudCodeCustomError.details[0]}");
                        break;

                    case k_VirtualPurchaseFailedStatusCode:
                        Debug.Log($"The purchase could not be completed: {cloudCodeCustomError.name}: " +
                                  $"{cloudCodeCustomError.message}");
                        break;

                    case k_EconomyPurchaseCostsNotMetStatusCode:
                        sceneView.ShowVirtualPurchaseFailedErrorPopup();
                        break;

                    case k_RateLimitExceptionStatusCode:
                        // With this status code, message will include which service triggered this rate limit.
                        Debug.Log($"{cloudCodeCustomError.message}. Wait {cloudCodeCustomError.retryAfter} " +
                                  $"seconds and try again.");
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
