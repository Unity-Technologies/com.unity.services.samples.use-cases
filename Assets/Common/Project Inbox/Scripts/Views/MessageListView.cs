using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.Services.Samples.ProjectInbox
{
    public class MessageListView : MonoBehaviour
    {
        [SerializeField] MessagePreviewView m_MessagePreviewPrefab;
        [SerializeField] Transform m_MessageListContainer;

        [Header("Assign In Scene")]
        [SerializeField] ProjectInboxManager m_ProjectInboxManager;

        Dictionary<string, (GameObject gameObject, Button button)> m_MessagePreviews =
            new Dictionary<string, (GameObject, Button)>();
        bool m_IsViewInteractable;
        string m_SelectedMessageId;
        MessagePreviewView m_SelectedMessagePreviewView;

        private void Awake()
        {
            InitializeMessagePreviews();
        }

        public void InitializeMessagePreviews()
        {
            ClearMessageListContainer();

            var inboxMessages = InboxStateManager.inboxMessages;

            // Put most recent messages at the top of the view
            for (var index = inboxMessages.Count - 1; index >= 0; index--)
            {
                var inboxMessage = inboxMessages[index];
                var isCurrentlySelected = string.Equals(inboxMessage.messageId, m_SelectedMessageId);

                var view = Instantiate(m_MessagePreviewPrefab, m_MessageListContainer);
                view.SetData(inboxMessage, isCurrentlySelected, m_ProjectInboxManager);

                var messagePreviewGameObject = view.gameObject;
                var button = messagePreviewGameObject.GetComponent<Button>();

                m_MessagePreviews.Add(inboxMessage.messageId, (messagePreviewGameObject, button));
            }
        }

        void ClearMessageListContainer()
        {
            foreach (var messagePreview in m_MessagePreviews.Values) 
            {
                Destroy(messagePreview.gameObject);
            }
            
            m_MessagePreviews.Clear();
        }

        public void SetInteractable(bool isInteractable)
        {
            m_IsViewInteractable = isInteractable;

            foreach (var messagePreview in m_MessagePreviews.Values)
            {
                var button = messagePreview.button;

                if (button != null)
                {
                    button.interactable = m_IsViewInteractable;
                }
            }
        }

        public void DeleteMessagePreview(string messageId)
        {
            if (m_MessagePreviews.TryGetValue(messageId, out var messagePreview))
            {
                Destroy(messagePreview.gameObject);
                m_MessagePreviews.Remove(messageId);
            }
        }

        public void SelectNewMessage(MessagePreviewView messagePreviewView, InboxMessage message)
        {
            m_SelectedMessageId = message.messageId;

            if (m_SelectedMessagePreviewView != null)
            {
                m_SelectedMessagePreviewView.Unselect();
            }

            m_SelectedMessagePreviewView = messagePreviewView;
            m_SelectedMessagePreviewView.Select();
        }
    }
}
