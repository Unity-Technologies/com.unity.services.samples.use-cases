using System;
using UnityEngine;

namespace Unity.Services.Samples.CommandBatching
{
    public class GameOverCommand : Command
    {
        public new const string key = "COMMANDBATCH_GAME_OVER";

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
            Debug.Log("Processing Game Over Command Locally");
            DistributeRewardsLocally(rewards);
        }
    }
}
