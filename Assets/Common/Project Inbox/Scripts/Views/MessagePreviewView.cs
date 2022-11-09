using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.Services.Samples.ProjectInbox
{
    public class MessagePreviewView : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI m_TitleText;
        [SerializeField] TextMeshProUGUI m_ExpirationText;
        [SerializeField] Button m_SelectorButton;

        [Header("State Indicators")]
        [SerializeField] GameObject m_MessageUnreadIndicator;
        [SerializeField] Image m_SelectedBackground;
        [SerializeField] Image m_ReadBackground;
        [SerializeField] Image m_UnreadBackground;

        InboxMessage m_Message;
        string m_Title;
        string m_Expiration;
        bool m_IsCurrentlySelected;
        ProjectInboxManager m_ProjectInboxManager;

        public void SetData(InboxMessage currentMessage, bool isCurrentlySelected,
            ProjectInboxManager projectInboxManager)
        {
            m_Message = currentMessage;
            if (m_Message != null && m_Message.messageInfo != null && m_Message.metadata != null)
            {
                m_Title = m_Message.messageInfo.title;
                SetExpirationText(m_Message.metadata.expiresAtDateTime);
            }

            m_IsCurrentlySelected = isCurrentlySelected;
            m_ProjectInboxManager = projectInboxManager;

            UpdateView();
        }

        void SetExpirationText(string expirationDate)
        {
            if (DateTime.TryParse(expirationDate, out var expiration))
            {
                var culture = CultureInfo.GetCultureInfo("en-US");
                var dayFormat = "m";
                var timeFormat = "t";
                m_Expiration = $"Expires on {expiration.ToString(dayFormat, culture)} " +
                               $"at {expiration.ToString(timeFormat, culture)}";
            }
            else
            {
                m_Expiration = "";
            }
        }

        void UpdateView()
        {
            if (m_TitleText != null)
            {
                m_TitleText.text = m_Title;
            }

            if (m_ExpirationText != null)
            {
                m_ExpirationText.text = m_Expiration;
            }

            if (m_MessageUnreadIndicator != null)
            {
                m_MessageUnreadIndicator.SetActive(!m_Message.metadata.isRead);
            }

            SetButtonBackgroundByMessageState();
        }

        void SetButtonBackgroundByMessageState()
        {
            if (m_SelectorButton == null || m_SelectedBackground == null || m_ReadBackground == null ||
                m_UnreadBackground == null)
            {
                return;
            }

            if (m_IsCurrentlySelected)
            {
                m_SelectorButton.targetGraphic = m_SelectedBackground;
                m_SelectedBackground.gameObject.SetActive(true);
                m_ReadBackground.gameObject.SetActive(false);
                m_UnreadBackground.gameObject.SetActive(false);
            }
            else if (m_Message.metadata.isRead)
            {
                m_SelectorButton.targetGraphic = m_ReadBackground;
                m_ReadBackground.gameObject.SetActive(true);
                m_UnreadBackground.gameObject.SetActive(false);
                m_SelectedBackground.gameObject.SetActive(false);
            }
            else
            {
                m_SelectorButton.targetGraphic = m_UnreadBackground;
                m_UnreadBackground.gameObject.SetActive(true);
                m_ReadBackground.gameObject.SetActive(false);
                m_SelectedBackground.gameObject.SetActive(false);
            }
        }

        public void OnOpenButtonPressed()
        {
            m_ProjectInboxManager.SelectMessage(this, m_Message);
        }

        public void Select()
        {
            m_IsCurrentlySelected = true;
            UpdateView();
        }

        public void Unselect()
        {
            m_IsCurrentlySelected = false;
            SetButtonBackgroundByMessageState();
        }
    }
}
