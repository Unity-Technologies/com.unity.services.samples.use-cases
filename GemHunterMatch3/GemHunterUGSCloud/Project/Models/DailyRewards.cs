using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
namespace GemHunterUGSCloud.Models;


/// <summary>
/// Represents the response data returned to the client for both status checks and claim operations.
/// Contains information about the current state of daily rewards, timers, and any rewards that were granted.
/// </summary>
public class DailyRewardsResult
{
    public bool Success { get; set; }
    public bool FirstVisit { get; set; }
    public int DaysRemaining { get; set; }
    public long SecondsTillClaimable { get; set; }
    public long SecondsTillNextDay { get; set; }
    public bool IsStarted { get; set; }
    public bool IsEnded { get; set; }
    public int DaysClaimed { get; set; }
    public List<DailyReward> RewardsGranted { get; set; }
    public ConfigData ConfigData { get; set; }
    
    public DailyRewardsResult()
    {
        ConfigData = new ConfigData();
        RewardsGranted = new List<DailyReward>();
    }
}

/// <summary>
/// Internal state management class that tracks the current state of the daily rewards event,
/// including epoch times, player status, and calculated timings. Not exposed to the client.
/// </summary>
public class RewardsClaimingState
{
    public long EpochTime { get; set; }
    public DailyRewardsPlayerStatus PlayerStatus { get; set; }
    public DailyRewardsResult Result { get; set; }
}

/// <summary>
/// Configuration data loaded from Remote Config that defines the daily rewards event parameters,
/// including reward schedules and timing settings.
/// </summary>
public class ConfigData
{
    [JsonPropertyName("dailyRewards")]
    public List<DailyReward> DailyRewards { get; set; }
    
    [JsonPropertyName("secondsPerDay")]
    public int SecondsPerDay { get; set; }
    
    [JsonPropertyName("totalDays")]
    public int TotalDays { get; set; }
    
    // Optional bonus rewards
    [JsonPropertyName("bonusReward")]
    public List<DailyReward> BonusReward { get; set; }
}

/// <summary>
/// Represents the player's current progress in the daily rewards event.
/// This data is stored in Cloud Save and persists between sessions.
/// </summary>
public class DailyRewardsPlayerStatus
{
    [JsonPropertyName("startEpochTime")]
    public long StartEpochTime { get; set; }
    [JsonPropertyName("daysClaimed")]
    public int DaysClaimed { get; set; }
    [JsonPropertyName("lastClaimTime")]
    public long LastClaimTime { get; set; }
}

/// <summary>
/// Represents a single reward item that can be granted to the player,
/// defined by its ID (e.g., currency type) and quantity.
/// </summary>
public class DailyReward
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }
    
    public override string ToString()
    {
        return $"({Id} {Quantity})";
    }
}