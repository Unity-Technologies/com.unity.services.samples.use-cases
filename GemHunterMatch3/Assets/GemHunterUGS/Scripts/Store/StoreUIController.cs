using System;
using System.Collections.Generic;
using GemHunterUGS.Scripts.Core;
using UnityEngine;
using Unity.Services.CloudCode.GeneratedBindings.GemHunterUGSCloud.Models;
using UnityEngine.UIElements;
using GemHunterUGS.Scripts.PlayerDataManagement;
using GemHunterUGS.Scripts.PlayerEconomyManagement;
using GemHunterUGS.Scripts.PlayerHub;
using GemHunterUGS.Scripts.Utilities;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;

namespace GemHunterUGS.Scripts.Store
{
    /// <summary>
    /// Controls the store UI interactions and coordinates between the store view and purchase client.
    /// Manages button states, inventory updates, and purchase flow UI feedback.
    /// </summary>
    public class StoreUIController : MonoBehaviour
    {
        [SerializeField]
        private StoreView m_StoreView;
        [SerializeField]
        private StoreClient m_StoreClient;
        [SerializeField]
        private HubUIController m_HubUIController;
        
        private PlayerEconomyManager m_PlayerEconomyManager;
        private PlayerDataManager m_PlayerDataManager;

        // Store button handlers for cleanup
        private readonly Dictionary<Button, Action> m_CoinPackHandlers = new();
        
        public event Action ClickPurchaseBundlePack;
        public event Action ClickPurchaseMegaPack;
        public event Action<int> ClickPurchaseCoinPack;
        public event Action ClickPurchaseFreeCoinPack;
        
        private void Start()
        {
            InitializeDependencies();
            
            m_StoreView.Initialize(m_PlayerDataManager.PlayerDataLocal, m_PlayerEconomyManager.PlayerEconomyDataLocal);
            m_StoreView.CloseStoreMenu();
            
            SetupEventHandlers();

            if (m_PlayerEconomyManager.PlayerEconomyDataLocal.HasPurchasedFreeCoinPack)
            {
                m_StoreView.DeactivatePurchaseButton(m_StoreView.CoinPackFreeButton);
            }
        }
        
        private void InitializeDependencies()
        {
            if (m_StoreView == null)
            {
                m_StoreView = GetComponent<StoreView>();
                if (m_StoreView == null)
                    Logger.LogError($"StoreView not found on {gameObject.name}");
            }

            if (m_StoreClient == null)
            {
                m_StoreClient = GetComponent<StoreClient>();
                if (m_StoreClient == null)
                    Logger.LogError($"StoreClient not found on {gameObject.name}");
            }
            
            m_PlayerEconomyManager = GameSystemLocator.Get<PlayerEconomyManager>();
            m_PlayerDataManager = GameSystemLocator.Get<PlayerDataManager>();
        }

        private void SetupEventHandlers()
        {
            m_PlayerEconomyManager.LocalEconomyDataUpdated += HandleUpdateItemInventory;
            
            m_PlayerEconomyManager.InfiniteHeartStatusUpdated += HandleInfiniteHeartStatus;
            
            m_StoreView.InventoryButton.clicked += HandleInventoryButtonClicked;
            m_StoreView.CloseStoreMenuButton.clicked += HandleCloseStoreMenuButtonClicked;
            m_StoreView.CloseInventoryButton.clicked += m_StoreView.CloseInventoryPopUp;

            m_StoreView.BundlePackPurchaseButton.clicked += HandleBundlePackPurchaseClick;
            m_StoreView.MegaPackPurchaseButton.clicked += HandleMegaPackPurchaseClick;

            m_StoreView.CoinPackFreeButton.clicked += HandleCoinPackFreePurchaseClick;
            
            m_StoreClient.SuccessfullyPurchased += HandlePurchaseSuccess;
            m_StoreView.ClosePopUpButton.clicked += HandleClosePurchaseSuccessPopup;
            
            SetupCoinPackButton(m_StoreView.CoinPack100Button, 100);
            SetupCoinPackButton(m_StoreView.CoinPack500Button, 500);
            SetupCoinPackButton(m_StoreView.CoinPack1kButton, 1000);
            SetupCoinPackButton(m_StoreView.CoinPack5kButton, 5000);
            SetupCoinPackButton(m_StoreView.CoinPack10kButton, 10000);
        }

