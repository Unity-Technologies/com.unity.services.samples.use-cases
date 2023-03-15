using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Services.Samples.CommandBatching
{
    public abstract class Command
    {
        public const string key = "";

        public abstract void Execute();
        public abstract string GetKey();

        internal void DistributeRewardsLocally(List<RemoteConfigManager.Reward> rewards)
        {
            foreach (var reward in rewards)
            {
                switch (reward.service)
                {
                    case "currency":
                        EconomyManager.instance.IncrementCurrencyBalance(reward.id, reward.amount);
                        break;

                    case "cloudSave":
                        switch (reward.id)
                        {
                            case CloudSaveManager.xpKey:
                                GameStateManager.instance.xp += reward.amount;
                                break;

                            case CloudSaveManager.goalsAchievedKey:
                                GameStateManager.instance.goalsAchieved += reward.amount;
                                break;

                            default:
                                Debug.Log($"No local disbursement action exists for the reward {reward.id}");
                                break;
                        }

                        break;
                }
            }
        }
    }
}
