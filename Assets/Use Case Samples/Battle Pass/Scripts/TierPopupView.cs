using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UnityGamingServicesUseCases
{
    namespace BattlePass
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

                seasonNameText.text = RemoteConfigManager.instance.activeEventName;
                tierNameText.text = $"Tier {tierIndex + 1}";

                RefreshClaimButtonState();
                RefreshRewardViews();

                gameObject.SetActive(true);
            }

            void RefreshClaimButtonState()
            {
                var normalRewardDetail = RemoteConfigManager.instance.normalRewards[m_TierIndex];
                var battlePassRewardDetail = RemoteConfigManager.instance.normalRewards[m_TierIndex];

                if (m_TierIndex >= battlePassSceneManager.battlePassProgress.tierStates.Length)
                {
                    throw new IndexOutOfRangeException(
                        $"The given index ({m_TierIndex}) is out of range of the current tier state array length " +
                        $"({battlePassSceneManager.battlePassProgress.tierStates.Length}).");
                }

                switch (battlePassSceneManager.battlePassProgress.tierStates[m_TierIndex])
                {
                    case TierState.Locked:
                        lockedOverlay.SetActive(true);
                        break;

                    case TierState.Unlocked:

                        // don't make it claimable if there's no rewards to claim

                        if (!string.IsNullOrEmpty(normalRewardDetail.id))
                        {
                            claimButton.gameObject.SetActive(true);
                        }
                        else if (string.IsNullOrEmpty(battlePassRewardDetail.id))
                        {
                            if (battlePassSceneManager.battlePassProgress.ownsBattlePass)
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
                            $"Unknown tier state value ({battlePassSceneManager.battlePassProgress.tierStates[m_TierIndex]}).");
                }
            }

            void RefreshRewardViews()
            {
                if (!string.IsNullOrEmpty(RemoteConfigManager.instance.normalRewards[m_TierIndex].id))
                {
                    normalRewardDisplayView.gameObject.SetActive(true);

                    normalRewardDisplayView.PopulateView(
                        new List<RewardDetail> { RemoteConfigManager.instance.normalRewards[m_TierIndex] });
                }

                if (!string.IsNullOrEmpty(RemoteConfigManager.instance.battlePassRewards[m_TierIndex].id))
                {
                    battlePassRewardDisplayView.gameObject.SetActive(true);

                    battlePassRewardDisplayView.PopulateView(
                        new List<RewardDetail> { RemoteConfigManager.instance.battlePassRewards[m_TierIndex] });

                    battlePassNotOwnedOverlay.SetActive(!battlePassSceneManager.battlePassProgress.ownsBattlePass);
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
}
