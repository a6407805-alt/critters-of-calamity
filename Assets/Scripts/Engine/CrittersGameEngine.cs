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
        public bool IsGuest;
        public bool CannotMoveThisTurn;
        public bool HasMovedThisTurn;
        public int TrapLeafTurnsRemaining; // Trap Leaf immobilizes for rest of turn + next turn (Part 17 Faction 4)
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
    }

    [Serializable]
    public class TokenInstance
    {
        public TokenType Type;
        public int OwnerPlayerIndex;
        public FactionId Faction;
        public int RemainingUses = 2; // Catapult Tokens have 2 uses (Part 17 Faction 2)
    }

    [Serializable]
    public class HexTileState
    {
        public HexCoord Coord;
        public TileTerrainType BaseTerrain;
        public FactionOverlayTileType Overlay = FactionOverlayTileType.None;
        public int OverlayOwnerPlayerIndex = -1;
        public BuildingInstance Building = null; // Max 1 building per tile (Part 4 Line 97)
        public List<TokenInstance> Tokens = new List<TokenInstance>(); // Max 2 tokens per tile (Part 4 Line 97)
        public List<UnitInstance> Units = new List<UnitInstance>();   // Max 6 units per faction per tile (Part 10 Line 175)
        public bool LootedThisTurn = false;
        public bool IsBoardEdge = false;
        public bool HasStatueOfGoddessBuff = false;

        public TileTerrainType EffectiveTerrain
        {
            get
            {
                if (Overlay == FactionOverlayTileType.CapybaraStream)
                    return TileTerrainType.Stream;
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

        // Card zones (Part 1: Hand, Resource Pile, Table)
        public List<ResourceType> ResourcePile = new List<ResourceType>();
        public List<RelicCardDef> RelicHand = new List<RelicCardDef>();
        public List<RelicCardDef> TableRelics = new List<RelicCardDef>();
        public List<FactionSubDeckCardDef> PurchasedTradedGoods = new List<FactionSubDeckCardDef>();

        // Faction 1: Curly Tail's Warband Meters & State
        public int CurlyTailThirstForGlory = 0; // 0-4
        public int CurlyTailHunger = 0;         // 0-4
        public int CurlyTailMomentum = 0;       // 0-4
        public bool CurlyTailWonBattleThisTurn = false;
        public bool CurlyTailSetAsideForRecruit = false;
        public HashSet<string> CraftedGoldenItemIds = new HashSet<string>(); // Only score When Crafted Authority first time

        // Faction 2: Horde of the Millions State
        public bool KingOfTheMillionsSummonedOnce = false;

        // Faction 3: Platypus Papacy Meters & State
        public int PlatypusFaith = 0;           // 0-5
        public int PlatyPopeLevel = 0;          // 0-5
        public int CrusadeTurnsRemaining = 0;
        public bool PlatyPopeOnBoard = true;

        // Faction 4: Trouble Making Tanukis
        public List<bool> FallenLeavesStack = new List<bool>(); // 13 tiles: true = Trap Leaf (5), false = Prank Leaf (8)

        // Faction 5: The Necro-mouser
        public int NecromouserCauldronDeadCount = 0;
        public int NecromouserSpecialSummonsUsed = 0; // Cap 4 per game
        public bool NecromouserAwakened = false;

        // Faction 6: Firestarting Squirrels
        public int SquirrelsCraftedFirestarters = 0;
        public bool PyromaniacSummonedOnce = false;

        // Faction 7: The Capybara Caravan
        public bool TraderUsedPeddlingThisTurn = false;

        // Faction 8: The Briared Flower
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
    /// Full deterministic rules engine implementing Critters of Calamity - Core Rulebook: Eight Faction Edition:
    /// - 108-Tile Hexagonal Gameboard (90 Forest tiles, 14 Stream tiles, 4 Ruin tiles - Part 2 Line 43)
    /// - 4 Players, 30 Authority victory condition with Authority floor 0 & Resource floor 0 (Part 7)
    /// - Stacking limit: max 6 units per faction per tile (ignored during setup - Part 10)
    /// - Reverse turn-order setup + Tanuki Coin Tokens placed last (Part 5 Lines 121-126)
    /// - 40-card Relic Deck + 5-slot Excavation Area + Discard Area
    /// - 50-card Resource Deck (20 Stone, 20 Wood, 10 Gold) with exact search/fallback rules (Part 2 Lines 55-58)
    /// - 8-card Forage Deck (reshuffled after every draw - Part 9) & 8-card Traded Goods Deck + 3-card Stall
    /// - 4-Phase Turn Structure (Draw, Excavation, Main [3 actions], End)
    /// - 5-Phase 2d6 Battle Calculator with Damage Cap & Mandatory Loser Damage
    /// </summary>
    public class CrittersGameEngine
    {
        public const int VictoryAuthorityThreshold = 30;
        public const int ExcavationAreaCapacity = 5;
        public const int MaxUnitsPerFactionPerTile = 6; // Part 10: Stacking Limit

        public List<PlayerState> Players = new List<PlayerState>();
        public Dictionary<HexCoord, HexTileState> Board = new Dictionary<HexCoord, HexTileState>();

        public List<RelicCardDef> RelicDeck = new List<RelicCardDef>();
        public List<RelicCardDef> RelicDiscardPile = new List<RelicCardDef>();
        public List<RelicCardDef> ExcavationArea = new List<RelicCardDef>();

        public List<ResourceType> ResourceDeck = new List<ResourceType>();
        public List<ResourceType> BurnedResources = new List<ResourceType>();

        // Faction Specific Decks (Part 4 Line 111)
        public List<FactionSubDeckCardDef> ForageDeck = new List<FactionSubDeckCardDef>();
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

        public void StartNewMatch(List<FactionId> chosenFactions, List<string> playerNames = null)
        {
            Players.Clear();
            Board.Clear();
            GameLog.Clear();
            WinnerPlayerIndex = -1;
            TurnNumber = 1;

            // 1. Build Exact 108-Tile Hexagonal Continent Map: 90 Forest, 14 Stream, 4 Ruin (Part 2 Line 43)
            GenerateCanonical108TileBoard();

            // 2. Build & Shuffle 40-Card Relic Deck and 50-Card Resource Deck (Part 2)
            RelicDeck = RulebookDatabase.BuildFull40RelicDeck();
            Shuffle(RelicDeck);
            RelicDiscardPile.Clear();
            ExcavationArea.Clear();

            ResourceDeck = RulebookDatabase.BuildStandard50ResourceDeck();
            Shuffle(ResourceDeck);
            BurnedResources.Clear();

            // 3. Build & Shuffle Faction-Specific Decks (Forage 8, Traded Goods 8)
            ForageDeck = RulebookDatabase.BuildMillionsForageDeck8();
            Shuffle(ForageDeck);

            TradedGoodsDeck = RulebookDatabase.BuildCapybaraTradedGoodsDeck8();
            Shuffle(TradedGoodsDeck);
            TradedGoodsStall.Clear();
            TradedGoodsDiscard.Clear();
            RefillTradedGoodsStall();

            // 4. Initialize 4 Players with their chosen Factions (Part 5)
            var allFactions = RulebookDatabase.GetAllFactions();
            for (int i = 0; i < chosenFactions.Count; i++)
            {
                var fDef = allFactions.First(f => f.Id == chosenFactions[i]);
                var p = new PlayerState
                {
                    PlayerIndex = i,
                    DisplayName = (playerNames != null && i < playerNames.Count) ? playerNames[i] : $"Player {i + 1}",
                    Faction = fDef,
                    Authority = 0
                };

                // Initialize Tanukis 13 Fallen Leaves stack (8 Prank = false, 5 Trap = true)
                for (int pr = 0; pr < 8; pr++) p.FallenLeavesStack.Add(false);
                for (int tr = 0; tr < 5; tr++) p.FallenLeavesStack.Add(true);
                Shuffle(p.FallenLeavesStack);

                // Part 5: Draw 2 cards from resource deck into Resource Pile, 1 card from relic deck into Hand
                for (int r = 0; r < 2; r++)
                    p.ResourcePile.Add(DrawRandomResourceCard());
                var startRelic = DrawRelicCard();
                if (startRelic != null)
                    p.RelicHand.Add(startRelic);

                Players.Add(p);
            }

            // Part 5: Place top 5 cards of relic deck faceup in the Excavation Area
            RefillExcavationArea();

            // Part 5: Roll 2d6 for turn order; Setup happens in REVERSE turn order (last player sets up first!)
            foreach (var p in Players)
            {
                p.SetupDiceRoll = Roll2D6(out int d1, out int d2);
                Log($"{p.DisplayName} ({p.Faction.Name}) rolled {d1}+{d2}={p.SetupDiceRoll} for Turn Order.");
            }

            PerformReverseOrderFactionSetups();

            CurrentTurnPlayerIndex = 0;
            BeginTurn(0);
        }

        /// <summary>
        /// Generates the canonical 108-Tile Hexagonal Continent Map specified in Part 2 Line 43:
        /// Exactly 108 tiles: 90 Forest tiles (green), 14 Stream tiles (light blue), and 4 Ruin tiles (gray).
        /// Built from a Radius-6 hex island (127 hexes) with the 6 corner tips trimmed (3 tiles per corner = 18)
        /// and 1 coastal inlet trimmed (127 - 19 = 108 tiles).
        /// </summary>
        private void GenerateCanonical108TileBoard()
        {
            const int radius = 6;
            var excluded = new HashSet<HexCoord>
            {
                // Trim the 6 sharp outer corners (3 tiles each = 18 tiles) + 1 coastal bay = 19 trimmed -> 108 tiles
                new HexCoord(6, 0), new HexCoord(6, -1), new HexCoord(5, 1),
                new HexCoord(-6, 0), new HexCoord(-6, 1), new HexCoord(-5, -1),
                new HexCoord(0, 6), new HexCoord(-1, 6), new HexCoord(1, 5),
                new HexCoord(0, -6), new HexCoord(1, -6), new HexCoord(-1, -5),
                new HexCoord(6, -6), new HexCoord(5, -6), new HexCoord(6, -5),
                new HexCoord(-6, 6), new HexCoord(-5, 6), new HexCoord(-6, 5),
                new HexCoord(0, -5)
            };

            // Exactly 14 Stream tiles winding across the Continent
            var streamCoords = new HashSet<HexCoord>
            {
                new HexCoord(-4, 2), new HexCoord(-3, 2), new HexCoord(-2, 1), new HexCoord(-1, 1),
                new HexCoord(0, 0),  new HexCoord(1, 0),  new HexCoord(2, -1), new HexCoord(3, -1),
                new HexCoord(4, -2), new HexCoord(0, 1),  new HexCoord(0, 2),  new HexCoord(-1, 3),
                new HexCoord(1, -1), new HexCoord(1, -2)
            };

            // Exactly 4 Ruin tiles symmetrically placed in the 4 regions of the Continent
            var ruinCoords = new HashSet<HexCoord>
            {
                new HexCoord(-2, -1),
                new HexCoord(2, 1),
                new HexCoord(-1, 2),
                new HexCoord(1, -3)
            };

            for (int q = -radius; q <= radius; q++)
            {
                int r1 = Mathf.Max(-radius, -q - radius);
                int r2 = Mathf.Min(radius, -q + radius);
                for (int r = r1; r <= r2; r++)
                {
                    var coord = new HexCoord(q, r);
                    if (excluded.Contains(coord)) continue;

                    TileTerrainType terrain = TileTerrainType.Forest;
                    if (streamCoords.Contains(coord))
                        terrain = TileTerrainType.Stream;
                    else if (ruinCoords.Contains(coord))
                        terrain = TileTerrainType.Ruin;

                    Board[coord] = new HexTileState
                    {
                        Coord = coord,
                        BaseTerrain = terrain
                    };
                }
            }

            // Mark board edge tiles (tiles with fewer than 6 neighbors because water around board is not a tile - Part 1 Line 21)
            foreach (var tile in Board.Values)
            {
                tile.IsBoardEdge = tile.Coord.GetNeighbors().Count(n => Board.ContainsKey(n)) < 6;
            }

            Log($"Generated Rulebook Continent Board: {Board.Count} Tiles (" +
                $"{Board.Values.Count(t => t.BaseTerrain == TileTerrainType.Forest)} Forest, " +
                $"{Board.Values.Count(t => t.BaseTerrain == TileTerrainType.Stream)} Stream, " +
                $"{Board.Values.Count(t => t.BaseTerrain == TileTerrainType.Ruin)} Ruin).");
        }

        private void PerformReverseOrderFactionSetups()
        {
            // Well-separated quadrant start hexes (distance 7-8 from each other, satisfying Briared Flower's 4-tile setup rule & Curly Tail's 2-tile rule)
            HexCoord[] spawnAnchors = new HexCoord[]
            {
                new HexCoord(-4, 0),
                new HexCoord(4, 0),
                new HexCoord(0, -4),
                new HexCoord(0, 4)
            };

            // 1. Capybara Caravan Special Setup happens BEFORE any other player sets up (Part 17 Faction 7 Line 810)
            foreach (var p in Players.Where(pl => pl.Faction.Id == FactionId.CapybaraCaravan))
            {
                int placedStreams = 0;
                foreach (var tile in Board.Values)
                {
                    if (tile.BaseTerrain == TileTerrainType.Forest && placedStreams < 8)
                    {
                        bool adjStream = tile.Coord.GetNeighbors().Any(n => Board.ContainsKey(n) && Board[n].EffectiveTerrain == TileTerrainType.Stream);
                        if (adjStream && !spawnAnchors.Contains(tile.Coord))
                        {
                            tile.Overlay = FactionOverlayTileType.CapybaraStream;
                            tile.OverlayOwnerPlayerIndex = p.PlayerIndex;
                            placedStreams++;
                        }
                    }
                }
                Log($"{p.DisplayName} completed Capybara Caravan Special Setup (placed {placedStreams} connected Stream Tiles onto Forest Tiles).");
            }

            // 2. Part 5 Line 121: Setup happens in REVERSE turn order (Player 4 -> Player 3 -> Player 2 -> Player 1),
            // and stacking limit is ignored during setup (Part 5 Line 125).
            for (int i = Players.Count - 1; i >= 0; i--)
            {
                var p = Players[i];
                var homeCoord = spawnAnchors[i % spawnAnchors.Length];
                var homeTile = Board[homeCoord];

                switch (p.Faction.Id)
                {
                    case FactionId.CurlyTail:
                        // Place Curly Tail, 4 Warband units, and 1 War Party on a forest or ruin tile >= 2 tiles from enemy units
                        PlaceBuilding(homeCoord, BuildingType.WarParty, p);
                        SpawnUnit(homeCoord, "curly_tail", p, 1, ignoreStackingLimit: true);
                        SpawnUnit(homeCoord, "warband", p, 4, ignoreStackingLimit: true);
                        break;

                    case FactionId.HordeOfTheMillions:
                        // Place 2 Burrows on 2 separate forest tiles with no enemy units + 2 Millions units on each
                        PlaceBuilding(homeCoord, BuildingType.Burrow, p);
                        SpawnUnit(homeCoord, "millions", p, 2, ignoreStackingLimit: true);
                        var secondBurrow = homeCoord.GetNeighbors().FirstOrDefault(n => Board.ContainsKey(n) && Board[n].EffectiveTerrain == TileTerrainType.Forest && Board[n].Units.Count == 0);
                        if (Board.ContainsKey(secondBurrow))
                        {
                            PlaceBuilding(secondBurrow, BuildingType.Burrow, p);
                            SpawnUnit(secondBurrow, "millions", p, 2, ignoreStackingLimit: true);
                        }
                        break;

                    case FactionId.PlatypusPapacy:
                        // Place the Citadel, 3 Pilgrims, and the Platy-Pope on a forest or stream tile with no enemy units
                        PlaceBuilding(homeCoord, BuildingType.TheCitadel, p);
                        SpawnUnit(homeCoord, "pilgrim", p, 3, ignoreStackingLimit: true);
                        SpawnUnit(homeCoord, "platy_pope", p, 1, ignoreStackingLimit: true);
                        p.PlatyPopeOnBoard = true;
                        break;

                    case FactionId.TroubleMakingTanukis:
                        // Step 1: Place 3 Tanuki units and 1 Shrine on a forest tile with no enemy units (Coins placed AFTER all players!)
                        PlaceBuilding(homeCoord, BuildingType.Shrine, p);
                        SpawnUnit(homeCoord, "tanuki", p, 3, ignoreStackingLimit: true);
                        break;

                    case FactionId.Necromouser:
                        // Place 3 Miasma Tiles facedown (not directly adjacent or on same tile), start with 3 Skeletons on Cauldron
                        p.NecromouserCauldronDeadCount = 3;
                        PlaceOverlayTile(homeCoord, FactionOverlayTileType.Miasma_Hidden, p.PlayerIndex);
                        var m2 = new HexCoord(homeCoord.q / 2, homeCoord.r / 2);
                        var m3 = new HexCoord(-homeCoord.q / 2, -homeCoord.r / 2);
                        if (Board.ContainsKey(m2) && HexCoord.Distance(homeCoord, m2) >= 2)
                            PlaceOverlayTile(m2, FactionOverlayTileType.Miasma_Hidden, p.PlayerIndex);
                        if (Board.ContainsKey(m3) && HexCoord.Distance(homeCoord, m3) >= 2 && HexCoord.Distance(m2, m3) >= 2)
                            PlaceOverlayTile(m3, FactionOverlayTileType.Miasma_Hidden, p.PlayerIndex);
                        break;

                    case FactionId.FirestartingSquirrels:
                        // Place 1 Squirrel Unit each on 3 adjacent forest tiles without enemy pieces, 1 Bunker on one, 2 Charcoal, 1 Acorn Firestarter
                        PlaceBuilding(homeCoord, BuildingType.Bunker, p);
                        SpawnUnit(homeCoord, "squirrel", p, 1, ignoreStackingLimit: true);
                        int adjPlaced = 0;
                        foreach (var n in homeCoord.GetNeighbors())
                        {
                            if (Board.ContainsKey(n) && Board[n].EffectiveTerrain == TileTerrainType.Forest && Board[n].Units.Count == 0 && adjPlaced < 2)
                            {
                                SpawnUnit(n, "squirrel", p, 1, ignoreStackingLimit: true);
                                adjPlaced++;
                            }
                        }
                        p.ResourcePile.Add(ResourceType.Charcoal);
                        p.ResourcePile.Add(ResourceType.Charcoal);
                        p.SquirrelsCraftedFirestarters = 1;
                        break;

                    case FactionId.CapybaraCaravan:
                        // Place 3 Capybara units, the Capybara Trader, and 1 Wallow on a stream tile
                        homeTile.Overlay = FactionOverlayTileType.CapybaraStream;
                        homeTile.OverlayOwnerPlayerIndex = p.PlayerIndex;
                        PlaceBuilding(homeCoord, BuildingType.Wallow, p);
                        SpawnUnit(homeCoord, "capybara", p, 3, ignoreStackingLimit: true);
                        SpawnUnit(homeCoord, "capybara_trader", p, 1, ignoreStackingLimit: true);
                        break;

                    case FactionId.BriaredFlower:
                        // Place Blooming Briar with 6 Thorns, 2 Thorned Seeds, and 1 Prickly Thicket on a forest tile with no enemy pieces within 4 tiles
                        p.BriaredFlowerThorns = 6;
                        PlaceBuilding(homeCoord, BuildingType.PricklyThicket, p);
                        SpawnUnit(homeCoord, "blooming_briar", p, 1, ignoreStackingLimit: true);
                        SpawnUnit(homeCoord, "thorned_seed", p, 2, ignoreStackingLimit: true);
                        break;
                }

                Log($"[Reverse Setup] {p.DisplayName} completed {p.Faction.Name} setup at hex {homeCoord}.");
            }

            // 3. Part 5 Line 126 & Part 17 Faction 4 Line 603:
            // After all other players have finished their setup entirely, the Tanuki player places 12 Coin Tokens on 12 different forest tiles with no enemy units (>= 2 tiles apart).
            foreach (var tanukiPlayer in Players.Where(pl => pl.Faction.Id == FactionId.TroubleMakingTanukis))
            {
                var coinCoords = new List<HexCoord>();
                foreach (var tile in Board.Values)
                {
                    if (coinCoords.Count >= 12) break;
                    bool hasEnemyUnits = tile.Units.Any(u => u.OwnerPlayerIndex != tanukiPlayer.PlayerIndex);
                    bool hasShrine = tile.Building != null && tile.Building.Type == BuildingType.Shrine;
                    if (tile.EffectiveTerrain == TileTerrainType.Forest && !hasEnemyUnits && !hasShrine)
                    {
                        if (coinCoords.All(c => HexCoord.Distance(c, tile.Coord) >= 2))
                        {
                            PlaceToken(tile.Coord, TokenType.Coin, tanukiPlayer);
                            coinCoords.Add(tile.Coord);
                        }
                    }
                }
                Log($"{tanukiPlayer.DisplayName} placed {coinCoords.Count} Coin Tokens across Forest tiles after all players completed setup.");
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
            p.TraderUsedPeddlingThisTurn = false;

            foreach (var tile in Board.Values)
            {
                tile.LootedThisTurn = false;
                foreach (var u in tile.Units)
                {
                    if (u.OwnerPlayerIndex == playerIndex)
                    {
                        if (u.TrapLeafTurnsRemaining > 0)
                        {
                            u.TrapLeafTurnsRemaining--;
                            u.CannotMoveThisTurn = true;
                        }
                        else
                        {
                            u.CannotMoveThisTurn = false;
                        }
                        u.HasMovedThisTurn = false;
                    }
                }
            }

            // 1. Draw Phase (Part 6 Line 130)
            CurrentPhase = TurnPhase.DrawPhase;

            // Platypus Papacy Prophetic Vision (Part 17 Faction 3 Line 542): Look at top 3 Relic cards, add 1 to Hand, put 2 on bottom
            if (p.Faction.Id == FactionId.PlatypusPapacy && RelicDeck.Count >= 3)
            {
                var c1 = DrawRelicCard();
                var c2 = DrawRelicCard();
                var c3 = DrawRelicCard();
                p.RelicHand.Add(c1);
                RelicDeck.Insert(0, c2);
                RelicDeck.Insert(0, c3);
                Log($"{p.DisplayName} used Prophetic Vision -> Added '{c1.Name}' to Hand and placed 2 cards on the bottom of the Relic Deck.");
            }

            var drawn = DrawRelicCard();
            if (drawn != null)
            {
                p.RelicHand.Add(drawn);
                Log($"{p.DisplayName} began Turn {TurnNumber} (1. Draw Phase) and drew '{drawn.Name}'.");
            }

            // Trouble Making Tanukis Draw Phase: Rustling in the Trees (Line 619)
            if (p.Faction.Id == FactionId.TroubleMakingTanukis)
            {
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

            // Transition into 2. Excavation Phase
            EnterExcavationPhase(p);
            OnStateChanged?.Invoke();
        }

        private void EnterExcavationPhase(PlayerState p)
        {
            CurrentPhase = TurnPhase.ExcavationPhase;

            // Platypus Papacy: Platy-Pope Return at the start of Excavation Phase (Part 17 Faction 3 Line 561)
            if (p.Faction.Id == FactionId.PlatypusPapacy)
            {
                bool popePresent = Board.Values.SelectMany(t => t.Units).Any(u => u.OwnerPlayerIndex == p.PlayerIndex && u.Definition.Id == "platy_pope");
                var citadelTile = Board.Values.FirstOrDefault(t => t.Building != null && t.Building.Type == BuildingType.TheCitadel && t.Building.OwnerPlayerIndex == p.PlayerIndex);
                if (!popePresent && citadelTile != null)
                {
                    SpawnUnit(citadelTile.Coord, "platy_pope", p, 1);
                    p.PlatyPopeOnBoard = true;
                    p.PlatyPopeLevel = p.PlatyPopeLevel == 0 ? 0 : 1;
                    p.PlatypusFaith = 0;
                    Log($"Platy-Pope Return! The Platy-Pope returned at The Citadel ({citadelTile.Coord}) at Level {p.PlatyPopeLevel} with 0 Faith.");
                }
            }
        }

        public void AdvancePhase()
        {
            if (CurrentPhase == TurnPhase.GameOver) return;

            var p = Players[CurrentTurnPlayerIndex];
            if (CurrentPhase == TurnPhase.DrawPhase)
            {
                EnterExcavationPhase(p);
            }
            else if (CurrentPhase == TurnPhase.ExcavationPhase)
            {
                CurrentPhase = TurnPhase.MainPhase;
                MainPhaseActionsRemaining = 3;

                // Horde of the Millions: Prolific Creatures triggers at start of the NEXT player's Main Phase (Part 17 Faction 2 Line 484)
                int prevPlayerIndex = (CurrentTurnPlayerIndex - 1 + Players.Count) % Players.Count;
                var prevPlayer = Players[prevPlayerIndex];
                if (prevPlayer.Faction.Id == FactionId.HordeOfTheMillions)
                {
                    foreach (var tile in Board.Values)
                    {
                        int mCount = tile.Units.Count(u => u.Faction == FactionId.HordeOfTheMillions);
                        bool hasEnemy = tile.Units.Any(u => u.Faction != FactionId.HordeOfTheMillions) ||
                                        (tile.Building != null && tile.Building.Faction != FactionId.HordeOfTheMillions) ||
                                        tile.Tokens.Any(tk => tk.Faction != FactionId.HordeOfTheMillions);
                        if (mCount >= 1 && mCount < 3 && hasEnemy)
                        {
                            SpawnUnit(tile.Coord, "millions", prevPlayer, 1);
                            Log($"Prolific Creatures spawned 1 Millions at {tile.Coord}.");
                        }
                    }
                }

                Log($"{p.DisplayName} entered 3. Main Phase (3 Actions remaining).");
            }
            else if (CurrentPhase == TurnPhase.MainPhase)
            {
                // Curly Tail's meters resolve at the END OF YOUR MAIN PHASE in exact order (Part 17 Faction 1 Line 394):
                // 1. Raise Thirst for Glory by 1. 2. Pay current Hunger level effect. 3. Raise Hunger by 1.
                if (p.Faction.Id == FactionId.CurlyTail)
                {
                    ResolveCurlyTailEndOfMainPhaseMeters(p);
                }

                ExecuteEndPhase();
            }

            OnStateChanged?.Invoke();
        }

        private void ResolveCurlyTailEndOfMainPhaseMeters(PlayerState p)
        {
            // 1. Raise Thirst for Glory by 1
            p.CurlyTailThirstForGlory = Mathf.Min(4, p.CurlyTailThirstForGlory + 1);

            // 2. Pay the effect of your current Hunger level
            bool paidFully = true;
            if (p.CurlyTailHunger == 0)
            {
                p.ResourcePile.Add(DrawRandomResourceCard());
                Log($"{p.DisplayName} resolved Hunger Level 0 -> Drew 1 Resource card.");
            }
            else
            {
                int resNeeded = (p.CurlyTailHunger == 1) ? 1 : 2;
                if (p.ResourcePile.Count < resNeeded) paidFully = false;
                int toDiscard = Mathf.Min(resNeeded, p.ResourcePile.Count);
                for (int i = 0; i < toDiscard; i++)
                    ReturnResourceToDeck(p.ResourcePile, 0);

                if (p.CurlyTailHunger == 4)
                {
                    if (p.RelicHand.Count < 1) paidFully = false;
                    else
                    {
                        RelicDiscardPile.Add(p.RelicHand[0]);
                        p.RelicHand.RemoveAt(0);
                    }
                }
            }

            if (!paidFully)
            {
                p.CurlyTailMomentum = Mathf.Max(0, p.CurlyTailMomentum - 1);
                p.CurlyTailThirstForGlory = Mathf.Max(0, p.CurlyTailThirstForGlory - 1);
                Log($"{p.DisplayName} could not fully pay Hunger Level {p.CurlyTailHunger} -> Lost 1 Momentum and 1 Thirst for Glory.");
            }

            // 3. Raise Hunger by 1
            p.CurlyTailHunger = Mathf.Min(4, p.CurlyTailHunger + 1);
        }

        private void ExecuteEndPhase()
        {
            CurrentPhase = TurnPhase.EndPhase;
            var p = Players[CurrentTurnPlayerIndex];
            Log($"{p.DisplayName} entered 4. End Phase.");

            // Step 1: Resolve "start of your end phase" effects (Part 6 Line 152)
            if (p.Faction.Id == FactionId.HordeOfTheMillions)
            {
                // Burrow Effect (start of your End phase): gain 1 Authority for every 2 Burrows on the board
                int burrows = Board.Values.Count(t => t.Building != null && t.Building.Type == BuildingType.Burrow && t.Building.OwnerPlayerIndex == p.PlayerIndex);
                if (burrows >= 2)
                    AwardAuthority(p.PlayerIndex, burrows / 2, "Burrow Effect (Start of End Phase)");
            }

            // Step 2: Resolve other "during your end phase" effects (Part 6 Line 153)
            switch (p.Faction.Id)
            {
                case FactionId.CurlyTail:
                    // Momentum Level 0: during your End phase, draw 1 card from the resource deck (Line 420)
                    p.ResourcePile.Add(DrawRandomResourceCard());
                    // Momentum Level 2: gain 1 Authority if you won a battle this turn (Line 422)
                    if (p.CurlyTailMomentum >= 2 && p.CurlyTailWonBattleThisTurn)
                        AwardAuthority(p.PlayerIndex, 1, "Momentum Level 2+");
                    break;

                case FactionId.HordeOfTheMillions:
                    // Burrow Upkeep: discard 1 Wood for every 2 Burrows; remove 2 Burrows for each unpaid Wood (Line 481)
                    var burrowTiles = Board.Values.Where(t => t.Building != null && t.Building.Type == BuildingType.Burrow && t.Building.OwnerPlayerIndex == p.PlayerIndex).ToList();
                    int woodRequired = burrowTiles.Count / 2;
                    for (int w = 0; w < woodRequired; w++)
                    {
                        if (p.ResourcePile.Contains(ResourceType.Wood))
                        {
                            RemoveResources(p, ResourceType.Wood, 1);
                        }
                        else
                        {
                            for (int rem = 0; rem < 2 && burrowTiles.Count > 0; rem++)
                            {
                                burrowTiles[0].Building = null;
                                burrowTiles.RemoveAt(0);
                            }
                            Log($"{p.DisplayName} could not pay 1 Wood Burrow Upkeep -> Removed 2 Burrows.");
                        }
                    }
                    // Out of Hibernation: at each Burrow tile with < 3 Millions, place 1 Millions (Line 483)
                    foreach (var bt in Board.Values.Where(t => t.Building != null && t.Building.Type == BuildingType.Burrow && t.Building.OwnerPlayerIndex == p.PlayerIndex))
                    {
                        if (bt.Units.Count(u => u.OwnerPlayerIndex == p.PlayerIndex) < 3)
                            SpawnUnit(bt.Coord, "millions", p, 1);
                    }
                    // On the Search for Food: +1 Authority per 3 Millions on tiles with enemy pieces (max 2/turn) (Line 485)
                    int foodAuth = 0;
                    foreach (var tile in Board.Values)
                    {
                        int mHere = tile.Units.Count(u => u.OwnerPlayerIndex == p.PlayerIndex);
                        bool enemyHere = tile.Units.Any(u => u.OwnerPlayerIndex != p.PlayerIndex) ||
                                         (tile.Building != null && tile.Building.OwnerPlayerIndex != p.PlayerIndex) ||
                                         tile.Tokens.Any(tk => tk.OwnerPlayerIndex != p.PlayerIndex);
                        if (enemyHere && mHere >= 3)
                            foodAuth += (mHere / 3);
                    }
                    if (foodAuth > 0)
                        AwardAuthority(p.PlayerIndex, Mathf.Min(2, foodAuth), "On the Search for Food");
                    break;

                case FactionId.PlatypusPapacy:
                    int holyRelics = Board.Values.Sum(t => t.Tokens.Count(tk => tk.Type == TokenType.HolyRelic && tk.OwnerPlayerIndex == p.PlayerIndex));
                    p.PlatypusFaith = Mathf.Min(5, p.PlatypusFaith + holyRelics);
                    if (p.CrusadeTurnsRemaining > 0) p.CrusadeTurnsRemaining--;
                    break;

                case FactionId.Necromouser:
                    // Undead Shackles: +1 Authority per 2 Dead in Cauldron (cap 2)
                    int shackleAuth = Mathf.Min(2, p.NecromouserCauldronDeadCount / 2);
                    if (shackleAuth > 0) AwardAuthority(p.PlayerIndex, shackleAuth, "Undead Shackles");
                    break;

                case FactionId.FirestartingSquirrels:
                    ResolveSquirrelsFireTilesEndPhase(p);
                    // Forest Fire: +1 Authority for every Fire Tile adjacent to another faction's pieces (cap 2)
                    int fireScore = 0;
                    foreach (var ft in Board.Values.Where(t => t.Overlay == FactionOverlayTileType.Fire))
                    {
                        bool adjEnemy = ft.Coord.GetNeighbors().Any(n => Board.ContainsKey(n) &&
                            (Board[n].Units.Any(u => u.OwnerPlayerIndex != p.PlayerIndex) ||
                             (Board[n].Building != null && Board[n].Building.OwnerPlayerIndex != p.PlayerIndex)));
                        if (adjEnemy) fireScore++;
                    }
                    if (fireScore > 0) AwardAuthority(p.PlayerIndex, Mathf.Min(2, fireScore), "Forest Fire");
                    break;

                case FactionId.CapybaraCaravan:
                    int guestOwners = Players.Count(pl => pl.PlayerIndex != p.PlayerIndex && Board.Values.SelectMany(t => t.Units).Any(u => u.OwnerPlayerIndex == pl.PlayerIndex && u.IsGuest));
                    if (guestOwners > 0) AwardAuthority(p.PlayerIndex, Mathf.Min(2, guestOwners), "Customary Greeting");
                    break;

                case FactionId.BriaredFlower:
                    // Pollination: 4+ different tiles with Thorned Seeds -> +1 Auth (7+ tiles -> +2 Auth)
                    int seedTiles = Board.Values.Count(t => t.Units.Any(u => u.Definition.Id == "thorned_seed"));
                    if (seedTiles >= 7) AwardAuthority(p.PlayerIndex, 2, "Pollination (7+ tiles)");
                    else if (seedTiles >= 4) AwardAuthority(p.PlayerIndex, 1, "Pollination (4+ tiles)");
                    break;
            }

            // Horde of the Millions Forage Trait (Part 17 Faction 2 Line 489):
            // During each OTHER player's End phase, if that player has 1+ units on a tile with a Millions unit, they draw at most 1 Forage card. The Horde player never draws Forage cards.
            if (p.Faction.Id != FactionId.HordeOfTheMillions)
            {
                bool sharesTileWithMillions = Board.Values.Any(t =>
                    t.Units.Any(u => u.OwnerPlayerIndex == p.PlayerIndex) &&
                    t.Units.Any(u => u.Faction == FactionId.HordeOfTheMillions));
                if (sharesTileWithMillions)
                {
                    DrawAndResolveForageCard(p);
                }
            }

            // Capybara Caravan Watering Hole Guest draw (Line 823) & Stall refill (Line 840)
            bool controlsGuest = Board.Values.SelectMany(t => t.Units).Any(u => u.OwnerPlayerIndex == p.PlayerIndex && u.IsGuest);
            if (controlsGuest)
            {
                var guestRes = DrawRandomResourceCard();
                p.ResourcePile.Add(guestRes);
                Log($"{p.DisplayName} controls a Guest unit -> Drew 1 Resource ({guestRes}) from Watering Hole.");
            }
            RefillTradedGoodsStall();

            if (CurrentPhase == TurnPhase.GameOver) return;

            // Step 3: Discard down to 5 cards in Hand and 5 cards in Resource Pile (Necro-mouser Hoarder keeps 8) (Part 6 Line 154)
            int maxResources = p.Faction.Id == FactionId.Necromouser ? 8 : 5;
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

            // Step 4: Refill the Excavation Area to 5 faceup cards (Part 6 Line 155)
            RefillExcavationArea();

            // Advance to next player clockwise
            int nextPlayer = (CurrentTurnPlayerIndex + 1) % Players.Count;
            if (nextPlayer == 0) TurnNumber++;
            BeginTurn(nextPlayer);
        }

        private void ResolveSquirrelsFireTilesEndPhase(PlayerState squirrelPlayer)
        {
            // Part 17 Faction 6 Lines 767-777: 5-step Fire Tile End Phase Resolution
            int totalEnemyDamageDealt = 0;
            int totalEnemiesDrivenOut = 0;
            var damagedThisEndPhase = new HashSet<string>();
            var drivenOutThisEndPhase = new HashSet<string>();
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

                // 2. Burn structures (roll 2d6; on doubles remove every building/token on every Fire Tile except Tar Pit)
                if (ft.Building != null && ft.Building.Type != BuildingType.Bunker)
                    ft.Building = null;
                Roll2D6(out int d1, out int d2);
                if (d1 == d2)
                {
                    foreach (var allFt in Board.Values.Where(t => t.Overlay == FactionOverlayTileType.Fire))
                    {
                        if (allFt.Building != null && allFt.Building.Type != BuildingType.Bunker)
                            allFt.Building = null;
                        allFt.Tokens.RemoveAll(tk => tk.Type != TokenType.TarPit);
                    }
                }

                // 3. Drive out enemies
                var enemies = ft.Units.Where(u => u.OwnerPlayerIndex != squirrelPlayer.PlayerIndex).ToList();
                HexCoord? lastDrivenForest = null;
                foreach (var enemy in enemies)
                {
                    if (drivenOutThisEndPhase.Contains(enemy.InstanceId)) continue;
                    var adjCoord = ft.Coord.GetNeighbors().FirstOrDefault(n => Board.ContainsKey(n) && CanAddUnitToTile(Board[n], enemy.OwnerPlayerIndex));
                    if (Board.ContainsKey(adjCoord))
                    {
                        ft.Units.Remove(enemy);
                        var destTile = Board[adjCoord];
                        destTile.Units.Add(enemy);
                        drivenOutThisEndPhase.Add(enemy.InstanceId);
                        totalEnemiesDrivenOut++;

                        if (destTile.EffectiveTerrain == TileTerrainType.Forest)
                        {
                            lastDrivenForest = adjCoord;
                            int dmg = 0;
                            if (!damagedThisEndPhase.Contains(enemy.InstanceId))
                            {
                                dmg += 1;
                                damagedThisEndPhase.Add(enemy.InstanceId);
                            }
                            if (ft.Tokens.Any(tk => tk.Type == TokenType.TarPit))
                                dmg += 1; // Tar Pit extra 1HP bypasses end phase damage cap (Line 761)

                            if (dmg > 0)
                            {
                                enemy.CurrentHP -= dmg;
                                totalEnemyDamageDealt += dmg;
                                if (enemy.CurrentHP <= 0)
                                    RemoveUnitFromBoard(destTile, enemy, squirrelPlayer.PlayerIndex);
                            }
                        }
                    }
                }

                // 4. Spread: Roll 1d6; on 1 or 6 place a new Fire Tile on a forest tile holding a driven-out enemy
                int spreadRoll = RollD6();
                if ((spreadRoll == 1 || spreadRoll == 6) && lastDrivenForest.HasValue)
                {
                    var spreadTile = Board[lastDrivenForest.Value];
                    if (spreadTile.Overlay == FactionOverlayTileType.None)
                    {
                        spreadTile.Overlay = FactionOverlayTileType.Fire;
                        spreadTile.OverlayOwnerPlayerIndex = squirrelPlayer.PlayerIndex;
                        Log($"Fire Tile Spread (rolled {spreadRoll}) to {spreadTile.Coord}!");
                    }
                }

                // 5. Burn the Deck
                if (ResourceDeck.Count > 0)
                {
                    var burned = DrawRandomResourceCard();
                    BurnedResources.Add(burned);
                    if (burned == ResourceType.Gold)
                        AwardAuthority(squirrelPlayer.PlayerIndex, 1, "Burned Gold from Resource Deck");
                }
            }

            // Flames of War scoring: +1 Auth per 2HP dealt, +1 Auth per 2 enemies moved (max 4)
            int flamesAuth = Mathf.Min(4, (totalEnemyDamageDealt / 2) + (totalEnemiesDrivenOut / 2));
            if (flamesAuth > 0)
                AwardAuthority(squirrelPlayer.PlayerIndex, flamesAuth, "Flames of War");
        }

        // =====================================================================
        // FACTION SUB-DECK RESOLVERS (Forage Deck & Traded Goods Deck)
        // =====================================================================

        public void DrawAndResolveForageCard(PlayerState drawingPlayer)
        {
            // Part 9 Line 173 & Part 17 Faction 2 Line 493:
            // "After a Forage card is used, it goes back into the Forage deck and the deck is shuffled."
            if (ForageDeck.Count == 0)
                ForageDeck = RulebookDatabase.BuildMillionsForageDeck8();

            Shuffle(ForageDeck);
            var card = ForageDeck[0];

            var millionsPlayer = Players.FirstOrDefault(pl => pl.Faction.Id == FactionId.HordeOfTheMillions);
            Log($"{drawingPlayer.DisplayName} drew Forage Card '{card.Title}' (shuffled back into Forage Deck): {card.EffectText}");

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
                        GainSpecificResourceFromDeck(millionsPlayer, ResourceType.Stone);
                        GainSpecificResourceFromDeck(millionsPlayer, ResourceType.Stone);
                    }
                    break;
                case "rummage_wood_1":
                case "rummage_wood_2":
                    if (millionsPlayer != null)
                    {
                        GainSpecificResourceFromDeck(millionsPlayer, ResourceType.Wood);
                        GainSpecificResourceFromDeck(millionsPlayer, ResourceType.Wood);
                    }
                    break;
                case "rummage_gold":
                    if (millionsPlayer != null)
                        GainSpecificResourceFromDeck(millionsPlayer, ResourceType.Gold);
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

            for (int i = 0; i < card.Cost.AnyResource && buyer.ResourcePile.Count > 0; i++)
            {
                capyPlayer.ResourcePile.Add(buyer.ResourcePile[0]);
                buyer.ResourcePile.RemoveAt(0);
            }

            TradedGoodsStall.RemoveAt(stallIndex);
            buyer.PurchasedTradedGoods.Add(card);

            // Both buyer and Capybara player gain 1 Authority, and buyer places 1 basic unit as a Guest at a Wallow (Line 840)
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

        // =====================================================================
        // 2. EXCAVATION PHASE ACTIONS (Unearth, Craft, Assemble, Disassemble)
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
            OnStateChanged?.Invoke();
            return true;
        }

        public bool TryAssembleRelicFromHand(int handIndex)
        {
            if (CurrentPhase != TurnPhase.ExcavationPhase || UsedAssembleThisTurn) return false;
            var p = Players[CurrentTurnPlayerIndex];
            if (handIndex < 0 || handIndex >= p.RelicHand.Count) return false;

            var card = p.RelicHand[handIndex];
            if (!card.HasCraftedEffect || !CanAfford(p, card.Cost)) return false;

            SpendResources(p, card.Cost);
            p.RelicHand.RemoveAt(handIndex);
            UsedAssembleThisTurn = true;

            ResolveCraftedRelic(p, card, "Assembled");
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
            if (CurrentPhase != TurnPhase.ExcavationPhase) return false;
            if (!Board.TryGetValue(targetCoord, out var tile)) return false;
            var p = Players[CurrentTurnPlayerIndex];

            if (p.Faction.PrimaryBuilding == BuildingType.None) return false;

            // Comeback Mechanic (Part 8 Line 168): At start of Excavation phase, if no units and no buildings on board, free craft recruitment building + 2 basic units!
            bool hasAnyUnitsOrBuildings = Board.Values.Any(t =>
                t.Units.Any(u => u.OwnerPlayerIndex == p.PlayerIndex) ||
                (t.Building != null && t.Building.OwnerPlayerIndex == p.PlayerIndex));
            bool isComebackFreeCraft = !hasAnyUnitsOrBuildings && p.Faction.Id != FactionId.PlatypusPapacy;

            if (!isComebackFreeCraft && UsedFactionCraftThisTurn) return false;
            if (tile.Building != null) return false;

            // Part 4 Line 95: Buildings cannot be placed on stream tiles. Exception: the Citadel (and Wallow per Faction 7 Line 817).
            if (tile.EffectiveTerrain == TileTerrainType.Stream &&
                p.Faction.PrimaryBuilding != BuildingType.TheCitadel &&
                p.Faction.PrimaryBuilding != BuildingType.Wallow)
                return false;

            if (!CanAfford(p, p.Faction.PrimaryBuildingCost)) return false;

            SpendResources(p, p.Faction.PrimaryBuildingCost);
            PlaceBuilding(targetCoord, p.Faction.PrimaryBuilding, p);

            if (isComebackFreeCraft)
            {
                var basic = p.Faction.Units.FirstOrDefault(u => !u.IsSpecialUnit);
                if (basic != null) SpawnUnit(targetCoord, basic.Id, p, 2);
                Log($"{p.DisplayName} used the Comeback Mechanic (free craft) at {targetCoord} -> Placed {p.Faction.PrimaryBuilding} and 2 basic units!");
            }
            else
            {
                UsedFactionCraftThisTurn = true;
                // Platypus Papacy Rebuilding The Citadel also places 1 Pilgrim and 1 Crusader (Line 539)
                if (p.Faction.Id == FactionId.PlatypusPapacy)
                {
                    SpawnUnit(targetCoord, "pilgrim", p, 1);
                    SpawnUnit(targetCoord, "crusader", p, 1);
                }
                Log($"{p.DisplayName} crafted {p.Faction.PrimaryBuilding} at {targetCoord}.");
            }

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

            // Part 17 Faction 1 Line 430: Only gain a Golden item's When Crafted Authority the first time you craft it in a game
            if (eq.AuthorityOnCraft > 0 && !p.CraftedGoldenItemIds.Contains(eq.Id))
            {
                p.CraftedGoldenItemIds.Add(eq.Id);
                AwardAuthority(p.PlayerIndex, eq.AuthorityOnCraft, $"Forged {eq.Name}");
            }

            // Necro-mouser Plague trait: spending Gold on equipment places 1 Miasma Tile adjacent to an existing Miasma Tile
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
            if (card.KeepOnTableWhenCrafted)
            {
                p.TableRelics.Add(card);
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
                case "statue_of_the_goddess":
                    var myTile = Board.Values.FirstOrDefault(t => t.Units.Any(u => u.OwnerPlayerIndex == p.PlayerIndex));
                    if (myTile != null)
                    {
                        myTile.HasStatueOfGoddessBuff = true;
                        Log($"Statue of the Goddess blessed units at {myTile.Coord} (+1 Attack in their next battle).");
                    }
                    break;
                case "bejeweled_chalice":
                    GainSpecificResourceFromDeck(p, ResourceType.Gold);
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
                        if (p.RelicHand.Count > 0)
                        {
                            RelicDiscardPile.Add(p.RelicHand[0]);
                            p.RelicHand.RemoveAt(0);
                        }
                        p.ResourcePile.Add(DrawRandomResourceCard());
                        p.ResourcePile.Add(DrawRandomResourceCard());
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
                        Log($"{p.DisplayName} took {stealCount} random resource(s) from {target.DisplayName} via Underground Passage.");
                    }
                    break;
                case "hallow_ground":
                    var enemyTile = Board.Values.FirstOrDefault(t => t.Units.Any(u => u.OwnerPlayerIndex != p.PlayerIndex && !u.Definition.IsSpecialUnit));
                    if (enemyTile != null)
                    {
                        var victim = enemyTile.Units.First(u => u.OwnerPlayerIndex != p.PlayerIndex && !u.Definition.IsSpecialUnit);
                        enemyTile.Units.Remove(victim);
                        Log($"{p.DisplayName} used Hallowed Ground to remove {victim.Definition.Name} at {enemyTile.Coord}.");
                    }
                    break;
            }

            RelicDiscardPile.Add(card);
        }

        // =====================================================================
        // 3. MAIN PHASE ACTIONS (Move, Battle, Loot, Recruit)
        // =====================================================================

        public bool CanAddUnitToTile(HexTileState tile, int ownerPlayerIndex)
        {
            // Part 10 Line 175: Each tile can hold at most 6 units from the same faction.
            return tile.Units.Count(u => u.OwnerPlayerIndex == ownerPlayerIndex) < MaxUnitsPerFactionPerTile;
        }

        public bool TryMoveUnits(HexCoord fromCoord, HexCoord toCoord)
        {
            if (CurrentPhase != TurnPhase.MainPhase || MainPhaseActionsRemaining <= 0) return false;
            if (!Board.TryGetValue(fromCoord, out var fromTile) || !Board.TryGetValue(toCoord, out var toTile)) return false;

            var p = Players[CurrentTurnPlayerIndex];
            var movableUnits = fromTile.Units.Where(u =>
                u.OwnerPlayerIndex == p.PlayerIndex &&
                !u.CannotMoveThisTurn &&
                u.Definition.Id != "platy_pope" && // Platy-Pope is Unyielding (cannot move)
                !(u.Definition.Id == "blooming_briar" && u.HasMovedThisTurn) // Rooted in Place: 1 tile per turn
            ).ToList();

            if (movableUnits.Count == 0) return false;

            // Part 10 Line 177: Stacking Limit (cannot move units onto a tile if it would put more than 6 of your units there)
            int existingFriendly = toTile.Units.Count(u => u.OwnerPlayerIndex == p.PlayerIndex);
            int maxMovable = MaxUnitsPerFactionPerTile - existingFriendly;
            if (maxMovable <= 0)
            {
                Log($"Stacking Limit (Part 10): Cannot move onto {toCoord} because you already have 6 units there!");
                return false;
            }
            if (movableUnits.Count > maxMovable)
                movableUnits = movableUnits.Take(maxMovable).ToList();

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

            // Gluttony Token check (Part 17 Faction 2 Line 491): blocks normal and Supply Line movement while a Millions unit is on tile
            if (fromTile.Tokens.Any(t => t.Type == TokenType.Gluttony) &&
                fromTile.Units.Any(u => u.Faction == FactionId.HordeOfTheMillions) &&
                p.Faction.Id != FactionId.HordeOfTheMillions)
            {
                Log("Gluttony Token debuff: Cannot use a Move action to leave this tile while a Millions unit is present!");
                return false;
            }

            int dist = HexCoord.Distance(fromCoord, toCoord);
            int maxDist = 1;
            // Capybara Semiaquatic: 2 Stream Tiles
            if (p.Faction.Id == FactionId.CapybaraCaravan && fromTile.EffectiveTerrain == TileTerrainType.Stream && toTile.EffectiveTerrain == TileTerrainType.Stream)
                maxDist = 2;
            // Curly Tail Thirst for Glory Lv2+ or Supply Line Network
            if (p.Faction.Id == FactionId.CurlyTail)
            {
                if (p.CurlyTailThirstForGlory >= 2 && movableUnits.Any(u => u.Definition.Id == "curly_tail"))
                    maxDist = 2;
                if (AreTilesConnectedBySupplyLineNetwork(fromTile, toTile, p.PlayerIndex))
                    maxDist = 99;
            }
            // Platypus Crusader Lv3+: Move action made only with Crusaders may move up to 2 tiles (Line 575)
            if (p.Faction.Id == FactionId.PlatypusPapacy && p.PlatyPopeLevel >= 3 && movableUnits.All(u => u.Definition.Id == "crusader"))
                maxDist = 2;
            // Leather Boots on The Brute
            if (movableUnits.Any(u => u.EquippedItems.Any(e => e.BonusMove > 0)))
                maxDist = 2;

            if (dist == 0 || dist > maxDist) return false;

            foreach (var u in movableUnits)
            {
                fromTile.Units.Remove(u);
                toTile.Units.Add(u);
                u.HasMovedThisTurn = true;
                u.IsGuest = (toTile.Building != null && toTile.Building.Type == BuildingType.Wallow && toTile.Building.OwnerPlayerIndex != p.PlayerIndex) ||
                            toTile.Units.Any(x => x.Definition.Id == "riverkeeper" && x.OwnerPlayerIndex != p.PlayerIndex);
            }

            // Trouble Making Tanukis: The Coin Hunt (compulsory - Line 614) & Shrine Coin Scoring (+2 Authority - Line 612)
            if (p.Faction.Id == FactionId.TroubleMakingTanukis)
            {
                MoveAndScoreTanukiCoin(fromTile, toTile, p.PlayerIndex);
            }

            // Fallen Leaves Flipping when an enemy group ends a Move action on an unflipped Fallen Leaves tile (Lines 626-629)
            if (toTile.Overlay == FactionOverlayTileType.FallenLeaf_Unflipped && p.Faction.Id != FactionId.TroubleMakingTanukis)
            {
                var tanukiOwner = Players.FirstOrDefault(pl => pl.Faction.Id == FactionId.TroubleMakingTanukis);
                bool isTrap = false;
                if (tanukiOwner != null && tanukiOwner.FallenLeavesStack.Count > 0)
                {
                    isTrap = tanukiOwner.FallenLeavesStack[0];
                    tanukiOwner.FallenLeavesStack.RemoveAt(0);
                }
                else
                {
                    isTrap = _rng.Next(13) < 5;
                }

                if (isTrap)
                {
                    toTile.Overlay = FactionOverlayTileType.FallenLeaf_Trap;
                    foreach (var u in movableUnits)
                    {
                        u.CannotMoveThisTurn = true;
                        u.TrapLeafTurnsRemaining = 1; // Immobilized for rest of this turn and during next turn
                    }
                    Log($"TRAP LEAF flipped at {toCoord}! Enemy units are immobilized for this turn and their next turn!");
                }
                else
                {
                    toTile.Overlay = FactionOverlayTileType.FallenLeaf_Prank;
                    if (!toTile.Tokens.Any(t => t.Type == TokenType.Coin) && toTile.Tokens.Count < 2)
                    {
                        PlaceToken(toCoord, TokenType.Coin, tanukiOwner ?? p);
                        Log($"PRANK LEAF flipped at {toCoord}! Placed 1 Coin Token from supply!");
                        if (toTile.Building != null && toTile.Building.Type == BuildingType.Shrine && tanukiOwner != null)
                        {
                            toTile.Tokens.RemoveAll(t => t.Type == TokenType.Coin);
                            AwardAuthority(tanukiOwner.PlayerIndex, 2, "Coin Scored at Shrine");
                        }
                    }
                }
            }

            // Platypus Papacy Martyrdom (Part 17 Faction 3 Line 547):
            // "When a Pilgrim ends a Move action on a tile with enemy units, you may remove that Pilgrim to gain 1 Faith and 1 Authority."
            if (p.Faction.Id == FactionId.PlatypusPapacy)
            {
                bool hasEnemyUnits = toTile.Units.Any(u => u.OwnerPlayerIndex != p.PlayerIndex);
                if (hasEnemyUnits)
                {
                    var pilgrims = toTile.Units.Where(u => u.Definition.Id == "pilgrim").ToList();
                    foreach (var pil in pilgrims)
                    {
                        toTile.Units.Remove(pil);
                        p.PlatypusFaith = Mathf.Min(5, p.PlatypusFaith + 1);
                        AwardAuthority(p.PlayerIndex, 1, "Pilgrim Martyrdom (+1 Faith & +1 Authority)");
                    }
                }
            }

            // Fire Tile in-movement damage (1HP to 1 moving enemy unit - Line 780)
            if (toTile.Overlay == FactionOverlayTileType.Fire && p.Faction.Id != FactionId.FirestartingSquirrels)
            {
                var victim = movableUnits.FirstOrDefault(u => u.Definition.Id != "pyromaniac");
                if (victim != null)
                {
                    victim.CurrentHP -= 1;
                    Log($"{victim.Definition.Name} took 1HP damage entering Fire Tile at {toCoord}!");
                    if (victim.CurrentHP <= 0)
                        RemoveUnitFromBoard(toTile, victim, -1);
                }
            }

            MainPhaseActionsRemaining -= actionCost;
            Log($"{p.DisplayName} moved {movableUnits.Count} unit(s) from {fromCoord} to {toCoord}. ({MainPhaseActionsRemaining} actions left)");
            OnStateChanged?.Invoke();
            return true;
        }

        private bool AreTilesConnectedBySupplyLineNetwork(HexTileState startTile, HexTileState endTile, int curlyPlayerIndex)
        {
            // Part 17 Faction 1 Lines 385-386: War Parties and Supply Line Tokens are linked when within 2 tiles of each other.
            bool IsNode(HexTileState t) =>
                (t.Building != null && t.Building.Type == BuildingType.WarParty && t.Building.OwnerPlayerIndex == curlyPlayerIndex) ||
                t.Tokens.Any(tk => tk.Type == TokenType.SupplyLine && tk.OwnerPlayerIndex == curlyPlayerIndex);

            if (!IsNode(startTile) || !IsNode(endTile)) return false;

            var visited = new HashSet<HexCoord> { startTile.Coord };
            var queue = new Queue<HexCoord>();
            queue.Enqueue(startTile.Coord);

            var allNodes = Board.Values.Where(IsNode).Select(t => t.Coord).ToList();
            while (queue.Count > 0)
            {
                var curr = queue.Dequeue();
                if (curr.Equals(endTile.Coord)) return true;
                foreach (var node in allNodes)
                {
                    if (!visited.Contains(node) && HexCoord.Distance(curr, node) <= 2)
                    {
                        visited.Add(node);
                        queue.Enqueue(node);
                    }
                }
            }
            return false;
        }

        private void MoveAndScoreTanukiCoin(HexTileState fromTile, HexTileState toTile, int tanukiPlayerIndex)
        {
            var coin = fromTile.Tokens.FirstOrDefault(t => t.Type == TokenType.Coin);
            if (coin != null && !toTile.Tokens.Any(t => t.Type == TokenType.Coin) && toTile.Tokens.Count < 2)
            {
                fromTile.Tokens.Remove(coin);
                if (toTile.Building != null && toTile.Building.Type == BuildingType.Shrine && toTile.Building.OwnerPlayerIndex == tanukiPlayerIndex)
                {
                    AwardAuthority(tanukiPlayerIndex, 2, "Coin Token Delivered to Shrine");
                }
                else
                {
                    toTile.Tokens.Add(coin);
                }
            }
        }

        public bool TryLootTile(HexCoord coord)
        {
            if (CurrentPhase != TurnPhase.MainPhase || MainPhaseActionsRemaining <= 0) return false;
            if (!Board.TryGetValue(coord, out var tile) || tile.LootedThisTurn) return false;

            var p = Players[CurrentTurnPlayerIndex];
            bool hasMyUnits = tile.Units.Any(u => u.OwnerPlayerIndex == p.PlayerIndex);
            bool hasEnemyPieces = tile.Units.Any(u => u.OwnerPlayerIndex != p.PlayerIndex) ||
                                  (tile.Building != null && tile.Building.OwnerPlayerIndex != p.PlayerIndex) ||
                                  tile.Tokens.Any(tk => tk.OwnerPlayerIndex != p.PlayerIndex && tk.Type != TokenType.Coin);
            if (!hasMyUnits || hasEnemyPieces) return false;

            // Check Rusty Shovel Traded Good card (draw 3 resources and end loot)
            var shovel = p.PurchasedTradedGoods.FirstOrDefault(tg => tg.Id == "tg_rusty_shovel");
            if (shovel != null)
            {
                p.PurchasedTradedGoods.Remove(shovel);
                TradedGoodsDiscard.Add(shovel);
                for (int i = 0; i < 3; i++) p.ResourcePile.Add(DrawRandomResourceCard());
                tile.LootedThisTurn = true;
                MainPhaseActionsRemaining--;
                Log($"{p.DisplayName} used Rusty Shovel while looting {coord} -> Drew 3 Resource cards!");
                OnStateChanged?.Invoke();
                return true;
            }

            // Part 6 Lines 144-147: Exact Loot table for Core Rulebook: Eight Faction Edition:
            // - Fallen Leaves tile (flipped or unflipped): gain 1 Wood (only Wood) (Line 147 & Line 624)
            // - Forest tile: draw the top card of the resource deck into your Resource Pile (Line 144)
            // - Stream tile: roll a d6 -> Odd: gain 1 Stone. Even: gain 1 Gold (Line 145)
            // - Ruin tile: roll a d6 -> Odd: draw the top card of the relic deck into your Hand. Even: gain 1 Gold (Line 146)
            if (tile.Overlay == FactionOverlayTileType.FallenLeaf_Unflipped ||
                tile.Overlay == FactionOverlayTileType.FallenLeaf_Trap ||
                tile.Overlay == FactionOverlayTileType.FallenLeaf_Prank)
            {
                GainSpecificResourceFromDeck(p, ResourceType.Wood);
                Log($"{p.DisplayName} looted Fallen Leaves Tile {coord} -> Gained 1 Wood.");
            }
            else if (tile.EffectiveTerrain == TileTerrainType.Forest)
            {
                var res = DrawRandomResourceCard();
                p.ResourcePile.Add(res);
                Log($"{p.DisplayName} looted Forest Tile {coord} -> Drew 1 {res}.");
            }
            else if (tile.EffectiveTerrain == TileTerrainType.Stream)
            {
                int roll = RollD6();
                if (roll % 2 == 1)
                {
                    GainSpecificResourceFromDeck(p, ResourceType.Stone);
                    Log($"{p.DisplayName} looted Stream Tile {coord} (rolled {roll} - Odd) -> Gained 1 Stone.");
                }
                else
                {
                    GainSpecificResourceFromDeck(p, ResourceType.Gold);
                    Log($"{p.DisplayName} looted Stream Tile {coord} (rolled {roll} - Even) -> Gained 1 Gold.");
                }
            }
            else if (tile.EffectiveTerrain == TileTerrainType.Ruin)
            {
                int roll = RollD6();
                if (roll % 2 == 1)
                {
                    var relic = DrawRelicCard();
                    if (relic != null)
                    {
                        p.RelicHand.Add(relic);
                        Log($"{p.DisplayName} looted Ruin Tile {coord} (rolled {roll} - Odd) -> Drew Relic '{relic.Name}' into Hand.");
                    }
                }
                else
                {
                    GainSpecificResourceFromDeck(p, ResourceType.Gold);
                    Log($"{p.DisplayName} looted Ruin Tile {coord} (rolled {roll} - Even) -> Gained 1 Gold.");
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
            if (p.Faction.Id == FactionId.Necromouser) return false; // Necro-mouser has no building and cannot recruit

            var myBuildingTiles = Board.Values.Where(t => t.Building != null && t.Building.OwnerPlayerIndex == p.PlayerIndex).ToList();
            if (myBuildingTiles.Count == 0) return false;

            var basicUnit = p.Faction.Units.FirstOrDefault(u => !u.IsSpecialUnit);
            if (basicUnit == null) return false;

            int totalSpawned = 0;
            foreach (var tile in myBuildingTiles)
            {
                if (p.Faction.Id == FactionId.PlatypusPapacy)
                {
                    // Part 17 Faction 3 Lines 563-573: Platy-Pope Level Meter stacks!
                    // Lv0: 1 Pilgrim. Lv1+: also 1 Crusader. Lv3+: also 2 more Crusaders (total 1 Pilgrim + 3 Crusaders at Lv3+).
                    totalSpawned += SpawnUnit(tile.Coord, "pilgrim", p, 1).Count;
                    if (p.PlatyPopeLevel >= 1)
                        totalSpawned += SpawnUnit(tile.Coord, "crusader", p, 1).Count;
                    if (p.PlatyPopeLevel >= 3)
                        totalSpawned += SpawnUnit(tile.Coord, "crusader", p, 2).Count;
                }
                else
                {
                    totalSpawned += SpawnUnit(tile.Coord, basicUnit.Id, p, 1).Count;
                }
            }

            // Curly Tail Strategic Retreat rule 5 (Line 432): If Curly Tail was set aside with no War Party, place him at 1 HP on recruit
            if (p.Faction.Id == FactionId.CurlyTail && p.CurlyTailSetAsideForRecruit && myBuildingTiles.Count > 0)
            {
                p.CurlyTailSetAsideForRecruit = false;
                var ctList = SpawnUnit(myBuildingTiles[0].Coord, "curly_tail", p, 1);
                foreach (var ct in ctList) ct.CurrentHP = 1;
                Log("Curly Tail returned from Strategic Retreat at 1 HP at a War Party!");
            }

            MainPhaseActionsRemaining--;
            Log($"{p.DisplayName} used Recruit -> Placed {totalSpawned} unit(s) across {myBuildingTiles.Count} recruitment building(s).");
            OnStateChanged?.Invoke();
            return true;
        }

        // =====================================================================
        // Part 12 & Part 13: 5-PHASE 2D6 BATTLE RESOLUTION & ISOLATED PIECE REMOVAL
        // =====================================================================

        public BattleReport TryBattleOnTile(HexCoord coord, int targetDefenderPlayerIndex = -1)
        {
            if (CurrentPhase != TurnPhase.MainPhase || MainPhaseActionsRemaining <= 0) return null;
            if (!Board.TryGetValue(coord, out var tile)) return null;

            var attacker = Players[CurrentTurnPlayerIndex];
            var attackerUnits = tile.Units.Where(u => u.OwnerPlayerIndex == attacker.PlayerIndex).ToList();
            if (attackerUnits.Count == 0) return null;

            // Find defender faction on this tile
            if (targetDefenderPlayerIndex < 0)
            {
                var enemyUnit = tile.Units.FirstOrDefault(u => u.OwnerPlayerIndex != attacker.PlayerIndex);
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
            var defenderUnits = tile.Units.Where(u => u.OwnerPlayerIndex == defender.PlayerIndex).ToList();

            // Necro-mouser Cowardice (Line 678): Cannot be part of a battle or attacked if there are any skeletons or special units on his tile
            if (defender.Faction.Id == FactionId.Necromouser && defenderUnits.Count > 1)
            {
                defenderUnits.RemoveAll(u => u.Definition.Id == "necromouser");
            }

            // Vanguard Devotion (Line 688): If Necromouser is defending and Vanguard is on an adjacent tile, Vanguard + friendly units on its tile join defense
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

            // Part 13: Removing Isolated Enemy Buildings, Tokens, and Removable Tiles (no dice rolled!)
            if (defenderUnits.Count == 0)
            {
                int canRemove = attackerUnits.Count;
                int removedCount = 0;
                int bonusCrusadeAuth = (attacker.Faction.Id == FactionId.PlatypusPapacy && attacker.CrusadeTurnsRemaining > 0) ? 1 : 0;

                if (tile.Building != null && tile.Building.OwnerPlayerIndex == defender.PlayerIndex && canRemove > 0)
                {
                    Log($"{attacker.DisplayName} removed isolated {tile.Building.Type} at {coord}!");
                    tile.Building = null;
                    canRemove--;
                    removedCount++;
                    AwardAuthority(attacker.PlayerIndex, 1 + bonusCrusadeAuth, "Removed Isolated Enemy Building");
                }
                while (canRemove > 0 && tile.Tokens.Any(t => t.OwnerPlayerIndex == defender.PlayerIndex && t.Type != TokenType.Coin))
                {
                    var tk = tile.Tokens.First(t => t.OwnerPlayerIndex == defender.PlayerIndex && t.Type != TokenType.Coin);
                    tile.Tokens.Remove(tk);
                    canRemove--;
                    removedCount++;
                    AwardAuthority(attacker.PlayerIndex, 1 + bonusCrusadeAuth, $"Removed Isolated {tk.Type} Token");
                }
                // Flipped Fallen Leaves or isolated Miasma Tile removal (Unflipped Fallen Leaves cannot be removed - Line 231)
                if (canRemove > 0 && tile.OverlayOwnerPlayerIndex == defender.PlayerIndex &&
                    (tile.Overlay == FactionOverlayTileType.Miasma_Revealed || tile.Overlay == FactionOverlayTileType.Miasma_Hidden ||
                     tile.Overlay == FactionOverlayTileType.FallenLeaf_Trap || tile.Overlay == FactionOverlayTileType.FallenLeaf_Prank))
                {
                    var oldOverlay = tile.Overlay;
                    bool wasTrap = (oldOverlay == FactionOverlayTileType.FallenLeaf_Trap);
                    tile.Overlay = FactionOverlayTileType.None;
                    removedCount++;
                    AwardAuthority(attacker.PlayerIndex, 1, $"Removed Isolated {oldOverlay}");
                    if (defender.Faction.Id == FactionId.Necromouser)
                        defender.NecromouserCauldronDeadCount++;
                    if (defender.Faction.Id == FactionId.TroubleMakingTanukis &&
                        (oldOverlay == FactionOverlayTileType.FallenLeaf_Trap || oldOverlay == FactionOverlayTileType.FallenLeaf_Prank))
                    {
                        defender.FallenLeavesStack.Add(wasTrap);
                        Shuffle(defender.FallenLeavesStack);
                    }
                }

                if (removedCount > 0)
                {
                    MainPhaseActionsRemaining--;
                    OnStateChanged?.Invoke();
                }
                return null;
            }

            // Part 12: 5-Phase Unit Battle
            var report = new BattleReport
            {
                TileCoord = coord,
                AttackerPlayerIndex = attacker.PlayerIndex,
                DefenderPlayerIndex = defender.PlayerIndex
            };

            // War Horn check on Defender (end of Attack Declaration phase: place 1 basic unit from supply on battle tile)
            var warHorn = defender.TableRelics.FirstOrDefault(r => r.Id == "war_horn");
            if (warHorn != null && CanAddUnitToTile(tile, defender.PlayerIndex))
            {
                defender.TableRelics.Remove(warHorn);
                RelicDiscardPile.Add(warHorn);
                var defBasic = defender.Faction.Units.FirstOrDefault(u => !u.IsSpecialUnit);
                if (defBasic != null)
                {
                    var hornSpawned = SpawnUnit(coord, defBasic.Id, defender, 1);
                    defenderUnits.AddRange(hornSpawned);
                    Log($"{defender.DisplayName} flipped & discarded War Horn -> Placed 1 {defBasic.Name} into the defending battle!");
                }
            }

            // Phase 2: Dice Roll (2d6 each)
            int atkRoll = Roll2D6(out report.AttackerDice1, out report.AttackerDice2);
            int defRoll = Roll2D6(out report.DefenderDice1, out report.DefenderDice2);

            // Auto-check Table combat Relics (Overgrown Wall, Iron Halberd, Political Treatise)
            var overgrownWall = defender.TableRelics.FirstOrDefault(r => r.Id == "overgrown_wall");
            if (overgrownWall != null)
            {
                defender.TableRelics.Remove(overgrownWall);
                RelicDiscardPile.Add(overgrownWall);
                report.AttackerDiceBonus -= 2;
                Log($"{defender.DisplayName} discarded Overgrown Wall (-2 to Attacker dice roll).");
            }

            var halberd = attacker.TableRelics.FirstOrDefault(r => r.Id == "iron_halberd");
            if (halberd != null)
            {
                attacker.TableRelics.Remove(halberd);
                RelicDiscardPile.Add(halberd);
                report.AttackerDiceBonus += 2;
                Log($"{attacker.DisplayName} discarded Iron Halberd (+2 to Attacker dice roll).");
            }

            var treatise = defender.TableRelics.FirstOrDefault(r => r.Id == "political_treatise");
            if (treatise != null)
            {
                defender.TableRelics.Remove(treatise);
                RelicDiscardPile.Add(treatise);
                report.DefenderDiceBonus += 2;
                Log($"{defender.DisplayName} discarded Political Treatise (+2 to Defender dice roll).");
            }

            // Phase 3: Attack and Defense Points (Part 12 Lines 197-202)
            // Note: Each side totals both Attack Points and Defense Points of its participating units!
            int attackerAttackPoints = attackerUnits.Sum(u => GetUnitBattleAttack(attacker, tile, u));
            int attackerDefensePoints = attackerUnits.Sum(u => GetUnitBattleDefense(attacker, tile, u));
            int defenderAttackPoints = defenderUnits.Sum(u => GetUnitBattleAttack(defender, tile, u));
            int defenderDefensePoints = defenderUnits.Sum(u => GetUnitBattleDefense(defender, tile, u));

            report.AttackerUnitAttackSum = attackerAttackPoints;
            report.DefenderUnitDefenseSum = defenderDefensePoints;

            tile.HasStatueOfGoddessBuff = false;

            report.AttackerTotal = Mathf.Max(0, atkRoll + report.AttackerDiceBonus) + attackerAttackPoints;
            report.DefenderTotal = Mathf.Max(0, defRoll + report.DefenderDiceBonus) + defenderDefensePoints;

            // Phase 4: Damage Calculation & Phase 5: Battle Resolution
            if (report.AttackerTotal == report.DefenderTotal)
            {
                // Tie: Each side takes 1 HP of damage, skip Phase 4 and go to Phase 5 (Line 204)
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

                // Damage Dealt = winner's Attack Points minus loser's Defense Points (min 1, max = winner's participating units) (Lines 206-208)
                int winnerAttackPts = attackerWon ? attackerAttackPoints : defenderAttackPoints;
                int loserDefensePts = attackerWon ? defenderDefensePoints : attackerDefensePoints;
                int rawDamage = Mathf.Max(1, winnerAttackPts - loserDefensePts);
                int damageCap = Mathf.Max(1, winnerUnits.Count);
                int finalWinnerDamage = Mathf.Min(rawDamage, damageCap);

                // Brass Shield check on loser (after damage cap is applied, reduce damage by 2 to a min of 0 - Line 316)
                var brassShield = loser.TableRelics.FirstOrDefault(r => r.Id == "brass_shield");
                if (brassShield != null && finalWinnerDamage > 0)
                {
                    loser.TableRelics.Remove(brassShield);
                    RelicDiscardPile.Add(brassShield);
                    finalWinnerDamage = Mathf.Max(0, finalWinnerDamage - 2);
                    Log($"{loser.DisplayName} discarded Brass Shield (-2HP damage taken).");
                }

                // Mandatory Loser Damage: loser always deals 1 HP back to winner unless Horde of the Millions (Oblivious to Danger - Line 210)
                int mandatoryLoserDmg = (loser.Faction.Id == FactionId.HordeOfTheMillions) ? 0 : 1;

                report.WinnerDamageDealt = finalWinnerDamage;
                report.MandatoryLoserDamageDealt = mandatoryLoserDmg;

                bool allow2HpToSingleUnit = winnerUnits.Any(u => u.EquippedItems.Any(e => e.Id == "golden_gauntlet")) ||
                                            (winner.Faction.Id == FactionId.BriaredFlower && winner.BriaredFlowerThorns == 0 && winnerUnits.Any(u => u.Definition.Id == "blooming_briar"));

                int crusadersBefore = loserUnits.Count(u => u.Definition.Id == "crusader");

                // Apply damage simultaneously
                ApplyBattleDamageToSide(tile, loserUnits, finalWinnerDamage, winner.PlayerIndex, report, allow2HpToSingleUnit);
                if (mandatoryLoserDmg > 0)
                    ApplyBattleDamageToSide(tile, winnerUnits, mandatoryLoserDmg, loser.PlayerIndex, report, false);

                // Phase 5 Step 1: Platypus Papacy Crusader removal Authority checks (Lines 578 & 582)
                int crusadersRemoved = crusadersBefore - tile.Units.Count(u => u.OwnerPlayerIndex == loser.PlayerIndex && u.Definition.Id == "crusader");
                if (crusadersRemoved >= 2)
                    AwardAuthority(winner.PlayerIndex, 1, "Removed 2+ Platypus Crusaders in one battle");
                if (loser.Faction.Id == FactionId.PlatypusPapacy && loser.PlatyPopeLevel >= 4 && loser.CrusadeTurnsRemaining > 0 && crusadersRemoved > 0)
                    AwardAuthority(winner.PlayerIndex, crusadersRemoved, "Removed Crusaders during Lv4+ Crusade");
                if (winner.Faction.Id == FactionId.PlatypusPapacy && winner.PlatyPopeLevel >= 4 && winner.CrusadeTurnsRemaining > 0 && report.DestroyedUnitNames.Count > 0)
                    AwardAuthority(winner.PlayerIndex, 1, "Crusader removed enemy unit during Lv4+ Crusade");

                // Trouble Making Tanukis Quick Feet (Line 618)
                if (loser.Faction.Id == FactionId.TroubleMakingTanukis && finalWinnerDamage >= 1)
                {
                    var survivingTanukis = tile.Units.Where(u => u.OwnerPlayerIndex == loser.PlayerIndex && u.Definition.Id == "tanuki").ToList();
                    if (survivingTanukis.Count > 0)
                    {
                        var safeAdj = coord.GetNeighbors().Where(n => Board.ContainsKey(n) && !Board[n].Units.Any(u => u.OwnerPlayerIndex != loser.PlayerIndex)).ToList();
                        if (safeAdj.Count > 0)
                        {
                            var dest = safeAdj[0];
                            foreach (var st in survivingTanukis)
                            {
                                if (CanAddUnitToTile(Board[dest], loser.PlayerIndex))
                                {
                                    tile.Units.Remove(st);
                                    Board[dest].Units.Add(st);
                                }
                            }
                            MoveAndScoreTanukiCoin(tile, Board[dest], loser.PlayerIndex);
                            if (Board[dest].EffectiveTerrain == TileTerrainType.Forest && Board[dest].Overlay == FactionOverlayTileType.None)
                                PlaceOverlayTile(dest, FactionOverlayTileType.FallenLeaf_Unflipped, loser.PlayerIndex);
                            Log($"Quick Feet! Surviving Tanukis scattered to {dest} and placed a Fallen Leaves Tile!");
                        }
                    }
                }

                // Phase 5 Step 2: Resolve effects that care about winning or losing the battle (Momentum - Line 215 & Lines 414-418)
                // "Momentum only changes from battles where your faction is the Attacker."
                if (attacker.Faction.Id == FactionId.CurlyTail)
                {
                    if (attackerWon)
                    {
                        attacker.CurlyTailMomentum = Mathf.Min(4, attacker.CurlyTailMomentum + 1);
                        Log($"{attacker.DisplayName} attacked and won -> Momentum rose to Level {attacker.CurlyTailMomentum}.");
                    }
                    else
                    {
                        attacker.CurlyTailMomentum = Mathf.Max(0, attacker.CurlyTailMomentum - 2);
                        attacker.CurlyTailHunger = Mathf.Max(0, attacker.CurlyTailHunger - 2);
                        attacker.CurlyTailThirstForGlory = Mathf.Max(0, attacker.CurlyTailThirstForGlory - 1);
                        Log($"{attacker.DisplayName} attacked and lost -> Momentum -2, Hunger -2, Thirst for Glory -1.");
                    }
                }

                // Phase 5 Step 3: Resolve "after a battle" effects (Spoils of War, Pillage, Crusade Faith - Line 216)
                if (winner.Faction.Id == FactionId.CurlyTail)
                {
                    if (!winner.CurlyTailWonBattleThisTurn)
                    {
                        winner.CurlyTailWonBattleThisTurn = true;
                        GainSpecificResourceFromDeck(winner, _rng.Next(2) == 0 ? ResourceType.Stone : ResourceType.Wood);
                        Log($"{winner.DisplayName} triggered Spoils of War (first win this turn) -> Gained 1 resource.");
                    }
                    if (winnerUnits.Any(u => u.Definition.Id == "curly_tail"))
                    {
                        winner.ResourcePile.Add(DrawRandomResourceCard());
                        if (winner.CurlyTailMomentum >= 1)
                        {
                            var rel = DrawRelicCard();
                            if (rel != null) winner.RelicHand.Add(rel);
                        }
                        Log($"{winner.DisplayName} triggered Curly Tail's Pillager (free Pillage)!");
                    }
                }
                if (winner.Faction.Id == FactionId.PlatypusPapacy && winner.CrusadeTurnsRemaining > 0)
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
            // Capybara Caravan Selfless Nature (Line 824): When a Guest would take damage in battle, a Capybara unit on that tile absorbs 1HP damage
            var ordered = units.OrderBy(u => u.Definition.IsSpecialUnit ? 1 : 0).ToList();
            int remaining = totalDamage;

            foreach (var u in ordered)
            {
                if (remaining <= 0) break;

                if (u.IsGuest)
                {
                    var protectorCapy = tile.Units.FirstOrDefault(c => c.Definition.Id == "capybara" && c.CurrentHP > 0);
                    if (protectorCapy != null)
                    {
                        protectorCapy.CurrentHP -= 1;
                        remaining -= 1;
                        Log($"Selfless Nature! Capybara at {tile.Coord} absorbed 1HP battle damage for Guest {u.Definition.Name}!");
                        if (protectorCapy.CurrentHP <= 0)
                        {
                            report.DestroyedUnitNames.Add(protectorCapy.Definition.Name);
                            RemoveUnitFromBoard(tile, protectorCapy, killerPlayerIndex);
                        }
                        continue;
                    }
                }

                var owner = Players[u.OwnerPlayerIndex];
                // Briared Flower Thorn Armor: Remove 1 Thorn instead of taking 1HP
                if (owner.Faction.Id == FactionId.BriaredFlower && u.Definition.Id == "blooming_briar" && owner.BriaredFlowerThorns > 0)
                {
                    owner.BriaredFlowerThorns--;
                    remaining--;
                    Log($"Blooming Briar shed 1 Thorn via Thorn Armor ({owner.BriaredFlowerThorns} Thorns remaining).");
                    continue;
                }

                int hit = (allow2HpToSingleUnit && u.Definition.IsSpecialUnit && remaining >= 2 && u.CurrentHP >= 2) ? 2 : 1;
                u.CurrentHP -= hit;
                remaining -= hit;
                if (u.CurrentHP <= 0)
                {
                    report.DestroyedUnitNames.Add(u.Definition.Name);
                    RemoveUnitFromBoard(tile, u, killerPlayerIndex);
                }
            }
        }

        private void RemoveUnitFromBoard(HexTileState tile, UnitInstance u, int removerPlayerIndex)
        {
            var owner = Players[u.OwnerPlayerIndex];
            int crusadeBonus = (removerPlayerIndex >= 0 && removerPlayerIndex != owner.PlayerIndex &&
                                Players[removerPlayerIndex].Faction.Id == FactionId.PlatypusPapacy &&
                                Players[removerPlayerIndex].CrusadeTurnsRemaining > 0) ? 1 : 0;

            // 1. Necro-mouser Cowardice (Line 678): When isolated and removed, lose 3 Authority, move 2 tiles away, place 3 adjacent Miasma Tiles
            if (u.Definition.Id == "necromouser")
            {
                owner.Authority = Mathf.Max(0, owner.Authority - 3);
                u.CurrentHP = 1;
                tile.Units.Remove(u);
                var escapeTile = Board.Values.FirstOrDefault(t => HexCoord.Distance(t.Coord, tile.Coord) == 2 && CanAddUnitToTile(t, owner.PlayerIndex)) ?? tile;
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
                Log($"Cowardice! The Necromouser lost 3 Authority and fled to {escapeTile.Coord}, placing {mPlaced} adjacent Miasma Tiles!");
                return;
            }

            // 2. Curly Tail Strategic Retreat (Part 17 Faction 1 Lines 427-432):
            // (1) Remover gains 2 Authority. (2) Discard 2 random resources. (3) Return up to 2 equipped Golden items to supply.
            // (4) Place Curly Tail at 1 HP on a War Party tile with 1 Warband unit (or set aside if no War Party).
            if (u.Definition.Id == "curly_tail")
            {
                if (removerPlayerIndex >= 0 && removerPlayerIndex != owner.PlayerIndex)
                    AwardAuthority(removerPlayerIndex, 2 + crusadeBonus, "Removed Curly Tail (Strategic Retreat)");
                for (int i = 0; i < 2 && owner.ResourcePile.Count > 0; i++)
                    ReturnResourceToDeck(owner.ResourcePile, _rng.Next(owner.ResourcePile.Count));

                int itemsToReturn = Mathf.Min(2, u.EquippedItems.Count);
                for (int i = 0; i < itemsToReturn; i++)
                {
                    var removedEq = u.EquippedItems[0];
                    u.EquippedItems.RemoveAt(0);
                    u.MaxHP = Mathf.Max(2, u.MaxHP - removedEq.BonusHP);
                }

                tile.Units.Remove(u);
                var warPartyTile = Board.Values.FirstOrDefault(t => t.Building != null && t.Building.Type == BuildingType.WarParty && t.Building.OwnerPlayerIndex == owner.PlayerIndex && CanAddUnitToTile(t, owner.PlayerIndex));
                if (warPartyTile != null)
                {
                    u.CurrentHP = 1;
                    warPartyTile.Units.Add(u);
                    SpawnUnit(warPartyTile.Coord, "warband", owner, 1);
                    Log($"Strategic Retreat! Curly Tail returned to War Party at {warPartyTile.Coord} at 1 HP with 1 Warband unit!");
                }
                else
                {
                    owner.CurlyTailSetAsideForRecruit = true;
                    Log("Strategic Retreat! No War Party on board -> Curly Tail set aside to deploy on next Recruit at a War Party.");
                }
                return;
            }

            // 3. Trouble Making Tanukis: No Honor Amongst Troublemakers (Line 637)
            if (u.Definition.Id == "band_of_troublemakers")
            {
                tile.Units.Remove(u);
                if (removerPlayerIndex >= 0 && removerPlayerIndex != owner.PlayerIndex)
                    AwardAuthority(removerPlayerIndex, 2 + crusadeBonus, "Removed A Band of Troublemakers");
                int split = 0;
                HexTileState firstDest = null;
                foreach (var n in tile.Coord.GetNeighbors())
                {
                    if (Board.ContainsKey(n) &&
                        (Board[n].EffectiveTerrain == TileTerrainType.Forest ||
                         Board[n].Overlay == FactionOverlayTileType.FallenLeaf_Unflipped ||
                         Board[n].Overlay == FactionOverlayTileType.FallenLeaf_Trap ||
                         Board[n].Overlay == FactionOverlayTileType.FallenLeaf_Prank) &&
                        CanAddUnitToTile(Board[n], owner.PlayerIndex) && split < 3)
                    {
                        SpawnUnit(n, "tanuki", owner, 1);
                        if (firstDest == null) firstDest = Board[n];
                        split++;
                    }
                }
                if (firstDest != null)
                    MoveAndScoreTanukiCoin(tile, firstDest, owner.PlayerIndex);
                Log($"No Honor Amongst Troublemakers! The Band split into {split} Tanukis on adjacent forest/Fallen Leaves tiles!");
                return;
            }

            // 4. The Briared Flower: Propagation (Line 888)
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
                if (removerPlayerIndex >= 0 && removerPlayerIndex != owner.PlayerIndex)
                    AwardAuthority(removerPlayerIndex, 2 + crusadeBonus, "Removed the Blooming Briar (Propagation)");
                return;
            }

            // 5. The Platy-Pope removal (Line 560): Papacy loses 1 Authority, remover gains 2 Authority, Pope returns next Excavation phase
            if (u.Definition.Id == "platy_pope")
            {
                owner.PlatyPopeOnBoard = false;
                owner.Authority = Mathf.Max(0, owner.Authority - 1);
            }

            tile.Units.Remove(u);

            // Score Authority for removing other Special Units
            if (u.Definition.IsSpecialUnit && removerPlayerIndex >= 0 && removerPlayerIndex != u.OwnerPlayerIndex)
            {
                int authGain = 2;
                if (owner.Faction.Id == FactionId.Necromouser)
                {
                    if (u.Definition.Id == "the_bloat")
                    {
                        authGain = 1;
                        Players[removerPlayerIndex].ResourcePile.Add(DrawRandomResourceCard());
                    }
                    else if (u.Definition.Id == "the_vanguard") authGain = 3;
                    else if (u.Definition.Id == "the_brute") authGain = 4;
                }
                else if (u.Definition.Id == "capybara_trader")
                {
                    authGain = 1;
                }
                AwardAuthority(removerPlayerIndex, authGain + crusadeBonus, $"Removed {u.Definition.Name}");
            }

            // Necro-mouser Miasma trigger (Line 667 & Line 675): Any unit removed on or adjacent to a Miasma Tile places 1 unit onto Cauldron & awakens Necromouser
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
                        Log($"THE NECROMOUSER AWAKENS at {tile.Coord}! All Miasma Tiles flipped faceup!");
                    }
                }
            }
        }

        // =====================================================================
        // FACTION ACTION PALETTE (Every Core Faction's Specific Abilities, Tokens, Tiles, Summons)
        // =====================================================================

        public List<string> GetAvailableFactionActions(PlayerState p)
        {
            var actions = new List<string>();
            switch (p.Faction.Id)
            {
                case FactionId.CurlyTail:
                    actions.Add("Supply Line (Free Action): 1 Wood -> Place Supply Line Token on Selected Hex");
                    actions.Add("Forge Golden Sword (1 Gold -> +2 Auth, +1 ATK)");
                    actions.Add("Forge Golden Chest Plate (2 Gold -> +2 Auth, +2 DEF)");
                    actions.Add("Forge Golden Gauntlet (2 Gold -> +2 Auth, 2HP vs Special)");
                    actions.Add("Forge Golden War Axe (2 Gold -> +2 Auth, Free Battle)");
                    actions.Add("Forge Tyrant's Crown (2 Gold -> +2 Auth, +2 Max HP & Heal 2)");
                    actions.Add("Taunt (Glory Lv3+): Pull Enemy within 2 Tiles & Free Battle");
                    break;
                case FactionId.HordeOfTheMillions:
                    actions.Add("Other People's Gluttony (Free Craft): 1 Gold -> Place Gluttony Token on Millions Hex");
                    actions.Add("Craft & Fire Catapult Token (2 Stone + 1 Wood -> Launch up to 3 Millions 3 tiles)");
                    actions.Add("Summon King of the Millions (Once/Game: 1G+1S+1W at Burrow -> Spread the Fun)");
                    actions.Add("King of the Rabbit Hill (Once/Main Phase: Place 1 Millions on King's tile)");
                    break;
                case FactionId.PlatypusPapacy:
                    actions.Add("Alms (5 Faith + Platy-Pope on Board -> +1 Platy-Pope Level, +1 Authority, Enter Crusade!)");
                    actions.Add("Indulgences (1 Gold -> +2 Faith)");
                    actions.Add("Holy Relic Token (1W+1S+1G -> Place at Citadel, +1 Authority now & +1 Faith/End Phase)");
                    actions.Add("Level 2 Cleanser (1 Gold -> Remove Debuffs from Crusaders on Tile)");
                    actions.Add("Level 3 Conversion (2 Faith -> Remove Enemy Basic Unit at Crusader & Place Pilgrim)");
                    actions.Add("Level 5 Faith Conversion (3 Faith -> +1 Authority)");
                    break;
                case FactionId.TroubleMakingTanukis:
                    actions.Add("Running Amok (Once/Main Phase): Place Fallen Leaves Tile on Tanuki Forest Hex");
                    actions.Add("Summon A Band of Troublemakers (3 Tanukis on same tile + 1 Gold)");
                    actions.Add("Belly Drums (Draw Phase): Place Fallen Leaves on up to 2 Coin Forest Tiles");
                    break;
                case FactionId.Necromouser:
                    actions.Add("Arise (Once/Turn): Spend 1 Dead from Cauldron -> Summon 1 Skeleton on Miasma Tile");
                    actions.Add("Spread the Bad News (Once/Main Phase): Remove 1 Skeleton -> Place Miasma Tile");
                    actions.Add("Necromancy: Summon The Bloat (1 Dead -> +1 Authority)");
                    actions.Add("Necromancy: Summon The Vanguard (3 Dead -> +3 Authority)");
                    actions.Add("Necromancy: Summon The Brute (4 Dead -> +4 Authority)");
                    actions.Add("Necrosis: Explode The Bloat (2HP AoE to all enemy units on tile)");
                    break;
                case FactionId.FirestartingSquirrels:
                    actions.Add("Craft Acorn Firestarter (1 Charcoal + 1 Wood)");
                    actions.Add("Use Acorn Firestarter -> Place Fire Tile on Squirrel Forest Tile");
                    actions.Add("Craft Tar Pit Token (2 Charcoal -> Place on/adjacent to Squirrel)");
                    actions.Add("Unquenchable Spirit (1 Main Action): Loot Adjacent Fire Tile for 1 Charcoal");
                    actions.Add("Disruption (Burn 2 Resources -> Burn 1 Excavation + 1 Resource from Each Player)");
                    actions.Add("Summon Pyromaniac (Once/Game: Burn 1W+1S+1G+1C on Fire Tile)");
                    break;
                case FactionId.CapybaraCaravan:
                    actions.Add("Capybara Forage (Once/Main Phase on Stream Tile -> Gain 1 Gold or 1 Stone)");
                    actions.Add("Exchange Rate (Once/Main Phase: Discard 3 Identical Resources -> Gain 2 Gold)");
                    actions.Add("Peddling (Free Move: Move Capybara Trader up to 2 Land or 3 Stream Tiles)");
                    actions.Add("Summon The Riverkeeper (Requires 2+ Wallows: 2 Resources + 1 Gold)");
                    actions.Add("Riverkeeper Hospitality (Guest draws 1 Relic -> +1 Authority & Recover Discarded Relic)");
                    break;
                case FactionId.BriaredFlower:
                    actions.Add("Thistle Barrage (1 Thorn + 1 Res per unit within 4-6 tiles -> 1HP each & Spawn Seed)");
                    actions.Add("Thistle Barrage Structure Clear (1 Thorn -> Remove isolated enemy buildings/tokens within 3 tiles)");
                    actions.Add("Consume Prickly Thicket on Briar's Tile -> +1 Thorn");
                    actions.Add("Sun Eater (Remove 2 Seeds on/adj Briar + 3 Resources -> +2 Thorns)");
                    actions.Add("Bursting Into Bloom (Remove up to 3 Seeds -> 1HP Damage on each tile, +1 Auth on removal)");
                    actions.Add("Furious Lashing (At 0 Thorns -> 1HP Damage to all enemy units within 3 tiles)");
                    actions.Add("In Half Bloom (2 Resources + 3 Seeds -> Respawn Blooming Briar with 2 Thorns at Prickly Thicket)");
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
                case FactionId.CurlyTail:
                    if (actionIndex == 0 && p.ResourcePile.Contains(ResourceType.Wood) && selectedTile.EffectiveTerrain != TileTerrainType.Stream)
                    {
                        RemoveResources(p, ResourceType.Wood, 1);
                        PlaceToken(selectedTile.Coord, TokenType.SupplyLine, p);
                        Log($"{p.DisplayName} placed a Supply Line Token at {selectedTile.Coord}.");
                    }
                    else if (actionIndex == 1) return TryCraftAndEquipFactionEquipment("golden_sword");
                    else if (actionIndex == 2) return TryCraftAndEquipFactionEquipment("golden_chestplate");
                    else if (actionIndex == 3) return TryCraftAndEquipFactionEquipment("golden_gauntlet");
                    else if (actionIndex == 4) return TryCraftAndEquipFactionEquipment("golden_war_axe");
                    else if (actionIndex == 5) return TryCraftAndEquipFactionEquipment("tyrants_crown");
                    else if (actionIndex == 6 && p.CurlyTailThirstForGlory >= 3)
                    {
                        var ctTile = Board.Values.FirstOrDefault(t => t.Units.Any(u => u.Definition.Id == "curly_tail"));
                        if (ctTile == null) return false;
                        var enemyTile = Board.Values.FirstOrDefault(t => HexCoord.Distance(t.Coord, ctTile.Coord) <= 2 && t != ctTile && t.Units.Any(u => u.OwnerPlayerIndex != p.PlayerIndex));
                        if (enemyTile == null) return false;
                        var pulled = enemyTile.Units.First(u => u.OwnerPlayerIndex != p.PlayerIndex);
                        enemyTile.Units.Remove(pulled);
                        ctTile.Units.Add(pulled);
                        Log($"Curly Tail used Taunt to pull {pulled.Definition.Name} onto {ctTile.Coord}!");
                        TryBattleOnTile(ctTile.Coord, pulled.OwnerPlayerIndex);
                    }
                    else return false;
                    break;

                case FactionId.HordeOfTheMillions:
                    if (actionIndex == 0 && p.ResourcePile.Contains(ResourceType.Gold))
                    {
                        var mTile = (selectedTile.Units.Any(u => u.Definition.Id == "millions") ? selectedTile :
                                     Board.Values.FirstOrDefault(t => t.Units.Any(u => u.Definition.Id == "millions")));
                        if (mTile == null) return false;
                        RemoveResources(p, ResourceType.Gold, 1);
                        PlaceToken(mTile.Coord, TokenType.Gluttony, p);
                        Log($"{p.DisplayName} placed a Gluttony Token at {mTile.Coord}.");
                    }
                    else if (actionIndex == 1 && CanAfford(p, new ResourceCost(wood: 1, stone: 2)))
                    {
                        SpendResources(p, new ResourceCost(wood: 1, stone: 2));
                        PlaceToken(selectedTile.Coord, TokenType.Catapult, p);
                        Log($"{p.DisplayName} crafted & fired a Catapult Token at {selectedTile.Coord}!");
                    }
                    else if (actionIndex == 2 && !p.KingOfTheMillionsSummonedOnce && CanAfford(p, new ResourceCost(wood: 1, stone: 1, gold: 1)))
                    {
                        var bTile = Board.Values.FirstOrDefault(t => t.Building != null && t.Building.Type == BuildingType.Burrow) ?? selectedTile;
                        SpendResources(p, new ResourceCost(wood: 1, stone: 1, gold: 1));
                        p.KingOfTheMillionsSummonedOnce = true;
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
                        Log("King of the Rabbit Hill placed 1 Millions unit on the King's tile!");
                    }
                    else return false;
                    break;

                case FactionId.PlatypusPapacy:
                    bool popeOnBoard = Board.Values.SelectMany(t => t.Units).Any(u => u.OwnerPlayerIndex == p.PlayerIndex && u.Definition.Id == "platy_pope");
                    if (actionIndex == 0 && p.PlatypusFaith >= 5 && popeOnBoard)
                    {
                        p.PlatypusFaith = 0;
                        p.PlatyPopeLevel = Mathf.Min(5, p.PlatyPopeLevel + 1);
                        p.CrusadeTurnsRemaining += 2;
                        AwardAuthority(p.PlayerIndex, 1, $"Alms (Platy-Pope Level {p.PlatyPopeLevel} & Crusade!)");
                    }
                    else if (actionIndex == 1 && p.ResourcePile.Contains(ResourceType.Gold))
                    {
                        RemoveResources(p, ResourceType.Gold, 1);
                        p.PlatypusFaith = Mathf.Min(5, p.PlatypusFaith + 2);
                        Log($"{p.DisplayName} used Indulgences (1 Gold -> +2 Faith, now {p.PlatypusFaith}/5).");
                    }
                    else if (actionIndex == 2 && CanAfford(p, new ResourceCost(wood: 1, stone: 1, gold: 1)) &&
                             Board.Values.SelectMany(t => t.Units).Any(u => u.Definition.Id == "crusader"))
                    {
                        var citTile = Board.Values.FirstOrDefault(t => t.Building != null && t.Building.Type == BuildingType.TheCitadel) ?? selectedTile;
                        SpendResources(p, new ResourceCost(wood: 1, stone: 1, gold: 1));
                        PlaceToken(citTile.Coord, TokenType.HolyRelic, p);
                        // Core Rulebook Line 550: immediately gain 1 Authority when placing Holy Relic Token!
                        AwardAuthority(p.PlayerIndex, 1, "Enshrined Holy Relic Token at The Citadel");
                    }
                    else if (actionIndex == 3 && p.PlatyPopeLevel >= 2 && p.ResourcePile.Contains(ResourceType.Gold))
                    {
                        RemoveResources(p, ResourceType.Gold, 1);
                        foreach (var u in selectedTile.Units.Where(u => u.OwnerPlayerIndex == p.PlayerIndex && u.Definition.Id == "crusader"))
                        {
                            u.CannotMoveThisTurn = false;
                            u.TrapLeafTurnsRemaining = 0;
                        }
                        Log($"{p.DisplayName} cleansed all debuffs from Crusaders at {selectedTile.Coord}.");
                    }
                    else if (actionIndex == 4 && p.PlatyPopeLevel >= 3 && p.PlatypusFaith >= 2)
                    {
                        var cTile = Board.Values.FirstOrDefault(t => t.Units.Any(u => u.Definition.Id == "crusader") && t.Units.Any(u => u.OwnerPlayerIndex != p.PlayerIndex && !u.Definition.IsSpecialUnit));
                        if (cTile == null) return false;
                        p.PlatypusFaith -= 2;
                        var enemy = cTile.Units.First(u => u.OwnerPlayerIndex != p.PlayerIndex && !u.Definition.IsSpecialUnit);
                        cTile.Units.Remove(enemy);
                        SpawnUnit(cTile.Coord, "pilgrim", p, 1);
                        Log($"{p.DisplayName} spent 2 Faith to convert {enemy.Definition.Name} at {cTile.Coord} into a Pilgrim!");
                    }
                    else if (actionIndex == 5 && p.PlatyPopeLevel >= 5 && p.PlatypusFaith >= 3)
                    {
                        p.PlatypusFaith -= 3;
                        AwardAuthority(p.PlayerIndex, 1, "Platy-Pope Level 5 Faith Conversion");
                    }
                    else return false;
                    break;

                case FactionId.TroubleMakingTanukis:
                    if (actionIndex == 0)
                    {
                        var tTile = (selectedTile.Units.Any(u => u.Faction == FactionId.TroubleMakingTanukis) && selectedTile.EffectiveTerrain == TileTerrainType.Forest && selectedTile.Overlay == FactionOverlayTileType.None)
                            ? selectedTile
                            : Board.Values.FirstOrDefault(t => t.Units.Any(u => u.Faction == FactionId.TroubleMakingTanukis) && t.EffectiveTerrain == TileTerrainType.Forest && t.Overlay == FactionOverlayTileType.None);
                        if (tTile == null) return false;
                        PlaceOverlayTile(tTile.Coord, FactionOverlayTileType.FallenLeaf_Unflipped, p.PlayerIndex);
                        Log($"{p.DisplayName} used Running Amok -> Placed Fallen Leaves Tile at {tTile.Coord}.");
                    }
                    else if (actionIndex == 1 && p.ResourcePile.Contains(ResourceType.Gold) &&
                             !Board.Values.SelectMany(t => t.Units).Any(u => u.Definition.Id == "band_of_troublemakers"))
                    {
                        var bTile = Board.Values.FirstOrDefault(t => t.Units.Count(u => u.Definition.Id == "tanuki") >= 3);
                        if (bTile == null) return false;
                        RemoveResources(p, ResourceType.Gold, 1);
                        for (int i = 0; i < 3; i++)
                            bTile.Units.Remove(bTile.Units.First(u => u.Definition.Id == "tanuki"));
                        SpawnUnit(bTile.Coord, "band_of_troublemakers", p, 1);
                        Log($"{p.DisplayName} formed A Band of Troublemakers at {bTile.Coord}!");
                    }
                    else if (actionIndex == 2 && Board.Values.SelectMany(t => t.Units).Any(u => u.Definition.Id == "band_of_troublemakers"))
                    {
                        int placed = 0;
                        foreach (var cTile in Board.Values.Where(t => t.Tokens.Any(tk => tk.Type == TokenType.Coin) && t.EffectiveTerrain == TileTerrainType.Forest && t.Overlay == FactionOverlayTileType.None && !t.Units.Any(u => u.OwnerPlayerIndex != p.PlayerIndex)))
                        {
                            if (placed >= 2) break;
                            PlaceOverlayTile(cTile.Coord, FactionOverlayTileType.FallenLeaf_Unflipped, p.PlayerIndex);
                            placed++;
                        }
                        Log($"{p.DisplayName} used Belly Drums -> Placed {placed} Fallen Leaves Tiles on Coin forest tiles!");
                    }
                    else return false;
                    break;

                case FactionId.Necromouser:
                    if (actionIndex == 0 && p.NecromouserCauldronDeadCount >= 1)
                    {
                        var mTile = Board.Values.FirstOrDefault(t => (t.Overlay == FactionOverlayTileType.Miasma_Revealed || t.Overlay == FactionOverlayTileType.Miasma_Hidden) && CanAddUnitToTile(t, p.PlayerIndex)) ?? selectedTile;
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
                        bool hasLocket = bloat.EquippedItems.Any(e => e.Id == "locket_of_memory");
                        bool hasRing = bloat.EquippedItems.Any(e => e.Id == "ring_of_favor");
                        bTile.Units.Remove(bloat);

                        var affectedTiles = new List<HexTileState> { bTile };
                        if (hasLocket)
                        {
                            foreach (var n in bTile.Coord.GetNeighbors())
                                if (Board.ContainsKey(n)) affectedTiles.Add(Board[n]);
                        }
                        foreach (var at in affectedTiles)
                        {
                            foreach (var enemy in at.Units.Where(u => u.OwnerPlayerIndex != p.PlayerIndex).ToList())
                            {
                                enemy.CurrentHP -= 2;
                                if (enemy.CurrentHP <= 0) RemoveUnitFromBoard(at, enemy, p.PlayerIndex);
                            }
                        }
                        if (hasRing)
                        {
                            PlaceOverlayTile(bTile.Coord, FactionOverlayTileType.Miasma_Revealed, p.PlayerIndex);
                            AwardAuthority(p.PlayerIndex, 2, "Ring of Favor Bloat Explosion");
                        }
                        Log($"The Bloat exploded via Necrosis at {bTile.Coord} for 2HP AoE damage!");
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
                        var sqTile = (selectedTile.Units.Any(u => u.Definition.Id == "squirrel") && selectedTile.EffectiveTerrain == TileTerrainType.Forest && selectedTile.Overlay == FactionOverlayTileType.None)
                            ? selectedTile
                            : Board.Values.FirstOrDefault(t => t.Units.Any(u => u.Definition.Id == "squirrel") && t.EffectiveTerrain == TileTerrainType.Forest && t.Overlay == FactionOverlayTileType.None);
                        if (sqTile == null) return false;
                        p.SquirrelsCraftedFirestarters--;
                        sqTile.Overlay = FactionOverlayTileType.Fire;
                        sqTile.OverlayOwnerPlayerIndex = p.PlayerIndex;
                        Log($"{p.DisplayName} discarded an Acorn Firestarter -> Placed Fire Tile at {sqTile.Coord}!");
                    }
                    else if (actionIndex == 2 && CanAfford(p, new ResourceCost(charcoal: 2)))
                    {
                        SpendResources(p, new ResourceCost(charcoal: 2));
                        PlaceToken(selectedTile.Coord, TokenType.TarPit, p);
                        Log($"{p.DisplayName} placed a Tar Pit Token at {selectedTile.Coord}.");
                    }
                    else if (actionIndex == 3 && MainPhaseActionsRemaining > 0)
                    {
                        MainPhaseActionsRemaining--;
                        p.ResourcePile.Add(ResourceType.Charcoal);
                        Log($"{p.DisplayName} used Unquenchable Spirit -> Looted adjacent Fire Tile for 1 Charcoal.");
                    }
                    else if (actionIndex == 4 && p.ResourcePile.Count >= 2)
                    {
                        // Disruption: burn 2 resources to burn 1 Excavation card and 1 resource from each opponent (+1 Authority if Gold burned)
                        bool burnedGold = false;
                        for (int i = 0; i < 2; i++)
                        {
                            var b = p.ResourcePile[0];
                            p.ResourcePile.RemoveAt(0);
                            BurnedResources.Add(b);
                            if (b == ResourceType.Gold) burnedGold = true;
                        }
                        if (ExcavationArea.Count > 0)
                        {
                            var exc = ExcavationArea[0];
                            ExcavationArea.RemoveAt(0);
                            if (exc.DisassembleGain.Gold > 0) burnedGold = true;
                        }
                        foreach (var op in Players.Where(x => x.PlayerIndex != p.PlayerIndex && x.ResourcePile.Count > 0))
                        {
                            var b = op.ResourcePile[0];
                            op.ResourcePile.RemoveAt(0);
                            BurnedResources.Add(b);
                            if (b == ResourceType.Gold) burnedGold = true;
                        }
                        if (burnedGold)
                            AwardAuthority(p.PlayerIndex, 1, "Disruption (Burned Gold)");
                        Log($"{p.DisplayName} triggered Disruption!");
                    }
                    else if (actionIndex == 5 && !p.PyromaniacSummonedOnce && CanAfford(p, new ResourceCost(wood: 1, stone: 1, gold: 1, charcoal: 1)))
                    {
                        var fTile = Board.Values.FirstOrDefault(t => t.Overlay == FactionOverlayTileType.Fire) ?? selectedTile;
                        SpendResources(p, new ResourceCost(wood: 1, stone: 1, gold: 1, charcoal: 1));
                        p.PyromaniacSummonedOnce = true;
                        SpawnUnit(fTile.Coord, "pyromaniac", p, 1);
                        Log($"{p.DisplayName} summoned the Pyromaniac at {fTile.Coord}!");
                    }
                    else return false;
                    break;

                case FactionId.CapybaraCaravan:
                    if (actionIndex == 0)
                    {
                        bool onStream = Board.Values.Any(t => t.EffectiveTerrain == TileTerrainType.Stream && t.Units.Any(u => u.Definition.Id == "capybara"));
                        if (!onStream) return false;
                        GainSpecificResourceFromDeck(p, ResourceType.Gold);
                        Log($"{p.DisplayName} used Capybara Forage on Stream Tile -> Gained 1 Gold!");
                    }
                    else if (actionIndex == 1)
                    {
                        foreach (ResourceType rt in new[] { ResourceType.Wood, ResourceType.Stone })
                        {
                            if (p.ResourcePile.Count(r => r == rt) >= 3)
                            {
                                RemoveResources(p, rt, 3);
                                GainSpecificResourceFromDeck(p, ResourceType.Gold);
                                GainSpecificResourceFromDeck(p, ResourceType.Gold);
                                Log($"{p.DisplayName} used Exchange Rate (3 {rt} -> 2 Gold)!");
                                OnStateChanged?.Invoke();
                                return true;
                            }
                        }
                        return false;
                    }
                    else if (actionIndex == 2 && !p.TraderUsedPeddlingThisTurn)
                    {
                        var trTile = Board.Values.FirstOrDefault(t => t.Units.Any(u => u.Definition.Id == "capybara_trader"));
                        if (trTile == null || trTile == selectedTile) return false;
                        int dist = HexCoord.Distance(trTile.Coord, selectedTile.Coord);
                        int maxD = (trTile.EffectiveTerrain == TileTerrainType.Stream && selectedTile.EffectiveTerrain == TileTerrainType.Stream) ? 3 : 2;
                        if (dist > maxD || !CanAddUnitToTile(selectedTile, p.PlayerIndex)) return false;
                        var trader = trTile.Units.First(u => u.Definition.Id == "capybara_trader");
                        trTile.Units.Remove(trader);
                        selectedTile.Units.Add(trader);
                        p.TraderUsedPeddlingThisTurn = true;
                        Log($"Capybara Trader used Peddling (free move) from {trTile.Coord} to {selectedTile.Coord}.");
                    }
                    else if (actionIndex == 3 && CanAfford(p, new ResourceCost(gold: 1, any: 2)))
                    {
                        int wallows = Board.Values.Count(t => t.Building != null && t.Building.Type == BuildingType.Wallow);
                        if (wallows < 2) { Log("Need at least 2 Wallows on the board to summon The Riverkeeper!"); return false; }
                        var wTile = Board.Values.First(t => t.Building != null && t.Building.Type == BuildingType.Wallow);
                        SpendResources(p, new ResourceCost(gold: 1, any: 2));
                        SpawnUnit(wTile.Coord, "riverkeeper", p, 1);
                        Log($"{p.DisplayName} summoned The Riverkeeper at {wTile.Coord}!");
                    }
                    else if (actionIndex == 4)
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
                        int auth = hits / 2;
                        if (auth > 0)
                            AwardAuthority(p.PlayerIndex, auth, $"Thistle Barrage ({hits}HP dealt)");
                        Log($"Thistle Barrage dealt {hits}HP damage at {targetTile.Coord}!");
                    }
                    else if (actionIndex == 1 && p.BriaredFlowerThorns >= 1 && briarTile != null && p.ThistleBarrageUsesThisTurn < 2)
                    {
                        // Spend 1 Thorn to remove all isolated enemy buildings and tokens on a tile within 3 tiles (Line 882)
                        var structTile = Board.Values.FirstOrDefault(t =>
                            HexCoord.Distance(briarTile.Coord, t.Coord) <= 3 &&
                            ((t.Building != null && t.Building.OwnerPlayerIndex != p.PlayerIndex && !t.Units.Any(u => u.OwnerPlayerIndex == t.Building.OwnerPlayerIndex)) ||
                             t.Tokens.Any(tk => tk.OwnerPlayerIndex != p.PlayerIndex && tk.Type != TokenType.Coin && !t.Units.Any(u => u.OwnerPlayerIndex == tk.OwnerPlayerIndex))));
                        if (structTile == null) return false;
                        p.BriaredFlowerThorns--;
                        p.ThistleBarrageUsesThisTurn++;
                        int removed = 0;
                        if (structTile.Building != null && structTile.Building.OwnerPlayerIndex != p.PlayerIndex)
                        {
                            structTile.Building = null;
                            removed++;
                        }
                        removed += structTile.Tokens.RemoveAll(tk => tk.OwnerPlayerIndex != p.PlayerIndex && tk.Type != TokenType.Coin);
                        AwardAuthority(p.PlayerIndex, removed, "Thistle Barrage Structure Removal");
                    }
                    else if (actionIndex == 2 && briarTile != null && briarTile.Building != null && briarTile.Building.Type == BuildingType.PricklyThicket)
                    {
                        briarTile.Building = null;
                        p.BriaredFlowerThorns = Mathf.Min(6, p.BriaredFlowerThorns + 1);
                        Log("Blooming Briar consumed Prickly Thicket for +1 Thorn!");
                    }
                    else if (actionIndex == 3 && p.ResourcePile.Count >= 3)
                    {
                        SpendResources(p, new ResourceCost(any: 3));
                        p.BriaredFlowerThorns = Mathf.Min(6, p.BriaredFlowerThorns + 2);
                        Log($"{p.DisplayName} used Sun Eater -> Regrew +2 Thorns (now {p.BriaredFlowerThorns}/6).");
                    }
                    else if (actionIndex == 4)
                    {
                        int burst = 0;
                        bool removedAnyEnemy = false;
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
                                if (victim.CurrentHP <= 0) { RemoveUnitFromBoard(sTile, victim, p.PlayerIndex); removedAnyEnemy = true; }
                            }
                        }
                        if (burst == 0) return false;
                        if (removedAnyEnemy) AwardAuthority(p.PlayerIndex, 1, "Bursting Into Bloom Removal");
                        Log($"{p.DisplayName} activated Bursting Into Bloom ({burst} Thorned Seed(s) removed)!");
                    }
                    else if (actionIndex == 5 && p.BriaredFlowerThorns == 0 && briarTile != null)
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
                    else if (actionIndex == 6 && briarTile == null && p.ResourcePile.Count >= 2)
                    {
                        var ptTile = Board.Values.FirstOrDefault(t => t.Building != null && t.Building.Type == BuildingType.PricklyThicket);
                        int totalSeeds = Board.Values.Sum(t => t.Units.Count(u => u.Definition.Id == "thorned_seed"));
                        if (ptTile == null || totalSeeds < 3) return false;
                        SpendResources(p, new ResourceCost(any: 2));
                        int removedSeeds = 0;
                        foreach (var t in Board.Values)
                        {
                            while (removedSeeds < 3 && t.Units.Any(u => u.Definition.Id == "thorned_seed"))
                            {
                                t.Units.Remove(t.Units.First(u => u.Definition.Id == "thorned_seed"));
                                removedSeeds++;
                            }
                        }
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

        // =====================================================================
        // HELPERS: AUTHORITY, SPECIFIC RESOURCE GAIN (Part 2 Lines 55-58), DECKS, SPAWNING
        // =====================================================================

        public void AwardAuthority(int playerIndex, int amount, string reason)
        {
            if (playerIndex < 0 || playerIndex >= Players.Count || amount == 0) return;
            var p = Players[playerIndex];
            // Part 7 Line 165: Authority floor cannot go below 0
            p.Authority = Mathf.Max(0, p.Authority + amount);
            Log($"{p.DisplayName} gained +{amount} Authority ({reason}) -> Total: {p.Authority}/{VictoryAuthorityThreshold}.");

            if (p.Authority >= VictoryAuthorityThreshold && WinnerPlayerIndex < 0)
            {
                WinnerPlayerIndex = playerIndex;
                CurrentPhase = TurnPhase.GameOver;
                Log($"VICTORY! {p.DisplayName} ({p.Faction.Name}) reached {p.Authority} Authority and won the game!");
            }
        }

        public ResourceType DrawRandomResourceCard()
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

        /// <summary>
        /// Part 2 Lines 55-58: Gaining a specific resource "from the resource deck":
        /// - Search the resource deck for that card, add it to your Resource Pile, and shuffle the deck.
        /// - If no Gold is left when you would gain Gold, roll 1d6: odd -> gain 1 Stone, even -> gain 1 Wood.
        /// - If no Stone or no Wood is left when you would gain it, gain the other basic resource instead.
        /// </summary>
        public void GainSpecificResourceFromDeck(PlayerState p, ResourceType requested)
        {
            int idx = ResourceDeck.IndexOf(requested);
            if (idx >= 0)
            {
                ResourceDeck.RemoveAt(idx);
                Shuffle(ResourceDeck);
                p.ResourcePile.Add(requested);
                return;
            }

            if (requested == ResourceType.Gold)
            {
                int roll = RollD6();
                ResourceType fallback = (roll % 2 == 1) ? ResourceType.Stone : ResourceType.Wood;
                Log($"No Gold left in Resource Deck -> Rolled {roll} for fallback ({fallback}).");
                GainSpecificResourceFromDeck(p, fallback);
                return;
            }

            ResourceType otherBasic = (requested == ResourceType.Stone) ? ResourceType.Wood : ResourceType.Stone;
            int otherIdx = ResourceDeck.IndexOf(otherBasic);
            if (otherIdx >= 0)
            {
                ResourceDeck.RemoveAt(otherIdx);
                Shuffle(ResourceDeck);
                p.ResourcePile.Add(otherBasic);
            }
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
            for (int i = 0; i < gain.Wood; i++) GainSpecificResourceFromDeck(p, ResourceType.Wood);
            for (int i = 0; i < gain.Stone; i++) GainSpecificResourceFromDeck(p, ResourceType.Stone);
            for (int i = 0; i < gain.Gold; i++) GainSpecificResourceFromDeck(p, ResourceType.Gold);
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
                Log("Relic Deck empty -> Shuffled Discard Area to form a new Relic Deck.");
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

        public List<UnitInstance> SpawnUnit(HexCoord coord, string unitId, PlayerState owner, int count = 1, bool ignoreStackingLimit = false)
        {
            var list = new List<UnitInstance>();
            if (!Board.TryGetValue(coord, out var tile)) return list;
            var def = owner.Faction.Units.FirstOrDefault(u => u.Id == unitId);
            if (def == null) return list;

            int onBoardSupplyUsed = Board.Values.SelectMany(t => t.Units).Count(u => u.OwnerPlayerIndex == owner.PlayerIndex && u.Definition.Id == unitId);
            int availableSupply = Mathf.Max(0, def.SupplyCount - onBoardSupplyUsed);

            int sameFactionOnTile = tile.Units.Count(u => u.OwnerPlayerIndex == owner.PlayerIndex);
            int stackingRoom = ignoreStackingLimit ? 99 : Mathf.Max(0, MaxUnitsPerFactionPerTile - sameFactionOnTile);

            int toSpawn = Mathf.Min(count, Mathf.Min(availableSupply, stackingRoom));

            for (int i = 0; i < toSpawn; i++)
            {
                int bonusHp = (owner.Faction.Id == FactionId.CurlyTail && owner.CurlyTailMomentum >= 3) ? 1 : 0;
                var inst = new UnitInstance
                {
                    InstanceId = $"{unitId}_{_unitUidCounter++}",
                    OwnerPlayerIndex = owner.PlayerIndex,
                    Faction = owner.Faction.Id,
                    Definition = def,
                    CurrentHP = def.BaseMaxHP + bonusHp,
                    MaxHP = def.BaseMaxHP + bonusHp,
                    IsGuest = (tile.Building != null && tile.Building.Type == BuildingType.Wallow && tile.Building.OwnerPlayerIndex != owner.PlayerIndex)
                };
                tile.Units.Add(inst);
                list.Add(inst);
            }

            // Trouble Making Tanukis: Score Coin immediately if Shrine is built on a Coin or Coin is on tile with Shrine
            if (owner.Faction.Id == FactionId.TroubleMakingTanukis && tile.Building != null && tile.Building.Type == BuildingType.Shrine &&
                tile.Tokens.Any(t => t.Type == TokenType.Coin))
            {
                tile.Tokens.RemoveAll(t => t.Type == TokenType.Coin);
                AwardAuthority(owner.PlayerIndex, 2, "Coin Scored at Shrine");
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
                Faction = owner.Faction.Id
            };

            // Trouble Making Tanukis: Placing a Shrine on a tile with a Coin Token immediately scores the Coin for 2 Authority (Line 612)
            if (type == BuildingType.Shrine && tile.Tokens.Any(t => t.Type == TokenType.Coin))
            {
                tile.Tokens.RemoveAll(t => t.Type == TokenType.Coin);
                AwardAuthority(owner.PlayerIndex, 2, "Shrine Built on Coin Token");
            }
            // Capybara Caravan Watering Hole Case 2 (Line 820): Building a Wallow on a tile that already has enemy units makes them Guests
            if (type == BuildingType.Wallow)
            {
                foreach (var enemy in tile.Units.Where(u => u.OwnerPlayerIndex != owner.PlayerIndex))
                    enemy.IsGuest = true;
            }
        }

        public void PlaceToken(HexCoord coord, TokenType type, PlayerState owner)
        {
            if (!Board.TryGetValue(coord, out var tile) || type == TokenType.None) return;
            if (tile.Tokens.Count >= 2) return; // Max 2 tokens per tile (Part 4 Line 97)
            if (type == TokenType.Coin && tile.Tokens.Any(t => t.Type == TokenType.Coin)) return; // Two Coin Tokens can never be on the same tile (Line 607)

            tile.Tokens.Add(new TokenInstance
            {
                Type = type,
                OwnerPlayerIndex = owner.PlayerIndex,
                Faction = owner.Faction.Id,
                RemainingUses = 2
            });
        }

        public void PlaceOverlayTile(HexCoord coord, FactionOverlayTileType overlay, int ownerPlayerIndex)
        {
            if (!Board.TryGetValue(coord, out var tile)) return;
            if (tile.Overlay != FactionOverlayTileType.None) return; // A unique faction tile cannot replace another unique faction tile (Line 102)
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
