using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.Services.Samples.VirtualShop
{
    public class CategoryButton : MonoBehaviour
    {
        public Button targetButton;
        public TextMeshProUGUI text;
        public Color defaultTextColor;
        public Material defaultTextMaterial;
        public Color activeTextColor;
        public Material activeTextMaterial;

        VirtualShopSceneManager m_VirtualShopSceneManager;

        public void Initialize(VirtualShopSceneManager virtualShopSceneManager, string category)
        {
            m_VirtualShopSceneManager = virtualShopSceneManager;
            text.text = category;
        }

        public void UpdateCategoryButtonUIState(string selectedCategoryId)
        {
            targetButton.interactable = text.text != selectedCategoryId;
            text.color = text.text == selectedCategoryId ? activeTextColor : defaultTextColor;
            text.fontMaterial = text.text == selectedCategoryId ? activeTextMaterial : defaultTextMaterial;
        }

        public void OnClick()
        {
            m_VirtualShopSceneManager.OnCategoryButtonClicked(text.text);
        }
    }
}
