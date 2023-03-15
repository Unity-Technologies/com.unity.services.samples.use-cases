using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.RemoteConfig;
using UnityEngine;

namespace Unity.Services.Samples.SeasonalEvents
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
                Destroy(this);
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
                await RemoteConfigService.Instance.FetchConfigsAsync(GetUserAttributes(), new AppAttributes());

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
            activeEventName = RemoteConfigService.Instance.appConfig.GetString("EVENT_NAME");
            activeEventEndTime = RemoteConfigService.Instance.appConfig.GetInt("EVENT_END_TIME");
            activeEventDurationMinutes = RemoteConfigService.Instance.appConfig.GetInt("EVENT_TOTAL_DURATION_MINUTES");
            activeEventKey = RemoteConfigService.Instance.appConfig.GetString("EVENT_KEY");
            var challengeRewardsJson = RemoteConfigService.Instance.appConfig.GetJson("SEASONAL_EVENTS_CHALLENGE_REWARD");
            challengeRewards = JsonUtility.FromJson<Rewards>(challengeRewardsJson).rewards;
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
            //
            // Note: We use the approximate server time here to ensure we are showing/claiming the correct season
            //       in case the client's clock is off for any reason.
            return new UserAttributes { timestampMinutes = ServerTimeHelper.UtcNow.Minute };
        }

        void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
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
        public struct AppAttributes { }

        [Serializable]
        public struct Rewards
        {
            public List<RewardDetail> rewards;
        }
    }
}
