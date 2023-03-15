using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.Services.Samples.BattlePass
{
    [RequireComponent(typeof(Image))]
    [RequireComponent(typeof(Button))]
    public class TierView : MonoBehaviour
    {
        public TextMeshProUGUI titleText;
        public FontStyles titleTextFontStyleLocked;
        public Color titleTextColorLocked;
        public FontStyles titleTextFontStyleUnlocked;
        public Color titleTextColorUnlocked;

        public CanvasGroup rewardCanvasGroup;
        public float lockedTierOpacity;

        public GameObject normalRewardPanel;
        public RewardItemView normalRewardItemView;
        public GameObject normalRewardLockIcon;
        public GameObject normalRewardGiftIcon;
        public GameObject normalRewardCheckmarkIcon;

        public GameObject battlePassRewardPanel;
        public RewardItemView battlePassRewardItemView;
        public GameObject battlePassRewardLockIcon;
        public GameObject battlePassRewardGiftIcon;
        public GameObject battlePassRewardCheckmarkIcon;
        public Image battlePassNotOwnedOverlay;

        BattlePassSceneManager m_BattlePassSceneManager;
        Button m_TierButton;
        int m_TierIndex;
        TierState m_TierState;

        void Awake()
        {
            m_TierButton = GetComponent<Button>();

            // ensure a clean startup state

            rewardCanvasGroup.alpha = lockedTierOpacity;

            normalRewardItemView.icon.sprite = null;
            normalRewardItemView.SetQuantity(0);
            normalRewardLockIcon.SetActive(false);
            normalRewardGiftIcon.SetActive(false);
            normalRewardCheckmarkIcon.SetActive(false);

            battlePassRewardItemView.icon.sprite = null;
            battlePassRewardItemView.SetQuantity(0);
            battlePassRewardLockIcon.SetActive(false);
            battlePassRewardGiftIcon.SetActive(false);
            battlePassRewardCheckmarkIcon.SetActive(false);
            battlePassNotOwnedOverlay.gameObject.SetActive(true);
        }

        public void SetInteractable(bool isInteractable = true)
        {
            m_TierButton.interactable = isInteractable;
        }

        public void Refresh(
            BattlePassSceneManager battlePassSceneManager, int tierIndex, TierState tierState, bool battlePassIsOwned)
        {
            m_BattlePassSceneManager = battlePassSceneManager;
            m_TierIndex = tierIndex;
            m_TierState = tierState;

            titleText.text = $"{m_TierIndex + 1}";

            RefreshTierRewards(battlePassIsOwned);
            RefreshTierStateVisuals(battlePassIsOwned);
        }

        void RefreshTierRewards(bool battlePassIsOwned)
        {
            var normalRewardDetail = m_BattlePassSceneManager.battlePassConfig.rewardsFree[m_TierIndex];

            if (!string.IsNullOrEmpty(normalRewardDetail.id))
            {
                normalRewardItemView.SetQuantity(normalRewardDetail.quantity);
                normalRewardItemView.LoadIconFromAddress(normalRewardDetail.spriteAddress);
            }
            else
            {
                normalRewardPanel.SetActive(false);
            }

            var battlePassRewardDetail = m_BattlePassSceneManager.battlePassConfig.rewardsPremium[m_TierIndex];

            if (!string.IsNullOrEmpty(battlePassRewardDetail.id))
            {
                battlePassRewardItemView.SetQuantity(battlePassRewardDetail.quantity);
                battlePassRewardItemView.LoadIconFromAddress(battlePassRewardDetail.spriteAddress);
            }
            else
            {
                battlePassRewardPanel.SetActive(false);
            }

            battlePassNotOwnedOverlay.gameObject.SetActive(!battlePassIsOwned);
        }

        void RefreshTierStateVisuals(bool battlePassIsOwned)
        {
            rewardCanvasGroup.alpha = lockedTierOpacity;
            titleText.fontStyle = titleTextFontStyleLocked;
            titleText.color = titleTextColorLocked;
            normalRewardLockIcon.SetActive(false);
            normalRewardGiftIcon.SetActive(false);
            normalRewardCheckmarkIcon.SetActive(false);
            battlePassRewardLockIcon.SetActive(false);
            battlePassRewardGiftIcon.SetActive(false);
            battlePassRewardCheckmarkIcon.SetActive(false);

            switch (m_TierState)
            {
                case TierState.Locked:
                    normalRewardLockIcon.SetActive(true);
                    battlePassRewardLockIcon.SetActive(true);
                    break;

                case TierState.Unlocked:
                    rewardCanvasGroup.alpha = 1.0f;
                    titleText.fontStyle = titleTextFontStyleUnlocked;
                    titleText.color = titleTextColorUnlocked;
                    normalRewardGiftIcon.SetActive(true);
                    battlePassRewardGiftIcon.SetActive(battlePassIsOwned);
                    battlePassRewardLockIcon.SetActive(!battlePassIsOwned);
                    break;

                case TierState.Claimed:
                    rewardCanvasGroup.alpha = 1.0f;
                    titleText.fontStyle = titleTextFontStyleUnlocked;
                    titleText.color = titleTextColorUnlocked;
                    normalRewardCheckmarkIcon.SetActive(true);
                    battlePassRewardCheckmarkIcon.SetActive(battlePassIsOwned);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void OnTierButtonClicked()
        {
            m_BattlePassSceneManager.OnTierButtonClicked(m_TierIndex);
        }
    }
}
