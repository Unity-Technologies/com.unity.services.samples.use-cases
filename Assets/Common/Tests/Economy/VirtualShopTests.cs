using System.Threading.Tasks;
using NUnit.Framework;

namespace Unity.Services.Samples.Tests.Economy
{
    /// <summary>
    /// Virtual Shop has no Cloud Code script: the client purchases straight through the
    /// substrate. These exercise that path plus the seed/read primitives every other
    /// fixture relies on.
    /// </summary>
    public class VirtualShopTests : EconomyIntegrationFixture
    {
        [Test]
        public async Task Set_balance_then_read_roundtrips()
        {
            await State.SetBalanceAsync("COIN", 42);
            await State.SetBalanceAsync("GEM", 7);

            var snapshot = await Snapshot();

            Assert.AreEqual(42, snapshot.Balance("COIN"));
            Assert.AreEqual(7, snapshot.Balance("GEM"));
        }

        [Test]
        public async Task Buying_coins_for_gems_moves_both_balances()
        {
            await State.SetBalanceAsync("GEM", 7);
            var before = await Snapshot();

            var outcome = await TryPurchase("VIRTUAL_SHOP_100_COIN_FOR_GEMS");

            Assert.IsTrue(outcome.Succeeded, outcome.ToString());
            var after = await Snapshot();
            AssertBalanceDelta(before, after, "GEM", -7);
            AssertBalanceDelta(before, after, "COIN", 100);
        }

        [Test]
        public async Task Buying_shields_for_coins_adds_inventory()
        {
            await State.SetBalanceAsync("COIN", 15);
            var before = await Snapshot();

            var outcome = await TryPurchase("VIRTUAL_SHOP_2_SHIELD_FOR_COINS");

            Assert.IsTrue(outcome.Succeeded, outcome.ToString());
            var after = await Snapshot();
            AssertBalanceDelta(before, after, "COIN", -15);
            AssertItemDelta(before, after, "SHIELD", 2);
        }

        [Test]
        public async Task Purchase_without_funds_fails_with_costs_not_met()
        {
            await State.SetBalanceAsync("GEM", 24);
            var before = await Snapshot();

            var outcome = await TryPurchase("VIRTUAL_SHOP_1000_COIN_FOR_GEMS");

            Assert.IsFalse(outcome.Succeeded, "purchase should fail with 24 GEM against a 25 GEM cost");
            Assert.AreEqual(PlayerEconomyState.PurchaseCostsNotMetErrorCode, outcome.ErrorCode, outcome.Message);
            AssertUnchanged(before, await Snapshot());
        }

        [Test]
        public async Task Unknown_purchase_id_fails_and_changes_nothing()
        {
            var before = await Snapshot();

            var outcome = await TryPurchase("NOT_A_PURCHASE");

            Assert.IsFalse(outcome.Succeeded);
            AssertUnchanged(before, await Snapshot());
        }
    }
}
