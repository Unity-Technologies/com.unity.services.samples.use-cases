using System;
using System.Threading.Tasks;
using GemHunterUGS.Scripts;
using UnityEngine;
using UnityEngine.Rendering.UI;
using UnityEngine.Tilemaps;

namespace Match3
{
    /// <summary>
    /// Tile only used at edit/load time to define the cell that can contain a tile. If the PlacedGem is null, this count as
    /// a random gem, a gem type will be picked at random from the list of available one.
    /// This tile won't have any visual component in play mode or in a build, as it's only useful during editing. 
    /// </summary>
    [CreateAssetMenu(fileName = "GemPlacerTile", menuName = "2D Match/Tile/Gem Placer")]
    public class GemPlacerTile : TileBase
    {
        public Sprite PreviewEditorSprite;
        [Tooltip("If null this will be a random gem")]
        public Gem PlacedGem = null;

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            //When no playing (so in editor outside of play mode) return the given preview Sprite, otherwise return null 
            //wo that tile is "invisible" during play (the gem are gameobject handled by our system, not the tilemap)
            tileData.sprite = !Application.isPlaying ? PreviewEditorSprite : null;
        }

        public override bool StartUp(Vector3Int position, ITilemap tilemap, GameObject go)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                return false;
#endif
            //This tile is only used in editor to help design the level. At runtime, we notify the board that this tile is
            //a place for a gem, then delete the GameObject that was just visual aid at design time. The Board will take care
            //of creating a gem there.

            Board.RegisterCell(position, PlacedGem);
                
            return base.StartUp(position, tilemap, go);
        }
    }
}
