using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameOperationsSamples
{
    namespace SeasonalEvents
    {
        public class SeasonalEventsSampleView : MonoBehaviour
        {
            public TextMeshProUGUI eventWelcomeText;
            public RewardDisplayView challengeRewardsDisplay;
            public TextMeshProUGUI countdownText;
            public GameObject playChallengeButtonContainter;
            public Button playChallengeButton;

            [Space]
            public Image backgroundImage;
            public GameObject playButtonContainer;
            public Button playButton;

            [Space]
            public RewardPopupView rewardPopupPrefab;


            public void Disable()
            {
                playChallengeButton.interactable = false;
            }

            public void Enable()
            {
                playChallengeButton.interactable = true;
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
                var playTextComponent = newPlayButtonGameObject.GetComponentInChildren<TextMeshProUGUI>();
                playTextComponent.text = "Play";
                playButton = newPlayButtonGameObject.GetComponent<Button>();
                playButton.interactable = false;
            }

            public void UpdatePlayChallengeButton(GameObject playChallengeButtonPrefab)
            {
                ClearContainer(playChallengeButtonContainter.transform);
                var newPlayChallengeButtonGameObject = Instantiate(playChallengeButtonPrefab, playChallengeButtonContainter.transform);
                var playChallengeTextComponent = newPlayChallengeButtonGameObject.GetComponentInChildren<TextMeshProUGUI>();
                playChallengeTextComponent.text = "Play Challenge";
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

            public RewardPopupView InstantiateRewardPopup(List<RewardDetail> rewards)
            {
                var gamePopup = Instantiate(rewardPopupPrefab, transform, false)
                    .GetComponent<RewardPopupView>();
                
                gamePopup.transform.localScale = Vector3.one;

                // Disconnect the reward popup 'close' button so we can call the custom close to permit grant.
                gamePopup.closeButton.onClick = new Button.ButtonClickedEvent();

                gamePopup.Show(rewards);

                return gamePopup;
            }
        }
    }
}
