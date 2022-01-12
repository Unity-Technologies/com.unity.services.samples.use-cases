using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.RemoteConfig;
using UnityEngine;

namespace GameOperationsSamples
{
    namespace SeasonalEvents
    {
        public class RemoteConfigManager : MonoBehaviour
        {
            public static RemoteConfigManager instance { get; private set; }

            public string activeEventName { get; private set; }
            public int activeEventEndTime { get; private set; }
            public int activeEventDurationMinutes { get; private set; }
            public string activeEventKey { get; private set; }
            public List<RewardDetail> challengeRewards { get; private set; }

            void Awake()
            {
                if (instance != null && instance != this)
                {
                    Destroy(gameObject);
                }
                else
                {
                    instance = this;
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
                activeEventName = ConfigManager.appConfig.GetString("EVENT_NAME");
                activeEventEndTime = ConfigManager.appConfig.GetInt("EVENT_END_TIME");
                activeEventDurationMinutes = ConfigManager.appConfig.GetInt("EVENT_TOTAL_DURATION_MINUTES");
                activeEventKey = ConfigManager.appConfig.GetString("EVENT_KEY");
                var challengeRewardsJson = ConfigManager.appConfig.GetJson("CHALLENGE_REWARD");
                challengeRewards = JsonUtility.FromJson<Rewards>(challengeRewardsJson).rewards;
            }

            UserAttributes GetUserAttributes()
            {
                // In this sample we are using the current minute to determine which campaign to serve, and passing that info
                // to Remote Config via the UserAttributes to get the correct campaign's data.
                // This is useful for testing different campaign information, and for filtering out which users get which campaigns
                // based on certain data. The user's timestamp would not typically be used to determine their campaign however,
                // since campaigns can be enabled/disabled or set to start and end at specific times on the Remote Config dashboard.
                // For the purposes of this demo however, this was the easiest way to demonstrate cycling through several special events.
                // Fall Event: Data is returned for minutes that end in 0, 1, or 2.
                // Winter Event: Data is returned for minutes ending in 3 or 4.
                // Spring Event: Data is returned for minutes ending in 5, 6, or 7.
                // Summer Event: Data is returned for minutes ending in 8 or 9.
                return new UserAttributes { timestampMinutes = DateTime.Now.Minute };
            }

            void OnDestroy()
            {
                instance = null;
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
            public struct Rewards
            {
                public List<RewardDetail> rewards;
            }
        }
    }
}
