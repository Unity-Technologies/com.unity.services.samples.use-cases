using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Unity.Services.CloudSave;

namespace Unity.Services.Samples.Tests.Economy
{
    public class IdleClickerGameTests : EconomyIntegrationFixture
    {
        [Test]
        public async Task Board_starts_with_1000_water_and_a_well_costs_100()
        {
            await CallVoid("IdleClicker_Reset");
            var state = await Call("IdleClicker_GetUpdatedState");

            Assert.AreEqual(1000, state.Value<long>("currencyBalance"), state.ToString());
            Assert.AreEqual(1000, (await Snapshot()).Balance("WATER"));

            var free = FreeCells(state).First();
            var placed = await Call("IdleClicker_PlaceWell", Args(("coord", Coord(free))));
            var after = await Snapshot();

            Assert.AreEqual(placed.Value<long>("currencyBalance"), after.Balance("WATER"),
                "reported balance should match the wallet");
            Assert.Less(after.Balance("WATER"), 1000, "the well should have cost 100 WATER");
            Assert.IsTrue(placed["wells_level1"].Any(w => w.Value<int>("x") == free.x && w.Value<int>("y") == free.y),
                $"new well missing from {placed["wells_level1"]}");

            await State.SetBalanceAsync("WATER", 0);
            var next = FreeCells(placed).First();
            await ExpectScriptError(() => Call("IdleClicker_PlaceWell", Args(("coord", Coord(next)))), "10504");
        }

        static Dictionary<string, int> Coord((int x, int y) cell) => new Dictionary<string, int> { { "x", cell.x }, { "y", cell.y } };

        static IEnumerable<(int x, int y)> FreeCells(JObject state)
        {
            var occupied = new HashSet<(int, int)>();
            foreach (var key in new[] { "obstacles", "wells_level1", "wells_level2", "wells_level3", "wells_level4" })
            {
                foreach (var cell in state[key] ?? new JArray())
                {
                    occupied.Add((cell.Value<int>("x"), cell.Value<int>("y")));
                }
            }

            for (var x = 0; x < 5; x++)
            {
                for (var y = 0; y < 5; y++)
                {
                    if (!occupied.Contains((x, y)))
                    {
                        yield return (x, y);
                    }
                }
            }
        }
    }

    public class ABTestLevelDifficultyTests : EconomyIntegrationFixture
    {
        [Test]
        public async Task Levelling_up_pays_100_coins()
        {
            var before = await Snapshot();

            JObject result = null;
            for (var i = 0; i < 12 && (result == null || !result.Value<bool>("didLevelUp")); i++)
            {
                result = await Call("ABTestLevelDifficulty_GainXPAndLevelIfReady");
            }

            Assert.IsTrue(result.Value<bool>("didLevelUp"), $"no level-up after 120 XP: {result}");
            Assert.AreEqual(2, result.Value<int>("playerLevel"));
            Assert.AreEqual(0, result.Value<int>("playerXP"));
            var rewards = result["levelUpRewards"];
            Assert.AreEqual("COIN", rewards.Value<string>("currencyId"));
            Assert.AreEqual(100, rewards.Value<long>("rewardAmount"));

            var after = await Snapshot();
            AssertBalanceDelta(before, after, "COIN", 100);
            Assert.AreEqual(rewards.Value<long>("balance"), after.Balance("COIN"));
        }
    }

    public class CloudAIMiniGameTests : EconomyIntegrationFixture
    {
        [Test]
        public async Task Finishing_a_game_pays_by_outcome()
        {
            await Call("CloudAiMiniGame_GetState");
            var state = await Call("CloudAiMiniGame_StartNewGame");
            var before = await Snapshot();

            for (var moves = 0; moves < 9 && state.Value<string>("status") == "playing"; moves++)
            {
                state = await Call("CloudAiMiniGame_ValidatePlayerMoveAndRespond",
                    Args(("coord", FreeCell(state))));
            }

            Assert.IsTrue(state.Value<bool>("isGameOver"), state.ToString());
            var expected = state.Value<string>("status") switch
            {
                "playerWon" => 100,
                "draw" => 25,
                _ => 0
            };
            AssertBalanceDelta(before, await Snapshot(), "COIN", expected);

            await ExpectScriptError(
                () => Call("CloudAiMiniGame_ValidatePlayerMoveAndRespond", Args(("coord", Coord(0, 0)))),
                "Game over");
        }

        static Dictionary<string, int> Coord(int x, int y) => new Dictionary<string, int> { { "x", x }, { "y", y } };

