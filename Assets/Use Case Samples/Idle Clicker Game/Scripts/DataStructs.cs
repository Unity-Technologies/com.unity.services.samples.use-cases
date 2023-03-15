using System;
using System.Collections.Generic;
using System.Linq;

namespace Unity.Services.Samples.IdleClickerGame
{
    [Serializable]
    public struct IdleClickerResult
    {
        public long timestamp;
        public long currencyBalance;
        public List<WellInfo> wells_level1;
        public List<WellInfo> wells_level2;
        public List<WellInfo> wells_level3;
        public List<WellInfo> wells_level4;
        public List<Coord> obstacles;
        public Dictionary<string, int> unlockCounters;

        public override string ToString()
        {
            var unlockCountersStr = string.Join(",", unlockCounters.Select(kv => $"{kv.Key}={kv.Value}"));
            return $"timestamp:{timestamp}, " +
                $"currencyBalance:{currencyBalance}, " +
                $"wells_level1:[{string.Join(",", wells_level1)}], " +
                $"wells_level2:[{string.Join(",", wells_level2)}], " +
                $"wells_level3:[{string.Join(",", wells_level3)}], " +
                $"wells_level4:[{string.Join(",", wells_level4)}], " +
                $"obstacles:[{string.Join(",", obstacles)}], " +
                $"unlockCounters:[{unlockCountersStr}]";
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
    public struct WellInfo
    {
        public int x;
        public int y;
        public long timestamp;
        public int wellLevel;

        public override string ToString()
        {
            return $"({x},{y}, level:{wellLevel}, timestamp:{timestamp})";
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
