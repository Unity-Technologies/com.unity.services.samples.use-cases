using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.Services.Samples.BattlePass
{
    public class BattlePassView : MonoBehaviour
    {
        public BattlePassSceneManager battlePassSceneManager;
        public GameObject tierPrefab;
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

        public void SetInteractable(bool isInteractable = true)
        {
            playGameButton.interactable = isInteractable;
            buyBattlePassButton.interactable = isInteractable;

            foreach (var tierView in m_TierViews)
            {
                tierView.SetInteractable(isInteractable);
            }
        }

        public void Refresh(BattlePassState battlePassState)
        {
            ClearList();

            for (var i = 0; i < battlePassState.tierStates.Length; i++)
            {
                var tierState = battlePassState.tierStates[i];
                var newTierGameObject = Instantiate(tierPrefab, tierListTransform);
                var newTierView = newTierGameObject.GetComponent<TierView>();
                m_TierViews.Add(newTierView);
                newTierView.Refresh(battlePassSceneManager, i, tierState, battlePassState.ownsBattlePass);
            }

            battlePassNotOwnedPanel.SetActive(!battlePassState.ownsBattlePass);
            battlePassOwnedPanel.SetActive(battlePassState.ownsBattlePass);

            RefreshProgressBar(battlePassState);
        }

        void RefreshProgressBar(BattlePassState battlePassState)
        {
            seasonXpProgressBarPanel.SetActive(true);

            if (battlePassState.seasonXP >= BattlePassHelper.MaxEffectiveSeasonXp(battlePassSceneManager.battlePassConfig))
            {
                seasonXpProgressCurrentTierText.text = "MAX";
                seasonXpProgressNextTierText.text = "MAX";
            }
            else
            {
                seasonXpProgressCurrentTierText.text
                    = $"TIER {BattlePassHelper.GetCurrentTierIndex(battlePassState.seasonXP, battlePassSceneManager.battlePassConfig) + 1}";
                seasonXpProgressNextTierText.text
                    = $"TIER {BattlePassHelper.GetNextTierIndex(battlePassState.seasonXP, battlePassSceneManager.battlePassConfig) + 1}";
            }

            seasonXpProgressBarTransform.anchorMax
                = new Vector2(BattlePassHelper.CurrentSeasonProgressFloat(battlePassState.seasonXP, battlePassSceneManager.battlePassConfig), 1f);

            seasonXpProgressBarStatusText.text
                = $"{battlePassSceneManager.battlePassState.seasonXP}" +
                $"/{BattlePassHelper.TotalSeasonXpNeededForNextTier(battlePassState.seasonXP, battlePassSceneManager.battlePassConfig)}";
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
