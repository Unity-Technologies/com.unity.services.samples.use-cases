using System;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Services.Samples.ServerlessMultiplayerGame
{
    public class GameSceneManager : MonoBehaviour
    {
        [field: SerializeField]
        public GameSceneView sceneView { get; private set; }

        [SerializeField]
        GameNetworkManager gameNetworkManagerPrefab;

        public static GameSceneManager instance { get; private set; }

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

        void Start()
        {
            if (ServerlessMultiplayerGameSampleManager.instance == null)
            {
                Debug.LogError("Please be sure to start Play mode on the ServerlessMultiplayerGameSample scene.");

#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif

                return;
            }

            ShowArenaPanel();

            // The host instantiates the Game Manager which will control game play throughout this scene
            if (LobbyManager.instance.isHost)
            {
                GameNetworkManager.Instantiate(gameNetworkManagerPrefab);
            }

            // Since client scenes are actually loaded after the Game Manager is instantiated on host and propagated
            // to all clients, call method to ensure all scores are updated so players list will be visible from start.
            UpdateScores();
        }

        public void SetCountdown(int seconds)
        {
            sceneView.arenaUiOverlayPanelView.ShowCountdown();
            sceneView.arenaUiOverlayPanelView.SetCountdown(seconds);
        }

        public void HideCountdown()
        {
            sceneView.arenaUiOverlayPanelView.HideCountdown();
        }

        public void ShowGameTimer(int seconds)
        {
            sceneView.arenaUiOverlayPanelView.ShowGameTimer(seconds);
        }

        public void UpdateScores()
        {
            sceneView.UpdateScores();
        }

        void ShowArenaPanel()
        {
            sceneView.ShowArenaPanel();

            sceneView.SetProfileDropdownIndex(ServerlessMultiplayerGameSampleManager.instance.profileDropdownIndex);

            sceneView.SetPlayerName(CloudSaveManager.instance.playerStats.playerName);

            ShowInitialGameTime();

            sceneView.SetInteractable(true);
        }

        void ShowInitialGameTime()
        {
            var numPlayers = LobbyManager.instance.numPlayers;
            var playerOptions = RemoteConfigManager.instance.GetConfigForPlayers(numPlayers);
            sceneView.arenaUiOverlayPanelView.ShowGameTimer((int) playerOptions.gameDuration);
        }

        public void OnGameOver(GameResultsData results)
        {
            // Update player stats so they're available for the results Panel.
            // Note that we do not need to wait for async to finish writing as they won't be needed again until the
            // end of the next game anyway.
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            CloudSaveManager.instance.UpdatePlayerStats(results);
#pragma warning restore CS4014

            // Save off game results so they can be shown when we return to the main menu.
            // Note: This simplifies exiting the game since it can be gracefully-destructed right now without having
            // to worry about whether the host or client leaves first.
            ServerlessMultiplayerGameSampleManager.instance.SetPreviousGameResults(results);
        }

        public void OnGameLeaveButtonPressed()
        {
            NetworkServiceManager.instance.Uninitialize();

            GameEndManager.instance.ReturnToMainMenu();
        }

        public void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
