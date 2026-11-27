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
        public bool IsTrapped;             // held by a Trap Leaf (a debuff): can't move or be moved by effects
        public bool CleansedThisTurn;      // Papacy Level 2: debuffs removed (ignores Gluttony this turn)
        public int ClayEdictTurns;         // Traded Good Clay Edict: +1 tile movement while above 0
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
        /// <summary>For an unflipped Fallen Leaves tile: whether it is a Trap Leaf (known only to the Tanuki player).</summary>
        public bool LeafIsTrap = false;

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
        public bool HorseHairBrushReady;   // Traded Good discarded in a Main phase: the next unearth or assemble costs 1 less

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
        public int CrusadeTurnsRemaining = 0;   // Crusade turns still to come
        public bool CrusadeActive = false;      // this turn is a Crusade turn ("for your next 2 turns")
        public bool PlatyPopeOnBoard = true;

        // Faction 4: Trouble Making Tanukis
        // Fallen Leaves tiles in the Tanuki player's supply (13: true = Trap Leaf (5), false = Prank Leaf (8)).
        // Placing one takes a random tile from supply; removing one returns it here.
        public List<bool> FallenLeavesStack = new List<bool>();

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

        // Faction 9: The Goat Cabal
        public int GoatPurged = 0;                                   // Goats on the Purgatory card
        public List<FactionSubDeckCardDef> Grimoire = new List<FactionSubDeckCardDef>();
        public List<FactionSubDeckCardDef> GrimoireDiscard = new List<FactionSubDeckCardDef>();
        public string LastGrimoireText;
        public Dictionary<string, int> DemonStates = new Dictionary<string, int>(); // see CrittersGameEngine.Demon* constants
        public bool RoyalDecreeArmed;
        public HexCoord RoyalDecreeTile;
        public bool LoseMainActionNextTurn;                           // The Horned Beast unbound
        // A Horned Beast restriction placed on this player (Subservience): 1-4, for this many of their turns
        public int SubservienceRestriction;
        public int SubservienceTurnsLeft;
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

    public enum BattleDecisionKind
    {
        IronHalberd,       // Attacker, start of Attack and Defense Points (after the dice)
        WarHorn,           // Defender, start of Attack and Defense Points (after the dice)
        OvergrownWall,     // Defender, start of Attack and Defense Points (after the dice)
        PoliticalTreatise, // Defender, start of Attack and Defense Points (after the dice)
        BrassShield,       // Either side, Damage Calculation
        AssignDamage,      // The owner picks which of their units take the HP damage
        QuickFeet,         // Trouble Making Tanukis trait, after 0 HP units are removed
        CopperFigurine     // Traded Good: after the dice are rolled, re-roll 1 die
    }

    public class BattleDecision
    {
        public BattleDecisionKind Kind;
        public int PlayerIndex;
        public string Title;
        public string Prompt;
        /// <summary>For pick-one decisions (AssignDamage): labels and the unit each option stands for.</summary>
        public List<string> Options = new List<string>();
        public List<string> UnitIds = new List<string>();
    }

    public enum PendingBattleStage
    {
        RollDice,
        FigurineAttackerWindow,
        FigurineDefenderWindow,
        HalberdWindow,
        WarHornWindow,
        OvergrownWallWindow,
        TreatiseWindow,
        DamageCalculation,
        BrassAttackerWindow,
        BrassDefenderWindow,
        AssignDamage,
        ApplyDamage,
        QuickFeetWindow,
        Finish
    }

    /// <summary>A unit battle in progress, paused while a player decides whether to use a card or trait.</summary>
    public class PendingBattleState
    {
        public HexTileState Tile;
        public PlayerState Attacker;
        public PlayerState Defender;
        public List<UnitInstance> AttackerUnits;
        public List<UnitInstance> DefenderUnits;
        public BattleReport Report;
        public bool ConsumesMainPhaseAction;
        public PendingBattleStage Stage;
        public BattleDecision CurrentDecision;

        public int AttackerRoll;
        public int DefenderRoll;
        public int AttackerAttackPoints;
        public int AttackerDefensePoints;
        public int DefenderAttackPoints;
        public int DefenderDefensePoints;

        public bool AttackerWon;
        public int FinalWinnerDamage;
        public int MandatoryLoserDamage;
        public int DamageToAttacker;
        public int DamageToDefender;
        /// <summary>Units each side's owner chose to take damage first (Attacker side, Defender side).</summary>
        public List<UnitInstance> AttackerDamageOrder = new List<UnitInstance>();
        public List<UnitInstance> DefenderDamageOrder = new List<UnitInstance>();
        public int QuickFeetEligiblePlayerIndex = -1;
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
    /// - 5-Phase 3-sided die Battle Calculator with Damage Cap & Mandatory Loser Damage
    /// </summary>
    public partial class CrittersGameEngine
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
        public int TurnNumber = 1;   // counts every player's turn
        public int RoundNumber = 1;  // goes up each time play comes back around to the first player
        public TurnPhase CurrentPhase = TurnPhase.PreGame_DraftRoll;

        // Excavation Phase flags (once each in any order per turn)
        public bool UsedUnearthThisTurn;
        public bool UsedFactionCraftThisTurn;
        public bool UsedAssembleThisTurn;
        public bool UsedDisassembleThisTurn;

        // Main Phase actions (up to 3 per turn)
        public int MainPhaseActionsRemaining = 3;
        /// <summary>Extra Battle actions from Rally this turn (they don't count toward the three Main Phase actions).</summary>
        public int ExtraBattleActions = 0;

        public int WinnerPlayerIndex = -1;
        public List<string> GameLog = new List<string>();
        public BattleReport LastBattleReport;

        // A unit battle waiting on a player's decision (relic cards / Quick Feet). Other actions are blocked until it finishes.
        public PendingBattleState PendingBattle;
        public bool IsBattlePending => PendingBattle != null;

        private System.Random _rng;
        private int _unitUidCounter = 1;

        public event Action OnStateChanged;
        public event Action<BattleReport> OnBattleResolved;
        public event Action<BattleDecision> OnBattleDecisionRequired;

        public CrittersGameEngine(int seed = 0)
        {
            _rng = seed == 0 ? new System.Random() : new System.Random(seed);
        }

        private int _logTotal;
        public void Log(string message)
        {
            string entry = $"[T{TurnNumber}] {message}";
            GameLog.Add(entry);
            _logTotal++;
            if (GameLog.Count > 150)
                GameLog.RemoveAt(0);
            Debug.Log($"[CrittersEngine] {entry}");
        }

        public int RollD6() => _rng.Next(1, 7);
        /// <summary>The battle and roll-off die: one 3-sided die (1 to 3).</summary>
        public int RollD3() => _rng.Next(1, 4);

        /// <summary>
        /// Starts a match with factions already chosen and Player 1 going first (used by tests and quick starts).
        /// Online and local games use StartPreGame instead, which rolls for pick order, lets players pick
        /// factions, and rolls for turn order (Part 3 and Part 5).
        /// </summary>
        public void StartNewMatch(List<FactionId> chosenFactions, List<string> playerNames = null)
        {
            var names = new List<string>();
            for (int i = 0; i < chosenFactions.Count; i++)
                names.Add((playerNames != null && i < playerNames.Count) ? playerNames[i] : $"Player {i + 1}");
            PrepareTableAndPlayers(names);

            var allFactions = RulebookDatabase.GetAllFactions();
            for (int i = 0; i < chosenFactions.Count; i++)
                Players[i].Faction = allFactions.First(f => f.Id == chosenFactions[i]);

            FinishSetupAndBeginFirstTurn(0);
        }

        /// <summary>Clears everything, builds the 108-tile board, and seats the players (no factions yet).</summary>
        private void PrepareTableAndPlayers(List<string> names)
        {
            Players.Clear();
            Board.Clear();
            GameLog.Clear();
            WinnerPlayerIndex = -1;
            TurnNumber = 1;
            RoundNumber = 1;
            PendingBattle = null;
            ResetPreGameRolls();

            // 1. Build Exact 108-Tile Hexagonal Continent Map: 90 Forest, 14 Stream, 4 Ruin (Part 2 Line 43)
            GenerateCanonical108TileBoard();

            for (int i = 0; i < names.Count; i++)
            {
                Players.Add(new PlayerState
                {
                    PlayerIndex = i,
                    DisplayName = names[i],
                    Faction = null,
                    Authority = 0
                });
            }
        }

        /// <summary>
        /// Part 2 and Part 5, once factions and turn order are known: shuffle every deck, deal each player
        /// 2 resource cards and 1 relic card, fill the Excavation Area, run faction setup in reverse turn
        /// order, then start the first player's turn.
        /// </summary>
        private void FinishSetupAndBeginFirstTurn(int firstPlayerIndex, bool manualSetup = false)
        {
            // Build & Shuffle 40-Card Relic Deck and 50-Card Resource Deck (Part 2)
            RelicDeck = RulebookDatabase.BuildFull40RelicDeck();
            Shuffle(RelicDeck);
            RelicDiscardPile.Clear();
            ExcavationArea.Clear();

            ResourceDeck = RulebookDatabase.BuildStandard50ResourceDeck();
            Shuffle(ResourceDeck);
            BurnedResources.Clear();

            // Faction-Specific Decks (Forage 8, Traded Goods 8)
            ForageDeck = RulebookDatabase.BuildMillionsForageDeck8();
            Shuffle(ForageDeck);

            TradedGoodsDeck = RulebookDatabase.BuildCapybaraTradedGoodsDeck8();
            Shuffle(TradedGoodsDeck);
            TradedGoodsStall.Clear();
            TradedGoodsDiscard.Clear();
            RefillTradedGoodsStall();

            FirstPlayerIndex = firstPlayerIndex;
            for (int k = 0; k < Players.Count; k++)
            {
                var p = Players[(firstPlayerIndex + k) % Players.Count];
                p.ResourcePile.Clear();
                p.RelicHand.Clear();
                p.FallenLeavesStack.Clear();

                // Tanukis' 13 Fallen Leaves (8 Prank = false, 5 Trap = true)
                for (int pr = 0; pr < 8; pr++) p.FallenLeavesStack.Add(false);
                for (int tr = 0; tr < 5; tr++) p.FallenLeavesStack.Add(true);
                Shuffle(p.FallenLeavesStack);

                // Part 5: Draw 2 cards from resource deck into Resource Pile, 1 card from relic deck into Hand
                for (int r = 0; r < 2; r++)
                    p.ResourcePile.Add(DrawRandomResourceCard());
                var startRelic = DrawRelicCard();
                if (startRelic != null)
                    p.RelicHand.Add(startRelic);
            }

            // Part 5: Place top 5 cards of relic deck faceup in the Excavation Area
            RefillExcavationArea();

            if (!manualSetup)
            {
                PerformReverseOrderFactionSetups();
                BeginFirstTurn();
            }
        }

        private void BeginFirstTurn()
        {
            int firstPlayerIndex = FirstPlayerIndex;
            CurrentTurnPlayerIndex = firstPlayerIndex;
            Log($"{Players[firstPlayerIndex].DisplayName} takes the first turn. Play continues clockwise.");
            BeginTurn(firstPlayerIndex);
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

        // Well-separated quadrant start hexes used when setup is done automatically (tests and quick starts).
        private static readonly HexCoord[] AutoSetupAnchors = new HexCoord[]
        {
            new HexCoord(-4, 0),
            new HexCoord(4, 0),
            new HexCoord(0, -4),
            new HexCoord(0, 4)
        };

        /// <summary>Automatic setup: Capybara streams, every faction at a fixed anchor in reverse turn order, then Tanuki coins.</summary>
        private void PerformReverseOrderFactionSetups()
        {
            RunCapybaraSpecialSetup();
            for (int k = Players.Count - 1; k >= 0; k--)
            {
                int i = (FirstPlayerIndex + k) % Players.Count; // last player in turn order sets up first
                ApplyFactionSetup(Players[i], AutoSetupPicks(Players[i], AutoSetupAnchors[i % AutoSetupAnchors.Length]));
            }
            PlaceTanukiCoins();
        }

        /// <summary>
        /// Part 17 Faction 7: the Capybara Caravan places its 6 Stream Tiles before any other player sets up.
        /// Each Stream Tile goes on a forest tile next to another stream tile.
        /// </summary>
        public const int CapybaraStreamTiles = 6;    // Faction 7 Special Setup

        private void RunCapybaraSpecialSetup()
        {
            foreach (var p in Players.Where(pl => pl.Faction.Id == FactionId.CapybaraCaravan))
            {
                int placedStreams = 0;
                foreach (var tile in Board.Values)
                {
                    if (tile.BaseTerrain == TileTerrainType.Forest && tile.Overlay == FactionOverlayTileType.None && placedStreams < CapybaraStreamTiles)
                    {
                        bool adjStream = tile.Coord.GetNeighbors().Any(n => Board.ContainsKey(n) && Board[n].EffectiveTerrain == TileTerrainType.Stream);
                        if (adjStream && !AutoSetupAnchors.Contains(tile.Coord))
                        {
                            tile.Overlay = FactionOverlayTileType.CapybaraStream;
                            tile.OverlayOwnerPlayerIndex = p.PlayerIndex;
                            placedStreams++;
                        }
                    }
                }
                Log($"{p.DisplayName} completed Capybara Caravan Special Setup (placed {placedStreams} connected Stream Tiles onto Forest Tiles).");
            }
        }

        /// <summary>Tiles an automatic setup uses for a faction, starting from an anchor tile.</summary>
        private List<HexCoord> AutoSetupPicks(PlayerState p, HexCoord home)
        {
            var picks = new List<HexCoord> { home };
            switch (p.Faction.Id)
            {
                case FactionId.HordeOfTheMillions:
                {
                    var second = home.GetNeighbors().FirstOrDefault(n => Board.ContainsKey(n) && Board[n].EffectiveTerrain == TileTerrainType.Forest && Board[n].Units.Count == 0);
                    if (Board.ContainsKey(second)) picks.Add(second);
                    break;
                }
                case FactionId.Necromouser:
                {
                    var m2 = new HexCoord(home.q / 2, home.r / 2);
                    var m3 = new HexCoord(-home.q / 2, -home.r / 2);
                    if (Board.ContainsKey(m2) && HexCoord.Distance(home, m2) >= 2) picks.Add(m2);
                    if (Board.ContainsKey(m3) && HexCoord.Distance(home, m3) >= 2 && HexCoord.Distance(m2, m3) >= 2) picks.Add(m3);
                    break;
                }
                case FactionId.FirestartingSquirrels:
                    foreach (var n in home.GetNeighbors())
                        if (picks.Count < 3 && Board.ContainsKey(n) && Board[n].EffectiveTerrain == TileTerrainType.Forest && Board[n].Units.Count == 0)
                            picks.Add(n);
                    break;
                case FactionId.CapybaraCaravan:
                {
                    // The Capybara home tile must be a stream; make the anchor one if needed.
                    var homeTile = Board[home];
                    if (homeTile.EffectiveTerrain != TileTerrainType.Stream && homeTile.Overlay == FactionOverlayTileType.None)
                    {
                        homeTile.Overlay = FactionOverlayTileType.CapybaraStream;
                        homeTile.OverlayOwnerPlayerIndex = p.PlayerIndex;
                    }
                    break;
                }
            }
            return picks;
        }

        /// <summary>
        /// Places a faction's starting pieces (Part 17 "Setup") on the chosen tiles. The stacking limit is ignored
        /// during setup (Part 5). picks[0] is the main tile; Horde uses 2 tiles, Necro-mouser 3, Squirrels 3.
        /// </summary>
        private void ApplyFactionSetup(PlayerState p, List<HexCoord> picks)
        {
            var homeCoord = picks[0];
            switch (p.Faction.Id)
            {
                case FactionId.CurlyTail:
                    // Curly Tail, 4 Warband units, and 1 War Party on a forest or ruin tile >= 2 tiles from enemy units
                    PlaceBuilding(homeCoord, BuildingType.WarParty, p);
                    SpawnUnit(homeCoord, "curly_tail", p, 1, ignoreStackingLimit: true);
                    SpawnUnit(homeCoord, "warband", p, 4, ignoreStackingLimit: true);
                    break;

                case FactionId.HordeOfTheMillions:
                    // 2 Burrows on 2 separate forest tiles with no enemy units, 2 Millions units on each
                    foreach (var c in picks.Take(2))
                    {
                        PlaceBuilding(c, BuildingType.Burrow, p);
                        SpawnUnit(c, "millions", p, 2, ignoreStackingLimit: true);
                    }
                    break;

                case FactionId.PlatypusPapacy:
                    // The Citadel, 3 Pilgrims, and the Platy-Pope on a forest or stream tile with no enemy units
                    PlaceBuilding(homeCoord, BuildingType.TheCitadel, p);
                    SpawnUnit(homeCoord, "pilgrim", p, 3, ignoreStackingLimit: true);
                    SpawnUnit(homeCoord, "platy_pope", p, 1, ignoreStackingLimit: true);
                    p.PlatyPopeOnBoard = true;
                    break;

                case FactionId.TroubleMakingTanukis:
                    // 3 Tanuki units and 1 Shrine on a forest tile with no enemy units (Coins are placed after everyone's setup)
                    PlaceBuilding(homeCoord, BuildingType.Shrine, p);
                    SpawnUnit(homeCoord, "tanuki", p, 3, ignoreStackingLimit: true);
                    break;

                case FactionId.Necromouser:
                    // 3 Miasma Tiles facedown (not adjacent to each other), 3 Skeletons start on the Cauldron
                    p.NecromouserCauldronDeadCount = 3;
                    foreach (var c in picks.Take(3))
                        PlaceOverlayTile(c, FactionOverlayTileType.Miasma_Hidden, p.PlayerIndex);
                    break;

                case FactionId.FirestartingSquirrels:
                    // 1 Squirrel each on 3 adjacent forest tiles, 1 Bunker on one of them, 2 Charcoal, 1 crafted Acorn Firestarter
                    PlaceBuilding(homeCoord, BuildingType.Bunker, p);
                    foreach (var c in picks.Take(3))
                        SpawnUnit(c, "squirrel", p, 1, ignoreStackingLimit: true);
                    p.ResourcePile.Add(ResourceType.Charcoal);
                    p.ResourcePile.Add(ResourceType.Charcoal);
                    p.SquirrelsCraftedFirestarters = 1;
                    break;

                case FactionId.CapybaraCaravan:
                    // 3 Capybara units, the Capybara Trader, and 1 Wallow on a stream tile
                    PlaceBuilding(homeCoord, BuildingType.Wallow, p);
                    SpawnUnit(homeCoord, "capybara", p, 3, ignoreStackingLimit: true);
                    SpawnUnit(homeCoord, "capybara_trader", p, 1, ignoreStackingLimit: true);
                    break;

                case FactionId.GoatCabal:
                    // 4 Goats and a Summoning Circle; 1 Goat on Purgatory; 1 Gold; 5 Authority; the Grimoire shuffled
                    PlaceBuilding(homeCoord, BuildingType.SummoningCircle, p);
                    SpawnUnit(homeCoord, "goat", p, 4, ignoreStackingLimit: true);
                    p.GoatPurged = 1;
                    GainSpecificResourceFromDeck(p, ResourceType.Gold);
                    p.Authority = 5;
                    SetupGrimoire(p);
                    break;

                case FactionId.BriaredFlower:
                    // Blooming Briar with 6 Thorns, 2 Thorned Seeds, and 1 Prickly Thicket on a forest tile with no enemy pieces within 4 tiles
                    p.BriaredFlowerThorns = 6;
                    PlaceBuilding(homeCoord, BuildingType.PricklyThicket, p);
                    SpawnUnit(homeCoord, "blooming_briar", p, 1, ignoreStackingLimit: true);
                    SpawnUnit(homeCoord, "thorned_seed", p, 2, ignoreStackingLimit: true);
                    break;
            }

            Log($"[Setup] {p.DisplayName} set up {p.Faction.Name} at {string.Join(", ", picks)}.");
        }

        /// <summary>
        /// Part 5 and Part 17 Faction 4: after every other player has finished setup, the Tanuki player places
        /// 12 Coin Tokens on 12 different forest tiles with no enemy units, each at least 2 tiles from every other coin.
        /// </summary>
        private void PlaceTanukiCoins()
        {
            foreach (var tanukiPlayer in Players.Where(pl => pl.Faction.Id == FactionId.TroubleMakingTanukis))
            {
                var coinCoords = new List<HexCoord>();
                foreach (var tile in Board.Values)
                {
                    if (coinCoords.Count >= 12) break;
                    bool hasEnemyUnits = tile.Units.Any(u => u.OwnerPlayerIndex != tanukiPlayer.PlayerIndex);
                    bool hasShrine = tile.Building != null && tile.Building.Type == BuildingType.Shrine;
                    if (tile.EffectiveTerrain == TileTerrainType.Forest && !hasEnemyUnits && !hasShrine && !NearGoatUnit(tanukiPlayer.PlayerIndex, tile.Coord))
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
            ExtraBattleActions = 0;
            AbilityUses.Clear();
            p.CurlyTailWonBattleThisTurn = false;
            p.ThistleBarrageUsesThisTurn = 0;
            p.TraderUsedPeddlingThisTurn = false;

            // Crusade: "for your next 2 turns" (Part 17 Faction 3)
            p.CrusadeActive = p.CrusadeTurnsRemaining > 0;
            if (p.CrusadeActive)
            {
                p.CrusadeTurnsRemaining--;
                Log($"{p.DisplayName} is on Crusade this turn ({p.CrusadeTurnsRemaining} Crusade turn(s) left after this one).");
            }

            foreach (var tile in Board.Values)
            {
                tile.LootedThisTurn = false;
                foreach (var u in tile.Units)
                {
                    if (u.OwnerPlayerIndex != playerIndex) continue;
                    u.CleansedThisTurn = false;
                    if (u.ClayEdictTurns > 0) u.ClayEdictTurns--;
                    if (u.TrapLeafTurnsRemaining > 0)
                    {
                        u.TrapLeafTurnsRemaining--;   // Trap Leaf: still stuck during this, their next turn
                        u.CannotMoveThisTurn = true;
                        u.IsTrapped = true;
                    }
                    else
                    {
                        u.CannotMoveThisTurn = false;
                        u.IsTrapped = false;
                    }
                    u.HasMovedThisTurn = false;
                }
            }

            // 1. Draw Phase (Part 6). Prophetic Vision happens first, before the normal draw.
            CurrentPhase = TurnPhase.DrawPhase;
            Log($"{p.DisplayName} began turn {TurnNumber} (1. Draw Phase).");
            GoatStartOfDrawPhase(p);
            if (!OfferPropheticVision(p))
                FinishDrawPhaseDraw(p);
            OnStateChanged?.Invoke();
        }

        /// <summary>The normal Draw phase draw. The phase stays open only if the player has Draw-phase abilities to use.</summary>
        private void FinishDrawPhaseDraw(PlayerState p)
        {
            var drawn = DrawRelicCard();
            if (drawn != null)
            {
                p.RelicHand.Add(drawn);
                Log($"{p.DisplayName} drew '{drawn.Name}'.");
            }
            // Goat Cabal, Secrets of the Ancients: after drawing, mill the top Grimoire card (mandatory)
            if (p.Faction.Id == FactionId.GoatCabal) MillGrimoire(p, 1);
            if (PendingChoice != null) return;   // a Grimoire choice: the Draw phase continues after it
            if (!HasDrawPhaseOptions(p))
                EnterExcavationPhase(p);
        }

        public void AdvancePhase()
        {
            if (CurrentPhase == TurnPhase.GameOver) return;
            if (PendingBattle != null) return; // finish the battle first
            if (PendingChoice != null) return; // answer the choice first

            var p = Players[CurrentTurnPlayerIndex];
            if (CurrentPhase == TurnPhase.DrawPhase)
            {
                EnterExcavationPhase(p);
            }
            else if (CurrentPhase == TurnPhase.ExcavationPhase)
            {
                CurrentPhase = TurnPhase.MainPhase;
                MainPhaseActionsRemaining = 3;
                if (p.LoseMainActionNextTurn)
                {
                    p.LoseMainActionNextTurn = false;
                    MainPhaseActionsRemaining = 2;
                    Log($"{p.DisplayName} has only 2 Main phase actions (Unbound Horned Beast).");
                }

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
            Log($"{Players[CurrentTurnPlayerIndex].DisplayName} entered 4. End Phase.");
            _endPhaseIgniteAsked = false;
            _bindingsHandled.Clear();
            ContinueEndPhase();
        }

        /// <summary>The End phase, resumable after the Squirrels' Ignite choice.</summary>
        private void ContinueEndPhase()
        {
            var p = Players[CurrentTurnPlayerIndex];
            if (!_endPhaseIgniteAsked)
            {
                _endPhaseIgniteAsked = true;
                if (OfferIgnite(p)) { OnStateChanged?.Invoke(); return; }
            }

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
                    break;

                case FactionId.Necromouser:
                    // Undead Shackles: +1 Authority per 2 Dead in Cauldron (cap 2)
                    int shackleAuth = Mathf.Min(2, p.NecromouserCauldronDeadCount / 2);
                    if (shackleAuth > 0) AwardAuthority(p.PlayerIndex, shackleAuth, "Undead Shackles");
                    break;

                case FactionId.FirestartingSquirrels:
                {
                    int fires = Board.Values.Count(t => t.Overlay == FactionOverlayTileType.Fire && t.OverlayOwnerPlayerIndex == p.PlayerIndex);
                    int logBefore = GameLog.Count, totalBefore = _logTotal;
                    ResolveSquirrelsFireTilesEndPhase(p);
                    if (fires > 0)
                    {
                        int added = Mathf.Min(GameLog.Count, _logTotal - totalBefore);
                        var lines = GameLog.Skip(GameLog.Count - added).Select(l => " • " + System.Text.RegularExpressions.Regex.Replace(l, @"^\[T\d+\] ", ""));
                        Announce($"{p.DisplayName}'s {fires} Fire Tile(s) burn",
                            "Each Fire Tile resolves in order: 1. Squirrels on it clear out for Charcoal. 2. One building or token burns. " +
                            "3. Enemy units are driven to an adjacent tile (forest costs 1 HP). 4. On a 1 or 6 the fire spreads. 5. The top resource card burns (Gold gives 1 Authority).\n\n" +
                            string.Join("\n", lines));
                    }
                }
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
                    if (guestOwners > 0) AwardAuthority(p.PlayerIndex, guestOwners, "Customary Greeting");
                    break;

                case FactionId.GoatCabal:
                    if (ResolveDemonBindings(p)) { OnStateChanged?.Invoke(); return; }   // paused for a binding choice
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
            if (p.Faction.Id == FactionId.GoatCabal)
            {
                // Kleptomancy: keep up to 10 Gold and 5 other resources
                while (p.ResourcePile.Count(r => r == ResourceType.Gold) > 10) ReturnResourceToDeck(p.ResourcePile, p.ResourcePile.LastIndexOf(ResourceType.Gold));
                while (p.ResourcePile.Count(r => r != ResourceType.Gold) > 5) ReturnResourceToDeck(p.ResourcePile, p.ResourcePile.FindLastIndex(r => r != ResourceType.Gold));
            }
            else
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

            // Subservience restrictions last for the restricted player's next two turns
            if (p.SubservienceTurnsLeft > 0) p.SubservienceTurnsLeft--;

            // Advance to next player clockwise
            int nextPlayer = (CurrentTurnPlayerIndex + 1) % Players.Count;
            TurnNumber++;
            if (nextPlayer == FirstPlayerIndex) RoundNumber++;
            BeginTurn(nextPlayer);
        }

        private void ResolveSquirrelsFireTilesEndPhase(PlayerState squirrelPlayer)
        {
            // Part 17 Faction 6: resolve each Fire Tile one at a time, steps 1-5 in order.
            int me = squirrelPlayer.PlayerIndex;
            int totalEnemyDamageDealt = 0;
            int totalEnemiesDrivenOut = 0;
            var damagedThisEndPhase = new HashSet<string>();   // end phase damage cap: 1 HP per unit (Tar Pit extra excepted)
            var drivenOutThisEndPhase = new HashSet<string>(); // a unit is driven out at most once per end phase
            var fireTiles = Board.Values.Where(t => t.Overlay == FactionOverlayTileType.Fire && t.OverlayOwnerPlayerIndex == me).ToList();

            Func<UnitInstance, HexTileState, bool> Protected = (u, at) =>
                u.Definition.Id == "pyromaniac" ||                                         // Affinity
                (u.Faction == FactionId.FirestartingSquirrels && at.Building != null && at.Building.Type == BuildingType.Bunker);

            // Deals 1 HP from the resolution, respecting the cap; returns true if dealt
            Func<UnitInstance, HexTileState, bool, bool> Burn = (u, at, tarPitExtra) =>
            {
                if (Protected(u, at)) return false;
                int dmg = 0;
                if (!damagedThisEndPhase.Contains(u.InstanceId)) { dmg++; damagedThisEndPhase.Add(u.InstanceId); }
                if (tarPitExtra) dmg++;
                if (dmg == 0) return false;
                u.CurrentHP -= dmg;
                if (u.OwnerPlayerIndex != me) totalEnemyDamageDealt += dmg;
                if (u.CurrentHP <= 0) RemoveUnitFromBoard(at, u, u.OwnerPlayerIndex == me ? -1 : me);
                return true;
            };

            foreach (var ft in fireTiles)
            {
                if (ft.Overlay != FactionOverlayTileType.Fire) continue; // put out earlier this phase
                BeginRemovalEvent();
                bool tarPit = ft.Tokens.Any(tk => tk.Type == TokenType.TarPit);

                // 1. Clear Squirrels: return your Squirrels here to supply for 1 Charcoal each (Bunker squirrels are untouched)
                foreach (var sq in ft.Units.Where(u => u.OwnerPlayerIndex == me && u.Definition.Id == "squirrel" && !Protected(u, ft)).ToList())
                {
                    ft.Units.Remove(sq);
                    squirrelPlayer.ResourcePile.Add(ResourceType.Charcoal);
                }

                // 2. Burn structures: remove 1 building or token (never Tar Pits, never Coins); doubles burn every one on every Fire Tile
                if (ft.Building != null && !(ft.Building.Type == BuildingType.Bunker && ft.Building.OwnerPlayerIndex == me))
                    ft.Building = null;
                else
                {
                    var tk = ft.Tokens.FirstOrDefault(k => k.Type != TokenType.TarPit && k.Type != TokenType.Coin && k.OwnerPlayerIndex != me);
                    if (tk != null) ft.Tokens.Remove(tk);
                }

                // 3. Drive out enemies: each goes to an adjacent tile (the controller would pick; the game picks the
                //    least harmful: no Fire, then stream/ruin over forest). Forest costs 1 HP (cap applies).
                var drivenForest = new List<HexCoord>();
                foreach (var enemy in ft.Units.Where(u => u.OwnerPlayerIndex != me).ToList())
                {
                    if (drivenOutThisEndPhase.Contains(enemy.InstanceId) || Protected(enemy, ft) || !CanBeMovedByEnemyEffect(enemy))
                    {
                        // already driven out once (or can't be moved): still burns where it stands if on forest
                        if (ft.EffectiveTerrain == TileTerrainType.Forest) Burn(enemy, ft, false);
                        continue;
                    }
                    var dest = Neighbors(ft).Where(n => CanAddUnitToTile(n, enemy.OwnerPlayerIndex))
                        .OrderBy(n => n.Overlay == FactionOverlayTileType.Fire ? 1 : 0)
                        .ThenBy(n => n.EffectiveTerrain == TileTerrainType.Forest ? 1 : 0)
                        .FirstOrDefault();
                    if (dest == null) continue; // no legal tile: stays (Part 10)
                    RelocateUnits(ft, dest, new List<UnitInstance> { enemy });
                    drivenOutThisEndPhase.Add(enemy.InstanceId);
                    totalEnemiesDrivenOut++;
                    if (dest.Overlay == FactionOverlayTileType.Fire)
                        ApplyFireEntryDamage(dest, new List<UnitInstance> { enemy }, me); // entering another Fire Tile
                    if (dest.EffectiveTerrain == TileTerrainType.Forest)
                    {
                        if (!drivenForest.Contains(dest.Coord)) drivenForest.Add(dest.Coord);
                        if (dest.Units.Contains(enemy)) Burn(enemy, dest, tarPit);
                    }
                }

                // 4. Spread: on a 1 or 6, a new Fire Tile goes on a forest tile holding a driven-out enemy and deals 1 HP to every unit there
                int spreadRoll = RollD6();
                if ((spreadRoll == 1 || spreadRoll == 6) && FireTilesLeft(squirrelPlayer) > 0)
                {
                    var spread = drivenForest.Select(c => Board[c]).FirstOrDefault(t => CanTakeForestOverlay(t) && HasEnemyUnits(t, me));
                    if (spread != null)
                    {
                        PlaceFireTile(spread, me);
                        Log($"The fire spread (rolled {spreadRoll}) to {spread.Coord}!");
                        foreach (var u in spread.Units.ToList()) Burn(u, spread, false);
                    }
                }

                // 5. Burn the Deck: burn the top resource card; Gold gains 1 Authority
                if (ResourceDeck.Count > 0)
                {
                    var burned = DrawRandomResourceCard();
                    BurnedResources.Add(burned);
                    if (burned == ResourceType.Gold)
                        AwardAuthority(me, 1, "Burned Gold from the resource deck");
                }
            }

            // Flames of War: 1 Authority per 2 HP dealt to enemies and per 2 enemy units moved (max 4 per End phase)
            int flamesAuth = Mathf.Min(4, (totalEnemyDamageDealt / 2) + (totalEnemiesDrivenOut / 2));
            if (flamesAuth > 0)
                AwardAuthority(me, flamesAuth, "Flames of War");
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
            Announce($"{drawingPlayer.DisplayName} drew a Forage card", $"<b>{card.Title}</b>\n{card.EffectText}");

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

        /// <summary>Traveling Caravan: a player with a unit on the Capybara Trader's tile may buy from the stall.</summary>
        public bool CanBuyTradedGood(PlayerState buyer, int stallIndex, out string reason)
        {
            reason = null;
            if (CurrentPhase != TurnPhase.MainPhase || buyer.PlayerIndex != CurrentTurnPlayerIndex) { reason = "Only during your Main phase."; return false; }
            if (PendingBattle != null || PendingChoice != null) { reason = "Finish what's in progress first."; return false; }
            var capy = Players.FirstOrDefault(pl => pl.Faction != null && pl.Faction.Id == FactionId.CapybaraCaravan);
            if (capy == null || capy.PlayerIndex == buyer.PlayerIndex) { reason = "Only other players buy from the Capybara Trader."; return false; }
            if (stallIndex < 0 || stallIndex >= TradedGoodsStall.Count) { reason = "That stall slot is empty."; return false; }
            var trader = Board.Values.FirstOrDefault(t => t.Units.Any(u => u.OwnerPlayerIndex == capy.PlayerIndex && u.Definition.Id == "capybara_trader"));
            bool onTile = trader != null && trader.Units.Any(u => u.OwnerPlayerIndex == buyer.PlayerIndex);
            if (!onTile) { reason = "You need a unit on the Capybara Trader's tile."; return false; }
            if (!CanAfford(buyer, TradedGoodsStall[stallIndex].Cost)) { reason = $"Costs {TradedGoodsStall[stallIndex].Cost}."; return false; }
            return true;
        }

        public bool TryBuyTradedGoodFromCapybaraStall(int stallIndex)
        {
            var buyer = Players[CurrentTurnPlayerIndex];
            if (!CanBuyTradedGood(buyer, stallIndex, out var why)) { Log(why); return false; }
            BuyTradedGood(buyer, stallIndex);
            OnStateChanged?.Invoke();
            return true;
        }

        /// <summary>Pays for and takes stall slot stallIndex (both sides gain 1 Authority), then offers the guest placement.</summary>
        private void BuyTradedGood(PlayerState buyer, int stallIndex)
        {
            var capyPlayer = Players.First(pl => pl.Faction.Id == FactionId.CapybaraCaravan);
            var card = TradedGoodsStall[stallIndex];

            // Pay the cost into the discard (cheapest cards first)
            var order = new[] { ResourceType.Charcoal, ResourceType.Wood, ResourceType.Stone, ResourceType.Gold };
            for (int i = 0; i < card.Cost.AnyResource && buyer.ResourcePile.Count > 0; i++)
                buyer.ResourcePile.Remove(order.First(x => buyer.ResourcePile.Contains(x)));

            TradedGoodsStall.RemoveAt(stallIndex);
            buyer.PurchasedTradedGoods.Add(card);
            Log($"{buyer.DisplayName} bought {card.Title} from the Capybara Trader.");
            AwardAuthority(buyer.PlayerIndex, 1, $"Bought Traded Good '{card.Title}'");
            AwardAuthority(capyPlayer.PlayerIndex, 1, $"Sold Traded Good '{card.Title}'");

            // The buyer places 1 basic unit from their supply on a Wallow as a guest (they pick the Wallow)
            OfferGuestPlacement(buyer, capyPlayer);
        }

        /// <summary>
        /// The Capybara Trader arrives on a tile with enemy units: each of those players may buy one Traded Good
        /// before the Capybara player acts again.
        /// </summary>
        private void OfferTraderVisit(PlayerState capy, HexTileState tile)
        {
            if (TradedGoodsStall.Count == 0) return;
            foreach (var seat in tile.Units.Where(u => u.OwnerPlayerIndex != capy.PlayerIndex).Select(u => u.OwnerPlayerIndex).Distinct().OrderBy(x => x).ToList())
            {
                var buyer = Players[seat];
                var c = NewChoice("trader_visit", seat, "The Capybara Trader is here",
                    $"{capy.DisplayName}'s Capybara Trader arrived on your tile. Buy one Traded Good before {capy.DisplayName} acts again? (You and the Capybaras each gain 1 Authority.)");
                foreach (var g in TradedGoodsStall)
                    AddOption(c, $"Buy {g.Title} ({g.Cost}): {g.EffectText}", CanAfford(buyer, g.Cost));
                c.CanSkip = true;
                c.SkipLabel = "Don't buy anything";
            }
        }

        /// <summary>Things everyone at the table should see in a popup (Grimoire mills, Forage cards, Fire, ...).</summary>
        public event Action<string, string> OnAnnouncement;
        public void Announce(string title, string body) => OnAnnouncement?.Invoke(title, body);

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
            if (CurrentPhase != TurnPhase.ExcavationPhase || UsedUnearthThisTurn || PendingChoice != null) return false;
            if (RelicActionsBlocked(Players[CurrentTurnPlayerIndex])) { Log("Subservience: no unearthing this turn."); return false; }
            if (excavationIndex < 0 || excavationIndex >= ExcavationArea.Count) return false;

            var p = Players[CurrentTurnPlayerIndex];
            var card = ExcavationArea[excavationIndex];
            var cost = RelicCostFor(p, card);
            if (!CanAfford(p, cost)) return false;

            SpendResources(p, cost);
            UseHorseHairBrushIfDiscounted(p, card);
            ExcavationArea.RemoveAt(excavationIndex);
            UsedUnearthThisTurn = true;

            ResolveCraftedRelic(p, card, "Unearthed");
            OnStateChanged?.Invoke();
            return true;
        }

        public bool TryAssembleRelicFromHand(int handIndex)
        {
            if (CurrentPhase != TurnPhase.ExcavationPhase || UsedAssembleThisTurn || PendingChoice != null) return false;
            if (RelicActionsBlocked(Players[CurrentTurnPlayerIndex])) { Log("Subservience: no assembling this turn."); return false; }
            var p = Players[CurrentTurnPlayerIndex];
            if (handIndex < 0 || handIndex >= p.RelicHand.Count) return false;

            var card = p.RelicHand[handIndex];
            var cost = RelicCostFor(p, card);
            if (!CanAfford(p, cost)) return false;

            SpendResources(p, cost);
            UseHorseHairBrushIfDiscounted(p, card);
            p.RelicHand.RemoveAt(handIndex);
            UsedAssembleThisTurn = true;

            ResolveCraftedRelic(p, card, "Assembled");
            OnStateChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Disassemble (Part 6): discard 1 relic card from your Hand (or your Table, fromTable) for its listed resources.
        /// Looted Vault lets you pick Wood (woodOrStone 0) or Stone (1).
        /// </summary>
        public bool TryDisassembleRelicFromHand(int handIndex, bool fromTable = false, int woodOrStone = 0)
        {
            if (CurrentPhase != TurnPhase.ExcavationPhase || UsedDisassembleThisTurn || PendingChoice != null) return false;
            if (RelicActionsBlocked(Players[CurrentTurnPlayerIndex])) { Log("Subservience: no disassembling this turn."); return false; }
            var p = Players[CurrentTurnPlayerIndex];
            var pile = fromTable ? p.TableRelics : p.RelicHand;
            if (handIndex < 0 || handIndex >= pile.Count) return false;

            var card = pile[handIndex];
            pile.RemoveAt(handIndex);
            RelicDiscardPile.Add(card);
            UsedDisassembleThisTurn = true;

            if (card.DisassembleChoiceWoodOrStone)
            {
                var r = woodOrStone == 1 ? ResourceType.Stone : ResourceType.Wood;
                GainSpecificResourceFromDeck(p, r);
                Log($"{p.DisplayName} disassembled '{card.Name}' for 1 {r}.");
            }
            else
            {
                GrantResourceCostToPile(p, card.DisassembleGain);
                Log($"{p.DisplayName} disassembled '{card.Name}' for {card.DisassembleGain}.");
            }
            OnStateChanged?.Invoke();
            return true;
        }

        /// <summary>Part 8 Comeback Mechanic: no units and no buildings on the board (not the Papacy).</summary>
        public bool QualifiesForComeback(PlayerState p) =>
            p.Faction.Id != FactionId.PlatypusPapacy && p.Faction.PrimaryBuilding != BuildingType.None &&
            !Board.Values.Any(t => t.Units.Any(u => u.OwnerPlayerIndex == p.PlayerIndex) ||
                                   (t.Building != null && t.Building.OwnerPlayerIndex == p.PlayerIndex));

        /// <summary>Whether the player's recruitment building can be crafted on this tile now (Part 4 and Part 17 "Building").</summary>
        public bool IsLegalBuildingTile(PlayerState p, HexTileState t, out string reason)
        {
            reason = null;
            var type = p.Faction.PrimaryBuilding;
            int me = p.PlayerIndex;
            if (type == BuildingType.None) { reason = "Your faction has no building."; return false; }
            if (t.Building != null) { reason = "That tile already has a building."; return false; }
            if (t.Overlay == FactionOverlayTileType.Fire) { reason = "Nothing but Tar Pits can go on a Fire Tile."; return false; }
            if (BuildingSupplyLeft(p, type) <= 0) { reason = $"All your {type} pieces are on the board."; return false; }

            bool forest = t.EffectiveTerrain == TileTerrainType.Forest;
            bool stream = t.EffectiveTerrain == TileTerrainType.Stream;
            if (stream && type != BuildingType.TheCitadel && type != BuildingType.Wallow) { reason = "Buildings can't go on stream tiles."; return false; }

            if (QualifiesForComeback(p))
            {
                // Comeback: the building and 2 basic units go on a forest tile with no enemy units
                if (!forest) { reason = "Comeback: pick a forest tile."; return false; }
                if (HasEnemyUnits(t, me)) { reason = "Comeback: the tile must have no enemy units."; return false; }
                return true;
            }

            switch (type)
            {
                case BuildingType.WarParty:
                    if (!Has(t, p, "warband")) { reason = "A War Party goes on a tile with a Warband unit."; return false; }
                    return true;
                case BuildingType.Burrow:
                    if (!Has(t, p, "millions")) { reason = "A Burrow goes on a tile with a Millions unit."; return false; }
                    if (t.Units.Count(u => u.OwnerPlayerIndex != me) >= 3) { reason = "That tile has 3 or more enemy units."; return false; }
                    return true;
                case BuildingType.TheCitadel:
                    if (!forest && !stream) { reason = "The Citadel goes on a forest or stream tile."; return false; }
                    if (HasEnemyUnits(t, me)) { reason = "The Citadel needs a tile with no enemy units."; return false; }
                    return true;
                case BuildingType.Shrine:
                    if (!forest) { reason = "A Shrine goes on a forest or Fallen Leaves tile."; return false; }
                    if (!Has(t, p, "tanuki") && !Has(t, p, "band_of_troublemakers")) { reason = "A Shrine needs a Tanuki or the Band on the tile."; return false; }
                    return true;
                case BuildingType.Bunker:
                    if (!Has(t, p, "squirrel")) { reason = "A Bunker goes on a tile with a Squirrel."; return false; }
                    return true;
                case BuildingType.Wallow:
                    if (stream && Has(t, p, "capybara")) return true;
                    if (forest && Neighbors(t).Any(n => n.EffectiveTerrain == TileTerrainType.Stream && Has(n, p, "capybara"))) return true;
                    reason = "A Wallow goes on a stream tile with a Capybara, or a forest tile next to one.";
                    return false;
                case BuildingType.SummoningCircle:
                    if (t.Units.Count(u => u.OwnerPlayerIndex == me && u.Definition.Id == "goat") < 5) { reason = "A Summoning Circle needs 5 or more Goats on the tile."; return false; }
                    return true;
                case BuildingType.PricklyThicket:
                    if (!forest) { reason = "A Prickly Thicket goes on a forest tile."; return false; }
                    if (!Has(t, p, "thorned_seed") && !Has(t, p, "blooming_briar")) { reason = "A Prickly Thicket needs a Thorned Seed or the Briar on the tile."; return false; }
                    return true;
            }
            return true;
        }

        public List<HexCoord> LegalBuildingTiles(PlayerState p) =>
            Board.Values.Where(t => IsLegalBuildingTile(p, t, out _)).Select(t => t.Coord).ToList();

        public bool CanCraftBuildingNow(PlayerState p, out string reason)
        {
            reason = null;
            if (CurrentPhase != TurnPhase.ExcavationPhase) { reason = "Only in your Excavation phase."; return false; }
            if (p.Faction.PrimaryBuilding == BuildingType.None) { reason = "Your faction has no building."; return false; }
            bool comeback = QualifiesForComeback(p);
            if (!comeback && UsedFactionCraftThisTurn) { reason = "You already used your Craft action."; return false; }
            if (p.Faction.Id == FactionId.PlatypusPapacy)
            {
                if (Board.Values.Any(t => t.Building != null && t.Building.Type == BuildingType.TheCitadel && t.Building.OwnerPlayerIndex == p.PlayerIndex))
                { reason = "The Citadel is already on the board."; return false; }
                if (!CanAfford(p, new ResourceCost(wood: 2, stone: 2))) { reason = "Rebuilding the Citadel costs 2 Stone and 2 Wood."; return false; }
                return true;
            }
            if (!CanAfford(p, p.Faction.PrimaryBuildingCost)) { reason = $"Costs {p.Faction.PrimaryBuildingCost}."; return false; }
            return true;
        }

        public bool TryFactionCraftBuilding(HexCoord targetCoord)
        {
            if (PendingBattle != null || PendingChoice != null) return false;
            if (!Board.TryGetValue(targetCoord, out var tile)) return false;
            var p = Players[CurrentTurnPlayerIndex];
            if (!CanCraftBuildingNow(p, out var why) || !IsLegalBuildingTile(p, tile, out why)) { Log(why); return false; }

            bool comeback = QualifiesForComeback(p);
            var cost = p.Faction.Id == FactionId.PlatypusPapacy ? new ResourceCost(wood: 2, stone: 2) : p.Faction.PrimaryBuildingCost;
            SpendResources(p, cost);
            PlaceBuilding(targetCoord, p.Faction.PrimaryBuilding, p);

            if (comeback)
            {
                var basic = p.Faction.Units.FirstOrDefault(u => !u.IsSpecialUnit);
                if (basic != null) SpawnUnit(targetCoord, basic.Id, p, 2);
                Log($"{p.DisplayName} made a comeback at {targetCoord}: a {p.Faction.PrimaryBuilding} and 2 basic units (free craft).");
            }
            else
            {
                UsedFactionCraftThisTurn = true;
                if (p.Faction.Id == FactionId.GoatCabal) PurgeGoatFrom(tile, p);   // one of the 5 Goats is purged
                if (p.Faction.Id == FactionId.PlatypusPapacy)
                {
                    // Rebuilding: also place 1 Pilgrim and 1 Crusader
                    SpawnUnit(targetCoord, "pilgrim", p, 1);
                    SpawnUnit(targetCoord, "crusader", p, 1);
                }
                Log($"{p.DisplayName} crafted a {p.Faction.PrimaryBuilding} at {targetCoord}.");
            }

            OnStateChanged?.Invoke();
            return true;
        }

        /// <summary>Crafts equipment through the matching ability (Curly Tail forges in Excavation; Necro-mouser equips in Main).</summary>
        public bool TryCraftAndEquipFactionEquipment(string equipmentId)
        {
            var p = Players[CurrentTurnPlayerIndex];
            string forge = "forge:" + equipmentId, equip = "equip:" + equipmentId;
            if (GetFactionAbilities(p).Any(a => a.Id == forge))
                return UseFactionAbility(forge, default(HexCoord), default(HexCoord), 0);
            var targets = AbilityTargets(p, equip, null);
            return targets.Count > 0 && UseFactionAbility(equip, targets[0], default(HexCoord), 0);
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
                case "marble_aqueduct":
                case "underground_passage":
                case "hallow_ground":
                    OfferCraftedRelicChoice(p, card); // the player chooses (Part 16)
                    break;
                case "bejeweled_chalice":
                    GainSpecificResourceFromDeck(p, ResourceType.Gold);
                    var r = DrawRelicCard();
                    if (r != null) p.RelicHand.Add(r);
                    break;
                case "looted_vault":
                    for (int i = 0; i < 2; i++)
                    {
                        var drawn = DrawRelicCard();
                        if (drawn != null) p.RelicHand.Add(drawn);
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

        /// <summary>Destinations a Move action from this tile could reach with these units (for highlighting).</summary>
        public List<HexCoord> LegalMoveDestinations(HexCoord fromCoord, ICollection<string> onlyUnitIds = null) =>
            Board.Keys.Where(c => ValidateMove(fromCoord, c, onlyUnitIds, false, out _, out _)).ToList();

        private bool ValidateMove(HexCoord fromCoord, HexCoord toCoord, ICollection<string> onlyUnitIds, bool log,
                                  out List<UnitInstance> movableUnits, out int actionCost)
        {
            movableUnits = null;
            actionCost = 1;
            if (PendingBattle != null || PendingChoice != null) return false;
            if (CurrentPhase != TurnPhase.MainPhase || MainPhaseActionsRemaining <= 0) return false;
            if (!Board.TryGetValue(fromCoord, out var fromTile) || !Board.TryGetValue(toCoord, out var toTile)) return false;

            var p = Players[CurrentTurnPlayerIndex];
            movableUnits = fromTile.Units.Where(u =>
                u.OwnerPlayerIndex == p.PlayerIndex &&
                !u.CannotMoveThisTurn &&
                u.Definition.Id != "platy_pope" && // Platy-Pope is Unyielding (cannot move)
                !(u.Definition.Id == "blooming_briar" && u.HasMovedThisTurn) && // Rooted in Place: 1 tile per turn
                (onlyUnitIds == null || onlyUnitIds.Count == 0 || onlyUnitIds.Contains(u.InstanceId)) // "any number of your units"
            ).ToList();

            if (movableUnits.Count == 0) return false;

            // Part 10 Line 177: Stacking Limit (cannot move units onto a tile if it would put more than 6 of your units there)
            int existingFriendly = toTile.Units.Count(u => u.OwnerPlayerIndex == p.PlayerIndex);
            int maxMovable = MaxUnitsPerFactionPerTile - existingFriendly;
            if (maxMovable <= 0)
            {
                if (log) Log($"Stacking Limit (Part 10): Cannot move onto {toCoord} because you already have 6 units there!");
                return false;
            }
            if (movableUnits.Count > maxMovable)
                movableUnits = movableUnits.Take(maxMovable).ToList();

            // Goat Cabal Suspicion: enemies need at least as many units as the Goats on a Summoning Circle tile
            if (toTile.Building != null && toTile.Building.Type == BuildingType.SummoningCircle && toTile.Building.OwnerPlayerIndex != p.PlayerIndex)
            {
                int goats = toTile.Units.Count(u => u.OwnerPlayerIndex == toTile.Building.OwnerPlayerIndex && u.Definition.Id == "goat");
                if (movableUnits.Count < goats)
                {
                    if (log) Log($"Suspicion: you need at least {goats} units to move onto that Summoning Circle.");
                    return false;
                }
            }

            // Tar Pit Token check: costs 2 Main Phase actions for enemies to leave
            if (fromTile.Tokens.Any(t => t.Type == TokenType.TarPit) && p.Faction.Id != FactionId.FirestartingSquirrels)
            {
                if (MainPhaseActionsRemaining < 2)
                {
                    if (log) Log("Tar Pit Token requires 2 Main Phase actions to move off this tile!");
                    return false;
                }
                actionCost = 2;
            }

            // Gluttony Token check (Part 17 Faction 2 Line 491): blocks normal and Supply Line movement while a Millions unit is on tile
            if (fromTile.Tokens.Any(t => t.Type == TokenType.Gluttony) &&
                fromTile.Units.Any(u => u.Faction == FactionId.HordeOfTheMillions) &&
                p.Faction.Id != FactionId.HordeOfTheMillions &&
                !movableUnits.All(u => u.CleansedThisTurn))
            {
                if (log) Log("Gluttony Token debuff: Cannot use a Move action to leave this tile while a Millions unit is present!");
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
                if (AreTilesConnectedBySupplyLineNetwork(fromTile, toTile, p.PlayerIndex) && !fromTile.Coord.Equals(toTile.Coord))
                    maxDist = 99;
            }
            // Platypus Crusader Lv3+: Move action made only with Crusaders may move up to 2 tiles (Line 575)
            if (p.Faction.Id == FactionId.PlatypusPapacy && p.PlatyPopeLevel >= 3 && movableUnits.All(u => u.Definition.Id == "crusader"))
                maxDist = 2;
            // Leather Boots on The Brute: only when every unit moving has the extra movement
            if (movableUnits.All(u => u.EquippedItems.Any(e => e.BonusMove > 0)))
                maxDist = Mathf.Max(maxDist, 2);
            // Clay Edict: +1 tile movement when every moving unit has it
            if (movableUnits.All(u => u.ClayEdictTurns > 0))
                maxDist += 1;
            // Rooted in Place: the Blooming Briar only moves 1 tile
            if (movableUnits.Any(u => u.Definition.Id == "blooming_briar"))
                maxDist = 1;

            if (dist == 0 || dist > maxDist) return false;
            // The Horned Beast's Subservience, restriction 1: movements cost 1 more action
            if (HasRestriction(p, 1))
            {
                actionCost++;
                if (MainPhaseActionsRemaining < actionCost)
                {
                    if (log) Log($"Subservience: moving costs {actionCost} actions this turn.");
                    return false;
                }
            }
            return true;
        }

        public bool TryMoveUnits(HexCoord fromCoord, HexCoord toCoord, ICollection<string> onlyUnitIds = null)
        {
            if (!ValidateMove(fromCoord, toCoord, onlyUnitIds, true, out var movableUnits, out int actionCost)) return false;
            var p = Players[CurrentTurnPlayerIndex];
            var fromTile = Board[fromCoord];
            var toTile = Board[toCoord];

            foreach (var u in movableUnits)
            {
                fromTile.Units.Remove(u);
                toTile.Units.Add(u);
                u.HasMovedThisTurn = true;
                u.IsGuest = BecomesGuestOnArrival(toTile, u.OwnerPlayerIndex);   // leaving its tile ends guest status
            }
            if (movableUnits.Any(u => u.Definition.Id == "riverkeeper")) WelcomeGuestsAtRiverkeeper(toTile);

            // Trouble Making Tanukis: The Coin Hunt (compulsory - Line 614) & Shrine Coin Scoring (+2 Authority - Line 612)
            if (p.Faction.Id == FactionId.TroubleMakingTanukis)
            {
                MoveAndScoreTanukiCoin(fromTile, toTile, p.PlayerIndex);
            }

            // Fallen Leaves Flipping when an enemy group ends a Move action on an unflipped Fallen Leaves tile (Lines 626-629)
            if (toTile.Overlay == FactionOverlayTileType.FallenLeaf_Unflipped && p.Faction.Id != FactionId.TroubleMakingTanukis)
            {
                var tanukiOwner = Players.FirstOrDefault(pl => pl.Faction.Id == FactionId.TroubleMakingTanukis);
                bool isTrap = toTile.LeafIsTrap; // decided when the tile was placed from the Tanuki player's supply

                if (isTrap)
                {
                    toTile.Overlay = FactionOverlayTileType.FallenLeaf_Trap;
                    // "all enemy units on this tile cannot move, and cannot be moved by effects, for the rest of this
                    // turn and during that enemy's next turn" (the enemy is the player who moved onto it)
                    foreach (var u in toTile.Units.Where(x => x.OwnerPlayerIndex == p.PlayerIndex))
                    {
                        u.CannotMoveThisTurn = true;
                        u.IsTrapped = true;
                        u.TrapLeafTurnsRemaining = 1;
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

            // Entering a Fire Tile: 1 of the moving player's units takes 1 HP (Faction 6)
            if (p.Faction.Id != FactionId.FirestartingSquirrels)
                ApplyFireEntryDamage(toTile, movableUnits, p.PlayerIndex);

            // Martyrdom (Faction 3): Pilgrims that end a Move action on a tile with enemy units MAY be removed
            if (p.Faction.Id == FactionId.PlatypusPapacy && toTile.Units.Any(u => u.OwnerPlayerIndex != p.PlayerIndex) &&
                movableUnits.Any(u => u.Definition.Id == "pilgrim" && toTile.Units.Contains(u)))
                OfferMartyrdom(p, toTile);

            // The Capybara Trader arriving on a tile with enemy units opens its stall to them
            if (p.Faction.Id == FactionId.CapybaraCaravan && toTile.Units.Any(u => u.OwnerPlayerIndex != p.PlayerIndex) &&
                movableUnits.Any(u => u.Definition.Id == "capybara_trader" && toTile.Units.Contains(u)))
                OfferTraderVisit(p, toTile);

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

        /// <summary>Could the turn player Loot this tile right now? (Used for the green tiles too.)</summary>
        public bool CanLootTile(HexCoord coord)
        {
            if (PendingBattle != null || PendingChoice != null) return false;
            if (CurrentPhase != TurnPhase.MainPhase || MainPhaseActionsRemaining <= 0) return false;
            if (!Board.TryGetValue(coord, out var tile) || tile.LootedThisTurn) return false;

            var p = Players[CurrentTurnPlayerIndex];
            bool hasMyUnits = tile.Units.Any(u => u.OwnerPlayerIndex == p.PlayerIndex);
            bool hasEnemyPieces = tile.Units.Any(u => u.OwnerPlayerIndex != p.PlayerIndex) ||
                                  (tile.Building != null && tile.Building.OwnerPlayerIndex != p.PlayerIndex) ||
                                  tile.Tokens.Any(tk => tk.OwnerPlayerIndex != p.PlayerIndex && tk.Type != TokenType.Coin);
            if (!hasMyUnits || hasEnemyPieces) return false;
            // Fire Tiles: only the Squirrels can loot them, and only for Charcoal.
            if (tile.Overlay == FactionOverlayTileType.Fire)
                return p.Faction.Id == FactionId.FirestartingSquirrels && tile.Units.Any(u => u.OwnerPlayerIndex == p.PlayerIndex && u.Definition.Id == "squirrel") &&
                       p.ResourcePile.Count(r => r == ResourceType.Charcoal) + BurnedResources.Count(r => r == ResourceType.Charcoal) < 6;
            return true;
        }

        public bool TryLootTile(HexCoord coord)
        {
            if (!CanLootTile(coord)) return false;
            var tile = Board[coord];
            var p = Players[CurrentTurnPlayerIndex];

            if (tile.Overlay == FactionOverlayTileType.Fire)
            {
                p.ResourcePile.Add(ResourceType.Charcoal);
                tile.LootedThisTurn = true;
                MainPhaseActionsRemaining--;
                Log($"{p.DisplayName} looted the Fire Tile at {coord} -> Gained 1 Charcoal.");
                OnStateChanged?.Invoke();
                return true;
            }

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
            if (PendingBattle != null || PendingChoice != null) return false;
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

        // =====================================================================
        // PART 12 BATTLES (staged so players choose whether to use relic cards)
        // =====================================================================

        /// <summary>
        /// Starts a battle on a tile. Part 13 removals (isolated buildings, tokens, tiles) resolve immediately.
        /// Unit battles run as a staged PendingBattle: whenever a player holds a relic card (or trait) that
        /// "may" be used at the current battle timing, the engine pauses, raises OnBattleDecisionRequired,
        /// and waits for RespondToBattleDecision(bool). Returns the report if the battle finished synchronously,
        /// otherwise null (watch OnBattleResolved).
        /// </summary>
        public BattleReport TryBattleOnTile(HexCoord coord, int targetDefenderPlayerIndex = -1, bool consumesMainPhaseAction = true)
        {
            if (PendingBattle != null || PendingChoice != null) return null;
            if (CurrentPhase != TurnPhase.MainPhase) return null;
            if (consumesMainPhaseAction && MainPhaseActionsRemaining <= 0 && ExtraBattleActions <= 0) return null;
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

            // Vanguard Devotion (Line 688): only when the Necromouser himself is the one defending in this battle
            // and a Vanguard is in range (on a tile adjacent to the battle tile). The Vanguard and any other of
            // the Necromouser's units on the Vanguard's tile join the defense.
            if (defender.Faction.Id == FactionId.Necromouser && defenderUnits.Any(u => u.Definition.Id == "necromouser"))
            {
                foreach (var nCoord in coord.GetNeighbors())
                {
                    if (Board.TryGetValue(nCoord, out var adjTile) &&
                        adjTile.Units.Any(u => u.OwnerPlayerIndex == defender.PlayerIndex && u.Definition.Id == "the_vanguard"))
                    {
                        foreach (var ally in adjTile.Units.Where(u => u.OwnerPlayerIndex == defender.PlayerIndex))
                            if (!defenderUnits.Contains(ally)) defenderUnits.Add(ally);
                        Log($"The Vanguard's Devotion triggered from {nCoord} to defend The Necromouser!");
                    }
                }
            }

            // Watering Hole: a unit that attacks the Capybara Trader or the Riverkeeper stops being a guest
            if (defenderUnits.Any(u => u.Definition.Id == "capybara_trader" || u.Definition.Id == "riverkeeper"))
                EndGuestStatus(attackerUnits, "attacked the Capybara Caravan's host");

            // Part 13: Removing Isolated Enemy Buildings, Tokens, and Removable Tiles (no dice rolled!)
            if (defenderUnits.Count == 0)
            {
                int canRemove = attackerUnits.Count;
                int removedCount = 0;
                int bonusCrusadeAuth = (attacker.Faction.Id == FactionId.PlatypusPapacy && attacker.CrusadeActive) ? 1 : 0;

                if (tile.Building != null && tile.Building.OwnerPlayerIndex == defender.PlayerIndex && canRemove > 0)
                {
                    Log($"{attacker.DisplayName} removed isolated {tile.Building.Type} at {coord}!");
                    if (tile.Building.Type == BuildingType.Wallow) EndGuestStatus(attackerUnits, "destroyed a Wallow");
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
                    canRemove--;
                    removedCount++;
                    AwardAuthority(attacker.PlayerIndex, 1, $"Removed Isolated {oldOverlay}");
                    if (defender.Faction.Id == FactionId.Necromouser)
                        defender.NecromouserCauldronDeadCount++;
                    if (defender.Faction.Id == FactionId.TroubleMakingTanukis &&
                        (oldOverlay == FactionOverlayTileType.FallenLeaf_Trap || oldOverlay == FactionOverlayTileType.FallenLeaf_Prank))
                    {
                        defender.FallenLeavesStack.Add(wasTrap); // back to the Tanuki player's supply
                    }
                }

                if (removedCount > 0)
                {
                    if (consumesMainPhaseAction) SpendBattleAction();
                    OnStateChanged?.Invoke();
                }
                return null;
            }

            // Part 12: 5-Phase Unit Battle (Phase 1: Attack Declaration is complete at this point)
            PendingBattle = new PendingBattleState
            {
                Tile = tile,
                Attacker = attacker,
                Defender = defender,
                AttackerUnits = attackerUnits,
                DefenderUnits = defenderUnits,
                ConsumesMainPhaseAction = consumesMainPhaseAction,
                Stage = PendingBattleStage.RollDice,
                Report = new BattleReport
                {
                    TileCoord = coord,
                    AttackerPlayerIndex = attacker.PlayerIndex,
                    DefenderPlayerIndex = defender.PlayerIndex
                }
            };
            Log($"{attacker.DisplayName} declared an attack on {defender.DisplayName} at {coord}.");

            ContinuePendingBattle();
            return PendingBattle == null ? LastBattleReport : null;
        }

        /// <summary>
        /// Answers the current battle decision ("use this card/trait?") for the player named in
        /// PendingBattle.CurrentDecision, then continues the battle.
        /// </summary>
        public void RespondToBattleDecision(bool use) => RespondToBattleDecision(use ? 1 : 0);

        /// <summary>answer: 1 use, 0 pass, 2+ picks option (answer - 2) of a pick-one decision.</summary>
        public void RespondToBattleDecision(int answer)
        {
            var pb = PendingBattle;
            if (pb == null || pb.CurrentDecision == null) return;
            var decision = pb.CurrentDecision;
            pb.CurrentDecision = null;
            var player = Players[decision.PlayerIndex];
            bool use = answer == 1;

            if (decision.Kind == BattleDecisionKind.AssignDamage)
            {
                bool atkSide = player == pb.Attacker;
                var order = atkSide ? pb.AttackerDamageOrder : pb.DefenderDamageOrder;
                var side = atkSide ? pb.AttackerUnits : pb.DefenderUnits;
                int pick = answer >= 2 && answer - 2 < decision.UnitIds.Count ? answer - 2 : 0;
                var unit = side.FirstOrDefault(u => decision.UnitIds.Count > 0 && u.InstanceId == decision.UnitIds[pick]);
                if (unit != null)
                {
                    order.Add(unit);
                    Log($"{player.DisplayName} assigned 1HP to {unit.Definition.Name}.");
                }
                else order.Add(side.First(u => !order.Contains(u)));
                ContinuePendingBattle();
                return;
            }

            if (!use)
            {
                Log($"{player.DisplayName} chose not to use {decision.Title}.");
            }
            else
            {
                switch (decision.Kind)
                {
                    case BattleDecisionKind.WarHorn:
                        if (DiscardTableRelic(player, "war_horn"))
                        {
                            var defBasic = player.Faction.Units.FirstOrDefault(u => !u.IsSpecialUnit);
                            if (defBasic != null)
                            {
                                var hornSpawned = SpawnUnit(pb.Tile.Coord, defBasic.Id, player, 1);
                                pb.DefenderUnits.AddRange(hornSpawned);
                                Log($"{player.DisplayName} discarded War Horn -> placed {hornSpawned.Count} {defBasic.Name} into the defending battle!");
                            }
                        }
                        break;
                    case BattleDecisionKind.OvergrownWall:
                        if (DiscardTableRelic(player, "overgrown_wall"))
                        {
                            pb.Report.AttackerDiceBonus -= 2;
                            Log($"{player.DisplayName} discarded Overgrown Wall (-2 to Attacker dice roll).");
                        }
                        break;
                    case BattleDecisionKind.IronHalberd:
                        if (DiscardTableRelic(player, "iron_halberd"))
                        {
                            pb.Report.AttackerDiceBonus += 2;
                            Log($"{player.DisplayName} discarded Iron Halberd (+2 to Attacker dice roll).");
                        }
                        break;
                    case BattleDecisionKind.PoliticalTreatise:
                        if (DiscardTableRelic(player, "political_treatise"))
                        {
                            pb.Report.DefenderDiceBonus += 2;
                            Log($"{player.DisplayName} discarded Political Treatise (+2 to Defender dice roll).");
                        }
                        break;
                    case BattleDecisionKind.BrassShield:
                        if (DiscardTableRelic(player, "brass_shield"))
                        {
                            bool atk = player == pb.Attacker;
                            int before = atk ? pb.DamageToAttacker : pb.DamageToDefender;
                            int after = Mathf.Max(0, before - 2);
                            if (atk) pb.DamageToAttacker = after; else pb.DamageToDefender = after;
                            Log($"{player.DisplayName} discarded Brass Shield (damage taken {before}HP -> {after}HP).");
                        }
                        break;
                    case BattleDecisionKind.QuickFeet:
                        ExecuteQuickFeet(pb, player);
                        break;
                    case BattleDecisionKind.CopperFigurine:
                    {
                        var fig = player.PurchasedTradedGoods.FirstOrDefault(g => g.Id == "tg_copper_figurine");
                        if (fig == null) break;
                        player.PurchasedTradedGoods.Remove(fig);
                        TradedGoodsDiscard.Add(fig);
                        var r = pb.Report;
                        int roll = RollD3();
                        if (player == pb.Attacker) pb.AttackerRoll = r.AttackerDice1 = roll;
                        else pb.DefenderRoll = r.DefenderDice1 = roll;
                        Log($"{player.DisplayName} used the Copper Figurine and re-rolled a {roll}.");
                        RecomputeBattlePoints(pb);
                        break;
                    }
                }
            }

            ContinuePendingBattle();
        }

        private bool HasTableRelic(PlayerState p, string relicId) => p.TableRelics.Any(r => r.Id == relicId);

        /// <summary>A Battle action uses an Rally extra Battle action first, otherwise one of the three Main Phase actions.</summary>
        private void SpendBattleAction()
        {
            if (ExtraBattleActions > 0) ExtraBattleActions--;
            else MainPhaseActionsRemaining = Mathf.Max(0, MainPhaseActionsRemaining - 1);
        }

        /// <summary>Table relics the turn player may use right now on their own turn (Rally, Ceremonial Bow and Arrow).</summary>
        public bool CanUseTableRelic(PlayerState p, string relicId)
        {
            if (PendingBattle != null || IsPreGame || CurrentPhase == TurnPhase.GameOver) return false;
            if (p.PlayerIndex != CurrentTurnPlayerIndex || !HasTableRelic(p, relicId)) return false;
            switch (relicId)
            {
                case "abandoned_road": return CurrentPhase == TurnPhase.MainPhase; // "During your Main phase"
                case "ceremonial_bow_and_arrow": return Players.Any(o => o.PlayerIndex != p.PlayerIndex && o.TableRelics.Count > 0); // "During your turn"
            }
            return false;
        }

        /// <summary>
        /// Use a relic card from your Table on your own turn.
        /// Rally: discard it to take 1 extra Battle action (not one of your three).
        /// Ceremonial Bow and Arrow: discard it to make another player discard a relic card from their Table
        /// (targetPlayer, targetIndex). Cards on a Table are facedown, so the target is picked by position.
        /// </summary>
        public bool UseTableRelic(int seat, string relicId, int targetPlayer = -1, int targetIndex = -1)
        {
            if (seat < 0 || seat >= Players.Count) return false;
            var p = Players[seat];
            if (!CanUseTableRelic(p, relicId)) return false;

            if (relicId == "abandoned_road")
            {
                DiscardTableRelic(p, relicId);
                ExtraBattleActions++;
                Log($"{p.DisplayName} discarded Rally for 1 extra Battle action this turn.");
            }
            else if (relicId == "ceremonial_bow_and_arrow")
            {
                if (targetPlayer < 0 || targetPlayer >= Players.Count || targetPlayer == seat) return false;
                var victim = Players[targetPlayer];
                if (targetIndex < 0 || targetIndex >= victim.TableRelics.Count) return false;
                DiscardTableRelic(p, relicId);
                var lost = victim.TableRelics[targetIndex];
                victim.TableRelics.RemoveAt(targetIndex);
                RelicDiscardPile.Add(lost);
                Log($"{p.DisplayName} used Ceremonial Bow and Arrow: {victim.DisplayName} discarded '{lost.Name}' from their Table.");
            }
            else return false;

            OnStateChanged?.Invoke();
            return true;
        }

        private bool DiscardTableRelic(PlayerState p, string relicId)
        {
            var card = p.TableRelics.FirstOrDefault(r => r.Id == relicId);
            if (card == null) return false;
            p.TableRelics.Remove(card);
            RelicDiscardPile.Add(card);
            return true;
        }

        private void AskBattleDecision(BattleDecisionKind kind, PlayerState player, string title, string prompt)
        {
            var decision = new BattleDecision { Kind = kind, PlayerIndex = player.PlayerIndex, Title = title, Prompt = prompt };
            PendingBattle.CurrentDecision = decision;
            Log($"Waiting for {player.DisplayName}: use {title}?");
            OnBattleDecisionRequired?.Invoke(decision);
            OnStateChanged?.Invoke();
        }

        /// <summary>Phase 3 inputs: Attack and Defense Points of the participating units (Part 12 Lines 197-202).</summary>
        private void RecomputeBattlePoints(PendingBattleState pb)
        {
            pb.AttackerAttackPoints = pb.AttackerUnits.Sum(u => GetUnitBattleAttack(pb.Attacker, pb.Tile, u));
            pb.AttackerDefensePoints = pb.AttackerUnits.Sum(u => GetUnitBattleDefense(pb.Attacker, pb.Tile, u));
            pb.DefenderAttackPoints = pb.DefenderUnits.Sum(u => GetUnitBattleAttack(pb.Defender, pb.Tile, u));
            pb.DefenderDefensePoints = pb.DefenderUnits.Sum(u => GetUnitBattleDefense(pb.Defender, pb.Tile, u));
            pb.Report.AttackerUnitAttackSum = pb.AttackerAttackPoints;
            pb.Report.DefenderUnitDefenseSum = pb.DefenderDefensePoints;
        }

        private string DescribeBattleTotals(PendingBattleState pb)
        {
            RecomputeBattlePoints(pb);
            int atk = Mathf.Max(0, pb.AttackerRoll + pb.Report.AttackerDiceBonus) + pb.AttackerAttackPoints;
            int def = Mathf.Max(0, pb.DefenderRoll + pb.Report.DefenderDiceBonus) + pb.DefenderDefensePoints;
            return $"Attacker rolled {pb.Report.AttackerDice1} (mod {pb.Report.AttackerDiceBonus:+#;-#;0}) + {pb.AttackerAttackPoints} Attack Points = {atk}\n" +
                   $"Defender rolled {pb.Report.DefenderDice1} (mod {pb.Report.DefenderDiceBonus:+#;-#;0}) + {pb.DefenderDefensePoints} Defense Points = {def}";
        }

        /// <summary>Runs the battle forward until it needs a player decision or finishes.</summary>
        private void ContinuePendingBattle()
        {
            while (PendingBattle != null && PendingBattle.CurrentDecision == null)
            {
                var pb = PendingBattle;
                var tile = pb.Tile;
                var attacker = pb.Attacker;
                var defender = pb.Defender;
                var report = pb.Report;

                switch (pb.Stage)
                {
                    case PendingBattleStage.RollDice:
                        // Phase 2: Dice Roll (one 3-sided die each)
                        pb.AttackerRoll = report.AttackerDice1 = RollD3();
                        pb.DefenderRoll = report.DefenderDice1 = RollD3();
                        report.AttackerDice2 = report.DefenderDice2 = 0;
                        if (HasRestriction(attacker, 3))
                        {
                            pb.AttackerRoll = report.AttackerDice1 = 1;   // Subservience: the attacker's roll counts as 1
                            Log($"Subservience: {attacker.DisplayName}'s roll counts as 1.");
                        }
                        // The Lord of War's Greed: roll a die for each battle it is in
                        foreach (var side in new[] { pb.AttackerUnits, pb.DefenderUnits })
                        {
                            var low = side.FirstOrDefault(u => u.Definition.Id == "lord_of_war");
                            if (low == null) continue;
                            var goatP = Players[low.OwnerPlayerIndex];
                            int g = RollD6();
                            if (g % 2 == 0) AwardAuthority(goatP.PlayerIndex, 1, $"Greed (rolled {g})");
                            else { RemoveResources(goatP, ResourceType.Gold, 1); Log($"Greed rolled {g}: {goatP.DisplayName} lost 1 Gold."); }
                        }
                        RecomputeBattlePoints(pb);
                        pb.Stage = PendingBattleStage.FigurineAttackerWindow;
                        break;

                    // Copper Figurine (Traded Good): after a die has been rolled, re-roll 1 die. Attacker first.
                    case PendingBattleStage.FigurineAttackerWindow:
                        pb.Stage = PendingBattleStage.FigurineDefenderWindow;
                        if (attacker.PurchasedTradedGoods.Any(g => g.Id == "tg_copper_figurine"))
                            AskBattleDecision(BattleDecisionKind.CopperFigurine, attacker, "Copper Figurine",
                                $"You rolled {report.AttackerDice1}. Discard Copper Figurine to re-roll your die?");
                        break;

                    case PendingBattleStage.FigurineDefenderWindow:
                        pb.Stage = PendingBattleStage.HalberdWindow;
                        if (defender.PurchasedTradedGoods.Any(g => g.Id == "tg_copper_figurine"))
                            AskBattleDecision(BattleDecisionKind.CopperFigurine, defender, "Copper Figurine",
                                $"You rolled {report.DefenderDice1}. Discard Copper Figurine to re-roll your die?");
                        break;

                    // All relic dice effects are offered after the dice, at the start of the Attack and Defense Points
                    // phase. Turn player (the Attacker) first, then the Defender (Part 6 timing rule).
                    case PendingBattleStage.HalberdWindow:
                        pb.Stage = PendingBattleStage.WarHornWindow;
                        if (HasTableRelic(attacker, "iron_halberd"))
                        {
                            AskBattleDecision(BattleDecisionKind.IronHalberd, attacker, "Iron Halberd",
                                DescribeBattleTotals(pb) + "\nDiscard Iron Halberd to add 2 to your dice roll?");
                        }
                        break;

                    case PendingBattleStage.WarHornWindow:
                        pb.Stage = PendingBattleStage.OvergrownWallWindow;
                        if (HasTableRelic(defender, "war_horn") && CanAddUnitToTile(tile, defender.PlayerIndex) &&
                            defender.Faction.Units.Any(u => !u.IsSpecialUnit))
                        {
                            AskBattleDecision(BattleDecisionKind.WarHorn, defender, "War Horn",
                                DescribeBattleTotals(pb) + "\nDiscard War Horn to place 1 basic unit from your supply on the battle tile? It joins the battle.");
                        }
                        break;

                    case PendingBattleStage.OvergrownWallWindow:
                        pb.Stage = PendingBattleStage.TreatiseWindow;
                        if (HasTableRelic(defender, "overgrown_wall"))
                        {
                            AskBattleDecision(BattleDecisionKind.OvergrownWall, defender, "Overgrown Wall",
                                DescribeBattleTotals(pb) + "\nDiscard Overgrown Wall so the Attacker subtracts 2 from their dice roll?");
                        }
                        break;

                    case PendingBattleStage.TreatiseWindow:
                        pb.Stage = PendingBattleStage.DamageCalculation;
                        if (HasTableRelic(defender, "political_treatise"))
                        {
                            AskBattleDecision(BattleDecisionKind.PoliticalTreatise, defender, "Political Treatise",
                                DescribeBattleTotals(pb) + "\nDiscard Political Treatise to add 2 to your dice roll?");
                        }
                        break;

                    case PendingBattleStage.DamageCalculation:
                        RecomputeBattlePoints(pb); // War Horn may have added a unit
                        tile.HasStatueOfGoddessBuff = false;
                        report.AttackerTotal = Mathf.Max(0, pb.AttackerRoll + report.AttackerDiceBonus) + pb.AttackerAttackPoints;
                        report.DefenderTotal = Mathf.Max(0, pb.DefenderRoll + report.DefenderDiceBonus) + pb.DefenderDefensePoints;
                        pb.Stage = PendingBattleStage.BrassAttackerWindow;

                        if (report.AttackerTotal == report.DefenderTotal)
                        {
                            // Tie: Each side takes 1 HP of damage, skip Phase 4 and go to Phase 5 (Line 204)
                            report.IsTie = true;
                            report.WinnerPlayerIndex = -1;
                            report.LoserPlayerIndex = -1;
                            report.WinnerDamageDealt = 1;
                            report.MandatoryLoserDamageDealt = 1;
                            pb.DamageToAttacker = pb.DamageToDefender = 1;
                            break;
                        }

                        pb.AttackerWon = report.AttackerTotal > report.DefenderTotal;
                        var winner = pb.AttackerWon ? attacker : defender;
                        var loser = pb.AttackerWon ? defender : attacker;
                        var winnerUnits = pb.AttackerWon ? pb.AttackerUnits : pb.DefenderUnits;
                        report.WinnerPlayerIndex = winner.PlayerIndex;
                        report.LoserPlayerIndex = loser.PlayerIndex;

                        // Damage Dealt = winner's Attack Points minus loser's Defense Points (min 1, max = winner's participating units) (Lines 206-208)
                        int winnerAttackPts = pb.AttackerWon ? pb.AttackerAttackPoints : pb.DefenderAttackPoints;
                        int loserDefensePts = pb.AttackerWon ? pb.DefenderDefensePoints : pb.AttackerDefensePoints;
                        int rawDamage = Mathf.Max(1, winnerAttackPts - loserDefensePts);
                        int damageCap = Mathf.Max(1, winnerUnits.Count);
                        pb.FinalWinnerDamage = Mathf.Min(rawDamage, damageCap);

                        // Mandatory Loser Damage: loser always deals 1 HP back to winner unless Horde of the Millions (Oblivious to Danger - Line 210)
                        pb.MandatoryLoserDamage = (loser.Faction.Id == FactionId.HordeOfTheMillions) ? 0 : 1;

                        pb.DamageToAttacker = pb.AttackerWon ? pb.MandatoryLoserDamage : pb.FinalWinnerDamage;
                        pb.DamageToDefender = pb.AttackerWon ? pb.FinalWinnerDamage : pb.MandatoryLoserDamage;
                        break;

                    // Brass Shield: either side may use it in any battle to reduce the damage its units take by 2. Attacker first.
                    case PendingBattleStage.BrassAttackerWindow:
                        pb.Stage = PendingBattleStage.BrassDefenderWindow;
                        if (pb.DamageToAttacker > 0 && HasTableRelic(attacker, "brass_shield"))
                            AskBattleDecision(BattleDecisionKind.BrassShield, attacker, "Brass Shield",
                                $"Battle: {report.AttackerTotal} vs {report.DefenderTotal}.\nYour units are about to take {pb.DamageToAttacker}HP.\n" +
                                "Discard Brass Shield to reduce the damage your units take by 2 (to a minimum of 0)?");
                        break;

                    case PendingBattleStage.BrassDefenderWindow:
                        pb.Stage = PendingBattleStage.AssignDamage;
                        if (pb.DamageToDefender > 0 && HasTableRelic(defender, "brass_shield"))
                            AskBattleDecision(BattleDecisionKind.BrassShield, defender, "Brass Shield",
                                $"Battle: {report.AttackerTotal} vs {report.DefenderTotal}.\nYour units are about to take {pb.DamageToDefender}HP.\n" +
                                "Discard Brass Shield to reduce the damage your units take by 2 (to a minimum of 0)?");
                        break;

                    // Each player chooses which of their units take the HP damage (when not every unit is hit).
                    case PendingBattleStage.AssignDamage:
                        if (!AskDamageAssignment(pb, true) && !AskDamageAssignment(pb, false))
                            pb.Stage = PendingBattleStage.ApplyDamage;
                        break;

                    case PendingBattleStage.ApplyDamage:
                        ApplyPendingBattleDamage(pb);
                        pb.Stage = PendingBattleStage.QuickFeetWindow;
                        break;

                    case PendingBattleStage.QuickFeetWindow:
                        // Quick Feet (Line 618): after the 0 HP units are removed
                        pb.Stage = PendingBattleStage.Finish;
                        if (pb.QuickFeetEligiblePlayerIndex >= 0)
                        {
                            var tanukiPlayer = Players[pb.QuickFeetEligiblePlayerIndex];
                            AskBattleDecision(BattleDecisionKind.QuickFeet, tanukiPlayer, "Quick Feet",
                                $"Your Tanuki units at {tile.Coord} took damage.\n" +
                                "Scatter your remaining units on that tile to adjacent tiles with no enemy units and place 1 Fallen Leaves tile?");
                        }
                        break;

                    case PendingBattleStage.Finish:
                        FinishPendingBattle(pb);
                        break;
                }
            }
        }

        private void ApplyPendingBattleDamage(PendingBattleState pb)
        {
            BeginRemovalEvent();
            var tile = pb.Tile;
            var attacker = pb.Attacker;
            var defender = pb.Defender;
            var report = pb.Report;

            // Snapshot for Quick Feet: Tanuki units on the battle tile before damage is applied
            var tanukiSide = attacker.Faction.Id == FactionId.TroubleMakingTanukis ? attacker
                           : defender.Faction.Id == FactionId.TroubleMakingTanukis ? defender : null;
            var tanukiHpBefore = new Dictionary<UnitInstance, int>();
            if (tanukiSide != null)
                foreach (var t in tile.Units.Where(u => u.OwnerPlayerIndex == tanukiSide.PlayerIndex && u.Definition.Id == "tanuki"))
                    tanukiHpBefore[t] = t.CurrentHP;

            List<UnitInstance> attackerLosses;
            List<UnitInstance> defenderLosses;

            bool allow2Atk = false, allow2Def = false;
            if (!report.IsTie)
            {
                var winner = pb.AttackerWon ? attacker : defender;
                var winnerUnits = pb.AttackerWon ? pb.AttackerUnits : pb.DefenderUnits;
                bool allow2 = winnerUnits.Any(u => u.EquippedItems.Any(e => e.Id == "golden_gauntlet")) ||
                              (winner.Faction.Id == FactionId.BriaredFlower && winner.BriaredFlowerThorns == 0 && winnerUnits.Any(u => u.Definition.Id == "blooming_briar"));
                if (pb.AttackerWon) allow2Def = allow2; else allow2Atk = allow2;
                report.WinnerDamageDealt = pb.AttackerWon ? pb.DamageToDefender : pb.DamageToAttacker;
                report.MandatoryLoserDamageDealt = pb.AttackerWon ? pb.DamageToAttacker : pb.DamageToDefender;
            }

            // All damage is applied at the same time at the end of Phase 4
            attackerLosses = pb.DamageToAttacker > 0
                ? ApplyBattleDamageToSide(tile, pb.AttackerUnits, pb.DamageToAttacker, defender.PlayerIndex, report, allow2Atk, pb.AttackerDamageOrder)
                : new List<UnitInstance>();
            defenderLosses = pb.DamageToDefender > 0
                ? ApplyBattleDamageToSide(tile, pb.DefenderUnits, pb.DamageToDefender, attacker.PlayerIndex, report, allow2Def, pb.DefenderDamageOrder)
                : new List<UnitInstance>();

            // Platypus Papacy Crusader Authority, whichever side removed them and whatever the battle outcome
            ScoreCrusaderBattleRemovals(attacker, pb.AttackerUnits, defender, defenderLosses);
            ScoreCrusaderBattleRemovals(defender, pb.DefenderUnits, attacker, attackerLosses);

            // Quick Feet (Line 618): a player dealt 1+ HP of damage to your Tanuki units on a tile with 2 or more of your Tanuki units
            pb.QuickFeetEligiblePlayerIndex = -1;
            if (tanukiSide != null && tanukiHpBefore.Count >= 2 &&
                tanukiHpBefore.Any(kv => kv.Key.CurrentHP < kv.Value))
            {
                bool hasRemainingUnits = tile.Units.Any(u => u.OwnerPlayerIndex == tanukiSide.PlayerIndex);
                bool hasSafeTile = tile.Coord.GetNeighbors().Any(n => Board.ContainsKey(n) &&
                                    !Board[n].Units.Any(u => u.OwnerPlayerIndex != tanukiSide.PlayerIndex));
                if (hasRemainingUnits && hasSafeTile)
                    pb.QuickFeetEligiblePlayerIndex = tanukiSide.PlayerIndex;
            }
        }

        /// <summary>
        /// Platypus Papacy Crusader scoring for one direction of a battle (remover's units removed victim's units):
        /// - Removing 2 or more Crusaders in one battle: remover gains 1 Authority (Ways Enemies Can Score Authority).
        /// - Level 4, during a Crusade: when an enemy removes one of your Crusaders, that enemy gains 1 Authority (per Crusader).
        /// - Level 4, during a Crusade: when one of your Crusaders removes an enemy unit, gain 1 Authority (per unit removed,
        ///   when Crusaders took part in the battle).
        /// </summary>
        private void ScoreCrusaderBattleRemovals(PlayerState remover, List<UnitInstance> removerUnits, PlayerState victim, List<UnitInstance> victimLosses)
        {
            if (victimLosses.Count == 0) return;

            if (victim.Faction.Id == FactionId.PlatypusPapacy)
            {
                int crusadersRemoved = victimLosses.Count(u => u.OwnerPlayerIndex == victim.PlayerIndex && u.Definition.Id == "crusader");
                if (crusadersRemoved >= 2)
                    AwardAuthority(remover.PlayerIndex, 1, "Removed 2+ Platypus Crusaders in one battle");
                if (victim.PlatyPopeLevel >= 4 && victim.CrusadeActive && crusadersRemoved > 0)
                    AwardAuthority(remover.PlayerIndex, crusadersRemoved, $"Removed {crusadersRemoved} Crusader(s) during a Level 4 Crusade");
            }

            if (remover.Faction.Id == FactionId.PlatypusPapacy && remover.PlatyPopeLevel >= 4 && remover.CrusadeActive &&
                removerUnits.Any(u => u.Definition.Id == "crusader"))
            {
                int enemyUnitsRemoved = victimLosses.Count(u => u.OwnerPlayerIndex != remover.PlayerIndex);
                if (enemyUnitsRemoved > 0)
                    AwardAuthority(remover.PlayerIndex, enemyUnitsRemoved, $"Crusaders removed {enemyUnitsRemoved} enemy unit(s) during a Level 4 Crusade");
            }
        }

        /// <summary>
        /// Quick Feet (Line 618): move the Tanuki player's remaining units on the battle tile to adjacent tiles with
        /// no enemy units. Units are spread across the safe tiles (each may go to a different tile), the coin follows
        /// the first unit that moves, then 1 Fallen Leaves tile is placed on one forest tile a unit moved to.
        /// </summary>
        private void ExecuteQuickFeet(PendingBattleState pb, PlayerState tanukiPlayer)
        {
            var tile = pb.Tile;
            var safeTiles = tile.Coord.GetNeighbors()
                .Where(n => Board.ContainsKey(n) && !Board[n].Units.Any(u => u.OwnerPlayerIndex != tanukiPlayer.PlayerIndex))
                .Select(n => Board[n])
                .ToList();
            var movers = tile.Units.Where(u => u.OwnerPlayerIndex == tanukiPlayer.PlayerIndex).ToList();
            if (safeTiles.Count == 0 || movers.Count == 0) return;

            var destinations = new List<HexTileState>();
            HexTileState coinDestination = null;
            for (int i = 0; i < movers.Count; i++)
            {
                HexTileState dest = null;
                for (int k = 0; k < safeTiles.Count; k++)
                {
                    var candidate = safeTiles[(i + k) % safeTiles.Count];
                    if (CanAddUnitToTile(candidate, tanukiPlayer.PlayerIndex)) { dest = candidate; break; }
                }
                if (dest == null) continue; // stacking limit: this unit stays

                tile.Units.Remove(movers[i]);
                dest.Units.Add(movers[i]);
                if (!destinations.Contains(dest)) destinations.Add(dest);
                if (coinDestination == null) coinDestination = dest;
            }

            if (coinDestination != null)
                MoveAndScoreTanukiCoin(tile, coinDestination, tanukiPlayer.PlayerIndex);

            var leafTile = destinations.FirstOrDefault(d => d.EffectiveTerrain == TileTerrainType.Forest && d.Overlay == FactionOverlayTileType.None);
            if (leafTile != null)
                PlaceOverlayTile(leafTile.Coord, FactionOverlayTileType.FallenLeaf_Unflipped, tanukiPlayer.PlayerIndex);

            Log($"Quick Feet! {tanukiPlayer.DisplayName}'s units scattered to {string.Join(", ", destinations.Select(d => d.Coord.ToString()))}" +
                (leafTile != null ? $" and placed a Fallen Leaves tile at {leafTile.Coord}." : "."));
        }

        private void FinishPendingBattle(PendingBattleState pb)
        {
            var attacker = pb.Attacker;
            var defender = pb.Defender;
            var report = pb.Report;

            if (!report.IsTie)
            {
                var winner = pb.AttackerWon ? attacker : defender;
                var winnerUnits = pb.AttackerWon ? pb.AttackerUnits : pb.DefenderUnits;

                // Phase 5 Step 2: Resolve effects that care about winning or losing the battle (Momentum - Line 215 & Lines 414-418)
                // "Momentum only changes from battles where your faction is the Attacker."
                if (attacker.Faction.Id == FactionId.CurlyTail)
                {
                    if (pb.AttackerWon)
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
                if (winner.Faction.Id == FactionId.PlatypusPapacy && winner.CrusadeActive)
                {
                    winner.PlatypusFaith = Mathf.Min(5, winner.PlatypusFaith + 1);
                }
            }

            if (pb.ConsumesMainPhaseAction) SpendBattleAction();
            report.SummaryLog = $"Battle at {report.TileCoord}: {attacker.DisplayName} ({report.AttackerTotal}) vs {defender.DisplayName} ({report.DefenderTotal}) -> " +
                                (report.IsTie ? "Tie (1HP each)" : $"{Players[report.WinnerPlayerIndex].DisplayName} wins ({report.WinnerDamageDealt}HP dealt)!");
            Log(report.SummaryLog);

            PendingBattle = null;
            LastBattleReport = report;
            OpenNextQueuedChoice();
            OnBattleResolved?.Invoke(report);
            OnStateChanged?.Invoke();
        }

        /// <summary>The tile a unit is standing on (null if it isn't on the board).</summary>
        public HexTileState FindUnitTile(UnitInstance u) => Board.Values.FirstOrDefault(t => t.Units.Contains(u));

        /// <summary>A unit's Attack right now, including meters, Crusade, equipment and tile effects.</summary>
        public int DisplayAttack(UnitInstance u)
        {
            var tile = FindUnitTile(u);
            return tile == null ? u.EffectiveAttack : GetUnitBattleAttack(Players[u.OwnerPlayerIndex], tile, u);
        }

        /// <summary>A unit's Defense right now, including meters, Crusade, equipment and tile effects.</summary>
        public int DisplayDefense(UnitInstance u)
        {
            var tile = FindUnitTile(u);
            return tile == null ? u.EffectiveDefense : GetUnitBattleDefense(Players[u.OwnerPlayerIndex], tile, u);
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
                if (owner.CrusadeActive) atk += 1;
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

        /// <summary>
        /// Asks one side's owner to pick the next unit to take damage, when the damage won't reach every unit.
        /// Returns true when a decision was raised.
        /// </summary>
        private bool AskDamageAssignment(PendingBattleState pb, bool attackerSide)
        {
            var player = attackerSide ? pb.Attacker : pb.Defender;
            var units = (attackerSide ? pb.AttackerUnits : pb.DefenderUnits).Where(u => u.CurrentHP > 0).ToList();
            var order = attackerSide ? pb.AttackerDamageOrder : pb.DefenderDamageOrder;
            int dmg = attackerSide ? pb.DamageToAttacker : pb.DamageToDefender;
            if (dmg <= 0 || units.Count <= 1 || units.Count <= dmg || order.Count >= dmg) return false;
            var left = units.Where(u => !order.Contains(u)).ToList();
            var d = new BattleDecision
            {
                Kind = BattleDecisionKind.AssignDamage, PlayerIndex = player.PlayerIndex, Title = "Assign damage",
                Prompt = $"Your units take {dmg}HP in this battle. Choose which unit takes the next 1HP ({order.Count + 1} of {dmg})."
            };
            foreach (var u in left)
            {
                d.Options.Add($"{u.Definition.Name} ({u.CurrentHP}HP)");
                d.UnitIds.Add(u.InstanceId);
            }
            pb.CurrentDecision = d;
            Log($"Waiting for {player.DisplayName} to choose which units take damage.");
            OnBattleDecisionRequired?.Invoke(d);
            OnStateChanged?.Invoke();
            return true;
        }

        private List<UnitInstance> ApplyBattleDamageToSide(HexTileState tile, List<UnitInstance> units, int totalDamage, int killerPlayerIndex, BattleReport report, bool allow2HpToSingleUnit = false, List<UnitInstance> chosenFirst = null)
        {
            var removed = new List<UnitInstance>();
            var ordered = units.OrderBy(u => u.Definition.IsSpecialUnit ? 1 : 0).ToList();
            if (chosenFirst != null && chosenFirst.Count > 0)
                ordered = chosenFirst.Where(units.Contains).Concat(ordered.Where(u => !chosenFirst.Contains(u))).ToList();
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

                int hit = (allow2HpToSingleUnit && u.Definition.IsSpecialUnit && remaining >= 2 && u.CurrentHP >= 2) ? 2 : 1;
                u.CurrentHP -= hit;
                remaining -= hit;
                if (u.CurrentHP <= 0)
                {
                    report.DestroyedUnitNames.Add(u.Definition.Name);
                    removed.Add(u);
                    RemoveUnitFromBoard(tile, u, killerPlayerIndex);
                }
            }
            return removed;
        }

        private void RemoveUnitFromBoard(HexTileState tile, UnitInstance u, int removerPlayerIndex)
        {
            // Units that joined a battle from an adjacent tile (Vanguard Devotion) are removed from the tile they stand on
            if (!tile.Units.Contains(u))
            {
                var actualTile = Board.Values.FirstOrDefault(t => t.Units.Contains(u));
                if (actualTile != null) tile = actualTile;
            }
            var owner = Players[u.OwnerPlayerIndex];
            int crusadeBonus = (removerPlayerIndex >= 0 && removerPlayerIndex != owner.PlayerIndex &&
                                Players[removerPlayerIndex].Faction.Id == FactionId.PlatypusPapacy &&
                                Players[removerPlayerIndex].CrusadeActive) ? 1 : 0;

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
                // Propagation is a Frenzied Bloom ability: only at 0 Thorns
                if (owner.BriaredFlowerThorns == 0)
                {
                    var enemyHere = tile.Units.FirstOrDefault(x => x.OwnerPlayerIndex != owner.PlayerIndex);
                    if (enemyHere != null)
                    {
                        enemyHere.CurrentHP -= 1;
                        if (enemyHere.CurrentHP <= 0) RemoveUnitFromBoard(tile, enemyHere, owner.PlayerIndex);
                    }
                    var adjForest = Neighbors(tile).FirstOrDefault(n => n.EffectiveTerrain == TileTerrainType.Forest && CanAddUnitToTile(n, owner.PlayerIndex));
                    if (adjForest != null)
                        SpawnUnit(adjForest.Coord, "thorned_seed", owner, 1);
                }
                if (removerPlayerIndex >= 0 && removerPlayerIndex != owner.PlayerIndex)
                    AwardAuthority(removerPlayerIndex, 2 + crusadeBonus, "Removed the Blooming Briar (Propagation)");
                return;
            }

            // 5. The Platy-Pope removal (Line 560): Papacy loses 1 Authority, remover gains 2 Authority, Pope returns next Excavation phase
            if (u.Definition.Id == "platy_pope")
            {
                owner.PlatyPopeOnBoard = false;
                // "During a Crusade ... when the Platy-Pope is removed by an enemy: you lose 1 Authority"
                if (owner.CrusadeActive && removerPlayerIndex >= 0 && removerPlayerIndex != owner.PlayerIndex)
                    owner.Authority = Mathf.Max(0, owner.Authority - 1);
            }

            tile.Units.Remove(u);
            if (owner.Faction.Id == FactionId.GoatCabal && DemonIds.Contains(u.Definition.Id))
            {
                owner.DemonStates[u.Definition.Id] = DemonUnsummoned;   // it can be summoned again at its full cost
                if (u.Definition.Id == "serpentine_sovereign") owner.RoyalDecreeArmed = false;
            }

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
                // Necro-mouser: an attacker who removes an equipped special unit may loot 1 material from its equipment
                if (owner.Faction.Id == FactionId.Necromouser && u.EquippedItems.Count > 0 &&
                    PendingBattle != null && PendingBattle.Attacker.PlayerIndex == removerPlayerIndex)
                    OfferNecroEquipmentLoot(Players[removerPlayerIndex], owner, u);
            }

            // Necro-mouser Miasma trigger (Line 667 & Line 675): Any unit removed on or adjacent to a Miasma Tile places 1 unit onto Cauldron & awakens Necromouser
            bool onOrAdjMiasma = (tile.Overlay == FactionOverlayTileType.Miasma_Hidden || tile.Overlay == FactionOverlayTileType.Miasma_Revealed) ||
                                 tile.Coord.GetNeighbors().Any(n => Board.ContainsKey(n) &&
                                     (Board[n].Overlay == FactionOverlayTileType.Miasma_Hidden || Board[n].Overlay == FactionOverlayTileType.Miasma_Revealed));
            if (onOrAdjMiasma)
            {
                foreach (var necroPlayer in Players.Where(pl => pl.Faction.Id == FactionId.Necromouser))
                {
                    // "place 1 of those removed units from each faction onto the Cauldron" (once per faction per removal)
                    if (_cauldronCountedThisEvent.Add(necroPlayer.PlayerIndex * 16 + owner.PlayerIndex))
                        necroPlayer.NecromouserCauldronDeadCount++;
                    if (!necroPlayer.NecromouserAwakened)
                    {
                        necroPlayer.NecromouserAwakened = true;
                        SpawnUnit(tile.Coord, "necromouser", necroPlayer, 1);
                        SpawnUnit(tile.Coord, "skeleton", necroPlayer, 1);
                        Log($"THE NECROMOUSER AWAKENS at {tile.Coord} with 1 Skeleton!");
                    }
                }
            }
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
            if (unitId == "goat") onBoardSupplyUsed += owner.GoatPurged;   // Purged Goats sit on the Purgatory card, not in the supply
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
                    IsGuest = BecomesGuestOnArrival(tile, owner.PlayerIndex)
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

        /// <summary>
        /// Watering Hole (Part 17 Faction 7): an enemy unit that enters a tile with a Wallow and a Capybara unit, or a tile
        /// with the Riverkeeper, becomes a Guest.
        /// </summary>
        internal bool BecomesGuestOnArrival(HexTileState tile, int unitOwner)
        {
            var capy = Players.FirstOrDefault(pl => pl.Faction != null && pl.Faction.Id == FactionId.CapybaraCaravan);
            if (capy == null || capy.PlayerIndex == unitOwner) return false;
            bool wallow = tile.Building != null && tile.Building.Type == BuildingType.Wallow && tile.Building.OwnerPlayerIndex == capy.PlayerIndex;
            bool capybara = tile.Units.Any(u => u.OwnerPlayerIndex == capy.PlayerIndex && u.Definition.Id == "capybara");
            bool riverkeeper = tile.Units.Any(u => u.OwnerPlayerIndex == capy.PlayerIndex && u.Definition.Id == "riverkeeper");
            return (wallow && capybara) || riverkeeper;
        }

        /// <summary>Watering Hole case 4: enemy units on the Riverkeeper's tile become Guests when it arrives.</summary>
        internal void WelcomeGuestsAtRiverkeeper(HexTileState tile)
        {
            var rk = tile.Units.FirstOrDefault(u => u.Definition.Id == "riverkeeper");
            if (rk == null) return;
            foreach (var u in tile.Units.Where(u => u.OwnerPlayerIndex != rk.OwnerPlayerIndex)) u.IsGuest = true;
        }

        /// <summary>Watering Hole: guests that attack the Trader or the Riverkeeper, or destroy a Wallow, stop being guests.</summary>
        internal void EndGuestStatus(IEnumerable<UnitInstance> units, string why)
        {
            var ended = units.Where(u => u.IsGuest).ToList();
            foreach (var u in ended) u.IsGuest = false;
            if (ended.Count > 0) Log($"{Players[ended[0].OwnerPlayerIndex].DisplayName}'s units are no longer guests: they {why}.");
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
            // Capybara Caravan Watering Hole case 2: building a Wallow on a tile that already has enemy units makes them Guests
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
            if (overlay == FactionOverlayTileType.FallenLeaf_Unflipped)
            {
                // Take a random Fallen Leaves tile from the Tanuki player's supply; none left means none can be placed.
                if (ownerPlayerIndex < 0 || ownerPlayerIndex >= Players.Count) return;
                var leaves = Players[ownerPlayerIndex].FallenLeavesStack;
                if (leaves.Count == 0)
                {
                    Log($"{Players[ownerPlayerIndex].DisplayName} has no Fallen Leaves tiles left in supply.");
                    return;
                }
                int pick = _rng.Next(leaves.Count);
                tile.LeafIsTrap = leaves[pick];
                leaves.RemoveAt(pick);
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
