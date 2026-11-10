using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SampleGameModule.Infrastructure;
using SampleGameModule.Models;
using Unity.Services.CloudCode.Core;

namespace SampleGameModule.Services;

/// <summary>A playfield position.</summary>
public class IdleClickerCoord
{
    [JsonProperty("x")] public int X { get; set; }
    [JsonProperty("y")] public int Y { get; set; }

    public bool IsAt(IdleClickerCoord other) => X == other.X && Y == other.Y;
}

/// <summary>A well: its position plus the time it was placed (its water accrues from there).</summary>
public class IdleClickerWell : IdleClickerCoord
{
    [JsonProperty("timestamp")] public long Timestamp { get; set; }
}

/// <summary>
/// The IDLE_CLICKER_GAME_STATE Cloud Save value, and the object every function returns.
/// currencyBalance is only filled in on the returned copy, after the state has been saved.
/// </summary>
public class IdleClickerState
{
    [JsonProperty("obstacles")] public List<IdleClickerCoord> Obstacles { get; set; } = new();
    [JsonProperty("wells_level1")] public List<IdleClickerWell> WellsLevel1 { get; set; } = new();
    [JsonProperty("wells_level2")] public List<IdleClickerWell> WellsLevel2 { get; set; } = new();
    [JsonProperty("wells_level3")] public List<IdleClickerWell> WellsLevel3 { get; set; } = new();
    [JsonProperty("wells_level4")] public List<IdleClickerWell> WellsLevel4 { get; set; } = new();
    [JsonProperty("timestamp")] public long Timestamp { get; set; }
    [JsonProperty("unlockCounters")] public Dictionary<string, int> UnlockCounters { get; set; } = new();

    [JsonProperty("currencyBalance", NullValueHandling = NullValueHandling.Ignore)]
    public long? CurrencyBalance { get; set; }

    public List<IdleClickerWell> Wells(int level) => level switch
    {
        1 => WellsLevel1,
        2 => WellsLevel2,
        3 => WellsLevel3,
        4 => WellsLevel4,
        _ => throw new ArgumentOutOfRangeException(nameof(level)),
    };
}

/// <summary>
/// Idle Clicker Game sample: a 5x5 playfield of obstacles and wells. A level-N well produces
/// N WATER per second, granted through the economy on every call; placing and merging wells
/// are virtual purchases. The board lives in the IDLE_CLICKER_GAME_STATE Cloud Save key.
/// </summary>
public class IdleClickerGameService
{
    const int k_MillisecondsPerSecond = 1000;
    const int k_PlayfieldSize = 5;
    const int k_NumStartingObstacles = 3;
    const string k_CurrencyId = "WATER";
    const long k_InitialCurrencyBalance = 1000;
    const long k_WellCurrencyCostPerLevel = 100;
    const int k_MaxWellLevel = 4;
    const int k_UnlockCountRequired = 4;
    const string k_GameStateCloudSaveKey = "IDLE_CLICKER_GAME_STATE";
    const string k_WellPurchaseIdPrefix = "IDLE_CLICKER_GAME_PURCHASE_WELL_LEVEL_";
    const string k_WellUnlockCounterPrefix = "Well_Level";
    static readonly Random s_Random = new();

    readonly EconomyModuleService m_Economy;
    readonly PlayerDataStore m_Store;
    readonly ILogger<IdleClickerGameService> m_Logger;

    public IdleClickerGameService(EconomyModuleService economy, PlayerDataStore store, ILogger<IdleClickerGameService> logger)
    {
        m_Economy = economy;
        m_Store = store;
        m_Logger = logger;
    }

