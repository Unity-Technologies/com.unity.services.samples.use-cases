using System.Collections.Generic;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace Unity.Services.Samples.ServerlessMultiplayerGame
{
    public class MenuSceneView : SceneViewBase
    {
        [SerializeField]
        MainMenuPanelView mainMenuPanelView;

        [SerializeField]
        HostSetupPanelView hostSetupPanelView;

        [SerializeField]
        GameSelectPanelView gameSelectPanelView;

        [SerializeField]
        JoinPrivateLobbyPanelView joinPrivateLobbyPanelView;

        [SerializeField]
        GameResultsPanelView gameResultsPanelView;

        public void ShowMainMenuPanel()
        {
            ShowPanel(mainMenuPanelView);
        }

        public void ShowHostSetupPanel()
        {
            ShowPanel(hostSetupPanelView);
        }

        public void ShowGameSelectPanel()
        {
            ShowPanel(gameSelectPanelView);
        }

        public bool IsGameSelectPanelVisible()
        {
            return IsPanelVisible(gameSelectPanelView);
        }

        public void ShowJoinPrivateLobbyPanel()
        {
            joinPrivateLobbyPanelView.ClearGameCode();

            ShowPanel(joinPrivateLobbyPanelView);
        }

        public override void SetPlayerName(string playerName)
        {
            base.SetPlayerName(playerName);

            mainMenuPanelView.SetLocalPlayerName(playerName);
        }

        public void SetMaxPlayers(int maxPlayers)
        {
            hostSetupPanelView.SetMaxPlayers(maxPlayers);
        }

        public void SetPrivateLobbyFlag(bool privateGameFlag)
        {
            hostSetupPanelView.SetPrivateGameFlag(privateGameFlag);
        }

        public void SetLobbyName(string gameName)
        {
            hostSetupPanelView.SetGameName(gameName);
        }

        public void SetLobbies(List<Lobby> lobbies)
        {
            gameSelectPanelView.SetLobbies(lobbies);
        }

        public void UpdateLobbies(List<Lobby> lobbies)
        {
            gameSelectPanelView.UpdateLobbies(lobbies);
        }

        public void SetLobbyIndexSelected(int lobbyIndex)
        {
            gameSelectPanelView.SetLobbyIndexSelected(lobbyIndex);
        }

        public void ClearLobbySelection()
        {
            gameSelectPanelView.ClearLobbySelection();
        }

        public void ShowGameResultsPanel(GameResultsData results)
        {
            gameResultsPanelView.ShowResults(results);

            ShowPanel(gameResultsPanelView);
        }
    }
}
