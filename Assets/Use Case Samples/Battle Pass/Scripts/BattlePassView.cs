using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UnityGamingServicesUseCases
{
    namespace BattlePass
    {
        public class BattlePassView : MonoBehaviour
        {
            public BattlePassSceneManager battlePassSceneManager;
            public GameObject tierPrefab;
            public TierPopupView tierPopupView;
            public GameObject seasonXpProgressBarPanel;
            public TextMeshProUGUI seasonXpProgressCurrentTierText;
            public TextMeshProUGUI seasonXpProgressNextTierText;
            public TextMeshProUGUI seasonXpProgressBarStatusText;
            public RectTransform seasonXpProgressBarTransform;
            public Transform tierListTransform;
            public GameObject battlePassNotOwnedPanel;
            public GameObject battlePassOwnedPanel;
            public Button buyBattlePassButton;
            public Button playGameButton;

            readonly List<TierView> m_TierViews = new List<TierView>();

            void Awake()
            {
                playGameButton.interactable = false;
                battlePassNotOwnedPanel.SetActive(false);
                battlePassOwnedPanel.SetActive(false);
                seasonXpProgressBarPanel.SetActive(false);

                ClearList();
            }

            public void SetInteractable(bool interactable)
            {
                playGameButton.interactable = interactable;
                buyBattlePassButton.interactable = interactable;

                foreach (var tierView in m_TierViews)
                {
                    tierView.SetInteractable(interactable);
                }
            }

            public void Refresh(BattlePassProgress battlePassProgress)
            {
                ClearList();

                for (var i = 0; i < battlePassProgress.tierStates.Length; i++)
                {
                    var tierState = battlePassProgress.tierStates[i];
                    var newTierGameObject = Instantiate(tierPrefab, tierListTransform);
                    var newTierView = newTierGameObject.GetComponent<TierView>();
                    m_TierViews.Add(newTierView);
                    newTierView.Refresh(battlePassSceneManager, i, tierState, battlePassProgress.ownsBattlePass);
                }

                battlePassNotOwnedPanel.SetActive(!battlePassProgress.ownsBattlePass);
                battlePassOwnedPanel.SetActive(battlePassProgress.ownsBattlePass);

                RefreshProgressBar(battlePassProgress);
            }

            void RefreshProgressBar(BattlePassProgress battlePassProgress)
            {
                seasonXpProgressBarPanel.SetActive(true);

                if (battlePassProgress.seasonXP >= BattlePassHelper.MaxEffectiveSeasonXp())
                {
                    seasonXpProgressCurrentTierText.text = "MAX";
                    seasonXpProgressNextTierText.text = "MAX";
                }
                else
                {
                    seasonXpProgressCurrentTierText.text
                        = $"TIER {BattlePassHelper.GetCurrentTierIndex(battlePassProgress.seasonXP) + 1}";
                    seasonXpProgressNextTierText.text
                        = $"TIER {BattlePassHelper.GetNextTierIndex(battlePassProgress.seasonXP) + 1}";
                }

                seasonXpProgressBarTransform.anchorMax
                    = new Vector2(BattlePassHelper.CurrentSeasonProgressFloat(battlePassProgress.seasonXP), 1f);

                seasonXpProgressBarStatusText.text
                    = $"{battlePassSceneManager.battlePassProgress.seasonXP}" +
                      $"/{BattlePassHelper.TotalSeasonXpNeededForNextTier(battlePassProgress.seasonXP)}";
            }

            void ClearList()
            {
                m_TierViews.Clear();

                while (tierListTransform.childCount > 0)
                {
                    var tierTransform = tierListTransform.GetChild(0);
                    tierTransform.SetParent(null);
                    Destroy(tierTransform.gameObject);
                }
            }
        }
    }
}
