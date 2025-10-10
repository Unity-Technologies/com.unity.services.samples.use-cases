using System.Collections.Generic;
using UnityEngine;

namespace Match3
{
    /// <summary>
    /// Bonus Gem is a special gem that contains a list of MatchShape.
    /// Bonus Gems are listed in the Game Settings on the GameManager.
    /// When a match happens, the system goes over all BonusGem and check if one of the MatchShape match the shape of the
    /// match and if it does, spawn that bonus gem there.
    /// </summary>
    public class BonusGem : Gem
    {
        public List<MatchShape> Shapes;
        
        public virtual void Awake(){}

        //helper function that inheriting class can use to destroy a gem, but handle obstacle and bonus gem properly
        //if the cell don't contains either an obstacle or usable gem and the targetted gem is destroyed, add it to the
        //given match
        protected void HandleContent(BoardCell cell, Match receivingMatch)
        {

            if (cell.Obstacle != null)
            {
                cell.Obstacle.Damage(1);
            }

            if (cell.ContainingGem == null)
                return;

            if (cell.ContainingGem.Usable && !cell.ContainingGem.Used)
            {
                cell.ContainingGem.Use(null);
            }
            else if (cell.ContainingGem.CurrentMatch == null && !cell.ContainingGem.Damage(1))
                receivingMatch.AddGem(cell.ContainingGem);
        }

        //effect on the board are triggered by the gems being destroyed, but when using a bonus item ot use a Bonus Gem
        //the effect won't be triggered. So Bonus item call this function to trigger the VFX
        public void BonusTriggerEffect()
        {
            var position = GameManager.Instance.Board.GetCellCenter(m_CurrentIndex); 
            foreach (var effectPrefab in MatchEffectPrefabs)
            {
                //normally the game will instantiate the bonus vfx when it first get spawn, but if using a bonus item
                //before that happen, this ensure the vfx will get instantiated first 
                GameManager.Instance.PoolSystem.AddNewInstance(effectPrefab, 8);
                GameManager.Instance.PoolSystem.PlayInstanceAt(effectPrefab, position);
            }
        }
    }

    /// <summary>
    /// A MatchShape defines a list of cells that will form a shape to match against during a gem match.
    /// </summary>
    [System.Serializable]
    public class MatchShape : ISerializationCallbackReceiver
    {
        public bool CanMirror;
        public bool CanRotate;
    
        public List<Vector3Int> Cells = new() { Vector3Int.zero };
        public RectInt Bounds = new RectInt(Vector2Int.zero, Vector2Int.zero);
        private List<Vector3Int> Cell90Rot = new();
        private List<Vector3Int> Cell180Rot = new();
        private List<Vector3Int> Cell270Rot = new();

        private List<Vector3Int> CellHMirror = new();
        private List<Vector3Int> CellVMirror = new();
    
        public void OnBeforeSerialize()
        {
        
        }

        public void OnAfterDeserialize()
        {
            //we ALWAYS need at least a single cells so initialize with (0,0) if no cell

            if (Cells.Count == 0)
            {
                Cells.Add(new Vector3Int(0, 0));
            }
        
            Bounds = GetBoundOf(Cells);
        
            //we cache the rotated and mirrored cells, so we can just quickly compare and not recompute them every match.
            Cell90Rot.Clear();
            Cell180Rot.Clear();
            Cell270Rot.Clear();
        
            CellHMirror.Clear();
            CellVMirror.Clear();

            foreach (var cell in Cells)
            {
                GetRotation((Vector3Int)Bounds.min, cell, out var rot90, out var rot180, out var rot270);
            
                //all rotate cell get shifted to fall back on the same bounds as the original one
                Cell90Rot.Add(rot90 + new Vector3Int(0, Bounds.width, 0));
                Cell180Rot.Add(rot180 + new Vector3Int(Bounds.width, Bounds.height, 0));
                Cell270Rot.Add(rot270 + new Vector3Int(Bounds.height, 0));

                var x = Bounds.xMax - (cell.x - Bounds.xMin);
                CellHMirror.Add( new Vector3Int(x, cell.y, 0) );
            
                var y = Bounds.yMax - (cell.y - Bounds.yMin);
                CellVMirror.Add( new Vector3Int(cell.x, y, 0) );
            }
        }

