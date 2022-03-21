using System;
using System.Threading.Tasks;
using Unity.RemoteConfig;
using UnityEngine;

namespace UnityGamingServicesUseCases
{
    namespace BattlePass
    {
        public class RemoteConfigManager : MonoBehaviour
        {
            public static RemoteConfigManager instance { get; private set; }

            public int tierCount { get; private set; }
            public int seasonXpPerTier { get; private set; }

            public string activeEventName { get; private set; }
            public int activeEventEndTime { get; private set; }
            public int activeEventDurationMinutes { get; private set; }
            public string activeEventKey { get; private set; }

            public RewardDetail[] normalRewards { get; private set; }
            public RewardDetail[] battlePassRewards { get; private set; }

            void Awake()
            {
                if (instance != null && instance != this)
                {
                    Destroy(this);
                }
                else
                {
                    instance = this;
                }
            }

            void OnDestroy()
            {
                if (instance == this)
                {
                    instance = null;
                }
            }

            public async Task FetchConfigs()
            {
                try
                {
                    await ConfigManager.FetchConfigsAsync(GetUserAttributes(), new AppAttributes());

                    // Check that scene has not been unloaded while processing async wait to prevent throw.
                    if (this == null) return;

                    GetConfigValues();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            void GetConfigValues()
            {
                tierCount = ConfigManager.appConfig.GetInt("BATTLE_PASS_TIER_COUNT");
                seasonXpPerTier = ConfigManager.appConfig.GetInt("BATTLE_PASS_SEASON_XP_PER_TIER");
                activeEventName = ConfigManager.appConfig.GetString("EVENT_NAME");
                activeEventEndTime = ConfigManager.appConfig.GetInt("EVENT_END_TIME");
                activeEventDurationMinutes = ConfigManager.appConfig.GetInt("EVENT_TOTAL_DURATION_MINUTES");
                activeEventKey = ConfigManager.appConfig.GetString("EVENT_KEY");

                normalRewards = new RewardDetail[tierCount];
                battlePassRewards = new RewardDetail[tierCount];

                for (var i = 0; i < tierCount; i++)
                {
                    var tierRewardsJson = ConfigManager.appConfig.GetJson($"BATTLE_PASS_TIER_{i + 1}");
                    var tierRewards = JsonUtility.FromJson<TierRewards>(tierRewardsJson);
                    normalRewards[i] = tierRewards.reward;
                    battlePassRewards[i] = tierRewards.battlePassReward;
                }
            }

            UserAttributes GetUserAttributes()
            {
                // In this sample we are using the current minute to determine which game override to serve, and passing
                // that info to Remote Config via the UserAttributes to get the correct game override's data.
                //
                // This is useful for testing different game overrides, and for filtering out which users et which game
                // override based on certain data. The user's timestamp would not typically be used to determine their
                // game override however, since game overrides can be enabled/disabled or set to start and end at
                // specific times on the Remote Config dashboard. For the purposes of this demo however, this was the
                // easiest way to demonstrate cycling through several special events.
                // Fall Event: Data is returned for minutes that end in 0, 1, or 2.
                // Winter Event: Data is returned for minutes ending in 3 or 4.
                // Spring Event: Data is returned for minutes ending in 5, 6, or 7.
                // Summer Event: Data is returned for minutes ending in 8 or 9.
                return new UserAttributes { timestampMinutes = DateTime.Now.Minute };
            }

            // Remote Config's FetchConfigs call requires passing two non-nullable objects to the method, regardless of
            // whether any data needs to be passed in them. In this case we are using the UserAttributes struct to pass
            // the current timestamp, used to determine which seasonal event should be returned (See a longer explanation
            // for this in the GetUserAttributes() method).
            public struct UserAttributes
            {
                public int timestampMinutes;
            }

            // Candidates for what you can pass in the AppAttributes struct could be things like what level the player
            // is on, or what version of the app is installed. The candidates are completely customizable.
            public struct AppAttributes
            {
            }

            [Serializable]
            public struct TierRewards
            {
                public RewardDetail reward;
                public RewardDetail battlePassReward;
            }
        }
    }
}