    /// <summary>
    /// Used by: Idle Clicker Game sample, CloudCodeManager.CallGetUpdatedStateEndpoint.
    /// Creates a random board (and resets WATER to its starting balance) when the player has
    /// none, then grants the water produced since the last call and saves the state.
    /// </summary>
    [CloudCodeFunction("IdleClicker_GetUpdatedState")]
    public async Task<IdleClickerState> GetUpdatedState(IExecutionContext context)
    {
        var now = CurrentTimestamp();

        var state = await ReadStateAsync(context);
        if (state is not null)
        {
            m_Logger.LogInformation("Read start state: {State}", JsonConvert.SerializeObject(state));
        }
        else
        {
            state = CreateRandomState(now);
            m_Logger.LogInformation("Created random start state: {State}", JsonConvert.SerializeObject(state));

            // Set starting water balance for a new game. This is also the initial value in the catalog, but, since we permit resetting the game state,
            // we need to also reset the water balance so the board is in the 'default state' when starting a new game.
            await m_Economy.SetBalance(context, k_CurrencyId, k_InitialCurrencyBalance);
        }

        var currencyBalance = await UpdateStateAsync(context, state, now);
        m_Logger.LogInformation("Updated state: {State}", JsonConvert.SerializeObject(state));

        await SaveGameStateAsync(context, state);

        // After we've saved the state, insert the currency balance to return to caller.
        state.CurrencyBalance = currencyBalance;
        return state;
    }

    /// <summary>
    /// Used by: Idle Clicker Game sample, CloudCodeManager.CallPlaceWellEndpoint.
    /// Buys a level-1 well and places it at <paramref name="coord"/>.
    /// </summary>
    [CloudCodeFunction("IdleClicker_PlaceWell")]
    public async Task<IdleClickerState> PlaceWell(IExecutionContext context, IdleClickerCoord coord)
    {
        var now = CurrentTimestamp();

        var state = await ReadStateAsync(context)
            ?? throw new ModuleError(2, "StateMissingError",
                "PlaceWell script executed without valid state setup. Be sure to call GetUpdatedState script before merging wells.");

        var currencyBalance = await UpdateStateAsync(context, state, now);

        // Save state now with updated timestamp.
        // It's important to save now and again after Well has been added so, if we throw between the calls, Cloud Save maintains
        // the correct timestamp for when we last granted water.
        // Port note: as in the scripts, the water grant and this save happen before validation, so water is granted even if the call then throws.
        await SaveGameStateAsync(context, state);

        // Validate the location and throw if invalid.
        // Note: This is done after updating status since we want to always update water even if the placement fails.
        ThrowIfLocationInvalid(coord, "Player attemped to place a piece at invalid location.");

        ThrowIfSpaceOccupied(state, coord);

        // Attempt the Virtual Purchase for the Well. Throws if purchase is unsuccessful.
        await PurchaseWellAsync(context, 1);

        // If we get to this point, the space is empty and Virtual Purchases has succeeded so we just need add it to the game state.
        AddWellToState(state, coord, 1, now);
        m_Logger.LogInformation("Placement successful. Updated state: {State}", JsonConvert.SerializeObject(state));

        // Save state now with new well added to game state.
        await SaveGameStateAsync(context, state);

        // After we've saved the state, insert the currency balance to return to caller.
        // We retrieved the currency balance at the start so we must now update it by deducting the cost of purchasing the Well.
        state.CurrencyBalance = currencyBalance - k_WellCurrencyCostPerLevel;
        return state;
    }

