using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.Services.Samples.VirtualShop
{
    public class VirtualShopItemView : MonoBehaviour
    {
        public Image rewardIcon;
        public TextMeshProUGUI rewardAmount;

        public Image costIcon;
        public TextMeshProUGUI costAmount;

        public Image badgeIcon;
        public TextMeshProUGUI badgeText;

        VirtualShopSceneManager m_VirtualShopSceneManager;
        VirtualShopItem m_VirtualShopItem;

        public void Initialize(VirtualShopSceneManager virtualShopSceneManager, VirtualShopItem virtualShopItem,
            AddressablesManager addressablesManager)
        {
            m_VirtualShopSceneManager = virtualShopSceneManager;
            m_VirtualShopItem = virtualShopItem;

            GetComponent<Image>().color = GetColorFromString(virtualShopItem.color);

            var cost = virtualShopItem.costs[0];
            var reward = virtualShopItem.rewards[0];

            costIcon.sprite = addressablesManager.preloadedSpritesByEconomyId[cost.id];
            rewardIcon.sprite = addressablesManager.preloadedSpritesByEconomyId[reward.id];

            costAmount.text = cost.amount.ToString();

            rewardAmount.enabled = reward.amount != 1;
            rewardAmount.text = $"x{reward.amount}";

            if (!string.IsNullOrEmpty(virtualShopItem.badgeIconAddress))
            {
                if (!addressablesManager.preloadedSpritesByAddress.TryGetValue(virtualShopItem.badgeIconAddress, out var sprite))
                {
                    throw new KeyNotFoundException($"Preloaded sprite not found for {virtualShopItem.badgeIconAddress}");
                }

                badgeIcon.sprite = sprite;
                badgeIcon.enabled = true;

                if (!string.IsNullOrEmpty(virtualShopItem.badgeText))
                {
                    badgeText.text = virtualShopItem.badgeText;

                    badgeIcon.color = GetColorFromString(virtualShopItem.badgeColor);
                    badgeText.color = GetColorFromString(virtualShopItem.badgeTextColor);
                }
            }
        }

        Color GetColorFromString(string colorString)
        {
            if (ColorUtility.TryParseHtmlString(colorString, out var color))
            {
                return color;
            }

            return Color.white;
        }

        public async void OnPurchaseButtonClicked()
        {
            try
            {
                await m_VirtualShopSceneManager.OnPurchaseClicked(m_VirtualShopItem);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
