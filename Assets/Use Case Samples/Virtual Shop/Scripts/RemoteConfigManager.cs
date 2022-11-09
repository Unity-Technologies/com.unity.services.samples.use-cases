using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Services.RemoteConfig;
using UnityEngine;

namespace Unity.Services.Samples.VirtualShop
{
    public class RemoteConfigManager : MonoBehaviour
    {
        public static RemoteConfigManager instance { get; private set; }

        public VirtualShopConfig virtualShopConfig { get; private set; }

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
            var shopCategoriesConfigJson = RemoteConfigService.Instance.appConfig.GetJson("VIRTUAL_SHOP_CONFIG");
            virtualShopConfig = JsonUtility.FromJson<VirtualShopConfig>(shopCategoriesConfigJson);
        }

        void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        struct UserAttributes { }

        struct AppAttributes { }

        [Serializable]
        public struct VirtualShopConfig
        {
            public List<CategoryConfig> categories;

            public override string ToString()
            {
                return $"categories: {string.Join(", ", categories.Select(category => category.ToString()).ToArray())}";
            }
        }

        [Serializable]
        public struct CategoryConfig
        {
            public string id;
            public bool enabledFlag;
            public List<ItemConfig> items;

            public override string ToString()
            {
                var returnString = new StringBuilder($"category:\"{id}\", enabled:{enabledFlag}");
                if (items?.Count > 0)
                {
                    returnString.Append($", items: {string.Join(", ", items.Select(itemConfig => itemConfig.ToString()).ToArray())}");
                }

                return returnString.ToString();
            }
        }

        [Serializable]
        public struct ItemConfig
        {
            public string id;
            public string color;
            public string badgeIconAddress;
            public string badgeColor;
            public string badgeText;
            public string badgeTextColor;

            public override string ToString()
            {
                var returnString = new StringBuilder($"\"{id}\" color:{color}");

                if (!string.IsNullOrEmpty(badgeIconAddress))
                {
                    returnString.Append($" badgeIconAddress:\"{badgeIconAddress}\"");
                }

                return returnString.ToString();
            }
        }
    }
}
