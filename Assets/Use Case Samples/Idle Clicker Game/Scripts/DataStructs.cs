
using System;
using System.Collections.Generic;

namespace GameOperationsSamples
{
    namespace IdleClickerGame
    {
        [Serializable]
        public struct UpdatedState
        {
            public long timestamp;
            public List<Coord> obstacles;
            public List<FactoryInfo> factories;

            public override string ToString()
            {
                return $"timestamp:{timestamp} " +
                    $"[obstacles:{string.Join(",", obstacles)}] " +
                    $"[factories:{string.Join(",", factories)}]";
            }
        }

        [Serializable]
        public struct Coord
        {
            public int x;
            public int y;

            public override string ToString()
            {
                return $"({x},{y})";
            }
        }

        [Serializable]
        public struct FactoryInfo
        {
            public int x;
            public int y;
            public long timestamp;

            public override string ToString()
            {
                return $"({x},{y}, timestamp:{timestamp})";
            }
        }

        [Serializable]
        public struct PlacePieceResult
        {
            public string placePieceResult;
            public long timestamp;
            public List<Coord> obstacles;
            public List<FactoryInfo> factories;

            public override string ToString()
            {
                return $"result:{placePieceResult} timestamp:{timestamp} " +
                    $"obstacles:[{string.Join(",", obstacles)}] " +
                    $"factories:[{string.Join(",", factories)}]";
            }
        }

        [Serializable]
        public struct CoordParam
        {
            public Coord coord;

            public override string ToString()
            {
                return coord.ToString();
            }
        }
    }
}
