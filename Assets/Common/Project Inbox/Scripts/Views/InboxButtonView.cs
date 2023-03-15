using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.Services.Samples.ProjectInbox
{
    public class InboxButtonView : MonoBehaviour
    {
        [SerializeField]
        Button inboxButton;

        [SerializeField]
        Image messagesCallout;

        [SerializeField]
        Image inboxCountIndicator;

        [SerializeField]
        TextMeshProUGUI inboxCountText;

        void Awake()
        {
            HideNewMessageAlerts();
            inboxButton.interactable = false;
        }

        public void SetInteractable(bool isInteractable)
        {
            inboxButton.interactable = isInteractable;
        }

        public void UpdateView(int unreadMessageCount)
        {
            if (unreadMessageCount > 0)
            {
                ShowNewMessageAlerts(unreadMessageCount);
            }
            else
            {
                HideNewMessageAlerts();
            }
        }

        void ShowNewMessageAlerts(int messageCount)
        {
            inboxCountText.text = messageCount.ToString();
            inboxCountIndicator.gameObject.SetActive(true);
            messagesCallout.gameObject.SetActive(true);
        }

        void HideNewMessageAlerts()
        {
            inboxCountIndicator.gameObject.SetActive(false);
            messagesCallout.gameObject.SetActive(false);
        }
    }
}
