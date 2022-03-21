using System.Collections.Generic;

namespace UnityGamingServicesUseCases
{
    namespace CommandBatching
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
                                case "COMMANDBATCH_XP":
                                    GameStateManager.instance.xp += reward.amount;
                                    break;
                                case "COMMANDBATCH_GOALSACHIEVED":
                                    GameStateManager.instance.goalsAchieved += reward.amount;
                                    break;
                            }
                            break;
                    }
                }
            }
        }
    }
}
