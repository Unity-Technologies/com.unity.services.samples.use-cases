using UnityEngine;

namespace Match3
{
    /// <summary>
    /// Bonus Gem that will delete gems in a 5x5 grid around itself.
    /// </summary>
    public class LargeBomb : BonusGem
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

            //we still test the content of the index as this could be a bonus used from the Ui so the clicked position
            //won't contains "this" but a different gem
            var currentContent = GameManager.Instance.Board.CellContent[m_CurrentIndex];
            HandleContent(currentContent, newMatch);


            GameManager.Instance.PlaySFX(TriggerSound);

            for (int x = -2; x <= 2; ++x)
            {
                for (int y = -2; y <= 2; ++y)
                {
                    var idx = m_CurrentIndex + new Vector3Int(x, y, 0);
                    if (GameManager.Instance.Board.CellContent.TryGetValue(idx, out var content) &&
                        content.ContainingGem != null)
                    {
                        HandleContent(content, newMatch);
                    }
                }
            }
        }
    }
}