using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;
using GemHunterUGS.Scripts.Core;
using GemHunterUGS.Scripts.PlayerEconomyManagement;
using Unity.Services.CloudCode.GeneratedBindings.GemHunterUGSCloud.Models;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Purchasing.Security;
#endif

namespace GemHunterUGS.Scripts.Store
{
    /// <summary>
    /// Handles in-app purchases using Unity's IAP system (v5 store controller), managing product fetching,
    /// purchase processing, and server-side granting. This client interfaces with platform-specific stores
    /// (e.g., Google Play) and grants purchases through the cloud module.
    ///
    /// Purchase flow (two-step, mandatory in IAP v5):
    /// 1. The store charges the player and raises OnPurchasePending with a PendingOrder.
    /// 2. The receipt is validated locally where the platform supports it (Google Play), then sent to
    ///    the HandlePurchase Cloud Code function, which grants the rewards once per transaction.
    /// 3. The order is confirmed only after the module granted it; an unconfirmed order is delivered
    ///    again on the next launch, and the module ignores a transaction it already granted.
    ///
    /// Key responsibilities:
    /// - Connects to the store and fetches product definitions and pricing
    /// - Processes purchase transactions
    /// - Validates purchases and grants them through the cloud backend
    /// - Updates player economy data after successful purchases
    ///
    /// Supported product types:
    /// - Coin packs (various denominations)
    /// - Bundle packs
    /// - Mega packs
    /// </summary>
    public class StoreClient : MonoBehaviour
    {
        [SerializeField]
        private StoreUIController m_StoreUIController;

        // Product IDs should match the IDs the cloud module grants (StoreService)
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
        private StoreController m_StoreController;

        private bool m_IsStoreConnected;
        private bool m_IsPurchaseInProgress;

        public event Action<string> SuccessfullyPurchased;

        private async void Awake()
        {
            m_StoreController = UnityIAPServices.StoreController();

            // Subscribe before Connect: orders left unconfirmed by a previous session are delivered on connect.
            m_StoreController.OnStoreConnected += HandleStoreConnected;
            m_StoreController.OnStoreDisconnected += failure => Logger.LogWarning($"Purchasing failed to connect: {failure.message}");
            m_StoreController.OnProductsFetched += products => Logger.LogDemo($"Store products fetched: {products.Count}");
            m_StoreController.OnProductsFetchFailed += failure => Logger.LogWarning($"Store products fetch failed: {failure.FailureReason}");
            m_StoreController.OnPurchasesFetched += orders => Logger.LogDemo($"Store purchases fetched, {orders.PendingOrders.Count} pending");
            m_StoreController.OnPurchasesFetchFailed += failure => Logger.LogWarning($"Store purchases fetch failed: {failure.message}");
            m_StoreController.OnPurchasePending += HandlePurchasePending;
            m_StoreController.OnPurchaseConfirmed += HandlePurchaseConfirmed;
            m_StoreController.OnPurchaseFailed += HandlePurchaseFailed;
            m_StoreController.OnPurchaseDeferred += deferred => Logger.LogDemo("Purchase deferred, waiting for approval");

            await m_StoreController.Connect();
        }

        private void Start()
        {
            if (m_StoreUIController == null)
            {
                m_StoreUIController = GetComponent<StoreUIController>();
            }

            m_PlayerEconomyClient = GameSystemLocator.Get<PlayerEconomyManagerClient>();
            m_BindingsProvider = GameSystemLocator.Get<CloudBindingsProvider>();

            SetupEventHandlers();
        }

        private void HandleStoreConnected()
        {
            m_IsStoreConnected = true;

            var catalog = new CatalogProvider();
            AddConsumable(catalog, m_BundlePackProductID, "bundle_pack");
            AddConsumable(catalog, m_MegaPackProductID, "mega_pack");
            foreach (var coinPack in m_CoinPackProducts)
            {
                AddConsumable(catalog, coinPack.Value, $"coin_pack_{coinPack.Key}");
            }

            catalog.FetchProducts(products =>
            {
                m_StoreController.FetchProducts(products);
                // Delivers any order a previous session left unconfirmed through OnPurchasePending.
                m_StoreController.FetchPurchases();
            });
        }

