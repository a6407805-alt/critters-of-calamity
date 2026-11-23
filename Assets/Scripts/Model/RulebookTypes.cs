using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrittersOfCalamity.Model
{
    public enum FactionId
    {
        Necromouser = 0,
        HordeOfTheMillions = 1,
        GoatCabal = 2,
        CurlyTail = 3,
        FirestartingSquirrels = 4,
        PlatypusPapacy = 5,
        CarnivorousQueen = 6,
        GlacierKingdom = 7,
        TroubleMakingTanukis = 8,
        CapybaraCaravan = 9,
        BriaredFlower = 10
    }

    public enum FactionDifficultyTier
    {
        BeginnerFriendly,
        Intermediate,
        Expert
    }

    public enum ResourceType
    {
        Wood,
        Stone,
        Gold,
        Charcoal // Firestarting Squirrels faction resource (6 cards)
    }

    public enum TileTerrainType
    {
        Forest, // Green
        Ruin,   // Gray
        River   // Light Blue
    }

    public enum FactionOverlayTileType
    {
        None = 0,
        Miasma_Hidden,       // Necromouser face-down setup
        Miasma_Revealed,     // Necromouser face-up
        Fire,                // Firestarting Squirrels
        Glacier,             // Glacier Kingdom
        FallenLeaf_Unflipped,// Trouble-Making Tanukis hidden
        FallenLeaf_Trap,     // Flipped Trap Leaf
        FallenLeaf_Prank,    // Flipped Prank Leaf
        CapybaraRiver        // Capybara Caravan River Tile overlay
    }

    public enum TurnPhase
    {
        PreGame_DraftRoll,
        PreGame_FactionPick,
        PreGame_TurnOrderRoll,
        PreGame_CapybaraSpecialSetup,
        PreGame_FactionSetup,
        DrawPhase,
        ExcavationPhase,
        MainPhase,
        EndPhase,
        GameOver
    }

    public enum BuildingType
    {
        None = 0,
        Burrow,          // Horde of the Millions
        Bunker,          // Firestarting Squirrels
        TheCitadel,      // Platypus Papacy
        Igloo,           // Glacier Kingdom
        FrozenHarbor,    // Glacier Kingdom
        WarParty,        // Curly Tail
        Chrysalis,       // Carnivorous Queen (also acts as 0/2/2 defending unit)
        SummoningCircle, // Goat Cabal
        Shrine,          // Trouble-Making Tanukis
        Wallow,          // Capybara Caravan
        PricklyThicket   // The Briared Flower
    }

    public enum TokenType
    {
        None = 0,
        Gluttony,        // Horde of the Millions
        Catapult,        // Horde of the Millions
        TarPit,          // Firestarting Squirrels
        HolyRelic,       // Platypus Papacy
        SupplyLine,      // Curly Tail
        Snowfall_Lv1,    // Glacier Kingdom
        Snowfall_Lv2,    // Glacier Kingdom
        Snowfall_Lv3,    // Glacier Kingdom
        Egg,             // Carnivorous Queen
        InitiationRite,  // Goat Cabal
        Effigy,          // Goat Cabal
        Coin             // Trouble-Making Tanukis
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
        public bool KeepFaceUpWhenCrafted;
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
        public string LoreDescription;
        public string SetupInstructions;
        public Color PrimaryColor;
        public Color SecondaryColor;
        public BuildingType PrimaryBuilding;
        public BuildingType SecondaryBuilding;
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
