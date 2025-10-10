using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GemHunterUGSCloud.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.Economy.Model;

namespace GemHunterUGSCloud.Services;

public class StoreService
{
    private readonly ILogger<StoreService> m_Logger;
    private IGameApiClient m_GameApiClient;
    private readonly PlayerEconomyService m_PlayerEconomyService;

    private readonly int m_FreeCoinPackReward = 10;
    
    private readonly record struct BundleReward(int LargeBombs, int ColorBonuses, bool InfiniteHeart, int Coins);
    private readonly record struct CoinReward(int Coins);

    private enum ProductType { Bundle, CoinPack }
        
    // Using a dictionary of tuples to identify reward type and data
    private static readonly Dictionary<string, (ProductType Type, object Data)> s_Rewards = new()
    {
        // Bundle Packs
        { 
            EconomyConstants.Products.k_BundlePack, 
            (ProductType.Bundle, new BundleReward(LargeBombs: 2, ColorBonuses: 3, InfiniteHeart: true, Coins: 50))
        },
        { 
            EconomyConstants.Products.k_MegaPack, 
            (ProductType.Bundle, new BundleReward(LargeBombs: 4, ColorBonuses: 6, InfiniteHeart: true, Coins: 500))
        },
            
        // Coin Packs
        { EconomyConstants.Products.k_CoinPack100, (ProductType.CoinPack, new CoinReward(100)) },
        { EconomyConstants.Products.k_CoinPack500, (ProductType.CoinPack, new CoinReward(500)) },
        { EconomyConstants.Products.k_CoinPack1000, (ProductType.CoinPack, new CoinReward(1000)) },
        { EconomyConstants.Products.k_CoinPack5000, (ProductType.CoinPack, new CoinReward(5000)) },
        { EconomyConstants.Products.k_CoinPack10000, (ProductType.CoinPack, new CoinReward(10000)) }
    };
    
    public StoreService(ILogger<StoreService> logger, IGameApiClient gameApiClient, PlayerEconomyService playerEconomyService)
    {
        m_Logger = logger;
        m_GameApiClient = gameApiClient;
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
            
            // Parse receipt to get store information
            var receiptData = JsonConvert.DeserializeAnonymousType(receipt, new { Store = "", Payload = "" });
            var store = receiptData?.Store?.ToLower();
            var payload = receiptData?.Payload;
            
            if (string.IsNullOrEmpty(store) || string.IsNullOrEmpty(payload))
            {
                throw new ArgumentException("Invalid receipt format");
            }
            
            m_Logger.LogInformation("Processing transaction {TransactionId} for product {ProductId}", 
                transactionId, productId);
            
            try 
            {
                switch(store.ToLower())
                {
                    case "googleplay":
                        var googleRequest = new PlayerPurchaseGoogleplaystoreRequest
                        {
                            Id = productId,
                            PurchaseData = receipt,
                            PurchaseDataSignature = transactionId
                        };
                        await m_GameApiClient.EconomyPurchases.RedeemGooglePlayPurchaseAsync(
                            context,
                            context.AccessToken,
                            context.ProjectId,
                            context.PlayerId,
                            googleRequest);
                        break;

                    case "apple":
                        var appleRequest = new PlayerPurchaseAppleappstoreRequest
                        {
                            Id = productId,
                            Receipt = receipt
                        };
                        await m_GameApiClient.EconomyPurchases.RedeemAppleAppStorePurchaseAsync(
                            context,
                            context.AccessToken,
                            context.ProjectId,
                            context.PlayerId,
                            appleRequest);
                        break;
                    
                    case "fake":
                        m_Logger.LogInformation("Using fake store - skipping receipt validation");
                        break;

                    default:
                        throw new ArgumentException($"Unsupported store type: {store}");
                }
            }
            catch (Exception e) when (e.Message.Contains("purchase already redeemed"))
            {
                // If Economy service says this receipt was already redeemed, stop here
                m_Logger.LogWarning("Receipt already redeemed. Possible duplicate purchase attempt.");
                throw;
            }
            await GrantRewards(context, reward.Type, reward.Data);
            
            return await m_PlayerEconomyService.GetPlayerEconomyData(context) 
                ?? throw new InvalidOperationException("Failed to get player economy data");
        }
        catch (Exception e)
        {
            m_Logger.LogError(e, "Error processing purchase for product {ProductId}", productId);
            throw;
        }
    }
    
    private async Task GrantRewards(IExecutionContext context, ProductType type, object rewardData)
    {
        switch (type)
        {
            case ProductType.Bundle when rewardData is BundleReward bundle:
                var bundleTasks = new List<Task>
                {
                    m_PlayerEconomyService.CleanupZeroAmountItems(context),
                    m_PlayerEconomyService.UpdateInventoryItem(context, EconomyConstants.Items.k_LargeBomb, bundle.LargeBombs),
                    m_PlayerEconomyService.UpdateInventoryItem(context, EconomyConstants.Items.k_ColorBonus, bundle.ColorBonuses),
                    m_PlayerEconomyService.UpdatePlayerCurrency(context, EconomyConstants.Currencies.k_Coin, bundle.Coins)
                };

                if (bundle.InfiniteHeart)
                {
                    bundleTasks.Add(m_PlayerEconomyService.AddInfiniteHeart(context, EconomyConstants.Items.k_InfiniteHeart));
                }

                await Task.WhenAll(bundleTasks);
                break;

            case ProductType.CoinPack when rewardData is CoinReward coins:
                await Task.WhenAll(
                    m_PlayerEconomyService.CleanupZeroAmountItems(context),
                    m_PlayerEconomyService.UpdatePlayerCurrency(context, EconomyConstants.Currencies.k_Coin, coins.Coins)
                );
                break;

            default:
                throw new ArgumentException($"Unknown reward type: {type}");
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
