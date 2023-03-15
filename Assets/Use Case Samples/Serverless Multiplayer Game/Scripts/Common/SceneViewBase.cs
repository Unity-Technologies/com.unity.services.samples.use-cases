using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.Services.Samples.ServerlessMultiplayerGame
{
    public class SceneViewBase : MonoBehaviour
    {
        [SerializeField]
        TMP_Dropdown profileSelectDropdown;

        [SerializeField]
        TextMeshProUGUI playerNameText;

        [SerializeField]
        MessagePopup messagePopup;

        protected PanelViewBase m_CurrentPanelView;

        public void SetProfileDropdownIndex(int profileDropdownIndex)
        {
            profileSelectDropdown.SetValueWithoutNotify(profileDropdownIndex);
        }

        public virtual void SetPlayerName(string playerName)
        {
            playerNameText.text = $"{playerName}";
        }

        public void SetInteractable(bool isInteractable)
        {
            m_CurrentPanelView.SetInteractable(isInteractable);
        }

        public void ShowPopup(string title, string text)
        {
            messagePopup.Show(title, text);
        }

        public bool IsPanelVisible(PanelViewBase panelView)
        {
            return m_CurrentPanelView == panelView;
        }

        protected void ShowPanel(PanelViewBase panelView)
        {
            HideCurrentPanel();

            panelView.Show();

            m_CurrentPanelView = panelView;
        }

        protected void HideCurrentPanel()
        {
            if (m_CurrentPanelView != null)
            {
                m_CurrentPanelView.SetInteractable(false);

                m_CurrentPanelView.Hide();

                m_CurrentPanelView = null;
            }
        }
    }
}
