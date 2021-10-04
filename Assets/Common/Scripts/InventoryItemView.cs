using UnityEngine;
using UnityEngine.UI;

namespace GameOperationsSamples
{
    public class InventoryItemView : MonoBehaviour
    {
        public Sprite swordSprite;
        public Sprite shieldSprite;

        private Image m_IconImage;

        void Awake()
        {
            m_IconImage = GetComponentInChildren<Image>();
        }

        public void SetKey(string key)
        {
            m_IconImage.sprite = key switch
            {
                "SWORD" => swordSprite,
                "SHIELD" => shieldSprite,
                _ => null
            };
        }
    }
}
