using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Match3
{
    public class BoardCell
    {
        public static readonly Vector3Int[] Neighbours =
        {
            Vector3Int.up,
            Vector3Int.right,
            Vector3Int.down,
            Vector3Int.left
        };
    
        public Gem ContainingGem;
        public Gem IncomingGem;
        public Obstacle Obstacle;

        public bool CanFall => (ContainingGem == null || (ContainingGem.CanMove && ContainingGem.CurrentMatch == null)) && !Locked;
        public bool BlockFall => Locked || (ContainingGem != null && !ContainingGem.CanMove);
        public bool CanBeMoved => !Locked && ContainingGem != null && ContainingGem.CanMove;
        
        public bool Locked = false;
        

        public bool CanMatch()
        {
            return ContainingGem != null;
        }

        public bool CanDelete()
        {
            return !Locked;
        }

        public bool IsEmpty()
        {
            return ContainingGem == null && IncomingGem == null;
        }
    }
}
