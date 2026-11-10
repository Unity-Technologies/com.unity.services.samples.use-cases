using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SampleGameModule.Infrastructure;
using SampleGameModule.Models;
using SampleGameModule.Services;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Api;
using Unity.Services.CloudSave.Model;
using Xunit;
using Coord = SampleGameModule.Services.CloudAiMiniGameService.Coord;
using GameState = SampleGameModule.Services.CloudAiMiniGameService.GameState;

namespace SampleGameModule.Tests;

public class CloudAiMiniGameServiceTests
{
    const string k_GameStateKey = "CLOUD_AI_GAME_STATE";
    const string k_PlayerEconomyKey = "player_economy";

    readonly Mock<ICloudSaveDataApi> m_CloudSave = new();
    readonly Mock<IExecutionContext> m_Context = new();
    readonly Dictionary<string, object> m_Stored = new();
    readonly List<SetItemBody> m_Writes = new();
    readonly CloudAiMiniGameService m_Service;

    public CloudAiMiniGameServiceTests()
    {
        m_Context.SetupGet(c => c.ProjectId).Returns("project");
        m_Context.SetupGet(c => c.PlayerId).Returns("player");
        m_Context.SetupGet(c => c.EnvironmentId).Returns("environment");
        m_Context.SetupGet(c => c.AccessToken).Returns("token");
        m_Context.SetupGet(c => c.ServiceToken).Returns("service-token");

        var gameApiClient = new Mock<IGameApiClient>();
        gameApiClient.SetupGet(c => c.CloudSaveData).Returns(m_CloudSave.Object);

        m_CloudSave
            .Setup(c => c.GetProtectedItemsAsync(It.IsAny<IExecutionContext>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((IExecutionContext _, string _, string _, string _, List<string> keys, string _, CancellationToken _) =>
            {
                var results = keys.Where(m_Stored.ContainsKey)
                    .Select(k => new Item(k, m_Stored[k], "lock1",
                        new ModifiedMetadata(DateTime.UtcNow), new ModifiedMetadata(DateTime.UtcNow)))
                    .ToList();
                return Task.FromResult(Response(new GetItemsResponse(results, new GetItemsResponseLinks(""))));
            });

        m_CloudSave
            .Setup(c => c.SetProtectedItemAsync(It.IsAny<IExecutionContext>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<SetItemBody>(), It.IsAny<CancellationToken>()))
            .Callback((IExecutionContext _, string _, string _, string _, SetItemBody body, CancellationToken _) =>
            {
                m_Writes.Add(body);
                m_Stored[body.Key] = body.Value;
            })
            .ReturnsAsync(Response(new SetItemResponse("newLock")));

        var store = new PlayerDataStore(gameApiClient.Object);
        var economy = new EconomyModuleService(NullLogger<EconomyModuleService>.Instance, store,
            new RemoteConfigReader(gameApiClient.Object));
        m_Service = new CloudAiMiniGameService(economy, store, NullLogger<CloudAiMiniGameService>.Instance);
    }

    // Game state is stored the way the script wrote it: a JSON string.
    void GivenGame(GameState state)
    {
        m_Stored[k_GameStateKey] = JsonConvert.SerializeObject(state);
    }

    static GameState Playing(IEnumerable<(int x, int y)> player, IEnumerable<(int x, int y)> ai)
    {
        return new GameState
        {
            PlayerPieces = player.Select(p => new Coord { X = p.x, Y = p.y }).ToList(),
            AiPieces = ai.Select(p => new Coord { X = p.x, Y = p.y }).ToList(),
            IsPlayerTurn = true,
            Status = "playing",
        };
    }

    static Coord At(int x, int y) => new() { X = x, Y = y };

    static bool Has(List<Coord> pieces, int x, int y) => pieces.Any(p => p.X == x && p.Y == y);

    GameState StoredGame() => JsonConvert.DeserializeObject<GameState>((string)m_Stored[k_GameStateKey])!;

    long StoredCoin()
    {
        var economy = Assert.Single(m_Writes, w => w.Key == k_PlayerEconomyKey);
        return JObject.FromObject(economy.Value)["wallet"]!.Value<long>("COIN");
    }

    static JObject ParseError(ModuleError error) => JObject.Parse(error.Message);

    // ApiResponse<T>.Data has a non-public setter, so responses are built via reflection.
    static ApiResponse<T> Response<T>(T data)
    {
        var response = new ApiResponse<T>();
        typeof(ApiResponse<T>).GetProperty(nameof(ApiResponse<T>.Data))!.SetValue(response, data);
        return response;
    }

    [Fact]
    public async Task GetState_FirstVisit_CreatesGameAndZeroesCoin()
    {
        var state = await m_Service.GetState(m_Context.Object);

        Assert.True(state.IsNewGame);
        Assert.Equal("playing", state.Status);
        Assert.Empty(state.PlayerPieces);
        Assert.InRange(state.AiPieces.Count, 0, 1);
        Assert.Equal(0, StoredCoin());
        Assert.IsType<string>(Assert.Single(m_Writes, w => w.Key == k_GameStateKey).Value);
    }

    [Fact]
    public async Task GetState_ExistingGame_ClearsNewFlagsAndDoesNotTouchEconomy()
    {
        GivenGame(new GameState { IsNewGame = true, IsNewMove = true, WinCount = 2, Status = "playing" });

        var state = await m_Service.GetState(m_Context.Object);

        Assert.False(state.IsNewGame);
        Assert.False(state.IsNewMove);
        Assert.Equal(2, state.WinCount);
        Assert.DoesNotContain(m_Writes, w => w.Key == k_PlayerEconomyKey);
    }

    [Fact]
    public async Task StartNewGame_UnfinishedGame_CountsAsLoss()
    {
        GivenGame(Playing(new[] { (0, 0) }, new[] { (1, 1) }));

        var state = await m_Service.StartNewGame(m_Context.Object);

        Assert.Equal(1, state.LossCount);
        Assert.True(state.IsNewGame);
        Assert.Empty(state.PlayerPieces);
        Assert.False(state.IsGameOver);
    }

    [Fact]
    public async Task StartNewGame_FinishedGame_DoesNotCountAsLoss()
    {
        var finished = Playing(Array.Empty<(int, int)>(), Array.Empty<(int, int)>());
        finished.IsGameOver = true;
        finished.Status = "playerWon";
        GivenGame(finished);

        var state = await m_Service.StartNewGame(m_Context.Object);

        Assert.Equal(0, state.LossCount);
        Assert.Equal("playing", state.Status);
    }

    [Fact]
    public async Task ValidateMove_GameOver_Throws3AndDoesNotWrite()
    {
        var finished = Playing(Array.Empty<(int, int)>(), Array.Empty<(int, int)>());
        finished.IsGameOver = true;
        GivenGame(finished);

        var error = await Assert.ThrowsAsync<ModuleError>(
            () => m_Service.ValidatePlayerMoveAndRespond(m_Context.Object, At(0, 0)));

        Assert.Equal(3, ParseError(error).Value<int>("status"));
        Assert.Equal("GameOverError", ParseError(error).Value<string>("name"));
        Assert.Empty(m_Writes);
    }

    [Fact]
    public async Task ValidateMove_OccupiedSpace_Throws2()
    {
        GivenGame(Playing(Array.Empty<(int, int)>(), new[] { (1, 1) }));

        var error = await Assert.ThrowsAsync<ModuleError>(
            () => m_Service.ValidatePlayerMoveAndRespond(m_Context.Object, At(1, 1)));

        Assert.Equal(2, ParseError(error).Value<int>("status"));
        Assert.Equal("SpaceOccupiedError", ParseError(error).Value<string>("name"));
    }

    [Fact]
    public async Task ValidateMove_OutOfBounds_Throws2()
    {
        GivenGame(Playing(Array.Empty<(int, int)>(), Array.Empty<(int, int)>()));

        var error = await Assert.ThrowsAsync<ModuleError>(
            () => m_Service.ValidatePlayerMoveAndRespond(m_Context.Object, At(3, 0)));

        Assert.Equal(2, ParseError(error).Value<int>("status"));
    }

    [Fact]
    public async Task ValidateMove_AiBlocksImminentPlayerWin()
    {
        GivenGame(Playing(new[] { (0, 0) }, new[] { (0, 1) }));

        var state = await m_Service.ValidatePlayerMoveAndRespond(m_Context.Object, At(1, 0));

        Assert.True(Has(state.PlayerPieces, 1, 0));
        Assert.True(Has(state.AiPieces, 2, 0));
        Assert.Equal(2, state.AiPieces.Count);
        Assert.True(state.IsPlayerTurn);
        Assert.True(state.IsNewMove);
        Assert.False(state.IsGameOver);
    }

    [Fact]
    public async Task ValidateMove_AiPrefersWinningOverBlocking()
    {
        // Player threatens row y=0 at (2,0); AI can win column x=1 at (1,2).
        GivenGame(Playing(new[] { (0, 0), (2, 2) }, new[] { (1, 0), (1, 1) }));

        var state = await m_Service.ValidatePlayerMoveAndRespond(m_Context.Object, At(0, 2));

        Assert.True(Has(state.AiPieces, 1, 2));
        Assert.Equal("aiWon", state.Status);
        Assert.True(state.IsGameOver);
        Assert.Equal(1, state.LossCount);
        Assert.DoesNotContain(m_Writes, w => w.Key == k_PlayerEconomyKey);
    }

    [Fact]
    public async Task ValidateMove_PlayerWin_Grants100Coin()
    {
        GivenGame(Playing(new[] { (0, 0), (1, 0) }, new[] { (0, 1), (2, 2) }));

        var state = await m_Service.ValidatePlayerMoveAndRespond(m_Context.Object, At(2, 0));

        Assert.Equal("playerWon", state.Status);
        Assert.True(state.IsGameOver);
        Assert.Equal(1, state.WinCount);
        Assert.Equal(2, state.AiPieces.Count);
        Assert.Equal(100, StoredCoin());
        Assert.Equal("playerWon", StoredGame().Status);
    }

    [Fact]
    public async Task ValidateMove_BoardFull_DrawGrants25Coin()
    {
        // X O X
        // X O O
        // O X _   <- player fills (2,2): no line for either side.
        GivenGame(Playing(
            new[] { (0, 0), (2, 0), (0, 1), (1, 2) },
            new[] { (1, 0), (1, 1), (2, 1), (0, 2) }));

        var state = await m_Service.ValidatePlayerMoveAndRespond(m_Context.Object, At(2, 2));

        Assert.Equal("draw", state.Status);
        Assert.Equal(1, state.TieCount);
        Assert.Equal(25, StoredCoin());
    }

    [Fact]
    public async Task ResetState_DeletesTheGame()
    {
        await m_Service.ResetState(m_Context.Object);

        m_CloudSave.Verify(c => c.DeleteProtectedItemAsync(It.IsAny<IExecutionContext>(), It.IsAny<string>(), k_GameStateKey,
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
