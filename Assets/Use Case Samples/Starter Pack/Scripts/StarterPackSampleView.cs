using System;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.Services.Samples.StarterPack
{
    public class StarterPackSampleView : MonoBehaviour
    {
        public Button buyStarterPackButton;
        public Button giveTenGemsButton;
        public Button resetPlayerDataButton;
        public GameObject claimedOverlay;
        public MessagePopup messagePopup;

        public static StarterPackSampleView instance { get; private set; }

        bool m_Enabled;

        // using a tri-state boolean so that null can indicate that we don't know yet
        bool? m_StarterPackIsClaimed;

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
            }
            else
            {
                instance = this;
            }

            Refresh();
        }

        void OnEnable()
        {
            StarterPackSceneManager.starterPackStatusChecked += OnStarterPackStatusChecked;
        }

        void OnDisable()
        {
            StarterPackSceneManager.starterPackStatusChecked -= OnStarterPackStatusChecked;
        }

        public void ShowCantAffordStarterPackPopup()
        {
            messagePopup.Show("Not Enough Gems", "You don't have enough gems to be able to afford the Starter Pack!");
        }

        void OnStarterPackStatusChecked(bool starterPackIsClaimed)
        {
            m_StarterPackIsClaimed = starterPackIsClaimed;
            Refresh();
        }

        void OnCurrencyBalanceChanged(string currencyKey, long amount)
        {
            Refresh();
        }

        void Refresh()
        {
            // Set everything to a default disabled state.
            // Then if we have heard from the server,
            // we enable the Buy button or the Claimed panel.

            giveTenGemsButton.interactable = m_Enabled;
            buyStarterPackButton.gameObject.SetActive(true);
            buyStarterPackButton.interactable = false;
            resetPlayerDataButton.interactable = false;
            claimedOverlay.SetActive(false);

            switch (m_StarterPackIsClaimed)
            {
                case null:
                    // we haven't heard back from the server yet - leave it in the default state
                    break;

                case false:
                    claimedOverlay.SetActive(false);
                    buyStarterPackButton.gameObject.SetActive(true);
                    buyStarterPackButton.interactable = m_Enabled;
                    break;

                case true:
                    buyStarterPackButton.gameObject.SetActive(false);
                    resetPlayerDataButton.interactable = m_Enabled;
                    claimedOverlay.SetActive(true);
                    break;
            }
        }

        public void SetInteractable(bool isInteractable = true)
        {
            m_Enabled = isInteractable;
            Refresh();
        }

        void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
