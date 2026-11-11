using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GemHunterUGSCloud.Models;
using GemHunterUGSCloud.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Unity.Services.CloudCode.Shared;
using Xunit;

namespace GemHunterUGSCloud.Tests;

public class PlayerEconomyServiceTests
{
    const string k_Coin = EconomyConstants.Currencies.k_Coin;

    readonly CloudSaveFake m_CloudSave = new();
    readonly PlayerEconomyService m_Service;

    public PlayerEconomyServiceTests()
    {
        m_Service = new PlayerEconomyService(NullLogger<PlayerEconomyService>.Instance, m_CloudSave.GameApiClient);
    }

    [Fact]
    public async Task InitializeNewPlayerEconomy_WritesStarterWalletAndItemsInOneUnconditionalWrite()
    {
        await m_Service.InitializeNewPlayerEconomy(m_CloudSave.Context.Object);

        var write = Assert.Single(m_CloudSave.Writes);
        Assert.Equal(PlayerEconomyService.k_CloudSaveKey, write.Key);
        Assert.Null(write.WriteLock);

        var record = m_CloudSave.StoredRecord!;
        Assert.Equal(200, record.Currencies[k_Coin]);
        Assert.Equal(1, record.Items[EconomyConstants.Items.k_LargeBomb]);
        Assert.Equal(3, record.Items[EconomyConstants.Items.k_SmallBomb]);
        Assert.Equal(1, record.Items[EconomyConstants.Items.k_HorizontalRocket]);
        Assert.Equal(1, record.Items[EconomyConstants.Items.k_VerticalRocket]);
        Assert.Equal(1, record.Items[EconomyConstants.Items.k_ColorBonus]);
    }

    [Fact]
    public async Task GetPlayerEconomyData_MissingRecord_ReadsAsEmptyWellFormedEconomy()
    {
        var data = (await m_Service.GetPlayerEconomyData(m_CloudSave.Context.Object))!;

        Assert.Equal(0, data.Currencies[k_Coin]);
        Assert.All(PlayerEconomyRecord.RequiredItemIds, id => Assert.Equal(0, data.ItemInventory[id]));
        Assert.False(data.HasPurchasedFreeCoinPack);
        Assert.Equal(0, data.InfiniteHeartsExpiryTimestamp);
        Assert.Empty(m_CloudSave.Writes);
    }

    [Fact]
    public async Task GetPlayerEconomyData_MapsStoredRecord()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        m_CloudSave.Store(new PlayerEconomyRecord
        {
            Currencies = { [k_Coin] = 42 },
            Items = { [EconomyConstants.Items.k_LargeBomb] = 7 },
            FreeCoinPackClaimedAt = now - 100,
            InfiniteHeartActivatedAt = now - 60,
        });

        var data = (await m_Service.GetPlayerEconomyData(m_CloudSave.Context.Object))!;

