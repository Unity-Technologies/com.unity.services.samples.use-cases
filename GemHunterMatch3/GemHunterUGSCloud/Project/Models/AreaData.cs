using System;
using System.Collections.Generic;
using Newtonsoft.Json;
namespace GemHunterUGSCloud.Models;

public class AreaData
{
    [JsonProperty("areaName")]
    public string? AreaName { get; set; }

    [JsonProperty("areaLevel")]
    public int AreaLevel { get; set; } = 1;

    [JsonProperty("currentProgress")]
    public int CurrentProgress { get; set; } = 0;

    [JsonProperty("maxProgress")]
    public int MaxProgress { get; set; } = 25;

    [JsonProperty("totalUpgradableSlots")]
    public int TotalUpgradableSlots { get; set; } = 5;
    
    [JsonProperty("unlockRequirement_Stars")]
    public int UnlockRequirement_Stars { get; set; } = 1;
    
    [JsonProperty("upgradableAreaItems")]
    public List<UpgradableAreaItem> UpgradableAreaItems { get; set; }

    public AreaData(string? areaName, int areaLevel, List<UpgradableAreaItem> upgradableAreaItems)
    {
        AreaName = areaName;
        AreaLevel = areaLevel;
        UpgradableAreaItems = upgradableAreaItems;
        CurrentProgress = 0;
        MaxProgress = 0;
    }

    public AreaData()
    {
        UpgradableAreaItems = new List<UpgradableAreaItem>();
    }
}

public class UpgradableAreaItem
{
    [JsonProperty("upgradableName")]
    public string? UpgradableName { get; set; }

    [JsonProperty("upgradableID")]
    public int UpgradableId { get; set; }

    [JsonProperty("isUnlocked")]
    public bool IsUnlocked { get; set; }

    [JsonProperty("currentLevel")]
    public int CurrentLevel { get; set; }

    [JsonProperty("maxLevel")]
    public int MaxLevel { get; set; } = 5;

    [JsonProperty("perLevelCoinUpgradeRequirement")]
    public int PerLevelCoinUpgradeRequirement { get; set; } = 20;

    public UpgradableAreaItem()
    {
    }
}