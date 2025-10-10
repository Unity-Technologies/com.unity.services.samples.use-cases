using UnityEngine;

namespace Match3
{
    /// <summary>
    /// Allow to use a BonusGem as a BonusItem (item listed on a bar on screen, usually bought in the Shop)
    /// </summary>
    [CreateAssetMenu(fileName = "Bonus Gem Item", menuName = "2D Match/Bonus Items/Bonus Gem Item")]
    public class BonusGemBonusItem : BonusItem
    {
        public BonusGem UsedBonusGem;

        public override void Use(Vector3Int target)
        {
            //call init to place the bonus in the world so its current index is properly set
            UsedBonusGem.Init(target);
            //we call awake as some bonus gem have setup steps there that need to be done before being used (e.g. color
            //bonus create a texture etc.)
            UsedBonusGem.Awake();
            UsedBonusGem.Use(GameManager.Instance.Board.CellContent[target].ContainingGem, true);
            //most bonus gem effect are the effect triggered when they get destroyed. But that special gem don't get destroyed
            //so we use a special function that trigger those effects manually.
            UsedBonusGem.BonusTriggerEffect();
        }
    }
}