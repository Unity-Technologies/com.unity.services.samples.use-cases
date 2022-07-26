using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UnityGamingServicesUseCases
{
    namespace RewardedAds
    {
        public class RewardedAdsSampleView : MonoBehaviour
        {
            public Button completeLevelButton;
            
            [Space]
            public GameObject levelCompletePopup;
            public Button claimLevelEndRewardsButton;
            
            [Space]
            public TextMeshProUGUI adsDisabledText;

            [Space]
            public Button watchRewardedAdButton;
            public TextMeshProUGUI standardRewardedAdRewardAmountText;

            [Space]
            public GameObject rewardedAdBooster;
            public TextMeshProUGUI leftWedgeText;
            public TextMeshProUGUI leftCenterWedgeText;
            public TextMeshProUGUI centerWedgeText;
            public TextMeshProUGUI rightCenterWedgeText;
            public TextMeshProUGUI rightWedgeText;
            public Button rewardedAdBoosterWatchAdButton;
            public TextMeshProUGUI rewardedAdBoosterRewardAmountText;

            bool m_IsReadyToCompleteLevel = true;
            bool m_IsSceneInteractable = true;

            public void InitializeScene()
            {
                levelCompletePopup.gameObject.SetActive(false);
                SetInteractable(false);
            }

            public void SetInteractable(bool isInteractable)
            {
                m_IsSceneInteractable = isInteractable;

                rewardedAdBoosterWatchAdButton.interactable = m_IsSceneInteractable && MediationManager.instance.isAdReady;
                watchRewardedAdButton.interactable = m_IsSceneInteractable && MediationManager.instance.isAdReady;
                claimLevelEndRewardsButton.interactable = m_IsSceneInteractable;
                completeLevelButton.interactable = m_IsSceneInteractable && m_IsReadyToCompleteLevel;
            }

            public void SetCompleteLevelButtonInteractable(bool interactable)
            {
                m_IsReadyToCompleteLevel = interactable;
                completeLevelButton.interactable = m_IsSceneInteractable && m_IsReadyToCompleteLevel;
            }

            public void ShowCompleteLevelPopup(int rewardAmount)
            {
                watchRewardedAdButton.gameObject.SetActive(true);
                watchRewardedAdButton.interactable = m_IsSceneInteractable && MediationManager.instance.isAdReady;
                rewardedAdBooster.SetActive(false);
                standardRewardedAdRewardAmountText.text = rewardAmount.ToString();
                adsDisabledText.gameObject.SetActive(!MediationManager.instance.isAdReady);
                levelCompletePopup.gameObject.SetActive(true);
            }

            public void ShowCompleteLevelPopupWithRewardedAdBooster(
                Dictionary<RewardedAdsSceneManager.RewardedAdBoosterWedge, int> rewardedAdBoosterWedgeMultipliers,
                int baseRewardAmount)
            {
                SetUpRewardedAdBoosterView(rewardedAdBoosterWedgeMultipliers, baseRewardAmount);

                // Only show the rewarded ad booster if an ad is ready to show.
                rewardedAdBooster.SetActive(MediationManager.instance.isAdReady);
                rewardedAdBoosterWatchAdButton.interactable = m_IsSceneInteractable && MediationManager.instance.isAdReady;
                adsDisabledText.gameObject.SetActive(false);
                watchRewardedAdButton.gameObject.SetActive(false);
                levelCompletePopup.gameObject.SetActive(true);
            }

            void SetUpRewardedAdBoosterView(
                Dictionary<RewardedAdsSceneManager.RewardedAdBoosterWedge, int> rewardedAdBoosterWedgeMultipliers,
                int baseRewardAmount)
            {
                if (rewardedAdBoosterWedgeMultipliers == null)
                {
                    return;
                }

                string multiplierFormat = "x{0}";
                leftWedgeText.text = string.Format(multiplierFormat,
                    rewardedAdBoosterWedgeMultipliers[RewardedAdsSceneManager.RewardedAdBoosterWedge.Left]);
                leftCenterWedgeText.text = string.Format(multiplierFormat,
                    rewardedAdBoosterWedgeMultipliers[RewardedAdsSceneManager.RewardedAdBoosterWedge.LeftCenter]);
                centerWedgeText.text = string.Format(multiplierFormat,
                    rewardedAdBoosterWedgeMultipliers[RewardedAdsSceneManager.RewardedAdBoosterWedge.Center]);
                rightCenterWedgeText.text = string.Format(multiplierFormat,
                    rewardedAdBoosterWedgeMultipliers[RewardedAdsSceneManager.RewardedAdBoosterWedge.RightCenter]);
                rightWedgeText.text = string.Format(multiplierFormat,
                    rewardedAdBoosterWedgeMultipliers[RewardedAdsSceneManager.RewardedAdBoosterWedge.Right]);
                rewardedAdBoosterRewardAmountText.text = 
                    (baseRewardAmount * rewardedAdBoosterWedgeMultipliers[RewardedAdsSceneManager.RewardedAdBoosterWedge.Left])
                    .ToString();
            }

            public void ChangeRewardedAdBoosterClaimRewardAmount(int newAmount)
            {
                rewardedAdBoosterRewardAmountText.text = newAmount.ToString();
            }

            public void CloseCompleteLevelPopup()
            {
                levelCompletePopup.gameObject.SetActive(false);
            }
        }
    }
}
