using System.Collections;
using System.Collections.Generic;
using GemHunterUGS.Scripts.PlayerEconomyManagement;
using Unity.Properties;
using Unity.Services.CloudCode.GeneratedBindings.GemHunterUGSCloud.Models;
using UnityEngine;
using UnityEngine.UIElements;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;
namespace GemHunterUGS.Scripts.Store
{
    /// <summary>
    /// Handles all UI-related functionality for the in-game store.
    /// This includes managing store menus, inventory display, and purchase feedback.
    /// </summary>
    public class StoreView : MonoBehaviour
    {
        [SerializeField]
        private UIDocument m_Document;
        
        // Main UI Containers
        private VisualElement m_Root;
        private VisualElement m_StoreMenu;
        private VisualElement m_ShopItemsContainer;
        private VisualElement m_InventoryPopUp;
        private VisualElement m_StoreDarkenOverlay;
        private VisualElement m_StoreActionPopUp;
        
        // Currency and Stats
        private Label m_CoinsLabel;
        private Label m_HeartLabel;
        private Label m_InfinityHeartLabel;
        
        // Navigation Buttons
        public Button InventoryButton { get; private set; }
        public Button CloseInventoryButton { get; private set; }
        public Button CloseStoreMenuButton { get; private set; }
        public Button ClosePopUpButton { get; private set; }
        
        // Bundle Purchase Buttons
        private VisualElement m_BundlePackItem;
        private VisualElement m_MegaPackItem;
        public Button BundlePackPurchaseButton { get; private set; }
        public Button MegaPackPurchaseButton { get; private set; }
        
        public Button StoreTestButton { get; private set; }
        
        // Coin Packs
        private VisualElement m_CoinPack10;
        private VisualElement m_CoinPack100;
        private VisualElement m_CoinPack500;
        private VisualElement m_CoinPack1k;
        private VisualElement m_CoinPack5k;
        private VisualElement m_CoinPack10k;
        
        public Button CoinPackFreeButton { get; private set; }
        public Button CoinPack100Button { get; private set; }
        public Button CoinPack500Button { get; private set; }
        public Button CoinPack1kButton { get; private set; }
        public Button CoinPack5kButton { get; private set; }
        public Button CoinPack10kButton { get; private set; }
        
        // Player Inventory Items
        private VisualElement m_LargeBombItem;
        private VisualElement m_SmallBombItem;
        private VisualElement m_ColorBonusItem;
        private VisualElement m_VerticalRocketItem;
        private VisualElement m_HorizontalRocketItem;
        
        // Inventory Labels
        private Label m_LargeBombAmountLabel;
        private Label m_SmallBombAmountLabel;
        private Label m_ColorBonusAmountLabel;
        private Label m_VerticalRocketAmountLabel;
        private Label m_HorizontalRocketAmountLabel;
        
        private Coroutine m_CurrentPopupCoroutine;
        private const float k_PopUpDisplayDuration = 3f;
        
        public void Initialize(PlayerData playerData, PlayerEconomyData economyData)
        {
            if (m_Document == null)
            {
                Logger.LogError("StoreView needs Hub's UIDocument");
            }
            
            SetupUIElements();
            InitializePurchasingUI();
            InitializeInventoryPopUp();
            SetupBindings(playerData, economyData);
        }

        private void SetupUIElements()
        {
            m_Root = m_Document.rootVisualElement;
            
            // Main containers
            m_StoreMenu = m_Root.Q<VisualElement>("StoreMenu");
            m_ShopItemsContainer = m_StoreMenu.Q<VisualElement>("ShopItemsContainer");
            
            // Currency and stats
            m_CoinsLabel = m_StoreMenu.Q<Label>("CoinsLabel");
            m_HeartLabel = m_StoreMenu.Q<Label>("HeartLabel");
            m_InfinityHeartLabel = m_StoreMenu.Q<Label>("InfinityHeartLabel");
            
            // Navigation buttons
            InventoryButton = m_StoreMenu.Q<Button>("InventoryButton");
            CloseStoreMenuButton = m_StoreMenu.Q<Button>("CloseStoreMenuButton");
            
            // Popup elements
            m_StoreActionPopUp = m_StoreMenu.Q<VisualElement>("StoreActionPopUp");
            ClosePopUpButton = m_StoreActionPopUp.Q<Button>("ClosePopUpButton");
            m_StoreDarkenOverlay = m_StoreMenu.Q<VisualElement>("StoreDarkenOverlay");
        }
        
