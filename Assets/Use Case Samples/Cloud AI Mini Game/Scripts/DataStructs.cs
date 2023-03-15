using System;
using System.Collections.Generic;

namespace Unity.Services.Samples.CloudAIMiniGame
{
    [Serializable]
    public struct UpdatedState
    {
        public bool isNewGame;
        public bool isNewMove;
        public bool isPlayerTurn;
        public bool isGameOver;
        public string status;
        public List<Coord> aiPieces;
        public List<Coord> playerPieces;
        public int winCount;
        public int lossCount;
        public int tieCount;

        public override string ToString()
        {
            return $"status:{status} " +
                $"isNewGame:{isNewGame} " +
                $"isNewMove:{isNewMove} " +
                $"isPlayerTurn:{isPlayerTurn} " +
                $"isGameOver:{isGameOver} " +
                $"[aiPieces:{string.Join(",", aiPieces)}] " +
                $"[playerPieces:{string.Join(",", playerPieces)}] " +
                $"W/L/T: {winCount}/{lossCount}/{tieCount}";
        }
    }

    [Serializable]
    public struct Coord
    {
        public int x;
        public int y;

        public Coord(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public override string ToString()
        {
            return $"({x},{y})";
        }
    }

    [Serializable]
    public struct CoordParam
    {
        public Coord coord;

        public CoordParam(Coord coord) { this.coord = coord; }

        public override string ToString()
        {
            return coord.ToString();
        }
    }
}
