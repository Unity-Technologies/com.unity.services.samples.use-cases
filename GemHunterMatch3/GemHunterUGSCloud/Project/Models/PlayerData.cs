using System;
using System.Collections.Generic;
using Newtonsoft.Json;
namespace GemHunterUGSCloud.Models;

public class PlayerData
{
    [JsonProperty("displayName")]
    public string? DisplayName { get; set; }
    
    [JsonProperty("hearts")]
    public int Hearts { get; set; }
    
    [JsonProperty("stars")]
    public int Stars { get; set; }
    
    [JsonProperty("giftHearts")]
    public int GiftHearts { get; set; }
    
    [JsonProperty("gameAreasData")]
    public List<AreaData>? GameAreasData { get; set; }
    
    [JsonProperty("currentArea")]
    public AreaData? CurrentArea { get; set; }
    
    [JsonProperty("hasInfiniteHeartEffectActive")]
    public bool HasInfiniteHeartEffectActive { get; set; }

    public PlayerData(string displayName)
    {
        DisplayName = displayName;
        
        GameAreasData = new List<AreaData>();
    }
    
    public PlayerData() : this("") { }
}

public class ProfilePicture
{
    [JsonProperty("type")]
    public string? Type { get; set; } = "pre-made";
    [JsonProperty("imageData")]
    public string? ImageData { get; set; }
    [JsonProperty("imageId")]
    public int ImageId { get; set; }
}

public class ProfilePictureChangeRequest
{
    public string? Type { get; set; }
    public string? ImageData { get; set; }
    public int ImageId { get; set; }
}

/// <summary>
/// Consolidated response for player initialization that includes all necessary data
/// to set up the player's game state in a single call.
/// </summary>
public class PlayerInitializationResponse
{
    [JsonProperty("playerData")]
    public PlayerData PlayerData { get; set; } = new PlayerData();

    [JsonProperty("economyData")]
    public PlayerEconomyData EconomyData { get; set; } = new PlayerEconomyData();

    [JsonProperty("profilePicture")]
    public ProfilePicture? ProfilePicture { get; set; }

    [JsonProperty("isNewPlayer")]
    public bool IsNewPlayer { get; set; }

    [JsonProperty("initializationTimestamp")]
    public long InitializationTimestamp { get; set; }
}

/// <summary>
/// Lightweight response for when only basic player data is needed
/// (used for connectivity sync, simple updates, etc.)
/// </summary>
public class PlayerDataSyncResponse
{
    [JsonProperty("playerData")]
    public PlayerData PlayerData { get; set; } = new PlayerData();

    [JsonProperty("economyData")]
    public PlayerEconomyData EconomyData { get; set; } = new PlayerEconomyData();

    [JsonProperty("lastUpdateTimestamp")]
    public long LastUpdateTimestamp { get; set; }
}