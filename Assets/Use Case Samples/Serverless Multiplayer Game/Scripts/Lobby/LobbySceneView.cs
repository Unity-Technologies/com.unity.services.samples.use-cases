using System.Collections.Generic;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace Unity.Services.Samples.ServerlessMultiplayerGame
{
    public class LobbySceneView : SceneViewBase
    {
        [SerializeField]
        HostLobbyPanelView hostLobbyPanelView;

        [SerializeField]
        JoinLobbyPanelView joinLobbyPanelView;

        [field: SerializeField]
        public Color[] playerColors { get; private set; }

        [field: SerializeField]
        public Color[] playerBackgroundColors { get; private set; }

        public void InitializeHostLobbyPanel()
        {
            hostLobbyPanelView.InitializeHostLobbyPanel();
        }

        public void ShowHostLobbyPanel()
        {
            ShowPanel(hostLobbyPanelView);
        }

        public void SetLobbyCode(bool isVisible, string lobbyCode)
        {
            hostLobbyPanelView.SetLobbyCode(isVisible, lobbyCode);
        }

        public void SetHostLobbyPlayers(List<Player> players)
        {
            hostLobbyPanelView.SetPlayers(players);
        }

        public void ShowJoinLobbyPanel()
        {
            ShowPanel(joinLobbyPanelView);
        }

        public void SetJoinedLobby(Lobby lobby)
        {
            joinLobbyPanelView.SetLobby(lobby);
        }

        public void SetJoinLobbyPlayers(List<Player> players)
        {
            joinLobbyPanelView.SetPlayers(players);
        }

        public void ToggleReadyState(string playerId)
        {
            if (m_CurrentPanelView == hostLobbyPanelView)
            {
                hostLobbyPanelView.TogglePlayerReadyState(playerId);
            }
            else
            {
                joinLobbyPanelView.TogglePlayerReadyState(playerId);
            }
        }
    }
}
