using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Unity.Services.Samples.Tests.Economy
{
    /// <summary>
    /// Seasonal Events and Battle Pass depend on the season Game Overrides (minute digit
    /// 0-2 Fall, 3-4 Winter, 5-7 Spring, 8-9 Summer). An empty event key means the
    /// overrides are not deployed in this environment.
    /// </summary>
    public class SeasonalEventsTests : EconomyIntegrationFixture
    {
        [Test]
        public async Task Server_time_is_close_to_now()
        {
            var serverMs = await UgsTestSession.CallScriptAsync<long>("SeasonalEvents_GetServerTime");

            var localMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Assert.That(Math.Abs(serverMs - localMs), Is.LessThan(5 * 60 * 1000));
        }

        [Test]
        public async Task Event_reward_is_granted_once_per_season()
        {
            var before = await Snapshot();

            var result = await Call("SeasonalEvents_GrantEventReward");

            Assert.That(result.Value<string>("eventKey"), Is.Not.Null.And.Not.Empty, "no season override is active; deploy the season .ugo files");
            var granted = result["grantedRewards"];
            Assert.IsNotEmpty(granted, result.ToString());

            var after = await Snapshot();
            foreach (var reward in granted)
            {
                var id = reward.Value<string>("id");
                Assert.Greater(after.Balance(id), before.Balance(id), $"{id} was not granted. {result}");
                // The script reports the balance after the grant, not the amount.
                Assert.AreEqual(reward.Value<long>("quantity"), after.Balance(id), $"{id} reported balance");
            }

            await ExpectScriptError(() => Call("SeasonalEvents_GrantEventReward"), "already been claimed");
            AssertUnchanged(after, await Snapshot());
        }
    }

    public class BattlePassTests : EconomyIntegrationFixture
    {
        [Test]
        public async Task Xp_unlocks_a_tier_whose_rewards_can_be_claimed_once_and_the_pass_can_be_bought()
        {
            var state = await Call("BattlePass_GetState");
            Assert.That(state["remoteConfigs"]?.Value<string>("eventKey"), Is.Not.Null.And.Not.Empty,
                "no season override is active; deploy the season .ugo files");
            Assert.IsFalse(state.Value<bool>("ownsBattlePass"));

            await ExpectScriptError(() => Call("BattlePass_PurchaseBattlePass"), "10504");

            var xp = await Call("BattlePass_GainSeasonXP", Args(("amount", 100)));
            Assert.AreEqual(1, xp.Value<int>("unlockedNewTier"), xp.ToString());

            var before = await Snapshot();
            var claim = await Call("BattlePass_ClaimTier", Args(("tierIndex", 0)));
            var after = await Snapshot();
            AssertRewardsApplied(before, after, claim["grantedRewards"]);

            await ExpectScriptError(() => Call("BattlePass_ClaimTier", Args(("tierIndex", 0))), "already claimed");

            await State.SetBalanceAsync("GEM", 10);
            before = await Snapshot();
            var purchase = await Call("BattlePass_PurchaseBattlePass");
            after = await Snapshot();
            AssertRewardsApplied(before, after, purchase["grantedRewards"], ("GEM", -10));

            state = await Call("BattlePass_GetState");
            Assert.IsTrue(state.Value<bool>("ownsBattlePass"), state.ToString());
        }

        /// <summary>
        /// Rewards are {service:"currency"|"inventory", id, quantity}; the same id may appear
        /// more than once, so deltas are summed per id before comparing.
        /// </summary>
        static void AssertRewardsApplied(EconomySnapshot before, EconomySnapshot after, JToken rewards,
            params (string currencyId, long delta)[] extraWalletDeltas)
        {
            var wallet = new Dictionary<string, long>();
            var inventory = new Dictionary<string, long>();
            foreach (var (currencyId, delta) in extraWalletDeltas)
            {
                wallet[currencyId] = delta;
            }

            foreach (var reward in rewards ?? new JArray())
            {
                var target = reward.Value<string>("service") == "inventory" ? inventory : wallet;
                var id = reward.Value<string>("id");
                target[id] = (target.TryGetValue(id, out var sum) ? sum : 0) + reward.Value<long>("quantity");
            }

            foreach (var (id, delta) in wallet)
            {
                AssertBalanceDelta(before, after, id, delta);
            }

            foreach (var (id, delta) in inventory)
            {
                AssertItemDelta(before, after, id, delta);
            }
        }
    }
}
