using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Core;

namespace SampleGameModule.Services;

/// <summary>
/// Loot Boxes sample: one random currency grant per claim, no cooldown.
/// Used by: Loot Boxes, CloudCodeManager.CallGrantRandomCurrencyEndpoint.
/// </summary>
public class LootBoxesService
{
    static readonly string[] k_CurrencyIds = { "COIN", "GEM", "PEARL", "STAR" };
    static readonly Random s_Random = new();

    readonly EconomyModuleService m_Economy;

    public LootBoxesService(EconomyModuleService economy)
    {
        m_Economy = economy;
    }

    public class GrantResult
    {
        [JsonProperty("currencyId")] public string CurrencyId { get; set; } = "";
        [JsonProperty("amount")] public long Amount { get; set; }
    }

    /// <summary>Grants 1 to 5 of a random currency and reports which.</summary>
    [CloudCodeFunction("LootBoxes_GrantRandomCurrency")]
    public async Task<GrantResult> GrantRandomCurrency(IExecutionContext context)
    {
        var currencyId = k_CurrencyIds[s_Random.Next(k_CurrencyIds.Length)];
        var amount = s_Random.Next(1, 6);
        await m_Economy.GrantAsync(context, new[] { new KeyValuePair<string, long>(currencyId, amount) });
        return new GrantResult { CurrencyId = currencyId, Amount = amount };
    }
}
