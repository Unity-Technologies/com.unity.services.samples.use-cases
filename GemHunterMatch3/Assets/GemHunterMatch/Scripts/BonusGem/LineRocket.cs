using Match3;
using UnityEngine;

namespace Match3
{
    /// <summary>
    /// Bonus gems that will move across time horizontally/vertically and delete all gems in that direction
    /// </summary>
    public class LineRocket : BonusGem
    {
        public GameObject VisualPrefab;
        public bool Vertical;

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
            
            var dir = Vertical ? Vector3Int.up : Vector3Int.right;

            GameManager.Instance.PlaySFX(TriggerSound);
            
            //delete itself first.
            var newMatch = GameManager.Instance.Board.CreateCustomMatch(m_CurrentIndex);
            HandleContent(GameManager.Instance.Board.CellContent[m_CurrentIndex], newMatch);

            //if there is a cell on a side, we add a new board action that will go in that direction.
            if (GameManager.Instance.Board.CellContent.ContainsKey(m_CurrentIndex + dir))
            {
                GameManager.Instance.Board.AddNewBoardAction(new RocketAction(m_CurrentIndex, dir, VisualPrefab, 0));
            }

            if (GameManager.Instance.Board.CellContent.ContainsKey(m_CurrentIndex - dir))
            {
                GameManager.Instance.Board.AddNewBoardAction(new RocketAction(m_CurrentIndex, -dir, VisualPrefab,
                    Vertical ? 2 : 1));
            }
        }
    }
    
    /// <summary>
    /// RocketAction is a board action that will delete gem along a direction at a given speed until it can no longer go
    /// forward
    /// </summary>
    class RocketAction : Board.IBoardAction
    {
        protected Vector3Int m_CurrentCell;
        protected Vector3Int m_Direction;

        protected GameObject m_Visual;

        private const float MoveSpeed = 10.0f;
        
        public RocketAction(Vector3Int startCell, Vector3Int direction, GameObject visualPrefab, int flip)
        {
            m_CurrentCell = startCell;
            m_Direction = direction;
            
            GameManager.Instance.Board.LockMovement();

            m_Visual = GameObject.Instantiate(visualPrefab, 
                GameManager.Instance.Board.GetCellCenter(m_CurrentCell), 
                Quaternion.identity);

            switch (flip)
            {
                case 1:
                    m_Visual.transform.localScale = new Vector3(-1, 1, 1);
                    break;
                case 2:
                    m_Visual.transform.localScale = new Vector3(1, -1, 1);
                    break;
            }
        }
        
        //Called by the board on all its board actions.
        public bool Tick()
        {
            m_Visual.transform.position += (Vector3)(m_Direction) * (Time.deltaTime * MoveSpeed);

            var cell = GameManager.Instance.Board.WorldToCell(m_Visual.transform.position);

            while (m_CurrentCell != cell)
            {
                m_CurrentCell += m_Direction;

                if (GameManager.Instance.Board.CellContent.TryGetValue(m_CurrentCell, out var content) && content.ContainingGem != null)
                {
                    if (content.Obstacle != null)
                    {
                        content.Obstacle.Damage(1);
                    }
                    else if (content.ContainingGem.Usable)
                    {
                        content.ContainingGem.Use(null);
                    }
                    else if (!content.ContainingGem.Damage(1))
                    {
                        GameManager.Instance.Board.DestroyGem(m_CurrentCell, true);
                        
                    }
                }

                if (!GameManager.Instance.Board.CellContent.ContainsKey(m_CurrentCell + m_Direction))
                {
                    GameObject.Destroy(m_Visual);
                    //if we don't have a cell after that one, we reached the end, return false to finish that BoardAction
                    GameManager.Instance.Board.UnlockMovement();
                    return false;
                }
            }
            
            return true;
        }
    }
}
