using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.Services.Samples.ProjectInbox
{
    public class InboxButtonView : MonoBehaviour
    {
        [SerializeField] Button m_InboxButton;
        [SerializeField] Image m_MessagesCallout;
        [SerializeField] Image m_InboxCountIndicator;
        [SerializeField] TextMeshProUGUI m_InboxCountText;

        void Awake()
        {
            HideNewMessageAlerts();
            m_InboxButton.interactable = false;
        }

        public void SetInteractable(bool isInteractable)
        {
            m_InboxButton.interactable = isInteractable;
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
            m_InboxCountText.text = messageCount.ToString();
            m_InboxCountIndicator.gameObject.SetActive(true);
            m_MessagesCallout.gameObject.SetActive(true);
        }

        void HideNewMessageAlerts()
        {
            m_InboxCountIndicator.gameObject.SetActive(false);
            m_MessagesCallout.gameObject.SetActive(false);
        }
    }
}
