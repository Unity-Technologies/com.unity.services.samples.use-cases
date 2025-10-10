using System.Collections.Generic;
using UnityEngine;

namespace Match3
{
    public class Match
    {
        public List<Vector3Int> MatchingGem = new();
        
        public Vector3Int OriginPoint;
        public float DeletionTimer = 0.0f;

        //public bool FromBonus = false;

        public BonusGem SpawnedBonus = null;

        //this is forced deletion, usually from a bonus. Used to remove obstacle
        public bool ForcedDeletion = false;

        //will be incremented with each gem deleted in that match, will allow to spawn coins when reaching 4+
        public int DeletedCount = 0;

        public void AddGem(Gem gem)
        {
            if(gem.CurrentMatch != null)
                return;
        
            MatchingGem.Add(gem.CurrentIndex);
            gem.CurrentMatch = this;
        }
    }
}