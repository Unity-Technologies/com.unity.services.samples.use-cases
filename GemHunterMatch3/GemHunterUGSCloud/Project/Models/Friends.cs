using System;
using System.Collections.Generic;
using Newtonsoft.Json;
namespace GemHunterUGSCloud.Models;

public class Player
{
    [JsonProperty("playerId")]
    public string? PlayerId { get; set; }
    
    [JsonProperty("displayName")]
    public string? DisplayName { get; set; }
    
    [JsonProperty("playerPortrait")]
    public ProfilePicture? PlayerPortrait { get; set; }
}

public class PlayerGifts
{
    [JsonProperty("heartsReceived")]
    public int GiftedHearts { get; set; } = 1;
    
    [JsonProperty("fromPlayerIdsAtTimestamp")]
    public Dictionary<string, long> FromPlayerIdsAtTimestamp { get; set; } = new();
}