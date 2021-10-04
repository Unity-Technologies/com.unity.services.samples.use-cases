using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SeasonalEvents
{
    public class SceneView : MonoBehaviour
    {
        public GameOperationsSamples.CurrencyHudView[] currencyHudViews;
        public Image backgroundImage;
        public TextMeshProUGUI eventWelcomeText;
        public RewardDisplayView challengeRewardsDisplay;
        public TextMeshProUGUI countdownText;
        public Button challengePlayButton;
        public RewardPopupManager rewardPopupPrefab;

        Canvas m_Canvas;

        void Awake()
        {
            m_Canvas = FindObjectOfType<Canvas>();
        }

        void OnEnable()
        {
            StartSubscribe();
        }

        private void OnDisable()
        {
            StopSubscribe();
        }

        void StartSubscribe()
        {
            foreach (var currencyHudView in currencyHudViews)
            {
                EconomyManager.CurrencyBalanceUpdated += currencyHudView.UpdateBalanceField;
                CloudCodeManager.CurrencyBalanceUpdated += currencyHudView.UpdateBalanceField;
            }

            RemoteConfigManager.RemoteConfigValuesUpdated += UpdateRewardView;
        }

        void StopSubscribe()
        {
            foreach (var currencyHudView in currencyHudViews)
            {
                EconomyManager.CurrencyBalanceUpdated -= currencyHudView.UpdateBalanceField;
                CloudCodeManager.CurrencyBalanceUpdated -= currencyHudView.UpdateBalanceField;
            }

            RemoteConfigManager.RemoteConfigValuesUpdated -= UpdateRewardView;
        }

        public void Disable()
        {
            challengePlayButton.interactable = false;
        }

        public void Enable()
        {
            challengePlayButton.interactable = true;
        }

        public void UpdateBackgroundImage(Sprite image)
        {
            if (backgroundImage != null)
            {
                backgroundImage.sprite = image;
            }
        }

        void UpdateRewardView()
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

        public RewardPopupManager InstantiateRewardPopup()
        {
            var gamePopup = Instantiate(rewardPopupPrefab, m_Canvas.transform, false)
                .GetComponent<RewardPopupManager>();
            gamePopup.transform.localScale = Vector3.one;
            return gamePopup;
        }
    }
}
