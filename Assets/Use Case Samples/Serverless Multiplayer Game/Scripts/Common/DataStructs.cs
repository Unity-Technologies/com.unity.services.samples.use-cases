using System;
using System.Collections.Generic;

namespace Unity.Services.Samples.ServerlessMultiplayerGame
{
    [Serializable]
    public struct PlayerStats
    {
        public string playerName;
        public List<int> highScores;
        public int winCount;
        public int gameCount;

        public PlayerStats(string playerName)
        {
            this.playerName = playerName;
            highScores = new List<int>();
            winCount = 0;
            gameCount = 0;
        }

        public override string ToString()
        {
            var scores = highScores is null ? "" : string.Join(",", highScores.ToArray());
            return $"playerName:\"{playerName}\" highScores:[{scores}] wins:{winCount}/{gameCount}";
        }
    }

    [Serializable]
    public struct GameResultsData
    {
        public string winnerPlayerName;

        public string winnerPlayerId;

        public int winnerScore;

        public List<PlayerScoreData> playerScoreData;

        public override string ToString()
        {
            return $"Results: Winner:{winnerPlayerName} with {winnerScore} points, total players: {playerScoreData.Count}.";
        }
    }

    [Serializable]
    public struct PlayerScoreData
    {
        public string playerId;

        public string playerName;

        public int score;

        public PlayerScoreData(PlayerAvatar playerAvatar)
        {
            playerId = playerAvatar.playerId;
            playerName = playerAvatar.playerName;
            score = playerAvatar.score;
        }
    }
}
