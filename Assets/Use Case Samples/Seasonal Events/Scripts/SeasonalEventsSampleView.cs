using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.Services.Samples.SeasonalEvents
{
    public class SeasonalEventsSampleView : MonoBehaviour
    {
        public TextMeshProUGUI eventWelcomeText;
        public RewardDisplayView challengeRewardsDisplay;
        public TextMeshProUGUI countdownText;
        public GameObject playChallengeButtonContainer;
        public Button playChallengeButton;
        public TextMeshProUGUI playChallengeButtonText;

        [Space]
        public Image backgroundImage;
        public GameObject playButtonContainer;
        public Button playButton;
        public TextMeshProUGUI playButtonText;

        [Space]
        public RewardPopupView rewardPopup;

        internal bool playChallengeAllowed;
        internal bool sceneInitialized;

        public void SetInteractable(bool isInteractable = true)
        {
            UpdateButtonTexts();
            playChallengeButton.interactable = isInteractable && playChallengeAllowed && sceneInitialized;
        }

        void UpdateButtonTexts()
        {
            if (sceneInitialized)
            {
                playButtonText.text = "Play";
                playChallengeButtonText.text = playChallengeAllowed ? "Play Challenge" : "Challenge Won!";
            }
            else
            {
                playButtonText.text = "Initializing";
                playChallengeButtonText.text = "Initializing";
            }
        }

        public void UpdateBackgroundImage(Sprite image)
        {
            if (backgroundImage != null)
            {
                backgroundImage.sprite = image;
            }
        }

        public void UpdatePlayButton(GameObject playButtonPrefab)
        {
            ClearContainer(playButtonContainer.transform);
            var newPlayButtonGameObject = Instantiate(playButtonPrefab, playButtonContainer.transform);
            playButtonText = newPlayButtonGameObject.GetComponentInChildren<TextMeshProUGUI>();
            playButtonText.text = "Play";
            playButton = newPlayButtonGameObject.GetComponent<Button>();
            playButton.interactable = false;
        }

        public void UpdatePlayChallengeButton(GameObject playChallengeButtonPrefab)
        {
            ClearContainer(playChallengeButtonContainer.transform);
            var newPlayChallengeButtonGameObject = Instantiate(playChallengeButtonPrefab, playChallengeButtonContainer.transform);
            playChallengeButtonText = newPlayChallengeButtonGameObject.GetComponentInChildren<TextMeshProUGUI>();
            playChallengeButtonText.text = "Play Challenge";
            playChallengeButton = newPlayChallengeButtonGameObject.GetComponent<Button>();
            playChallengeButton.interactable = false;
        }

        void ClearContainer(Transform buttonContainerTransform)
        {
            for (var i = buttonContainerTransform.childCount - 1; i >= 0; i--)
            {
                Destroy(buttonContainerTransform.GetChild(i).gameObject);
            }
        }

        public void UpdateRewardView()
        {
            var welcomeText = "";

            if (!string.IsNullOrEmpty(RemoteConfigManager.instance.activeEventName))
            {
                welcomeText = $"Welcome to {RemoteConfigManager.instance.activeEventName}";
            }

            eventWelcomeText.text = welcomeText;
            challengeRewardsDisplay.PopulateView(RemoteConfigManager.instance.challengeRewards);
        }

        public void UpdateCountdownText(string counterText)
        {
            countdownText.text = counterText;
        }

        public void ShowRewardPopup(List<RewardDetail> rewards)
        {
            rewardPopup.transform.localScale = Vector3.one;

            rewardPopup.Show(rewards);
        }

        public void CloseRewardPopup()
        {
            rewardPopup.Close();
        }
    }
}
