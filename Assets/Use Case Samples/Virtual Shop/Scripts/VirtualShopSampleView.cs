using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.Services.Samples.VirtualShop
{
    public class VirtualShopSampleView : MonoBehaviour
    {
        public VirtualShopSceneManager virtualShopSceneManager;

        public GameObject comingSoonPanel;

        public Button inventoryButton;
        public InventoryPopupView inventoryPopupView;

        public Button gainCurrencyDebugButton;

        public GameObject itemsContainer;

        public VirtualShopItemView virtualShopItemPrefab;

        public GameObject categoryButtonsContainerGroup;
        public CategoryButton categoryButtonPrefab;

        public RewardPopupView rewardPopup;

        public MessagePopup messagePopup;

        List<CategoryButton> m_CategoryButtons = new List<CategoryButton>();

        public void SetInteractable(bool isInteractable = true)
        {
            inventoryButton.interactable = isInteractable;
            gainCurrencyDebugButton.interactable = isInteractable;
        }

        public async void OnInventoryButtonPressed()
        {
            try
            {
                await inventoryPopupView.Show();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public void Initialize(Dictionary<string, VirtualShopCategory> virtualShopCategories)
        {
            foreach (var kvp in virtualShopCategories)
            {
                var categoryButtonGameObject = Instantiate(categoryButtonPrefab.gameObject,
                    categoryButtonsContainerGroup.transform);
                var categoryButton = categoryButtonGameObject.GetComponent<CategoryButton>();
                categoryButton.Initialize(virtualShopSceneManager, kvp.Value.id);
                m_CategoryButtons.Add(categoryButton);
            }
        }

        public void ShowCategory(VirtualShopCategory virtualShopCategory)
        {
            ShowItems(virtualShopCategory);

            foreach (var categoryButton in m_CategoryButtons)
            {
                categoryButton.UpdateCategoryButtonUIState(virtualShopCategory.id);
            }

            comingSoonPanel.SetActive(!virtualShopCategory.enabledFlag);
        }

        void ShowItems(VirtualShopCategory virtualShopCategory)
        {
            if (virtualShopItemPrefab is null)
            {
                throw new NullReferenceException("Shop Item Prefab was null.");
            }

            ClearContainer();

            foreach (var virtualShopItem in virtualShopCategory.virtualShopItems)
            {
                var virtualShopItemGameObject = Instantiate(virtualShopItemPrefab.gameObject,
                    itemsContainer.transform);
                virtualShopItemGameObject.GetComponent<VirtualShopItemView>().Initialize(
                    virtualShopSceneManager, virtualShopItem, AddressablesManager.instance);
            }
        }

        void ClearContainer()
        {
            var itemsContainerTransform = itemsContainer.transform;
            for (var i = itemsContainerTransform.childCount - 1; i >= 0; i--)
            {
                Destroy(itemsContainerTransform.GetChild(i).gameObject);
            }
        }

        public void ShowRewardPopup(List<RewardDetail> rewards)
        {
            rewardPopup.Show(rewards);
        }

        public void OnCloseRewardPopupClicked()
        {
            rewardPopup.Close();
        }

        public void ShowVirtualPurchaseFailedErrorPopup()
        {
            messagePopup.Show("Purchase Failed.",
                "Please ensure that you have sufficient funds to complete your purchase.");
        }
    }
}