        static Dictionary<string, int> FreeCell(JObject state)
        {
            var taken = new HashSet<(int, int)>();
            foreach (var key in new[] { "playerPieces", "aiPieces" })
            {
                foreach (var piece in state[key] ?? new JArray())
                {
                    taken.Add((piece.Value<int>("x"), piece.Value<int>("y")));
                }
            }

            for (var x = 0; x < 3; x++)
            {
                for (var y = 0; y < 3; y++)
                {
                    if (!taken.Contains((x, y)))
                    {
                        return Coord(x, y);
                    }
                }
            }

            Assert.Fail($"board is full but status is still playing: {state}");
            return null;
        }
    }

    public class StarterPackTests : EconomyIntegrationFixture
    {
        [Test]
        public async Task Starter_pack_needs_10_gems_and_can_be_bought_once()
        {
            var before = await Snapshot();
            await ExpectScriptError(() => Call("StarterPack_PurchaseStarterPack"), "10504");
            AssertUnchanged(before, await Snapshot());

            await State.SetBalanceAsync("GEM", 10);
            before = await Snapshot();
            await Call("StarterPack_PurchaseStarterPack");
            var after = await Snapshot();

            AssertBalanceDelta(before, after, "GEM", -10);
            AssertBalanceDelta(before, after, "COIN", 100);
            AssertBalanceDelta(before, after, "PEARL", 10);
            AssertItemDelta(before, after, "SWORD", 1);

            await ExpectScriptError(() => Call("StarterPack_PurchaseStarterPack"), "already been claimed");
            AssertUnchanged(after, await Snapshot());
        }
    }

    public class InGameMailboxTests : EconomyIntegrationFixture
    {
        [Test]
        public async Task Claiming_one_attachment_grants_its_gift_once()
        {
            await SaveInbox(("MESSAGE_005", "MESSAGE_005_GIFT"));
            var before = await Snapshot();

            await CallVoid("InGameMailbox_ClaimAttachment", Args(("messageId", "MESSAGE_005")));

            AssertBalanceDelta(before, await Snapshot(), "COIN", 50);
            await ExpectScriptError(
                () => CallVoid("InGameMailbox_ClaimAttachment", Args(("messageId", "MESSAGE_005"))),
                "already been claimed");
        }

        [Test]
        public async Task Claim_all_grants_every_unclaimed_gift()
        {
            await SaveInbox(("MESSAGE_008", "MESSAGE_008_GIFT"), ("MESSAGE_011", "MESSAGE_011_GIFT"));
            var before = await Snapshot();

            var result = await Call("InGameMailbox_ClaimAllAttachments");

            CollectionAssert.AreEquivalent(new[] { "MESSAGE_008_GIFT", "MESSAGE_011_GIFT" },
                result["processedTransactions"].ToObject<string[]>());

            // The script fires the grants without awaiting them, so give them a moment to land.
            var after = await WaitForState(s =>
                s.Balance("COIN") - before.Balance("COIN") >= 100 && s.Count("SWORD") - before.Count("SWORD") >= 1);
            AssertBalanceDelta(before, after, "COIN", 100);
            AssertBalanceDelta(before, after, "GEM", 50);
            AssertItemDelta(before, after, "SWORD", 1);

            await ExpectScriptError(() => Call("InGameMailbox_ClaimAllAttachments"), "no messages with unclaimed");
        }

        [Test]
        public async Task Mailbox_ForgedAttachment_PaysNothing()
        {
            // The inbox is player-written: a modified client can put any attachment on any message, or invent one.
            // MESSAGE_006 has no attachment in Remote Config, and FORGED_MESSAGE is not in MESSAGES_ALL at all.
            await SaveInbox(("MESSAGE_006", "MESSAGE_008_GIFT"), ("FORGED_MESSAGE", "MESSAGE_008_GIFT"));
            var before = await Snapshot();

            await ExpectScriptError(
                () => CallVoid("InGameMailbox_ClaimAttachment", Args(("messageId", "MESSAGE_006"))),
                "does not have an attachment");
            await ExpectScriptError(
                () => CallVoid("InGameMailbox_ClaimAttachment", Args(("messageId", "FORGED_MESSAGE"))),
                "does not have an attachment");
            await ExpectScriptError(() => Call("InGameMailbox_ClaimAllAttachments"), "no messages with unclaimed");

            AssertUnchanged(before, await Snapshot());
        }

        [Test]
        public async Task Mailbox_SameMessage_ClaimsOnce()
        {
            await SaveInbox(("MESSAGE_010", "MESSAGE_010_GIFT"));
            var before = await Snapshot();
            await CallVoid("InGameMailbox_ClaimAttachment", Args(("messageId", "MESSAGE_010")));

            // The claim marked the message claimed in the inbox; the player writes it back as unclaimed.
            await SaveInbox(("MESSAGE_010", "MESSAGE_010_GIFT"));
            await ExpectScriptError(
                () => CallVoid("InGameMailbox_ClaimAttachment", Args(("messageId", "MESSAGE_010"))),
                "already been claimed");
            await SaveInbox(("MESSAGE_010", "MESSAGE_010_GIFT"));
            await ExpectScriptError(() => Call("InGameMailbox_ClaimAllAttachments"), "no messages with unclaimed");

            AssertBalanceDelta(before, await Snapshot(), "GEM", 50);
        }

