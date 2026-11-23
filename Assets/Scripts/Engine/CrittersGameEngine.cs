using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CrittersOfCalamity.Model;

namespace CrittersOfCalamity.Engine
{
    [Serializable]
    public class UnitInstance
    {
        public string InstanceId;
        public int OwnerPlayerIndex;
        public FactionId Faction;
        public UnitDef Definition;
        public int CurrentHP;
        public int MaxHP;
        public int BonusAttack;
        public int BonusDefense;
        public bool IsFrozen;
        public bool IsHidden;
        public bool IsGuest;
        public bool CannotMoveThisTurn;
        public bool HasMovedThisTurn;
        public List<EquipmentDef> EquippedItems = new List<EquipmentDef>();

        public int EffectiveAttack => Mathf.Max(0, Definition.BaseAttack + BonusAttack + EquippedItems.Sum(e => e.BonusAttack));
        public int EffectiveDefense => Mathf.Max(0, Definition.BaseDefense + BonusDefense + EquippedItems.Sum(e => e.BonusDefense));
    }

    [Serializable]
    public class BuildingInstance
    {
        public BuildingType Type;
        public int OwnerPlayerIndex;
        public FactionId Faction;
        public int CurrentHP = 2; // Used for Chrysalis (0/2/2)
    }

    [Serializable]
    public class TokenInstance
    {
        public TokenType Type;
        public int OwnerPlayerIndex;
        public FactionId Faction;
    }

    [Serializable]
    public class HexTileState
    {
        public HexCoord Coord;
        public TileTerrainType BaseTerrain;
        public FactionOverlayTileType Overlay = FactionOverlayTileType.None;
        public int OverlayOwnerPlayerIndex = -1;
        public BuildingInstance Building = null; // Max 1 building per tile (Rulebook Part 3 Line 144)
        public List<TokenInstance> Tokens = new List<TokenInstance>(); // Max 2 tokens per tile (Rulebook Part 3 Line 144)
        public List<UnitInstance> Units = new List<UnitInstance>();
        public bool LootedThisTurn = false;
        public bool HasIcebergWall = false;
        public int IcebergWallHP = 2;
        public int IcebergWallOwner = -1;
        public bool IsBoardEdge = false;
        public bool HasStatueOfGoddessBuff = false;
        public bool MarkedByRoyalDecree = false;

        public TileTerrainType EffectiveTerrain
        {
            get
            {
                if (Overlay == FactionOverlayTileType.CapybaraRiver)
                    return TileTerrainType.River;
                return BaseTerrain;
            }
        }
    }

    [Serializable]
    public class PlayerState
    {
        public int PlayerIndex;
        public string DisplayName;
        public FactionDef Faction;
        public int Authority = 0;
        public int SetupDiceRoll = 0;

        // Card piles
        public List<ResourceType> ResourcePile = new List<ResourceType>();
        public List<RelicCardDef> RelicHand = new List<RelicCardDef>();
        public List<RelicCardDef> FaceUpRelics = new List<RelicCardDef>();
        public List<FactionSubDeckCardDef> PurchasedTradedGoods = new List<FactionSubDeckCardDef>();

        // Faction-specific meters & state
        public int NecromouserCauldronDeadCount = 0;
        public int NecromouserSpecialSummonsUsed = 0; // Cap 4 per game
        public bool NecromouserAwakened = false;

        public int GoatPurgatoryCount = 0;
        public List<string> KilledDemonIds = new List<string>();

        public int CurlyTailThirstForGlory = 0; // 0-4
        public int CurlyTailHunger = 0;         // 0-4
        public int CurlyTailMomentum = 0;       // 0-4
        public bool CurlyTailWonBattleThisTurn = false;

        public int SquirrelsCraftedFirestarters = 0;

        public int PlatypusFaith = 0;           // 0-5
        public int PlatyPopeLevel = 0;          // 0-5
        public int CrusadeTurnsRemaining = 0;
        public bool PlatyPopeKilled = false;

        public int QueenRage = 0;               // 0-15
        public int QueenFeeding = 0;            // 0-7
        public int QueenStage = 1;              // 1-4
        public List<EquipmentDef> DraftedMutations = new List<EquipmentDef>();

        public bool GlacierBlizzardActive = false;

        public int BriaredFlowerThorns = 6;     // 0-6
        public int ThistleBarrageUsesThisTurn = 0;
    }

    [Serializable]
    public class BattleReport
    {
        public HexCoord TileCoord;
        public int AttackerPlayerIndex;
        public int DefenderPlayerIndex;
        public int AttackerDice1;
        public int AttackerDice2;
        public int DefenderDice1;
        public int DefenderDice2;
        public int AttackerDiceBonus;
        public int DefenderDiceBonus;
        public int AttackerUnitAttackSum;
        public int DefenderUnitDefenseSum;
        public int AttackerTotal;
        public int DefenderTotal;
        public bool IsTie;
        public int WinnerPlayerIndex;
        public int LoserPlayerIndex;
        public int WinnerDamageDealt;
        public int MandatoryLoserDamageDealt;
        public List<string> DestroyedUnitNames = new List<string>();
        public string SummaryLog;
    }

    /// <summary>
    /// Full deterministic rules engine implementing Eben's Critters of Calamity rulebook:
    /// - 4 Players, 30 Authority victory condition
    /// - Full-scale 217-Hex Continent Map (Radius 8) with Rivers, Ruins, and Forests
    /// - 40-card Relic Deck + 5-slot Excavation Area + Discard Pile
    /// - 50-card Resource Deck (20 Stone, 20 Wood, 10 Gold) + Burned Area
    /// - Faction Sub-Decks: 8-card Forage Deck, 14-card Grimoire Deck, 8-card Traded Goods Deck + 3-card Stall
    /// - 4-Phase Turn Structure (1st Draw, 2nd Excavation, 3rd Main [3 actions], 4th End)
    /// - 5-Phase 2d6 Battle Calculator with Damage Cap & Mandatory Loser Damage
    /// </summary>
    public class CrittersGameEngine
    {
        public const int VictoryAuthorityThreshold = 30;
        public const int ExcavationAreaCapacity = 5;
        public const int DefaultContinentMapRadius = 8; // 217 Hexes!

        public int CurrentMapRadius = DefaultContinentMapRadius;
        public List<PlayerState> Players = new List<PlayerState>();
        public Dictionary<HexCoord, HexTileState> Board = new Dictionary<HexCoord, HexTileState>();

        public List<RelicCardDef> RelicDeck = new List<RelicCardDef>();
        public List<RelicCardDef> RelicDiscardPile = new List<RelicCardDef>();
        public List<RelicCardDef> ExcavationArea = new List<RelicCardDef>();

        public List<ResourceType> ResourceDeck = new List<ResourceType>();
        public List<ResourceType> BurnedResources = new List<ResourceType>();

        // Faction Specific Decks (Rulebook Material 9)
        public List<FactionSubDeckCardDef> ForageDeck = new List<FactionSubDeckCardDef>();
        public List<FactionSubDeckCardDef> ForageDiscard = new List<FactionSubDeckCardDef>();

        public List<FactionSubDeckCardDef> GrimoireDeck = new List<FactionSubDeckCardDef>();
        public List<FactionSubDeckCardDef> GrimoireDiscard = new List<FactionSubDeckCardDef>();

        public List<FactionSubDeckCardDef> TradedGoodsDeck = new List<FactionSubDeckCardDef>();
        public List<FactionSubDeckCardDef> TradedGoodsStall = new List<FactionSubDeckCardDef>(); // 3 faceup stall cards
        public List<FactionSubDeckCardDef> TradedGoodsDiscard = new List<FactionSubDeckCardDef>();

        public int CurrentTurnPlayerIndex = 0;
        public int TurnNumber = 1;
        public TurnPhase CurrentPhase = TurnPhase.PreGame_DraftRoll;

        // Excavation Phase flags (once each in any order per turn)
        public bool UsedUnearthThisTurn;
        public bool UsedFactionCraftThisTurn;
        public bool UsedAssembleThisTurn;
        public bool UsedDisassembleThisTurn;

        // Main Phase actions (up to 3 per turn)
        public int MainPhaseActionsRemaining = 3;

        public int WinnerPlayerIndex = -1;
        public List<string> GameLog = new List<string>();
        public BattleReport LastBattleReport;

        private System.Random _rng;
        private int _unitUidCounter = 1;

        public event Action OnStateChanged;
        public event Action<BattleReport> OnBattleResolved;

        public CrittersGameEngine(int seed = 0)
        {
            _rng = seed == 0 ? new System.Random() : new System.Random(seed);
        }

        public void Log(string message)
        {
            string entry = $"[T{TurnNumber}] {message}";
            GameLog.Add(entry);
            if (GameLog.Count > 150)
                GameLog.RemoveAt(0);
            Debug.Log($"[CrittersEngine] {entry}");
        }

        public int RollD6() => _rng.Next(1, 7);
        public int Roll2D6(out int d1, out int d2)
        {
            d1 = RollD6();
            d2 = RollD6();
            return d1 + d2;
        }

        public void StartNewMatch(List<FactionId> chosenFactions, List<string> playerNames = null, int mapRadius = DefaultContinentMapRadius)
        {
            CurrentMapRadius = Mathf.Max(6, mapRadius);
            Players.Clear();
            Board.Clear();
            GameLog.Clear();
            WinnerPlayerIndex = -1;
            TurnNumber = 1;

            // 1. Build Full-Scale Hexagonal Continent Map (Radius 8 = 217 hexes: Forest, Ruin, River)
            GenerateContinentHexMap(CurrentMapRadius);

            // 2. Build & Shuffle 40-Card Relic Deck and 50-Card Resource Deck (Rulebook Part 1)
            RelicDeck = RulebookDatabase.BuildFull40RelicDeck();
            Shuffle(RelicDeck);
            RelicDiscardPile.Clear();
            ExcavationArea.Clear();

            ResourceDeck = RulebookDatabase.BuildStandard50ResourceDeck();
            Shuffle(ResourceDeck);
            BurnedResources.Clear();

            // 3. Build & Shuffle Faction-Specific Decks (Forage 8, Grimoire 14, Traded Goods 8)
            ForageDeck = RulebookDatabase.BuildMillionsForageDeck8();
            Shuffle(ForageDeck);
            ForageDiscard.Clear();

            GrimoireDeck = RulebookDatabase.BuildGoatGrimoireDeck14();
            Shuffle(GrimoireDeck);
            GrimoireDiscard.Clear();

            TradedGoodsDeck = RulebookDatabase.BuildCapybaraTradedGoodsDeck8();
            Shuffle(TradedGoodsDeck);
            TradedGoodsStall.Clear();
            TradedGoodsDiscard.Clear();
            RefillTradedGoodsStall();

            // 4. Initialize 4 Players with their asymmetric Factions
            var allFactions = RulebookDatabase.GetAllFactions();
            for (int i = 0; i < chosenFactions.Count; i++)
            {
                var fDef = allFactions.First(f => f.Id == chosenFactions[i]);
                var p = new PlayerState
                {
                    PlayerIndex = i,
                    DisplayName = (playerNames != null && i < playerNames.Count) ? playerNames[i] : $"Player {i + 1}",
                    Faction = fDef,
                    Authority = fDef.Id == FactionId.GoatCabal ? 5 : 0 // Goat Cabal starts with 5 Authority (Line 559)
                };

                // Part 4: Starting a Game - Draw 2 random cards from resource deck, 1 random card from relic deck
                for (int r = 0; r < 2; r++)
                    p.ResourcePile.Add(DrawResourceCard());
                var startRelic = DrawRelicCard();
                if (startRelic != null)
                    p.RelicHand.Add(startRelic);

                Players.Add(p);
            }

            // Part 4: Place top 5 cards of relic deck face-up into Excavation Area
            RefillExcavationArea();

            // Roll 2d6 for turn order and execute each faction's board setup
            foreach (var p in Players)
            {
                p.SetupDiceRoll = Roll2D6(out int d1, out int d2);
                Log($"{p.DisplayName} ({p.Faction.Name}) rolled {d1}+{d2}={p.SetupDiceRoll} for setup & turn order.");
            }

            // Perform canonical faction board setup for all 4 players at distinct quadrant anchor tiles
            PerformCanonicalFactionSetups(CurrentMapRadius);

            CurrentTurnPlayerIndex = 0;
            BeginTurn(0);
        }

        private void GenerateContinentHexMap(int radius)
        {
            for (int q = -radius; q <= radius; q++)
            {
                int r1 = Mathf.Max(-radius, -q - radius);
                int r2 = Mathf.Min(radius, -q + radius);
                for (int r = r1; r <= r2; r++)
                {
                    var coord = new HexCoord(q, r);
                    int distFromCenter = HexCoord.Distance(coord, new HexCoord(0, 0));
                    bool isEdge = (distFromCenter == radius);

                    TileTerrainType terrain = TileTerrainType.Forest;

                    // Winding Anankian Great River + Tributary Rivers across the 217-hex Continent
                    bool isMainRiver = (q == 0 && Mathf.Abs(r) <= radius - 1) ||
                                       (r == 0 && Mathf.Abs(q) <= radius - 2 && Mathf.Abs(q) >= 2) ||
                                       (q + r == 0 && Mathf.Abs(q) <= radius - 2 && Mathf.Abs(q) >= 1);

                    // Ancient Anankian Ruins distributed symmetrically across inner, mid, and outer rings
                    bool isRuin = !isMainRiver && (
                        (distFromCenter == 0) ||
                        (distFromCenter == 2 && (q == 0 || r == 0 || q + r == 0)) ||
                        (distFromCenter == 4 && (Mathf.Abs(q) == 2 || Mathf.Abs(r) == 2)) ||
                        (distFromCenter == 6 && (q == 0 || r == 0 || q + r == 0 || Mathf.Abs(q) == 3)) ||
                        (distFromCenter == 7 && (Mathf.Abs(q - r) == 7))
                    );

                    if (isMainRiver)
                        terrain = TileTerrainType.River;
                    else if (isRuin)
                        terrain = TileTerrainType.Ruin;

                    Board[coord] = new HexTileState
                    {
                        Coord = coord,
                        BaseTerrain = terrain,
                        IsBoardEdge = isEdge
                    };
                }
            }
            Log($"Generated Full Continent Hex Map: {Board.Count} Hex Tiles (Radius {radius}: " +
                $"{Board.Values.Count(t => t.BaseTerrain == TileTerrainType.Forest)} Forest, " +
                $"{Board.Values.Count(t => t.BaseTerrain == TileTerrainType.Ruin)} Ruin, " +
                $"{Board.Values.Count(t => t.BaseTerrain == TileTerrainType.River)} River).");
        }

