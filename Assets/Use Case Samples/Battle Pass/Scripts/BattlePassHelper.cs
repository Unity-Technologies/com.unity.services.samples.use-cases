using System;

namespace UnityGamingServicesUseCases
{
    namespace BattlePass
    {
        public static class BattlePassHelper
        {
            public static int MaxEffectiveSeasonXp()
            {
                // There are 10 tiers, and it takes 100 XP to unlock a tier.
                // But since you don't have to unlock the first tier, you only need 900 total XP to unlock all tiers.

                return RemoteConfigManager.instance.seasonXpPerTier * (RemoteConfigManager.instance.tierCount - 1);
            }

            public static int GetCurrentTierIndex(int seasonXP)
            {
                return Math.Min(seasonXP / RemoteConfigManager.instance.seasonXpPerTier, RemoteConfigManager.instance.tierCount - 1);
            }

            public static int GetNextTierIndex(int seasonXP)
            {
                return Math.Min(GetCurrentTierIndex(seasonXP) + 1, RemoteConfigManager.instance.tierCount - 1);
            }

            public static int TotalSeasonXpNeededForNextTier(int seasonXP)
            {
                // Tier 1 starts out unlocked, so it requires 0 Season XP.
                // Tier 2 requires 100 total Season XP, Tier 3 requires 200 Season XP, and so on.

                return GetNextTierIndex(seasonXP) * RemoteConfigManager.instance.seasonXpPerTier;
            }

            public static float CurrentSeasonProgressFloat(int seasonXP)
            {
                if (seasonXP >= MaxEffectiveSeasonXp()) return 1f;

                var xpEarnedThisTier = seasonXP % RemoteConfigManager.instance.seasonXpPerTier;

                return xpEarnedThisTier / (float)RemoteConfigManager.instance.seasonXpPerTier;
            }
        }
    }
}
