using UnityEngine;

namespace Match3
{
    public class TieBlocker : Obstacle
    {
        public override void Init(Vector3Int cell)
        {
            base.Init(cell);
        
            // we also register the cell as a normal "gem" cell so a gem is spawn under the blocker on start.
            Board.RegisterCell(cell);
            Board.ChangeLock(cell, true);
            Board.RegisterMatchedCallback(cell, CellMatch);
        }

        public override void Clear()
        {
            Board.UnregisterMatchedCallback(m_Cell, CellMatch);
            Board.ChangeLock(m_Cell, false);
            Destroy(gameObject);
        }

        void CellMatch()
        {
            if(ChangeState(m_CurrentState + 1))
            {
                Clear();
            }
        }
    }
}