        private void PerformCanonicalFactionSetups(int radius)
        {
            // Quadrant spawn anchors on the large Continent map (distance >= 10 tiles apart, > 4 tiles from everyone!)
            int anchorRing = Mathf.Max(3, radius - 2);
            HexCoord[] spawnAnchors = new HexCoord[]
            {
                new HexCoord(-anchorRing, anchorRing / 2),
                new HexCoord(anchorRing, -anchorRing / 2),
                new HexCoord(-anchorRing / 2, -anchorRing / 2),
                new HexCoord(anchorRing / 2, anchorRing / 2)
            };

            // Capybara Caravan Special Setup happens BEFORE any other player sets up (Rulebook Line 1336)
            foreach (var p in Players)
            {
                if (p.Faction.Id == FactionId.CapybaraCaravan)
                {
                    int placedRivers = 0;
                    foreach (var tile in Board.Values)
                    {
                        if (tile.BaseTerrain == TileTerrainType.Forest && placedRivers < 8)
                        {
                            bool adjRiver = tile.Coord.GetNeighbors().Any(n => Board.ContainsKey(n) && Board[n].EffectiveTerrain == TileTerrainType.River);
                            if (adjRiver)
                            {
                                tile.Overlay = FactionOverlayTileType.CapybaraRiver;
                                tile.OverlayOwnerPlayerIndex = p.PlayerIndex;
                                placedRivers++;
                            }
                        }
                    }
                    Log($"{p.DisplayName} completed Capybara Caravan Special Setup (placed {placedRivers} connected River Tiles onto Forest Tiles).");
                }
            }

            for (int i = 0; i < Players.Count; i++)
            {
                var p = Players[i];
                var homeCoord = spawnAnchors[i % spawnAnchors.Length];
                if (!Board.ContainsKey(homeCoord))
                    homeCoord = new HexCoord(0, 0);
                var homeTile = Board[homeCoord];
                homeTile.BaseTerrain = TileTerrainType.Forest; // Ensure legal forest start tile

                switch (p.Faction.Id)
                {
                    case FactionId.Necromouser:
                        // Place 3 Miasma Tiles facedown (not adjacent), start with 3 Skeletons on Cauldron
                        p.NecromouserCauldronDeadCount = 3;
                        PlaceOverlayTile(homeCoord, FactionOverlayTileType.Miasma_Hidden, p.PlayerIndex);
                        var m2 = new HexCoord(homeCoord.q / 2, homeCoord.r / 2);
                        var m3 = new HexCoord(-homeCoord.q / 2, homeCoord.r / 2);
                        if (Board.ContainsKey(m2) && HexCoord.Distance(homeCoord, m2) >= 2)
                            PlaceOverlayTile(m2, FactionOverlayTileType.Miasma_Hidden, p.PlayerIndex);
                        if (Board.ContainsKey(m3) && HexCoord.Distance(homeCoord, m3) >= 2 && HexCoord.Distance(m2, m3) >= 2)
                            PlaceOverlayTile(m3, FactionOverlayTileType.Miasma_Hidden, p.PlayerIndex);
                        Log($"{p.DisplayName} placed 3 facedown Miasma Tiles and 3 Dead Skeletons on the Cauldron.");
                        break;

                    case FactionId.HordeOfTheMillions:
                        // 2 Burrows on 2 separate forest tiles + 2 Millions each
                        PlaceBuilding(homeCoord, BuildingType.Burrow, p);
                        SpawnUnit(homeCoord, "millions", p, 2);
                        var secondBurrow = homeCoord.GetNeighbors().FirstOrDefault(n => Board.ContainsKey(n) && Board[n].EffectiveTerrain == TileTerrainType.Forest);
                        if (Board.ContainsKey(secondBurrow))
                        {
                            PlaceBuilding(secondBurrow, BuildingType.Burrow, p);
                            SpawnUnit(secondBurrow, "millions", p, 2);
                        }
                        break;

                    case FactionId.GoatCabal:
                        // 4 Goat units + 1 Summoning Circle, 1 Goat on Purgatory, start with 5 Authority
                        p.GoatPurgatoryCount = 1;
                        PlaceBuilding(homeCoord, BuildingType.SummoningCircle, p);
                        SpawnUnit(homeCoord, "goat", p, 4);
                        break;

                    case FactionId.CurlyTail:
                        // Curly Tail, 4 Warband units, 1 War Party
                        PlaceBuilding(homeCoord, BuildingType.WarParty, p);
                        SpawnUnit(homeCoord, "curly_tail", p, 1);
                        SpawnUnit(homeCoord, "warband", p, 4);
                        break;

                    case FactionId.FirestartingSquirrels:
                        // 1 Squirrel on 3 adjacent forest tiles, 1 Bunker, 2 Charcoal in resource pile, 1 Acorn Firestarter
                        PlaceBuilding(homeCoord, BuildingType.Bunker, p);
                        SpawnUnit(homeCoord, "squirrel", p, 1);
                        int adjCount = 0;
                        foreach (var n in homeCoord.GetNeighbors())
                        {
                            if (Board.ContainsKey(n) && Board[n].EffectiveTerrain == TileTerrainType.Forest && adjCount < 2)
                            {
                                SpawnUnit(n, "squirrel", p, 1);
                                adjCount++;
                            }
                        }
                        p.ResourcePile.Add(ResourceType.Charcoal);
                        p.ResourcePile.Add(ResourceType.Charcoal);
                        p.SquirrelsCraftedFirestarters = 1;
                        break;

                    case FactionId.PlatypusPapacy:
                        // The Citadel, 3 Pilgrims, Platy-Pope
                        PlaceBuilding(homeCoord, BuildingType.TheCitadel, p);
                        SpawnUnit(homeCoord, "pilgrim", p, 3);
                        SpawnUnit(homeCoord, "platy_pope", p, 1);
                        break;

                    case FactionId.CarnivorousQueen:
                        // Queen + 1 Chrysalis on forest tile; 3 other forest tiles get 1 Larvae + 1 Egg Token
                        PlaceBuilding(homeCoord, BuildingType.Chrysalis, p);
                        SpawnUnit(homeCoord, "carnivorous_queen", p, 1);
                        int eggsPlaced = 0;
                        foreach (var n in homeCoord.GetNeighbors())
                        {
                            if (Board.ContainsKey(n) && Board[n].EffectiveTerrain == TileTerrainType.Forest && eggsPlaced < 3)
                            {
                                SpawnUnit(n, "larvae", p, 1);
                                PlaceToken(n, TokenType.Egg, p);
                                eggsPlaced++;
                            }
                        }
                        break;

                    case FactionId.GlacierKingdom:
                        // 1 Glacier Tile, 3 Penguins, 1 Igloo
                        PlaceOverlayTile(homeCoord, FactionOverlayTileType.Glacier, p.PlayerIndex);
                        PlaceBuilding(homeCoord, BuildingType.Igloo, p);
                        SpawnUnit(homeCoord, "penguin", p, 3);
                        break;

                    case FactionId.TroubleMakingTanukis:
                        // 3 Tanukis + 1 Shrine on home forest tile; 12 Coin Tokens spaced around forest tiles (>= 2 tiles apart)
                        PlaceBuilding(homeCoord, BuildingType.Shrine, p);
                        SpawnUnit(homeCoord, "tanuki", p, 3);
                        var coinCoords = new List<HexCoord>();
                        foreach (var tile in Board.Values)
                        {
                            if (coinCoords.Count >= 12) break;
                            if (tile.EffectiveTerrain == TileTerrainType.Forest && !tile.Coord.Equals(homeCoord))
                            {
                                if (coinCoords.All(c => HexCoord.Distance(c, tile.Coord) >= 2))
                                {
                                    PlaceToken(tile.Coord, TokenType.Coin, p);
                                    coinCoords.Add(tile.Coord);
                                }
                            }
                        }
                        break;

                    case FactionId.CapybaraCaravan:
                        // 3 Capybara units, Capybara Trader, 1 Wallow on a River Tile
                        homeTile.Overlay = FactionOverlayTileType.CapybaraRiver;
                        homeTile.OverlayOwnerPlayerIndex = p.PlayerIndex;
                        PlaceBuilding(homeCoord, BuildingType.Wallow, p);
                        SpawnUnit(homeCoord, "capybara", p, 3);
                        SpawnUnit(homeCoord, "capybara_trader", p, 1);
                        break;

                    case FactionId.BriaredFlower:
                        // Blooming Briar with 6 Thorns, 2 Thorned Seeds, 1 Prickly Thicket
                        p.BriaredFlowerThorns = 6;
                        PlaceBuilding(homeCoord, BuildingType.PricklyThicket, p);
                        SpawnUnit(homeCoord, "blooming_briar", p, 1);
                        SpawnUnit(homeCoord, "thorned_seed", p, 2);
                        break;
                }

                Log($"{p.DisplayName} completed {p.Faction.Name} setup at hex {homeCoord}.");
            }
        }

        public void BeginTurn(int playerIndex)
        {
            if (WinnerPlayerIndex >= 0) return;

            CurrentTurnPlayerIndex = playerIndex;
            var p = Players[CurrentTurnPlayerIndex];

            // Reset per-turn flags
            UsedUnearthThisTurn = false;
            UsedFactionCraftThisTurn = false;
            UsedAssembleThisTurn = false;
            UsedDisassembleThisTurn = false;
            MainPhaseActionsRemaining = 3;
            p.CurlyTailWonBattleThisTurn = false;
            p.ThistleBarrageUsesThisTurn = 0;

            foreach (var tile in Board.Values)
            {
                tile.LootedThisTurn = false;
                foreach (var u in tile.Units)
                {
                    if (u.OwnerPlayerIndex == playerIndex)
                    {
                        u.CannotMoveThisTurn = false;
                        u.HasMovedThisTurn = false;
                        u.IsFrozen = false; // Frozen clears on your next draw phase (Line 1228)
                    }
                }
            }

            // 1st Phase: Draw Phase (Rulebook Part 5 Line 192)
            CurrentPhase = TurnPhase.DrawPhase;

            // Platypus Papacy Prophetic Vision: Look at top 3 Relic cards, add best 1 to hand, put 2 on bottom
            if (p.Faction.Id == FactionId.PlatypusPapacy && RelicDeck.Count >= 3)
            {
                var c1 = DrawRelicCard();
                var c2 = DrawRelicCard();
                var c3 = DrawRelicCard();
                p.RelicHand.Add(c1);
                RelicDeck.Insert(0, c2);
                RelicDeck.Insert(0, c3);
                Log($"{p.DisplayName} used Prophetic Vision (inspected top 3 Relics, kept '{c1.Name}', placed 2 on bottom of deck).");
            }

            var drawn = DrawRelicCard();
            if (drawn != null)
            {
                p.RelicHand.Add(drawn);
                Log($"{p.DisplayName} began Turn {TurnNumber} (1st Draw Phase) and drew '{drawn.Name}'.");
            }

            // Faction Draw Phase triggers
            if (p.Faction.Id == FactionId.GoatCabal)
            {
                // Royal Decree detonation check
                foreach (var tile in Board.Values.Where(t => t.MarkedByRoyalDecree))
                {
                    tile.MarkedByRoyalDecree = false;
                    if (Board.Values.SelectMany(t => t.Units).Any(u => u.Definition.Id == "serpentine_sovereign"))
                    {
                        tile.Units.Clear();
                        tile.Tokens.Clear();
                        tile.Building = null;
                        tile.Overlay = FactionOverlayTileType.None;
                        Log($"Serpentine Sovereign's ROYAL DECREE obliterated all pieces and overlays at {tile.Coord}!");
                    }
                }
                // Secrets of the Ancients: Mill top card of 14-card Grimoire Deck
                MillAndResolveGrimoireCard(p);
            }
            else if (p.Faction.Id == FactionId.GlacierKingdom)
            {
                p.GlacierBlizzardActive = false;
                // Ice Age: Place 1 Glacier Tile adjacent to an existing Glacier Tile (+1 if Emperor Penguin on board)
                bool hasEmperor = Board.Values.SelectMany(t => t.Units).Any(u => u.Definition.Id == "emperor_penguin");
                int iceCount = hasEmperor ? 2 : 1;
                for (int k = 0; k < iceCount; k++)
                {
                    var existingGlacier = Board.Values.FirstOrDefault(t => t.Overlay == FactionOverlayTileType.Glacier &&
                        t.Coord.GetNeighbors().Any(n => Board.ContainsKey(n) && Board[n].Overlay == FactionOverlayTileType.None));
                    if (existingGlacier != null)
                    {
                        var target = existingGlacier.Coord.GetNeighbors()
                            .First(n => Board.ContainsKey(n) && Board[n].Overlay == FactionOverlayTileType.None);
                        PlaceOverlayTile(target, FactionOverlayTileType.Glacier, p.PlayerIndex);
                        Log($"{p.DisplayName} triggered Ice Age -> Glacier spread to {target}.");
                    }
                }
            }
            else if (p.Faction.Id == FactionId.CarnivorousQueen)
            {
                // Swarm: Hatch Egg Tokens into Larvae
                int hatched = 0;
                foreach (var tile in Board.Values)
                {
                    int removed = tile.Tokens.RemoveAll(t => t.Type == TokenType.Egg && t.OwnerPlayerIndex == p.PlayerIndex);
                    if (removed > 0)
                    {
                        SpawnUnit(tile.Coord, "larvae", p, removed);
                        hatched += removed;
                    }
                }
                if (hatched > 0)
                    Log($"{p.DisplayName} triggered Swarm -> Hatched {hatched} Egg Token(s) into Larvae.");
            }
            else if (p.Faction.Id == FactionId.TroubleMakingTanukis)
            {
                // Rustling in the Trees: Place 1 Fallen Leaves tile on or adjacent to a Shrine without enemies
                var shrineTile = Board.Values.FirstOrDefault(t => t.Building != null && t.Building.Type == BuildingType.Shrine && t.Building.OwnerPlayerIndex == p.PlayerIndex);
                if (shrineTile != null)
                {
                    var leafTarget = shrineTile.Coord.GetNeighbors()
                        .FirstOrDefault(n => Board.ContainsKey(n) && Board[n].EffectiveTerrain == TileTerrainType.Forest &&
                                             Board[n].Overlay == FactionOverlayTileType.None &&
                                             !Board[n].Units.Any(u => u.OwnerPlayerIndex != p.PlayerIndex));
                    if (Board.ContainsKey(leafTarget))
                    {
                        PlaceOverlayTile(leafTarget, FactionOverlayTileType.FallenLeaf_Unflipped, p.PlayerIndex);
                        Log($"{p.DisplayName} triggered Rustling in the Trees -> Placed Fallen Leaves Tile at {leafTarget}.");
                    }
                }
            }

            // Transition into 2nd Excavation Phase so player can Unearth/Craft/Assemble/Disassemble
            CurrentPhase = TurnPhase.ExcavationPhase;
            OnStateChanged?.Invoke();
        }

        public void AdvancePhase()
        {
            if (CurrentPhase == TurnPhase.GameOver) return;

            var p = Players[CurrentTurnPlayerIndex];
            if (CurrentPhase == TurnPhase.DrawPhase)
            {
                CurrentPhase = TurnPhase.ExcavationPhase;
                Log($"{p.DisplayName} entered 2nd Excavation Phase.");
            }
            else if (CurrentPhase == TurnPhase.ExcavationPhase)
            {
                CurrentPhase = TurnPhase.MainPhase;
                MainPhaseActionsRemaining = 3;

                // Horde of the Millions: Prolific Creatures trigger at start of Main Phase
                foreach (var millionsPlayer in Players.Where(pl => pl.Faction.Id == FactionId.HordeOfTheMillions))
                {
                    foreach (var tile in Board.Values)
                    {
                        int mCount = tile.Units.Count(u => u.Faction == FactionId.HordeOfTheMillions);
                        bool hasEnemy = tile.Units.Any(u => u.Faction != FactionId.HordeOfTheMillions) ||
                                        (tile.Building != null && tile.Building.Faction != FactionId.HordeOfTheMillions);
                        if (mCount >= 1 && mCount < 3 && hasEnemy)
                        {
                            SpawnUnit(tile.Coord, "millions", millionsPlayer, 1);
                            Log($"Prolific Creatures spawned 1 Millions at {tile.Coord}.");
                        }
                    }
                }

                Log($"{p.DisplayName} entered 3rd Main Phase (3 Actions remaining).");
            }
            else if (CurrentPhase == TurnPhase.MainPhase)
            {
                ExecuteEndPhase();
            }

            OnStateChanged?.Invoke();
        }

        private void ExecuteEndPhase()
        {
            CurrentPhase = TurnPhase.EndPhase;
            var p = Players[CurrentTurnPlayerIndex];
            Log($"{p.DisplayName} entered 4th End Phase.");

            // Horde of the Millions Forage Trait: During EVERY player's end phase, if 1+ of their units shares a tile with a Millions unit, draw 1 Forage card
            bool sharesTileWithMillions = Board.Values.Any(t =>
                t.Units.Any(u => u.OwnerPlayerIndex == p.PlayerIndex) &&
                t.Units.Any(u => u.Faction == FactionId.HordeOfTheMillions));
            if (sharesTileWithMillions)
            {
                DrawAndResolveForageCard(p);
            }

            // Capybara Caravan Guest bonus: Any player controlling a Guest unit draws 1 Resource card during their End Phase (Line 1353)
            bool controlsGuest = Board.Values.SelectMany(t => t.Units).Any(u => u.OwnerPlayerIndex == p.PlayerIndex && u.IsGuest);
            if (controlsGuest)
            {
                var guestRes = DrawResourceCard();
                p.ResourcePile.Add(guestRes);
                Log($"{p.DisplayName} controls a Guest at a Capybara Wallow -> Drew 1 bonus Resource ({guestRes}).");
            }

            // Faction End-Phase Scoring & Upkeep
            switch (p.Faction.Id)
            {
                case FactionId.Necromouser:
                    // Undead Shackles: +1 Authority per 2 Dead in Cauldron (cap 2)
                    int shackleAuth = Mathf.Min(2, p.NecromouserCauldronDeadCount / 2);
                    if (shackleAuth > 0) AwardAuthority(p.PlayerIndex, shackleAuth, "Undead Shackles");
                    break;

                case FactionId.HordeOfTheMillions:
                    // Burrow Effect: +1 Authority per 2 Burrows
                    int burrows = Board.Values.Count(t => t.Building != null && t.Building.Type == BuildingType.Burrow && t.Building.OwnerPlayerIndex == p.PlayerIndex);
                    if (burrows >= 2) AwardAuthority(p.PlayerIndex, burrows / 2, "Burrows");
                    // Out of Hibernation: Recruit 1 Millions at each Burrow with < 3 Millions
                    foreach (var tile in Board.Values.Where(t => t.Building != null && t.Building.Type == BuildingType.Burrow && t.Building.OwnerPlayerIndex == p.PlayerIndex))
                    {
                        if (tile.Units.Count(u => u.OwnerPlayerIndex == p.PlayerIndex) < 3)
                            SpawnUnit(tile.Coord, "millions", p, 1);
                    }
                    // On the Search for Food: +1 Authority per 3 Millions on enemy tiles (cap 2)
                    int foodTiles = Board.Values.Count(t =>
                        t.Units.Count(u => u.OwnerPlayerIndex == p.PlayerIndex) >= 3 &&
                        (t.Units.Any(u => u.OwnerPlayerIndex != p.PlayerIndex) || (t.Building != null && t.Building.OwnerPlayerIndex != p.PlayerIndex)));
                    if (foodTiles > 0) AwardAuthority(p.PlayerIndex, Mathf.Min(2, foodTiles), "On the Search for Food");
                    break;

                case FactionId.CurlyTail:
                    // End of Main/End phase meter progression (Part 10 Line 260)
                    p.CurlyTailThirstForGlory = Mathf.Min(4, p.CurlyTailThirstForGlory + 1);
                    if (p.CurlyTailHunger == 0)
                        p.ResourcePile.Add(DrawResourceCard());
                    else if (p.ResourcePile.Count >= 1)
                        ReturnResourceToDeck(p.ResourcePile, 0);
                    p.CurlyTailHunger = Mathf.Min(4, p.CurlyTailHunger + 1);
                    if (p.CurlyTailMomentum >= 2 && p.CurlyTailWonBattleThisTurn)
                        AwardAuthority(p.PlayerIndex, 1, "Momentum Level 2+");
                    break;

                case FactionId.FirestartingSquirrels:
                    ResolveSquirrelsFireTilesEndPhase(p);
                    // Forest Fire: +1 Authority per Fire Tile adjacent to enemy pieces (cap 2)
                    int fireScore = 0;
                    foreach (var ft in Board.Values.Where(t => t.Overlay == FactionOverlayTileType.Fire))
                    {
                        bool adjEnemy = ft.Coord.GetNeighbors().Any(n => Board.ContainsKey(n) && Board[n].Units.Any(u => u.OwnerPlayerIndex != p.PlayerIndex));
                        if (adjEnemy) fireScore++;
                    }
                    if (fireScore > 0) AwardAuthority(p.PlayerIndex, Mathf.Min(2, fireScore), "Forest Fire");
                    break;

                case FactionId.PlatypusPapacy:
                    int holyRelics = Board.Values.Sum(t => t.Tokens.Count(tk => tk.Type == TokenType.HolyRelic && tk.OwnerPlayerIndex == p.PlayerIndex));
                    if (!p.PlatyPopeKilled)
                        p.PlatypusFaith = Mathf.Min(5, p.PlatypusFaith + holyRelics);
                    if (p.CrusadeTurnsRemaining > 0) p.CrusadeTurnsRemaining--;
                    break;

                case FactionId.CarnivorousQueen:
                    bool queenHidden = Board.Values.SelectMany(t => t.Units).Any(u => u.OwnerPlayerIndex == p.PlayerIndex && u.Definition.Id == "carnivorous_queen" && u.IsHidden);
                    if (queenHidden) AwardAuthority(p.PlayerIndex, 1, "Watching From The Dark");
                    break;

                case FactionId.GlacierKingdom:
                    bool anyFrozen = Board.Values.SelectMany(t => t.Units).Any(u => u.OwnerPlayerIndex != p.PlayerIndex && u.IsFrozen);
                    if (anyFrozen) AwardAuthority(p.PlayerIndex, 1, "Cold Shoulder");
                    break;

                case FactionId.CapybaraCaravan:
                    int guestOwners = Players.Count(pl => pl.PlayerIndex != p.PlayerIndex && Board.Values.SelectMany(t => t.Units).Any(u => u.OwnerPlayerIndex == pl.PlayerIndex && u.IsGuest));
                    if (guestOwners > 0) AwardAuthority(p.PlayerIndex, Mathf.Min(2, guestOwners), "Customary Greeting");
                    RefillTradedGoodsStall();
                    break;

                case FactionId.BriaredFlower:
                    // Pollination: 4+ tiles with Thorned Seeds -> +1 Auth (7+ tiles -> +2 Auth)
                    int seedTiles = Board.Values.Count(t => t.Units.Any(u => u.Definition.Id == "thorned_seed"));
                    if (seedTiles >= 7) AwardAuthority(p.PlayerIndex, 2, "Pollination (7+ tiles)");
                    else if (seedTiles >= 4) AwardAuthority(p.PlayerIndex, 1, "Pollination (4+ tiles)");
                    break;
            }

            if (CurrentPhase == TurnPhase.GameOver) return;

            // Rulebook Part 5 (4th End Phase): Discard hand and resource pile down to 5 cards
            // (Necromouser Hoarder keeps 8 resources; Goat Cabal Kleptomancy keeps up to 10 Gold + 5 other)
            int maxResources = p.Faction.Id == FactionId.Necromouser ? 8 :
                               (p.Faction.Id == FactionId.GoatCabal ? 10 : 5);
            while (p.ResourcePile.Count > maxResources)
            {
                ReturnResourceToDeck(p.ResourcePile, p.ResourcePile.Count - 1);
            }
            while (p.RelicHand.Count > 5)
            {
                var discarded = p.RelicHand[p.RelicHand.Count - 1];
                p.RelicHand.RemoveAt(p.RelicHand.Count - 1);
                RelicDiscardPile.Add(discarded);
            }

            // Refill Excavation Area back up to 5 cards
            RefillExcavationArea();

            // Advance to next player clockwise
            int nextPlayer = (CurrentTurnPlayerIndex + 1) % Players.Count;
            if (nextPlayer == 0) TurnNumber++;
            BeginTurn(nextPlayer);
        }

