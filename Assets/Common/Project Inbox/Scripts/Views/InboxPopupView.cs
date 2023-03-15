using System;
using System.Collections;
using UnityEngine;

namespace Unity.Services.Samples.ProjectInbox
{
    public class InboxPopupView : MonoBehaviour
    {
        [SerializeField]
        MessageListView messageListView;

        [SerializeField]
        GameObject loadingSpinner;

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
            messageListView.SetInteractable(false);
            StartLoadingSpinner();
        }

        public void HideUpdatingState()
        {
            StartCoroutine(StopLoadingSpinner());
            messageListView.SetInteractable(true);
        }

        void StartLoadingSpinner()
        {
            if (loadingSpinner == null)
            {
                return;
            }

            loadingSpinner.SetActive(true);
        }

        IEnumerator StopLoadingSpinner()
        {
            // Want to ensure that even if the spinner is started and stopped very quickly, that it appears
            // long enough that it is still visible to the user. So we'll delay the stopping for one second.
            yield return new WaitForSeconds(1);

            if (loadingSpinner != null)
            {
                loadingSpinner.SetActive(false);
            }
        }
    }
}
