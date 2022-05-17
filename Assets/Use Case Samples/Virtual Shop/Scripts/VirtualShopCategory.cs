using System.Collections.Generic;

namespace UnityGamingServicesUseCases
{
    namespace VirtualShop
    {
        public class VirtualShopCategory
        {
            public string id { get; private set; }
            public bool enabledFlag { get; private set; }
            public List<VirtualShopItem> virtualShopItems { get; private set; }

            public VirtualShopCategory(RemoteConfigManager.CategoryConfig categoryConfig)
            {
                id = categoryConfig.id;
                enabledFlag = categoryConfig.enabledFlag;
                virtualShopItems = new List<VirtualShopItem>();

                foreach (var item in categoryConfig.items)
                {
                    virtualShopItems.Add(new VirtualShopItem(item));
                }
            }

            public override string ToString()
            {
                return $"\"{id}\" enabled:{enabledFlag} items:{virtualShopItems?.Count}";
            }
        }
    }
}