        private void ResolveSquirrelsFireTilesEndPhase(PlayerState squirrelPlayer)
        {
            // Rulebook Lines 841-855: 5-step Fire Tile End-Phase Resolution
            int totalEnemyDamageDealt = 0;
            int totalEnemiesDrivenOut = 0;
            var fireTiles = Board.Values.Where(t => t.Overlay == FactionOverlayTileType.Fire).ToList();

            foreach (var ft in fireTiles)
            {
                bool hasBunker = ft.Building != null && ft.Building.Type == BuildingType.Bunker;
                // 1. Clear Squirrels (unless protected by Bunker)
                if (!hasBunker)
                {
                    var squirrelsHere = ft.Units.Where(u => u.Definition.Id == "squirrel").ToList();
                    foreach (var sq in squirrelsHere)
                    {
                        ft.Units.Remove(sq);
                        squirrelPlayer.ResourcePile.Add(ResourceType.Charcoal);
                    }
                }

                // 2. Burn structures
                if (ft.Building != null && ft.Building.Type != BuildingType.Bunker)
                    ft.Building = null;

                // 3. Drive out enemies
                var enemies = ft.Units.Where(u => u.OwnerPlayerIndex != squirrelPlayer.PlayerIndex).ToList();
                foreach (var enemy in enemies)
                {
                    var adjCoord = ft.Coord.GetNeighbors().FirstOrDefault(n => Board.ContainsKey(n));
                    if (Board.ContainsKey(adjCoord))
                    {
                        ft.Units.Remove(enemy);
                        var destTile = Board[adjCoord];
                        destTile.Units.Add(enemy);
                        totalEnemiesDrivenOut++;

                        if (destTile.EffectiveTerrain == TileTerrainType.Forest)
                        {
                            int dmg = ft.Tokens.Any(tk => tk.Type == TokenType.TarPit) ? 2 : 1;
                            enemy.CurrentHP -= dmg;
                            totalEnemyDamageDealt += dmg;
                            if (enemy.CurrentHP <= 0)
                                RemoveUnitFromBoard(destTile, enemy, squirrelPlayer.PlayerIndex);
                        }
                    }
                }

                // 5. Burn the Deck
                if (ResourceDeck.Count > 0)
                {
                    var burned = DrawResourceCard();
                    BurnedResources.Add(burned);
                    if (burned == ResourceType.Gold)
                        AwardAuthority(squirrelPlayer.PlayerIndex, 1, "Burned Gold from Deck");
                }
            }

            // Flames of War scoring: +1 Auth per 2HP dealt, +1 Auth per 2 enemies moved (cap 4)
            int flamesAuth = Mathf.Min(4, (totalEnemyDamageDealt / 2) + (totalEnemiesDrivenOut / 2));
            if (flamesAuth > 0)
                AwardAuthority(squirrelPlayer.PlayerIndex, flamesAuth, "Flames of War");
        }

        // =====================================================================
        // FACTION SUB-DECK RESOLVERS (Forage, Grimoire, Traded Goods)
        // =====================================================================

        public void DrawAndResolveForageCard(PlayerState drawingPlayer)
        {
            if (ForageDeck.Count == 0)
            {
                ForageDeck = RulebookDatabase.BuildMillionsForageDeck8();
                Shuffle(ForageDeck);
                ForageDiscard.Clear();
            }
            var card = ForageDeck[ForageDeck.Count - 1];
            ForageDeck.RemoveAt(ForageDeck.Count - 1);
            ForageDiscard.Add(card);

            var millionsPlayer = Players.FirstOrDefault(pl => pl.Faction.Id == FactionId.HordeOfTheMillions);
            Log($"{drawingPlayer.DisplayName} drew Forage Card '{card.Title}': {card.EffectText}");

            switch (card.Id)
            {
                case "plague_authority":
                    drawingPlayer.Authority = Mathf.Max(0, drawingPlayer.Authority - 1);
                    if (millionsPlayer != null) AwardAuthority(millionsPlayer.PlayerIndex, 1, "Forage Plague");
                    break;
                case "plague_infest":
                    if (drawingPlayer.ResourcePile.Count > 0)
                        ReturnResourceToDeck(drawingPlayer.ResourcePile, 0);
                    if (millionsPlayer != null)
                    {
                        var tile = Board.Values.FirstOrDefault(t => t.Units.Any(u => u.OwnerPlayerIndex == drawingPlayer.PlayerIndex));
                        if (tile != null) SpawnUnit(tile.Coord, "millions", millionsPlayer, 1);
                    }
                    break;
                case "plague_sickness":
                    var sharedTile = Board.Values.FirstOrDefault(t =>
                        t.Units.Any(u => u.OwnerPlayerIndex == drawingPlayer.PlayerIndex) &&
                        t.Units.Any(u => u.Faction == FactionId.HordeOfTheMillions));
                    if (sharedTile != null)
                    {
                        var victim = sharedTile.Units.First(u => u.OwnerPlayerIndex == drawingPlayer.PlayerIndex);
                        victim.CurrentHP -= 1;
                        if (victim.CurrentHP <= 0)
                            RemoveUnitFromBoard(sharedTile, victim, millionsPlayer != null ? millionsPlayer.PlayerIndex : -1);
                    }
                    break;
                case "rummage_stone_1":
                case "rummage_stone_2":
                    if (millionsPlayer != null)
                    {
                        millionsPlayer.ResourcePile.Add(ResourceType.Stone);
                        millionsPlayer.ResourcePile.Add(ResourceType.Stone);
                    }
                    break;
                case "rummage_wood_1":
                case "rummage_wood_2":
                    if (millionsPlayer != null)
                    {
                        millionsPlayer.ResourcePile.Add(ResourceType.Wood);
                        millionsPlayer.ResourcePile.Add(ResourceType.Wood);
                    }
                    break;
                case "rummage_gold":
                    if (millionsPlayer != null)
                        millionsPlayer.ResourcePile.Add(ResourceType.Gold);
                    break;
            }
        }

        public void MillAndResolveGrimoireCard(PlayerState goatPlayer)
        {
            if (GrimoireDeck.Count == 0)
            {
                GrimoireDeck = RulebookDatabase.BuildGoatGrimoireDeck14();
                Shuffle(GrimoireDeck);
                GrimoireDiscard.Clear();
            }
            var card = GrimoireDeck[GrimoireDeck.Count - 1];
            GrimoireDeck.RemoveAt(GrimoireDeck.Count - 1);
            GrimoireDiscard.Add(card);

            Log($"{goatPlayer.DisplayName} milled Grimoire '{card.Title}': {card.EffectText}");
            switch (card.Id)
            {
                case "grim_1": goatPlayer.GoatPurgatoryCount++; break;
                case "grim_2":
                    if (goatPlayer.GoatPurgatoryCount >= 1) { goatPlayer.GoatPurgatoryCount--; AwardAuthority(goatPlayer.PlayerIndex, 1, "Grimoire: Soul Tithe"); }
                    break;
                case "grim_3":
                    if (goatPlayer.Authority >= 2) { goatPlayer.Authority -= 2; goatPlayer.GoatPurgatoryCount += 2; }
                    break;
                case "grim_4":
                    if (goatPlayer.RelicHand.Count >= 1) { RelicDiscardPile.Add(goatPlayer.RelicHand[0]); goatPlayer.RelicHand.RemoveAt(0); AwardAuthority(goatPlayer.PlayerIndex, 2, "Grimoire: Burnt Offering"); }
                    break;
                case "grim_5":
                    if (goatPlayer.Authority >= 2) { goatPlayer.Authority -= 2; var r = DrawRelicCard(); if (r != null) goatPlayer.RelicHand.Add(r); }
                    break;
                case "grim_6":
                    if (goatPlayer.ResourcePile.Count >= 2) { ReturnResourceToDeck(goatPlayer.ResourcePile, 0); ReturnResourceToDeck(goatPlayer.ResourcePile, 0); goatPlayer.GoatPurgatoryCount += 2; }
                    break;
                case "grim_7":
                    goatPlayer.ResourcePile.Add(ResourceType.Gold);
                    goatPlayer.ResourcePile.Add(ResourceType.Gold);
                    break;
                case "grim_8":
                    if (goatPlayer.ResourcePile.Contains(ResourceType.Gold)) { RemoveResources(goatPlayer, ResourceType.Gold, 1); AwardAuthority(goatPlayer.PlayerIndex, 2, "Grimoire: Golden Covenant"); }
                    break;
                case "grim_9":
                    var d1 = DrawRelicCard();
                    var d2 = DrawRelicCard();
                    if (d1 != null) goatPlayer.RelicHand.Add(d1);
                    if (d2 != null) RelicDiscardPile.Add(d2);
                    break;
                case "grim_10":
                    goatPlayer.ResourcePile.Add(ResourceType.Gold);
                    break;
                case "grim_11":
                    if (goatPlayer.Authority >= 2) { goatPlayer.Authority -= 2; goatPlayer.ResourcePile.Add(ResourceType.Gold); }
                    break;
                case "grim_12":
                    AwardAuthority(goatPlayer.PlayerIndex, 3, "Grimoire: Abyssal Dominion");
                    break;
                case "grim_13":
                    goatPlayer.ResourcePile.Add(ResourceType.Gold);
                    goatPlayer.GoatPurgatoryCount++;
                    AwardAuthority(goatPlayer.PlayerIndex, 1, "Grimoire: Unholy Trinity");
                    break;
                case "grim_14":
                    if (goatPlayer.Authority >= 1 && goatPlayer.GoatPurgatoryCount >= 1)
                    {
                        goatPlayer.Authority--;
                        goatPlayer.GoatPurgatoryCount--;
                        goatPlayer.ResourcePile.Add(ResourceType.Gold);
                        goatPlayer.ResourcePile.Add(ResourceType.Gold);
                        goatPlayer.ResourcePile.Add(ResourceType.Gold);
                    }
                    break;
            }
        }

        public void RefillTradedGoodsStall()
        {
            while (TradedGoodsStall.Count < 3)
            {
                if (TradedGoodsDeck.Count == 0)
                {
                    if (TradedGoodsDiscard.Count == 0) break;
                    TradedGoodsDeck.AddRange(TradedGoodsDiscard);
                    TradedGoodsDiscard.Clear();
                    Shuffle(TradedGoodsDeck);
                }
                var top = TradedGoodsDeck[TradedGoodsDeck.Count - 1];
                TradedGoodsDeck.RemoveAt(TradedGoodsDeck.Count - 1);
                TradedGoodsStall.Add(top);
            }
        }

        public bool TryBuyTradedGoodFromCapybaraStall(int stallIndex)
        {
            if (CurrentPhase != TurnPhase.MainPhase) return false;
            if (stallIndex < 0 || stallIndex >= TradedGoodsStall.Count) return false;

            var buyer = Players[CurrentTurnPlayerIndex];
            var capyPlayer = Players.FirstOrDefault(pl => pl.Faction.Id == FactionId.CapybaraCaravan);
            if (capyPlayer == null || capyPlayer.PlayerIndex == buyer.PlayerIndex) return false;

            var card = TradedGoodsStall[stallIndex];
            if (!CanAfford(buyer, card.Cost)) return false;

            // Transfer resource cost to Capybara player
            for (int i = 0; i < card.Cost.AnyResource && buyer.ResourcePile.Count > 0; i++)
            {
                capyPlayer.ResourcePile.Add(buyer.ResourcePile[0]);
                buyer.ResourcePile.RemoveAt(0);
            }

            TradedGoodsStall.RemoveAt(stallIndex);
            buyer.PurchasedTradedGoods.Add(card);

            // Both buyer and Capybara player gain 1 Authority, and buyer places 1 basic unit as a Guest at a Wallow (Line 1381)
            AwardAuthority(buyer.PlayerIndex, 1, $"Bought Traded Good '{card.Title}'");
            AwardAuthority(capyPlayer.PlayerIndex, 1, $"Sold Traded Good '{card.Title}'");

            var wallowTile = Board.Values.FirstOrDefault(t => t.Building != null && t.Building.Type == BuildingType.Wallow);
            var basicUnit = buyer.Faction.Units.FirstOrDefault(u => !u.IsSpecialUnit);
            if (wallowTile != null && basicUnit != null)
            {
                var spawned = SpawnUnit(wallowTile.Coord, basicUnit.Id, buyer, 1);
                foreach (var u in spawned) u.IsGuest = true;
            }

            OnStateChanged?.Invoke();
            return true;
        }

        public bool TryTradeResourceWithPlayer(int targetPlayerIndex, ResourceType giveType, ResourceType receiveType)
        {
            var p = Players[CurrentTurnPlayerIndex];
            if (targetPlayerIndex < 0 || targetPlayerIndex >= Players.Count || targetPlayerIndex == p.PlayerIndex) return false;
            var partner = Players[targetPlayerIndex];

            // Briared Flower trait Instinctual Hatred: cannot trade with other players (Line 1445)
            if (p.Faction.Id == FactionId.BriaredFlower || partner.Faction.Id == FactionId.BriaredFlower)
            {
                Log("Instinctual Hatred: The Briared Flower cannot trade with other players!");
                return false;
            }

            if (!p.ResourcePile.Contains(giveType) || !partner.ResourcePile.Contains(receiveType))
                return false;

            p.ResourcePile.Remove(giveType);
            partner.ResourcePile.Remove(receiveType);
            p.ResourcePile.Add(receiveType);
            partner.ResourcePile.Add(giveType);

            Log($"{p.DisplayName} traded 1 {giveType} with {partner.DisplayName} for 1 {receiveType}.");
            OnStateChanged?.Invoke();
            return true;
        }

        // =====================================================================
        // 2nd Phase: EXCAVATION PHASE ACTIONS (Unearth, Faction Craft, Assemble, Disassemble)
        // =====================================================================

        public bool CanAfford(PlayerState p, ResourceCost cost)
        {
            int w = p.ResourcePile.Count(r => r == ResourceType.Wood);
            int s = p.ResourcePile.Count(r => r == ResourceType.Stone);
            int g = p.ResourcePile.Count(r => r == ResourceType.Gold);
            int c = p.ResourcePile.Count(r => r == ResourceType.Charcoal);

            if (w < cost.Wood || s < cost.Stone || g < cost.Gold || c < cost.Charcoal)
                return false;

            int remaining = (w - cost.Wood) + (s - cost.Stone) + (g - cost.Gold) + (c - cost.Charcoal);
            return remaining >= cost.AnyResource;
        }

        public void SpendResources(PlayerState p, ResourceCost cost)
        {
            RemoveResources(p, ResourceType.Wood, cost.Wood);
            RemoveResources(p, ResourceType.Stone, cost.Stone);
            RemoveResources(p, ResourceType.Gold, cost.Gold);
            RemoveResources(p, ResourceType.Charcoal, cost.Charcoal);
            for (int i = 0; i < cost.AnyResource && p.ResourcePile.Count > 0; i++)
            {
                ReturnResourceToDeck(p.ResourcePile, 0);
            }
        }

        private void RemoveResources(PlayerState p, ResourceType type, int count)
        {
            for (int i = 0; i < count; i++)
            {
                int idx = p.ResourcePile.IndexOf(type);
                if (idx >= 0)
                    ReturnResourceToDeck(p.ResourcePile, idx);
            }
        }

        public bool TryUnearthRelic(int excavationIndex)
        {
            if (CurrentPhase != TurnPhase.ExcavationPhase || UsedUnearthThisTurn) return false;
            if (excavationIndex < 0 || excavationIndex >= ExcavationArea.Count) return false;

            var p = Players[CurrentTurnPlayerIndex];
            var card = ExcavationArea[excavationIndex];
            if (!CanAfford(p, card.Cost)) return false;

            SpendResources(p, card.Cost);
            ExcavationArea.RemoveAt(excavationIndex);
            UsedUnearthThisTurn = true;

            ResolveCraftedRelic(p, card, "Unearthed");
            NotifyEnemyActionTriggerForCarnivorousQueen(p.PlayerIndex, 1);
            OnStateChanged?.Invoke();
            return true;
        }

        public bool TryAssembleRelicFromHand(int handIndex)
        {
            if (CurrentPhase != TurnPhase.ExcavationPhase || UsedAssembleThisTurn) return false;
            var p = Players[CurrentTurnPlayerIndex];
            // Carnivorous Queen trait Lack of Humanity: cannot use Assemble action (Line 1004)
            if (p.Faction.Id == FactionId.CarnivorousQueen) return false;
            if (handIndex < 0 || handIndex >= p.RelicHand.Count) return false;

            var card = p.RelicHand[handIndex];
            if (!card.HasCraftedEffect || !CanAfford(p, card.Cost)) return false;

            SpendResources(p, card.Cost);
            p.RelicHand.RemoveAt(handIndex);
            UsedAssembleThisTurn = true;

            ResolveCraftedRelic(p, card, "Assembled");
            NotifyEnemyActionTriggerForCarnivorousQueen(p.PlayerIndex, 1);
            OnStateChanged?.Invoke();
            return true;
        }

        public bool TryDisassembleRelicFromHand(int handIndex)
        {
            if (CurrentPhase != TurnPhase.ExcavationPhase || UsedDisassembleThisTurn) return false;
            var p = Players[CurrentTurnPlayerIndex];
            if (handIndex < 0 || handIndex >= p.RelicHand.Count) return false;

            var card = p.RelicHand[handIndex];
            p.RelicHand.RemoveAt(handIndex);
            RelicDiscardPile.Add(card);
            UsedDisassembleThisTurn = true;

            GrantResourceCostToPile(p, card.DisassembleGain);
            Log($"{p.DisplayName} Disassembled '{card.Name}' for {card.DisassembleGain}.");
            OnStateChanged?.Invoke();
            return true;
        }

