using System.Linq;
using System.Text;
using TMPro;
using Unity.Services.Authentication;
using UnityEngine;

namespace Unity.Services.Samples.ServerlessMultiplayerGame
{
    public class GameResultsPanelView : PanelViewBase
    {
        [SerializeField]
        TextMeshProUGUI winnerText;

        [SerializeField]
        TextMeshProUGUI sessionScoresText;

        [SerializeField]
        TextMeshProUGUI personalStatsText;

        public void ShowResults(GameResultsData results)
        {
            ShowWinner(results);

            ShowSessionScores(results);

            ShowPersonalStats();
        }

        void ShowWinner(GameResultsData results)
        {
            if (results.winnerPlayerId == AuthenticationService.Instance.PlayerId)
            {
                winnerText.text = $"You won with {results.winnerScore} points!";
            }
            else
            {
                winnerText.text = $"{results.winnerPlayerName} won with {results.winnerScore} points.";
            }
        }

        void ShowSessionScores(GameResultsData results)
        {
            var scores = new StringBuilder();
            foreach (var scoreData in results.playerScoreData)
            {
                scores.Append($"{scoreData.playerName}: {scoreData.score}\n");
            }

            sessionScoresText.text = scores.ToString();
        }

        void ShowPersonalStats()
        {
            var playerStats = CloudSaveManager.instance.playerStats;
            var gamesCount = playerStats.gameCount;
            var winCount = playerStats.winCount;
            var highScores = playerStats.highScores;

            var stats = new StringBuilder();
            if (gamesCount == 1)
            {
                stats.Append($"Wins: {winCount} out of 1 game.\n");
            }
            else
            {
                stats.Append($"Wins: {winCount} out of {gamesCount} games.\n");
            }

            stats.Append("High Scores: ");
            stats.Append(string.Join(", ", highScores.Select(score => score.ToString()).ToArray()));

            personalStatsText.text = stats.ToString();
        }
    }
}
