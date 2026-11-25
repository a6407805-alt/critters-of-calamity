using System.Collections.Generic;
using System.Linq;
using CrittersOfCalamity.Model;

namespace CrittersOfCalamity.Engine
{
    /// <summary>
    /// The start of a game, before the first turn:
    /// 1. Part 3: every player rolls 2 dice; the highest roll picks a faction first, then the next highest, and so on.
    /// 2. Players pick factions in that order (each faction once).
    /// 3. Part 5: every player rolls 2 dice again; the highest roll takes the first turn and play continues clockwise.
    /// 4. Every deck is shuffled and starting cards are dealt. The Capybara Caravan places its streams, then each
    ///    player, in reverse turn order, clicks the tiles for their starting pieces (Part 17 "Setup"). The Tanuki
    ///    player's Coin Tokens go down after everyone else, then the first turn begins.
    /// Ties are rerolled: only the tied players roll again, and their new roll breaks the tie.
    /// </summary>
    public partial class CrittersGameEngine
    {
        /// <summary>Seat index of the player who took the first turn (setup runs in reverse from here).</summary>
        public int FirstPlayerIndex;

        /// <summary>For each seat, every 2d6 total rolled in the current roll-off (rerolls are appended).</summary>
        public List<List<int>> PreGameRollHistory = new List<List<int>>();
        /// <summary>For each seat, the two dice of their latest roll (0 = not rolled yet).</summary>
        public List<int> PreGameLastDie1 = new List<int>();
        public List<int> PreGameLastDie2 = new List<int>();
        /// <summary>Seats that still need to roll in the current round (everyone at first, then only tied players).</summary>
        public List<int> PreGameSeatsToRoll = new List<int>();

        /// <summary>Faction pick order (seat indexes, highest roll first) and how many have picked.</summary>
        public List<int> FactionPickOrder = new List<int>();
        public int FactionPickCursor;

        /// <summary>Players in setup order (reverse turn order), how many have finished, and the current player's chosen tiles.</summary>
        public List<int> SetupOrder = new List<int>();
        public int SetupCursor;
        public List<HexCoord> SetupPicks = new List<HexCoord>();

        public bool IsPreGame => CurrentPhase == TurnPhase.PreGame_DraftRoll ||
                                 CurrentPhase == TurnPhase.PreGame_FactionPick ||
                                 CurrentPhase == TurnPhase.PreGame_TurnOrderRoll ||
                                 CurrentPhase == TurnPhase.PreGame_ReverseFactionSetup;

        public int CurrentSetupPlayer =>
            CurrentPhase == TurnPhase.PreGame_ReverseFactionSetup && SetupCursor < SetupOrder.Count ? SetupOrder[SetupCursor] : -1;

        public int CurrentFactionPicker =>
            CurrentPhase == TurnPhase.PreGame_FactionPick && FactionPickCursor < FactionPickOrder.Count
                ? FactionPickOrder[FactionPickCursor] : -1;

        /// <summary>
        /// Starts a game at the roll for faction pick order. The board is built and players are seated, but
        /// nobody has a faction yet and no cards are dealt.
        /// </summary>
        public void StartPreGame(List<string> playerNames)
        {
            PrepareTableAndPlayers(playerNames);
            CurrentPhase = TurnPhase.PreGame_DraftRoll;
            StartRollOff();
            Log("Roll for faction pick order: everyone rolls 2 dice. Highest roll picks first.");
            OnStateChanged?.Invoke();
        }

        public bool CanRollPreGameDice(int seat)
        {
            if (CurrentPhase != TurnPhase.PreGame_DraftRoll && CurrentPhase != TurnPhase.PreGame_TurnOrderRoll) return false;
            return PreGameSeatsToRoll.Contains(seat);
        }

        public bool RollPreGameDice(int seat)
        {
            if (!CanRollPreGameDice(seat)) return false;

            int total = Roll2D6(out int d1, out int d2);
            PreGameRollHistory[seat].Add(total);
            PreGameLastDie1[seat] = d1;
            PreGameLastDie2[seat] = d2;
            PreGameSeatsToRoll.Remove(seat);

            string what = CurrentPhase == TurnPhase.PreGame_DraftRoll ? "faction pick order" : "turn order";
            Log($"{Players[seat].DisplayName} rolled {d1} + {d2} = {total} for {what}.");

            if (PreGameSeatsToRoll.Count == 0)
                ResolveRollOff();

            OnStateChanged?.Invoke();
            return true;
        }