        public bool TryFactionCraftBuilding(HexCoord targetCoord)
        {
            if (CurrentPhase != TurnPhase.ExcavationPhase || UsedFactionCraftThisTurn) return false;
            if (!Board.TryGetValue(targetCoord, out var tile)) return false;
            var p = Players[CurrentTurnPlayerIndex];

            if (p.Faction.PrimaryBuilding == BuildingType.None) return false;
            if (tile.Building != null) return false;
            if (tile.Overlay == FactionOverlayTileType.Glacier && p.Faction.Id != FactionId.GlacierKingdom)
                return false;
            if (tile.EffectiveTerrain == TileTerrainType.River && p.Faction.PrimaryBuilding != BuildingType.Wallow)
                return false;
            if (!CanAfford(p, p.Faction.PrimaryBuildingCost)) return false;

            SpendResources(p, p.Faction.PrimaryBuildingCost);
            PlaceBuilding(targetCoord, p.Faction.PrimaryBuilding, p);
            UsedFactionCraftThisTurn = true;
            NotifyEnemyActionTriggerForCarnivorousQueen(p.PlayerIndex, 1);
            Log($"{p.DisplayName} crafted {p.Faction.PrimaryBuilding} at {targetCoord}.");
            OnStateChanged?.Invoke();
            return true;
        }

        public bool TryCraftAndEquipFactionEquipment(string equipmentId)
        {
            var p = Players[CurrentTurnPlayerIndex];
            var eq = p.Faction.Equipment.FirstOrDefault(e => e.Id == equipmentId);
            if (eq == null || !CanAfford(p, eq.Cost)) return false;

            var targetUnit = Board.Values.SelectMany(t => t.Units)
                .FirstOrDefault(u => u.OwnerPlayerIndex == p.PlayerIndex && u.Definition.Id == eq.TargetUnitId &&
                                     u.EquippedItems.Count(x => x.Id == eq.Id) < eq.MaxPerUnit);
            if (targetUnit == null)
            {
                Log($"No eligible '{eq.TargetUnitId}' on the board to equip '{eq.Name}'.");
                return false;
            }

            bool spentGold = eq.Cost.Gold > 0;
            SpendResources(p, eq.Cost);
            targetUnit.EquippedItems.Add(eq);
            targetUnit.MaxHP += eq.BonusHP;
            targetUnit.CurrentHP += eq.BonusHP;

            if (eq.AuthorityOnCraft > 0)
                AwardAuthority(p.PlayerIndex, eq.AuthorityOnCraft, $"Forged {eq.Name}");

            // Necromouser Plague trait: spending Gold on equipment places 1 Miasma Tile adjacent to an existing Miasma Tile
            if (p.Faction.Id == FactionId.Necromouser && spentGold)
            {
                var existingMiasma = Board.Values.FirstOrDefault(t =>
                    (t.Overlay == FactionOverlayTileType.Miasma_Revealed || t.Overlay == FactionOverlayTileType.Miasma_Hidden) &&
                    t.Coord.GetNeighbors().Any(n => Board.ContainsKey(n) && Board[n].Overlay == FactionOverlayTileType.None));
                if (existingMiasma != null)
                {
                    var nCoord = existingMiasma.Coord.GetNeighbors().First(n => Board.ContainsKey(n) && Board[n].Overlay == FactionOverlayTileType.None);
                    PlaceOverlayTile(nCoord, FactionOverlayTileType.Miasma_Revealed, p.PlayerIndex);
                    Log($"{p.DisplayName} triggered Plague -> Spread Miasma to {nCoord}.");
                }
            }

            Log($"{p.DisplayName} crafted & equipped '{eq.Name}' onto {targetUnit.Definition.Name}!");
            OnStateChanged?.Invoke();
            return true;
        }

        private void ResolveCraftedRelic(PlayerState p, RelicCardDef card, string verb)
        {
            Log($"{p.DisplayName} {verb} '{card.Name}'.");
            if (card.KeepFaceUpWhenCrafted)
            {
                p.FaceUpRelics.Add(card);
                return;
            }

            switch (card.Id)
            {
                case "locked_chest":
                    AwardAuthority(p.PlayerIndex, 2, "Locked Chest");
                    break;
                case "damaged_anankian_chariot":
                    foreach (var tile in Board.Values.Where(t => t.Building != null && t.Building.OwnerPlayerIndex == p.PlayerIndex))
                    {
                        var basicUnit = p.Faction.Units.FirstOrDefault(u => !u.IsSpecialUnit);
                        if (basicUnit != null)
                        {
                            var spawned = SpawnUnit(tile.Coord, basicUnit.Id, p, 1);
                            foreach (var u in spawned) u.CannotMoveThisTurn = true;
                        }
                    }
                    break;
                case "bejeweled_chalice":
                    p.ResourcePile.Add(ResourceType.Gold);
                    var r = DrawRelicCard();
                    if (r != null) p.RelicHand.Add(r);
                    break;
                case "marble_aqueduct":
                    if (BurnedResources.Count >= 2)
                    {
                        p.ResourcePile.Add(BurnedResources[BurnedResources.Count - 1]);
                        BurnedResources.RemoveAt(BurnedResources.Count - 1);
                        p.ResourcePile.Add(BurnedResources[BurnedResources.Count - 1]);
                        BurnedResources.RemoveAt(BurnedResources.Count - 1);
                    }
                    else
                    {
                        p.ResourcePile.Add(DrawResourceCard());
                        p.ResourcePile.Add(DrawResourceCard());
                    }
                    break;
                case "looted_vault":
                    for (int i = 0; i < 2; i++)
                    {
                        var drawn = DrawRelicCard();
                        if (drawn != null) p.RelicHand.Add(drawn);
                    }
                    break;
                case "underground_passage":
                    var target = Players.Where(op => op.PlayerIndex != p.PlayerIndex && op.ResourcePile.Count > 0)
                                        .OrderByDescending(op => op.ResourcePile.Count)
                                        .FirstOrDefault();
                    if (target != null)
                    {
                        int stealCount = target.ResourcePile.Count >= 5 ? 2 : 1;
                        for (int i = 0; i < stealCount && target.ResourcePile.Count > 0; i++)
                        {
                            int idx = _rng.Next(target.ResourcePile.Count);
                            p.ResourcePile.Add(target.ResourcePile[idx]);
                            target.ResourcePile.RemoveAt(idx);
                        }
                        Log($"{p.DisplayName} stole {stealCount} resource(s) from {target.DisplayName} via Underground Passage.");
                    }
                    break;
                case "hallow_ground":
                    var enemyTile = Board.Values.FirstOrDefault(t => t.Units.Any(u => u.OwnerPlayerIndex != p.PlayerIndex && !u.Definition.IsSpecialUnit));
                    if (enemyTile != null)
                    {
                        var victim = enemyTile.Units.First(u => u.OwnerPlayerIndex != p.PlayerIndex && !u.Definition.IsSpecialUnit);
                        enemyTile.Units.Remove(victim);
                        Log($"{p.DisplayName} used Hallow Ground to return {victim.Definition.Name} at {enemyTile.Coord} to supply.");
                    }
                    break;
            }

            RelicDiscardPile.Add(card);
        }

        // =====================================================================
        // 3rd Phase: MAIN PHASE ACTIONS (Move, Battle, Loot, Recruit)
        // =====================================================================

        public bool TryMoveUnits(HexCoord fromCoord, HexCoord toCoord)
        {
            if (CurrentPhase != TurnPhase.MainPhase || MainPhaseActionsRemaining <= 0) return false;
            if (!Board.TryGetValue(fromCoord, out var fromTile) || !Board.TryGetValue(toCoord, out var toTile)) return false;

            var p = Players[CurrentTurnPlayerIndex];
            var movableUnits = fromTile.Units.Where(u =>
                u.OwnerPlayerIndex == p.PlayerIndex &&
                !u.CannotMoveThisTurn &&
                !u.IsFrozen &&
                u.Definition.Id != "platy_pope" && // Platy-Pope is Unyielding (cannot move)
                u.Definition.Id != "ensnared" &&
                !(u.Definition.Id == "blooming_briar" && u.HasMovedThisTurn) // Rooted in Place: 1 tile per turn
            ).ToList();

            if (movableUnits.Count == 0) return false;

            // Tar Pit Token check: costs 2 Main Phase actions for enemies to leave
            int actionCost = 1;
            if (fromTile.Tokens.Any(t => t.Type == TokenType.TarPit) && p.Faction.Id != FactionId.FirestartingSquirrels)
            {
                if (MainPhaseActionsRemaining < 2)
                {
                    Log("Tar Pit Token requires 2 Main Phase actions to move off this tile!");
                    return false;
                }
                actionCost = 2;
            }

            int dist = HexCoord.Distance(fromCoord, toCoord);
            int maxDist = 1;
            // Capybara Semiaquatic: 2 River Tiles
            if (p.Faction.Id == FactionId.CapybaraCaravan && fromTile.EffectiveTerrain == TileTerrainType.River && toTile.EffectiveTerrain == TileTerrainType.River)
                maxDist = 2;
            // Penguin Slip and Slide: 2 Glacier Tiles
            if (p.Faction.Id == FactionId.GlacierKingdom && fromTile.Overlay == FactionOverlayTileType.Glacier && toTile.Overlay == FactionOverlayTileType.Glacier)
                maxDist = 2;
            // Curly Tail Thirst for Glory Lv2+ or Supply Line connection
            if (p.Faction.Id == FactionId.CurlyTail)
            {
                if (p.CurlyTailThirstForGlory >= 2) maxDist = 2;
                if (fromTile.Tokens.Any(t => t.Type == TokenType.SupplyLine) && toTile.Tokens.Any(t => t.Type == TokenType.SupplyLine))
                    maxDist = 6;
            }
            // Platypus Crusader Lv3+: +1 tile movement
            if (p.Faction.Id == FactionId.PlatypusPapacy && p.PlatyPopeLevel >= 3 && movableUnits.Any(u => u.Definition.Id == "crusader"))
                maxDist = 2;
            // Leather Boots on The Brute
            if (movableUnits.Any(u => u.EquippedItems.Any(e => e.BonusMove > 0)))
                maxDist = 2;

            if (dist == 0 || dist > maxDist) return false;

            // Gluttony Token check (Horde of the Millions)
            if (fromTile.Tokens.Any(t => t.Type == TokenType.Gluttony) &&
                fromTile.Units.Any(u => u.Faction == FactionId.HordeOfTheMillions) &&
                p.Faction.Id != FactionId.HordeOfTheMillions)
            {
                Log("Cannot move off a tile with a Gluttony Token while a Millions unit is present!");
                return false;
            }

            // Goat Cabal Suspicion check
            if (toTile.Building != null && toTile.Building.Type == BuildingType.SummoningCircle && toTile.Building.OwnerPlayerIndex != p.PlayerIndex)
            {
                int goatsThere = toTile.Units.Count(u => u.Faction == FactionId.GoatCabal);
                if (movableUnits.Count <= goatsThere)
                {
                    Log($"Suspicion blocked movement: need more than {goatsThere} units to enter the Summoning Circle tile.");
                    return false;
                }
            }

            foreach (var u in movableUnits)
            {
                fromTile.Units.Remove(u);
                toTile.Units.Add(u);
                u.HasMovedThisTurn = true;
                u.IsGuest = (toTile.Building != null && toTile.Building.Type == BuildingType.Wallow && toTile.Building.OwnerPlayerIndex != p.PlayerIndex);
            }

            // Tanuki Coin Hunt trait: Carry Coin Token with moved Tanuki; score +2 Authority at a Shrine!
            if (p.Faction.Id == FactionId.TroubleMakingTanukis)
            {
                var coin = fromTile.Tokens.FirstOrDefault(t => t.Type == TokenType.Coin);
                if (coin != null && !toTile.Tokens.Any(t => t.Type == TokenType.Coin))
                {
                    fromTile.Tokens.Remove(coin);
                    if (toTile.Building != null && toTile.Building.Type == BuildingType.Shrine && toTile.Building.OwnerPlayerIndex == p.PlayerIndex)
                    {
                        AwardAuthority(p.PlayerIndex, 2, "Coin Delivered to Shrine");
                    }
                    else
                    {
                        toTile.Tokens.Add(coin);
                    }
                }
            }

            // Fallen Leaves Trap / Prank trigger when enemy steps onto an unflipped Fallen Leaves tile (Lines 1294-1297)
            if (toTile.Overlay == FactionOverlayTileType.FallenLeaf_Unflipped && p.Faction.Id != FactionId.TroubleMakingTanukis)
            {
                bool isTrap = _rng.Next(13) < 5; // 5 Trap, 8 Prank ratio
                if (isTrap)
                {
                    toTile.Overlay = FactionOverlayTileType.FallenLeaf_Trap;
                    foreach (var u in movableUnits) u.CannotMoveThisTurn = true;
                    Log($"TRAP LEAF triggered at {toCoord}! Moving units are immobilized!");
                }
                else
                {
                    toTile.Overlay = FactionOverlayTileType.FallenLeaf_Prank;
                    var tanukiOwner = Players.FirstOrDefault(pl => pl.Faction.Id == FactionId.TroubleMakingTanukis) ?? p;
                    PlaceToken(toCoord, TokenType.Coin, tanukiOwner);
                    Log($"PRANK LEAF triggered at {toCoord}! A Coin Token appeared!");
                }
            }

            // Platypus Papacy Martyrdom: When a Pilgrim moves onto a tile with enemy pieces, sacrifice for +1 Faith
            if (p.Faction.Id == FactionId.PlatypusPapacy)
            {
                bool hasEnemies = toTile.Units.Any(u => u.OwnerPlayerIndex != p.PlayerIndex) ||
                                  (toTile.Building != null && toTile.Building.OwnerPlayerIndex != p.PlayerIndex);
                if (hasEnemies)
                {
                    var pilgrims = toTile.Units.Where(u => u.Definition.Id == "pilgrim").ToList();
                    foreach (var pil in pilgrims)
                    {
                        toTile.Units.Remove(pil);
                        if (!p.PlatyPopeKilled)
                            p.PlatypusFaith = Mathf.Min(5, p.PlatypusFaith + 1);
                        Log($"Pilgrim martyred at {toCoord}! Faith increased to {p.PlatypusFaith}/5.");
                    }
                }
            }

            // Fire Tile in-movement damage (1HP to 1 moving enemy unit)
            if (toTile.Overlay == FactionOverlayTileType.Fire && p.Faction.Id != FactionId.FirestartingSquirrels)
            {
                var victim = movableUnits.First();
                victim.CurrentHP -= 1;
                Log($"{victim.Definition.Name} took 1HP damage entering Fire Tile at {toCoord}!");
                if (victim.CurrentHP <= 0)
                    RemoveUnitFromBoard(toTile, victim, -1);
            }

            MainPhaseActionsRemaining -= actionCost;
            Log($"{p.DisplayName} moved {movableUnits.Count} unit(s) from {fromCoord} to {toCoord}. ({MainPhaseActionsRemaining} actions left)");
            OnStateChanged?.Invoke();
            return true;
        }

        public bool TryLootTile(HexCoord coord)
        {
            if (CurrentPhase != TurnPhase.MainPhase || MainPhaseActionsRemaining <= 0) return false;
            if (!Board.TryGetValue(coord, out var tile) || tile.LootedThisTurn) return false;

            var p = Players[CurrentTurnPlayerIndex];
            bool hasMyUnits = tile.Units.Any(u => u.OwnerPlayerIndex == p.PlayerIndex);
            bool hasEnemyUnits = tile.Units.Any(u => u.OwnerPlayerIndex != p.PlayerIndex);
            if (!hasMyUnits || hasEnemyUnits) return false;

            // Check Rusty Shovel Traded Good card (draw 3 resources and end loot)
            var shovel = p.PurchasedTradedGoods.FirstOrDefault(tg => tg.Id == "tg_rusty_shovel");
            if (shovel != null)
            {
                p.PurchasedTradedGoods.Remove(shovel);
                TradedGoodsDiscard.Add(shovel);
                for (int i = 0; i < 3; i++) p.ResourcePile.Add(DrawResourceCard());
                tile.LootedThisTurn = true;
                MainPhaseActionsRemaining--;
                Log($"{p.DisplayName} used Rusty Shovel while looting {coord} -> Drew 3 Resource cards!");
                OnStateChanged?.Invoke();
                return true;
            }

            // Glacier Frozen Harbor: looting on Frozen Harbor tile yields 1 Gold
            if (tile.Building != null && tile.Building.Type == BuildingType.FrozenHarbor && p.Faction.Id == FactionId.GlacierKingdom)
            {
                p.ResourcePile.Add(ResourceType.Gold);
                Log($"{p.DisplayName} looted at Frozen Harbor {coord} -> Gained 1 Gold!");
            }
            else if (tile.Overlay == FactionOverlayTileType.Glacier)
            {
                if (p.Faction.Id != FactionId.GlacierKingdom) return false;
                int roll = RollD6();
                ResourceType fished = roll <= 3 ? ResourceType.Wood : ResourceType.Stone;
                p.ResourcePile.Add(fished);
                Log($"{p.DisplayName} used Ice Fishing at {coord} (rolled {roll}) -> Gained 1 {fished}.");
            }
            else if (tile.EffectiveTerrain == TileTerrainType.Forest)
            {
                var res = DrawResourceCard();
                p.ResourcePile.Add(res);
                Log($"{p.DisplayName} looted Forest Tile {coord} -> Gained 1 {res}.");
            }
            else if (tile.EffectiveTerrain == TileTerrainType.River)
            {
                int roll = RollD6();
                ResourceType riverLoot = (roll % 2 == 1) ? ResourceType.Stone : ResourceType.Gold;
                p.ResourcePile.Add(riverLoot);
                Log($"{p.DisplayName} looted River Tile {coord} (rolled {roll}) -> Gained 1 {riverLoot}.");
            }
            else if (tile.EffectiveTerrain == TileTerrainType.Ruin)
            {
                var relic = DrawRelicCard();
                if (relic != null)
                {
                    p.RelicHand.Add(relic);
                    Log($"{p.DisplayName} looted Ruin Tile {coord} -> Drew Relic '{relic.Name}'.");
                }
            }

            tile.LootedThisTurn = true;
            MainPhaseActionsRemaining--;
            OnStateChanged?.Invoke();
            return true;
        }

