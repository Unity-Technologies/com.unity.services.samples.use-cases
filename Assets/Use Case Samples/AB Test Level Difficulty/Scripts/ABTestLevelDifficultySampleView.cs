using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameOperationsSamples
{
    namespace ABTestLevelDifficulty
    {
        public class ABTestLevelDifficultySampleView : MonoBehaviour
        {
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
            public RewardPopupView rewardPopupPrefab;

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
                CloudCodeManager.XPIncreased += ShowXPUpdateToast;
            }

            void StopSubscribe()
            {
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
                progressBar.maxValue = RemoteConfigManager.instance.levelUpXPNeeded;
                progressBar.value = CloudSaveManager.instance.playerXP;
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

            public void OpenLevelUpPopup(List<RewardDetail> rewards)
            {
                var gamePopup = Instantiate(rewardPopupPrefab, transform, false)
                    .GetComponent<RewardPopupView>();

                gamePopup.transform.localScale = Vector3.one;

                gamePopup.headerText.text = "<size=45>Congratulations!</size>\n" +
                                            "<size=10>\n</size>" +
                                            "<size=25>You leveled up and received:</size>";

                gamePopup.closeButtonText.text = "Yay!";

                gamePopup.Show(rewards);
            }
        }
    }
}