        public bool CanPickFaction(int seat, FactionId faction)
        {
            if (CurrentPhase != TurnPhase.PreGame_FactionPick) return false;
            if (seat != CurrentFactionPicker) return false;
            return Players.All(p => p.Faction == null || p.Faction.Id != faction);
        }

        public bool PickFaction(int seat, FactionId faction)
        {
            if (!CanPickFaction(seat, faction)) return false;

            Players[seat].Faction = RulebookDatabase.GetAllFactions().First(f => f.Id == faction);
            Log($"{Players[seat].DisplayName} picked {Players[seat].Faction.Name}.");
            FactionPickCursor++;

            if (FactionPickCursor >= FactionPickOrder.Count)
            {
                CurrentPhase = TurnPhase.PreGame_TurnOrderRoll;
                StartRollOff();
                Log("Roll for turn order: everyone rolls 2 dice. Highest roll takes the first turn.");
            }

            OnStateChanged?.Invoke();
            return true;
        }

        /// <summary>Seats sorted best roll first (by each seat's roll history, so rerolls break ties).</summary>
        public List<int> PreGameRanking()
        {
            var seats = Enumerable.Range(0, Players.Count).ToList();
            seats.Sort((a, b) => CompareHistories(PreGameRollHistory[b], PreGameRollHistory[a]));
            return seats;
        }

        /// <summary>Seats that are currently tied with someone and must reroll.</summary>
        public bool IsSeatTied(int seat)
        {
            if (PreGameRollHistory.Count <= seat || PreGameRollHistory[seat].Count == 0) return false;
            for (int other = 0; other < Players.Count; other++)
                if (other != seat && CompareHistories(PreGameRollHistory[seat], PreGameRollHistory[other]) == 0)
                    return true;
            return false;
        }

        // ------------------------------------------------------------------
        // SETUP: each player clicks the tiles for their starting pieces
        // ------------------------------------------------------------------

        void BeginSetupPlacement()
        {
            RunCapybaraSpecialSetup();
            CurrentPhase = TurnPhase.PreGame_ReverseFactionSetup;
            SetupOrder.Clear();
            for (int k = Players.Count - 1; k >= 0; k--)
                SetupOrder.Add((FirstPlayerIndex + k) % Players.Count); // last player in turn order sets up first
            SetupCursor = 0;
            SetupPicks.Clear();
            Log($"Setup in reverse turn order: {string.Join(", ", SetupOrder.Select(i => Players[i].DisplayName))}.");
            SkipSetupPlayersWithNoLegalTile();
        }

        /// <summary>How many tiles a faction's setup needs.</summary>
        public static int SetupTilesNeeded(FactionId faction)
        {
            switch (faction)
            {
                case FactionId.HordeOfTheMillions: return 2;     // 2 Burrows
                case FactionId.Necromouser: return 3;            // 3 Miasma Tiles
                case FactionId.FirestartingSquirrels: return 3;  // 3 adjacent forest tiles (Bunker on the first)
                default: return 1;
            }
        }

        /// <summary>Plain-language instructions for a faction's setup tiles.</summary>
        public static string SetupInstructions(FactionId faction)
        {
            switch (faction)
            {
                case FactionId.CurlyTail: return "Click a forest or ruin tile with no enemy units, at least 2 tiles away from every enemy unit, for Curly Tail, 4 Warband units and a War Party.";
                case FactionId.HordeOfTheMillions: return "Click 2 different forest tiles with no enemy units. Each gets a Burrow and 2 Millions units.";
                case FactionId.PlatypusPapacy: return "Click a forest or stream tile with no enemy units for the Citadel, 3 Pilgrims and the Platy-Pope.";
                case FactionId.TroubleMakingTanukis: return "Click a forest tile with no enemy units for 3 Tanukis and a Shrine. Your 12 Coin Tokens are placed after everyone else sets up.";
                case FactionId.Necromouser: return "Click 3 tiles for your facedown Miasma Tiles. They can't be next to each other.";
                case FactionId.FirestartingSquirrels: return "Click 3 forest tiles that touch each other, with no enemy units. Each gets a Squirrel; the first gets your Bunker.";
                case FactionId.CapybaraCaravan: return "Click a stream tile with no enemy units for 3 Capybaras, the Capybara Trader and a Wallow.";
                case FactionId.BriaredFlower: return "Click a forest tile with no enemy units within 4 tiles for the Blooming Briar, 2 Thorned Seeds and a Prickly Thicket.";
            }
            return "Click a tile.";
        }

