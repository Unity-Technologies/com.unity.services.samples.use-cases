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
            // Cloud Code SDK exceptions.
            const int k_CloudCodeUnprocessableEntityExceptionErrorCode = 9009;
            const int k_CloudCodeRateLimitExceptionErrorCode = 50;
            const int k_CloudCodeMissingScriptExceptionErrorCode = 9002;

            // Cloud Code script errors.
            const int k_UntypedCustomScriptError = 0;
            const int k_GenericCloudCodeScriptError = 1;
            const int k_SpaceOccupiedScriptError = 2;
            const int k_ValidationScriptError = 400;
            const int k_CantAffordScriptError = 422;
            const int k_RateLimitScriptError = 429;

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
            // play in the Cloud Ai Mini Game, it shows a popup in the scene to explain the error.
            void HandleCloudCodeScriptError(CloudCodeCustomError cloudCodeCustomError)
            {
                switch (cloudCodeCustomError.status)
                {
                    case k_SpaceOccupiedScriptError:
                        sceneView.ShowSpaceOccupiedErrorPopup();
                        break;

                    case k_UntypedCustomScriptError:
                        Debug.Log($"Cloud code returned error: {cloudCodeCustomError.status}: " +
                                  $"{cloudCodeCustomError.title}: {cloudCodeCustomError.message}");
                        break;

                    case k_ValidationScriptError:
                        Debug.Log($"{cloudCodeCustomError.title}: {cloudCodeCustomError.message} : " +
                                  $"{cloudCodeCustomError.additionalDetails[0]}");
                        break;

                    case k_CantAffordScriptError:
                        sceneView.ShowVirtualPurchaseFailedErrorPopup();
                        break;

                    case k_RateLimitScriptError:
                        Debug.Log($"Rate Limit has been exceeded. Wait {cloudCodeCustomError.retryAfter} " +
                                  $"seconds and try again.");
                        break;

                    case k_GenericCloudCodeScriptError:
                        Debug.Log("Cloud Code unspecified custom error encountered.");
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
