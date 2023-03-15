using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.Services.Samples.InGameMailbox
{
    public class MessageListItemView : MonoBehaviour
    {
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI expirationText;
        public RewardDisplayView rewardDisplayView;
        public Button selectorButton;

        [Header("State Indicators")]
        public GameObject messageUnreadIndicator;
        public Image selectedBackground;
        public Image readBackground;
        public Image unreadBackground;
        public Color rewardItemViewClaimedColor;

        public event Action<InboxMessage> messageSelected;

        InboxMessage m_Message;
        string m_Title;
        string m_Expiration;
        List<RewardDetail> m_RewardDetails = new List<RewardDetail>();
        bool m_HasAttachment;
        bool m_IsCurrentlySelected;

        public void SetData(InboxMessage currentMessage, bool isCurrentlySelected)
        {
            m_Message = currentMessage;
            if (m_Message != null && m_Message.messageInfo != null && m_Message.metadata != null)
            {
                m_Title = m_Message.messageInfo.title;
                SetExpirationText(m_Message.metadata.expirationDate);

                GetRewardDetails(m_Message.messageInfo.attachment);
                m_HasAttachment = !string.IsNullOrEmpty(m_Message.messageInfo.attachment);
            }

            m_IsCurrentlySelected = isCurrentlySelected;

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

        void GetRewardDetails(string virtualPurchaseId)
        {
            m_RewardDetails.Clear();

            if (EconomyManager.instance.virtualPurchaseTransactions.TryGetValue(virtualPurchaseId, out var rewards))
            {
                foreach (var reward in rewards)
                {
                    if (AddressablesManager.instance.preloadedSpritesByEconomyId.TryGetValue(reward.id, out var sprite))
                    {
                        m_RewardDetails.Add(new RewardDetail
                        {
                            id = reward.id,
                            quantity = reward.amount,
                            sprite = sprite
                        });
                    }
                }
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
            UpdateAttachmentPreview();
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

        void UpdateAttachmentPreview()
        {
            if (m_HasAttachment)
            {
                if (rewardDisplayView != null)
                {
                    // The message only has a claimed attachment if it both has an attachment, and that
                    // attachment is not unclaimed.
                    if (m_HasAttachment && !m_Message.metadata.hasUnclaimedAttachment)
                    {
                        rewardDisplayView.PopulateView(m_RewardDetails, rewardItemViewClaimedColor);
                    }
                    else
                    {
                        rewardDisplayView.PopulateView(m_RewardDetails);
                    }

                    rewardDisplayView.gameObject.SetActive(true);
                }
            }
            else
            {
                if (rewardDisplayView != null)
                {
                    rewardDisplayView.gameObject.SetActive(false);
                }
            }
        }

        public void OnOpenButtonPressed()
        {
            messageSelected?.Invoke(m_Message);
        }
    }
}
