using System;
using UnityEngine;

namespace Unity.Services.Samples.CommandBatching
{
    public class AchieveBonusGoalCommand : Command
    {
        public new const string key = "COMMANDBATCH_ACHIEVE_BONUS_GOAL";

        public override void Execute()
        {
            CommandBatchSystem.instance.EnqueueCommand(this);
            ProcessCommandLocally();
        }

        public override string GetKey()
        {
            return key;
        }

        void ProcessCommandLocally()
        {
            var rewards = RemoteConfigManager.instance.commandRewards[key];
            Debug.Log("Processing Achieve Bonus Goal Command Locally");
            DistributeRewardsLocally(rewards);
            GameStateManager.instance.SetIsOpenChestValidMove(false);
        }
    }
}
