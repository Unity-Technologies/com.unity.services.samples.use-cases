using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;
using GemHunterUGS.Scripts.Core;
using GemHunterUGS.Scripts.PlayerEconomyManagement;
using Unity.Services.CloudCode.GeneratedBindings.GemHunterUGSCloud.Models;
using GemHunterUGS.Scripts.Utilities;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;

namespace GemHunterUGS.Scripts.Store
{
    /// <summary>
    /// Handles in-app purchases using Unity's IAP system, managing product initialization, purchase processing,
    /// and server-side validation. This client interfaces with platform-specific stores (e.g., Google Play)
    /// and ensures purchase integrity through cloud validation.
    /// 
    /// Key responsibilities:
    /// - Initializes store products and pricing
    /// - Processes purchase transactions
    /// - Validates purchases with cloud backend
    /// - Updates player economy data after successful purchases
    /// 
    /// Supported product types:
    /// - Coin packs (various denominations)
    /// - Bundle packs
    /// - Mega packs
    /// </summary>
    public class StoreClient : MonoBehaviour, IDetailedStoreListener
    {
        [SerializeField]
        private StoreUIController m_StoreUIController;

        // Product IDs should match the IDs configured in the Unity Dashboard
        [SerializeField]
        private string m_BundlePackProductID = "BUNDLE_PACK";
        [SerializeField]
        private string m_MegaPackProductID = "MEGA_PACK";
        
        private readonly Dictionary<int, string> m_CoinPackProducts = new()
        {
            { 100, "COIN_PACK_100" },
            { 500, "COIN_PACK_500" },
            { 1000, "COIN_PACK_1000" },
            { 5000, "COIN_PACK_5000" },
            { 10000, "COIN_PACK_10000" }
        };
        
        private PlayerEconomyManagerClient m_PlayerEconomyClient;
        private CloudBindingsProvider m_BindingsProvider;
        private IStoreController m_StoreController;
        private IExtensionProvider m_StoreExtensionProvider;
        
        private bool m_IsPurchaseInProgress;

        public event Action<string> SuccessfullyPurchased;
        
        private void Start()
        {
            if (m_StoreUIController == null)
            {
                m_StoreUIController = GetComponent<StoreUIController>();
            }
            
            m_PlayerEconomyClient = GameSystemLocator.Get<PlayerEconomyManagerClient>();
            m_BindingsProvider = GameSystemLocator.Get<CloudBindingsProvider>();
            
            InitializeStore();
            SetupEventHandlers();
        }

        private void InitializeStore()
        {
            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
            builder.AddProduct(m_BundlePackProductID, ProductType.Consumable, new IDs()
            {
                {"bundle_pack_play", GooglePlay.Name},
                {"bundle_pack_apple", AppleAppStore.Name}
            });

            builder.AddProduct(m_MegaPackProductID, ProductType.Consumable, new IDs()
            {
                { "mega_pack_play", GooglePlay.Name },
                { "mega_pack_apple", AppleAppStore.Name }
            });
            
            foreach (var coinPack in m_CoinPackProducts)
            {
                builder.AddProduct(coinPack.Value, ProductType.Consumable, new IDs()
                {
                    { $"coin_pack_{coinPack.Key}_play", GooglePlay.Name },
                    { $"coin_pack_{coinPack.Key}_apple", AppleAppStore.Name }
                });
            }
            
            UnityPurchasing.Initialize(this, builder);
        }

        private void SetupEventHandlers()
        {
            m_StoreUIController.ClickPurchaseBundlePack += HandleBundlePackPurchase;
            m_StoreUIController.ClickPurchaseMegaPack += HandleMegaPackPurchase;
            m_StoreUIController.ClickPurchaseCoinPack += HandleCoinPackPurchase;
            m_StoreUIController.ClickPurchaseFreeCoinPack += HandleCoinPackFreePurchase;
        }

        private void HandleBundlePackPurchase()
        {
            if (m_IsPurchaseInProgress)
            {
                Logger.LogWarning("Purchase already in progress");
                return;
            }
            
            InitiatePurchase(m_BundlePackProductID);
        }

        private void HandleMegaPackPurchase()
        {
            if (m_IsPurchaseInProgress)
            {
                Logger.LogWarning("Purchase already in progress");
                return;
            }
            
            InitiatePurchase(m_MegaPackProductID);
        }
        
        private void HandleCoinPackPurchase(int coinAmount)
        {
            if (m_IsPurchaseInProgress)
            {
                Logger.LogWarning("Purchase already in progress");
                return;
            }

            if (m_CoinPackProducts.TryGetValue(coinAmount, out string productId))
            {
                InitiatePurchase(productId);
            }
            else
            {
                Logger.LogError($"No product ID found for coin amount: {coinAmount}");
            }
        }

        private void InitiatePurchase(string productId)
        {
            if (m_StoreController == null)
            {
                Logger.LogError("Store not initialized");
                return;
            }
            
            m_IsPurchaseInProgress = true;
            m_StoreController.InitiatePurchase(productId);
        }
        
        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            m_StoreController = controller;
            m_StoreExtensionProvider = extensions;
        }
        
        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs purchaseEvent)
        {
            Logger.LogDemo($"Purchase pending - Product: {purchaseEvent.purchasedProduct.definition.id}");
            
            var product = purchaseEvent.purchasedProduct;
            ProcessPurchaseAsync(product);
            
            return PurchaseProcessingResult.Pending;
        }
        
        private async void ProcessPurchaseAsync(Product product)
        {
            try
            {
                if (!product.hasReceipt)
                {
                    Logger.LogError("Purchase has no receipt");
                    return;
                }
                
                Logger.LogDemo($"Receipt data: {product.receipt}"); // Log full receipt for debugging
                
                PlayerEconomyData playerEconomyData = await m_BindingsProvider.GemHunterBindings.HandlePurchase(
                    product.definition.id,
                    product.receipt,
                    product.transactionID
                );

                if (playerEconomyData == null)
                {
                    Logger.LogError($"Failed to process purchase - invalid server response");
                    return;
                }
                
                // Update local economy data
                m_PlayerEconomyClient.HandleEconomyUpdate(playerEconomyData);
                
                // Confirm the purchase only after successful server validation and local update
                m_StoreController.ConfirmPendingPurchase(product);
                
                Logger.LogDemo($"⚡ SuccessfullyPurchased - Product: {product.definition.id}");
                SuccessfullyPurchased?.Invoke(product.metadata.localizedTitle);
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to process purchase: {e.Message}");
                // Consider implementing retry logic or showing an error to the user
            }
            finally
            {
                m_IsPurchaseInProgress = false;
            }
        }

        private void HandleCoinPackFreePurchase()
        {
            if (m_IsPurchaseInProgress)
            {
                Logger.LogWarning("Purchase already in progress");
            }
        }
        
        public void OnInitializeFailed(InitializationFailureReason error)
        {
            OnInitializeFailed(error, null);
        }

        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            var errorMessage = $"Purchasing failed to initialize. Reason: {error}.";

            if (message != null)
            {
                errorMessage += $" More details: {message}";
            }

            Logger.LogWarning(errorMessage);
        }
        
        public void OnPurchaseFailed(Product product, PurchaseFailureDescription failureDescription)
        {
            Logger.LogWarning($"Store purchase failed: {product.definition.id} {failureDescription}");
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
        {
            Logger.LogWarning($"PurchaseFailed on {product.definition.id} for reason: {failureReason.ToString()}");
        }
    }
}