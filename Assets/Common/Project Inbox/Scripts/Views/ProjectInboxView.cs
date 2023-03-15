using System;
using UnityEngine;

namespace Unity.Services.Samples.ProjectInbox
{
    public class ProjectInboxView : MonoBehaviour
    {
        [SerializeField]
        InboxButtonView inboxButtonView;

        [SerializeField]
        InboxPopupView inboxPopupView;

        [SerializeField]
        MessageListView messageListView;

        [SerializeField]
        MessageDetailView messageDetailView;

        public void Initialize()
        {
            inboxButtonView.UpdateView(InboxStateManager.unreadMessageCount);
            inboxButtonView.SetInteractable(true);
        }

        public void UpdateInboxView()
        {
            inboxPopupView.ShowUpdatingState();
            messageListView.InitializeMessagePreviews();
            inboxButtonView.UpdateView(InboxStateManager.unreadMessageCount);
            inboxPopupView.HideUpdatingState();
        }

        public void DeleteMessagePreview(string messageId)
        {
            messageListView.DeleteMessagePreview(messageId);
        }

        public void UpdateViewForNewMessageSelected(MessagePreviewView messagePreviewView, InboxMessage message)
        {
            messageListView.SelectNewMessage(messagePreviewView, message);
            messageDetailView.Show(message);
            inboxButtonView.UpdateView(InboxStateManager.unreadMessageCount);
        }
    }
}