        public bool TryRecruit()
        {
            if (CurrentPhase != TurnPhase.MainPhase || MainPhaseActionsRemaining <= 0) return false;
            var p = Players[CurrentTurnPlayerIndex];
            if (p.Faction.Id == FactionId.Necromouser) return false; // Necromouser has no building and cannot recruit

            var myBuildingTiles = Board.Values.Where(t => t.Building != null && t.Building.OwnerPlayerIndex == p.PlayerIndex).ToList();
            if (myBuildingTiles.Count == 0) return false;

            var basicUnit = p.Faction.Units.FirstOrDefault(u => !u.IsSpecialUnit);
            if (basicUnit == null) return false;

            int totalSpawned = 0;
            foreach (var tile in myBuildingTiles)
            {
                if (p.Faction.Id == FactionId.PlatypusPapacy && p.PlatyPopeLevel >= 1)
                {
                    int crusaderCount = p.PlatyPopeLevel >= 3 ? 2 : 1;
                    SpawnUnit(tile.Coord, "crusader", p, crusaderCount);
                    totalSpawned += crusaderCount;
                }
                else
                {
                    SpawnUnit(tile.Coord, basicUnit.Id, p, 1);
                    totalSpawned++;
                }
            }

            MainPhaseActionsRemaining--;
            NotifyEnemyActionTriggerForCarnivorousQueen(p.PlayerIndex, 1);
            Log($"{p.DisplayName} used Recruit -> Spawned {totalSpawned} unit(s) across {myBuildingTiles.Count} building(s).");
            OnStateChanged?.Invoke();
            return true;
        }

        // =====================================================================
        // Part 11 & Part 12: 5-PHASE 2D6 BATTLE RESOLUTION & STRUCTURE DESTRUCTION
        // =====================================================================

        public BattleReport TryBattleOnTile(HexCoord coord, int targetDefenderPlayerIndex = -1)
        {
            if (CurrentPhase != TurnPhase.MainPhase || MainPhaseActionsRemaining <= 0) return null;
            if (!Board.TryGetValue(coord, out var tile)) return null;

            var attacker = Players[CurrentTurnPlayerIndex];
            var attackerUnits = tile.Units.Where(u => u.OwnerPlayerIndex == attacker.PlayerIndex && !u.IsHidden && u.Definition.Id != "ensnared").ToList();
            if (attackerUnits.Count == 0) return null;

            // Find defender faction on this tile
            if (targetDefenderPlayerIndex < 0)
            {
                var enemyUnit = tile.Units.FirstOrDefault(u => u.OwnerPlayerIndex != attacker.PlayerIndex && !u.IsHidden);
                if (enemyUnit != null)
                    targetDefenderPlayerIndex = enemyUnit.OwnerPlayerIndex;
                else if (tile.Building != null && tile.Building.OwnerPlayerIndex != attacker.PlayerIndex)
                    targetDefenderPlayerIndex = tile.Building.OwnerPlayerIndex;
                else if (tile.Tokens.Any(t => t.OwnerPlayerIndex != attacker.PlayerIndex && t.Type != TokenType.Coin))
                    targetDefenderPlayerIndex = tile.Tokens.First(t => t.OwnerPlayerIndex != attacker.PlayerIndex && t.Type != TokenType.Coin).OwnerPlayerIndex;
                else if ((tile.Overlay == FactionOverlayTileType.Miasma_Revealed || tile.Overlay == FactionOverlayTileType.Miasma_Hidden ||
                          tile.Overlay == FactionOverlayTileType.FallenLeaf_Trap || tile.Overlay == FactionOverlayTileType.FallenLeaf_Prank) &&
                         tile.OverlayOwnerPlayerIndex != attacker.PlayerIndex && tile.OverlayOwnerPlayerIndex >= 0)
                    targetDefenderPlayerIndex = tile.OverlayOwnerPlayerIndex;
            }

            if (targetDefenderPlayerIndex < 0 || targetDefenderPlayerIndex == attacker.PlayerIndex)
                return null;

            var defender = Players[targetDefenderPlayerIndex];
            var defenderUnits = tile.Units.Where(u => u.OwnerPlayerIndex == defender.PlayerIndex && !u.IsHidden).ToList();

            // Necromouser Cowardice: Necromouser cannot be part of a battle if friendly Skeletons or Special Units are on his tile (Line 402)
            if (defender.Faction.Id == FactionId.Necromouser && defenderUnits.Count > 1)
            {
                defenderUnits.RemoveAll(u => u.Definition.Id == "necromouser");
            }

            // Vanguard Devotion: If Necromouser is defending and Vanguard is on an adjacent tile, Vanguard + friendly units on its tile join defense (Line 413)
            if (defender.Faction.Id == FactionId.Necromouser)
            {
                foreach (var nCoord in coord.GetNeighbors())
                {
                    if (Board.TryGetValue(nCoord, out var adjTile) && adjTile.Units.Any(u => u.Definition.Id == "the_vanguard"))
                    {
                        defenderUnits.AddRange(adjTile.Units.Where(u => u.OwnerPlayerIndex == defender.PlayerIndex));
                        Log($"The Vanguard's Devotion triggered from {nCoord} to defend The Necromouser!");
                    }
                }
            }

            // Part 12: How to Destroy Tokens, Buildings, and Unguarded Overlay Tiles when no defending units are present
            bool hasChrysalis = tile.Building != null && tile.Building.Type == BuildingType.Chrysalis && tile.Building.OwnerPlayerIndex == defender.PlayerIndex;
            if (defenderUnits.Count == 0 && !hasChrysalis)
            {
                int canDestroy = attackerUnits.Count;
                int destroyedCount = 0;
                int bonusCrusadeAuth = (attacker.Faction.Id == FactionId.PlatypusPapacy && attacker.CrusadeTurnsRemaining > 0) ? 1 : 0;

                if (tile.Building != null && tile.Building.OwnerPlayerIndex == defender.PlayerIndex && canDestroy > 0)
                {
                    Log($"{attacker.DisplayName} destroyed {defender.DisplayName}'s {tile.Building.Type} at {coord}!");
                    tile.Building = null;
                    canDestroy--;
                    destroyedCount++;
                    AwardAuthority(attacker.PlayerIndex, 1 + bonusCrusadeAuth, "Destroyed Enemy Building");
                }
                while (canDestroy > 0 && tile.Tokens.Any(t => t.OwnerPlayerIndex == defender.PlayerIndex && t.Type != TokenType.Coin))
                {
                    var tk = tile.Tokens.First(t => t.OwnerPlayerIndex == defender.PlayerIndex && t.Type != TokenType.Coin);
                    tile.Tokens.Remove(tk);
                    canDestroy--;
                    destroyedCount++;
                    AwardAuthority(attacker.PlayerIndex, 1 + bonusCrusadeAuth, $"Destroyed {tk.Type} Token");
                }
                if (canDestroy > 0 && tile.OverlayOwnerPlayerIndex == defender.PlayerIndex &&
                    (tile.Overlay == FactionOverlayTileType.Miasma_Revealed || tile.Overlay == FactionOverlayTileType.Miasma_Hidden ||
                     tile.Overlay == FactionOverlayTileType.FallenLeaf_Trap || tile.Overlay == FactionOverlayTileType.FallenLeaf_Prank))
                {
                    var oldOverlay = tile.Overlay;
                    tile.Overlay = FactionOverlayTileType.None;
                    destroyedCount++;
                    AwardAuthority(attacker.PlayerIndex, 1, $"Destroyed {oldOverlay}");
                    if (defender.Faction.Id == FactionId.Necromouser)
                        defender.NecromouserCauldronDeadCount++;
                }

                if (destroyedCount > 0)
                {
                    MainPhaseActionsRemaining--;
                    OnStateChanged?.Invoke();
                }
                return null;
            }

            if (hasChrysalis)
                NotifyEnemyActionTriggerForCarnivorousQueen(attacker.PlayerIndex, 2);

            // Part 11: 5-Phase Unit Battle
            var report = new BattleReport
            {
                TileCoord = coord,
                AttackerPlayerIndex = attacker.PlayerIndex,
                DefenderPlayerIndex = defender.PlayerIndex
            };

            // War Horn check on Defender (end of Attack Declaration phase: recruit 1 basic unit before dice/defense)
            var warHorn = defender.FaceUpRelics.FirstOrDefault(r => r.Id == "war_horn");
            if (warHorn != null)
            {
                defender.FaceUpRelics.Remove(warHorn);
                RelicDiscardPile.Add(warHorn);
                var defBasic = defender.Faction.Units.FirstOrDefault(u => !u.IsSpecialUnit);
                if (defBasic != null)
                {
                    var hornSpawned = SpawnUnit(coord, defBasic.Id, defender, 1);
                    defenderUnits.AddRange(hornSpawned);
                    Log($"{defender.DisplayName} sounded War Horn -> Recruited 1 {defBasic.Name} into the defending battle!");
                }
            }

            // Phase 2: Dice Roll (2d6 each)
            int atkRoll = Roll2D6(out report.AttackerDice1, out report.AttackerDice2);
            int defRoll = Roll2D6(out report.DefenderDice1, out report.DefenderDice2);

            // Auto-check faceup combat Relics (Overgrown Wall, Iron Halberd, Political Treatise)
            var overgrownWall = defender.FaceUpRelics.FirstOrDefault(r => r.Id == "overgrown_wall");
            if (overgrownWall != null)
            {
                defender.FaceUpRelics.Remove(overgrownWall);
                RelicDiscardPile.Add(overgrownWall);
                report.AttackerDiceBonus -= 2;
                Log($"{defender.DisplayName} discarded Overgrown Wall (-2 to Attacker dice roll).");
            }

            var halberd = attacker.FaceUpRelics.FirstOrDefault(r => r.Id == "iron_halberd");
            if (halberd != null)
            {
                attacker.FaceUpRelics.Remove(halberd);
                RelicDiscardPile.Add(halberd);
                report.AttackerDiceBonus += 2;
                Log($"{attacker.DisplayName} discarded Iron Halberd (+2 to Attacker roll).");
            }

            var treatise = defender.FaceUpRelics.FirstOrDefault(r => r.Id == "political_treatise");
            if (treatise != null)
            {
                defender.FaceUpRelics.Remove(treatise);
                RelicDiscardPile.Add(treatise);
                report.DefenderDiceBonus += 2;
                Log($"{defender.DisplayName} discarded Political Treatise (+2 to Defender roll).");
            }

            // Phase 3: Attack and Defense Points
            report.AttackerUnitAttackSum = attackerUnits.Sum(u => GetUnitBattleAttack(attacker, tile, u));
            report.DefenderUnitDefenseSum = defenderUnits.Sum(u => GetUnitBattleDefense(defender, tile, u));
            if (hasChrysalis)
                report.DefenderUnitDefenseSum += 2; // Chrysalis is 0/2/2

            // Penguin Huddle: 3+ Penguins defending adds +1 total defense point
            if (defender.Faction.Id == FactionId.GlacierKingdom && defenderUnits.Count(u => u.Definition.Id == "penguin") >= 3)
                report.DefenderUnitDefenseSum += 1;

            tile.HasStatueOfGoddessBuff = false;

            report.AttackerTotal = Mathf.Max(0, atkRoll + report.AttackerDiceBonus) + report.AttackerUnitAttackSum;
            report.DefenderTotal = Mathf.Max(0, defRoll + report.DefenderDiceBonus) + report.DefenderUnitDefenseSum;

            // Phase 4: Damage Calculation
            if (report.AttackerTotal == report.DefenderTotal)
            {
                // Tie: Both factions take 1HP damage and battle ends immediately
                report.IsTie = true;
                report.WinnerPlayerIndex = -1;
                report.LoserPlayerIndex = -1;
                report.WinnerDamageDealt = 1;
                report.MandatoryLoserDamageDealt = 1;
                ApplyBattleDamageToSide(tile, attackerUnits, 1, defender.PlayerIndex, report);
                ApplyBattleDamageToSide(tile, defenderUnits, 1, attacker.PlayerIndex, report);
            }
            else
            {
                bool attackerWon = report.AttackerTotal > report.DefenderTotal;
                var winner = attackerWon ? attacker : defender;
                var loser = attackerWon ? defender : attacker;
                var winnerUnits = attackerWon ? attackerUnits : defenderUnits;
                var loserUnits = attackerWon ? defenderUnits : attackerUnits;

                report.WinnerPlayerIndex = winner.PlayerIndex;
                report.LoserPlayerIndex = loser.PlayerIndex;

                // Winner's Damage Dealt Formula (Rulebook Line 285): Winner's Attack Point Score minus Loser's Defense Point Score
                int winnerAttackScore = winnerUnits.Sum(u => GetUnitBattleAttack(winner, tile, u));
                int loserDefenseScore = loserUnits.Sum(u => GetUnitBattleDefense(loser, tile, u));
                int rawDamage = Mathf.Max(1, winnerAttackScore - loserDefenseScore);

                // Damage Cap: Per battle, each unit can only receive 1HP damage; cannot exceed number of winner's participating units
                int damageCap = Mathf.Max(1, winnerUnits.Count);
                // Golden Gauntlet or Withered Blossom bypasses 1HP cap for a single unit (+1 effective cap)
                bool bypassCap = winnerUnits.Any(u => u.EquippedItems.Any(e => e.Id == "golden_gauntlet")) ||
                                 (winner.Faction.Id == FactionId.BriaredFlower && winner.BriaredFlowerThorns == 0 && winnerUnits.Any(u => u.Definition.Id == "blooming_briar"));
                if (bypassCap) damageCap += 1;

                int finalWinnerDamage = Mathf.Min(rawDamage, damageCap);

                // Check Brass Shield on loser (-2HP from Winner's Damage Dealt)
                var brassShield = loser.FaceUpRelics.FirstOrDefault(r => r.Id == "brass_shield");
                if (brassShield != null && finalWinnerDamage > 1)
                {
                    loser.FaceUpRelics.Remove(brassShield);
                    RelicDiscardPile.Add(brassShield);
                    finalWinnerDamage = Mathf.Max(0, finalWinnerDamage - 2);
                    Log($"{loser.DisplayName} discarded Brass Shield (-2HP damage taken).");
                }

                // Mandatory Loser Damage: Loser always deals 1HP damage back to winner unless Horde of the Millions (Oblivious to Danger)
                int mandatoryLoserDmg = (loser.Faction.Id == FactionId.HordeOfTheMillions) ? 0 : 1;

                report.WinnerDamageDealt = finalWinnerDamage;
                report.MandatoryLoserDamageDealt = mandatoryLoserDmg;

                int crusadersBefore = loserUnits.Count(u => u.Definition.Id == "crusader");

                // Apply damage simultaneously
                ApplyBattleDamageToSide(tile, loserUnits, finalWinnerDamage, winner.PlayerIndex, report, bypassCap);
                if (mandatoryLoserDmg > 0)
                    ApplyBattleDamageToSide(tile, winnerUnits, mandatoryLoserDmg, loser.PlayerIndex, report, false);

                // Platypus Papacy Enemy Scoring: Killing 2 or more Crusaders in one battle gives 1 Authority (Line 941)
                int crusadersAfter = tile.Units.Count(u => u.OwnerPlayerIndex == loser.PlayerIndex && u.Definition.Id == "crusader");
                if (crusadersBefore - crusadersAfter >= 2)
                {
                    AwardAuthority(winner.PlayerIndex, 1, "Killed 2+ Platypus Crusaders in one battle");
                }

                // Trouble-Making Tanukis Quick Feet: surviving Tanukis scatter to empty adjacent tiles and drop 1 Fallen Leaves Tile (Line 1286)
                if (loser.Faction.Id == FactionId.TroubleMakingTanukis && finalWinnerDamage >= 1)
                {
                    var survivingTanukis = tile.Units.Where(u => u.OwnerPlayerIndex == loser.PlayerIndex && u.Definition.Id == "tanuki").ToList();
                    if (survivingTanukis.Count > 0)
                    {
                        var safeAdj = coord.GetNeighbors().Where(n => Board.ContainsKey(n) && Board[n].Units.Count == 0).ToList();
                        if (safeAdj.Count > 0)
                        {
                            var dest = safeAdj[0];
                            foreach (var st in survivingTanukis)
                            {
                                tile.Units.Remove(st);
                                Board[dest].Units.Add(st);
                            }
                            PlaceOverlayTile(dest, FactionOverlayTileType.FallenLeaf_Unflipped, loser.PlayerIndex);
                            Log($"Quick Feet! Surviving Tanukis scattered to {dest} and dropped a Fallen Leaves Tile!");
                        }
                    }
                }

                // Post-Battle Faction Triggers (Curly Tail Momentum, Spoils of War, Pillager; Platypus Crusade)
                if (winner.Faction.Id == FactionId.CurlyTail)
                {
                    if (!winner.CurlyTailWonBattleThisTurn)
                    {
                        winner.CurlyTailWonBattleThisTurn = true;
                        winner.ResourcePile.Add(_rng.Next(2) == 0 ? ResourceType.Wood : ResourceType.Stone);
                        Log($"{winner.DisplayName} triggered Spoils of War -> Gained 1 resource.");
                    }
                    winner.CurlyTailMomentum = Mathf.Min(4, winner.CurlyTailMomentum + 1);
                    if (winnerUnits.Any(u => u.Definition.Id == "curly_tail"))
                    {
                        winner.ResourcePile.Add(DrawResourceCard());
                        if (winner.CurlyTailMomentum >= 1)
                        {
                            var rel = DrawRelicCard();
                            if (rel != null) winner.RelicHand.Add(rel);
                        }
                        Log($"{winner.DisplayName} triggered Curly Tail's Pillager!");
                    }
                }
                if (loser.Faction.Id == FactionId.CurlyTail)
                {
                    loser.CurlyTailMomentum = Mathf.Max(0, loser.CurlyTailMomentum - 2);
                    loser.CurlyTailHunger = Mathf.Max(0, loser.CurlyTailHunger - 2);
                    loser.CurlyTailThirstForGlory = Mathf.Max(0, loser.CurlyTailThirstForGlory - 1);
                }
                if (winner.Faction.Id == FactionId.PlatypusPapacy && winner.CrusadeTurnsRemaining > 0 && !winner.PlatyPopeKilled)
                {
                    winner.PlatypusFaith = Mathf.Min(5, winner.PlatypusFaith + 1);
                }
            }

            MainPhaseActionsRemaining--;
            report.SummaryLog = $"Battle at {coord}: {attacker.DisplayName} ({report.AttackerTotal}) vs {defender.DisplayName} ({report.DefenderTotal}) -> " +
                                (report.IsTie ? "Tie (1HP each)" : $"{Players[report.WinnerPlayerIndex].DisplayName} wins ({report.WinnerDamageDealt}HP dealt)!");
            Log(report.SummaryLog);

            LastBattleReport = report;
            OnBattleResolved?.Invoke(report);
            OnStateChanged?.Invoke();
            return report;
        }

