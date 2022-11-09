using System.Collections;
using UnityEngine;

namespace Unity.Services.Samples.ProjectInbox
{
    public class InboxPopupView : MonoBehaviour
    {
        [SerializeField] MessageListView m_MessageListView;
        [SerializeField] GameObject m_LoadingSpinner;

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void ShowUpdatingState()
        {
            m_MessageListView.SetInteractable(false);
            StartLoadingSpinner();
        }

        public void HideUpdatingState()
        {
            StartCoroutine(StopLoadingSpinner());
            m_MessageListView.SetInteractable(true);
        }

        void StartLoadingSpinner()
        {
            if (m_LoadingSpinner == null)
            {
                return;
            }

            m_LoadingSpinner.SetActive(true);
        }

        IEnumerator StopLoadingSpinner()
        {
            // Want to ensure that even if the spinner is started and stopped very quickly, that it appears
            // long enough that it is still visible to the user. So we'll delay the stopping for one second.
            yield return new WaitForSeconds(1);

            if (m_LoadingSpinner != null)
            {
                m_LoadingSpinner.SetActive(false);
            }
        }
    }
}
