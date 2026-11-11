using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SampleGameModule.Infrastructure;
using SampleGameModule.Models;
using Unity.Services.CloudCode.Core;

namespace SampleGameModule.Services;

/// <summary>
/// Cloud AI Mini Game sample: server-authoritative tic-tac-toe against a simple AI
/// (win, then block, then random). State lives in the Cloud Save key CLOUD_AI_GAME_STATE
/// as a JSON string; wins and draws pay COIN through the economy.
/// </summary>
public class CloudAiMiniGameService
{
    const string k_GameStateKey = "CLOUD_AI_GAME_STATE";
    const int k_PlayfieldSize = 3;
    const string k_CurrencyId = "COIN";
    const long k_InitialQuantity = 0;
    const long k_WinCurrencyQuantity = 100;
    const long k_TieCurrencyQuantity = 25;
    static readonly Random s_Random = new();

    readonly EconomyModuleService m_Economy;
    readonly PlayerDataStore m_Store;
    readonly ILogger<CloudAiMiniGameService> m_Logger;

    public CloudAiMiniGameService(EconomyModuleService economy, PlayerDataStore store, ILogger<CloudAiMiniGameService> logger)
    {
        m_Economy = economy;
        m_Store = store;
        m_Logger = logger;
    }

    public class Coord
    {
        [JsonProperty("x")] public int X { get; set; }
        [JsonProperty("y")] public int Y { get; set; }
    }

    public class GameState
    {
        [JsonProperty("winCount")] public int WinCount { get; set; }
        [JsonProperty("lossCount")] public int LossCount { get; set; }
        [JsonProperty("tieCount")] public int TieCount { get; set; }
        [JsonProperty("playerPieces")] public List<Coord> PlayerPieces { get; set; } = new();
        [JsonProperty("aiPieces")] public List<Coord> AiPieces { get; set; } = new();
        [JsonProperty("isNewGame")] public bool IsNewGame { get; set; }
        [JsonProperty("isNewMove")] public bool IsNewMove { get; set; }
        [JsonProperty("isPlayerTurn")] public bool IsPlayerTurn { get; set; }
        [JsonProperty("isGameOver")] public bool IsGameOver { get; set; }
        [JsonProperty("status")] public string Status { get; set; } = "";
    }

    /// <summary>
    /// Used by: Cloud AI Mini Game sample, CloudCodeManager.CallGetStateEndpoint.
    /// Returns the saved game; on first visit creates one with a random first mover and zeroes the COIN balance.
    /// </summary>
    [CloudCodeFunction("CloudAiMiniGame_GetState")]
    public async Task<GameState> GetState(IExecutionContext context)
    {
        // Port note: the script logged the whole execution context (access token included); this does not.
        var gameState = await ReadStateAsync(context);
        if (gameState is not null)
        {
            gameState.IsNewMove = false;
            gameState.IsNewGame = false;
        }
        else
        {
            gameState = new GameState();
            StartRandomGame(gameState);
            await m_Economy.SetBalance(context, k_CurrencyId, k_InitialQuantity);
            m_Logger.LogInformation("Created starting state for player {PlayerId}", context.PlayerId);
        }

        await SaveStateAsync(context, gameState);
        return gameState;
    }

    /// <summary>
    /// Used by: Cloud AI Mini Game sample, CloudCodeManager.CallStartNewGameEndpoint.
    /// Starts a fresh game; abandoning an unfinished game counts as a loss.
    /// </summary>
    [CloudCodeFunction("CloudAiMiniGame_StartNewGame")]
    public async Task<GameState> StartNewGame(IExecutionContext context)
    {
        var gameState = await ReadStateAsync(context);
        if (gameState is not null)
        {
            if (!gameState.IsGameOver)
            {
                gameState.LossCount += 1;
                m_Logger.LogInformation("Player {PlayerId} forfeited the current game", context.PlayerId);
            }
        }
        else
        {
            // DASHBOARD TESTING CODE: If the starting state is not found (only occurs in dashboard) then setup dummy state.
            gameState = new GameState();
        }

        StartRandomGame(gameState);
        await SaveStateAsync(context, gameState);
        return gameState;
    }

    /// <summary>
    /// Used by: Cloud AI Mini Game sample, CloudCodeManager.CallResetStateEndpoint.
    /// Deletes the saved game, so the next GetState starts over as a first visit.
    /// </summary>
    [CloudCodeFunction("CloudAiMiniGame_ResetState")]
    public Task ResetState(IExecutionContext context) => m_Store.DeleteAsync(context, k_GameStateKey);

