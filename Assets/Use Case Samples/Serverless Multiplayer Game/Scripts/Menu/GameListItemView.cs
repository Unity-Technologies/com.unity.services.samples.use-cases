using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.Services.Samples.ServerlessMultiplayerGame
{
    public class GameListItemView : MonoBehaviour
    {
        [SerializeField]
        TextMeshProUGUI titleText;

        [SerializeField]
        GameObject selectedIndicator;

        [SerializeField]
        Button selectorButton;

        [SerializeField]
        GameObject lobbyLockedIndicator;

        [SerializeField]
        TextMeshProUGUI lobbyPlayerCount;

        [SerializeField]
        TextMeshProUGUI lobbyMaxPlayers;

        public Lobby lobby { get; private set; }

        public int lobbyIndex { get; private set; }

        MenuSceneManager m_MenuSceneManager;

        public void SetData(MenuSceneManager menuSceneManager,
            Lobby lobby, int lobbyIndex, bool isSelected)
        {
            m_MenuSceneManager = menuSceneManager;

            this.lobby = lobby;
            this.lobbyIndex = lobbyIndex;

            titleText.text = lobby.Name;
            lobbyPlayerCount.text = $"{lobby.Players.Count}";
            lobbyMaxPlayers.text = $"{lobby.MaxPlayers}";

            selectedIndicator.SetActive(isSelected);
            lobbyLockedIndicator.SetActive(lobby.IsPrivate);
        }

        public void OnGameButtonPressed()
        {
            m_MenuSceneManager.OnGameSelectListItemButtonPressed(this);
        }

        public void SetSelected(bool isSelected)
        {
            selectedIndicator.SetActive(isSelected);
        }
    }
}
