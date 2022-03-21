using TMPro;
using UnityEngine;

namespace UnityGamingServicesUseCases
{
    public class MessagePopup : MonoBehaviour
    {
        public TextMeshProUGUI titleField;
        public TextMeshProUGUI messageField;

        public void Show(string title, string message)
        {
            titleField.text = "  " + title;
            messageField.text = message;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
