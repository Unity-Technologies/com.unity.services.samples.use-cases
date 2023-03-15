using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Services.Samples.ServerlessMultiplayerGame
{
    public class LobbySceneManager : MonoBehaviour
    {
        [SerializeField]
        LobbySceneView sceneView;

        LobbyManager lobbyManager => LobbyManager.instance;
        bool isHost => lobbyManager.isHost;

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

            LobbyManager.OnLobbyChanged += OnLobbyChanged;
            LobbyManager.OnPlayerNotInLobbyEvent += OnPlayerNotInLobby;

            JoinLobby(LobbyManager.instance.activeLobby);
        }

        public void JoinLobby(Lobby lobbyJoined)
        {
            if (isHost)
            {
                sceneView.InitializeHostLobbyPanel();
                sceneView.ShowHostLobbyPanel();

                sceneView.SetLobbyCode(lobbyJoined.IsPrivate, lobbyJoined.LobbyCode);
                sceneView.SetHostLobbyPlayers(lobbyJoined.Players);
            }
            else
            {
                sceneView.ShowJoinLobbyPanel();
            }

            sceneView.SetJoinedLobby(lobbyJoined);

            sceneView.SetProfileDropdownIndex(ServerlessMultiplayerGameSampleManager.instance.profileDropdownIndex);

            sceneView.SetPlayerName(CloudSaveManager.instance.playerStats.playerName);

            sceneView.SetInteractable(true);
        }

        void OnLobbyChanged(Lobby updatedLobby, bool isGameReady)
        {
            if (isHost)
            {
                OnHostLobbyChanged(updatedLobby, isGameReady);
            }
            else
            {
                OnJoinLobbyChanged(updatedLobby, isGameReady);
            }
        }

        public void OnHostLobbyChanged(Lobby updatedLobby, bool isGameReady)
        {
            sceneView.SetHostLobbyPlayers(updatedLobby.Players);

            if (isGameReady)
            {
                NetworkManager.Singleton.SceneManager.LoadScene("Game", LoadSceneMode.Single);
            }
        }

        public void OnJoinLobbyChanged(Lobby updatedLobby, bool isGameReady)
        {
            sceneView.SetJoinLobbyPlayers(updatedLobby.Players);
        }

        public async void OnLobbyLeaveButtonPressed()
        {
            try
            {
                await LeaveLobby();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public void OnPlayerNotInLobby()
        {
            Debug.Log($"This player is no longer in the lobby so returning to main menu.");

            ReturnToMainMenu();
        }

        public async void OnReadyButtonPressed()
        {
            try
            {
                sceneView.SetInteractable(false);

                // Show immediate feedback for button press by changing ready state to predicted new state.
                // Note: This change will also be caught as a state change in the Lobby Manager which will cause
                //       the player's state to be forced to the correct state which will have no effect since
                //       we've already changed the check mark to the correctly-predicted final state.
                sceneView.ToggleReadyState(AuthenticationService.Instance.PlayerId);

                await LobbyManager.instance.ToggleReadyState();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                if (this != null)
                {
                    sceneView.SetInteractable(true);
                }
            }
        }

        public async void OnBootPlayerButtonPressed(PlayerIconView playerIcon)
        {
            try
            {
                sceneView.SetInteractable(false);

                var playerId = playerIcon.playerId;

                Debug.Log($"Booting player {playerId}");
                await LobbyManager.instance.RemovePlayer(playerIcon.playerId);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                if (this != null)
                {
                    sceneView.SetInteractable(true);
                }
            }
        }

        public async Task LeaveLobby()
        {
            try
            {
                sceneView.SetInteractable(false);

                if (LobbyManager.instance.isHost)
                {
                    await LobbyManager.instance.DeleteAnyActiveLobbyWithNotify();
                }
                else
                {
                    await LobbyManager.instance.LeaveJoinedLobby();
                }
                if (this == null) return;

                ReturnToMainMenu();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        void ReturnToMainMenu()
        {
            SceneManager.LoadScene("ServerlessMultiplayerGameSample");
        }

        void OnDestroy()
        {
            LobbyManager.OnLobbyChanged -= OnLobbyChanged;
            LobbyManager.OnPlayerNotInLobbyEvent -= OnPlayerNotInLobby;
        }
    }
}
