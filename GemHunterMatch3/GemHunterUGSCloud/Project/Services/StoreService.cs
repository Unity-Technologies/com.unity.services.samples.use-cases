using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GemHunterUGSCloud.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Core;

namespace GemHunterUGSCloud.Services;

/// <summary>
/// Grants real-money store products and the one-time free coin pack.
///
/// The client validates each purchase with Unity IAP before calling <c>HandlePurchase</c>
/// (Google Play receipts locally through <c>CrossPlatformValidator</c>; Apple receipts are
/// StoreKit 2 JWS tokens). A production game should verify the receipt again here against the
/// store's server API before granting. Replays of a transaction are refused by
/// <see cref="PlayerEconomyService.GrantPurchase"/>, so a retried or restored purchase never pays twice.
/// </summary>
public class StoreService
{
    readonly ILogger<StoreService> m_Logger;
    readonly PlayerEconomyService m_PlayerEconomyService;

    readonly int m_FreeCoinPackReward = 10;

    /// <summary>What a store product grants once the store has charged the player.</summary>
    record PurchaseReward(Dictionary<string, int> Currencies, Dictionary<string, int> Items, bool InfiniteHeart);

    static readonly Dictionary<string, PurchaseReward> s_Rewards = new()
    {
        // Bundle Packs
        {
            EconomyConstants.Products.k_BundlePack,
            new PurchaseReward(Coins(50),
                new() { { EconomyConstants.Items.k_LargeBomb, 2 }, { EconomyConstants.Items.k_ColorBonus, 3 } },
                InfiniteHeart: true)
        },
        {
            EconomyConstants.Products.k_MegaPack,
            new PurchaseReward(Coins(500),
                new() { { EconomyConstants.Items.k_LargeBomb, 4 }, { EconomyConstants.Items.k_ColorBonus, 6 } },
                InfiniteHeart: true)
        },

        // Coin Packs
        { EconomyConstants.Products.k_CoinPack100, CoinPack(100) },
        { EconomyConstants.Products.k_CoinPack500, CoinPack(500) },
        { EconomyConstants.Products.k_CoinPack1000, CoinPack(1000) },
        { EconomyConstants.Products.k_CoinPack5000, CoinPack(5000) },
        { EconomyConstants.Products.k_CoinPack10000, CoinPack(10000) }
    };

    // Unity IAP's store names, lower-cased: GooglePlay.Name, AppleAppStore.Name, FakeStore.Name.
    const string k_AppleAppStore = "appleappstore";
    static readonly HashSet<string> s_SupportedStores = new() { "googleplay", k_AppleAppStore, "fake" };

    static Dictionary<string, int> Coins(int amount) => new() { { EconomyConstants.Currencies.k_Coin, amount } };
    static PurchaseReward CoinPack(int coins) => new(Coins(coins), new(), InfiniteHeart: false);

    public StoreService(ILogger<StoreService> logger, PlayerEconomyService playerEconomyService)
    {
        m_Logger = logger;
        m_PlayerEconomyService = playerEconomyService;
    }

    [CloudCodeFunction("HandlePurchase")]
    public async Task<PlayerEconomyData> HandlePurchase(
        IExecutionContext context,
        string productId,
        string receipt,
        string transactionId)
    {
        try
        {
            if (!s_Rewards.TryGetValue(productId, out var reward))
            {
                throw new ArgumentException($"Invalid product ID: {productId}");
            }

            if (string.IsNullOrEmpty(transactionId))
            {
                throw new ArgumentException("Transaction ID is required");
            }

            // Parse receipt to get store information
            var receiptData = JsonConvert.DeserializeAnonymousType(receipt, new { Store = "", Payload = "" });
            var store = receiptData?.Store?.ToLower();
            // App Store receipts carry an empty Payload when the device has no app receipt.
            if (string.IsNullOrEmpty(store) || (store != k_AppleAppStore && string.IsNullOrEmpty(receiptData?.Payload)))
            {
                throw new ArgumentException("Invalid receipt format");
            }

            if (!s_SupportedStores.Contains(store))
            {
                throw new ArgumentException($"Unsupported store type: {store}");
            }

            m_Logger.LogInformation("Processing {Store} transaction {TransactionId} for product {ProductId}",
                store, transactionId, productId);

            var granted = await m_PlayerEconomyService.GrantPurchase(context, transactionId,
                reward.Currencies, reward.Items, reward.InfiniteHeart);
            if (!granted)
            {
                m_Logger.LogWarning("Transaction {TransactionId} was already redeemed; nothing granted again", transactionId);
            }

            return await m_PlayerEconomyService.GetPlayerEconomyData(context)
                ?? throw new InvalidOperationException("Failed to get player economy data");
        }
        catch (Exception e)
        {
            m_Logger.LogError(e, "Error processing purchase for product {ProductId}", productId);
            throw;
        }
    }

    [CloudCodeFunction("HandleFreeCoinPackPurchase")]
    public async Task<PlayerEconomyData> HandleFreeCoinPackPurchase(IExecutionContext context)
    {
        try
        {
            var economyData = await m_PlayerEconomyService.GetPlayerEconomyData(context)
                ?? throw new InvalidOperationException("Failed to get player economy data");

            if (economyData.HasPurchasedFreeCoinPack == false)
            {
                await m_PlayerEconomyService.UpdatePlayerCurrency(context, EconomyConstants.Currencies.k_Coin, m_FreeCoinPackReward);
                economyData.HasPurchasedFreeCoinPack = true;
                await m_PlayerEconomyService.MarkFreePackAsClaimed(context);
                return await m_PlayerEconomyService.GetPlayerEconomyData(context)
                    ?? throw new InvalidOperationException("Failed to get player economy data");
            }
            m_Logger.LogWarning("Free pack already purchased");
            return economyData;
        }

        catch (Exception e)
        {
            m_Logger.LogError(e, "Error processing purchase for free coin pack");
            throw;
        }
    }
}
