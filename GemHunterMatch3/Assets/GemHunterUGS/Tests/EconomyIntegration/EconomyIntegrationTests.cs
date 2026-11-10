using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.CloudCode.GeneratedBindings;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;
using UnityEngine.TestTools;

namespace GemHunterUGS.Tests.Economy
{
    /// <summary>
    /// Drives the deployed GemHunterUGSCloud module against live UGS, as the game does through its generated
    /// bindings, and asserts the economy the module reports back. Each fixture signs in a fresh anonymous
    /// player, so every test starts from an uninitialized economy. Runs against the environment the game uses
    /// (production) unless GHM3_TEST_ENVIRONMENT says otherwise.
    /// </summary>
    public class EconomyIntegrationTests
    {
        const string k_Coin = "COIN";
        const string k_LargeBomb = "LARGE_BOMB";
        const string k_SmallBomb = "SMALL_BOMB";
        const string k_ColorBonus = "COLOR_BONUS";
        const int k_StarterCoins = 200;
        const int k_FreeCoinPackReward = 10;
        const int k_InfiniteHeartSeconds = 6 * 60;

        GemHunterUGSCloudBindings m_Bindings;

        [UnitySetUp]
        public IEnumerator SignInFreshPlayer()
        {
            yield return Await(SignInFreshPlayerAsync());
            m_Bindings = new GemHunterUGSCloudBindings(CloudCodeService.Instance);
        }

        [UnityTest]
        public IEnumerator NewPlayer_StartsWithStarterCoinsAndItems_AndSignsInAgainAsExisting() => Await(async () =>
        {
            var first = await m_Bindings.OnSignInHandlePlayerInitialization();
            var read = await m_Bindings.GetPlayerEconomyData();
            var second = await m_Bindings.OnSignInHandlePlayerInitialization();

            Assert.That(first.IsNewPlayer, Is.True);
            Assert.That(first.EconomyData.Currencies[k_Coin], Is.EqualTo(k_StarterCoins));
            Assert.That(first.EconomyData.ItemInventory[k_LargeBomb], Is.EqualTo(1));
            Assert.That(first.EconomyData.ItemInventory[k_SmallBomb], Is.EqualTo(3));
            Assert.That(first.EconomyData.ItemInventory["HORIZONTAL_ROCKET"], Is.EqualTo(1));
            Assert.That(first.EconomyData.ItemInventory["VERTICAL_ROCKET"], Is.EqualTo(1));
            Assert.That(first.EconomyData.ItemInventory[k_ColorBonus], Is.EqualTo(1));
            Assert.That(first.EconomyData.HasPurchasedFreeCoinPack, Is.False);
            Assert.That(first.EconomyData.InfiniteHeartsExpiryTimestamp, Is.EqualTo(0));

            Assert.That(read.Currencies[k_Coin], Is.EqualTo(k_StarterCoins));
            Assert.That(read.ItemInventory, Is.EquivalentTo(first.EconomyData.ItemInventory));

            Assert.That(second.IsNewPlayer, Is.False);
            Assert.That(second.EconomyData.Currencies[k_Coin], Is.EqualTo(k_StarterCoins));
        });

        [UnityTest]
        public IEnumerator FreeCoinPack_PaysOnce() => Await(async () =>
        {
            await m_Bindings.OnSignInHandlePlayerInitialization();

            var claimed = await m_Bindings.HandleFreeCoinPackPurchase();
            var claimedAgain = await m_Bindings.HandleFreeCoinPackPurchase();

            Assert.That(claimed.Currencies[k_Coin], Is.EqualTo(k_StarterCoins + k_FreeCoinPackReward));
            Assert.That(claimed.HasPurchasedFreeCoinPack, Is.True);
            Assert.That(claimedAgain.Currencies[k_Coin], Is.EqualTo(k_StarterCoins + k_FreeCoinPackReward));
        });

        [UnityTest]
        public IEnumerator Purchase_CoinPack_GrantsOncePerTransaction() => Await(async () =>
        {
            await m_Bindings.OnSignInHandlePlayerInitialization();
            var transactionId = Guid.NewGuid().ToString();

            var granted = await m_Bindings.HandlePurchase("COIN_PACK_100", FakeReceipt(transactionId), transactionId);
            var replayed = await m_Bindings.HandlePurchase("COIN_PACK_100", FakeReceipt(transactionId), transactionId);
            var read = await m_Bindings.GetPlayerEconomyData();

            Assert.That(granted.Currencies[k_Coin], Is.EqualTo(k_StarterCoins + 100));
            Assert.That(replayed.Currencies[k_Coin], Is.EqualTo(k_StarterCoins + 100));
            Assert.That(read.Currencies[k_Coin], Is.EqualTo(k_StarterCoins + 100));
        });

        [UnityTest]
        public IEnumerator Purchase_AppStoreReceiptWithoutAppReceipt_Grants() => Await(async () =>
        {
            await m_Bindings.OnSignInHandlePlayerInitialization();
            var transactionId = Guid.NewGuid().ToString();
            // What Unity IAP sends for an App Store order on a device with no app receipt.
            var receipt = "{\"Store\":\"AppleAppStore\",\"TransactionID\":\"" + transactionId + "\",\"Payload\":\"\"}";

            var data = await m_Bindings.HandlePurchase("COIN_PACK_100", receipt, transactionId);

            Assert.That(data.Currencies[k_Coin], Is.EqualTo(k_StarterCoins + 100));
        });