        /// <summary>Whether a tile is a legal next pick for this player's setup (Part 5 and Part 17 "Setup").</summary>
        public bool IsLegalSetupTile(PlayerState p, HexCoord c, IList<HexCoord> picksSoFar, out string reason)
        {
            reason = null;
            if (!Board.TryGetValue(c, out var tile)) { reason = "That isn't a tile."; return false; }
            if (picksSoFar.Contains(c)) { reason = "You already picked that tile."; return false; }

            // Setup restrictions only care about enemy units (Part 5).
            var enemyUnitTiles = Board.Values.Where(t => t.Units.Any(u => u.OwnerPlayerIndex != p.PlayerIndex)).Select(t => t.Coord).ToList();
            bool enemyUnitsHere = enemyUnitTiles.Contains(c);
            var terrain = tile.EffectiveTerrain;

            // Nobody else may place within 4 tiles of any Briared Flower piece during setup.
            if (p.Faction.Id != FactionId.BriaredFlower)
            {
                foreach (var briar in Players.Where(o => o.PlayerIndex != p.PlayerIndex && o.Faction != null && o.Faction.Id == FactionId.BriaredFlower))
                {
                    bool near = Board.Values.Any(t => HexCoord.Distance(t.Coord, c) <= 4 &&
                        (t.Units.Any(u => u.OwnerPlayerIndex == briar.PlayerIndex) || (t.Building != null && t.Building.OwnerPlayerIndex == briar.PlayerIndex)));
                    if (near) { reason = "Too close to the Briared Flower (it keeps everyone 4 tiles away during setup)."; return false; }
                }
            }

            switch (p.Faction.Id)
            {
                case FactionId.CurlyTail:
                    if (terrain != TileTerrainType.Forest && terrain != TileTerrainType.Ruin) { reason = "Curly Tail sets up on a forest or ruin tile."; return false; }
                    if (enemyUnitTiles.Any(e => HexCoord.Distance(e, c) < 2)) { reason = "Must be at least 2 tiles away from every enemy unit."; return false; }
                    return true;

                case FactionId.HordeOfTheMillions:
                case FactionId.TroubleMakingTanukis:
                    if (terrain != TileTerrainType.Forest) { reason = "Must be a forest tile."; return false; }
                    if (enemyUnitsHere) { reason = "That tile has enemy units."; return false; }
                    return true;

                case FactionId.PlatypusPapacy:
                    if (terrain != TileTerrainType.Forest && terrain != TileTerrainType.Stream) { reason = "Must be a forest or stream tile."; return false; }
                    if (enemyUnitsHere) { reason = "That tile has enemy units."; return false; }
                    return true;

                case FactionId.Necromouser:
                    if (tile.Overlay != FactionOverlayTileType.None) { reason = "A unique faction tile is already there."; return false; }
                    if (picksSoFar.Any(o => HexCoord.Distance(o, c) <= 1)) { reason = "Miasma Tiles can't be next to each other."; return false; }
                    return true;

                case FactionId.FirestartingSquirrels:
                    if (terrain != TileTerrainType.Forest) { reason = "Must be a forest tile."; return false; }
                    if (enemyUnitsHere) { reason = "That tile has enemy units."; return false; }
                    if (picksSoFar.Count > 0 && !picksSoFar.Any(o => HexCoord.Distance(o, c) == 1)) { reason = "Your 3 tiles must touch each other."; return false; }
                    return true;

                case FactionId.CapybaraCaravan:
                    if (terrain != TileTerrainType.Stream) { reason = "Must be a stream tile."; return false; }
                    if (enemyUnitsHere) { reason = "That tile has enemy units."; return false; }
                    return true;

                case FactionId.BriaredFlower:
                    if (terrain != TileTerrainType.Forest) { reason = "Must be a forest tile."; return false; }
                    if (enemyUnitTiles.Any(e => HexCoord.Distance(e, c) <= 4)) { reason = "No enemy units may be within 4 tiles."; return false; }
                    return true;
            }
            return true;
        }

        /// <summary>Every tile the current setup player could click next (for highlighting).</summary>
        public List<HexCoord> LegalSetupTiles()
        {
            int seat = CurrentSetupPlayer;
            if (seat < 0) return new List<HexCoord>();
            var p = Players[seat];
            return Board.Keys.Where(c => IsLegalSetupTile(p, c, SetupPicks, out _)).ToList();
        }

