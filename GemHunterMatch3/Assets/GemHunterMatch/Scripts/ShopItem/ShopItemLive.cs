using UnityEngine;

namespace Match3
{
    [CreateAssetMenu(fileName = "LiveItem", menuName = "2D Match/Shop Items/Live Item")]
    public class ShopItemLive : ShopSetting.ShopItem
    {
        public override void Buy()
        {
            GameManager.Instance.AddLive(1);
        }
    }
}

//This make no real sense "gameplay" wise but is for demo purpose : allow to get "free" coins