using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace GemHunterUGSCloud.Models;

/// <summary>
/// The player's economy as stored in Cloud Save, under <c>PlayerEconomyService.k_CloudSaveKey</c>.
/// A fresh record carries a zero balance and a zero slot for every required item, so a player
/// whose record is missing still reads as a well-formed economy. Timestamps are Unix seconds; 0 means never.
/// </summary>
public class PlayerEconomyRecord
{
    /// <summary>Every player gets a slot for these items. If you add new items later, add them here.</summary>
    public static readonly string[] RequiredItemIds =
    {
        EconomyConstants.Items.k_LargeBomb,
        EconomyConstants.Items.k_SmallBomb,
        EconomyConstants.Items.k_HorizontalRocket,
        EconomyConstants.Items.k_VerticalRocket,
        EconomyConstants.Items.k_ColorBonus,
    };

    [JsonProperty("currencies")]
    public Dictionary<string, int> Currencies { get; set; } = new() { { EconomyConstants.Currencies.k_Coin, 0 } };

    [JsonProperty("items")]
    public Dictionary<string, int> Items { get; set; } = RequiredItemIds.ToDictionary(id => id, _ => 0);

    [JsonProperty("infiniteHeartActivatedAt")]
    public long InfiniteHeartActivatedAt { get; set; }

    [JsonProperty("freeCoinPackClaimedAt")]
    public long FreeCoinPackClaimedAt { get; set; }

    /// <summary>Store transaction ids already granted, so a retried or restored purchase never pays twice.</summary>
    [JsonProperty("redeemedTransactionIds")]
    public List<string> RedeemedTransactionIds { get; set; } = new();
}
