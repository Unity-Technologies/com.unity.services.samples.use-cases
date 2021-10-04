using System;
using System.Collections.Generic;
using Unity.RemoteConfig;
using Unity.Services.Core;
using UnityEngine;

namespace SeasonalEvents
{
    public class RemoteConfigManager : MonoBehaviour
    {
        public static RemoteConfigManager instance { get; private set; }
        public static event Action RemoteConfigValuesUpdated;
        public static event Action RemoteConfigFetchConfigAborted;

        public string activeEventName = "";
        public int activeEventEndTime = 0;
        public string activeEventKey = "";
        public List<RewardDetail> challengeRewards = new List<RewardDetail>();

        // Remote Config's FetchConfigs call requires passing two non-nullable objects to the method, regardless of
        // whether any data needs to be passed in them. In this case we are using the UserAttributes struct to pass
        // the current timestamp, used to determine which seasonal event should be returned (See a longer explanation
        // for this in the GetUserAttributes() method).
        struct UserAttributes
        {
            public int timestampMinutes;
        }

        // Remote Config's FetchConfigs call requires passing two non-nullable objects to the method, regardless of
        // whether any data needs to be passed in them. Candidates for what you may want to pass in the AppAttributes
        // struct could be things like what level the player is on, or what version of the app is installed, however
        // it is completely customizable.
        struct AppAttributes
        {
        }

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

        public void StartSubscribe()
        {
            // ConfigManager.FetchCompleted can only be subscribed to after Unity Services has finished initializing.
            ConfigManager.FetchCompleted += OnFetchCompleted;
        }

        public void FetchConfigsIfServicesAreInitialized()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                RemoteConfigFetchConfigAborted?.Invoke();
                return;
            }

            ConfigManager.FetchConfigs(GetUserAttributes(), new AppAttributes());
        }

        void OnFetchCompleted(ConfigResponse configResponse)
        {
            activeEventName = ConfigManager.appConfig.GetString("EVENT_NAME");
            activeEventEndTime = ConfigManager.appConfig.GetInt("EVENT_END_TIME");
            activeEventKey = ConfigManager.appConfig.GetString("EVENT_KEY");
            var challengeRewardsJson = ConfigManager.appConfig.GetJson("CHALLENGE_REWARD");
            challengeRewards = JsonUtility.FromJson<Rewards>(challengeRewardsJson).rewards;
            RemoteConfigValuesUpdated?.Invoke();
        }

        UserAttributes GetUserAttributes()
        {
            // In this sample we are using the current minute to determine which campaign to serve, and passing that info
            // to Remote Config via the UserAttributes to get the correct campaign's data.
            // This is useful for testing different campaign information, and for filtering out which users get which campaigns
            // based on certain data. The user's timestamp would not typically be used to determine their campaign however,
            // since campaigns can be enabled/disabled or set to start and end at specific times on the Remote Config dashboard.
            // For the purposes of this demo however, this was the easiest way to demonstrate cycling through several special events.
            // Event 1: Data is returned for minutes that end in 0, 1, or 2.
            // Event 2: Data is returned for minutes ending in 3 or 4.
            // Event 3: Data is returned for minutes ending in 5, 6, or 7.
            // Event 4: Data is returned for minutes ending in 8 or 9.
            return new UserAttributes {timestampMinutes = DateTime.Now.Minute};
        }
    }
}
