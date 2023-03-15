using System;
using TMPro;
using UnityEngine;

namespace Unity.Services.Samples
{
    public class MessagePopup : MonoBehaviour
    {
        public TextMeshProUGUI titleField;
        public TextMeshProUGUI messageField;

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Show(string title, string message)
        {
            titleField.text = title;
            messageField.text = message;
            Show();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
