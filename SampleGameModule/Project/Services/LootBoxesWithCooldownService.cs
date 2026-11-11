using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SampleGameModule.Infrastructure;
using SampleGameModule.Models;
using Unity.Services.CloudCode.Core;

namespace SampleGameModule.Services;

/// <summary>
/// Loot Boxes With Cooldown sample: a random bundle of two currencies and one item,
/// claimable once per 60 s cooldown tracked in Cloud Save.
/// Used by: Loot Boxes With Cooldown, CloudCodeManager.CallGetStatusEndpoint / CallClaimEndpoint.
/// </summary>
public class LootBoxesWithCooldownService
{
    const int k_CooldownSeconds = 60;
    const string k_GrantRewardTimeKey = "LOOT_BOX_COOLDOWN_GRANT_REWARD_TIME";

    static readonly string[] k_CurrencyIds = { "COIN", "GEM", "PEARL", "STAR" };
    static readonly string[] k_InventoryItemIds = { "SWORD", "SHIELD" };
    static readonly Random s_Random = new();

    readonly EconomyModuleService m_Economy;
    readonly PlayerDataStore m_Store;
    readonly ILogger<LootBoxesWithCooldownService> m_Logger;

    public LootBoxesWithCooldownService(EconomyModuleService economy, PlayerDataStore store,
        ILogger<LootBoxesWithCooldownService> logger)
    {
        m_Economy = economy;
        m_Store = store;
        m_Logger = logger;
    }

    public class StatusResult
    {
        [JsonProperty("canGrantFlag")] public bool CanGrantFlag { get; set; }
        [JsonProperty("grantCooldown")] public long GrantCooldown { get; set; }
        [JsonProperty("defaultCooldown")] public int DefaultCooldown { get; set; } = k_CooldownSeconds;
    }

    public class ClaimResult
    {
        [JsonProperty("currencyId")] public List<string> CurrencyId { get; set; } = new();
        [JsonProperty("currencyQuantity")] public List<long> CurrencyQuantity { get; set; } = new();
        [JsonProperty("inventoryItemId")] public List<string> InventoryItemId { get; set; } = new();
        [JsonProperty("inventoryItemQuantity")] public List<long> InventoryItemQuantity { get; set; } = new();
    }

    /// <summary>
    /// Used by: Loot Boxes With Cooldown sample, CloudCodeManager.CallGetStatusEndpoint.
    /// Reports whether a claim is allowed now and, if not, the remaining cooldown in seconds.
    /// </summary>
    [CloudCodeFunction("LootBoxesWithCooldown_GetStatus")]
    public async Task<StatusResult> GetStatus(IExecutionContext context)
    {
        var cooldown = await GetRemainingCooldownAsync(context, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        // If cooldown has NOT expired, return canGrantFlag: false and the remaining cooldown time in seconds
        if (cooldown > 0)
            return new StatusResult { CanGrantFlag = false, GrantCooldown = cooldown };

        // Last grant time doesn't exist or cooldown has expired - okay to claim reward now
        return new StatusResult { CanGrantFlag = true, GrantCooldown = 0 };
    }

    /// <summary>
    /// Used by: Loot Boxes With Cooldown sample, CloudCodeManager.CallClaimEndpoint.
    /// Restarts the cooldown, then grants two distinct random currencies and one random item in one economy write.
    /// </summary>
    [CloudCodeFunction("LootBoxesWithCooldown_Claim")]
    public async Task<ClaimResult> Claim(IExecutionContext context)
    {
        var epochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // If cooldown timer has not expired (using 1 for slight tolerance in case the Claim button is pressed early)
        if (await GetRemainingCooldownAsync(context, epochTime) > 1)
        {
            m_Logger.LogError("The player tried to claim a Loot Box before the cooldown timer expired.");
            throw new ModuleError(1, "CloudCodeCustomError",
                "The player tried to claim a Loot Box before the cooldown timer expired.");
        }

        // Select a random reward to grant
        var currencyId1 = PickRandomCurrencyId(null);
        var currencyQuantity1 = PickRandomCurrencyQuantity();
        var currencyId2 = PickRandomCurrencyId(currencyId1);
        var currencyQuantity2 = PickRandomCurrencyQuantity();
        var inventoryItemId = k_InventoryItemIds[s_Random.Next(k_InventoryItemIds.Length)];
        var inventoryItemQuantity = PickRandomInventoryItemQuantity();

        // Update the cooldown timer, then grant all rewards.
        await m_Store.SetAsync(context, k_GrantRewardTimeKey, epochTime);
        await m_Economy.GrantAsync(context,
            new[]
            {
                new KeyValuePair<string, long>(currencyId1, currencyQuantity1),
                new KeyValuePair<string, long>(currencyId2, currencyQuantity2),
            },
            new[] { new KeyValuePair<string, long>(inventoryItemId, inventoryItemQuantity) });

        return new ClaimResult
        {
            CurrencyId = { currencyId1, currencyId2 },
            CurrencyQuantity = { currencyQuantity1, currencyQuantity2 },
            InventoryItemId = { inventoryItemId },
            InventoryItemQuantity = { inventoryItemQuantity },
        };
    }

    /// <summary>Seconds of cooldown left, or zero or less when the player never claimed or the cooldown has run out.</summary>
    async Task<long> GetRemainingCooldownAsync(IExecutionContext context, long nowEpochTime)
    {
        var grantEpochTime = (await m_Store.GetAsync(context, k_GrantRewardTimeKey)).As<long>();
        if (grantEpochTime == 0)
            return 0;
        return k_CooldownSeconds - (nowEpochTime - grantEpochTime);
    }

    // Pick a random currency reward from the list; when it is the id to avoid, take the next one instead
    static string PickRandomCurrencyId(string? invalidId)
    {
        var i = s_Random.Next(k_CurrencyIds.Length);
        if (k_CurrencyIds[i] == invalidId)
            i = (i + 1) % k_CurrencyIds.Length;
        return k_CurrencyIds[i];
    }

    // Pick a random quantity for the currency (uses 1-5 for sample)
    static long PickRandomCurrencyQuantity() => s_Random.Next(1, 6);

    // Pick a quantity of inventory items to grant (usually 1, but about a quarter of the time 2)
    static long PickRandomInventoryItemQuantity() => s_Random.Next(1, 101) >= 75 ? 2 : 1;
}