        private void SetupCoinPackButton(Button button, int coinAmount)
        {
            if (button == null)
            {
                Logger.LogError($"CoinPack button for {coinAmount} coins is null");
                return;
            }
            
            void CoinPackClickHandler()
            {
                Logger.LogVerbose($"Clicked to purchase {coinAmount} coins");
                HandleCoinPackPurchaseClick(coinAmount, button);
            }
            m_CoinPackHandlers[button] = CoinPackClickHandler;
            button.clicked += CoinPackClickHandler;
        }
        
        private void HandleBundlePackPurchaseClick()
        {
            ClickPurchaseBundlePack?.Invoke();
            m_StoreView.DeactivatePurchaseButton(m_StoreView.BundlePackPurchaseButton);
        }

        private void HandleMegaPackPurchaseClick()
        {
            ClickPurchaseMegaPack?.Invoke();
            m_StoreView.DeactivatePurchaseButton(m_StoreView.MegaPackPurchaseButton);
        }

        private void HandleCoinPackPurchaseClick(int amount, Button button)
        {
            ClickPurchaseCoinPack?.Invoke(amount);
            m_StoreView.DeactivatePurchaseButton(button);
        }
        
        private void HandleCoinPackFreePurchaseClick()
        {
            if (m_PlayerEconomyManager.PlayerEconomyDataLocal.HasPurchasedFreeCoinPack)
            {
                Debug.LogWarning($"Player has already purchased free coin pack!");
                return;
            }
            ClickPurchaseFreeCoinPack?.Invoke();
            m_StoreView.DeactivatePurchaseButton(m_StoreView.CoinPackFreeButton);
        }
        
        private void HandleCloseStoreMenuButtonClicked()
        {
            m_StoreView.CloseStoreMenu();
            m_HubUIController.ShowMainHub();
        }
        
        private void HandleInventoryButtonClicked()
        {
            m_StoreView.TogglePlayerInventoryPopUp();
        }

        private void HandleUpdateItemInventory(PlayerEconomyData playerEconomyData)
        {
            m_StoreView.UpdatePlayerInventoryItemAmounts(playerEconomyData);
        }

        private void HandleInfiniteHeartStatus(bool active)
        {
            m_StoreView.ShowInfinityHeartLabel(active);
        }

        private void HandlePurchaseSuccess(string productName)
        {
            string message = $"Successfully purchased {productName}";
            m_StoreView.ShowPurchaseSuccessPopUp(message);
        }

        private void HandleClosePurchaseSuccessPopup()
        {
            m_StoreView.HidePurchaseSuccessPopUp();
        }
        
        private void OnDisable()
        {
            // Reduces error clutter if PlayerHub scene is accidentally loaded first
            if (m_PlayerEconomyManager == null)
            {
                return;
            }
            RemoveEventHandlers(); 
        }

        private void RemoveEventHandlers()
        {
            m_PlayerEconomyManager.LocalEconomyDataUpdated -= HandleUpdateItemInventory;
            
            m_StoreView.InventoryButton.clicked -= HandleInventoryButtonClicked;
            m_StoreView.CloseStoreMenuButton.clicked -= HandleCloseStoreMenuButtonClicked;
            m_StoreView.CloseInventoryButton.clicked -= m_StoreView.CloseInventoryPopUp;
            
            m_StoreView.BundlePackPurchaseButton.clicked -= HandleBundlePackPurchaseClick;
            m_StoreView.MegaPackPurchaseButton.clicked -= HandleMegaPackPurchaseClick;
            m_StoreView.CoinPackFreeButton.clicked -= HandleCoinPackFreePurchaseClick;
            
            m_PlayerEconomyManager.InfiniteHeartStatusUpdated -= HandleInfiniteHeartStatus;
            m_StoreView.ClosePopUpButton.clicked -= HandleClosePurchaseSuccessPopup;
            
            foreach (var kvp in m_CoinPackHandlers)
            {
                if (kvp.Key != null)  // Check if button still exists
                {
                    kvp.Key.clicked -= kvp.Value;
                }
            }
            m_CoinPackHandlers.Clear();
        }
    }
}
