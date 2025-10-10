using UnityEngine;

namespace Match3
{
    [CreateAssetMenu(fileName = "CoinItem", menuName = "2D Match/Shop Items/Coin Item")]
    public class ShopItemCoin : ShopSetting.ShopItem
    {
        public int CoinAmount;
    
        public override void Buy()
        {
            GameManager.Instance.ChangeCoins(CoinAmount);
        }
    }
}

//This make no real sense "gameplay" wise but is for demo purpose : allow to get "free" coins