    /// <summary>
    /// Used by: Idle Clicker Game sample, CloudCodeManager.CallMergeWellsEndpoint.
    /// Merges two same-level wells into one well of the next level at the drop location,
    /// buying the new well and advancing the unlock counter of the level above it.
    /// </summary>
    [CloudCodeFunction("IdleClicker_MergeWells")]
    public async Task<IdleClickerState> MergeWells(IExecutionContext context, IdleClickerCoord drag, IdleClickerCoord drop)
    {
        var now = CurrentTimestamp();

        var state = await ReadStateAsync(context)
            ?? throw new ModuleError(2, "StateMissingError",
                "MergeWells script executed without valid state setup. Be sure to call GetUpdatedState script before merging wells.");

        var currencyBalance = await UpdateStateAsync(context, state, now);

        // Save state now with updated timestamp.
        // It's important to save now and again after Well has been added so, if we throw between the calls, Cloud Save maintains
        // the correct timestamp for when we last granted water.
        // Port note: as in the scripts, the water grant and this save happen before validation, so water is granted even if the call then throws.
        await SaveGameStateAsync(context, state);

        // Validate locations and throw if either is invalid.
        // Note: This is done after updating status since we want to always update water even if the merge fails.
        ThrowIfLocationInvalid(drag, "Player specified an invalid location.");
        ThrowIfLocationInvalid(drop, "Player specified an invalid location.");

        // Make sure drag & drop not same well.
        if (drag.IsAt(drop))
            throw new ModuleError(6, "InvalidDragError", "Drag and drop locations are the same.");

        // Find and remove the wells at both locations. Throws if either not found.
        // Note: we can remove the wells from the local state as we find them since, if anything goes wrong, we'll throw out
        //       and the local data will just be discarded.
        var dragLevel = RemoveWell(state, drag);
        var dropLevel = RemoveWell(state, drop);

        if (dragLevel != dropLevel)
            throw new ModuleError(7, "WellsDifferentLevelError", "Drag and drop wells must be the same level to merge.");

        var mergedLevel = dragLevel + 1;
        if (mergedLevel > k_MaxWellLevel)
            throw new ModuleError(8, "MaxWellLevelError", "Drag and drop wells are max level and cannot be merged.");

        if (!IsWellLevelUnlocked(state, mergedLevel))
            throw new ModuleError(10, "WellLevelLockedError", "Next well level (" + mergedLevel + ") is locked.");

        // Execute Virtual Purchase to consume the correct water. If successful, we can add the well to the game state.
        await PurchaseWellAsync(context, mergedLevel);

        AddWellToState(state, drop, mergedLevel, now);

        IncrementUnlockCounter(state, mergedLevel + 1);

        // Save state now with new well added to game state.
        await SaveGameStateAsync(context, state);

        // After we've saved the state, insert the currency balance to return to caller.
        // We retrieved the currency balance at the start so we must now update it by deducting the cost of purchasing the Well.
        state.CurrencyBalance = currencyBalance - k_WellCurrencyCostPerLevel * mergedLevel;

        m_Logger.LogInformation("Merge successful.");
        return state;
    }

    /// <summary>
    /// Used by: Idle Clicker Game sample, CloudCodeManager.CallMoveWellEndpoint.
    /// Moves the well at the drag location to the (empty) drop location.
    /// </summary>
    [CloudCodeFunction("IdleClicker_MoveWell")]
    public async Task<IdleClickerState> MoveWell(IExecutionContext context, IdleClickerCoord drag, IdleClickerCoord drop)
    {
        var now = CurrentTimestamp();

        // Port note: the script carried a leftover "//todo1" debug board for the missing-state case; it is not ported.
        // Missing state throws StateMissingError like the other functions.
        var state = await ReadStateAsync(context)
            ?? throw new ModuleError(2, "StateMissingError",
                "MoveWell script executed without valid state setup. Be sure to call GetUpdatedState script before moving wells.");

        var currencyBalance = await UpdateStateAsync(context, state, now);

        // Save state now with updated timestamp.
        // It's important to save now and again after Well has been added so, if we throw between the calls, Cloud Save maintains
        // the correct timestamp for when we last granted water.
        // Port note: as in the scripts, the water grant and this save happen before validation, so water is granted even if the call then throws.
        await SaveGameStateAsync(context, state);

        // Validate locations and throw if either is invalid.
        // Note: This is done after updating status since we want to always update water even if the move fails.
        ThrowIfLocationInvalid(drag, "Player specified an invalid location.");
        ThrowIfLocationInvalid(drop, "Player specified an invalid location.");

        // Make sure drag & drop not same well.
        if (drag.IsAt(drop))
            throw new ModuleError(6, "InvalidDragError", "Drag and drop locations are the same.");

        // Find the well to drag. Throws if well not found.
        var well = FindWell(state, drag);

        // Check the drop coord for Obstacles and Wells. Throws if movement is blocked.
        ThrowIfSpaceOccupied(state, drop);

        // Update well coords in state which is returned to client.
        well.X = drop.X;
        well.Y = drop.Y;

        // Save state again, now with well at new coord.
        await SaveGameStateAsync(context, state);

        // After we've saved the state, insert the currency balance to return to caller.
        state.CurrencyBalance = currencyBalance;

        m_Logger.LogInformation("Move successful.");
        return state;
    }

