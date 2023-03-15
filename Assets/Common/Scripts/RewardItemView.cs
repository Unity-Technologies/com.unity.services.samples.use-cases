using System;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

namespace Unity.Services.Samples
{
    public class RewardItemView : MonoBehaviour
    {
        public Image icon;
        public TextMeshProUGUI quantityField;

        AsyncOperationHandle<Sprite> m_IconHandle;

        public void LoadIconFromAddress(string spriteAddress)
        {
            if (m_IconHandle.IsValid())
            {
                Addressables.Release(m_IconHandle);
            }

            m_IconHandle = Addressables.LoadAssetAsync<Sprite>(spriteAddress);
            m_IconHandle.Completed += handle =>
            {
                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    SetIcon(handle.Result);
                }
                else
                {
                    Debug.Log($"A sprite could not be found for the address {spriteAddress}." +
                        $" Addressables exception: {handle.OperationException}");
                }
            };
        }

        public void SetIcon(Sprite sprite)
        {
            if (icon != null)
            {
                icon.sprite = sprite;
                gameObject.SetActive(true);
            }
        }

        public void SetQuantity(long quantity)
        {
            if (quantityField != null)
            {
                quantityField.text = $"+{quantity}";
            }
        }

        public void SetColor(Color newColor)
        {
            icon.color = newColor;
            quantityField.color = newColor;
        }
    }
}
