using UnityEngine;
using UnityEngine.Tilemaps;

namespace Match3
{
    [CreateAssetMenu(fileName = "ObstaclePlacer", menuName = "2D Match/Tile/Obstacle Placer")]
    public class ObstaclePlacer : TileBase
    {
        public Sprite PreviewEditorSprite;
        public Obstacle ObstaclePrefab;

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            tileData.sprite = !Application.isPlaying ? PreviewEditorSprite : null;
        }

        public override bool StartUp(Vector3Int position, ITilemap tilemap, GameObject go)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                return false;
#endif

            var newObstacle = Instantiate(ObstaclePrefab);
            newObstacle.Init(position);

            return true;
        }
    }
}
