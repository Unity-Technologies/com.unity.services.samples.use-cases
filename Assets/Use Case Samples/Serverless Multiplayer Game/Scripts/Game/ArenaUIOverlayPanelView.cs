using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace Unity.Services.Samples.ServerlessMultiplayerGame
{
    public class ArenaUIOverlayPanelView : PanelViewBase
    {
        [SerializeField]
        TextMeshProUGUI gameTimerText;

        [SerializeField]
        TextMeshProUGUI countdownText;

        [SerializeField]
        TextMeshProUGUI scoresText;

        public void ShowCountdown()
        {
            countdownText.gameObject.SetActive(true);
        }

        public void SetCountdown(int seconds)
        {
            countdownText.text = seconds.ToString();
        }

        public void HideCountdown()
        {
            countdownText.gameObject.SetActive(false);
        }

        public void ShowGameTimer(int seconds)
        {
            gameTimerText.text = seconds.ToString();
        }

        public void UpdateScores()
        {
            // If game is already over and client is about to return to results screen, we can skip updating scores if
            // the Game Network Manager has already been despawned.
            if (GameNetworkManager.instance == null)
            {
                return;
            }

            var playerAvatars = GameNetworkManager.instance.playerAvatars;

            scoresText.text = string.Join("\n",
                playerAvatars.Select(playerAvatar =>
                $"{playerAvatar.playerName}: {playerAvatar.score}").ToArray());
        }
    }
}