        /// <summary>
        /// Called by the Board when a new match happens.
        /// </summary>
        /// <param name="cellList">The list of cells in the match we test against</param>
        /// <param name="matchedCells">This will be filled by the cell this MatchShape used in the cell List if it could fit</param>
        /// <returns>True if the shape could fit, false otherwise</returns>
        public bool FitIn(List<Vector3Int> cellList, ref List<Vector3Int> matchedCells)
        {
            var targetBound = GetBoundOf(cellList);
        
            //we move the shape bound rect inside the cellList bound rect to check if all cell part of the shape can match cell
            //inside the cell list.

            //we make the shape rect into a square, so we can rotate & mirror the shape in the same bound.  
            var largestBoundSize = Mathf.Max(targetBound.width, targetBound.height);
            var smallestBoundSize = Mathf.Min(targetBound.width, targetBound.height);

            for (int y = targetBound.yMin; y <= targetBound.yMax - smallestBoundSize + 1; ++y)
            {
                for (int x = targetBound.xMin; x <= targetBound.xMax - smallestBoundSize + 1; ++x)
                {
                    List<Vector3Int> matchingCells = new();
                    List<Vector3Int> matching90Cells = new();
                    List<Vector3Int> matching180Cells = new();
                    List<Vector3Int> matching270Cells = new();
                    List<Vector3Int> matchingHMirrorCells = new();
                    List<Vector3Int> matchingVMirrorCells = new();
                

                    for (int iy = 0; iy <= largestBoundSize; ++iy)
                    {
                        for (int ix = 0; ix <= largestBoundSize; ++ix)
                        {
                            var normalShapeCell = new Vector3Int(ix + Bounds.xMin, iy + Bounds.yMin, 0);
                            var localCell = new Vector3Int(x + ix, y + iy, 0);
                        
                            if (cellList.Contains(localCell))
                            {
                                if (Cells.Contains(normalShapeCell))
                                    matchingCells.Add(localCell);

                                if (Cell90Rot.Contains(normalShapeCell))
                                    matching90Cells.Add(localCell);
                            
                                if (Cell180Rot.Contains(normalShapeCell))
                                    matching180Cells.Add(localCell);
                            
                                if (Cell270Rot.Contains(normalShapeCell))
                                    matching270Cells.Add(localCell);
                            
                                if(CellHMirror.Contains(normalShapeCell))
                                    matchingHMirrorCells.Add(localCell);
                            
                                if(CellVMirror.Contains(normalShapeCell))
                                    matchingVMirrorCells.Add(localCell);
                            }
                        }
                    }

                    List<Vector3Int> usableList = null;
                    int count = Cells.Count;
                    if (matchingCells.Count == count)
                    {
                        usableList = matchingCells;
                    }
                
                    if (usableList == null && CanRotate)
                    {
                        if (matching90Cells.Count == count)
                        {
                            usableList = matching90Cells;
                        }
                        else if (matching180Cells.Count == count)
                        {
                            usableList = matching180Cells;
                        }
                        else if (matching270Cells.Count == count)
                        {
                            usableList = matching270Cells;
                        }
                    }

                    if (usableList == null && CanMirror)
                    {
                        if (matchingHMirrorCells.Count == count)
                        {
                            usableList = matchingHMirrorCells;
                        }
                        else if (matchingVMirrorCells.Count == count)
                        {
                            usableList = matchingVMirrorCells;
                        }
                    }

                    if (usableList != null)
                    {
                        foreach (var cell in usableList)
                        {
                            matchedCells.Add(cell);
                        }
                        return true;
                    }
                }
            }

            return false;
        }

        void GetRotation(Vector3Int pivot, Vector3Int point, 
            out Vector3Int rot90, out Vector3Int rot180, out Vector3Int rot270)
        {
            var toPoint = point - pivot;
        
            rot90 = new Vector3Int(toPoint.y, -toPoint.x, 0) + pivot;
            rot180 = new Vector3Int(-toPoint.x, -toPoint.y, 0) + pivot;
            rot270 = new Vector3Int(-toPoint.y, toPoint.x, 0) + pivot;
        }

        /// <summary>
        /// Return the bound of a list of cells
        /// </summary>
        /// <param name="cellList">The list of the cells for which to get the bounds</param>
        /// <returns>The bounding rect of all the cells in the cellList</returns>
        public static RectInt GetBoundOf(List<Vector3Int> cellList)
        {
            if (cellList.Count == 0)
                return new RectInt(0, 0, 0, 0);
        
            RectInt rect = new RectInt(cellList[0].x, cellList[0].y, 0, 0);

            for(int i = 1; i < cellList.Count; ++i)
            {
                var cell = cellList[i];
                if (rect.xMin > cell.x)
                {
                    rect.xMin = cell.x;
                }
                else if (rect.xMax < cell.x)
                {
                    rect.xMax = cell.x;
                }
            
                if (rect.yMin > cell.y)
                {
                    rect.yMin = cell.y;
                }
                else if (rect.yMax < cell.y)
                {
                    rect.yMax = cell.y;
                }
            }

            return rect;
        }
    }
}