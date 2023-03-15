using System.Collections.Generic;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace Unity.Services.Samples.ServerlessMultiplayerGame
{
    public class HostLobbyPanelView : LobbyPanelViewBase
    {
        [SerializeField]
        LobbySceneManager lobbySceneManager;

        [SerializeField]
        GameObject lobbyTextCodeContainer;

        [SerializeField]
        TextMeshProUGUI lobbyCodeText;

        public void InitializeHostLobbyPanel()
        {
            m_IsReady = false;
        }

        public void SetLobbyCode(bool isVisible, string lobbyCode)
        {
            lobbyTextCodeContainer.SetActive(isVisible);
            lobbyCodeText.text = lobbyCode;
        }

        public override void SetPlayers(List<Player> players)
        {
            // Disconnect all previous host 'boot' buttons (remove listeners, remove from all-selectables list).
            DisableBootButtons();

            // Refresh all player icons.
            base.SetPlayers(players);

            // Connect all new host 'boot' buttons (enable buttons, add listeners, add to all-selectables list).
            EnableBootButtons();
        }

        void EnableBootButtons()
        {
            // We start with player 1 since the first player is always the Host who cannot be booted.
            for (var i = 1; i < m_PlayerIcons.Count; i++)
            {
                var playerIcon = m_PlayerIcons[i];
                var bootButton = playerIcon.BootButton;

                AddSelectable(bootButton);

                bootButton.onClick.AddListener(() =>
                    lobbySceneManager.OnBootPlayerButtonPressed(playerIcon));

                playerIcon.EnableHostBootButton();
            }
        }

        void DisableBootButtons()
        {
            for (var i = 1; i < m_PlayerIcons.Count; i++)
            {
                var playerIcon = m_PlayerIcons[i];
                var bootButton = playerIcon.BootButton;

                RemoveSelectable(bootButton);

                bootButton.onClick.RemoveListener(() =>
                    lobbySceneManager.OnBootPlayerButtonPressed(playerIcon));
            }
        }
    }
}
