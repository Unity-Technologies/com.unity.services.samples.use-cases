using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Services.Samples.ServerlessMultiplayerGame
{
    [DisallowMultipleComponent]
    public class GameEndManager : MonoBehaviour
    {
        public static GameEndManager instance;

        [SerializeField]
        GameSceneManager gameSceneManager;

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
            }
            else
            {
                instance = this;
            }
        }

        public void HostGameOver()
        {
            var gameResultsData = CompileScores();

            gameResultsData = DetermineWinner(gameResultsData);

            var gameResultsJson = JsonUtility.ToJson(gameResultsData);

            gameSceneManager.ShowGameTimer(0);

            // By using the results passed from host, we ensure all players show the same results and allow
            // the host to pick a random winner if players tie.
            var results = JsonUtility.FromJson<GameResultsData>(gameResultsJson);
            GameNetworkManager.instance?.OnGameOver(gameResultsJson);
        }

        GameResultsData CompileScores()
        {
            var playerScores = new List<PlayerScoreData>();
            var gameResultsData = new GameResultsData()
            {
                playerScoreData = playerScores,
            };

            var playerAvatars = GameNetworkManager.instance.playerAvatars;
            foreach (var playerAvatar in playerAvatars)
            {
                playerScores.Add(new PlayerScoreData(playerAvatar));
            }

            return gameResultsData;
        }

        GameResultsData DetermineWinner(GameResultsData gameResultsData)
        {
            gameResultsData.winnerScore = int.MinValue;

            // We count ties so we can randomly select a winner based on number of tieing players. For example, if
            // 3 players tie, each has a 33% chance so, when we encounter the 3rd tie, we give them a 1 in 3 chance.
            var numTies = 1;

            var playerAvatars = GameNetworkManager.instance.playerAvatars;
            foreach (var playerAvatar in playerAvatars)
            {
                if (playerAvatar.score > gameResultsData.winnerScore)
                {
                    gameResultsData.winnerPlayerName = playerAvatar.playerName;
                    gameResultsData.winnerPlayerId = playerAvatar.playerId;
                    gameResultsData.winnerScore = playerAvatar.score;
                    numTies = 1;
                }
                else if (playerAvatar.score == gameResultsData.winnerScore)
                {
                    // Base chance of each new tieing player winning on count of players that have tied so, if this
                    // is the 2nd tie, they're given a 1 in 2 chance of winning, and the 3rd receives a 1 in 3.
                    numTies++;
                    if (UnityEngine.Random.Range(0, numTies) == 0)
                    {
                        gameResultsData.winnerPlayerName = playerAvatar.playerName;
                        gameResultsData.winnerPlayerId = playerAvatar.playerId;
                        gameResultsData.winnerScore = playerAvatar.score;
                    }
                }
            }

            return gameResultsData;
        }

        public void ReturnToMainMenu()
        {
            SceneManager.LoadScene("ServerlessMultiplayerGameSample");
        }

        public void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
