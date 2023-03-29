using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace Unity.Services.Samples.ServerlessMultiplayerGame
{
    [DisallowMultipleComponent]
    public class LobbyManager : MonoBehaviour
    {
        // Lobby data key used to lookup each players' name as displayed in the lobby.
        public const string k_PlayerNameKey = "playerName";

        // Lobby data key used to check if each player has clicked the [Ready] button.
        public const string k_IsReadyKey = "isReady";

        // Lobby data for host name.
        public const string k_HostNameKey = "hostName";

        // Lobby data for host's Relay Join Code. Used to allow all players to initialize Relay so NGO
        // (Netcode for GameObjects) can synchronize multiplayer game play between players.
        public const string k_RelayJoinCodeKey = "relayJoinCode";

        // Frequency to call GetLobbyAsync to update player state, such as join/leave and ready state.
        // Note that if called to frequently, this will result in rate limit exceptions.
        const float k_UpdatePlayersFrequency = 1.5f;

        // Frequency for host to call SendHeartbeatPingAsync to keep lobby active.
        // Note that if called to frequently, this will result in rate limit exceptions.
        const float k_HostHeartbeatFrequency = 15;

        public static LobbyManager instance { get; private set; }

        public List<Lobby> lobbiesList { get; private set; } = new List<Lobby>();

        public Lobby activeLobby { get; private set; }

        public static string playerId => AuthenticationService.Instance.PlayerId;

        public List<Player> players { get; private set; }

        public int numPlayers => players.Count;

        public bool isHost { get; private set; }

        public static event Action<Lobby, bool> OnLobbyChanged;

        public static event Action OnPlayerNotInLobbyEvent;

        float m_NextHostHeartbeatTime;

        float m_NextUpdatePlayersTime;

        string m_PlayerName;

        bool m_IsPlayerReady = false;

        bool m_WasGameStarted = false;

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
            }
            else
            {
                instance = this;
            }
        }

        async void Update()
        {
            try
            {
                if (activeLobby != null && !m_WasGameStarted)
                {
                    if (isHost && Time.realtimeSinceStartup >= m_NextHostHeartbeatTime)
                    {
                        await PeriodicHostHeartbeat();

                        // Exit this update now so we'll only ever update 1 item (heartbeat or lobby changes) in 1 Update().
                        return;
                    }

                    if (Time.realtimeSinceStartup >= m_NextUpdatePlayersTime)
                    {
                        await PeriodicUpdateLobby();
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        async Task PeriodicHostHeartbeat()
        {
            try
            {
                // Set next heartbeat time before calling Lobby Service since next update could also trigger a
                // heartbeat which could cause throttling issues.
                m_NextHostHeartbeatTime = Time.realtimeSinceStartup + k_HostHeartbeatFrequency;

                await LobbyService.Instance.SendHeartbeatPingAsync(activeLobby.Id);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        async Task PeriodicUpdateLobby()
        {
            try
            {
                // Set next update time before calling Lobby Service since next update could also trigger an
                // update which could cause throttling issues.
                m_NextUpdatePlayersTime = Time.realtimeSinceStartup + k_UpdatePlayersFrequency;

                var updatedLobby = await LobbyService.Instance.GetLobbyAsync(activeLobby.Id);
                if (this == null) return;

                UpdateLobby(updatedLobby);
            }

            // Handle lobby no longer exists (host canceled game and returned to main menu).
            catch (LobbyServiceException e) when (e.Reason == LobbyExceptionReason.LobbyNotFound)
            {
                if (this == null) return;

                ServerlessMultiplayerGameSampleManager.instance.SetReturnToMenuReason(
                    ServerlessMultiplayerGameSampleManager.ReturnToMenuReason.LobbyClosed);

                OnPlayerNotInLobby();
            }

            // Handle player no longer allowed to view lobby (host booted player so player is no longer in the lobby).
            catch (LobbyServiceException e) when (e.Reason == LobbyExceptionReason.Forbidden)
            {
                if (this == null) return;

                ServerlessMultiplayerGameSampleManager.instance.SetReturnToMenuReason(
                    ServerlessMultiplayerGameSampleManager.ReturnToMenuReason.PlayerKicked);

                OnPlayerNotInLobby();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public async Task<Lobby> CreateLobby(string lobbyName, int maxPlayers, string hostName,
            bool isPrivate, string relayJoinCode)
        {
            try
            {
                isHost = true;
                m_PlayerName = hostName;
                m_WasGameStarted = false;
                m_IsPlayerReady = false;

                await DeleteAnyActiveLobbyWithNotify();
                if (this == null) return default;

                var options = new CreateLobbyOptions();
                options.IsPrivate = isPrivate;
                options.Data = new Dictionary<string, DataObject>
                {
                    { k_HostNameKey, new DataObject(DataObject.VisibilityOptions.Public, hostName) },
                    { k_RelayJoinCodeKey, new DataObject(DataObject.VisibilityOptions.Public, relayJoinCode) },
                };

                options.Player = CreatePlayerData();

                activeLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);
                if (this == null) return default;

                players = activeLobby?.Players;

                Log(activeLobby);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            return activeLobby;
        }

        public async Task DeleteAnyActiveLobbyWithNotify()
        {
            try
            {
                if (activeLobby != null && isHost)
                {
                    await LobbyService.Instance.DeleteLobbyAsync(activeLobby.Id);
                    if (this == null) return;

                    OnPlayerNotInLobby();
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public async Task DeleteActiveLobbyNoNotify()
        {
            try
            {
                if (activeLobby != null && isHost)
                {
                    await LobbyService.Instance.DeleteLobbyAsync(activeLobby.Id);
                    if (this == null) return;

                    activeLobby = null;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public async Task<List<Lobby>> GetUpdatedLobbiesList()
        {
            try
            {
                var lobbiesQuery = await LobbyService.Instance.QueryLobbiesAsync();
                if (this == null) return default;

                lobbiesList = lobbiesQuery.Results;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            return lobbiesList;
        }

        public async Task<Lobby> JoinLobby(string lobbyId, string playerName)
        {
            try
            {
                await PrepareToJoinLobby(playerName);
                if (this == null) return default;

                var options = new JoinLobbyByIdOptions();
                options.Player = CreatePlayerData();

                activeLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId, options);
                if (this == null) return default;

                players = activeLobby?.Players;
            }
            catch (LobbyServiceException e) when (e.Reason == LobbyExceptionReason.LobbyNotFound)
            {
                // Catch the lobby-not-found exception and rethrow so caller can pop a message.
                if (this == null) return null;

                activeLobby = null;

                throw;
            }
            catch (LobbyServiceException e) when (e.Reason == LobbyExceptionReason.LobbyFull)
            {
                if (this == null) return null;

                activeLobby = null;

                throw;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            return activeLobby;
        }

        public async Task<Lobby> JoinPrivateLobby(string lobbyJoinCode, string playerName)
        {
            try
            {
                await PrepareToJoinLobby(playerName);
                if (this == null) return default;

                var options = new JoinLobbyByCodeOptions();
                options.Player = CreatePlayerData();

                activeLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyJoinCode, options);
                if (this == null) return default;

                players = activeLobby?.Players;
            }
            catch (LobbyServiceException e) when (e.Reason == LobbyExceptionReason.LobbyNotFound)
            {
                if (this == null) return null;

                activeLobby = null;

                throw;
            }
            catch (LobbyServiceException e) when (e.Reason == LobbyExceptionReason.LobbyFull)
            {
                if (this == null) return null;

                activeLobby = null;

                throw;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            return activeLobby;
        }

        public async Task LeaveJoinedLobby()
        {
            try
            {
                await RemovePlayer(playerId);
                if (this == null) return;

                OnPlayerNotInLobby();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public async Task RemovePlayer(string playerId)
        {
            try
            {
                if (activeLobby != null)
                {
                    await LobbyService.Instance.RemovePlayerAsync(activeLobby.Id, playerId);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public async Task ToggleReadyState()
        {
            try
            {
                if (activeLobby == null)
                {
                    Debug.Log("Attempting to toggle ready state when not already in a lobby.");
                    return;
                }

                m_IsPlayerReady = !m_IsPlayerReady;

                var lobbyId = activeLobby.Id;

                var options = new UpdatePlayerOptions();
                options.Data = CreatePlayerDictionary();

                var updatedLobby = await LobbyService.Instance.UpdatePlayerAsync(lobbyId, playerId, options);
                if (this == null) return;

                UpdateLobby(updatedLobby);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public void OnGameStarted()
        {
            // When game starts actually starts, the host stops updating
            if (isHost)
            {
                m_WasGameStarted = true;
            }

            // When the game actually starts, all clients clear the active lobby. This is possible because the host will
            // actually delete the lobby itself once all clients have acknowledged that they've started.
            else
            {
                activeLobby = null;
            }
        }

        public void OnPlayerNotInLobby()
        {
            if (activeLobby != null)
            {
                activeLobby = null;

                OnPlayerNotInLobbyEvent?.Invoke();
            }
        }

        public string GetPlayerId(int playerIndex)
        {
            return players[playerIndex].Id;
        }

        public string GetPlayerName(int playerIndex)
        {
            var player = players[playerIndex].Data;
            return player[k_PlayerNameKey].Value;
        }

        async Task PrepareToJoinLobby(string playerName)
        {
            isHost = false;
            m_PlayerName = playerName;
            m_WasGameStarted = false;
            m_IsPlayerReady = false;

            if (activeLobby != null)
            {
                Debug.Log("Already in a lobby when attempting to join so leaving old lobby.");
                await LeaveJoinedLobby();
            }
        }

        Player CreatePlayerData()
        {
            var player = new Player();
            player.Data = CreatePlayerDictionary();

            return player;
        }

        Dictionary<string, PlayerDataObject> CreatePlayerDictionary()
        {
            var playerDictionary = new Dictionary<string, PlayerDataObject>
            {
                { k_PlayerNameKey,  new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, m_PlayerName) },
                { k_IsReadyKey,  new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, m_IsPlayerReady.ToString()) },
            };

            return playerDictionary;
        }

        void UpdateLobby(Lobby updatedLobby)
        {
            // Since this is called after an await, ensure that the Lobby wasn't closed while waiting.
            if (activeLobby == null || updatedLobby == null) return;

            if (DidPlayersChange(activeLobby.Players, updatedLobby.Players))
            {
                activeLobby = updatedLobby;
                players = activeLobby?.Players;

                if (updatedLobby.Players.Exists(player => player.Id == playerId))
                {
                    var isGameReady = IsGameReady(updatedLobby);

                    OnLobbyChanged?.Invoke(updatedLobby, isGameReady);
                }
                else
                {
                    ServerlessMultiplayerGameSampleManager.instance.SetReturnToMenuReason(
                        ServerlessMultiplayerGameSampleManager.ReturnToMenuReason.PlayerKicked);

                    OnPlayerNotInLobby();
                }
            }
        }

        static bool DidPlayersChange(List<Player> oldPlayers, List<Player> newPlayers)
        {
            if (oldPlayers.Count != newPlayers.Count)
            {
                return true;
            }

            for (int i = 0; i < newPlayers.Count; i++)
            {
                if (oldPlayers[i].Id != newPlayers[i].Id ||
                    oldPlayers[i].Data[k_IsReadyKey].Value != newPlayers[i].Data[k_IsReadyKey].Value)
                {
                    return true;
                }
            }

            return false;
        }

        static bool IsGameReady(Lobby lobby)
        {
            if (lobby.Players.Count <= 1)
            {
                return false;
            }

            foreach (var player in lobby.Players)
            {
                var isReady = bool.Parse(player.Data[k_IsReadyKey].Value);
                if (!isReady)
                {
                    return false;
                }
            }

            return true;
        }

        public static void Log(Lobby lobby)
        {
            if (lobby is null)
            {
                Debug.Log("No active lobby.");

                return;
            }

            var lobbyData = lobby.Data.Select(kvp => $"{kvp.Key} is {kvp.Value.Value}" );
            var lobbyDataStr = string.Join(", ", lobbyData);

            Debug.Log($"Lobby Named:{lobby.Name}, " +
                $"Players:{lobby.Players.Count}/{lobby.MaxPlayers}, " +
                $"IsPrivate:{lobby.IsPrivate}, " +
                $"IsLocked:{lobby.IsLocked}, " +
                $"LobbyCode:{lobby.LobbyCode}, " +
                $"Id:{lobby.Id}, " +
                $"Created:{lobby.Created}, " +
                $"HostId:{lobby.HostId}, " +
                $"EnvironmentId:{lobby.EnvironmentId}, " +
                $"Upid:{lobby.Upid}, " +
                $"Lobby.Data:{lobbyDataStr}");
        }

        public static void Log(string message, List<Lobby> lobbies)
        {
            if (lobbies.Count == 0)
            {
                Debug.Log($"{message}: No Lobbies found.");
            }
            else
            {
                Debug.Log($"{message}: Lobbies list:");
                foreach (var lobby in lobbies)
                {
                    Debug.Log($"  Lobby: {lobby.Name}, " +
                        $"players: {lobby.Players.Count}/{lobby.MaxPlayers}, " +
                        $"id:{lobby.Id}");
                }
            }
        }

        void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