        private int GetUnitBattleAttack(PlayerState owner, HexTileState tile, UnitInstance u)
        {
            int atk = u.EffectiveAttack;
            if (tile.HasStatueOfGoddessBuff) atk += 1;
            if (owner.Faction.Id == FactionId.CurlyTail)
            {
                if (u.Definition.Id == "curly_tail" && owner.CurlyTailThirstForGlory >= 1) atk += 1;
                if (owner.CurlyTailMomentum >= 2) atk += 1;
                if (owner.CurlyTailMomentum >= 4) atk += 1;
            }
            else if (owner.Faction.Id == FactionId.PlatypusPapacy && u.Definition.Id == "crusader")
            {
                if (owner.CrusadeTurnsRemaining > 0) atk += 1;
                if (owner.PlatyPopeLevel >= 2) atk += 1;
                if (owner.PlatyPopeLevel >= 4) atk += 1;
            }
            else if (owner.Faction.Id == FactionId.BriaredFlower && u.Definition.Id == "blooming_briar" && owner.BriaredFlowerThorns == 0)
            {
                atk += 3; // Withered Blossom (+3 Attack at 0 Thorns)
            }
            return atk;
        }

        private int GetUnitBattleDefense(PlayerState owner, HexTileState tile, UnitInstance u)
        {
            int def = u.EffectiveDefense;
            if (owner.Faction.Id == FactionId.CurlyTail && u.Definition.Id == "curly_tail" && owner.CurlyTailThirstForGlory >= 3)
                def += 1;
            if (owner.Faction.Id == FactionId.PlatypusPapacy && u.Definition.Id == "crusader")
            {
                if (owner.PlatyPopeLevel >= 2) def += 1;
                if (owner.PlatyPopeLevel >= 4) def += 1;
            }
            // Anankian Shield on Vanguard buffs all friendly units on the same tile by +1 Defense
            if (owner.Faction.Id == FactionId.Necromouser &&
                tile.Units.Any(x => x.OwnerPlayerIndex == owner.PlayerIndex && x.EquippedItems.Any(e => e.Id == "anankian_shield")))
            {
                def += 1;
            }
            return def;
        }

        private void ApplyBattleDamageToSide(HexTileState tile, List<UnitInstance> units, int totalDamage, int killerPlayerIndex, BattleReport report, bool allow2HpToSingleUnit = false)
        {
            var ordered = units.OrderBy(u => u.Definition.IsSpecialUnit ? 1 : 0).ToList();
            int remaining = totalDamage;
            foreach (var u in ordered)
            {
                if (remaining <= 0) break;

                var owner = Players[u.OwnerPlayerIndex];
                // Briared Flower Thorn Armor: Remove 1 Thorn instead of taking 1HP
                if (owner.Faction.Id == FactionId.BriaredFlower && u.Definition.Id == "blooming_briar" && owner.BriaredFlowerThorns > 0)
                {
                    owner.BriaredFlowerThorns--;
                    remaining--;
                    Log($"Blooming Briar shed 1 Thorn via Thorn Armor ({owner.BriaredFlowerThorns} Thorns remaining).");
                    continue;
                }

                int hit = (allow2HpToSingleUnit && remaining >= 2 && u.CurrentHP >= 2) ? 2 : 1;
                u.CurrentHP -= hit;
                remaining -= hit;
                if (u.CurrentHP <= 0)
                {
                    report.DestroyedUnitNames.Add(u.Definition.Name);
                    RemoveUnitFromBoard(tile, u, killerPlayerIndex);
                }
            }
        }

        private void RemoveUnitFromBoard(HexTileState tile, UnitInstance u, int killerPlayerIndex)
        {
            var owner = Players[u.OwnerPlayerIndex];

            // 1. Necromouser Cowardice (Line 402): When isolated and killed, lose 3 Authority, relocate 2 tiles away, place 3 Miasma Tiles
            if (u.Definition.Id == "necromouser")
            {
                owner.Authority = Mathf.Max(0, owner.Authority - 3);
                u.CurrentHP = 1;
                tile.Units.Remove(u);
                var escapeTile = Board.Values.FirstOrDefault(t => HexCoord.Distance(t.Coord, tile.Coord) == 2) ?? tile;
                escapeTile.Units.Add(u);
                int mPlaced = 0;
                foreach (var n in escapeTile.Coord.GetNeighbors())
                {
                    if (Board.ContainsKey(n) && Board[n].Overlay == FactionOverlayTileType.None && mPlaced < 3)
                    {
                        PlaceOverlayTile(n, FactionOverlayTileType.Miasma_Revealed, owner.PlayerIndex);
                        mPlaced++;
                    }
                }
                Log($"Cowardice! The Necromouser lost 3 Authority and fled to {escapeTile.Coord}, spreading {mPlaced} Miasma Tiles!");
                return;
            }

            // 2. Curly Tail Strategic Retreat (Line 735): Killer gains 2 Authority, Curly Tail discards 2 resources, keeps equipment, respawns at a War Party
            if (u.Definition.Id == "curly_tail")
            {
                if (killerPlayerIndex >= 0 && killerPlayerIndex != owner.PlayerIndex)
                    AwardAuthority(killerPlayerIndex, 2, "Forced Curly Tail to Strategic Retreat");
                for (int i = 0; i < 2 && owner.ResourcePile.Count > 0; i++)
                    ReturnResourceToDeck(owner.ResourcePile, 0);

                tile.Units.Remove(u);
                var warPartyTile = Board.Values.FirstOrDefault(t => t.Building != null && t.Building.Type == BuildingType.WarParty && t.Building.OwnerPlayerIndex == owner.PlayerIndex);
                if (warPartyTile != null)
                {
                    u.CurrentHP = u.MaxHP;
                    warPartyTile.Units.Add(u);
                    SpawnUnit(warPartyTile.Coord, "warband", owner, 1);
                    Log($"Strategic Retreat! Curly Tail retreated to War Party at {warPartyTile.Coord} with his equipment intact!");
                }
                return;
            }

            // 3. Tanukis No Honor Amongst Troublemakers (Line 1309): Splits into 3 Tanukis on adjacent forest tiles, killer gains 2 Authority
            if (u.Definition.Id == "band_of_troublemakers")
            {
                tile.Units.Remove(u);
                if (killerPlayerIndex >= 0 && killerPlayerIndex != owner.PlayerIndex)
                    AwardAuthority(killerPlayerIndex, 2, "Disbanded A Band of Troublemakers");
                int split = 0;
                foreach (var n in tile.Coord.GetNeighbors())
                {
                    if (Board.ContainsKey(n) && Board[n].EffectiveTerrain == TileTerrainType.Forest && split < 3)
                    {
                        SpawnUnit(n, "tanuki", owner, 1);
                        split++;
                    }
                }
                Log($"No Honor Amongst Troublemakers! The Band split into {split} Tanukis on adjacent Forest tiles!");
                return;
            }

            // 4. Briared Flower Propagation (Line 1462): Deals 1HP to an enemy on tile, places 1 Thorned Seed on adjacent forest tile, killer gains 2 Authority
            if (u.Definition.Id == "blooming_briar")
            {
                tile.Units.Remove(u);
                var enemyHere = tile.Units.FirstOrDefault(x => x.OwnerPlayerIndex != owner.PlayerIndex);
                if (enemyHere != null)
                {
                    enemyHere.CurrentHP -= 1;
                    if (enemyHere.CurrentHP <= 0) tile.Units.Remove(enemyHere);
                }
                var adjForest = tile.Coord.GetNeighbors().FirstOrDefault(n => Board.ContainsKey(n) && Board[n].EffectiveTerrain == TileTerrainType.Forest);
                if (Board.ContainsKey(adjForest))
                    SpawnUnit(adjForest, "thorned_seed", owner, 1);
                if (killerPlayerIndex >= 0 && killerPlayerIndex != owner.PlayerIndex)
                    AwardAuthority(killerPlayerIndex, 2, "Cut down the Blooming Briar (Propagation triggered)");
                return;
            }

            // 5. Platy-Pope death penalty (Line 916)
            if (u.Definition.Id == "platy_pope")
            {
                owner.PlatyPopeKilled = true;
                owner.Authority = Mathf.Max(0, owner.Authority - 1);
            }

            tile.Units.Remove(u);

            // Score Authority for killing Special Units (Rulebook Part 15)
            if (u.Definition.IsSpecialUnit && killerPlayerIndex >= 0 && killerPlayerIndex != u.OwnerPlayerIndex)
            {
                int authGain = 2;
                if (owner.Faction.Id == FactionId.Necromouser)
                {
                    if (u.Definition.Id == "the_bloat")
                    {
                        authGain = 1;
                        Players[killerPlayerIndex].ResourcePile.Add(DrawResourceCard());
                    }
                    else if (u.Definition.Id == "the_vanguard") authGain = 3;
                    else if (u.Definition.Id == "the_brute") authGain = 4;
                }
                else if (u.Definition.Id == "capybara_trader")
                {
                    authGain = 1;
                }
                if (owner.Faction.Id == FactionId.GoatCabal)
                {
                    owner.KilledDemonIds.Add(u.Definition.Id);
                }
                AwardAuthority(killerPlayerIndex, authGain, $"Defeated {u.Definition.Name}");
            }

            // Carnivorous Queen Feeding & Rage triggers
            if (killerPlayerIndex >= 0)
            {
                var killer = Players[killerPlayerIndex];
                if (killer.Faction.Id == FactionId.CarnivorousQueen)
                {
                    killer.QueenFeeding = Mathf.Min(7, killer.QueenFeeding + (u.Definition.IsSpecialUnit ? 2 : 1));
                    killer.QueenRage = Mathf.Min(15, killer.QueenRage + 1);
                    UpdateCarnivorousQueenStage(killer);
                }
            }
            if (owner.Faction.Id == FactionId.CarnivorousQueen && u.Definition.Id == "larvae")
            {
                owner.QueenRage = Mathf.Min(15, owner.QueenRage + 1);
            }

            // Necromouser Miasma trigger: Any unit removed on or adjacent to a Miasma Tile adds 1 Dead to Cauldron & awakens Necromouser
            bool onOrAdjMiasma = (tile.Overlay == FactionOverlayTileType.Miasma_Hidden || tile.Overlay == FactionOverlayTileType.Miasma_Revealed) ||
                                 tile.Coord.GetNeighbors().Any(n => Board.ContainsKey(n) &&
                                     (Board[n].Overlay == FactionOverlayTileType.Miasma_Hidden || Board[n].Overlay == FactionOverlayTileType.Miasma_Revealed));
            if (onOrAdjMiasma)
            {
                foreach (var necroPlayer in Players.Where(pl => pl.Faction.Id == FactionId.Necromouser))
                {
                    necroPlayer.NecromouserCauldronDeadCount++;
                    if (!necroPlayer.NecromouserAwakened)
                    {
                        necroPlayer.NecromouserAwakened = true;
                        foreach (var bt in Board.Values.Where(t => t.Overlay == FactionOverlayTileType.Miasma_Hidden))
                            bt.Overlay = FactionOverlayTileType.Miasma_Revealed;
                        SpawnUnit(tile.Coord, "necromouser", necroPlayer, 1);
                        SpawnUnit(tile.Coord, "skeleton", necroPlayer, 1);
                        Log($"THE NECROMOUSER AWAKENS at {tile.Coord}! All Miasma Tiles revealed!");
                    }
                }
            }
        }

        private void UpdateCarnivorousQueenStage(PlayerState queenPlayer)
        {
            int newStage = 1;
            if (queenPlayer.QueenFeeding >= 7) newStage = 4;
            else if (queenPlayer.QueenFeeding >= 4) newStage = 3;
            else if (queenPlayer.QueenFeeding >= 2) newStage = 2;

            if (newStage > queenPlayer.QueenStage)
            {
                queenPlayer.QueenStage = newStage;
                var qUnit = Board.Values.SelectMany(t => t.Units).FirstOrDefault(u => u.OwnerPlayerIndex == queenPlayer.PlayerIndex && u.Definition.Id == "carnivorous_queen");
                if (qUnit != null)
                {
                    qUnit.BonusAttack = newStage - 1;
                    qUnit.BonusDefense = newStage >= 3 ? 1 : 0;
                    qUnit.MaxHP = newStage == 4 ? 5 : (newStage == 3 ? 4 : 3);
                    qUnit.CurrentHP = Mathf.Min(qUnit.MaxHP, qUnit.CurrentHP + 1);
                }

                // Auto-draft next available Mutation card from the 7-card pool
                var availableMut = queenPlayer.Faction.Equipment.FirstOrDefault(m => !queenPlayer.DraftedMutations.Any(d => d.Id == m.Id));
                if (availableMut != null)
                {
                    queenPlayer.DraftedMutations.Add(availableMut);
                    if (qUnit != null) qUnit.EquippedItems.Add(availableMut);
                    Log($"{queenPlayer.DisplayName}'s Carnivorous Queen evolved to Stage {newStage} and drafted Mutation '{availableMut.Name}'!");
                }
            }
        }

        private void NotifyEnemyActionTriggerForCarnivorousQueen(int actingPlayerIndex, int rageAmount)
        {
            foreach (var p in Players.Where(pl => pl.Faction.Id == FactionId.CarnivorousQueen && pl.PlayerIndex != actingPlayerIndex))
            {
                p.QueenRage = Mathf.Min(15, p.QueenRage + rageAmount);
            }
        }

        // =====================================================================
        // FACTION ACTION PALETTE (Every Faction's Specific Abilities, Tokens, Tiles, Summons)
        // =====================================================================

        public List<string> GetAvailableFactionActions(PlayerState p)
        {
            var actions = new List<string>();
            switch (p.Faction.Id)
            {
                case FactionId.Necromouser:
                    actions.Add("Arise: Spend 1 Dead -> Summon Skeleton on Miasma");
                    actions.Add("Spread the Bad News: Sacrifice Skeleton -> Place Miasma Tile");
                    actions.Add("Summon The Bloat (1 Dead -> +1 Authority)");
                    actions.Add("Summon The Vanguard (3 Dead -> +3 Authority)");
                    actions.Add("Summon The Brute (4 Dead -> +4 Authority)");
                    actions.Add("Necrosis: Explode The Bloat (2HP AoE)");
                    break;
                case FactionId.HordeOfTheMillions:
                    actions.Add("Other People's Gluttony: 1 Gold -> Place Gluttony Token");
                    actions.Add("Catapult Token: 2 Stone + 1 Wood -> Launch 1-3 Millions 3 tiles");
                    actions.Add("Summon King of the Millions (1G+1S+1W -> All Opponents Forage)");
                    actions.Add("King of the Rabbit Hill: Spawn 1 Millions at King");
                    break;
                case FactionId.GoatCabal:
                    actions.Add("Sparagmos: Purge 1 Goat at Summoning Circle -> Purgatory");
                    actions.Add("Invocation & Canto Inferno: Summon Demon (Faust / Midian / Sovereign / Lord of War / Beast)");
                    actions.Add("Effigy Token: Spend 1 Purged -> Gain 1 Gold");
                    actions.Add("Faust Bargain: Purge 1 Goat -> Mill Grimoire");
                    break;
                case FactionId.CurlyTail:
                    actions.Add("Supply Line: 1 Wood -> Place Supply Line Token on Selected Hex");
                    actions.Add("Forge Golden Sword (1 Gold -> +2 Auth, +1 ATK)");
                    actions.Add("Forge Golden Chest Plate (2 Gold -> +2 Auth, +2 DEF)");
                    actions.Add("Forge Golden Gauntlet (2 Gold -> +2 Auth, 2HP vs Special)");
                    actions.Add("Taunt (Glory Lv3+): Pull Enemy & Free Battle");
                    break;
                case FactionId.FirestartingSquirrels:
                    actions.Add("Craft Acorn Firestarter (1 Charcoal + 1 Wood)");
                    actions.Add("Ignite Acorn Firestarter -> Place Fire Tile on Squirrel Hex");
                    actions.Add("Place Tar Pit Token (2 Charcoal)");
                    actions.Add("Unquenchable Spirit: Loot Adjacent Fire Tile for 1 Charcoal");
                    actions.Add("Summon Pyromaniac (1W+1S+1G+1C on Fire Tile)");
                    break;
                case FactionId.PlatypusPapacy:
                    actions.Add("Alms (5 Faith -> +1 Platy-Pope Level, +1 Authority, Enter Crusade!)");
                    actions.Add("Indulgences (1 Gold -> +2 Faith)");
                    actions.Add("Craft Holy Relic Token at Citadel (1W+1S+1G -> +1 Faith/turn)");
                    actions.Add("Crusader Conversion (Lv3+: 2 Faith -> Convert Enemy to Pilgrim)");
                    break;
                case FactionId.CarnivorousQueen:
                    actions.Add("Hidden Amongst the Trees (2 Rage -> Queen becomes Hidden)");
                    actions.Add("Ensnare Enemy on Queen's Tile (3 Rage -> +1 Auth, +1 Feeding)");
                    actions.Add("Verdant Voracity (3 Rage -> Teleport Queen to Chrysalis/Larvae)");
                    actions.Add("Nesting (Discard 1 Relic -> Place Egg Token at Chrysalis)");
                    actions.Add("Darwinism (1 Egg + 1 Larvae -> Respawn Queen at Chrysalis)");
                    break;
                case FactionId.GlacierKingdom:
                    actions.Add("Avalanche: Place/Upgrade/Cascade Snowfall Token");
                    actions.Add("Craft Iceberg Wall (2 Stone -> 0/0/2 Wall on Glacier Hex)");
                    actions.Add("Craft Frozen Harbor (2 Wood -> +1 Excavation, Loots Gold)");
                    actions.Add("Summon Emperor Penguin (2 Gold at Igloo/Harbor)");
                    actions.Add("Blizzard (1 Gold -> +1 Authority & Freeze Isolated Enemies)");
                    break;
                case FactionId.TroubleMakingTanukis:
                    actions.Add("Running Amok: Place Fallen Leaves Tile on Tanuki Hex");
                    actions.Add("Summon A Band of Troublemakers (3 Tanukis + 1 Gold)");
                    actions.Add("Belly Drums: Place Fallen Leaves on up to 2 Coin Tiles");
                    break;
                case FactionId.CapybaraCaravan:
                    actions.Add("Capybara Forage (On River Tile -> Gain 1 Gold or 1 Stone)");
                    actions.Add("Exchange Rate (Trade 3 Identical Resources -> 2 Gold)");
                    actions.Add("Summon The Riverkeeper (2+ Wallows, 2 Res + 1 Gold)");
                    actions.Add("Riverkeeper Hospitality (+1 Authority & Recover Relic)");
                    break;
                case FactionId.BriaredFlower:
                    actions.Add("Thistle Barrage (1 Thorn + 1 Res -> Snipe Enemy within 4-6 tiles & Spawn Seed)");
                    actions.Add("Sun Eater (2 Seeds + 3 Res -> +2 Thorns)");
                    actions.Add("Bursting Into Bloom (Sacrifice up to 3 Seeds -> 1HP Damage Each)");
                    actions.Add("Furious Lashing (At 0 Thorns -> 1HP AoE within 3 Tiles)");
                    actions.Add("In Half Bloom (2 Res + 3 Seeds -> Respawn Blooming Briar)");
                    break;
            }
            return actions;
        }

