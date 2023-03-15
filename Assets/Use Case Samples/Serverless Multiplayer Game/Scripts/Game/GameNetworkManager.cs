using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Services.Samples.ServerlessMultiplayerGame
{
    [DisallowMultipleComponent]
    public class GameNetworkManager : NetworkBehaviour
    {
        // Countdown lasts 3 seconds.
        const int k_CountdownDuration = 3;

        // Time to wait before exiting game if network object for player avatar isn't created.
        const float k_ClientTimeoutDelay = k_CountdownDuration + 5;

        public static GameNetworkManager instance { get; private set; }

        [SerializeField]
        float m_PlayerMinRadius = 1.5f;

        [SerializeField]
        float m_PlayerRadiusIncreasePerPlayer = 1;

        [field: SerializeField]
        public NetworkObject networkObject { get; private set; }

        [SerializeField]
        PlayerAvatar[] playerAvatarPrefabs;

        // The host is always the first connected client in the Network Manager.
        public static ulong hostRelayClientId => NetworkManager.Singleton.ConnectedClients[0].ClientId;

        public List<PlayerAvatar> playerAvatars { get; private set; } = new List<PlayerAvatar>();

        PlayerAvatar m_LocalPlayerAvatar;

        float m_ClientTimeout = float.MaxValue;

        bool m_IsCountdownActive = true;

        float m_GameCountdownEndsTime = float.MaxValue;

        RemoteConfigManager.PlayerOptionsConfig m_PlayerOptions;

        int m_LastGameTimerShown;

        float m_GameEndsTime = float.MaxValue;

        // Only send updated countdown value when it changes.
        int m_PreviousCountdownSeconds;

        // Be sure to stop all processing once local player avatar is removed.
        bool m_IsShuttingDown = false;

        // We count responses for all players to know when all are in game so we can destroy the lobby.
        int m_NumStartGameAcknowledgments = 0;

        // Wait for all clients to acknowledge game results before returning to main menu / results Panel
        int m_NumGameOverAcknowledgments = 0;

        static public void Instantiate(GameNetworkManager gameManagerPrefab)
        {
            var gameManager = GameObject.Instantiate(gameManagerPrefab);
            gameManager.networkObject.SpawnWithOwnership(hostRelayClientId);
        }

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

        // When the GameManager is instantiated, the game is ready to begin. This kicks off the game by
        // intantiating player avatars and starting the countdown (host only).
        void Start()
        {
            if (IsHost)
            {
                InitializeHostGame();
            }
            else
            {
                InitializeClientGame();
            }
        }

        void Update()
        {
            // Be sure to stop all processing once local player avatar is removed.
            if (m_IsShuttingDown) return;

            if (DidClientTimeout())
            {
                Debug.Log("Client timed out so shutting down.");
                Shutdown();

                return;
            }

            if (IsHost)
            {
                UpdateHost();
            }
        }

        bool DidClientTimeout()
        {
            if (Time.realtimeSinceStartup >= m_ClientTimeout)
            {
                if (m_LocalPlayerAvatar == null)
                {
                    return true;
                }
            }

            return false;
        }

        void UpdateHost()
        {
            if (m_IsCountdownActive)
            {
                HostUpdateCountdown();
            }
            else
            {
                if (Time.time >= m_GameEndsTime)
                {
                    GameEndManager.instance?.HostGameOver();
                }
                else
                {
                    GameCoinManager.instance?.HostHandleSpawningCoins();

                    HostUpdateGameTime();
                }
            }
        }

        public void InitializeHostGame()
        {
            Debug.Log("Host starting game...");

            SpawnAllPlayers();

            InitializeGame();
        }

        public void InitializeClientGame()
        {
            Debug.Log("Client starting game...");

            InitializeGame();
        }

        void InitializeGame()
        {
            // Set a timeout if we have not yet setup the avatar. If we have, we'll watch to ensure it isn't
            // destroyed by host. Either way, we return to the lobby if the avatar is missing after timeout.
            m_ClientTimeout = m_LocalPlayerAvatar == null ? Time.realtimeSinceStartup + k_ClientTimeoutDelay : 0;

            m_IsCountdownActive = true;
            m_GameCountdownEndsTime = Time.time + k_CountdownDuration;

            var numPlayers = LobbyManager.instance.numPlayers;
            m_PlayerOptions = RemoteConfigManager.instance.GetConfigForPlayers(numPlayers);

            LobbyManager.instance.OnGameStarted();

            // Inform host that this player has started the game. Once all players have started (and thus
            // stopped using the lobby they joined with) the lobby will be deleted by the host.
            PlayerStartedGameServerRpc();
        }

        void SpawnAllPlayers()
        {
            var connectedClients = NetworkManager.Singleton.ConnectedClients;
            var numPlayers = connectedClients.Count();

            var angle = UnityEngine.Random.Range(0, Mathf.PI * 2);
            var spacing = Mathf.PI * 2 / numPlayers;
            var radius = m_PlayerMinRadius + m_PlayerRadiusIncreasePerPlayer * numPlayers;

            var playerIndex = 0;
            foreach (var relayClientId in connectedClients.Keys)
            {
                var position = new Vector3(Mathf.Cos(angle) * radius, 0,
                    Mathf.Sin(angle) * radius);

                SpawnPlayer(playerIndex, relayClientId, position);

                angle += spacing;

                playerIndex++;
            }
        }

        void SpawnPlayer(int playerIndex, ulong relayClientId, Vector3 position)
        {
            var playerAvatarPrefab = playerAvatarPrefabs[playerIndex];
            var playerAvatar = GameObject.Instantiate(playerAvatarPrefab, position, Quaternion.identity);

            playerAvatar.networkObject.SpawnWithOwnership(relayClientId);

            var playerId = LobbyManager.instance.GetPlayerId(playerIndex);
            var playerName = LobbyManager.instance.GetPlayerName(playerIndex);
            playerAvatar.SetPlayerAvatarClientRpc(playerIndex, playerId, playerName, relayClientId);
        }

        public void AddPlayerAvatar(PlayerAvatar playerAvatar, bool isLocalPlayer)
        {
            playerAvatars.Add(playerAvatar);

            if (isLocalPlayer)
            {
                m_LocalPlayerAvatar = playerAvatar;
            }
        }

        [ServerRpc(RequireOwnership = false)]
        void PlayerStartedGameServerRpc()
        {
            m_NumStartGameAcknowledgments++;
            if (m_NumStartGameAcknowledgments >= playerAvatars.Count)
            {
                // Delete and clear active lobby on this host (i.e. server). Note that we do not await since we are entering starting
                // the game now and do not need to act on deletion or confirm that it's successfully deleted. If it fails for any
                // reason (which it shouldn't) the lobby will simply time out and disappear anyway.
#pragma warning disable CS4014  // Because this call is not awaited, execution of the current method continues before the call is completed
                LobbyManager.instance.DeleteActiveLobbyNoNotify();
#pragma warning restore CS4014
            }
        }

        void StartPlayingGame()
        {
            m_GameEndsTime = Time.time + m_PlayerOptions.gameDuration;

            GameCoinManager.instance?.Initialize(IsHost, m_PlayerOptions);

            GameCoinManager.instance?.StartTimerToSpawnCoins();

            GameSceneManager.instance?.HideCountdown();

            if (m_LocalPlayerAvatar != null)
            {
                m_LocalPlayerAvatar.AllowMovement();
            }
        }

        void OnScoreChanged()
        {
            GameSceneManager.instance?.UpdateScores();
        }

        void Shutdown()
        {
            Debug.Log($"Local player's avatar disappeared or didn't appear so returning to lobby.");

            // Be sure to stop all processing once local player avatar is removed.
            m_IsShuttingDown = true;

            GameEndManager.instance?.ReturnToMainMenu();
        }

        void HostUpdateCountdown()
        {
            var countdownSeconds = (int)Mathf.Ceil(m_GameCountdownEndsTime - Time.time);

            if (countdownSeconds != m_PreviousCountdownSeconds)
            {
                m_PreviousCountdownSeconds = countdownSeconds;

                UpdateCountdownClientRpc(countdownSeconds);

                if (countdownSeconds <= 0)
                {
                    m_IsCountdownActive = false;
                }
            }
        }

        [ClientRpc]
        void UpdateCountdownClientRpc(int seconds)
        {
            GameSceneManager.instance?.SetCountdown(seconds);

            if (seconds <= 0)
            {
                StartPlayingGame();
            }

            // Refresh player names and starting scores each second to ensure the list is populated.
            OnScoreChanged();
        }

        void HostUpdateGameTime()
        {
            var timeRemaining = Mathf.CeilToInt(m_GameEndsTime - Time.time);
            if (timeRemaining != m_LastGameTimerShown)
            {
                m_LastGameTimerShown = timeRemaining;

                UpdateGameTimerClientRpc(timeRemaining);
            }
        }

        [ClientRpc]
        void UpdateGameTimerClientRpc(int seconds)
        {
            GameSceneManager.instance?.ShowGameTimer(seconds);
        }

        public void OnGameOver(string gameResultsJson)
        {
            GameOverClientRpc(gameResultsJson);
        }

        [ClientRpc]
        void GameOverClientRpc(string gameResultsJson)
        {
            m_IsShuttingDown = true;

            GameSceneManager.instance?.ShowGameTimer(0);

            // By using the results passed from host, we ensure all players show the same results and allow
            // the host to pick a random winner if players tie.
            var results = JsonUtility.FromJson<GameResultsData>(gameResultsJson);
            GameSceneManager.instance?.OnGameOver(results);

            GameOverAcknowledgedServerRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        void GameOverAcknowledgedServerRpc()
        {
            m_NumGameOverAcknowledgments++;
            if (m_NumGameOverAcknowledgments >= playerAvatars.Count)
            {
                networkObject.Despawn(true);

                // Load the main menu. Note that this will cause the host and all clients to change scenes
                // which will automatically cause this GameManager to be destroyed (including all mirrored
                // Network Objects on all clients).
                NetworkManager.Singleton.SceneManager.LoadScene("ServerlessMultiplayerGameSample", LoadSceneMode.Single);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void OnPlayerAvatarDestroyedServerRpc(ulong playerRelayId)
        {
            playerAvatars.RemoveAll(avatar => avatar.playerRelayId == playerRelayId);

            // Update scores to remove player's name/score from the scoreboard.
            if (!m_IsShuttingDown)
            {
                OnScoreChanged();
            }
        }

        public override void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }

            base.OnDestroy();

            // When game manager is destroyed, return player to main menu (unless already doing so).
            if (!m_IsShuttingDown)
            {
                GameEndManager.instance?.ReturnToMainMenu();
            }
        }
    }
}
