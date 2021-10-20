using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameOperationsSamples
{
    namespace ABTestLevelDifficulty
    {
        public class ABTestLevelDifficultySampleView : MonoBehaviour
        {
            public CurrencyHudView[] currencyHudViews;

            [Space]
            public TextMeshProUGUI playerLevel;
            public Slider progressBar;
            public TextMeshProUGUI playerXPProgressText;

            [Space]
            public Button gainXPButton;
            public TextMeshProUGUI xpUpdateToast;
            public Animator xpUpdateToastAnimator;

            [Space]
            public Button signInAsNewPlayerButton;

            [Space]
            public TextMeshProUGUI abTestGroupText;

            [Space]
            public GameObject levelUpPopup;

            bool m_Enabled;
            bool m_IsSignedIn;

            void OnEnable()
            {
                StartSubscribe();
            }

            void OnDisable()
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

                CloudCodeManager.XPIncreased += ShowXPUpdateToast;
            }

            void StopSubscribe()
            {
                foreach (var currencyHudView in currencyHudViews)
                {
                    EconomyManager.CurrencyBalanceUpdated -= currencyHudView.UpdateBalanceField;
                    CloudCodeManager.CurrencyBalanceUpdated -= currencyHudView.UpdateBalanceField;
                }

                CloudCodeManager.XPIncreased -= ShowXPUpdateToast;
            }

            public void UpdateScene()
            {
                UpdatePlayerABGroup();
                UpdatePlayerLevel();
                UpdateProgressBar();
                UpdateButtons();
            }

            void UpdatePlayerABGroup()
            {
                abTestGroupText.text = $"Group: {RemoteConfigManager.instance.abGroupName}";
            }

            void UpdatePlayerLevel()
            {
                playerLevel.text = CloudSaveManager.instance.playerLevel.ToString();
            }

            void UpdateProgressBar()
            {
                progressBar.value = CloudSaveManager.instance.playerXP;
                progressBar.maxValue = RemoteConfigManager.instance.levelUpXPNeeded;
                playerXPProgressText.text = $"{CloudSaveManager.instance.playerXP}/{RemoteConfigManager.instance.levelUpXPNeeded}";
            }

            void UpdateButtons()
            {
                gainXPButton.interactable = m_Enabled && m_IsSignedIn;
                signInAsNewPlayerButton.interactable = m_Enabled;
            }

            public void OnSignedIn()
            {
                m_IsSignedIn = true;
            }

            public void OnSignedOut()
            {
                m_IsSignedIn = false;
            }

            public void DisableAndUpdate()
            {
                m_Enabled = false;
                UpdateScene();
            }

            public void EnableAndUpdate()
            {
                m_Enabled = true;
                UpdateScene();
            }

            public void ShowXPUpdateToast(int xpIncreaseAmount)
            {
                xpUpdateToast.text = $"+{xpIncreaseAmount} XP";
                xpUpdateToast.gameObject.SetActive(true);
                xpUpdateToastAnimator.SetTrigger("ToastPop");
                Invoke(nameof(HideXPUpdateToast), 1f);
            }

            void HideXPUpdateToast()
            {
                xpUpdateToastAnimator.ResetTrigger("ToastPop");
                xpUpdateToast.gameObject.SetActive(false);
            }

            public void OpenLevelUpPopup()
            {
                levelUpPopup.SetActive(true);
            }

            public void CloseLevelUpPopup()
            {
                levelUpPopup.SetActive(false);
            }
        }
    }
}
