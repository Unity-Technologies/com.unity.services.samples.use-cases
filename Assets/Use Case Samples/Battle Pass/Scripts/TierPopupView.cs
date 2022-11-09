using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.Services.Samples.BattlePass
{
    public class TierPopupView : MonoBehaviour
    {
        public BattlePassSceneManager battlePassSceneManager;
        public TextMeshProUGUI seasonNameText;
        public TextMeshProUGUI tierNameText;
        public RewardDisplayView normalRewardDisplayView;
        public RewardDisplayView battlePassRewardDisplayView;
        public Button claimButton;
        public GameObject claimedOverlay;
        public GameObject lockedOverlay;
        public GameObject battlePassNotOwnedOverlay;

        int m_TierIndex;

        public void Show(int tierIndex)
        {
            m_TierIndex = tierIndex;

            normalRewardDisplayView.gameObject.SetActive(false);
            battlePassRewardDisplayView.gameObject.SetActive(false);
            claimButton.gameObject.SetActive(false);
            claimedOverlay.SetActive(false);
            lockedOverlay.SetActive(false);

            seasonNameText.text = battlePassSceneManager.battlePassConfig.eventName;
            tierNameText.text = $"Tier {tierIndex + 1}";

            RefreshClaimButtonState();
            RefreshRewardViews();

            gameObject.SetActive(true);
        }

        void RefreshClaimButtonState()
        {
            if (m_TierIndex >= battlePassSceneManager.battlePassState.tierStates.Length)
            {
                throw new IndexOutOfRangeException(
                    $"The given index ({m_TierIndex}) is out of range of the current tier state array length " +
                    $"({battlePassSceneManager.battlePassState.tierStates.Length}).");
            }

            switch (battlePassSceneManager.battlePassState.tierStates[m_TierIndex])
            {
                case TierState.Locked:
                    lockedOverlay.SetActive(true);
                    break;

                case TierState.Unlocked:

                    // don't make it claimable if there's no rewards to claim

                    if (!string.IsNullOrEmpty(battlePassSceneManager.battlePassConfig.rewardsFree[m_TierIndex].id))
                    {
                        claimButton.gameObject.SetActive(true);
                    }
                    else if (string.IsNullOrEmpty(battlePassSceneManager.battlePassConfig.rewardsPremium[m_TierIndex].id))
                    {
                        if (battlePassSceneManager.battlePassState.ownsBattlePass)
                        {
                            claimButton.gameObject.SetActive(true);
                        }
                        else
                        {
                            lockedOverlay.SetActive(true);
                        }
                    }

                    break;

                case TierState.Claimed:
                    claimedOverlay.SetActive(true);
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unknown tier state value ({battlePassSceneManager.battlePassState.tierStates[m_TierIndex]}).");
            }
        }

        void RefreshRewardViews()
        {
            var normalRewardDetail = battlePassSceneManager.battlePassConfig.rewardsFree[m_TierIndex];

            if (!string.IsNullOrEmpty(normalRewardDetail.id))
            {
                normalRewardDisplayView.gameObject.SetActive(true);

                normalRewardDisplayView.PopulateView(new List<RewardDetail> { normalRewardDetail });
            }

            var battlePassRewardDetail = battlePassSceneManager.battlePassConfig.rewardsPremium[m_TierIndex];

            if (!string.IsNullOrEmpty(battlePassRewardDetail.id))
            {
                battlePassRewardDisplayView.gameObject.SetActive(true);

                battlePassRewardDisplayView.PopulateView(new List<RewardDetail> { battlePassRewardDetail });

                battlePassNotOwnedOverlay.SetActive(!battlePassSceneManager.battlePassState.ownsBattlePass);
            }
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        public void OnCloseButtonPressed()
        {
            Close();
        }

        public void OnClaimButtonPressed()
        {
            Close();
            battlePassSceneManager.OnTierPopupClaimButtonClicked(m_TierIndex);
        }
    }
}