    /// <summary>
    /// Used by: Idle Clicker Game sample, CloudCodeManager.CallResetEndpoint.
    /// Deletes the player's board; a missing board is not an error.
    /// </summary>
    [CloudCodeFunction("IdleClicker_Reset")]
    public async Task Reset(IExecutionContext context)
    {
        await m_Store.DeleteAsync(context, k_GameStateCloudSaveKey);
        m_Logger.LogInformation("Game state reset.");
    }

    static long CurrentTimestamp() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    async Task<IdleClickerState?> ReadStateAsync(IExecutionContext context)
    {
        var item = await m_Store.GetAsync(context, k_GameStateCloudSaveKey);
        return item.As<IdleClickerState>();
    }

    Task SaveGameStateAsync(IExecutionContext context, IdleClickerState state)
        => m_Store.SetAsync(context, k_GameStateCloudSaveKey, JObject.FromObject(state));

    static IdleClickerState CreateRandomState(long now)
    {
        var state = new IdleClickerState
        {
            Timestamp = now,

            // Unlock Manager starting state: Level-1 Wells can be placed and level-2 Wells can be created by merging level-1 Wells, however
            // level-3 and level-4 Wells must be unlocked by merging 4 times to create Wells of the previous level.
            UnlockCounters =
            {
                [k_WellUnlockCounterPrefix + 1] = 4,
                [k_WellUnlockCounterPrefix + 2] = 4,
                [k_WellUnlockCounterPrefix + 3] = 0,
                [k_WellUnlockCounterPrefix + 4] = 0,
            },
        };

        for (var i = 0; i < k_NumStartingObstacles; i++)
            AddRandomObstacle(state);

        return state;
    }

    static void AddRandomObstacle(IdleClickerState state)
    {
        // Find an empty space on the playfield.
        // The while statement makes it retry in the unlikely event that an obstacle is already in a given position until an empty space is found.
        while (true)
        {
            var coord = new IdleClickerCoord { X = s_Random.Next(k_PlayfieldSize), Y = s_Random.Next(k_PlayfieldSize) };
            if (!state.Obstacles.Any(item => item.IsAt(coord)))
            {
                state.Obstacles.Add(coord);
                return;
            }
        }
    }

    /// <summary>Grants the water every well produced since the stored timestamp, stamps the state with now, and returns the WATER balance.</summary>
    async Task<long> UpdateStateAsync(IExecutionContext context, IdleClickerState state, long now)
    {
        var lastTimestamp = state.Timestamp;
        var waterToProduce = Enumerable.Range(1, k_MaxWellLevel)
            .Sum(level => state.Wells(level).Sum(well => WaterProducedSinceLastUpdate(well, level, lastTimestamp, now)));

        state.Timestamp = now;

        return await GrantWaterAsync(context, waterToProduce);
    }

    // A level-N well grants N water per second.
    static long WaterProducedSinceLastUpdate(IdleClickerWell well, int waterGrantedPerSecond, long lastTimestamp, long now)
    {
        var totalWaterProduced = FloorSeconds((now - well.Timestamp) * waterGrantedPerSecond);
        var waterAlreadyProduced = FloorSeconds((lastTimestamp - well.Timestamp) * waterGrantedPerSecond);
        return totalWaterProduced - waterAlreadyProduced;
    }

