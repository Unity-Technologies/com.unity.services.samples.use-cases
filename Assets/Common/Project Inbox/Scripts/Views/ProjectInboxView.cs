using UnityEngine;

namespace Unity.Services.Samples.ProjectInbox
{
    public class ProjectInboxView : MonoBehaviour
    {
        [SerializeField] InboxButtonView m_InboxButtonView;
        [SerializeField] InboxPopupView m_InboxPopupView;
        [SerializeField] MessageListView m_MessageListView;
        [SerializeField] MessageDetailView m_MessageDetailView;

        public void Initialize()
        {
            m_InboxButtonView.UpdateView(InboxStateManager.unreadMessageCount);
            m_InboxButtonView.SetInteractable(true);
        }

        public void UpdateInboxView()
        {
            m_InboxPopupView.ShowUpdatingState();
            m_MessageListView.InitializeMessagePreviews();
            m_InboxButtonView.UpdateView(InboxStateManager.unreadMessageCount);
            m_InboxPopupView.HideUpdatingState();
        }

        public void DeleteMessagePreview(string messageId)
        {
            m_MessageListView.DeleteMessagePreview(messageId);
        }

        public void UpdateViewForNewMessageSelected(MessagePreviewView messagePreviewView, InboxMessage message)
        {
            m_MessageListView.SelectNewMessage(messagePreviewView, message);
            m_MessageDetailView.Show(message);
            m_InboxButtonView.UpdateView(InboxStateManager.unreadMessageCount);
        }
    }
}
