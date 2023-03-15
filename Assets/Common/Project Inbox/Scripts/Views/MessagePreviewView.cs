using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.Services.Samples.ProjectInbox
{
    public class MessagePreviewView : MonoBehaviour
    {
        [SerializeField]
        TextMeshProUGUI titleText;

        [SerializeField]
        TextMeshProUGUI expirationText;

        [SerializeField]
        Button selectorButton;

        [Header("State Indicators")]
        [SerializeField]
        GameObject messageUnreadIndicator;

        [SerializeField]
        Image selectedBackground;

        [SerializeField]
        Image readBackground;

        [SerializeField]
        Image unreadBackground;

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
            if (titleText != null)
            {
                titleText.text = m_Title;
            }

            if (expirationText != null)
            {
                expirationText.text = m_Expiration;
            }

            if (messageUnreadIndicator != null)
            {
                messageUnreadIndicator.SetActive(!m_Message.metadata.isRead);
            }

            SetButtonBackgroundByMessageState();
        }

        void SetButtonBackgroundByMessageState()
        {
            if (selectorButton == null || selectedBackground == null || readBackground == null ||
                unreadBackground == null)
            {
                return;
            }

            if (m_IsCurrentlySelected)
            {
                selectorButton.targetGraphic = selectedBackground;
                selectedBackground.gameObject.SetActive(true);
                readBackground.gameObject.SetActive(false);
                unreadBackground.gameObject.SetActive(false);
            }
            else if (m_Message.metadata.isRead)
            {
                selectorButton.targetGraphic = readBackground;
                readBackground.gameObject.SetActive(true);
                unreadBackground.gameObject.SetActive(false);
                selectedBackground.gameObject.SetActive(false);
            }
            else
            {
                selectorButton.targetGraphic = unreadBackground;
                unreadBackground.gameObject.SetActive(true);
                readBackground.gameObject.SetActive(false);
                selectedBackground.gameObject.SetActive(false);
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
