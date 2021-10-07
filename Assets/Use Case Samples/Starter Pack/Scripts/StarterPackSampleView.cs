using UnityEngine;
using UnityEngine.UI;

namespace GameOperationsSamples
{
    namespace StarterPack
    {
        public class StarterPackSampleView : MonoBehaviour
        {
            public Button buyStarterPackButton;
            public Button giveTenGemsButton;
            public Button resetPlayerDataButton;
            public GameObject claimedOverlay;

            public static StarterPackSampleView instance => m_Instance;
            static StarterPackSampleView m_Instance;

            bool m_Enabled;

            // using a tri-state boolean so that null can indicate that we don't know yet
            bool? m_StarterPackIsClaimed = null;

            private void Awake()
            {
                m_Instance = this;
                Refresh();
            }

            private void OnEnable()
            {
                StarterPackSample.StarterPackStatusChecked += OnStarterPackStatusChecked;
                StarterPackSample.CurrencyBalanceChanged += OnCurrencyBalanceChanged;
            }

            private void OnDisable()
            {
                StarterPackSample.StarterPackStatusChecked -= OnStarterPackStatusChecked;
                StarterPackSample.CurrencyBalanceChanged -= OnCurrencyBalanceChanged;
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

                resetPlayerDataButton.interactable = m_Enabled;
                giveTenGemsButton.interactable = m_Enabled;
                buyStarterPackButton.gameObject.SetActive(true);
                buyStarterPackButton.interactable = false;
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
                        claimedOverlay.SetActive(true);
                        break;
                }
            }

            public void Disable()
            {
                m_Enabled = false;
                Refresh();
            }

            public void Enable()
            {
                m_Enabled = true;
                Refresh();
            }
        }
    }
}
