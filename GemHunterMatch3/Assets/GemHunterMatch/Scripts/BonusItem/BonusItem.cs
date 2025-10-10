using UnityEngine;

namespace Match3
{
    /// <summary>
    /// Bonus Item are items that can be used during gameplay, they are listed in a bar on the screen. This class needs to be
    /// subclass for each type of bonus item (see BonusGemBonusItem that allows to use a specific BonusGem as a Bonus Item).
    /// </summary>
    public abstract class BonusItem : ScriptableObject
    {
        public Sprite DisplaySprite;
        public bool NeedTarget = false;

        public abstract void Use(Vector3Int target);
    }
}