using UnityEngine;

namespace Match3
{
    /// <summary>
    /// Bonus Gem that delete gems directly left, right up and down of itself.
    /// </summary>
    public class SmallBomb : BonusGem
    {
        public AudioClip TriggerSound;
        
        public override void Awake()
        {
            m_Usable = true;
        }

        public override void Use(Gem swappedGem, bool isBonus = true)
        {
            //this allow to stop recursion on some bonus (like bomb trying to explode themselve again and again)
            //if isBonus is true, this is not a gem on the board so no risk of recursion we can ignore this
            if (!isBonus && m_Used)
                return;

            m_Used = true;
            
            var newMatch = GameManager.Instance.Board.CreateCustomMatch(m_CurrentIndex);
            newMatch.ForcedDeletion = true;
            HandleContent(GameManager.Instance.Board.CellContent[m_CurrentIndex], newMatch);

            GameManager.Instance.PlaySFX(TriggerSound);

            Vector3Int[] spaces = new[]
            {
                m_CurrentIndex + Vector3Int.left,
                m_CurrentIndex + Vector3Int.right,
                m_CurrentIndex + Vector3Int.up,
                m_CurrentIndex + Vector3Int.down
            };
        
            foreach (var idx in spaces)
            {
                if (GameManager.Instance.Board.CellContent.TryGetValue(idx, out var content) &&
                    content.ContainingGem != null)
                {
                    HandleContent(content, newMatch);
                }
            }
        }
    }
}