        private static void AddConsumable(CatalogProvider catalog, string productId, string storeIdPrefix)
        {
            catalog.AddProduct(productId, ProductType.Consumable, new StoreSpecificIds
            {
                { $"{storeIdPrefix}_play", GooglePlay.Name },
                { $"{storeIdPrefix}_apple", AppleAppStore.Name }
            });
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
            if (!m_IsStoreConnected)
            {
                Logger.LogError("Store not connected");
                return;
            }

            m_IsPurchaseInProgress = true;
            m_StoreController.PurchaseProduct(productId);
        }

        private async void HandlePurchasePending(PendingOrder order)
        {
            var product = order.CartOrdered.Items().FirstOrDefault()?.Product;
            if (product == null)
            {
                Logger.LogError("Pending order has no product");
                m_IsPurchaseInProgress = false;
                return;
            }

            Logger.LogDemo($"Purchase pending - Product: {product.definition.id}");

            try
            {
                if (!IsReceiptValid(order))
                {
                    // Left unconfirmed on purpose: nothing is granted for a receipt the store did not sign.
                    return;
                }

                PlayerEconomyData playerEconomyData = await m_BindingsProvider.GemHunterBindings.HandlePurchase(
                    product.definition.id,
                    order.Info.Receipt,
                    order.Info.TransactionID
                );

                if (playerEconomyData == null)
                {
                    Logger.LogError("Failed to process purchase - invalid server response");
                    return;
                }

                // Update local economy data
                m_PlayerEconomyClient.HandleEconomyUpdate(playerEconomyData);

                // Confirm the purchase only after the server granted it and the local state is updated
                m_StoreController.ConfirmPurchase(order);

                Logger.LogDemo($"⚡ SuccessfullyPurchased - Product: {product.definition.id}");
                SuccessfullyPurchased?.Invoke(product.metadata.localizedTitle);
            }
            catch (Exception e)
            {
                // The order stays pending; the store delivers it again on the next launch and the
                // module grants a transaction only once, so a retry can never pay twice.
                Logger.LogError($"Failed to process purchase: {e.Message}");
            }
            finally
            {
                m_IsPurchaseInProgress = false;
            }
        }

        /// <summary>
        /// Google Play receipts are checked against the app's public key (GooglePlayTangle, generated from
        /// Services > In-App Purchasing > Receipt Validation Obfuscator). Apple receipts are StoreKit 2 JWS
        /// tokens that only a server can verify, and the Editor's fake store has nothing to validate.
        /// </summary>
        private static bool IsReceiptValid(PendingOrder order)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                new CrossPlatformValidator(GooglePlayTangle.Data(), Application.identifier).Validate(order.Info.Receipt);
                return true;
            }
            catch (IAPSecurityException e)
            {
                Logger.LogError($"Invalid receipt: {e.Message}");
                return false;
            }
#else
            return true;
#endif
        }

        private static void HandlePurchaseConfirmed(Order order)
        {
            switch (order)
            {
                case ConfirmedOrder:
                    Logger.LogDemo("Purchase confirmed");
                    break;
                case FailedOrder failed:
                    Logger.LogWarning($"Purchase confirmation failed: {failed.FailureReason} {failed.Details}");
                    break;
            }
        }

        private void HandlePurchaseFailed(FailedOrder failed)
        {
            var productId = failed.CartOrdered.Items().FirstOrDefault()?.Product.definition.id;
            Logger.LogWarning($"Store purchase failed: {productId} {failed.FailureReason} {failed.Details}");
            m_IsPurchaseInProgress = false;
        }

        private void HandleCoinPackFreePurchase()
        {
            if (m_IsPurchaseInProgress)
            {
                Logger.LogWarning("Purchase already in progress");
            }
        }
    }
}
