using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode;
using UnityEngine;

namespace Unity.Services.Samples.IdleClickerGame
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
        const int k_CloudSaveStateMissingCode = 2;
        const int k_SpaceOccupiedScriptStatusCode = 3;
        const int k_VirtualPurchaseFailedStatusCode = 4;
        const int k_WellNotFoundCode = 5;
        const int k_InvalidDragCode = 6;
        const int k_WellsDifferentLevelCode = 7;
        const int k_MaxLevelCode = 8;
        const int k_InvalidLocationCode = 9;
        const int k_WellLevelLockedCode = 10;

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

        public async Task<IdleClickerResult> CallGetUpdatedStateEndpoint()
        {
            try
            {
                var updatedState = await CloudCodeService.Instance.CallEndpointAsync<IdleClickerResult>(
                    "IdleClicker_GetUpdatedState",
                    new Dictionary<string, object>());

                return updatedState;
            }
            catch (CloudCodeException e)
            {
                HandleCloudCodeException(e);
                throw new CloudCodeResultUnavailableException(e,
                    "Handled exception in CallGetUpdatedStateEndpoint.");
            }
        }

        public async Task<IdleClickerResult> CallPlaceWellEndpoint(Vector2 coord)
        {
            try
            {
                var updatedState = await CloudCodeService.Instance.CallEndpointAsync<IdleClickerResult>(
                    "IdleClicker_PlaceWell",
                    new Dictionary<string, object>
                    {
                        { "coord", new Coord { x = (int)coord.x, y = (int)coord.y } }
                    });

                return updatedState;
            }
            catch (CloudCodeException e)
            {
                HandleCloudCodeException(e);
                throw new CloudCodeResultUnavailableException(e,
                    "Handled exception in CallPlaceWellEndpoint.");
            }
        }

        public async Task<IdleClickerResult> CallMergeWellsEndpoint(Vector2 drag, Vector2 drop)
        {
            try
            {
                var updatedState = await CloudCodeService.Instance.CallEndpointAsync<IdleClickerResult>(
                    "IdleClicker_MergeWells",
                    new Dictionary<string, object>
                    {
                        { "drag", new Coord { x = (int)drag.x, y = (int)drag.y } },
                        { "drop", new Coord { x = (int)drop.x, y = (int)drop.y } }
                    });

                return updatedState;
            }
            catch (CloudCodeException e)
            {
                HandleCloudCodeException(e);
                throw new CloudCodeResultUnavailableException(e,
                    "Handled exception in CallMergeWellsEndpoint.");
            }
        }

        public async Task<IdleClickerResult> CallMoveWellEndpoint(Vector2 drag, Vector2 drop)
        {
            try
            {
                var updatedState = await CloudCodeService.Instance.CallEndpointAsync<IdleClickerResult>(
                    "IdleClicker_MoveWell",
                    new Dictionary<string, object>
                    {
                        { "drag", new Coord { x = (int)drag.x, y = (int)drag.y } },
                        { "drop", new Coord { x = (int)drop.x, y = (int)drop.y } }
                    });

                return updatedState;
            }
            catch (CloudCodeException e)
            {
                HandleCloudCodeException(e);
                throw new CloudCodeResultUnavailableException(e,
                    "Handled exception in CallMoveWellEndpoint.");
            }
        }

        public async Task CallResetEndpoint()
        {
            try
            {
                await CloudCodeService.Instance.CallEndpointAsync(
                    "IdleClicker_Reset", new Dictionary<string, object>());
            }
            catch (CloudCodeException e)
            {
                HandleCloudCodeException(e);
                throw new CloudCodeResultUnavailableException(e,
                    "Handled exception in CallMoveWellEndpoint.");
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

        // This method does whatever handling is appropriate given the specific error. So for example for an invalid
        // play in the Cloud Ai Mini Game, it shows a popup in the scene to explain the error.
        void HandleCloudCodeScriptError(CloudCodeCustomError cloudCodeCustomError)
        {
            switch (cloudCodeCustomError.status)
            {
                case k_CloudSaveStateMissingCode:
                    sceneView.ShowCloudSaveMissingPopup();
                    break;

                case k_SpaceOccupiedScriptStatusCode:
                    sceneView.ShowSpaceOccupiedErrorPopup();
                    break;

                case k_VirtualPurchaseFailedStatusCode:
                    sceneView.ShowVirtualPurchaseFailedErrorPopup();
                    break;

                case k_WellNotFoundCode:
                    sceneView.ShowWellNotFoundPopup();
                    break;

                case k_InvalidDragCode:
                    sceneView.ShowInvalidDragPopup();
                    break;

                case k_WellsDifferentLevelCode:
                    sceneView.ShowWellsDifferentLevelPopup();
                    break;

                case k_MaxLevelCode:
                    sceneView.ShowMaxLevelPopup();
                    break;

                case k_InvalidLocationCode:
                    sceneView.ShowInvalidLocationPopup();
                    break;

                case k_WellLevelLockedCode:
                    sceneView.ShowWellLockedPopup();
                    break;

                case k_EconomyValidationExceptionStatusCode:
                case k_HttpBadRequestStatusCode:
                    Debug.Log("A bad server request occurred during Cloud Code script execution: " +
                        $"{cloudCodeCustomError.name}: {cloudCodeCustomError.message} : " +
                        $"{cloudCodeCustomError.details[0]}");
                    break;

                case k_EconomyPurchaseCostsNotMetStatusCode:
                    sceneView.ShowVirtualPurchaseFailedErrorPopup();
                    break;

                case k_RateLimitExceptionStatusCode:
                    // With this status code, message will include which service triggered this rate limit.
                    Debug.Log($"{cloudCodeCustomError.message}. Wait {cloudCodeCustomError.retryAfter} " +
                        "seconds and try again.");
                    break;

                case k_HttpTooManyRequestsStatusCode:
                    Debug.Log($"Rate Limit has been exceeded. Wait {cloudCodeCustomError.retryAfter} " +
                        "seconds and try again.");
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
