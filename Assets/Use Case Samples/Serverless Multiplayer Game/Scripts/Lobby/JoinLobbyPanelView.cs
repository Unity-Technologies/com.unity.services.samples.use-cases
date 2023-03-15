using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace Unity.Services.Samples.ServerlessMultiplayerGame
{
    public class JoinLobbyPanelView : LobbyPanelViewBase
    {
        [SerializeField]
        TextMeshProUGUI titleText;

        public void SetLobby(Lobby lobby)
        {
            titleText.text = lobby.Name;

            m_IsReady = false;

            SetPlayers(lobby.Players);
        }
    }
}