        public bool ExecuteSpecificFactionAction(int actionIndex, HexCoord selectedCoord)
        {
            var p = Players[CurrentTurnPlayerIndex];
            if (!Board.TryGetValue(selectedCoord, out var selectedTile))
                selectedTile = Board.Values.First();

            switch (p.Faction.Id)
            {
                case FactionId.Necromouser:
                    if (actionIndex == 0 && p.NecromouserCauldronDeadCount >= 1)
                    {
                        var mTile = Board.Values.FirstOrDefault(t => t.Overlay == FactionOverlayTileType.Miasma_Revealed || t.Overlay == FactionOverlayTileType.Miasma_Hidden) ?? selectedTile;
                        p.NecromouserCauldronDeadCount--;
                        SpawnUnit(mTile.Coord, "skeleton", p, 1);
                        Log($"{p.DisplayName} used Arise -> Summoned 1 Skeleton at {mTile.Coord}.");
                    }
                    else if (actionIndex == 1)
                    {
                        var skTile = Board.Values.FirstOrDefault(t => t.Units.Any(u => u.Definition.Id == "skeleton"));
                        if (skTile == null) return false;
                        skTile.Units.Remove(skTile.Units.First(u => u.Definition.Id == "skeleton"));
                        PlaceOverlayTile(skTile.Coord, FactionOverlayTileType.Miasma_Revealed, p.PlayerIndex);
                        Log($"{p.DisplayName} used Spread the Bad News -> Placed Miasma Tile at {skTile.Coord}.");
                    }
                    else if (actionIndex >= 2 && actionIndex <= 4 && p.NecromouserSpecialSummonsUsed < 4)
                    {
                        int cost = actionIndex == 2 ? 1 : (actionIndex == 3 ? 3 : 4);
                        string uId = actionIndex == 2 ? "the_bloat" : (actionIndex == 3 ? "the_vanguard" : "the_brute");
                        if (p.NecromouserCauldronDeadCount < cost) return false;
                        var nTile = Board.Values.FirstOrDefault(t => t.Units.Any(u => u.Definition.Id == "necromouser")) ?? selectedTile;
                        p.NecromouserCauldronDeadCount -= cost;
                        p.NecromouserSpecialSummonsUsed++;
                        SpawnUnit(nTile.Coord, uId, p, 1);
                        AwardAuthority(p.PlayerIndex, cost, $"Summoned {uId}");
                    }
                    else if (actionIndex == 5)
                    {
                        var bTile = Board.Values.FirstOrDefault(t => t.Units.Any(u => u.Definition.Id == "the_bloat"));
                        if (bTile == null) return false;
                        var bloat = bTile.Units.First(u => u.Definition.Id == "the_bloat");
                        bTile.Units.Remove(bloat);
                        foreach (var enemy in bTile.Units.Where(u => u.OwnerPlayerIndex != p.PlayerIndex).ToList())
                        {
                            enemy.CurrentHP -= 2;
                            if (enemy.CurrentHP <= 0) RemoveUnitFromBoard(bTile, enemy, p.PlayerIndex);
                        }
                        Log($"The Bloat exploded via Necrosis at {bTile.Coord} for 2HP AoE damage!");
                    }
                    else return false;
                    break;

                case FactionId.HordeOfTheMillions:
                    if (actionIndex == 0 && p.ResourcePile.Contains(ResourceType.Gold))
                    {
                        var mTile = Board.Values.FirstOrDefault(t => t.Units.Any(u => u.Definition.Id == "millions")) ?? selectedTile;
                        RemoveResources(p, ResourceType.Gold, 1);
                        PlaceToken(mTile.Coord, TokenType.Gluttony, p);
                        Log($"{p.DisplayName} placed a Gluttony Token at {mTile.Coord}.");
                    }
                    else if (actionIndex == 1 && CanAfford(p, new ResourceCost(wood: 1, stone: 2)))
                    {
                        SpendResources(p, new ResourceCost(wood: 1, stone: 2));
                        PlaceToken(selectedTile.Coord, TokenType.Catapult, p);
                        DrawAndResolveForageCard(p);
                        Log($"{p.DisplayName} crafted & fired a Catapult Token at {selectedTile.Coord}!");
                    }
                    else if (actionIndex == 2 && CanAfford(p, new ResourceCost(wood: 1, stone: 1, gold: 1)))
                    {
                        var bTile = Board.Values.FirstOrDefault(t => t.Building != null && t.Building.Type == BuildingType.Burrow) ?? selectedTile;
                        SpendResources(p, new ResourceCost(wood: 1, stone: 1, gold: 1));
                        SpawnUnit(bTile.Coord, "king_of_the_millions", p, 1);
                        foreach (var op in Players.Where(x => x.PlayerIndex != p.PlayerIndex))
                            DrawAndResolveForageCard(op);
                        Log($"{p.DisplayName} summoned King of the Millions at {bTile.Coord} (Spread the Fun)!");
                    }
                    else if (actionIndex == 3)
                    {
                        var kTile = Board.Values.FirstOrDefault(t => t.Units.Any(u => u.Definition.Id == "king_of_the_millions"));
                        if (kTile == null) return false;
                        SpawnUnit(kTile.Coord, "millions", p, 1);
                        Log("King of the Rabbit Hill spawned 1 Millions unit!");
                    }
                    else return false;
                    break;

                case FactionId.GoatCabal:
                    if (actionIndex == 0)
                    {
                        var scTile = Board.Values.FirstOrDefault(t => t.Building != null && t.Building.Type == BuildingType.SummoningCircle && t.Units.Any(u => u.Definition.Id == "goat"));
                        if (scTile == null) return false;
                        scTile.Units.Remove(scTile.Units.First(u => u.Definition.Id == "goat"));
                        p.GoatPurgatoryCount++;
                        Log($"{p.DisplayName} used Sparagmos -> Purged 1 Goat to Purgatory ({p.GoatPurgatoryCount}).");
                    }
                    else if (actionIndex == 1 && p.GoatPurgatoryCount >= 2)
                    {
                        var scTile = Board.Values.FirstOrDefault(t => t.Building != null && t.Building.Type == BuildingType.SummoningCircle) ?? selectedTile;
                        string[] demonIds = { "faust", "the_midian", "serpentine_sovereign", "lord_of_war", "horned_beast" };
                        string pick = demonIds.FirstOrDefault(d => !p.KilledDemonIds.Contains(d) && !Board.Values.SelectMany(t => t.Units).Any(u => u.Definition.Id == d)) ?? "faust";
                        p.GoatPurgatoryCount -= 2;
                        SpawnUnit(scTile.Coord, pick, p, 1);
                        if (pick == "faust") { MillAndResolveGrimoireCard(p); MillAndResolveGrimoireCard(p); }
                        else if (pick == "the_midian") { int circles = Board.Values.Count(t => t.Building != null && t.Building.Type == BuildingType.SummoningCircle); AwardAuthority(p.PlayerIndex, Mathf.Max(1, circles), "The Midian: A Gift"); }
                        else if (pick == "serpentine_sovereign") { selectedTile.MarkedByRoyalDecree = true; Log($"Serpentine Sovereign marked {selectedTile.Coord} for Royal Decree destruction!"); }
                        Log($"{p.DisplayName} summoned Demon '{pick}' at {scTile.Coord}!");
                    }
                    else if (actionIndex == 2 && p.GoatPurgatoryCount >= 1)
                    {
                        p.GoatPurgatoryCount--;
                        p.ResourcePile.Add(ResourceType.Gold);
                        Log($"{p.DisplayName} used Effigy -> Spent 1 Purged for +1 Gold.");
                    }
                    else if (actionIndex == 3)
                    {
                        MillAndResolveGrimoireCard(p);
                    }
                    else return false;
                    break;

                case FactionId.CurlyTail:
                    if (actionIndex == 0 && p.ResourcePile.Contains(ResourceType.Wood))
                    {
                        RemoveResources(p, ResourceType.Wood, 1);
                        PlaceToken(selectedTile.Coord, TokenType.SupplyLine, p);
                        Log($"{p.DisplayName} placed a Supply Line Token at {selectedTile.Coord}.");
                    }
                    else if (actionIndex == 1) return TryCraftAndEquipFactionEquipment("golden_sword");
                    else if (actionIndex == 2) return TryCraftAndEquipFactionEquipment("golden_chestplate");
                    else if (actionIndex == 3) return TryCraftAndEquipFactionEquipment("golden_gauntlet");
                    else if (actionIndex == 4 && p.CurlyTailThirstForGlory >= 3)
                    {
                        var ctTile = Board.Values.FirstOrDefault(t => t.Units.Any(u => u.Definition.Id == "curly_tail"));
                        if (ctTile == null) return false;
                        var enemyTile = Board.Values.FirstOrDefault(t => HexCoord.Distance(t.Coord, ctTile.Coord) <= 2 && t.Units.Any(u => u.OwnerPlayerIndex != p.PlayerIndex));
                        if (enemyTile == null) return false;
                        var pulled = enemyTile.Units.First(u => u.OwnerPlayerIndex != p.PlayerIndex);
                        enemyTile.Units.Remove(pulled);
                        ctTile.Units.Add(pulled);
                        Log($"Curly Tail used Taunt to pull {pulled.Definition.Name} onto {ctTile.Coord}!");
                        TryBattleOnTile(ctTile.Coord, pulled.OwnerPlayerIndex);
                    }
                    else return false;
                    break;

                case FactionId.FirestartingSquirrels:
                    if (actionIndex == 0 && CanAfford(p, new ResourceCost(wood: 1, charcoal: 1)))
                    {
                        SpendResources(p, new ResourceCost(wood: 1, charcoal: 1));
                        p.SquirrelsCraftedFirestarters++;
                        Log($"{p.DisplayName} crafted an Acorn Firestarter ({p.SquirrelsCraftedFirestarters}).");
                    }
                    else if (actionIndex == 1 && p.SquirrelsCraftedFirestarters > 0)
                    {
                        var sqTile = (selectedTile.Units.Any(u => u.Definition.Id == "squirrel") ? selectedTile :
                                      Board.Values.FirstOrDefault(t => t.Units.Any(u => u.Definition.Id == "squirrel") && t.Overlay == FactionOverlayTileType.None));
                        if (sqTile == null) return false;
                        p.SquirrelsCraftedFirestarters--;
                        sqTile.Overlay = FactionOverlayTileType.Fire;
                        sqTile.OverlayOwnerPlayerIndex = p.PlayerIndex;
                        Log($"{p.DisplayName} ignited an Acorn Firestarter -> Placed Fire Tile at {sqTile.Coord}!");
                    }
                    else if (actionIndex == 2 && CanAfford(p, new ResourceCost(charcoal: 2)))
                    {
                        SpendResources(p, new ResourceCost(charcoal: 2));
                        PlaceToken(selectedTile.Coord, TokenType.TarPit, p);
                        Log($"{p.DisplayName} placed a Tar Pit Token at {selectedTile.Coord}.");
                    }
                    else if (actionIndex == 3)
                    {
                        p.ResourcePile.Add(ResourceType.Charcoal);
                        Log($"{p.DisplayName} used Unquenchable Spirit -> Gained 1 Charcoal.");
                    }
                    else if (actionIndex == 4 && CanAfford(p, new ResourceCost(wood: 1, stone: 1, gold: 1, charcoal: 1)))
                    {
                        var fTile = Board.Values.FirstOrDefault(t => t.Overlay == FactionOverlayTileType.Fire) ?? selectedTile;
                        SpendResources(p, new ResourceCost(wood: 1, stone: 1, gold: 1, charcoal: 1));
                        SpawnUnit(fTile.Coord, "pyromaniac", p, 1);
                        Log($"{p.DisplayName} summoned the Pyromaniac at {fTile.Coord}!");
                    }
                    else return false;
                    break;

                case FactionId.PlatypusPapacy:
                    if (actionIndex == 0 && p.PlatypusFaith >= 5 && !p.PlatyPopeKilled)
                    {
                        p.PlatypusFaith = 0;
                        p.PlatyPopeLevel = Mathf.Min(5, p.PlatyPopeLevel + 1);
                        p.CrusadeTurnsRemaining += 2;
                        AwardAuthority(p.PlayerIndex, 1, $"Alms (Platy-Pope Level {p.PlatyPopeLevel} & Crusade!)");
                    }
                    else if (actionIndex == 1 && p.ResourcePile.Contains(ResourceType.Gold) && !p.PlatyPopeKilled)
                    {
                        RemoveResources(p, ResourceType.Gold, 1);
                        p.PlatypusFaith = Mathf.Min(5, p.PlatypusFaith + 2);
                        Log($"{p.DisplayName} used Indulgences (1 Gold -> +2 Faith, now {p.PlatypusFaith}/5).");
                    }
                    else if (actionIndex == 2 && CanAfford(p, new ResourceCost(wood: 1, stone: 1, gold: 1)))
                    {
                        var citTile = Board.Values.FirstOrDefault(t => t.Building != null && t.Building.Type == BuildingType.TheCitadel) ?? selectedTile;
                        SpendResources(p, new ResourceCost(wood: 1, stone: 1, gold: 1));
                        PlaceToken(citTile.Coord, TokenType.HolyRelic, p);
                        Log($"{p.DisplayName} enshrined a Holy Relic Token at The Citadel ({citTile.Coord})!");
                    }
                    else if (actionIndex == 3 && p.PlatyPopeLevel >= 3 && p.PlatypusFaith >= 2)
                    {
                        var cTile = Board.Values.FirstOrDefault(t => t.Units.Any(u => u.Definition.Id == "crusader") && t.Units.Any(u => u.OwnerPlayerIndex != p.PlayerIndex && !u.Definition.IsSpecialUnit));
                        if (cTile == null) return false;
                        p.PlatypusFaith -= 2;
                        var enemy = cTile.Units.First(u => u.OwnerPlayerIndex != p.PlayerIndex && !u.Definition.IsSpecialUnit);
                        cTile.Units.Remove(enemy);
                        SpawnUnit(cTile.Coord, "pilgrim", p, 1);
                        Log($"{p.DisplayName} converted {enemy.Definition.Name} at {cTile.Coord} into a Pilgrim!");
                    }
                    else return false;
                    break;

                case FactionId.CarnivorousQueen:
                    var qTile = Board.Values.FirstOrDefault(t => t.Units.Any(u => u.Definition.Id == "carnivorous_queen"));
                    var qUnit = qTile?.Units.First(u => u.Definition.Id == "carnivorous_queen");
                    if (actionIndex == 0 && p.QueenRage >= 2 && qUnit != null)
                    {
                        p.QueenRage -= 2;
                        qUnit.IsHidden = true;
                        Log($"{p.DisplayName}'s Carnivorous Queen became Hidden Amongst the Trees!");
                    }
                    else if (actionIndex == 1 && p.QueenRage >= 3 && qTile != null)
                    {
                        var victim = qTile.Units.FirstOrDefault(u => u.OwnerPlayerIndex != p.PlayerIndex && !u.Definition.IsSpecialUnit);
                        if (victim == null) return false;
                        p.QueenRage -= 3;
                        qTile.Units.Remove(victim); // Exiled
                        SpawnUnit(qTile.Coord, "ensnared", p, 1);
                        p.QueenFeeding = Mathf.Min(7, p.QueenFeeding + 1);
                        UpdateCarnivorousQueenStage(p);
                        AwardAuthority(p.PlayerIndex, 1, $"Ensnared {victim.Definition.Name}");
                    }
                    else if (actionIndex == 2 && p.QueenRage >= 3 && qUnit != null)
                    {
                        var destTile = Board.Values.FirstOrDefault(t => t != qTile && (t.Units.Any(u => u.Definition.Id == "larvae") || (t.Building != null && t.Building.Type == BuildingType.Chrysalis)));
                        if (destTile == null) return false;
                        p.QueenRage -= 3;
                        qTile.Units.Remove(qUnit);
                        destTile.Units.Add(qUnit);
                        Log($"Verdant Voracity! Carnivorous Queen stalked through the canopy to {destTile.Coord}!");
                    }
                    else if (actionIndex == 3 && p.RelicHand.Count > 0)
                    {
                        var chTile = Board.Values.FirstOrDefault(t => t.Building != null && t.Building.Type == BuildingType.Chrysalis) ?? selectedTile;
                        RelicDiscardPile.Add(p.RelicHand[0]);
                        p.RelicHand.RemoveAt(0);
                        PlaceToken(chTile.Coord, TokenType.Egg, p);
                        Log($"{p.DisplayName} used Nesting -> Placed 1 Egg Token at {chTile.Coord}.");
                    }
                    else if (actionIndex == 4 && qUnit == null)
                    {
                        var chTile = Board.Values.FirstOrDefault(t => t.Building != null && t.Building.Type == BuildingType.Chrysalis) ?? selectedTile;
                        SpawnUnit(chTile.Coord, "carnivorous_queen", p, 1);
                        Log($"{p.DisplayName} used Darwinism -> Respawned Carnivorous Queen at {chTile.Coord}!");
                    }
                    else return false;
                    break;

                case FactionId.GlacierKingdom:
                    if (actionIndex == 0)
                    {
                        var gTile = (selectedTile.Overlay == FactionOverlayTileType.Glacier) ? selectedTile :
                                    Board.Values.FirstOrDefault(t => t.Overlay == FactionOverlayTileType.Glacier);
                        if (gTile == null) return false;
                        var sf = gTile.Tokens.FirstOrDefault(t => t.Type == TokenType.Snowfall_Lv1 || t.Type == TokenType.Snowfall_Lv2 || t.Type == TokenType.Snowfall_Lv3);
                        if (sf == null)
                        {
                            PlaceToken(gTile.Coord, TokenType.Snowfall_Lv1, p);
                            Log($"Avalanche (Snowfall): Placed Level-1 Snowfall Token at {gTile.Coord}.");
                        }
                        else if (sf.Type == TokenType.Snowfall_Lv1)
                        {
                            sf.Type = TokenType.Snowfall_Lv2;
                            Log($"Avalanche (Buildup): Upgraded Snowfall Token at {gTile.Coord} to Level 2.");
                        }
                        else if (sf.Type == TokenType.Snowfall_Lv2)
                        {
                            sf.Type = TokenType.Snowfall_Lv3;
                            Log($"Avalanche (Buildup): Upgraded Snowfall Token at {gTile.Coord} to Level 3.");
                        }
                        else
                        {
                            gTile.Tokens.Remove(sf);
                            foreach (var tile in Board.Values.Where(t => HexCoord.Distance(t.Coord, gTile.Coord) <= 2))
                            {
                                foreach (var u in tile.Units.Where(u => u.OwnerPlayerIndex != p.PlayerIndex))
                                    u.IsFrozen = true;
                            }
                            Log($"AVALANCHE CASCADE at {gTile.Coord}! Enemy units within 2 tiles pushed and Frozen!");
                        }
                    }
                    else if (actionIndex == 1 && CanAfford(p, new ResourceCost(stone: 2)))
                    {
                        SpendResources(p, new ResourceCost(stone: 2));
                        selectedTile.HasIcebergWall = true;
                        selectedTile.IcebergWallHP = 2;
                        selectedTile.IcebergWallOwner = p.PlayerIndex;
                        Log($"{p.DisplayName} erected an Iceberg Wall (0/0/2) at {selectedTile.Coord}!");
                    }
                    else if (actionIndex == 2 && CanAfford(p, new ResourceCost(wood: 2)))
                    {
                        var hTile = Board.Values.FirstOrDefault(t => t.Overlay == FactionOverlayTileType.Glacier && t.Building == null) ?? selectedTile;
                        SpendResources(p, new ResourceCost(wood: 2));
                        PlaceBuilding(hTile.Coord, BuildingType.FrozenHarbor, p);
                        Log($"{p.DisplayName} built the Frozen Harbor at {hTile.Coord}!");
                    }
                    else if (actionIndex == 3 && CanAfford(p, new ResourceCost(gold: 2)))
                    {
                        var igTile = Board.Values.FirstOrDefault(t => t.Building != null && (t.Building.Type == BuildingType.Igloo || t.Building.Type == BuildingType.FrozenHarbor)) ?? selectedTile;
                        SpendResources(p, new ResourceCost(gold: 2));
                        SpawnUnit(igTile.Coord, "emperor_penguin", p, 1);
                        Log($"{p.DisplayName} summoned The Emperor Penguin at {igTile.Coord}!");
                    }
                    else if (actionIndex == 4 && p.ResourcePile.Contains(ResourceType.Gold))
                    {
                        RemoveResources(p, ResourceType.Gold, 1);
                        p.GlacierBlizzardActive = true;
                        foreach (var tile in Board.Values)
                        {
                            foreach (var u in tile.Units.Where(x => x.OwnerPlayerIndex != p.PlayerIndex))
                            {
                                int sameFactionCount = tile.Units.Count(x => x.OwnerPlayerIndex == u.OwnerPlayerIndex);
                                bool hasOwnBldg = tile.Building != null && tile.Building.OwnerPlayerIndex == u.OwnerPlayerIndex;
                                if (sameFactionCount < 3 && !hasOwnBldg)
                                    u.IsFrozen = true;
                            }
                        }
                        AwardAuthority(p.PlayerIndex, 1, "Emperor Penguin's Blizzard");
                    }
                    else return false;
                    break;

                case FactionId.TroubleMakingTanukis:
                    if (actionIndex == 0)
                    {
                        var tTile = (selectedTile.Units.Any(u => u.Faction == FactionId.TroubleMakingTanukis) ? selectedTile :
                                     Board.Values.FirstOrDefault(t => t.Units.Any(u => u.Faction == FactionId.TroubleMakingTanukis) && t.Overlay == FactionOverlayTileType.None));
                        if (tTile == null) return false;
                        PlaceOverlayTile(tTile.Coord, FactionOverlayTileType.FallenLeaf_Unflipped, p.PlayerIndex);
                        Log($"{p.DisplayName} used Running Amok -> Placed Fallen Leaves Tile at {tTile.Coord}.");
                    }
                    else if (actionIndex == 1 && p.ResourcePile.Contains(ResourceType.Gold))
                    {
                        var bTile = Board.Values.FirstOrDefault(t => t.Units.Count(u => u.Definition.Id == "tanuki") >= 3);
                        if (bTile == null) return false;
                        RemoveResources(p, ResourceType.Gold, 1);
                        for (int i = 0; i < 3; i++)
                            bTile.Units.Remove(bTile.Units.First(u => u.Definition.Id == "tanuki"));
                        SpawnUnit(bTile.Coord, "band_of_troublemakers", p, 1);
                        Log($"{p.DisplayName} formed A Band of Troublemakers at {bTile.Coord}!");
                    }
                    else if (actionIndex == 2)
                    {
                        int placed = 0;
                        foreach (var cTile in Board.Values.Where(t => t.Tokens.Any(tk => tk.Type == TokenType.Coin) && t.Overlay == FactionOverlayTileType.None))
                        {
                            if (placed >= 2) break;
                            PlaceOverlayTile(cTile.Coord, FactionOverlayTileType.FallenLeaf_Unflipped, p.PlayerIndex);
                            placed++;
                        }
                        Log($"{p.DisplayName} used Belly Drums -> Placed {placed} Fallen Leaves Tiles on Coin hexes!");
                    }
                    else return false;
                    break;

                case FactionId.CapybaraCaravan:
                    if (actionIndex == 0)
                    {
                        bool onRiver = Board.Values.Any(t => t.EffectiveTerrain == TileTerrainType.River && t.Units.Any(u => u.Definition.Id == "capybara"));
                        if (!onRiver) return false;
                        p.ResourcePile.Add(ResourceType.Gold);
                        Log($"{p.DisplayName} used Capybara Forage on River Tile -> Gained 1 Gold!");
                    }
                    else if (actionIndex == 1)
                    {
                        foreach (ResourceType rt in new[] { ResourceType.Wood, ResourceType.Stone })
                        {
                            if (p.ResourcePile.Count(r => r == rt) >= 3)
                            {
                                RemoveResources(p, rt, 3);
                                p.ResourcePile.Add(ResourceType.Gold);
                                p.ResourcePile.Add(ResourceType.Gold);
                                Log($"{p.DisplayName} used Exchange Rate (3 {rt} -> 2 Gold)!");
                                OnStateChanged?.Invoke();
                                return true;
                            }
                        }
                        return false;
                    }
                    else if (actionIndex == 2 && CanAfford(p, new ResourceCost(gold: 1, any: 2)))
                    {
                        int wallows = Board.Values.Count(t => t.Building != null && t.Building.Type == BuildingType.Wallow);
                        if (wallows < 2) { Log("Need at least 2 Wallows on the board to summon The Riverkeeper!"); return false; }
                        var wTile = Board.Values.First(t => t.Building != null && t.Building.Type == BuildingType.Wallow);
                        SpendResources(p, new ResourceCost(gold: 1, any: 2));
                        SpawnUnit(wTile.Coord, "riverkeeper", p, 1);
                        Log($"{p.DisplayName} summoned The Riverkeeper at {wTile.Coord}!");
                    }
                    else if (actionIndex == 3)
                    {
                        bool hasRiverkeeper = Board.Values.SelectMany(t => t.Units).Any(u => u.Definition.Id == "riverkeeper");
                        if (!hasRiverkeeper) return false;
                        AwardAuthority(p.PlayerIndex, 1, "Riverkeeper Hospitality");
                        if (RelicDiscardPile.Count > 0)
                        {
                            p.RelicHand.Add(RelicDiscardPile[RelicDiscardPile.Count - 1]);
                            RelicDiscardPile.RemoveAt(RelicDiscardPile.Count - 1);
                        }
                    }
                    else return false;
                    break;

                case FactionId.BriaredFlower:
                    var briarTile = Board.Values.FirstOrDefault(t => t.Units.Any(u => u.Definition.Id == "blooming_briar"));
                    if (actionIndex == 0 && p.BriaredFlowerThorns >= 1 && p.ResourcePile.Count >= 1 && briarTile != null && p.ThistleBarrageUsesThisTurn < 2)
                    {
                        int bonusRange = Mathf.Min(2, briarTile.Units.Count(u => u.Definition.Id == "thorned_seed"));
                        var targetTile = (HexCoord.Distance(briarTile.Coord, selectedCoord) <= 4 + bonusRange && selectedTile.Units.Any(u => u.OwnerPlayerIndex != p.PlayerIndex))
                            ? selectedTile
                            : Board.Values.FirstOrDefault(t => HexCoord.Distance(briarTile.Coord, t.Coord) <= 4 + bonusRange && t.Units.Any(u => u.OwnerPlayerIndex != p.PlayerIndex));
                        if (targetTile == null) { Log($"No enemy units within Thistle Barrage range ({4 + bonusRange} tiles)."); return false; }

                        int hits = 0;
                        foreach (var enemy in targetTile.Units.Where(u => u.OwnerPlayerIndex != p.PlayerIndex).ToList())
                        {
                            if (p.BriaredFlowerThorns <= 0 || p.ResourcePile.Count <= 0) break;
                            p.BriaredFlowerThorns--;
                            ReturnResourceToDeck(p.ResourcePile, 0);
                            enemy.CurrentHP -= 1;
                            hits++;
                            if (enemy.CurrentHP <= 0) RemoveUnitFromBoard(targetTile, enemy, p.PlayerIndex);
                            var adjForest = targetTile.Coord.GetNeighbors().FirstOrDefault(n => Board.ContainsKey(n) && Board[n].EffectiveTerrain == TileTerrainType.Forest);
                            if (Board.ContainsKey(adjForest)) SpawnUnit(adjForest, "thorned_seed", p, 1);
                        }
                        p.ThistleBarrageUsesThisTurn++;
                        int auth = Mathf.Max(1, hits / 2);
                        AwardAuthority(p.PlayerIndex, auth, $"Thistle Barrage ({hits}HP dealt)");
                    }
                    else if (actionIndex == 1 && p.ResourcePile.Count >= 3)
                    {
                        SpendResources(p, new ResourceCost(any: 3));
                        p.BriaredFlowerThorns = Mathf.Min(6, p.BriaredFlowerThorns + 2);
                        Log($"{p.DisplayName} used Sun Eater -> Regrew +2 Thorns (now {p.BriaredFlowerThorns}/6).");
                    }
                    else if (actionIndex == 2)
                    {
                        int burst = 0;
                        bool killedAny = false;
                        foreach (var sTile in Board.Values.Where(t => t.Units.Any(u => u.Definition.Id == "thorned_seed")).ToList())
                        {
                            if (burst >= 3) break;
                            var seed = sTile.Units.First(u => u.Definition.Id == "thorned_seed");
                            sTile.Units.Remove(seed);
                            burst++;
                            var victim = sTile.Units.FirstOrDefault(u => u.OwnerPlayerIndex != p.PlayerIndex);
                            if (victim != null)
                            {
                                victim.CurrentHP -= 1;
                                if (victim.CurrentHP <= 0) { RemoveUnitFromBoard(sTile, victim, p.PlayerIndex); killedAny = true; }
                            }
                        }
                        if (burst == 0) return false;
                        if (killedAny) AwardAuthority(p.PlayerIndex, 1, "Bursting Into Bloom Kill");
                        Log($"{p.DisplayName} detonated {burst} Thorned Seed(s) via Bursting Into Bloom!");
                    }
                    else if (actionIndex == 3 && p.BriaredFlowerThorns == 0 && briarTile != null)
                    {
                        int dmgDealt = 0;
                        foreach (var tile in Board.Values.Where(t => HexCoord.Distance(t.Coord, briarTile.Coord) <= 3))
                        {
                            foreach (var enemy in tile.Units.Where(u => u.OwnerPlayerIndex != p.PlayerIndex).ToList())
                            {
                                enemy.CurrentHP -= 1;
                                dmgDealt++;
                                if (enemy.CurrentHP <= 0) RemoveUnitFromBoard(tile, enemy, p.PlayerIndex);
                            }
                        }
                        if (dmgDealt >= 2) AwardAuthority(p.PlayerIndex, dmgDealt / 2, "Furious Lashing");
                        Log($"Furious Lashing dealt {dmgDealt}HP total damage within 3 tiles!");
                    }
                    else if (actionIndex == 4 && briarTile == null && p.ResourcePile.Count >= 2)
                    {
                        var ptTile = Board.Values.FirstOrDefault(t => t.Building != null && t.Building.Type == BuildingType.PricklyThicket) ?? selectedTile;
                        SpendResources(p, new ResourceCost(any: 2));
                        p.BriaredFlowerThorns = 2;
                        SpawnUnit(ptTile.Coord, "blooming_briar", p, 1);
                        Log($"{p.DisplayName} used In Half Bloom -> Blooming Briar rose again at {ptTile.Coord} with 2 Thorns!");
                    }
                    else return false;
                    break;
            }

            OnStateChanged?.Invoke();
            return true;
        }

