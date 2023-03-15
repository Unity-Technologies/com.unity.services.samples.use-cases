using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Unity.Services.Samples.VirtualShop
{
    public class VirtualShopItem
    {
        public string id { get; private set; }
        public string color { get; private set; }
        public string badgeIconAddress { get; private set; }
        public string badgeColor { get; private set; }
        public string badgeText { get; private set; }
        public string badgeTextColor { get; private set; }

        public List<ItemAndAmountSpec> costs { get; private set; }
        public List<ItemAndAmountSpec> rewards { get; private set; }

        public VirtualShopItem(RemoteConfigManager.ItemConfig itemConfig)
        {
            id = itemConfig.id;
            color = itemConfig.color;
            badgeIconAddress = itemConfig.badgeIconAddress;
            badgeColor = itemConfig.badgeColor;
            badgeText = itemConfig.badgeText;
            badgeTextColor = itemConfig.badgeTextColor;

            var transactionInfo = EconomyManager.instance.virtualPurchaseTransactions[id];
            costs = transactionInfo.costs;
            rewards = transactionInfo.rewards;
        }

        public override string ToString()
        {
            var returnString = new StringBuilder($"\"{id}\"");

            returnString.Append($" costs:[{string.Join(", ", costs.Select(cost => cost.ToString()).ToArray())}]"
                + $" rewards:[{string.Join(", ", rewards.Select(reward => reward.ToString()).ToArray())}]");

            returnString.Append($" color:{color}");

            if (!string.IsNullOrEmpty(badgeIconAddress))
            {
                returnString.Append($" badgeIconAddress:\"{badgeIconAddress}\"");
            }

            return returnString.ToString();
        }
    }
}
