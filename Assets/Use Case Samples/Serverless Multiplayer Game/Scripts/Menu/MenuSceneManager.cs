using System;
using System.Threading.Tasks;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Services.Samples.ServerlessMultiplayerGame
{
    public class MenuSceneManager : MonoBehaviour
    {
        float k_UpdateLobbiesListInterval = 1.5f;

        [SerializeField]
        MenuSceneView sceneView;

        [SerializeField]
        JoinPrivateLobbyPanelView joinPrivateLobbyPanel;

        ServerlessMultiplayerGameSampleManager sampleManager => ServerlessMultiplayerGameSampleManager.instance;

        string playerName => CloudSaveManager.instance.playerName;

        int m_HostMaxPlayers = 2;

        bool m_HostPrivateLobbyFlag = false;

        string m_HostLobbyName;

        float m_UpdateLobbiesListTime = float.MaxValue;

        int m_LobbyIndexSelected = -1;

        // Lobby name is not valid at startup (never constructed) or when player name changes.
        bool m_IsLobbyNameValid = false;

        void Start()
        {
            // The first time the sample is run, the sample manager will request that the main menu be shown, on subsequent
            // returns to main menu, the sample manager will already be initialized so we will show the menu here.
            if (sampleManager.isInitialized)
            {
                ShowMainMenu();
            }
        }

        async void Update()
        {
            try
            {
                if (Time.time >= m_UpdateLobbiesListTime)
                {
                    // Turn off lobbies-list updating. We will only turn it back on if still on lobby select Panel AND
                    // we've finished updating the lobbies list.
                    m_UpdateLobbiesListTime = float.MaxValue;

                    // If not showing the game select Panel then turn off updating.
                    if (!sceneView.IsGameSelectPanelVisible())
                    {
                        return;
                    }

                    var lobbies = await LobbyManager.instance.GetUpdatedLobbiesList();
                    if (this == null) return;

                    // If player left the lobby-select Panel while updating lobbies list then turn off updating and ignore the result.
                    if (!sceneView.IsGameSelectPanelVisible())
                    {
                        return;
                    }

                    // Update the lobbies list, clear lobby selection if no longer valid.
                    sceneView.UpdateLobbies(lobbies);

                    m_UpdateLobbiesListTime = Time.time + k_UpdateLobbiesListInterval;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public async void OnProfileDropDownChanged(int dropdownSelectionIndex)
        {
            try
            {
                sceneView.SetInteractable(false);

                await sampleManager.SignInAndInitialize(dropdownSelectionIndex);

                sceneView.SetProfileDropdownIndex(ServerlessMultiplayerGameSampleManager.instance.profileDropdownIndex);

                sceneView.SetPlayerName(playerName);

                // Since we changed to a different profile, update the name field to reflect new player name.
                OnPlayerNameChanged();
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

        public void OnShowMainMenuPanelButtonPressed()
        {
            ShowMainMenu();
        }

        public void ShowMainMenu()
        {
            sceneView.SetPlayerName(playerName);

            sceneView.SetProfileDropdownIndex(ServerlessMultiplayerGameSampleManager.instance.profileDropdownIndex);

            if (ServerlessMultiplayerGameSampleManager.instance.arePreviousGameResultsSet)
            {
                sceneView.ShowGameResultsPanel(ServerlessMultiplayerGameSampleManager.instance.previousGameResults);

                ServerlessMultiplayerGameSampleManager.instance.ClearPreviousGameResults();
            }
            else
            {
                NetworkServiceManager.instance.Uninitialize();

                sceneView.ShowMainMenuPanel();
            }

            ShowReturnToLobbyReasonPopupIfNecessary();

            sceneView.SetInteractable(true);
        }

        public async void OnMainMenuRandomizePlayerNameButtonPressed()
        {
            try
            {
                sceneView.SetInteractable(false);

                string newLocalPlayerName;
                do
                {
                    newLocalPlayerName = PlayerNameManager.GenerateRandomName();
                }
                while (newLocalPlayerName == playerName);

                await CloudSaveManager.instance.SetPlayerName(newLocalPlayerName);
                if (this == null) return;

                sceneView.SetPlayerName(newLocalPlayerName);

                // Since the player name changed, we need to generate a new lobby name
                OnPlayerNameChanged();

                Debug.Log($"Randomized player. Updated playerStats:{CloudSaveManager.instance.playerStats}");
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

        public void OnPlayerNameChanged()
        {
            // Since the player name changed, we need to generate a new lobby name
            m_IsLobbyNameValid = false;
        }

        public void OnMainMenuHostGameButtonPressed()
        {
            if (!m_IsLobbyNameValid)
            {
                OnHostSetupRandomizeLobbyNameButtonPressed();
            }

            sceneView.SetMaxPlayers(m_HostMaxPlayers);

            sceneView.SetPrivateLobbyFlag(m_HostPrivateLobbyFlag);

            sceneView.ShowHostSetupPanel();

            sceneView.SetInteractable(true);
        }

        public async void OnMainMenuJoinPublicGameButtonPressed()
        {
            try
            {
                // Clear any old lobby selection so nothing is selected when player first enters the lobby.
                ClearLobbySelection();

                var lobbies = await LobbyManager.instance.GetUpdatedLobbiesList();
                if (this == null) return;

                sceneView.SetLobbies(lobbies);
                LobbyManager.Log("Showing game select panel", lobbies);

                sceneView.ShowGameSelectPanel();

                m_UpdateLobbiesListTime = Time.time + k_UpdateLobbiesListInterval;
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

        public void OnMainMenuJoinPrivateGameButtonPressed()
        {
            sceneView.ShowJoinPrivateLobbyPanel();

            sceneView.SetInteractable(true);
        }

        public async void OnHostSetupConfirmButtonPressed()
        {
            try
            {
                sceneView.SetInteractable(false);

                var relayJoinCode = await NetworkServiceManager.instance.InitializeHost(m_HostMaxPlayers);
                if (this == null) return;

                var lobby = await LobbyManager.instance.CreateLobby(m_HostLobbyName,
                    m_HostMaxPlayers, playerName, m_HostPrivateLobbyFlag, relayJoinCode);
                if (this == null) return;

                SceneManager.LoadScene("Lobby");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public void OnHostSetMaxPlayersTogglePressed(int maxPlayers)
        {
            m_HostMaxPlayers = maxPlayers;

            sceneView.SetMaxPlayers(maxPlayers);
        }

        public void OnHostSetPrivateLobbyTogglePressed()
        {
            m_HostPrivateLobbyFlag = true;

            sceneView.SetPrivateLobbyFlag(m_HostPrivateLobbyFlag);
        }

        public void OnHostSetPublicLobbyTogglePressed()
        {
            m_HostPrivateLobbyFlag = false;

            sceneView.SetPrivateLobbyFlag(m_HostPrivateLobbyFlag);
        }

        public void OnHostSetupRandomizeLobbyNameButtonPressed()
        {
            var oldLobbyName = m_HostLobbyName;

            do
            {
                m_HostLobbyName = LobbyNameManager.GenerateRandomName(playerName);
            }
            while (m_HostLobbyName == oldLobbyName);

            sceneView.SetLobbyName(m_HostLobbyName);

            m_IsLobbyNameValid = true;
        }

        public void OnGameSelectListItemButtonPressed(GameListItemView gameListItem)
        {
            var lobbyIndex = gameListItem.lobbyIndex;

            if (lobbyIndex == m_LobbyIndexSelected)
            {
                ClearLobbySelection();
            }
            else
            {
                UpdateLobbySelectionIndex(lobbyIndex);
            }
        }

        public void UpdateLobbySelectionIndex(int lobbyIndex)
        {
            m_LobbyIndexSelected = lobbyIndex;

            if (lobbyIndex < 0)
            {
                ClearLobbySelection();
            }
            else
            {
                sceneView.SetLobbyIndexSelected(lobbyIndex);
            }
        }

        // Called by view when filter causes the current selection to no longer be valid
        public void ClearLobbySelection()
        {
            m_LobbyIndexSelected = -1;

            sceneView.ClearLobbySelection();
        }

        public async void OnGameSelectJoinButtonPressed()
        {
            try
            {
                sceneView.SetInteractable(false);

                var lobbiesList = LobbyManager.instance.lobbiesList;
                if (m_LobbyIndexSelected >= 0 && m_LobbyIndexSelected <= lobbiesList.Count)
                {
                    var lobbyToJoin = lobbiesList[m_LobbyIndexSelected];
                    var lobbyJoined = await LobbyManager.instance.JoinLobby(lobbyToJoin.Id, playerName);
                    if (this == null) return;

                    // If lobby no longer exists (i.e. host left) then popup
                    if (lobbyJoined == null)
                    {
                        ShowLobbyNotFoundPopup();
                    }
                    else
                    {
                        await OpenLobby(lobbyJoined);
                   }
               }
            }
            catch (LobbyServiceException e) when (e.Reason == LobbyExceptionReason.LobbyNotFound)
            {
                ShowLobbyNotFoundPopup();
            }
            catch (LobbyServiceException e) when (e.Reason == LobbyExceptionReason.LobbyFull)
            {
                ShowLobbyFullPopup();
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

        public async void OnPrivateGameJoinButtonPressed()
        {
            try
            {
                if (joinPrivateLobbyPanel.isGameCodeValid)
                {
                    var lobbyJoined = await LobbyManager.instance.JoinPrivateLobby(
                        joinPrivateLobbyPanel.gameCode, playerName);
                    if (this == null) return;

                    if (lobbyJoined == null)
                    {
                        ShowInvalidCodePopup();
                        return;
                    }

                    await OpenLobby(lobbyJoined);
                }
            }
            catch (LobbyServiceException e) when (e.Reason == LobbyExceptionReason.LobbyNotFound)
            {
                ShowInvalidCodePopup();
            }
            catch (LobbyServiceException e) when (e.Reason == LobbyExceptionReason.LobbyFull)
            {
                ShowLobbyFullPopup();
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

        async Task InitializeRelayClient(Lobby lobbyJoined)
        {
            try
            {
                var relayJoinCode = lobbyJoined.Data[LobbyManager.k_RelayJoinCodeKey].Value;
                await NetworkServiceManager.instance.InitializeClient(relayJoinCode);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        async Task OpenLobby(Lobby lobbyJoined)
        {
            try
            {
                await InitializeRelayClient(lobbyJoined);
                if (this == null) return;

                SceneManager.LoadScene("Lobby");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        void ShowReturnToLobbyReasonPopupIfNecessary()
        {
            switch (ServerlessMultiplayerGameSampleManager.instance.returnToMenuReason)
            {
                case ServerlessMultiplayerGameSampleManager.ReturnToMenuReason.PlayerKicked:
                    ShowPlayerKickedPopup();
                    break;

                case ServerlessMultiplayerGameSampleManager.ReturnToMenuReason.LobbyClosed:
                    ShowLobbyClosedPopup();
                    break;

                case ServerlessMultiplayerGameSampleManager.ReturnToMenuReason.HostLeftGame:
                    ShowHostLeftGamePopup();
                    break;
            }

            ServerlessMultiplayerGameSampleManager.instance.ClearReturnToMenuReason();
        }

        void ShowInvalidCodePopup()
        {
            sceneView.ShowPopup("Invalid Game Code", "The Game Code you entered is invalid.\n\nPlease try again.");
        }

        void ShowLobbyNotFoundPopup()
        {
            sceneView.ShowPopup("Invalid Lobby", "The lobby you attempted to join no longer exists.\n\nPlease try again.");
        }

        void ShowLobbyFullPopup()
        {
            sceneView.ShowPopup("Lobby Full", "The lobby you attempted to join is full.\n\nPlease try a different lobby.");
        }

        void ShowPlayerKickedPopup()
        {
            sceneView.ShowPopup("Kicked", "The host has kicked you from the lobby.\n\nPlease rejoin or try a different lobby.");
        }

        void ShowLobbyClosedPopup()
        {
            sceneView.ShowPopup("Lobby Closed", "The lobby you joined has been closed.\n\nPlease try a different lobby.");
        }

        void ShowHostLeftGamePopup()
        {
            sceneView.ShowPopup("Host Left", "The host has left the game causing it to be aborted.\n\nPlease try a different game.");
        }
    }
}