        public bool CanPlaceSetupTile(int seat, HexCoord c, out string reason)
        {
            reason = null;
            if (seat != CurrentSetupPlayer) { reason = "It isn't your turn to set up."; return false; }
            if (SetupPicks.Contains(c)) return true; // clicking a picked tile again un-picks it
            return IsLegalSetupTile(Players[seat], c, SetupPicks, out reason);
        }

        /// <summary>Pick (or un-pick) a setup tile. When enough tiles are picked, the pieces go down and setup moves on.</summary>
        public bool PlaceSetupTile(int seat, HexCoord c)
        {
            if (!CanPlaceSetupTile(seat, c, out _)) return false;
            var p = Players[seat];

            if (SetupPicks.Contains(c))
            {
                SetupPicks.Remove(c);
                // Squirrels: removing a tile can leave the rest disconnected; start over in that case.
                if (p.Faction.Id == FactionId.FirestartingSquirrels && SetupPicks.Count == 2 && HexCoord.Distance(SetupPicks[0], SetupPicks[1]) != 1)
                    SetupPicks.Clear();
                OnStateChanged?.Invoke();
                return true;
            }

            SetupPicks.Add(c);
            if (SetupPicks.Count >= SetupTilesNeeded(p.Faction.Id))
            {
                ApplyFactionSetup(p, SetupPicks.ToList());
                SetupPicks.Clear();
                SetupCursor++;
                SkipSetupPlayersWithNoLegalTile();
            }
            OnStateChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Moves past finished players; if a player somehow has no legal tile at all, sets them up automatically.
        /// When everyone is done, the Tanuki coins go down and the first turn begins.
        /// </summary>
        void SkipSetupPlayersWithNoLegalTile()
        {
            while (SetupCursor < SetupOrder.Count)
            {
                var p = Players[SetupOrder[SetupCursor]];
                if (LegalSetupTiles().Count > 0) return;
                Log($"{p.DisplayName} had no legal setup tile, so their pieces were placed automatically.");
                ApplyFactionSetup(p, AutoSetupPicks(p, AutoSetupAnchors[SetupOrder[SetupCursor] % AutoSetupAnchors.Length]));
                SetupCursor++;
            }

            PlaceTanukiCoins();
            BeginFirstTurn();
        }

        void ResetPreGameRolls()
        {
            SetupOrder.Clear();
            SetupCursor = 0;
            SetupPicks.Clear();
            PreGameRollHistory.Clear();
            PreGameLastDie1.Clear();
            PreGameLastDie2.Clear();
            PreGameSeatsToRoll.Clear();
            FactionPickOrder.Clear();
            FactionPickCursor = 0;
        }

        void StartRollOff()
        {
            PreGameRollHistory.Clear();
            PreGameLastDie1.Clear();
            PreGameLastDie2.Clear();
            PreGameSeatsToRoll.Clear();
            for (int i = 0; i < Players.Count; i++)
            {
                PreGameRollHistory.Add(new List<int>());
                PreGameLastDie1.Add(0);
                PreGameLastDie2.Add(0);
                PreGameSeatsToRoll.Add(i);
            }
        }

        void ResolveRollOff()
        {
            // Anyone whose rolls exactly match someone else's rolls again.
            var tied = Enumerable.Range(0, Players.Count).Where(IsSeatTied).ToList();
            if (tied.Count > 0)
            {
                PreGameSeatsToRoll.AddRange(tied);
                Log($"Tie! {string.Join(", ", tied.Select(t => Players[t].DisplayName))} roll again.");
                return;
            }

            var ranking = PreGameRanking();
            if (CurrentPhase == TurnPhase.PreGame_DraftRoll)
            {
                FactionPickOrder = ranking;
                FactionPickCursor = 0;
                CurrentPhase = TurnPhase.PreGame_FactionPick;
                Log($"Faction pick order: {string.Join(", ", ranking.Select(r => Players[r].DisplayName))}.");
            }
            else
            {
                int first = ranking[0];
                Log($"{Players[first].DisplayName} rolled highest and goes first.");
                FinishSetupAndBeginFirstTurn(first, manualSetup: true); // shuffle and deal
                BeginSetupPlacement();
            }
        }

        static int CompareHistories(List<int> a, List<int> b)
        {
            int n = System.Math.Min(a.Count, b.Count);
            for (int i = 0; i < n; i++)
                if (a[i] != b[i]) return a[i].CompareTo(b[i]);
            return 0; // equal so far: still tied
        }
    }
}
