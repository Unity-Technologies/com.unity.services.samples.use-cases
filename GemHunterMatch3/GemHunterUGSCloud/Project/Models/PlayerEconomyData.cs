using System.Collections.Generic;
using Newtonsoft.Json;
namespace GemHunterUGSCloud.Models;

public class PlayerEconomyData
{
    [JsonProperty("currencies")]
    public Dictionary<string, int> Currencies { get; set; } = new Dictionary<string, int>();
        
    [JsonProperty("itemInventory")]
    public Dictionary<string, int> ItemInventory { get; set; } = new Dictionary<string, int>();
    
    [JsonProperty("hasPurchasedFreeCoinPack")]
    public bool HasPurchasedFreeCoinPack { get; set; } = false;
    
    [JsonProperty("infiniteHeartsExpiryTimestamp")]
    public long InfiniteHeartsExpiryTimestamp { get; set; } = 0;
}