    static long FloorSeconds(long milliseconds) => (long)Math.Floor(milliseconds / (double)k_MillisecondsPerSecond);

    async Task<long> GrantWaterAsync(IExecutionContext context, long amount)
    {
        m_Logger.LogInformation("Granting {Amount} water.", amount);
        var snapshot = await m_Economy.GrantAsync(context, new[] { new KeyValuePair<string, long>(k_CurrencyId, amount) });
        return snapshot.Wallet.GetValueOrDefault(k_CurrencyId);
    }

    async Task PurchaseWellAsync(IExecutionContext context, int level)
    {
        try
        {
            await m_Economy.MakeVirtualPurchasesAsync(context, new[] { k_WellPurchaseIdPrefix + level });
        }
        catch (EconomyError e)
        {
            // The scripts wrapped the economy's error so the client sees the economy status (e.g. 10504) under an "EconomyError: ..." name.
            var innerMessage = JObject.Parse(e.Message).Value<string>("message");
            throw new ModuleError(e.Status, "EconomyError: " + e.Name, "Purchasing Well failed: " + innerMessage);
        }
    }

    static void ThrowIfLocationInvalid(IdleClickerCoord coord, string message)
    {
        if (coord.X < 0 || coord.X >= k_PlayfieldSize ||
            coord.Y < 0 || coord.Y >= k_PlayfieldSize)
            throw new ModuleError(9, "InvalidLocationError", message);
    }

    static void ThrowIfSpaceOccupied(IdleClickerState state, IdleClickerCoord coord)
    {
        if (state.Obstacles.Any(item => item.IsAt(coord)) ||
            Enumerable.Range(1, k_MaxWellLevel).Any(level => state.Wells(level).Any(item => item.IsAt(coord))))
            throw new ModuleError(3, "SpaceOccupiedError", "Player attemped to place a piece in an occupied space.");
    }

    static void AddWellToState(IdleClickerState state, IdleClickerCoord coord, int level, long now)
        => state.Wells(level).Add(new IdleClickerWell { X = coord.X, Y = coord.Y, Timestamp = now });

    /// <summary>Removes the well at coord from whichever level list holds it and returns that level. Only Wells of the same level may be merged.</summary>
    static int RemoveWell(IdleClickerState state, IdleClickerCoord coord)
    {
        for (var level = 1; level <= k_MaxWellLevel; level++)
        {
            var wells = state.Wells(level);
            var index = wells.FindIndex(item => item.IsAt(coord));
            if (index >= 0)
            {
                wells.RemoveAt(index);
                return level;
            }
        }

        throw new ModuleError(5, "WellNotFoundError",
            "Player attemped to merge wells, however no well was found at location: " + JsonConvert.SerializeObject(coord));
    }

    static IdleClickerWell FindWell(IdleClickerState state, IdleClickerCoord coord)
    {
        return Enumerable.Range(1, k_MaxWellLevel)
            .Select(level => state.Wells(level).Find(item => item.IsAt(coord)))
            .FirstOrDefault(well => well is not null)
            ?? throw new ModuleError(5, "WellNotFoundError",
                "Player attemped to move a well, however well was not found at starting drag location.");
    }

    static bool IsWellLevelUnlocked(IdleClickerState state, int wellLevel)
        => state.UnlockCounters.GetValueOrDefault(k_WellUnlockCounterPrefix + wellLevel) >= k_UnlockCountRequired;

    static void IncrementUnlockCounter(IdleClickerState state, int unlockLevel)
    {
        if (unlockLevel > k_MaxWellLevel)
            return;
        var key = k_WellUnlockCounterPrefix + unlockLevel;
        state.UnlockCounters[key] = state.UnlockCounters.GetValueOrDefault(key) + 1;
    }
}
