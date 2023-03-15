using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode;
using UnityEngine;

namespace Unity.Services.Samples.InGameMailbox
{
    public class CloudCodeManager : MonoBehaviour
    {
        public static CloudCodeManager instance { get; private set; }

        public InGameMailboxSampleView sceneView;

        // Cloud Code SDK status codes from Client
        const int k_CloudCodeRateLimitExceptionStatusCode = 50;
        const int k_CloudCodeMissingScriptExceptionStatusCode = 9002;
        const int k_CloudCodeUnprocessableEntityExceptionStatusCode = 9009;

        // HTTP REST API status codes
        const int k_HttpBadRequestStatusCode = 400;
        const int k_HttpTooManyRequestsStatusCode = 429;

        // Custom status codes
        const int k_UnexpectedFormatCustomStatusCode = int.MinValue;
        const int k_VirtualPurchaseFailedStatusCode = 2;
        const int k_MissingCloudSaveDataStatusCode = 3;
        const int k_InvalidArgumentStatusCode = 4;
        const int k_AttachmentAlreadyClaimedStatusCode = 5;
        const int k_NoClaimableAttachmentsStatusCode = 6;

        // Unity Gaming Services status codes via Cloud Code
        const int k_EconomyValidationExceptionStatusCode = 1007;
        const int k_RateLimitExceptionStatusCode = 50;

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

        public async Task CallClaimMessageAttachmentEndpoint(string messageId)
        {
            try
            {
                sceneView.SetInteractable(false);

                Debug.Log($"Claiming attachment for message {messageId} via Cloud Code...");

                await CloudCodeService.Instance.CallEndpointAsync("InGameMailbox_ClaimAttachment",
                    new Dictionary<string, object> { { "messageId", messageId } });
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
                if (this != null)
                {
                    sceneView.SetInteractable(true);
                }
            }
        }

        public async Task CallClaimAllMessageAttachmentsEndpoint()
        {
            try
            {
                sceneView.SetInteractable(false);

                Debug.Log("Claiming all message attachments via Cloud Code...");

                var result = await CloudCodeService.Instance.CallEndpointAsync<ClaimAllResult>(
                    "InGameMailbox_ClaimAllAttachments", new Dictionary<string, object>());
                if (this == null) return;

                var rewards = GetAggregatedRewardDetails(result.processedTransactions);
                if (rewards.Count > 0)
                {
                    sceneView.ShowClaimAllSucceededPopup(rewards);
                }
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
                if (this != null)
                {
                    sceneView.SetInteractable(true);
                }
            }
        }

        List<RewardDetail> GetAggregatedRewardDetails(string[] processedTransactions)
        {
            var aggregatedRewardCounts = GetAggregatedRewardCounts(processedTransactions);
            return GetRewardDetails(aggregatedRewardCounts);
        }

        Dictionary<string, int> GetAggregatedRewardCounts(string[] processedTransactions)
        {
            var aggregatedRewardCounts = new Dictionary<string, int>();

            if (processedTransactions == null)
            {
                return aggregatedRewardCounts;
            }

            foreach (var transactionId in processedTransactions)
            {
                if (EconomyManager.instance.virtualPurchaseTransactions.TryGetValue(transactionId, out var rewards))
                {
                    foreach (var reward in rewards)
                    {
                        if (aggregatedRewardCounts.ContainsKey(reward.id))
                        {
                            aggregatedRewardCounts[reward.id] += reward.amount;
                        }
                        else
                        {
                            aggregatedRewardCounts.Add(reward.id, reward.amount);
                        }
                    }
                }
            }

            return aggregatedRewardCounts;
        }

        List<RewardDetail> GetRewardDetails(Dictionary<string, int> aggregatedRewardCounts)
        {
            var rewardDetails = new List<RewardDetail>();

            foreach (var rewardCount in aggregatedRewardCounts)
            {
                if (AddressablesManager.instance.preloadedSpritesByEconomyId.TryGetValue(rewardCount.Key, out var sprite))
                {
                    rewardDetails.Add(new RewardDetail
                    {
                        id = rewardCount.Key,
                        quantity = rewardCount.Value,
                        sprite = sprite
                    });
                }
            }

            return rewardDetails;
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
                case k_VirtualPurchaseFailedStatusCode:
                case k_MissingCloudSaveDataStatusCode:
                case k_InvalidArgumentStatusCode:
                    sceneView.ShowClaimFailedPopup("Failed to Claim Attachment", cloudCodeCustomError.message);
                    break;

                case k_AttachmentAlreadyClaimedStatusCode:
                    sceneView.ShowClaimFailedPopup("Attachment Already Claimed", cloudCodeCustomError.message);
                    break;

                case k_NoClaimableAttachmentsStatusCode:
                    sceneView.ShowClaimFailedPopup("No Attachments to Claim", cloudCodeCustomError.message);
                    break;

                case k_EconomyValidationExceptionStatusCode:
                case k_HttpBadRequestStatusCode:
                    Debug.Log("A bad server request occurred during Cloud Code script execution: " +
                        $"{cloudCodeCustomError.name}: {cloudCodeCustomError.message} : " +
                        $"{cloudCodeCustomError.details[0]}");
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

        void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        public struct ClaimAllResult
        {
            public string[] processedTransactions;
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
