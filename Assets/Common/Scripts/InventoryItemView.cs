using System;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.Services.Samples
{
    public class InventoryItemView : MonoBehaviour
    {
        public Sprite swordSprite;
        public Sprite shieldSprite;

        Image m_IconImage;

        void Awake()
        {
            m_IconImage = GetComponentInChildren<Image>();
        }

        public void SetKey(string key)
        {
            switch (key)
            {
                case "SWORD":
                    m_IconImage.sprite = swordSprite;
                    break;

                case "SHIELD":
                    m_IconImage.sprite = shieldSprite;
                    break;

                default:
                    m_IconImage.sprite = null;
                    break;
            }
        }
    }
}
