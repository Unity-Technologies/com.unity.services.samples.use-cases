using System;
using Unity.RemoteConfig;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace GameOperationsSamples
{
    namespace ABTestLevelDifficulty
    {
        public class RemoteConfigManager : MonoBehaviour
        {
            public static RemoteConfigManager instance { get; private set; }
            public static event Action ConfigValuesUpdated;


            public int levelUpXPNeeded { get; private set; }
            public string abGroupName { get; private set; }

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
                    return;
                }

                // When Remote Config determines what variant values to supply for the A/B test, it is using the Custom
                // User ID field to check whether the player has already been grouped into a specific variant group. Since
                // we call FetchConfigsIfServicesAreInitialized only upon a new sign-in, we want to make sure the custom
                // User ID is pointing to the most current Player ID.
                ConfigManager.SetCustomUserID(AuthenticationService.Instance.PlayerId);
                ConfigManager.FetchConfigs(new UserAttributes(), new AppAttributes());
            }

            void OnFetchCompleted(ConfigResponse configResponse)
            {
                switch (configResponse.requestOrigin)
                {
                    // Because we are only calling FetchConfigs on sign-in, and we know sign-in only occurs either
                    // when the scene initializes or after the user signs out as an existing anonymous user, we
                    // always want to get the new Remote Config data from the remote origin. If the data tries
                    // to return from a different source, we will try again.
                    // If FetchConfigs was a call we were making more frequently, we would want to allow loading
                    // from the cache and default when appropriate to save calls to Remote Config and to
                    // optimize game efficiency.
                    case ConfigOrigin.Default:
                    case ConfigOrigin.Cached:
                        FetchConfigsIfServicesAreInitialized();
                        break;

                    case ConfigOrigin.Remote:
                        if (configResponse.status == ConfigRequestStatus.Failed)
                        {
                            Debug.Log("Remote Settings failed to load config data from remote");
                        }
                        else
                        {
                            levelUpXPNeeded = ConfigManager.appConfig.GetInt("LEVEL_UP_XP_NEEDED");
                            abGroupName = ConfigManager.appConfig.GetString("A_B_TEST_GROUP");
                            ConfigValuesUpdated?.Invoke();
                        }
                        
                        break;
                }
            }

            public void ClearCachedData()
            {
                levelUpXPNeeded = 0;
                abGroupName = "";
            }

            // Remote Config's FetchConfigs call requires passing two non-nullable objects to the method, regardless of
            // whether any data needs to be passed in them. Candidates for what you may want to pass in the UserAttributes
            // struct could be things like device type, however it is completely customizable.
            public struct UserAttributes
            {
            }

            // Candidates for what you may want to pass in the AppAttributes struct could be things like what level the
            // player is on, or what version of the app is installed, however it is completely customizable.
            public struct AppAttributes
            {
            }
        }
    }
}
