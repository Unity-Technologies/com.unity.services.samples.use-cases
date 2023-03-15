using System;
using UnityEngine;

namespace Unity.Services.Samples.CommandBatching
{
    public class DefeatRedEnemyCommand : Command
    {
        public new const string key = "COMMANDBATCH_DEFEAT_RED_ENEMY";

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
            Debug.Log("Processing Defeat Red Enemy Command Locally");
            DistributeRewardsLocally(rewards);
            GameStateManager.instance.SetIsOpenChestValidMove(true);
        }
    }
}