         private void InitializePurchasingUI()
        {
            m_BundlePackItem = m_ShopItemsContainer.Q<VisualElement>("BundlePackItem");
            m_MegaPackItem = m_ShopItemsContainer.Q<VisualElement>("MegaPackItem");
            
            BundlePackPurchaseButton = m_BundlePackItem.Q<Button>("PurchaseButton");
            MegaPackPurchaseButton = m_MegaPackItem.Q<Button>("PurchaseButton");
            
            m_CoinPack10 = m_ShopItemsContainer.Q<VisualElement>("CoinGridElement_10");
            m_CoinPack100 = m_ShopItemsContainer.Q<VisualElement>("CoinGridElement_100");
            m_CoinPack500 = m_ShopItemsContainer.Q<VisualElement>("CoinGridElement_500");
            m_CoinPack1k = m_ShopItemsContainer.Q<VisualElement>("CoinGridElement_1k");
            m_CoinPack5k = m_ShopItemsContainer.Q<VisualElement>("CoinGridElement_5k");
            m_CoinPack10k = m_ShopItemsContainer.Q<VisualElement>("CoinGridElement_10k");
            
            StoreTestButton = m_ShopItemsContainer.Q<Button>("StoreTestButton");
            
            CoinPackFreeButton = m_ShopItemsContainer.Q<Button>("PurchaseButtonFree");
            CoinPack100Button = m_CoinPack100.Q<Button>("PurchaseButton");
            CoinPack500Button = m_CoinPack500.Q<Button>("PurchaseButton");
            CoinPack1kButton = m_CoinPack1k.Q<Button>("PurchaseButton");
            CoinPack5kButton = m_CoinPack5k.Q<Button>("PurchaseButton");
            CoinPack10kButton = m_CoinPack10k.Q<Button>("PurchaseButton");
        }

        private void InitializeInventoryPopUp()
        {
            m_InventoryPopUp = m_StoreMenu.Q<VisualElement>("InventoryPopUp");
            CloseInventoryButton = m_InventoryPopUp.Q<Button>("CloseInventoryButton");
            
            m_LargeBombItem = m_InventoryPopUp.Q<VisualElement>("LargeBombItem");
            m_SmallBombItem = m_InventoryPopUp.Q<VisualElement>("SmallBombItem");
            m_ColorBonusItem = m_InventoryPopUp.Q<VisualElement>("ColorBonusItem");
            m_VerticalRocketItem = m_InventoryPopUp.Q<VisualElement>("VerticalRocketItem");
            m_HorizontalRocketItem = m_InventoryPopUp.Q<VisualElement>("HorizontalRocketItem");

            m_LargeBombAmountLabel = m_LargeBombItem.Q<Label>("AmountLabel");
            m_SmallBombAmountLabel = m_SmallBombItem.Q<Label>("AmountLabel");
            m_ColorBonusAmountLabel = m_ColorBonusItem.Q<Label>("AmountLabel");
            m_VerticalRocketAmountLabel = m_VerticalRocketItem.Q<Label>("AmountLabel");
            m_HorizontalRocketAmountLabel = m_HorizontalRocketItem.Q<Label>("AmountLabel");
        }

        private void SetupBindings(PlayerData playerData, PlayerEconomyData economyData)
        {
            var coinBinding = new DataBinding()
            {
                dataSource = economyData,
                dataSourcePath = new PropertyPath("Currencies"),
                bindingMode = BindingMode.ToTarget
            };
            
            coinBinding.sourceToUiConverters.AddConverter((ref Dictionary<string,int> currencies) => 
                currencies[PlayerEconomyManager.k_Coin].ToString());
            
            m_CoinsLabel.SetBinding("text", coinBinding);
            
            m_HeartLabel.dataSource = playerData;
            m_HeartLabel.SetBinding("text", new DataBinding()
            {
                dataSourcePath = new PropertyPath("Hearts")
            });
        }
        