        [UnityTest]
        public IEnumerator Purchase_BundlePack_GrantsCoinsItemsAndInfiniteHeart() => Await(async () =>
        {
            await m_Bindings.OnSignInHandlePlayerInitialization();
            var transactionId = Guid.NewGuid().ToString();
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var data = await m_Bindings.HandlePurchase("BUNDLE_PACK", FakeReceipt(transactionId), transactionId);

            Assert.That(data.Currencies[k_Coin], Is.EqualTo(k_StarterCoins + 50));
            Assert.That(data.ItemInventory[k_LargeBomb], Is.EqualTo(1 + 2));
            Assert.That(data.ItemInventory[k_ColorBonus], Is.EqualTo(1 + 3));
            Assert.That(data.InfiniteHeartsExpiryTimestamp, Is.InRange(now + k_InfiniteHeartSeconds - 30, now + k_InfiniteHeartSeconds + 30));
        });

        [UnityTest]
        public IEnumerator Purchase_UnknownProduct_IsRefusedAndGrantsNothing() => Await(async () =>
        {
            await m_Bindings.OnSignInHandlePlayerInitialization();
            LogAssert.ignoreFailingMessages = true;

            var transactionId = Guid.NewGuid().ToString();
            // Assert.ThrowsAsync would block the main thread the call needs to complete on, so await it by hand.
            CloudCodeException refused = null;
            try
            {
                await m_Bindings.HandlePurchase("NOT_A_PRODUCT", FakeReceipt(transactionId), transactionId);
            }
            catch (CloudCodeException e)
            {
                refused = e;
            }

            Assert.That(refused, Is.Not.Null, "unknown product was granted");

            var read = await m_Bindings.GetPlayerEconomyData();
            Assert.That(read.Currencies[k_Coin], Is.EqualTo(k_StarterCoins));
        });

        [UnityTest]
        public IEnumerator LootBox_RewardsLandInTheEconomy() => Await(async () =>
        {
            var before = (await m_Bindings.OnSignInHandlePlayerInitialization()).EconomyData;

            var rewards = await m_Bindings.ClaimLootBox();
            var after = await m_Bindings.GetPlayerEconomyData();

            Assert.That(rewards.Currencies[k_Coin], Is.InRange(100, 500));
            Assert.That(after.Currencies[k_Coin], Is.EqualTo(before.Currencies[k_Coin] + rewards.Currencies[k_Coin]));
            foreach (var item in rewards.InventoryItems)
            {
                Assert.That(after.ItemInventory[item.Key], Is.EqualTo(before.ItemInventory[item.Key] + item.Value), item.Key);
            }
        });

        [UnityTest]
        public IEnumerator DailyReward_FirstClaimPaysTheDayOneCoins() => Await(async () =>
        {
            var before = (await m_Bindings.OnSignInHandlePlayerInitialization()).EconomyData;

            var result = await m_Bindings.ClaimDailyReward();
            var after = await m_Bindings.GetPlayerEconomyData();

            // The claim answers with the event status, not the grant, so the day-one reward comes from its config.
            Assert.That(result.Success, Is.True);
            Assert.That(result.DaysClaimed, Is.EqualTo(1));
            var dayOne = result.ConfigData.DailyRewards[0];
            Assert.That(dayOne.Id, Is.EqualTo(k_Coin));
            Assert.That(dayOne.Quantity, Is.GreaterThan(0));
            Assert.That(after.Currencies[k_Coin], Is.EqualTo(before.Currencies[k_Coin] + dayOne.Quantity));
        });

        [UnityTest]
        public IEnumerator Economy_IgnoresPlayerWrittenDefaultKey() => Await(async () =>
        {
            await m_Bindings.OnSignInHandlePlayerInitialization();

            // A modified client can write any key in the default access class. The module keeps the economy in
            // the protected class, a separate namespace, so this write must not reach the balance.
            var poisoned = new Dictionary<string, object>
            {
                { "currencies", new Dictionary<string, int> { { k_Coin, 1_000_000 } } },
            };
            await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object> { { "PLAYER_ECONOMY", poisoned } });

            var read = await m_Bindings.GetPlayerEconomyData();
            Assert.That(read.Currencies[k_Coin], Is.EqualTo(k_StarterCoins));
        });

        static string FakeReceipt(string transactionId)
        {
            return "{\"Store\":\"fake\",\"TransactionID\":\"" + transactionId + "\",\"Payload\":\"{}\"}";
        }

        static async Task SignInFreshPlayerAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                var environment = Environment.GetEnvironmentVariable("GHM3_TEST_ENVIRONMENT") ?? "production";
                await UnityServices.InitializeAsync(new InitializationOptions().SetEnvironmentName(environment));
            }

            var auth = AuthenticationService.Instance;
            if (auth.IsSignedIn)
            {
                auth.SignOut(true);
            }

            auth.ClearSessionToken();
            await auth.SignInAnonymouslyAsync();
            Debug.Log($"[EconomyIntegration] project={Application.cloudProjectId} player={auth.PlayerId}");
        }

        static IEnumerator Await(Func<Task> body) => Await(body());

        static IEnumerator Await(Task task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                throw task.Exception.InnerException ?? task.Exception;
            }
        }
    }
}