        [Test]
        public async Task Mailbox_AudienceOverride_PaysOnlyForThatAudience()
        {
            // MESSAGE_003 carries a gift only in the New Players Game Override; the module must read the same override.
            await SaveInbox(("MESSAGE_003", "MESSAGE_003_GIFT_NEW_PLAYERS"));
            var before = await Snapshot();

            await ExpectScriptError(
                () => CallVoid("InGameMailbox_ClaimAttachment", Args(("messageId", "MESSAGE_003"))),
                "does not have an attachment");
            await CallVoid("InGameMailbox_ClaimAttachment", Args(("messageId", "MESSAGE_003"), ("audience", "NewPlayers")));

            var after = await Snapshot();
            AssertBalanceDelta(before, after, "COIN", 100);
            AssertItemDelta(before, after, "SWORD", 1);
            AssertItemDelta(before, after, "SHIELD", 1);
        }

        /// <summary>Writes the client-owned inbox state the mailbox scripts read.</summary>
        static Task SaveInbox(params (string messageId, string attachment)[] messages)
        {
            var inbox = new JObject
            {
                ["messages"] = new JArray(messages.Select(m => new JObject
                {
                    ["messageId"] = m.messageId,
                    ["messageInfo"] = new JObject
                    {
                        ["title"] = m.messageId,
                        ["content"] = "integration test",
                        ["attachment"] = m.attachment,
                        ["expirationPeriod"] = "7.00:00:00"
                    },
                    ["metadata"] = new JObject
                    {
                        ["expirationDate"] = "2099-01-01T00:00:00.0000000Z",
                        ["isRead"] = false,
                        ["hasUnclaimedAttachment"] = true
                    }
                }))
            };

            return CloudSaveService.Instance.Data.Player.SaveAsync(
                new Dictionary<string, object> { { "MESSAGES_INBOX_STATE", inbox.ToString(Newtonsoft.Json.Formatting.None) } });
        }
    }

    /// <summary>
    /// A modified client can write any key in the default access class. SampleGameModule keeps its keys in the
    /// protected class, a separate namespace, so a poisoned default value must not change what the module does:
    /// each function behaves as it does for a fresh player.
    /// </summary>
    public class ModuleKeyTests : EconomyIntegrationFixture
    {
        [TestCase("player_economy")]
        [TestCase("STARTER_PACK_STATUS")]
        [TestCase("LOOT_BOX_COOLDOWN_GRANT_REWARD_TIME")]
        [TestCase("BATTLE_PASS_PURCHASED_SEASON")]
        public async Task ModuleKey_IgnoresPlayerWrittenDefaultValue(string key)
        {
            switch (key)
            {
                case "player_economy":
                {
                    var before = await Snapshot();
                    await SavePlayerWritten(key, new Dictionary<string, object>
                    {
                        { "wallet", new Dictionary<string, long> { { "COIN", 1_000_000 } } },
                        { "inventory", new Dictionary<string, long>() }
                    });

                    var result = await Call("Economy_ModifyBalance", Args(("currencyId", "COIN"), ("delta", 1)));
                    Assert.AreEqual(before.Balance("COIN") + 1, result["wallet"].Value<long>("COIN"), result.ToString());
                    break;
                }

                case "STARTER_PACK_STATUS":
                    await SavePlayerWritten(key, new Dictionary<string, object> { { "claimed", true } });
                    await State.SetBalanceAsync("GEM", 10);

                    await Call("StarterPack_PurchaseStarterPack");
                    break;

                case "LOOT_BOX_COOLDOWN_GRANT_REWARD_TIME":
                {
                    await SavePlayerWritten(key, System.DateTimeOffset.UtcNow.ToUnixTimeSeconds());

                    var status = await Call("LootBoxesWithCooldown_GetStatus");
                    Assert.IsTrue(status.Value<bool>("canGrantFlag"), status.ToString());
                    break;
                }

                case "BATTLE_PASS_PURCHASED_SEASON":
                {
                    var state = await Call("BattlePass_GetState");
                    var eventKey = state["remoteConfigs"].Value<string>("eventKey");
                    await SavePlayerWritten(key, eventKey);

                    state = await Call("BattlePass_GetState");
                    Assert.IsFalse(state.Value<bool>("ownsBattlePass"), state.ToString());
                    break;
                }

                default:
                    Assert.Fail($"no case for {key}");
                    break;
            }
        }

        static Task SavePlayerWritten(string key, object value)
            => CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object> { { key, value } });
    }
}
