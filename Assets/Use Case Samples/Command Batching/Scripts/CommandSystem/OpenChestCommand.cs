using System;
using UnityEngine;

namespace Unity.Services.Samples.CommandBatching
{
    public class OpenChestCommand : Command
    {
        public new const string key = "COMMANDBATCH_OPEN_CHEST";

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
            Debug.Log("Processing Open Chest Command Locally");
            DistributeRewardsLocally(rewards);
            GameStateManager.instance.SetIsOpenChestValidMove(false);
            GameStateManager.instance.SetIsAchieveBonusGoalValidMove(true);
        }
    }
}
