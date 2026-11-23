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

        // Faction-specific meters & state
        public int NecromouserCauldronDeadCount = 0;
        public int NecromouserSpecialSummonsUsed = 0; // Cap 4 per game
        public bool NecromouserAwakened = false;

        public int GoatPurgatoryCount = 0;

        public int CurlyTailThirstForGlory = 0; // 0-4
        public int CurlyTailHunger = 0;         // 0-4
        public int CurlyTailMomentum = 0;       // 0-4
        public bool CurlyTailWonBattleThisTurn = false;

        public int SquirrelsCraftedFirestarters = 0;

        public int PlatypusFaith = 0;           // 0-5
        public int PlatyPopeLevel = 0;          // 0-5
        public int CrusadeTurnsRemaining = 0;

        public int QueenRage = 0;               // 0-15
        public int QueenFeeding = 0;            // 0-7
        public int QueenStage = 1;              // 1-4

        public bool GlacierBlizzardActive = false;

        public int BriaredFlowerThorns = 6;     // 0-6
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
    /// - 40-card Relic Deck + 5-slot Excavation Area + Discard Pile
    /// - 50-card Resource Deck (20 Stone, 20 Wood, 10 Gold) + Burned Area
    /// - 4-Phase Turn Structure (1st Draw, 2nd Excavation, 3rd Main [3 actions], 4th End)
    /// - 5-Phase 2d6 Battle Calculator with Damage Cap & Mandatory Loser Damage
    /// </summary>
    public class CrittersGameEngine
    {
        public const int VictoryAuthorityThreshold = 30;
        public const int ExcavationAreaCapacity = 5;

        public List<PlayerState> Players = new List<PlayerState>();
        public Dictionary<HexCoord, HexTileState> Board = new Dictionary<HexCoord, HexTileState>();

        public List<RelicCardDef> RelicDeck = new List<RelicCardDef>();
        public List<RelicCardDef> RelicDiscardPile = new List<RelicCardDef>();
        public List<RelicCardDef> ExcavationArea = new List<RelicCardDef>();

        public List<ResourceType> ResourceDeck = new List<ResourceType>();
        public List<ResourceType> BurnedResources = new List<ResourceType>();

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
            if (GameLog.Count > 120)
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

        public void StartNewMatch(List<FactionId> chosenFactions, List<string> playerNames = null, int mapRadius = 4)
        {
            Players.Clear();
            Board.Clear();
            GameLog.Clear();
            WinnerPlayerIndex = -1;
            TurnNumber = 1;

            // 1. Build Hexagonal Continent Map (radius 4 = 61 hexes: Forest, Ruin, River)
            GenerateContinentHexMap(mapRadius);

            // 2. Build & Shuffle 40-Card Relic Deck and 50-Card Resource Deck (Rulebook Part 1)
            RelicDeck = RulebookDatabase.BuildFull40RelicDeck();
            Shuffle(RelicDeck);
            RelicDiscardPile.Clear();
            ExcavationArea.Clear();

            ResourceDeck = RulebookDatabase.BuildStandard50ResourceDeck();
            Shuffle(ResourceDeck);
            BurnedResources.Clear();

            // 3. Initialize 4 Players with their asymmetric Factions
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
            PerformCanonicalFactionSetups(mapRadius);

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
                    TileTerrainType terrain = TileTerrainType.Forest;

                    // Winding Anankian River through the Continent + scattered Ancient Ruins
                    if ((q == 0 && Mathf.Abs(r) <= radius - 1) || (r == 1 && q >= -2 && q <= 2))
                    {
                        terrain = TileTerrainType.River;
                    }
                    else if ((Mathf.Abs(q) == 2 && Mathf.Abs(r) == 0) ||
                             (q == 0 && r == 0) ||
                             (q == -2 && r == 2) ||
                             (q == 2 && r == -2) ||
                             (q == 1 && r == -3) ||
                             (q == -1 && r == 3))
                    {
                        terrain = TileTerrainType.Ruin;
                    }

                    Board[coord] = new HexTileState
                    {
                        Coord = coord,
                        BaseTerrain = terrain
                    };
                }
            }
        }

        private void PerformCanonicalFactionSetups(int radius)
        {
            // Quadrant spawn anchors at distance >= 3 from center and >= 4 from each other
            HexCoord[] spawnAnchors = new HexCoord[]
            {
                new HexCoord(-radius + 1, 0),
                new HexCoord(radius - 1, 0),
                new HexCoord(0, -radius + 1),
                new HexCoord(0, radius - 1)
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
                    Log($"{p.DisplayName} completed Capybara Caravan Special Setup (placed {placedRivers} connected River Tiles).");
                }
            }

            for (int i = 0; i < Players.Count; i++)
            {
                var p = Players[i];
                var homeCoord = spawnAnchors[i % spawnAnchors.Length];
                var homeTile = Board[homeCoord];
                homeTile.BaseTerrain = TileTerrainType.Forest; // Ensure legal forest start tile

                switch (p.Faction.Id)
                {
                    case FactionId.Necromouser:
                        // Place 3 Miasma Tiles facedown (not adjacent), start with 3 Skeletons on Cauldron
                        p.NecromouserCauldronDeadCount = 3;
                        PlaceOverlayTile(homeCoord, FactionOverlayTileType.Miasma_Hidden, p.PlayerIndex);
                        var m2 = new HexCoord(homeCoord.q / 2, homeCoord.r / 2);
                        if (Board.ContainsKey(m2)) PlaceOverlayTile(m2, FactionOverlayTileType.Miasma_Hidden, p.PlayerIndex);
                        Log($"{p.DisplayName} placed facedown Miasma Tiles and 3 Dead Skeletons on the Cauldron.");
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
                        // 3 Tanukis + 1 Shrine on home forest tile; 12 Coin Tokens spaced around forest tiles
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

            foreach (var tile in Board.Values)
            {
                tile.LootedThisTurn = false;
                foreach (var u in tile.Units)
                {
                    if (u.OwnerPlayerIndex == playerIndex)
                    {
                        u.CannotMoveThisTurn = false;
                        u.IsFrozen = false; // Frozen clears on your next draw phase (Line 1228)
                    }
                }
            }

            // 1st Phase: Draw Phase (Rulebook Part 5 Line 192)
            CurrentPhase = TurnPhase.DrawPhase;
            var drawn = DrawRelicCard();
            if (drawn != null)
            {
                p.RelicHand.Add(drawn);
                Log($"{p.DisplayName} began Turn {TurnNumber} (1st Draw Phase) and drew '{drawn.Name}'.");
            }

            // Faction Draw Phase triggers
            if (p.Faction.Id == FactionId.GoatCabal)
            {
                // Secrets of the Ancients: Mill top card of Grimoire
                p.GoatPurgatoryCount++;
                Log($"{p.DisplayName} (Goat Cabal) milled Grimoire -> +1 Purged Goat in Purgatory ({p.GoatPurgatoryCount}).");
            }
            else if (p.Faction.Id == FactionId.GlacierKingdom)
            {
                // Ice Age: Place 1 Glacier Tile adjacent to an existing Glacier Tile
                var existingGlacier = Board.Values.FirstOrDefault(t => t.Overlay == FactionOverlayTileType.Glacier);
                if (existingGlacier != null)
                {
                    var target = existingGlacier.Coord.GetNeighbors()
                        .FirstOrDefault(n => Board.ContainsKey(n) && Board[n].Overlay == FactionOverlayTileType.None);
                    if (Board.ContainsKey(target))
                    {
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

            // Automatically transition into 2nd Excavation Phase so player can Unearth/Craft/Assemble/Disassemble
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

            // Refill Excavation Area back up to 5 cards
            RefillExcavationArea();

            // Advance to next player clockwise
            int nextPlayer = (CurrentTurnPlayerIndex + 1) % Players.Count;
            if (nextPlayer == 0) TurnNumber++;
            BeginTurn(nextPlayer);
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
            // Cannot place buildings on river tiles except Capybara Wallow
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
                    p.ResourcePile.Add(DrawResourceCard());
                    p.ResourcePile.Add(DrawResourceCard());
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
            var movableUnits = fromTile.Units.Where(u => u.OwnerPlayerIndex == p.PlayerIndex && !u.CannotMoveThisTurn && !u.IsFrozen).ToList();
            if (movableUnits.Count == 0) return false;

            int dist = HexCoord.Distance(fromCoord, toCoord);
            int maxDist = 1;
            // Capybara Semiaquatic: 2 River Tiles
            if (p.Faction.Id == FactionId.CapybaraCaravan && fromTile.EffectiveTerrain == TileTerrainType.River && toTile.EffectiveTerrain == TileTerrainType.River)
                maxDist = 2;
            // Penguin Slip and Slide: 2 Glacier Tiles
            if (p.Faction.Id == FactionId.GlacierKingdom && fromTile.Overlay == FactionOverlayTileType.Glacier && toTile.Overlay == FactionOverlayTileType.Glacier)
                maxDist = 2;
            // Curly Tail Thirst for Glory Lv2+: +1 tile movement
            if (p.Faction.Id == FactionId.CurlyTail && p.CurlyTailThirstForGlory >= 2)
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
                u.IsGuest = false;
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

            MainPhaseActionsRemaining--;
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

            // Glacier Tile rule: only Penguins can loot Glacier Tiles (via Ice Fishing)
            if (tile.Overlay == FactionOverlayTileType.Glacier)
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
            var attackerUnits = tile.Units.Where(u => u.OwnerPlayerIndex == attacker.PlayerIndex && !u.IsHidden).ToList();
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
            }

            if (targetDefenderPlayerIndex < 0 || targetDefenderPlayerIndex == attacker.PlayerIndex)
                return null;

            var defender = Players[targetDefenderPlayerIndex];
            var defenderUnits = tile.Units.Where(u => u.OwnerPlayerIndex == defender.PlayerIndex && !u.IsHidden).ToList();

            // Part 12: How to Destroy Tokens and Buildings when no defending units are present
            // (Exception: Carnivorous Queen's Chrysalis participates in unit battles as a 0/2/2 unit)
            bool hasChrysalis = tile.Building != null && tile.Building.Type == BuildingType.Chrysalis && tile.Building.OwnerPlayerIndex == defender.PlayerIndex;
            if (defenderUnits.Count == 0 && !hasChrysalis)
            {
                int canDestroy = attackerUnits.Count;
                int destroyedCount = 0;
                if (tile.Building != null && tile.Building.OwnerPlayerIndex == defender.PlayerIndex && canDestroy > 0)
                {
                    Log($"{attacker.DisplayName} destroyed {defender.DisplayName}'s {tile.Building.Type} at {coord}!");
                    tile.Building = null;
                    canDestroy--;
                    destroyedCount++;
                    AwardAuthority(attacker.PlayerIndex, 1, "Destroyed Enemy Building");
                }
                while (canDestroy > 0 && tile.Tokens.Any(t => t.OwnerPlayerIndex == defender.PlayerIndex && t.Type != TokenType.Coin))
                {
                    var tk = tile.Tokens.First(t => t.OwnerPlayerIndex == defender.PlayerIndex && t.Type != TokenType.Coin);
                    tile.Tokens.Remove(tk);
                    canDestroy--;
                    destroyedCount++;
                    AwardAuthority(attacker.PlayerIndex, 1, $"Destroyed {tk.Type} Token");
                }
                if (destroyedCount > 0)
                {
                    MainPhaseActionsRemaining--;
                    OnStateChanged?.Invoke();
                }
                return null;
            }

            // Part 11: 5-Phase Unit Battle
            var report = new BattleReport
            {
                TileCoord = coord,
                AttackerPlayerIndex = attacker.PlayerIndex,
                DefenderPlayerIndex = defender.PlayerIndex
            };

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
            report.AttackerUnitAttackSum = attackerUnits.Sum(u => GetUnitBattleAttack(attacker, u));
            report.DefenderUnitDefenseSum = defenderUnits.Sum(u => GetUnitBattleDefense(defender, tile, u));
            if (hasChrysalis)
                report.DefenderUnitDefenseSum += 2; // Chrysalis is 0/2/2

            // Penguin Huddle: 3+ Penguins defending adds +1 total defense point
            if (defender.Faction.Id == FactionId.GlacierKingdom && defenderUnits.Count(u => u.Definition.Id == "penguin") >= 3)
                report.DefenderUnitDefenseSum += 1;

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
                int winnerAttackScore = winnerUnits.Sum(u => GetUnitBattleAttack(winner, u));
                int loserDefenseScore = loserUnits.Sum(u => GetUnitBattleDefense(loser, tile, u));
                int rawDamage = Mathf.Max(1, winnerAttackScore - loserDefenseScore);

                // Damage Cap: Per battle, each unit can only receive 1HP damage; cannot exceed number of winner's participating units
                int damageCap = Mathf.Max(1, winnerUnits.Count);
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

                // Apply damage simultaneously
                ApplyBattleDamageToSide(tile, loserUnits, finalWinnerDamage, winner.PlayerIndex, report);
                if (mandatoryLoserDmg > 0)
                    ApplyBattleDamageToSide(tile, winnerUnits, mandatoryLoserDmg, loser.PlayerIndex, report);

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

        private int GetUnitBattleAttack(PlayerState owner, UnitInstance u)
        {
            int atk = u.EffectiveAttack;
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
            return def;
        }

        private void ApplyBattleDamageToSide(HexTileState tile, List<UnitInstance> units, int totalDamage, int killerPlayerIndex, BattleReport report)
        {
            // Rulebook Part 11: Each unit can only lose 1HP per battle (prefer non-special units first to protect special unit)
            var ordered = units.OrderBy(u => u.Definition.IsSpecialUnit ? 1 : 0).ToList();
            int remaining = totalDamage;
            foreach (var u in ordered)
            {
                if (remaining <= 0) break;

                // Briared Flower Thorn Armor: Remove 1 Thorn instead of taking 1HP
                var owner = Players[u.OwnerPlayerIndex];
                if (owner.Faction.Id == FactionId.BriaredFlower && u.Definition.Id == "blooming_briar" && owner.BriaredFlowerThorns > 0)
                {
                    owner.BriaredFlowerThorns--;
                    remaining--;
                    Log($"Blooming Briar shed 1 Thorn via Thorn Armor ({owner.BriaredFlowerThorns} Thorns remaining).");
                    continue;
                }

                u.CurrentHP -= 1;
                remaining--;
                if (u.CurrentHP <= 0)
                {
                    report.DestroyedUnitNames.Add(u.Definition.Name);
                    RemoveUnitFromBoard(tile, u, killerPlayerIndex);
                }
            }
        }

        private void RemoveUnitFromBoard(HexTileState tile, UnitInstance u, int killerPlayerIndex)
        {
            tile.Units.Remove(u);
            var owner = Players[u.OwnerPlayerIndex];

            // Score Authority for killing Special Units (Rulebook Part 15)
            if (u.Definition.IsSpecialUnit && killerPlayerIndex >= 0 && killerPlayerIndex != u.OwnerPlayerIndex)
            {
                int authGain = 2;
                if (owner.Faction.Id == FactionId.Necromouser)
                {
                    if (u.Definition.Id == "the_bloat") authGain = 1;
                    else if (u.Definition.Id == "the_vanguard") authGain = 3;
                    else if (u.Definition.Id == "the_brute") authGain = 4;
                }
                else if (u.Definition.Id == "capybara_trader")
                {
                    authGain = 1;
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
                Log($"{queenPlayer.DisplayName}'s Carnivorous Queen evolved to Stage {newStage}!");
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
        // FACTION SPECIAL ABILITY SHORTCUTS (Free Actions during Main Phase)
        // =====================================================================

        public bool TryUseFactionSignatureAbility(HexCoord selectedCoord)
        {
            if (CurrentPhase != TurnPhase.MainPhase) return false;
            var p = Players[CurrentTurnPlayerIndex];

            switch (p.Faction.Id)
            {
                case FactionId.BriaredFlower:
                    // Thistle Barrage: Spend 1 Thorn + 1 Resource to deal 1HP damage to enemy within 4 tiles & spawn 1 Thorned Seed
                    if (p.BriaredFlowerThorns <= 0 || p.ResourcePile.Count == 0) return false;
                    var briarTile = Board.Values.FirstOrDefault(t => t.Units.Any(u => u.Definition.Id == "blooming_briar"));
                    if (briarTile == null) return false;
                    int bonusRange = Mathf.Min(2, briarTile.Units.Count(u => u.Definition.Id == "thorned_seed"));
                    if (!Board.TryGetValue(selectedCoord, out var targetTile)) return false;
                    if (HexCoord.Distance(briarTile.Coord, selectedCoord) > 4 + bonusRange) return false;
                    var enemy = targetTile.Units.FirstOrDefault(u => u.OwnerPlayerIndex != p.PlayerIndex);
                    if (enemy == null) return false;

                    p.BriaredFlowerThorns--;
                    ReturnResourceToDeck(p.ResourcePile, 0);
                    enemy.CurrentHP -= 1;
                    if (enemy.CurrentHP <= 0) RemoveUnitFromBoard(targetTile, enemy, p.PlayerIndex);
                    var seedForest = selectedCoord.GetNeighbors().FirstOrDefault(n => Board.ContainsKey(n) && Board[n].EffectiveTerrain == TileTerrainType.Forest);
                    if (Board.ContainsKey(seedForest)) SpawnUnit(seedForest, "thorned_seed", p, 1);
                    AwardAuthority(p.PlayerIndex, 1, "Thistle Barrage");
                    OnStateChanged?.Invoke();
                    return true;

                case FactionId.Necromouser:
                    // Arise / Necromancy: Spend 1 Dead from Cauldron to summon Skeleton or Special Unit
                    if (p.NecromouserCauldronDeadCount <= 0) return false;
                    var miasmaTile = Board.Values.FirstOrDefault(t => t.Overlay == FactionOverlayTileType.Miasma_Revealed || t.Overlay == FactionOverlayTileType.Miasma_Hidden);
                    if (miasmaTile == null) return false;
                    p.NecromouserCauldronDeadCount--;
                    SpawnUnit(miasmaTile.Coord, "skeleton", p, 1);
                    AwardAuthority(p.PlayerIndex, 1, "Necromancy Summon");
                    OnStateChanged?.Invoke();
                    return true;

                case FactionId.PlatypusPapacy:
                    // Alms (5 Faith -> +1 Platy-Pope Level & +1 Authority) or Indulgences (1 Gold -> +2 Faith)
                    if (p.PlatypusFaith >= 5)
                    {
                        p.PlatypusFaith = 0;
                        p.PlatyPopeLevel = Mathf.Min(5, p.PlatyPopeLevel + 1);
                        p.CrusadeTurnsRemaining += 2;
                        AwardAuthority(p.PlayerIndex, 1, $"Alms (Platy-Pope Level {p.PlatyPopeLevel} & Crusade!)");
                        OnStateChanged?.Invoke();
                        return true;
                    }
                    else if (p.ResourcePile.Contains(ResourceType.Gold))
                    {
                        RemoveResources(p, ResourceType.Gold, 1);
                        p.PlatypusFaith = Mathf.Min(5, p.PlatypusFaith + 2);
                        Log($"{p.DisplayName} used Indulgences (1 Gold -> +2 Faith, now {p.PlatypusFaith}/5).");
                        OnStateChanged?.Invoke();
                        return true;
                    }
                    return false;

                case FactionId.CarnivorousQueen:
                    // Hidden Amongst the Trees (2 Rage -> Hidden)
                    if (p.QueenRage < 2) return false;
                    var queen = Board.Values.SelectMany(t => t.Units).FirstOrDefault(u => u.Definition.Id == "carnivorous_queen");
                    if (queen == null) return false;
                    p.QueenRage -= 2;
                    queen.IsHidden = true;
                    Log($"{p.DisplayName}'s Carnivorous Queen used Hidden Amongst the Trees (2 Rage).");
                    OnStateChanged?.Invoke();
                    return true;

                default:
                    // Comeback Mechanic (Rulebook Part 7 Line 238): If a player has 0 units and 0 buildings, place building + 2 units
                    bool hasAnyPieces = Board.Values.Any(t => t.Units.Any(u => u.OwnerPlayerIndex == p.PlayerIndex) || (t.Building != null && t.Building.OwnerPlayerIndex == p.PlayerIndex));
                    if (!hasAnyPieces && Board.TryGetValue(selectedCoord, out var emptyForest) && emptyForest.EffectiveTerrain == TileTerrainType.Forest && emptyForest.Units.Count == 0)
                    {
                        PlaceBuilding(selectedCoord, p.Faction.PrimaryBuilding, p);
                        var basic = p.Faction.Units.FirstOrDefault(u => !u.IsSpecialUnit);
                        if (basic != null) SpawnUnit(selectedCoord, basic.Id, p, 2);
                        Log($"{p.DisplayName} triggered the Comeback Mechanic at {selectedCoord}!");
                        OnStateChanged?.Invoke();
                        return true;
                    }
                    return false;
            }
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

            for (int i = 0; i < count; i++)
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