        public bool TryUseFactionSignatureAbility(HexCoord selectedCoord)
        {
            return ExecuteSpecificFactionAction(0, selectedCoord);
        }

        // =====================================================================
        // HELPERS: AUTHORITY, DECKS, SPAWNING
        // =====================================================================

        public void AwardAuthority(int playerIndex, int amount, string reason)
        {
            if (playerIndex < 0 || playerIndex >= Players.Count || amount == 0) return;
            var p = Players[playerIndex];
            p.Authority = Mathf.Max(0, p.Authority + amount);
            Log($"{p.DisplayName} gained +{amount} Authority ({reason}) -> Total: {p.Authority}/{VictoryAuthorityThreshold}.");

            if (p.Authority >= VictoryAuthorityThreshold && WinnerPlayerIndex < 0)
            {
                WinnerPlayerIndex = playerIndex;
                CurrentPhase = TurnPhase.GameOver;
                Log($"VICTORY! {p.DisplayName} ({p.Faction.Name}) reached {p.Authority} Authority and claimed the Continent!");
            }
        }

        public ResourceType DrawResourceCard()
        {
            if (ResourceDeck.Count == 0)
            {
                ResourceDeck = RulebookDatabase.BuildStandard50ResourceDeck();
                Shuffle(ResourceDeck);
            }
            var card = ResourceDeck[ResourceDeck.Count - 1];
            ResourceDeck.RemoveAt(ResourceDeck.Count - 1);
            return card;
        }

        public void ReturnResourceToDeck(List<ResourceType> pile, int index)
        {
            if (index < 0 || index >= pile.Count) return;
            var card = pile[index];
            pile.RemoveAt(index);
            if (card != ResourceType.Charcoal)
            {
                ResourceDeck.Insert(_rng.Next(ResourceDeck.Count + 1), card);
            }
        }

        public void GrantResourceCostToPile(PlayerState p, ResourceCost gain)
        {
            for (int i = 0; i < gain.Wood; i++) p.ResourcePile.Add(ResourceType.Wood);
            for (int i = 0; i < gain.Stone; i++) p.ResourcePile.Add(ResourceType.Stone);
            for (int i = 0; i < gain.Gold; i++) p.ResourcePile.Add(ResourceType.Gold);
            for (int i = 0; i < gain.Charcoal; i++) p.ResourcePile.Add(ResourceType.Charcoal);
        }

        public RelicCardDef DrawRelicCard()
        {
            if (RelicDeck.Count == 0)
            {
                if (RelicDiscardPile.Count == 0) return null;
                RelicDeck.AddRange(RelicDiscardPile);
                RelicDiscardPile.Clear();
                Shuffle(RelicDeck);
                Log("Relic Deck empty -> Shuffled Relic Discard Area back into the Relic Deck.");
            }
            var top = RelicDeck[RelicDeck.Count - 1];
            RelicDeck.RemoveAt(RelicDeck.Count - 1);
            return top;
        }

        public void RefillExcavationArea()
        {
            while (ExcavationArea.Count < ExcavationAreaCapacity)
            {
                var card = DrawRelicCard();
                if (card == null) break;
                ExcavationArea.Add(card);
            }
        }

        public List<UnitInstance> SpawnUnit(HexCoord coord, string unitId, PlayerState owner, int count = 1)
        {
            var list = new List<UnitInstance>();
            if (!Board.TryGetValue(coord, out var tile)) return list;
            var def = owner.Faction.Units.FirstOrDefault(u => u.Id == unitId);
            if (def == null) return list;

            // Enforce Rulebook Part 15 Unit Supply cap
            int onBoardCount = Board.Values.SelectMany(t => t.Units).Count(u => u.OwnerPlayerIndex == owner.PlayerIndex && u.Definition.Id == unitId);
            int availableSupply = Mathf.Max(0, def.SupplyCount - onBoardCount);
            int toSpawn = Mathf.Min(count, availableSupply);

            for (int i = 0; i < toSpawn; i++)
            {
                var inst = new UnitInstance
                {
                    InstanceId = $"{unitId}_{_unitUidCounter++}",
                    OwnerPlayerIndex = owner.PlayerIndex,
                    Faction = owner.Faction.Id,
                    Definition = def,
                    CurrentHP = def.BaseMaxHP,
                    MaxHP = def.BaseMaxHP
                };
                tile.Units.Add(inst);
                list.Add(inst);
            }
            return list;
        }

        public void PlaceBuilding(HexCoord coord, BuildingType type, PlayerState owner)
        {
            if (!Board.TryGetValue(coord, out var tile) || type == BuildingType.None) return;
            tile.Building = new BuildingInstance
            {
                Type = type,
                OwnerPlayerIndex = owner.PlayerIndex,
                Faction = owner.Faction.Id,
                CurrentHP = 2
            };
        }

        public void PlaceToken(HexCoord coord, TokenType type, PlayerState owner)
        {
            if (!Board.TryGetValue(coord, out var tile) || type == TokenType.None) return;
            if (tile.Tokens.Count >= 2) return; // Max 2 tokens per tile (Line 144)
            tile.Tokens.Add(new TokenInstance
            {
                Type = type,
                OwnerPlayerIndex = owner.PlayerIndex,
                Faction = owner.Faction.Id
            });
        }

        public void PlaceOverlayTile(HexCoord coord, FactionOverlayTileType overlay, int ownerPlayerIndex)
        {
            if (!Board.TryGetValue(coord, out var tile)) return;
            if (tile.Overlay != FactionOverlayTileType.None) return; // Cannot stack Unique Faction Tiles (Line 149)

            // Glacier Tile placement pushes enemy pieces to an adjacent non-glacier tile (Line 1187)
            if (overlay == FactionOverlayTileType.Glacier)
            {
                var enemies = tile.Units.Where(u => u.OwnerPlayerIndex != ownerPlayerIndex).ToList();
                if (enemies.Count > 0)
                {
                    var pushTarget = coord.GetNeighbors().FirstOrDefault(n => Board.ContainsKey(n) && Board[n].Overlay != FactionOverlayTileType.Glacier);
                    if (Board.ContainsKey(pushTarget))
                    {
                        foreach (var e in enemies)
                        {
                            tile.Units.Remove(e);
                            Board[pushTarget].Units.Add(e);
                        }
                    }
                }
            }

            tile.Overlay = overlay;
            tile.OverlayOwnerPlayerIndex = ownerPlayerIndex;
        }

        private void Shuffle<T>(List<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = _rng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
    }
}
