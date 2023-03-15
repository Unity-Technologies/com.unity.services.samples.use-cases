using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Unity.Services.Samples.ProjectInbox
{
    public class MessageDetailView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField]
        TextMeshProUGUI title;

        [SerializeField]
        TextMeshProUGUI content;

        [SerializeField]
        Image messageImage;

        [SerializeField]
        ScrollRect contentScrollRect;

        [Header("Assign In Scene")]
        [SerializeField]
        ProjectInboxManager projectInboxManager;

        [SerializeField]
        Camera sceneCamera;

        string m_MessageId;

        public void Show(InboxMessage message)
        {
            if (message == null)
            {
                return;
            }

            m_MessageId = message.messageId;
            title.text = message.messageInfo?.title ?? "";
            content.text = message.messageInfo?.content ?? "";

            if (AddressablesManager.instance.addressableSpriteContent.TryGetValue(m_MessageId, out var spriteContent))
            {
                messageImage.sprite = spriteContent.sprite;
                messageImage.gameObject.SetActive(true);
            }
            else
            {
                messageImage.gameObject.SetActive(false);
            }

            contentScrollRect.verticalNormalizedPosition = 1f;

            gameObject.SetActive(true);
        }

        public void OnDeleteMessageButtonPressed()
        {
            projectInboxManager.DeleteMessage(m_MessageId);
            gameObject.SetActive(false);
        }

        public void OnPointerClick(PointerEventData pointerEventData)
        {
            var linkIndex = TMP_TextUtilities.FindIntersectingLink(content, Input.mousePosition, sceneCamera);

            if (linkIndex == -1)
            {
                return;
            }

            var linkInfo = content.textInfo.linkInfo[linkIndex];

            // open the link id as a url, which is the metadata we added in the text field
            Application.OpenURL(linkInfo.GetLinkID());
        }
    }
}