    /// <summary>
    /// Used by: Cloud AI Mini Game sample, CloudCodeManager.CallValidatePlayerMoveAndRespondEndpoint.
    /// Places the player's piece, resolves the game or answers with the AI's piece, and pays out wins and draws.
    /// </summary>
    [CloudCodeFunction("CloudAiMiniGame_ValidatePlayerMoveAndRespond")]
    public async Task<GameState> ValidatePlayerMoveAndRespond(IExecutionContext context, Coord coord)
    {
        var gameState = await ReadStateAsync(context);

        // If save state is found (normal condition) then remember this isn't a new game/move to avoid duplicate popups.
        if (gameState is not null)
        {
            gameState.IsNewGame = false;
            gameState.IsNewMove = false;
        }
        else
        {
            // DASHBOARD TESTING CODE: If the starting state is not found (only occurs in dashboard) then setup dummy state
            // for testing only. By not randomizing here, testing can request valid or invalid locations as needed.
            gameState = new GameState
            {
                AiPieces = { new Coord { X = 0, Y = 1 } },
                IsPlayerTurn = true,
                IsNewGame = true,
                IsNewMove = true,
                Status = "playing",
            };
        }

        // If game is already over, return status to signal select-new-game popup.
        if (gameState.IsGameOver)
            throw new ModuleError(3, "GameOverError", "Game over when player attempted to place a piece.");

        // If player placed a piece in occupied space, show popup on client.
        if (IsSpaceOccupied(gameState, coord.X, coord.Y))
            throw new ModuleError(2, "SpaceOccupiedError", "Player attemped to place a piece in an occupied space.");

        gameState.IsNewMove = true;
        gameState.Status = "playing";
        gameState.IsPlayerTurn = false;
        gameState.PlayerPieces.Add(new Coord { X = coord.X, Y = coord.Y });

        if (!await DetectAndHandleGameOverAsync(context, gameState))
        {
            PlaceAiPiece(gameState);
            if (!await DetectAndHandleGameOverAsync(context, gameState))
                gameState.IsPlayerTurn = true;
        }

        await SaveStateAsync(context, gameState);
        return gameState;
    }

    async Task<GameState?> ReadStateAsync(IExecutionContext context)
    {
        var item = await m_Store.GetAsync(context, k_GameStateKey);
        var json = item.As<string>();
        return string.IsNullOrEmpty(json) ? null : JsonConvert.DeserializeObject<GameState>(json);
    }

    Task SaveStateAsync(IExecutionContext context, GameState gameState)
    {
        return m_Store.SetAsync(context, k_GameStateKey, JsonConvert.SerializeObject(gameState));
    }

    static void StartRandomGame(GameState gameState)
    {
        gameState.PlayerPieces = new List<Coord>();
        gameState.AiPieces = new List<Coord>();
        gameState.IsNewGame = true;
        gameState.IsNewMove = true;
        gameState.IsPlayerTurn = true;
        gameState.IsGameOver = false;
        gameState.Status = "playing";

        // Pick first player at random. If it's the AI then place the first move in random space.
        // Note: the fact that isNewGame is true and aiPieces contains an entry is used by client to detect that AI was player 1.
        if (s_Random.Next(2) == 1)
            gameState.AiPieces.Add(new Coord { X = s_Random.Next(k_PlayfieldSize), Y = s_Random.Next(k_PlayfieldSize) });
    }

    // Handle the ai logic: first try to win, next try to block, otherwise play randomly.
    static void PlaceAiPiece(GameState gameState)
    {
        // To win, place the 3rd piece in any line already containing 2 ai pieces (unless the player already blocked it).
        if (TryToPlace3rdPieceOnAnyLine(gameState, gameState.AiPieces, gameState.PlayerPieces))
            return;

        // To block, place the 3rd piece in any line already containing 2 player pieces (unless the ai already blocked it).
        if (TryToPlace3rdPieceOnAnyLine(gameState, gameState.PlayerPieces, gameState.AiPieces))
            return;

        MakeRandomAiMove(gameState);
    }

    static void MakeRandomAiMove(GameState gameState)
    {
        var x = s_Random.Next(k_PlayfieldSize);
        var y = s_Random.Next(k_PlayfieldSize);

        while (IsSpaceOccupied(gameState, x, y))
        {
            // Note: this allows all board positions to be tried only once quickly while avoiding
            // horizontal/vertical line preference using a simple equation.
            x = (x + (y == 1 ? 1 : 2)) % k_PlayfieldSize;
            y = (y + 1) % k_PlayfieldSize;
        }

        gameState.AiPieces.Add(new Coord { X = x, Y = y });
    }

    static bool IsSpaceOccupied(GameState gameState, int x, int y)
    {
        if (x < 0 || x >= k_PlayfieldSize || y < 0 || y >= k_PlayfieldSize)
            return true;

        return IsPieceFound(gameState.PlayerPieces, x, y) || IsPieceFound(gameState.AiPieces, x, y);
    }

