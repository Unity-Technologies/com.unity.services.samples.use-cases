using System;

namespace Unity.Services.Samples.BattlePass
{
    public struct BattlePassConfig
    {
        public RewardDetail[] rewardsFree;
        public RewardDetail[] rewardsPremium;
        public int seasonXpPerTier;
        public int tierCount;
        public string eventName;
    }
}
