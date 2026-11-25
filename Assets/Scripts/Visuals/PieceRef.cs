using UnityEngine;
using CrittersOfCalamity.Engine;
using CrittersOfCalamity.Model;

namespace CrittersOfCalamity.Visuals
{
    public enum PieceKind { Unit, Building, Token, FactionTile }

    /// <summary>
    /// Attached to every piece on the 3D board so hovering or clicking it can show that piece's details.
    /// Works for every player, whoever owns the piece.
    /// </summary>
    public class PieceRef : MonoBehaviour
    {
        public PieceKind Kind;
        public HexCoord Coord;
        public UnitInstance Unit;
        public BuildingInstance Building;
        public TokenInstance Token;
    }
}