    // Try all lines (horizontal, vertical, diagonal) to see if any has 2 pieces already and
    // place the ai piece in the 3rd position, if possible.
    static bool TryToPlace3rdPieceOnAnyLine(GameState gameState, List<Coord> piecesToCheck, List<Coord> opponentPieces)
    {
        for (var i = 0; i < k_PlayfieldSize; i++)
        {
            // Try all 3 columns.
            if (TryToPlace3rdPieceOnLine(gameState, i, 0, 0, 1, piecesToCheck, opponentPieces))
                return true;

            // Try all 3 rows.
            if (TryToPlace3rdPieceOnLine(gameState, 0, i, 1, 0, piecesToCheck, opponentPieces))
                return true;
        }

        // Try diagonal starting at top-left
        if (TryToPlace3rdPieceOnLine(gameState, 0, 0, 1, 1, piecesToCheck, opponentPieces))
            return true;

        // Try diagonal starting at bottom-left
        if (TryToPlace3rdPieceOnLine(gameState, 0, 2, 1, -1, piecesToCheck, opponentPieces))
            return true;

        // All options exhausted--not possible to place the 3rd piece on any line
        return false;
    }

    // Check specified line (using starting x,y and delta dx,dy) to try to place last piece on the line to win or block.
    // Note: dx,dy is the movement of x,y (the delta) as we move along the line we're checking. So, for example,
    //       to start at bottom-left and move diagonally up, we'd start at x,y=(0,2) and move up and
    //       right 1 each time so dx=1 (move 1 to the right each step) and dy=-1 (move up 1 each step).
    static bool TryToPlace3rdPieceOnLine(GameState gameState, int x, int y, int dx, int dy,
        List<Coord> piecesToCheck, List<Coord> opponentPieces)
    {
        if (CountLinePieces(piecesToCheck, x, y, dx, dy) != k_PlayfieldSize - 1)
            return false;

        for (var i = 0; i < k_PlayfieldSize; i++, x += dx, y += dy)
        {
            if (!IsPieceFound(piecesToCheck, x, y) && !IsPieceFound(opponentPieces, x, y))
            {
                gameState.AiPieces.Add(new Coord { X = x, Y = y });
                return true;
            }
        }

        return false;
    }

    async Task<bool> DetectAndHandleGameOverAsync(IExecutionContext context, GameState gameState)
    {
        if (gameState.IsGameOver)
            return true;

        if (IsWin(gameState.PlayerPieces))
        {
            await GrantCurrencyRewardAsync(context, k_WinCurrencyQuantity);
            gameState.Status = "playerWon";
            gameState.IsGameOver = true;
            gameState.WinCount += 1;
            return true;
        }

        if (IsWin(gameState.AiPieces))
        {
            gameState.Status = "aiWon";
            gameState.IsGameOver = true;
            gameState.LossCount += 1;
            return true;
        }

        if (IsBoardFull(gameState))
        {
            await GrantCurrencyRewardAsync(context, k_TieCurrencyQuantity);
            gameState.Status = "draw";
            gameState.IsGameOver = true;
            gameState.TieCount += 1;
            return true;
        }

        return false;
    }

    static bool IsWin(List<Coord> pieces)
    {
        for (var i = 0; i < k_PlayfieldSize; i++)
        {
            if (CountLinePieces(pieces, i, 0, 0, 1) == k_PlayfieldSize)
                return true;
            if (CountLinePieces(pieces, 0, i, 1, 0) == k_PlayfieldSize)
                return true;
        }

        return CountLinePieces(pieces, 0, 0, 1, 1) == k_PlayfieldSize
            || CountLinePieces(pieces, 0, k_PlayfieldSize - 1, 1, -1) == k_PlayfieldSize;
    }

    // Count number of game pieces (either player's or ai's) along specified line (starting at x,y and using delta dx,dy).
    static int CountLinePieces(List<Coord> pieces, int x, int y, int dx, int dy)
    {
        var count = 0;
        for (var i = 0; i < k_PlayfieldSize; i++, x += dx, y += dy)
        {
            if (IsPieceFound(pieces, x, y))
                count++;
        }

        return count;
    }

    static bool IsPieceFound(List<Coord> pieces, int x, int y) => pieces.Any(p => p.X == x && p.Y == y);

    static bool IsBoardFull(GameState gameState)
        => gameState.PlayerPieces.Count + gameState.AiPieces.Count >= k_PlayfieldSize * k_PlayfieldSize;

    Task GrantCurrencyRewardAsync(IExecutionContext context, long amount)
    {
        return m_Economy.GrantAsync(context, new[] { new KeyValuePair<string, long>(k_CurrencyId, amount) });
    }
}
