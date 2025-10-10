using UnityEngine;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;
namespace GemHunterUGS.Scripts.Ads
{
    public class AdRewardUIController : MonoBehaviour
    {
        private AdRewardView m_AdRewardView;
        private AdRewardManagerClient m_AdRewardManagerClient;
        [SerializeField]
        private string m_PlacementName = "Main_Menu";
        
        private void Start()
        {
            m_AdRewardView = GetComponent<AdRewardView>();
            m_AdRewardView.Initialize();
            
            m_AdRewardManagerClient = GetComponent<AdRewardManagerClient>();
            if (m_AdRewardManagerClient != null)
            {
                m_AdRewardManagerClient.AdSuccessfullyCompleted += HandleAdCompleted;
                m_AdRewardManagerClient.AdAvailable += HandleAdAvailabilityChanged;
            }
            m_AdRewardView.AdRewardButton.clicked += HandleClickAdReward;
            UpdateButtonState();
        }

        private void HandleClickAdReward()
        {
            // Disable button immediately to prevent multiple clicks
            m_AdRewardView.AdRewardButton.SetEnabled(false);
            
            // Let the manager handle all the checks
            m_AdRewardManagerClient.ClickShowAdReward(m_PlacementName);
            Logger.LogDemo($"Requested ad for placement: {m_PlacementName}");
        }

        private void HandleAdCompleted(bool success)
        {
            UpdateButtonState();
            if (success)
            {
                Logger.LogDemo($"Ad completed successfully for placement: {m_PlacementName}");
                // You could show a success message to the user here
            }
            else
            {
                Logger.LogWarning($"Ad failed to complete for placement: {m_PlacementName}");
                // You could show a failure message to the user here
            }
        }

        private void HandleAdAvailabilityChanged(bool isAvailable)
        {
            UpdateButtonState();
            Logger.LogDemo($"Ad availability changed: {(isAvailable ? "available" : "unavailable")}");
        }

        private void UpdateButtonState()
        {
            if (m_AdRewardManagerClient == null)
            {
                m_AdRewardView.AdRewardButton.SetEnabled(false);
                Logger.LogWarning("Ad manager not available");
                return;
            }

            // Check availability for this specific placement
            bool isAvailable = m_AdRewardManagerClient.CanShowAd(m_PlacementName);
            m_AdRewardView.AdRewardButton.SetEnabled(isAvailable);
            
            Logger.LogVerbose($"Button state updated for placement '{m_PlacementName}': {(isAvailable ? "enabled" : "disabled")}");
            
            // Show cooldown info if relevant
            float cooldownRemaining = m_AdRewardManagerClient.GetRemainingCooldownSeconds();
            if (cooldownRemaining > 0)
            {
                Logger.LogVerbose($"Ad on cooldown for {cooldownRemaining:F1} seconds");
            }
        }

        private void OnDisable()
        {
            if (m_AdRewardManagerClient != null)
            {
                m_AdRewardManagerClient.AdSuccessfullyCompleted -= HandleAdCompleted;
                m_AdRewardManagerClient.AdAvailable -= HandleAdAvailabilityChanged;
            }
            
            if (m_AdRewardView?.AdRewardButton != null)
            {
                m_AdRewardView.AdRewardButton.clicked -= HandleClickAdReward;
            }
        }
    }
}
