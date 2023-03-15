using System;
using UnityEngine;

namespace Unity.Services.Samples.CommandBatching
{
    public class GameStateManager : MonoBehaviour
    {
        public static GameStateManager instance { get; private set; }

        public bool isOpenChestValidMove { get; private set; }
        public bool isAchieveBonusGoalValidMove { get; private set; }
        public int turnsRemaining = k_TotalTurnCount;
        public int xp;
        public int goalsAchieved;

        const int k_TotalTurnCount = 6;

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

        public void SetUpNewGame()
        {
            turnsRemaining = k_TotalTurnCount;
            xp = CloudSaveManager.instance.GetCachedXP();
            goalsAchieved = CloudSaveManager.instance.GetCachedGoalsAchieved();

            SetIsOpenChestValidMove(false);
            SetIsAchieveBonusGoalValidMove(false);
        }

        public void SetIsOpenChestValidMove(bool valid)
        {
            isOpenChestValidMove = valid;
        }

        public void SetIsAchieveBonusGoalValidMove(bool valid)
        {
            isAchieveBonusGoalValidMove = valid;
        }

        public bool ConsumeTurnIfAnyAvailable()
        {
            if (turnsRemaining <= 0)
                return false;

            turnsRemaining -= 1;
            return true;
        }

        public bool IsGameOver()
        {
            return turnsRemaining <= 0;
        }

        void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