        Assert.Equal(42, data.Currencies[k_Coin]);
        Assert.Equal(7, data.ItemInventory[EconomyConstants.Items.k_LargeBomb]);
        Assert.Equal(0, data.ItemInventory[EconomyConstants.Items.k_SmallBomb]);
        Assert.True(data.HasPurchasedFreeCoinPack);
        Assert.Equal(now - 60 + 6 * 60, data.InfiniteHeartsExpiryTimestamp);
    }

    [Fact]
    public async Task GetPlayerEconomyData_ExpiredInfiniteHeart_ReportsZero()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        m_CloudSave.Store(new PlayerEconomyRecord { InfiniteHeartActivatedAt = now - 7 * 60 });

        var data = (await m_Service.GetPlayerEconomyData(m_CloudSave.Context.Object))!;

        Assert.Equal(0, data.InfiniteHeartsExpiryTimestamp);
    }

    [Fact]
    public async Task UpdatePlayerCurrency_AddsUnderTheWriteLockOfTheRead()
    {
        m_CloudSave.Store(new PlayerEconomyRecord { Currencies = { [k_Coin] = 10 } });

        var balance = await m_Service.UpdatePlayerCurrency(m_CloudSave.Context.Object, k_Coin, 25);

        Assert.Equal(35, balance);
        Assert.Equal(35, m_CloudSave.StoredRecord!.Currencies[k_Coin]);
        Assert.Equal("lock1", Assert.Single(m_CloudSave.Writes).WriteLock);
    }

    [Fact]
    public async Task UpdatePlayerCurrency_Spend_ReducesBalance()
    {
        m_CloudSave.Store(new PlayerEconomyRecord { Currencies = { [k_Coin] = 10 } });

        var balance = await m_Service.UpdatePlayerCurrency(m_CloudSave.Context.Object, k_Coin, -10);

        Assert.Equal(0, balance);
    }

    [Fact]
    public async Task UpdatePlayerCurrency_BelowZero_ThrowsAndWritesNothing()
    {
        m_CloudSave.Store(new PlayerEconomyRecord { Currencies = { [k_Coin] = 10 } });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => m_Service.UpdatePlayerCurrency(m_CloudSave.Context.Object, k_Coin, -11));

        Assert.Empty(m_CloudSave.Writes);
    }

    [Fact]
    public async Task UpdateInventoryItem_AddsToExistingSlot()
    {
        m_CloudSave.Store(new PlayerEconomyRecord { Items = { [EconomyConstants.Items.k_SmallBomb] = 2 } });

        await m_Service.UpdateInventoryItem(m_CloudSave.Context.Object, EconomyConstants.Items.k_SmallBomb, 3);

        Assert.Equal(5, m_CloudSave.StoredRecord!.Items[EconomyConstants.Items.k_SmallBomb]);
    }

    [Fact]
    public async Task UpdateInventoryItem_NegativeAmount_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => m_Service.UpdateInventoryItem(m_CloudSave.Context.Object, EconomyConstants.Items.k_SmallBomb, -1));
        await Assert.ThrowsAsync<ArgumentException>(
            () => m_Service.UpdateInventoryItem(m_CloudSave.Context.Object, "", 1));

        Assert.Empty(m_CloudSave.Writes);
    }

    [Fact]
    public async Task EnsurePlayerHasRequiredInventoryItems_AddsMissingSlotsOnce()
    {
        m_CloudSave.Store(new PlayerEconomyRecord { Items = new Dictionary<string, int> { [EconomyConstants.Items.k_LargeBomb] = 4 } });
        var data = (await m_Service.GetPlayerEconomyData(m_CloudSave.Context.Object))!;
        data.ItemInventory.Remove(EconomyConstants.Items.k_ColorBonus);

        var added = await m_Service.EnsurePlayerHasRequiredInventoryItems(m_CloudSave.Context.Object, data);
        var addedAgain = await m_Service.EnsurePlayerHasRequiredInventoryItems(m_CloudSave.Context.Object,
            (await m_Service.GetPlayerEconomyData(m_CloudSave.Context.Object))!);

        Assert.True(added);
        Assert.False(addedAgain);
        Assert.Single(m_CloudSave.Writes);
        Assert.Equal(4, m_CloudSave.StoredRecord!.Items[EconomyConstants.Items.k_LargeBomb]);
        Assert.Equal(0, m_CloudSave.StoredRecord!.Items[EconomyConstants.Items.k_ColorBonus]);
    }

    [Fact]
    public async Task GrantPurchase_GrantsEverythingInOneWriteAndRemembersTheTransaction()
    {
        m_CloudSave.Store(new PlayerEconomyRecord { Currencies = { [k_Coin] = 1 } });
        var before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var granted = await m_Service.GrantPurchase(m_CloudSave.Context.Object, "tx-1",
            new Dictionary<string, int> { [k_Coin] = 50 },
            new Dictionary<string, int> { [EconomyConstants.Items.k_LargeBomb] = 2, [EconomyConstants.Items.k_ColorBonus] = 3 },
            infiniteHeart: true);

        Assert.True(granted);
        Assert.Single(m_CloudSave.Writes);
        var record = m_CloudSave.StoredRecord!;
        Assert.Equal(51, record.Currencies[k_Coin]);
        Assert.Equal(2, record.Items[EconomyConstants.Items.k_LargeBomb]);
        Assert.Equal(3, record.Items[EconomyConstants.Items.k_ColorBonus]);
        Assert.InRange(record.InfiniteHeartActivatedAt, before, before + 5);
        Assert.Equal(new[] { "tx-1" }, record.RedeemedTransactionIds);
    }

    [Fact]
    public async Task GrantPurchase_SameTransactionTwice_GrantsOnce()
    {
        var coins = new Dictionary<string, int> { [k_Coin] = 100 };
        var noItems = new Dictionary<string, int>();

        var first = await m_Service.GrantPurchase(m_CloudSave.Context.Object, "tx-1", coins, noItems, false);
        var replay = await m_Service.GrantPurchase(m_CloudSave.Context.Object, "tx-1", coins, noItems, false);
        var other = await m_Service.GrantPurchase(m_CloudSave.Context.Object, "tx-2", coins, noItems, false);

        Assert.True(first);
        Assert.False(replay);
        Assert.True(other);
        Assert.Equal(200, m_CloudSave.StoredRecord!.Currencies[k_Coin]);
    }

    [Fact]
    public async Task FreePack_ClaimedFlagRoundTrips()
    {
        Assert.False(await m_Service.HasPlayerClaimedFreePack(m_CloudSave.Context.Object));

        await m_Service.MarkFreePackAsClaimed(m_CloudSave.Context.Object);

        Assert.True(await m_Service.HasPlayerClaimedFreePack(m_CloudSave.Context.Object));
        Assert.True((await m_Service.GetPlayerEconomyData(m_CloudSave.Context.Object))!.HasPurchasedFreeCoinPack);
    }

    [Fact]
    public async Task WriteConflict_RetriesOnFreshDataSoNoUpdateIsLost()
    {
        m_CloudSave.Store(new PlayerEconomyRecord { Currencies = { [k_Coin] = 10 } });
        m_CloudSave.BeforeWrite = () =>
        {
            // Another call lands between this call's read and its write, invalidating the lock it read.
            m_CloudSave.BeforeWrite = null;
            m_CloudSave.StoreConcurrently(r => r.Currencies[k_Coin] += 100);
        };

        var balance = await m_Service.UpdatePlayerCurrency(m_CloudSave.Context.Object, k_Coin, 5);

        Assert.Equal(115, balance);
        Assert.Equal(115, m_CloudSave.StoredRecord!.Currencies[k_Coin]);
        Assert.Single(m_CloudSave.Writes);
    }

    [Fact]
    public async Task WriteConflict_ExhaustsRetries_Throws()
    {
        m_CloudSave.ConflictsToInject = 3;

        await Assert.ThrowsAsync<ApiException>(
            () => m_Service.UpdatePlayerCurrency(m_CloudSave.Context.Object, k_Coin, 5));
    }
}
