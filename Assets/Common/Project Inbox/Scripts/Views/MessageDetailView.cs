using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Unity.Services.Samples.ProjectInbox
{
    public class MessageDetailView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] TextMeshProUGUI m_Title;
        [SerializeField] TextMeshProUGUI m_Content;
        [SerializeField] Image m_MessageImage;
        [SerializeField] ScrollRect m_ContentScrollRect;

        [Header("Assign In Scene")]
        [SerializeField] ProjectInboxManager m_ProjectInboxManager;
        [SerializeField] Camera m_SceneCamera;

        string messageId;
    
        public void Show(InboxMessage message)
        {
            if (message == null)
            {
                return;
            }

            messageId = message.messageId;
            m_Title.text = message.messageInfo?.title ?? "";
            m_Content.text = message.messageInfo?.content ?? "";

            if (AddressablesManager.instance.addressableSpriteContent.TryGetValue(messageId, out var spriteContent))
            {
                m_MessageImage.sprite = spriteContent.sprite;
                m_MessageImage.gameObject.SetActive(true);
            }
            else
            {
                m_MessageImage.gameObject.SetActive(false);
            }

            m_ContentScrollRect.verticalNormalizedPosition = 1f;

            gameObject.SetActive(true);
        }

        public void OnDeleteMessageButtonPressed()
        {
            m_ProjectInboxManager.DeleteMessage(messageId);
            gameObject.SetActive(false);
        }

        public void OnPointerClick(PointerEventData pointerEventData) {
            var linkIndex = TMP_TextUtilities.FindIntersectingLink(m_Content, Input.mousePosition, m_SceneCamera);

            if (linkIndex == -1)
            {
                return;
            }

            var linkInfo = m_Content.textInfo.linkInfo[linkIndex];

            // open the link id as a url, which is the metadata we added in the text field
            Application.OpenURL(linkInfo.GetLinkID());
        }
    }
}
