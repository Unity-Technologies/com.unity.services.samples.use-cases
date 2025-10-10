using UnityEngine;
using UnityEngine.Tilemaps;

namespace Match3
{
    /// <summary>
    /// This is an edit time tile, used to define the position of cell that will spawn cell under them. At edit time and outside
    /// play mode, the PReview Sprite will be displayed on the tilemap but at runtime, the tile will be empty as its only a
    /// game logic placement.
    /// </summary>
    [CreateAssetMenu(fileName = "GemSpawnerPlacerTile", menuName = "2D Match/Tile/Gem Spawner Placer")]
    public class GemSpawner : TileBase
    {
        public Sprite EditorPreviewSprite;

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            tileData.sprite = !Application.isPlaying ? EditorPreviewSprite : null;
        }

        public override bool StartUp(Vector3Int position, ITilemap tilemap, GameObject go)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                return false;
#endif
        
            //This tile is only used in editor to help design the level. At runtime, we notify the board that this tile is
            //a place for a gem. The Board will take care of creating a gem there.
            Board.RegisterSpawner(position);

            return base.StartUp(position, tilemap, go);
        }
    }
}
