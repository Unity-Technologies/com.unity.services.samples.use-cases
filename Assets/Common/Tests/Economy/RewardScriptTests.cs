using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Unity.Services.CloudCode;

namespace Unity.Services.Samples.Tests.Economy
{
    public class LootBoxesTests : EconomyIntegrationFixture
    {
        static readonly string[] k_Currencies = { "COIN", "GEM", "PEARL", "STAR" };

        [Test]
        public async Task Grant_random_currency_lands_in_wallet()
        {
            var before = await Snapshot();

            var result = await Call("LootBoxes_GrantRandomCurrency");

            var currencyId = result.Value<string>("currencyId");
            var amount = result.Value<long>("amount");
            CollectionAssert.Contains(k_Currencies, currencyId);
            Assert.That(amount, Is.InRange(1, 5));
            AssertBalanceDelta(before, await Snapshot(), currencyId, amount);
        }
    }

    public class LootBoxesWithCooldownTests : EconomyIntegrationFixture
    {
        [Test]
        public async Task Claim_grants_two_currencies_and_an_item_then_starts_cooldown()
        {
            var status = await Call("LootBoxesWithCooldown_GetStatus");
            Assert.IsTrue(status.Value<bool>("canGrantFlag"), status.ToString());

            var before = await Snapshot();
            var claim = await Call("LootBoxesWithCooldown_Claim");
            var after = await Snapshot();

            var currencyIds = claim["currencyId"].ToObject<string[]>();
            var currencyQuantities = claim["currencyQuantity"].ToObject<long[]>();
            var itemIds = claim["inventoryItemId"].ToObject<string[]>();
            var itemQuantities = claim["inventoryItemQuantity"].ToObject<long[]>();
            Assert.AreEqual(2, currencyIds.Length, claim.ToString());
            Assert.AreNotEqual(currencyIds[0], currencyIds[1], "the two granted currencies must differ");
            for (var i = 0; i < currencyIds.Length; i++)
            {
                AssertBalanceDelta(before, after, currencyIds[i], currencyQuantities[i]);
            }

            AssertItemDelta(before, after, itemIds[0], itemQuantities[0]);

            status = await Call("LootBoxesWithCooldown_GetStatus");
            Assert.IsFalse(status.Value<bool>("canGrantFlag"), status.ToString());
            Assert.Greater(status.Value<long>("grantCooldown"), 0);

            await ExpectScriptError(() => Call("LootBoxesWithCooldown_Claim"), "cooldown");
            AssertUnchanged(after, await Snapshot());
        }
    }

    public class DailyRewardsTests : EconomyIntegrationFixture
    {
        [Test]
        public async Task First_two_days_grant_and_the_same_day_cannot_be_claimed_twice()
        {
            await CallVoid("DailyRewards_ResetEvent");
            await Call("DailyRewards_GetStatus");

            var before = await Snapshot();
            var claim = await Call("DailyRewards_Claim");
            var after = await Snapshot();

            Assert.IsTrue(claim.Value<bool>("success"), claim.ToString());
            Assert.AreEqual(1, claim.Value<int>("daysClaimed"));
            AssertRewardsGranted(before, after, claim["rewardsGranted"]);

            await ExpectScriptError(() => Call("DailyRewards_Claim"), "already claimed");

            var status = await Call("DailyRewards_GetStatus");
            var secondsTillClaimable = status.Value<double>("secondsTillClaimable");
            Assert.That(secondsTillClaimable, Is.InRange(0, 10), "days are 10 seconds long in the sample config");
            await Task.Delay((int)(secondsTillClaimable * 1000) + 1500);

            before = after;
            claim = await Call("DailyRewards_Claim");
            after = await Snapshot();

            Assert.AreEqual(2, claim.Value<int>("daysClaimed"), claim.ToString());
            AssertRewardsGranted(before, after, claim["rewardsGranted"]);
        }

        static void AssertRewardsGranted(EconomySnapshot before, EconomySnapshot after, JToken rewards)
        {
            Assert.IsNotNull(rewards);
            Assert.IsNotEmpty(rewards);
            foreach (var reward in rewards)
            {
                AssertBalanceDelta(before, after, reward.Value<string>("id"), reward.Value<long>("quantity"));
            }
        }
    }

    public class CommandBatchingTests : EconomyIntegrationFixture
    {
        const string k_Red = "COMMANDBATCH_DEFEAT_RED_ENEMY";
        const string k_Blue = "COMMANDBATCH_DEFEAT_BLUE_ENEMY";
        const string k_Chest = "COMMANDBATCH_OPEN_CHEST";
        const string k_Bonus = "COMMANDBATCH_ACHIEVE_BONUS_GOAL";
        const string k_GameOver = "COMMANDBATCH_GAME_OVER";

        [Test]
        public async Task Valid_batch_grants_the_summed_rewards()
        {
            var before = await Snapshot();

            // COIN: red 5 + chest 25 + red 5 + chest 25 = 60. GEM: chest 25 + blue 5 + chest 25 = 55.
            await CallVoid("CommandBatching_ProcessBatch",
                Args(("commands", new[] { k_Red, k_Chest, k_Bonus, k_Blue, k_Red, k_Chest, k_GameOver })));

            var after = await Snapshot();
            AssertBalanceDelta(before, after, "COIN", 60);
            AssertBalanceDelta(before, after, "GEM", 55);
        }

        [Test]
        public async Task Short_batch_is_rejected_and_grants_nothing()
        {
            var before = await Snapshot();

            await ExpectScriptError(
                () => CallVoid("CommandBatching_ProcessBatch", Args(("commands", new[] { k_Red, k_GameOver }))),
                "Not enough commands");

            AssertUnchanged(before, await Snapshot());
        }
    }
}
