using System;

namespace Unity.Services.Samples.BattlePass
{
    public static class BattlePassHelper
    {
        public static int MaxEffectiveSeasonXp(BattlePassConfig battlePassConfig)
        {
            // There are 10 tiers, and it takes 100 XP to unlock a tier.
            // But since you don't have to unlock the first tier, you only need 900 total XP to unlock all tiers.

            return battlePassConfig.seasonXpPerTier * (battlePassConfig.tierCount - 1);
        }

        public static int GetCurrentTierIndex(int seasonXP, BattlePassConfig battlePassConfig)
        {
            return Math.Min(seasonXP / battlePassConfig.seasonXpPerTier, battlePassConfig.tierCount - 1);
        }

        public static int GetNextTierIndex(int seasonXP, BattlePassConfig battlePassConfig)
        {
            return Math.Min(GetCurrentTierIndex(seasonXP, battlePassConfig) + 1, battlePassConfig.tierCount - 1);
        }

        public static int TotalSeasonXpNeededForNextTier(int seasonXP, BattlePassConfig battlePassConfig)
        {
            // Tier 1 starts out unlocked, so it requires 0 Season XP.
            // Tier 2 requires 100 total Season XP, Tier 3 requires 200 Season XP, and so on.

            return GetNextTierIndex(seasonXP, battlePassConfig) * battlePassConfig.seasonXpPerTier;
        }

        public static float CurrentSeasonProgressFloat(int seasonXP, BattlePassConfig battlePassConfig)
        {
            if (seasonXP >= MaxEffectiveSeasonXp(battlePassConfig)) return 1f;

            var xpEarnedThisTier = seasonXP % battlePassConfig.seasonXpPerTier;

            return xpEarnedThisTier / (float)battlePassConfig.seasonXpPerTier;
        }
    }
}
