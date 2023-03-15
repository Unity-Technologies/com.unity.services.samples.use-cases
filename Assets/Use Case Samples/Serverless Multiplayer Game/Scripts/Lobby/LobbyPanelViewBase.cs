using System.Collections.Generic;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.Services.Samples.ServerlessMultiplayerGame
{
    public class LobbyPanelViewBase : PanelViewBase
    {
        [SerializeField]
        LobbySceneView sceneView;

        [SerializeField]
        Transform playersContainer;

        [SerializeField]
        PlayerIconView playerIconPrefab;

        [SerializeField]
        Button readyButton;

        [SerializeField]
        Button notReadyButton;

        protected List<PlayerIconView> m_PlayerIcons = new List<PlayerIconView>();

        protected bool m_IsReady = false;

        // This sets the players visible in the view by removing any existing players and re-adding those listed.
        // Note that this method is virtual because we override it for the Host View so the boot buttons can be
        // manually activated for all joining players.
        public virtual void SetPlayers(List<Player> players)
        {
            RemoveAllPlayers();

            foreach (var player in players)
            {
                AddPlayer(player);
            }
        }

        public void TogglePlayerReadyState(string playerId)
        {
            foreach (var playerIcon in m_PlayerIcons)
            {
                if (playerIcon.playerId == playerId)
                {
                    m_IsReady = playerIcon.ToggleReadyState();
                }
            }
        }

        public override void SetInteractable(bool isInteractable)
        {
            base.SetInteractable(isInteractable);

            if (isInteractable)
            {
                readyButton.gameObject.SetActive(!m_IsReady);
                notReadyButton.gameObject.SetActive(m_IsReady);
            }
        }

        void AddPlayer(Player player)
        {
            var playerIcon = GameObject.Instantiate(playerIconPrefab, playersContainer);

            var playerId = player.Id;
            var playerName = player.Data[LobbyManager.k_PlayerNameKey].Value;
            var playerIndex = m_PlayerIcons.Count;
            var isReady = bool.Parse(player.Data[LobbyManager.k_IsReadyKey].Value);
            var color = sceneView.playerColors[playerIndex];
            var backgroundColor = sceneView.playerBackgroundColors[playerIndex];

            // Ensure that the player name is not profane and, if it is, sanitize it using asterisks.
            playerName = ProfanityManager.SanitizePlayerName(playerName);

            playerIcon.Initialize(playerId, playerName, playerIndex, isReady, color, backgroundColor);

            m_PlayerIcons.Add(playerIcon);
        }

        void RemoveAllPlayers()
        {
            foreach (var playerIcon in m_PlayerIcons)
            {
                Destroy(playerIcon.gameObject);
            }
            m_PlayerIcons.Clear();
        }
    }
}
