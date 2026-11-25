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
    /// 4. Setup runs automatically in reverse turn order, then the first turn begins.
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

        public bool IsPreGame => CurrentPhase == TurnPhase.PreGame_DraftRoll ||
                                 CurrentPhase == TurnPhase.PreGame_FactionPick ||
                                 CurrentPhase == TurnPhase.PreGame_TurnOrderRoll;

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

        void ResetPreGameRolls()
        {
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
                FinishSetupAndBeginFirstTurn(first);
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
