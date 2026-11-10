using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models.Data.Player;
using UnityEngine;
using UnityEngine.Networking;

namespace Unity.Services.Samples.Tests.Economy
{
    /// <summary>Wallet balances and inventory counts, keyed by currency / item id.</summary>
    public class EconomySnapshot
    {
        [JsonProperty("wallet")]
        public Dictionary<string, long> Wallet = new Dictionary<string, long>();

        [JsonProperty("inventory")]
        public Dictionary<string, long> Inventory = new Dictionary<string, long>();

        public long Balance(string currencyId) => Wallet.TryGetValue(currencyId, out var b) ? b : 0;
        public long Count(string itemId) => Inventory.TryGetValue(itemId, out var c) ? c : 0;

        public override string ToString() => JsonConvert.SerializeObject(this);
    }

    public struct PurchaseOutcome
    {
        public bool Succeeded;
        public int ErrorCode;
        public string Message;

        public override string ToString() => Succeeded ? "ok" : $"error {ErrorCode}: {Message}";
    }

    /// <summary>
    /// The player's economy state, read and seeded through whichever substrate this checkout
    /// runs on. Tests only talk to this interface, so the same assembly runs unchanged against
    /// the pre-migration project (Economy service) and the migrated one (Cloud Save + module).
    /// </summary>
    public interface IPlayerEconomyState
    {
        Task<EconomySnapshot> ReadAsync();
        Task SetBalanceAsync(string currencyId, long balance);
        Task<PurchaseOutcome> MakeVirtualPurchaseAsync(string purchaseId);
    }

    public static class PlayerEconomyState
    {
        public const int PurchaseCostsNotMetErrorCode = 10504;

#if UGS_ECONOMY_SDK
        public const string WorldName = "legacy (Economy service)";
        public static IPlayerEconomyState Create() => new EconomyServiceState();
#else
        public const string WorldName = "migrated (Cloud Save + SampleGameModule)";
        public static IPlayerEconomyState Create() => new CloudSaveModuleState();
#endif
    }

    /// <summary>Migrated world: reads the player_economy Cloud Save key, writes through the module.</summary>
    class CloudSaveModuleState : IPlayerEconomyState
    {
        const string k_Module = "SampleGameModule";
        const string k_Key = "player_economy";

        public async Task<EconomySnapshot> ReadAsync()
        {
            var data = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { k_Key },
                new LoadOptions(new ProtectedReadAccessClassOptions()));
            if (!data.TryGetValue(k_Key, out var item))
            {
                return new EconomySnapshot();
            }

            return JsonConvert.DeserializeObject<EconomySnapshot>(item.Value.GetAsString());
        }

        public Task SetBalanceAsync(string currencyId, long balance)
        {
            return CloudCodeService.Instance.CallModuleEndpointAsync<EconomySnapshot>(
                k_Module, "Economy_SetBalance",
                new Dictionary<string, object> { { "currencyId", currencyId }, { "balance", balance } });
        }

        public async Task<PurchaseOutcome> MakeVirtualPurchaseAsync(string purchaseId)
        {
            try
            {
                await CloudCodeService.Instance.CallModuleEndpointAsync<EconomySnapshot>(
                    k_Module, "Economy_MakeVirtualPurchase",
                    new Dictionary<string, object> { { "purchaseId", purchaseId } });
                return new PurchaseOutcome { Succeeded = true };
            }
            catch (CloudCodeException e)
            {
                return new PurchaseOutcome
                {
                    Succeeded = false,
                    ErrorCode = ParseStatus(e.Message),
                    Message = e.Message
                };
            }
        }

        // The module throws with a JSON body {status,name,message,...}; the SDK wraps it in the message.
        static int ParseStatus(string message)
        {
            try
            {
                var start = message.IndexOf('{');
                var end = message.LastIndexOf('}');
                if (start < 0 || end <= start) return 0;
                var body = JObject.Parse(message.Substring(start, end - start + 1));
                return body.Value<int?>("status") ?? body.Value<int?>("code") ?? 0;
            }
            catch (JsonException)
            {
                return 0;
            }
        }
    }

#if UGS_ECONOMY_SDK
    /// <summary>
    /// Legacy world: the Economy service's player REST API, called with the signed-in player's
    /// token. Deliberately not the Economy SDK, so this assembly compiles when the package is gone.
    /// </summary>
    class EconomyServiceState : IPlayerEconomyState
    {
        static string BaseUrl =>
            $"https://economy.services.api.unity.com/v2/projects/{Application.cloudProjectId}" +
            $"/players/{AuthenticationService.Instance.PlayerId}";

        public async Task<EconomySnapshot> ReadAsync()
        {
            var snapshot = new EconomySnapshot();

            var currencies = JObject.Parse(await SendAsync("GET", $"{BaseUrl}/currencies?limit=100"));
            foreach (var row in currencies["results"] ?? new JArray())
            {
                snapshot.Wallet[row.Value<string>("currencyId")] = row.Value<long>("balance");
            }

            var inventory = JObject.Parse(await SendAsync("GET", $"{BaseUrl}/inventory?limit=100"));
            foreach (var row in inventory["results"] ?? new JArray())
            {
                var id = row.Value<string>("inventoryItemId");
                snapshot.Inventory[id] = snapshot.Count(id) + 1;
            }

            return snapshot;
        }

        public Task SetBalanceAsync(string currencyId, long balance)
        {
            return SendAsync("PUT", $"{BaseUrl}/currencies/{currencyId}", new JObject { ["balance"] = balance });
        }

        public async Task<PurchaseOutcome> MakeVirtualPurchaseAsync(string purchaseId)
        {
            try
            {
                await SendAsync("POST", $"{BaseUrl}/purchases/virtual", new JObject { ["id"] = purchaseId });
                return new PurchaseOutcome { Succeeded = true };
            }
            catch (EconomyRestException e)
            {
                return new PurchaseOutcome { Succeeded = false, ErrorCode = e.Code, Message = e.Message };
            }
        }

        static async Task<string> SendAsync(string method, string url, JObject body = null)
        {
            using var request = new UnityWebRequest(url, method);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", $"Bearer {AuthenticationService.Instance.AccessToken}");
            if (body != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString(Formatting.None)));
                request.SetRequestHeader("Content-Type", "application/json");
            }

            var completion = new TaskCompletionSource<bool>();
            request.SendWebRequest().completed += _ => completion.SetResult(true);
            await completion.Task;

            var text = request.downloadHandler.text;
            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new EconomyRestException((int)request.responseCode, text);
            }

            return text;
        }

        class EconomyRestException : Exception
        {
            public int Code { get; }

            public EconomyRestException(int httpStatus, string body)
                : base($"Economy {httpStatus}: {body}")
            {
                Code = httpStatus;
                try
                {
                    Code = JObject.Parse(body).Value<int?>("code") ?? httpStatus;
                }
                catch (JsonException)
                {
                }
            }
        }
    }
#endif
}
