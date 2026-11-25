using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrittersOfCalamity.Model
{
    /// <summary>
    /// Core Rulebook: Eight Faction Edition
    /// 1. Curly Tail's Warband
    /// 2. Horde of the Millions
    /// 3. Platypus Papacy
    /// 4. Trouble Making Tanukis
    /// 5. The Necro-mouser
    /// 6. Firestarting Squirrels
    /// 7. The Capybara Caravan
    /// 8. The Briared Flower
    /// </summary>
    public enum FactionId
    {
        CurlyTail = 0,
        HordeOfTheMillions = 1,
        PlatypusPapacy = 2,
        TroubleMakingTanukis = 3,
        Necromouser = 4,
        FirestartingSquirrels = 5,
        CapybaraCaravan = 6,
        BriaredFlower = 7,
        GoatCabal = 8
    }

    public enum FactionDifficultyTier
    {
        RecommendedForNewPlayers
    }

    public enum ResourceType
    {
        Wood,
        Stone,
        Gold,
        Charcoal // Firestarting Squirrels faction resource (6 cards)
    }

    /// <summary>
    /// Rulebook Part 1 & Part 2:
    /// "The board has about 108 tiles: roughly 90 forest tiles, 14 stream tiles, and 4 ruin tiles."
    /// "Tile types are forest (green), ruin (gray), stream (light blue), and unique faction tiles (Fallen Leaves)."
    /// </summary>
    public enum TileTerrainType
    {
        Forest, // Green (90 tiles)
        Ruin,   // Gray (4 tiles)
        Stream  // Light Blue (14 tiles)
    }

    public enum FactionOverlayTileType
    {
        None = 0,
        Miasma_Hidden,       // Necro-mouser facedown setup
        Miasma_Revealed,     // Necro-mouser faceup
        Fire,                // Firestarting Squirrels (6 tiles)
        FallenLeaf_Unflipped,// Trouble Making Tanukis facedown (13 stack: 8 Prank, 5 Trap)
        FallenLeaf_Trap,     // Flipped Trap Leaf
        FallenLeaf_Prank,    // Flipped Prank Leaf
        CapybaraStream       // Capybara Caravan Stream Tile overlay (8 tiles)
    }

    public enum TurnPhase
    {
        PreGame_DraftRoll,
        PreGame_FactionPick,
        PreGame_TurnOrderRoll,
        PreGame_CapybaraSpecialSetup,
        PreGame_ReverseFactionSetup,
        PreGame_TanukiCoinSetup,
        DrawPhase,
        ExcavationPhase,
        MainPhase,
        EndPhase,
        GameOver
    }

    public enum BuildingType
    {
        None = 0,
        WarParty,        // Curly Tail's Warband (5 pieces)
        Burrow,          // Horde of the Millions (3 pieces)
        TheCitadel,      // Platypus Papacy (1 piece, can be placed on forest or stream)
        Shrine,          // Trouble Making Tanukis (3 pieces)
        Bunker,          // Firestarting Squirrels (3 pieces)
        Wallow,          // Capybara Caravan (5 pieces)
        PricklyThicket,  // The Briared Flower (4 pieces)
        SummoningCircle  // The Goat Cabal (3 pieces)
    }

    public enum TokenType
    {
        None = 0,
        Gluttony,        // Horde of the Millions (3 pieces)
        Catapult,        // Horde of the Millions (2 pieces, any player can craft, 2 uses each)
        HolyRelic,       // Platypus Papacy (2 pieces, can be placed on stream with Citadel)
        SupplyLine,      // Curly Tail's Warband (8 pieces)
        Coin,            // Trouble Making Tanukis (20 pieces, 12 placed in setup after all players)
        TarPit,          // Firestarting Squirrels (3 pieces)
        InitiationRite,  // The Goat Cabal (2 pieces)
        Effigy           // The Goat Cabal (3 pieces)
    }

    [Serializable]
    public struct HexCoord : IEquatable<HexCoord>
    {
        public int q;
        public int r;

        public HexCoord(int q, int r)
        {
            this.q = q;
            this.r = r;
        }

        public int s => -q - r;

        public static int Distance(HexCoord a, HexCoord b)
        {
            return (Mathf.Abs(a.q - b.q) + Mathf.Abs(a.q + a.r - b.q - b.r) + Mathf.Abs(a.r - b.r)) / 2;
        }

        public static readonly HexCoord[] Directions = new HexCoord[]
        {
            new HexCoord(1, 0), new HexCoord(1, -1), new HexCoord(0, -1),
            new HexCoord(-1, 0), new HexCoord(-1, 1), new HexCoord(0, 1)
        };

        public HexCoord Neighbor(int dirIndex)
        {
            var d = Directions[((dirIndex % 6) + 6) % 6];
            return new HexCoord(q + d.q, r + d.r);
        }

        public List<HexCoord> GetNeighbors()
        {
            var list = new List<HexCoord>(6);
            for (int i = 0; i < 6; i++)
                list.Add(Neighbor(i));
            return list;
        }

        public Vector3 ToWorldPosition(float hexSize = 1.15f)
        {
            float x = hexSize * (Mathf.Sqrt(3f) * q + Mathf.Sqrt(3f) / 2f * r);
            float z = hexSize * (1.5f * r);
            return new Vector3(x, 0f, z);
        }

        public bool Equals(HexCoord other) => q == other.q && r == other.r;
        public override bool Equals(object obj) => obj is HexCoord other && Equals(other);
        public override int GetHashCode() => (q * 397) ^ r;
        public override string ToString() => $"({q}, {r})";
    }

    [Serializable]
    public class ResourceCost
    {
        public int Wood;
        public int Stone;
        public int Gold;
        public int Charcoal;
        public int AnyResource;

        public ResourceCost(int wood = 0, int stone = 0, int gold = 0, int any = 0, int charcoal = 0)
        {
            Wood = wood;
            Stone = stone;
            Gold = gold;
            AnyResource = any;
            Charcoal = charcoal;
        }

        public override string ToString()
        {
            var parts = new List<string>();
            if (Wood > 0) parts.Add($"{Wood} Wood");
            if (Stone > 0) parts.Add($"{Stone} Stone");
            if (Gold > 0) parts.Add($"{Gold} Gold");
            if (Charcoal > 0) parts.Add($"{Charcoal} Charcoal");
            if (AnyResource > 0) parts.Add($"{AnyResource} Any");
            return parts.Count > 0 ? string.Join(", ", parts) : "Free";
        }
    }

    [Serializable]
    public class RelicCardDef
    {
        public string Id;
        public string Name;
        public int CopiesInDeck;
        public ResourceCost Cost;
        public ResourceCost DisassembleGain;
        public bool DisassembleChoiceWoodOrStone;
        public bool KeepOnTableWhenCrafted;
        public bool HasCraftedEffect;
        public string EffectText;
    }

    [Serializable]
    public class UnitDef
    {
        public string Id;
        public string Name;
        public FactionId Faction;
        public bool IsSpecialUnit;
        public int BaseAttack;
        public int BaseDefense;
        public int BaseMaxHP;
        public int SupplyCount;
        public string SummoningCondition;
        public string AbilitiesText;
    }

    [Serializable]
    public class EquipmentDef
    {
        public string Id;
        public string Name;
        public FactionId Faction;
        public string TargetUnitId;
        public int MaxPerUnit;
        public int SupplyCount;
        public ResourceCost Cost;
        public int BonusAttack;
        public int BonusDefense;
        public int BonusHP;
        public int BonusMove;
        public int AuthorityOnCraft;
        public string EffectText;
    }

    [Serializable]
    public class FactionSubDeckCardDef
    {
        public string Id;
        public string DeckName;
        public string Title;
        public int Copies;
        public ResourceCost Cost;
        public string EffectText;
    }

    [Serializable]
    public class FactionDef
    {
        public FactionId Id;
        public string Name;
        public FactionDifficultyTier Tier;
        public int DifficultyRatingOutOf10;
        public string ShortSummary;
        public string HowToScoreAuthority; // plain-language summary of the faction's Authority sources (Part 17)
        public string LoreDescription;
        public string SetupInstructions;
        public Color PrimaryColor;
        public Color SecondaryColor;
        public BuildingType PrimaryBuilding;
        public ResourceCost PrimaryBuildingCost;
        public string BuildingRules;
        public List<string> SpeciesTraits = new List<string>();
        public List<string> EnemyAuthorityScoring = new List<string>();
        public List<string> MaterialsManifest = new List<string>();
        public List<UnitDef> Units = new List<UnitDef>();
        public List<EquipmentDef> Equipment = new List<EquipmentDef>();
        public List<FactionSubDeckCardDef> SubDeckCards = new List<FactionSubDeckCardDef>();
    }
}
