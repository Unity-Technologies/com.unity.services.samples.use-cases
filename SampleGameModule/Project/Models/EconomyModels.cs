using System.Collections.Generic;
using Newtonsoft.Json;

namespace SampleGameModule.Models;

/// <summary>
/// The single Cloud Save value stored under the "player_economy" key.
/// Wallet maps currency ids to balances; Inventory maps inventory item ids to counts.
/// A missing key is treated as an empty snapshot and created on first write.
/// </summary>
public class PlayerEconomySnapshot
{
    [JsonProperty("wallet")]
    public Dictionary<string, long> Wallet { get; set; } = new();

    [JsonProperty("inventory")]
    public Dictionary<string, long> Inventory { get; set; } = new();
}

/// <summary>
/// One cost or reward entry of a virtual purchase, as stored in the
/// ECONOMY_VIRTUAL_PURCHASES Remote Config key.
/// </summary>
public class PurchaseItemQuantity
{
    [JsonProperty("id")]
    public string Id { get; set; } = "";

    [JsonProperty("amount")]
    public long Amount { get; set; }
}

/// <summary>
/// A virtual purchase definition, as stored in the ECONOMY_VIRTUAL_PURCHASES
/// Remote Config key (keyed by purchase id).
/// </summary>
public class VirtualPurchaseDefinition
{
    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("costs")]
    public List<PurchaseItemQuantity> Costs { get; set; } = new();

    [JsonProperty("rewards")]
    public List<PurchaseItemQuantity> Rewards { get; set; } = new();
}
