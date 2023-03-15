using System;
using UnityEngine;

namespace Unity.Services.Samples.CommandBatching
{
    public class DefeatBlueEnemyCommand : Command
    {
        public new const string key = "COMMANDBATCH_DEFEAT_BLUE_ENEMY";

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
            Debug.Log("Processing Defeat Blue Enemy Command Locally");
            DistributeRewardsLocally(rewards);
            GameStateManager.instance.SetIsOpenChestValidMove(true);
        }
    }
}
