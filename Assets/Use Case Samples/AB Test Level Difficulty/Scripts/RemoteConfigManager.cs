using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.RemoteConfig;
using UnityEngine;

namespace Unity.Services.Samples.ABTestLevelDifficulty
{
    public class RemoteConfigManager : MonoBehaviour
    {
        public static RemoteConfigManager instance { get; private set; }

        public int levelUpXPNeeded { get; private set; }
        public string abGroupName { get; private set; }
        public string abTestID { get; private set; }
        public Dictionary<string, CurrencySpec> currencyDataDictionary { get; private set; }

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
                // When Game Overrides determines what variant values to supply for the A/B test, it uses Remote Config's
                // Custom User ID field to check whether the player has already been grouped into a specific variant group.
                // Since this use case changes user log in more than is typical, and we call FetchConfigs after each new
                // sign-in, we want to make sure the custom User ID Remote Config is using is the most current Player ID.
                //
                // Calling SetCustomUserId() before RemoteConfigService.Instance.FetchConfigsAsync() is not necessary
                // in most typical uses of Remote Config and Game Overrides.
                RemoteConfigService.Instance.SetCustomUserID(AuthenticationService.Instance.PlayerId);
                await RemoteConfigService.Instance.FetchConfigsAsync(new UserAttributes(), new AppAttributes());

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
            levelUpXPNeeded = RemoteConfigService.Instance.appConfig.GetInt("AB_TEST_LEVEL_UP_XP_NEEDED");
            abGroupName = RemoteConfigService.Instance.appConfig.GetString("AB_TEST_GROUP");
            abTestID = RemoteConfigService.Instance.appConfig.GetString("AB_TEST_ID");
            var json = RemoteConfigService.Instance.appConfig.GetJson("CURRENCIES");
            currencyDataDictionary = CreateCurrencyDictionary(json);
        }

        Dictionary<string, CurrencySpec> CreateCurrencyDictionary(string json)
        {
            var dictionary = new Dictionary<string, CurrencySpec>();

            var currencyDataHolder = JsonUtility.FromJson<CurrencyDataHolder>(json);

            foreach (var currencyData in currencyDataHolder.currencyData)
            {
                dictionary[currencyData.currencyId] = currencyData.currencySpec;
            }

            return dictionary;
        }

        public void ClearCachedData()
        {
            levelUpXPNeeded = 0;
            abGroupName = "";
            abTestID = "";
            currencyDataDictionary = default;
        }

        void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        // Remote Config's FetchConfigs call requires passing two non-nullable objects to the method, regardless of
        // whether any data needs to be passed in them. Candidates for what you may want to pass in the UserAttributes
        // struct could be things like device type, however it is completely customizable.
        public struct UserAttributes { }

        // Candidates for what you may want to pass in the AppAttributes struct could be things like what level the
        // player is on, or what version of the app is installed, however it is completely customizable.
        public struct AppAttributes { }

        [Serializable]
        public class CurrencyDataHolder
        {
            public List<CurrencyData> currencyData;
        }

        [Serializable]
        public class CurrencyData
        {
            public string currencyId;
            public CurrencySpec currencySpec;
        }

        // This container holds metadata about the definition of a currency. Currently we only need
        // the sprite address, but we're using a container to facilitate adding additional data as needed.
        [Serializable]
        public class CurrencySpec
        {
            public string spriteAddress;
        }
    }
}
