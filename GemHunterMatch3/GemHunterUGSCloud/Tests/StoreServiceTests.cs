using System;
using System.Threading.Tasks;
using GemHunterUGSCloud.Models;
using GemHunterUGSCloud.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GemHunterUGSCloud.Tests;

public class StoreServiceTests
{
    const string k_Coin = EconomyConstants.Currencies.k_Coin;
    const string k_FakeReceipt = "{\"Store\":\"fake\",\"TransactionID\":\"tx-1\",\"Payload\":\"{}\"}";

    readonly CloudSaveFake m_CloudSave = new();
    readonly StoreService m_Service;

    public StoreServiceTests()
    {
        var economy = new PlayerEconomyService(NullLogger<PlayerEconomyService>.Instance, m_CloudSave.GameApiClient);
        m_Service = new StoreService(NullLogger<StoreService>.Instance, economy);
        m_CloudSave.Store(new PlayerEconomyRecord { Currencies = { [k_Coin] = 10 } });
    }

    [Fact]
    public async Task HandlePurchase_CoinPack_GrantsCoinsAndReturnsTheEconomy()
    {
        var data = await m_Service.HandlePurchase(m_CloudSave.Context.Object, EconomyConstants.Products.k_CoinPack500, k_FakeReceipt, "tx-1");

        Assert.Equal(510, data.Currencies[k_Coin]);
        Assert.Equal(510, m_CloudSave.StoredRecord!.Currencies[k_Coin]);
    }

    [Fact]
    public async Task HandlePurchase_BundlePack_GrantsCoinsItemsAndInfiniteHeartInOneWrite()
    {
        var data = await m_Service.HandlePurchase(m_CloudSave.Context.Object, EconomyConstants.Products.k_BundlePack, k_FakeReceipt, "tx-1");

        Assert.Single(m_CloudSave.Writes);
        Assert.Equal(60, data.Currencies[k_Coin]);
        Assert.Equal(2, data.ItemInventory[EconomyConstants.Items.k_LargeBomb]);
        Assert.Equal(3, data.ItemInventory[EconomyConstants.Items.k_ColorBonus]);
        Assert.InRange(data.InfiniteHeartsExpiryTimestamp - DateTimeOffset.UtcNow.ToUnixTimeSeconds(), 6 * 60 - 5, 6 * 60);
    }

    [Fact]
    public async Task HandlePurchase_SameTransactionAgain_ReturnsTheEconomyWithoutGrantingTwice()
    {
        await m_Service.HandlePurchase(m_CloudSave.Context.Object, EconomyConstants.Products.k_CoinPack100, k_FakeReceipt, "tx-1");
        var data = await m_Service.HandlePurchase(m_CloudSave.Context.Object, EconomyConstants.Products.k_CoinPack100, k_FakeReceipt, "tx-1");

        Assert.Equal(110, data.Currencies[k_Coin]);
        Assert.Single(m_CloudSave.Writes);
    }

    // Unity IAP labels App Store receipts "AppleAppStore", and sends an empty Payload when the device has no app receipt.
    [Theory]
    [InlineData("{\"Store\":\"AppleAppStore\",\"TransactionID\":\"tx-1\",\"Payload\":\"MIIT...\"}")]
    [InlineData("{\"Store\":\"AppleAppStore\",\"TransactionID\":\"tx-1\",\"Payload\":\"\"}")]
    public async Task HandlePurchase_AppStoreReceipt_GrantsTheProduct(string receipt)
    {
        var data = await m_Service.HandlePurchase(m_CloudSave.Context.Object, EconomyConstants.Products.k_CoinPack500, receipt, "tx-1");

        Assert.Equal(510, data.Currencies[k_Coin]);
    }

    [Theory]
    [InlineData("NOT_A_PRODUCT", k_FakeReceipt, "tx-1")]
    [InlineData(EconomyConstants.Products.k_CoinPack100, "{\"Store\":\"\",\"Payload\":\"\"}", "tx-1")]
    [InlineData(EconomyConstants.Products.k_CoinPack100, "{\"Store\":\"GooglePlay\",\"Payload\":\"\"}", "tx-1")]
    [InlineData(EconomyConstants.Products.k_CoinPack100, "{\"Store\":\"steam\",\"Payload\":\"x\"}", "tx-1")]
    [InlineData(EconomyConstants.Products.k_CoinPack100, k_FakeReceipt, "")]
    public async Task HandlePurchase_InvalidRequest_ThrowsAndGrantsNothing(string productId, string receipt, string transactionId)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => m_Service.HandlePurchase(m_CloudSave.Context.Object, productId, receipt, transactionId));

        Assert.Empty(m_CloudSave.Writes);
    }

    [Fact]
    public async Task HandleFreeCoinPackPurchase_GrantsTenCoinsOnce()
    {
        var first = await m_Service.HandleFreeCoinPackPurchase(m_CloudSave.Context.Object);
        var second = await m_Service.HandleFreeCoinPackPurchase(m_CloudSave.Context.Object);

        Assert.Equal(20, first.Currencies[k_Coin]);
        Assert.True(first.HasPurchasedFreeCoinPack);
        Assert.Equal(20, second.Currencies[k_Coin]);
        Assert.True(second.HasPurchasedFreeCoinPack);
    }
}