        public void OpenStoreMenu()
        {
            m_StoreMenu.style.display = DisplayStyle.Flex;
            HidePurchaseSuccessPopUp();
        }
        
        public void CloseStoreMenu()
        {
            m_StoreMenu.style.display = DisplayStyle.None;
            m_ShopItemsContainer.style.display = DisplayStyle.Flex;
            m_InventoryPopUp.style.display = DisplayStyle.None;
            HidePurchaseSuccessPopUp();
        }
        
        public void TogglePlayerInventoryPopUp()
        {
            bool isInventoryOpen = m_InventoryPopUp.style.display == DisplayStyle.Flex;

            if (isInventoryOpen)
            {
                CloseInventoryPopUp();
            }
            else
            {
                OpenInventoryPopUp();
            }
        }
        
        private void OpenInventoryPopUp()
        {
            m_InventoryPopUp.style.display = DisplayStyle.Flex;
            m_ShopItemsContainer.style.display = DisplayStyle.None;
            HidePurchaseSuccessPopUp();
        }

        public void CloseInventoryPopUp()
        {
            m_InventoryPopUp.style.display = DisplayStyle.None;
            m_ShopItemsContainer.style.display = DisplayStyle.Flex;
        }

        public void ShowPurchaseSuccessPopUp(string message)
        {
            if (m_CurrentPopupCoroutine != null)
            {
                StopCoroutine(m_CurrentPopupCoroutine);
            }
            
            m_StoreActionPopUp.style.display = DisplayStyle.Flex;
            ClosePopUpButton.text = message;
            m_StoreDarkenOverlay.style.display = DisplayStyle.Flex;
            
            m_CurrentPopupCoroutine = StartCoroutine(AutoClosePopup());
        }
        
        private IEnumerator AutoClosePopup()
        {
            yield return new WaitForSeconds(k_PopUpDisplayDuration);
            HidePurchaseSuccessPopUp();
            m_CurrentPopupCoroutine = null;
        }

        public void HidePurchaseSuccessPopUp()
        {
            m_StoreActionPopUp.style.display = DisplayStyle.None;
            ClosePopUpButton.text = string.Empty;
            m_StoreDarkenOverlay.style.display = DisplayStyle.None;
            ActivateButtonsForRepurchase();
        }
        
        private void ActivatePurchaseButton(Button button)
        {
            button.SetEnabled(true);
        }
        
        public void DeactivatePurchaseButton(Button button)
        {
            button.SetEnabled(false);
        }

        private void ActivateButtonsForRepurchase()
        {
            // Not the free Coin Pack though :) -- even if clicked, this can't be redeemed again
            ActivatePurchaseButton(BundlePackPurchaseButton);
            ActivatePurchaseButton(MegaPackPurchaseButton);
            ActivatePurchaseButton(CoinPack100Button);
            ActivatePurchaseButton(CoinPack500Button);
            ActivatePurchaseButton(CoinPack1kButton);
            ActivatePurchaseButton(CoinPack5kButton);
            ActivatePurchaseButton(CoinPack10kButton);
        }
        
        public void ShowInfinityHeartLabel(bool show)
        {
            m_InfinityHeartLabel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }
        
        public void UpdatePlayerInventoryItemAmounts(PlayerEconomyData economyData)
        {
            m_CoinsLabel.text = economyData.Currencies[PlayerEconomyManager.k_Coin].ToString();
            m_LargeBombAmountLabel.text = economyData.ItemInventory[PlayerEconomyManager.k_LargeBomb].ToString();
            m_SmallBombAmountLabel.text = economyData.ItemInventory[PlayerEconomyManager.k_SmallBomb].ToString();
            m_ColorBonusAmountLabel.text = economyData.ItemInventory[PlayerEconomyManager.k_ColorBonus].ToString();
            m_VerticalRocketAmountLabel.text = economyData.ItemInventory[PlayerEconomyManager.k_VerticalRocket].ToString();
            m_HorizontalRocketAmountLabel.text = economyData.ItemInventory[PlayerEconomyManager.k_HorizontalRocket].ToString();
        }
    }
}
