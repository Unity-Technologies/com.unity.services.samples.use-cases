using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Services.CloudCode;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models.Data.Player;

namespace Unity.Services.Samples.Economy
{
    // Replacement for EconomyService.Instance.PlayerBalances / PlayerInventory /
    // Purchases. Player state lives in the single Cloud Save key "player_economy"
    // owned by the SampleGameModule Cloud Code module: reads go directly to
    // Cloud Save, writes go through the module (server-authoritative), matching
    // the samples' existing "reads direct, writes via Cloud Code" pattern.
    public class PlayerEconomy
    {
        public const string PlayerEconomyCloudSaveKey = "player_economy";

        const string k_ModuleName = "SampleGameModule";

        public static PlayerEconomy Instance { get; } = new PlayerEconomy();

        PlayerEconomy() { }

        public async Task<GetBalancesResult> GetBalancesAsync(GetBalancesOptions options = null)
        {
            var snapshot = await LoadSnapshotAsync();
            return ToBalancesResult(snapshot);
        }

        public async Task<GetInventoryResult> GetInventoryAsync(GetInventoryOptions options = null)
        {
            var snapshot = await LoadSnapshotAsync();
            return ToInventoryResult(snapshot);
        }

        public async Task<PlayerBalance> IncrementBalanceAsync(string currencyId, long amount)
        {
            var snapshot = await CallModuleAsync("Economy_ModifyBalance",
                new Dictionary<string, object> { { "currencyId", currencyId }, { "delta", amount } });

            return new PlayerBalance
            {
                CurrencyId = currencyId,
                Balance = snapshot.Wallet.TryGetValue(currencyId, out var balance) ? balance : 0,
            };
        }

        public async Task<MakeVirtualPurchaseResult> MakeVirtualPurchaseAsync(string virtualPurchaseId)
        {
            await CallModuleAsync("Economy_MakeVirtualPurchase",
                new Dictionary<string, object> { { "purchaseId", virtualPurchaseId } });

            // The module applied exactly the costs and rewards defined in the
            // catalog, so the result is reconstructed from the purchase definition.
            var result = new MakeVirtualPurchaseResult();
            var definition = EconomyCatalog.Instance.GetVirtualPurchase(virtualPurchaseId);
            if (definition == null)
            {
                return result;
            }

            foreach (var cost in definition.Costs)
            {
                AddExchangeItem(result.Costs.Currency, result.Costs.Inventory, cost);
            }
            foreach (var reward in definition.Rewards)
            {
                AddExchangeItem(result.Rewards.Currency, result.Rewards.Inventory, reward);
            }

            return result;
        }

        static void AddExchangeItem(List<CurrencyExchangeItem> currency, List<InventoryExchangeItem> inventory,
            PurchaseItemQuantity itemQuantity)
        {
            var id = itemQuantity.Item.GetReferencedConfigurationItem().Id;
            if (EconomyCatalog.Instance.IsCurrency(id))
            {
                currency.Add(new CurrencyExchangeItem { Id = id, Amount = itemQuantity.Amount });
            }
            else
            {
                inventory.Add(new InventoryExchangeItem { Id = id, Amount = itemQuantity.Amount });
            }
        }

        static async Task<PlayerEconomySnapshot> LoadSnapshotAsync()
        {
            // The wallet lives in the protected access class: the client reads it, only SampleGameModule writes it.
            var savedData = await CloudSaveService.Instance.Data.Player.LoadAsync(
                new HashSet<string> { PlayerEconomyCloudSaveKey },
                new LoadOptions(new ProtectedReadAccessClassOptions()));

            if (!savedData.TryGetValue(PlayerEconomyCloudSaveKey, out var item))
            {
                return new PlayerEconomySnapshot();
            }

            return JsonConvert.DeserializeObject<PlayerEconomySnapshot>(item.Value.GetAsString()) ?? new PlayerEconomySnapshot();
        }

        static GetBalancesResult ToBalancesResult(PlayerEconomySnapshot snapshot)
        {
            var result = new GetBalancesResult();

            // Report a balance for every catalog currency (0 when absent from the
            // wallet) so HUD views refresh the same way Economy balances did.
            foreach (var currency in EconomyCatalog.Instance.GetCurrencies())
            {
                result.Balances.Add(new PlayerBalance
                {
                    CurrencyId = currency.Id,
                    Balance = snapshot.Wallet.TryGetValue(currency.Id, out var balance) ? balance : 0,
                });
            }

            foreach (var entry in snapshot.Wallet)
            {
                if (!result.Balances.Exists(playerBalance => playerBalance.CurrencyId == entry.Key))
                {
                    result.Balances.Add(new PlayerBalance { CurrencyId = entry.Key, Balance = entry.Value });
                }
            }

            return result;
        }

        static GetInventoryResult ToInventoryResult(PlayerEconomySnapshot snapshot)
        {
            var result = new GetInventoryResult();

            // Economy created one PlayersInventoryItem instance per item granted,
            // and the HUD shows one row per instance — expand counts to match.
            foreach (var entry in snapshot.Inventory)
            {
                for (var i = 0; i < entry.Value; i++)
                {
                    result.PlayersInventoryItems.Add(new PlayersInventoryItem
                    {
                        InventoryItemId = entry.Key,
                        PlayersInventoryItemId = $"{entry.Key}_{i}",
                    });
                }
            }

            return result;
        }

        static async Task<PlayerEconomySnapshot> CallModuleAsync(string function, Dictionary<string, object> args)
        {
            try
            {
                return await CloudCodeService.Instance.CallModuleEndpointAsync<PlayerEconomySnapshot>(
                    k_ModuleName, function, args);
            }
            catch (CloudCodeRateLimitedException e)
            {
                throw new EconomyRateLimitedException(e.RetryAfter,
                    $"{function} was rate limited; retry after {e.RetryAfter} seconds.", e);
            }
            catch (CloudCodeException e)
            {
                throw new EconomyException(ParseErrorStatus(e.Message), e.Message, e);
            }
        }

        // The module reports errors as structured JSON in the exception message
        // ({ status, name, message, ... }), the same shape the sample JS scripts
        // use. Extract the status so catch blocks keyed on ErrorCode keep working.
        static int ParseErrorStatus(string message)
        {
            try
            {
                var start = message.IndexOf('{');
                var end = message.LastIndexOf('}');
                if (start < 0 || end <= start)
                {
                    return 0;
                }

                var error = JsonConvert.DeserializeAnonymousType(
                    message.Substring(start, end - start + 1), new { status = 0 });
                return error?.status ?? 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        class PlayerEconomySnapshot
        {
            [JsonProperty("wallet")]
            public Dictionary<string, long> Wallet = new Dictionary<string, long>();

            [JsonProperty("inventory")]
            public Dictionary<string, long> Inventory = new Dictionary<string, long>();
        }
    }
}
