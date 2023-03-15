using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.RemoteConfig;
using UnityEngine;

namespace Unity.Services.Samples.OverTheAirContent
{
    public class RemoteConfigManager : MonoBehaviour
    {
        const string k_CatalogAddressKey = "OTA_CATALOG_URL";
        const string k_ContentUpdatesKey = "OTA_CONTENT_UPDATES";

        public static RemoteConfigManager instance { get; private set; }

        public string cloudCatalogAddress { get; private set; }

        public ContentUpdates contentUpdates { get; private set; }

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
                await RemoteConfigService.Instance.FetchConfigsAsync(new UserAttributes(), new AppAttributes());
                if (this == null) return;

                cloudCatalogAddress = RemoteConfigService.Instance.appConfig.GetString(k_CatalogAddressKey);

                var contentUpdatesJson = RemoteConfigService.Instance.appConfig.GetJson(k_ContentUpdatesKey);
                if (!string.IsNullOrEmpty(contentUpdatesJson))
                {
                    contentUpdates = JsonUtility.FromJson<ContentUpdates>(contentUpdatesJson);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public List<string> GetNewContentAddresses()
        {
            var newContentAddresses = new List<string>();

            foreach (var contentUpdate in contentUpdates.updates)
            {
                var configKey = contentUpdate.configKey;
                var contentUpdateJson = RemoteConfigService.Instance.appConfig.GetJson(configKey);
                var newContent = JsonUtility.FromJson<NewContent>(contentUpdateJson);
                newContentAddresses.Add(newContent.prefabAddress);
            }

            return newContentAddresses;
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

        // Candidates for what you can pass in the AppAttributes struct could be things like what level the player
        // is on, or what version of the app is installed. The candidates are completely customizable.
        public struct AppAttributes { }

        [Serializable]
        public struct NewContent
        {
            public string prefabAddress;
        }

        [Serializable]
        public struct ContentUpdate
        {
            public string configKey;
        }

        [Serializable]
        public struct ContentUpdates
        {
            public List<ContentUpdate> updates;
        }
    }
}
