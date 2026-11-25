using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CrittersOfCalamity.Engine;
using CrittersOfCalamity.Model;

namespace CrittersOfCalamity.Multiplayer
{
    /// <summary>Every player action that changes the game. Online, these are the only things sent between computers.</summary>
    public enum GameCommandType
    {
        AdvancePhase = 0,
        Move = 1,            // A,B = from (q,r)  C,D = to (q,r)
        Battle = 2,          // A,B = tile (q,r)
        Loot = 3,            // A,B = tile (q,r)
        Recruit = 4,
        CraftBuilding = 5,   // A,B = tile (q,r)
        Unearth = 6,         // A = Excavation Area slot
        Assemble = 7,        // A = hand index
        Disassemble = 8,     // A = hand index
        FactionAction = 9,   // S = ability id[#option], A,B = first tile (q,r), C,D = second tile (q,r)
        CraftEquipment = 10, // S = equipment id
        BuyTradedGood = 11,  // A = stall slot
        BattleDecision = 12, // A = 1 use / 0 pass
        RollDice = 13,       // pre-game roll for pick order / turn order (the seat rolls for itself)
        PickFaction = 14,    // A = FactionId
        SetupPlace = 15,     // A,B = tile (q,r) for your starting pieces (click again to un-pick)
        UseTableRelic = 16,  // S = relic id, A = target player, B = target Table card index
        ResolveChoice = 17   // A = option (-1 tile, -2 skip), B,C = tile (q,r)
    }

    /// <summary>
    /// One player action. Online play is lockstep: every computer runs the same engine from the same seed and
    /// applies the same commands in the same order (the host decides the order), so the games stay identical.
    /// </summary>
    [Serializable]
    public struct GameCommand
    {
        public int Seat;
        public GameCommandType Type;
        public int A, B, C, D;
        public string S;

        public static GameCommand Make(GameCommandType type, int a = 0, int b = 0, int c = 0, int d = 0, string s = "")
            => new GameCommand { Type = type, A = a, B = b, C = c, D = d, S = s ?? "" };

        public static GameCommand Move(HexCoord from, HexCoord to) => Make(GameCommandType.Move, from.q, from.r, to.q, to.r);
        public static GameCommand Battle(HexCoord at) => Make(GameCommandType.Battle, at.q, at.r);
        public static GameCommand Loot(HexCoord at) => Make(GameCommandType.Loot, at.q, at.r);
        public static GameCommand CraftBuilding(HexCoord at) => Make(GameCommandType.CraftBuilding, at.q, at.r);
        public static GameCommand Ability(string id, HexCoord t1, HexCoord t2, int option = 0) =>
            Make(GameCommandType.FactionAction, t1.q, t1.r, t2.q, t2.r, option > 0 ? id + "#" + option : id);
        public static GameCommand Choice(int option, HexCoord tile) => Make(GameCommandType.ResolveChoice, option, tile.q, tile.r);
        public static GameCommand MoveUnits(HexCoord from, HexCoord to, IEnumerable<string> unitIds) =>
            Make(GameCommandType.Move, from.q, from.r, to.q, to.r, unitIds == null ? "" : string.Join(",", unitIds));
        public static GameCommand Disassemble(int index, bool fromTable, int woodOrStone) =>
            Make(GameCommandType.Disassemble, index, fromTable ? 1 : 0, woodOrStone);
        public static GameCommand BattleAgainst(HexCoord at, int defenderSeat) => Make(GameCommandType.Battle, at.q, at.r, defenderSeat + 1);
        public static GameCommand CraftEquipment(string equipmentId) => Make(GameCommandType.CraftEquipment, s: equipmentId);
        public static GameCommand BattleDecision(bool use) => Make(GameCommandType.BattleDecision, use ? 1 : 0);
        public static GameCommand RollDice() => Make(GameCommandType.RollDice);
        public static GameCommand PickFaction(FactionId faction) => Make(GameCommandType.PickFaction, (int)faction);
        public static GameCommand SetupPlace(HexCoord at) => Make(GameCommandType.SetupPlace, at.q, at.r);
        public static GameCommand UseTableRelic(string relicId, int targetPlayer = -1, int targetIndex = -1) =>
            Make(GameCommandType.UseTableRelic, targetPlayer, targetIndex, s: relicId);

        /// <summary>Compact text form for the network: seat|type|a|b|c|d|s</summary>
        public string Encode()
        {
            var inv = CultureInfo.InvariantCulture;
            return string.Join("|", Seat.ToString(inv), ((int)Type).ToString(inv), A.ToString(inv), B.ToString(inv),
                C.ToString(inv), D.ToString(inv), (S ?? "").Replace("|", ""));
        }

        public static bool TryDecode(string text, out GameCommand cmd)
        {
            cmd = default;
            if (string.IsNullOrEmpty(text)) return false;
            var parts = text.Split('|');
            if (parts.Length < 7) return false;
            var inv = CultureInfo.InvariantCulture;
            if (!int.TryParse(parts[0], NumberStyles.Integer, inv, out cmd.Seat)) return false;
            if (!int.TryParse(parts[1], NumberStyles.Integer, inv, out int type) || !Enum.IsDefined(typeof(GameCommandType), type)) return false;
            cmd.Type = (GameCommandType)type;
            if (!int.TryParse(parts[2], NumberStyles.Integer, inv, out cmd.A)) return false;
            if (!int.TryParse(parts[3], NumberStyles.Integer, inv, out cmd.B)) return false;
            if (!int.TryParse(parts[4], NumberStyles.Integer, inv, out cmd.C)) return false;
            if (!int.TryParse(parts[5], NumberStyles.Integer, inv, out cmd.D)) return false;
            cmd.S = parts[6];
            return true;
        }

        public override string ToString() => $"Seat {Seat + 1}: {Type} ({A},{B},{C},{D}{(string.IsNullOrEmpty(S) ? "" : "," + S)})";
    }

    /// <summary>Applies commands to the rules engine and decides who may send them.</summary>
    public static class GameCommandExecutor
    {
        /// <summary>
        /// Which seat must act right now for this command. Battle decisions belong to the player being asked;
        /// everything else belongs to the turn player.
        /// </summary>
        public static int RequiredSeat(CrittersGameEngine engine, GameCommand cmd)
        {
            if (cmd.Type == GameCommandType.BattleDecision)
                return engine.PendingBattle?.CurrentDecision != null ? engine.PendingBattle.CurrentDecision.PlayerIndex : -1;
            if (cmd.Type == GameCommandType.ResolveChoice)
                return engine.PendingChoice != null ? engine.PendingChoice.PlayerIndex : -1;
            if (cmd.Type == GameCommandType.PickFaction)
                return engine.CurrentFactionPicker;
            if (cmd.Type == GameCommandType.SetupPlace)
                return engine.CurrentSetupPlayer;
            if (cmd.Type == GameCommandType.RollDice)
                return engine.PreGameSeatsToRoll.Count > 0 ? engine.PreGameSeatsToRoll[0] : -1; // any seat that still has to roll may
            return engine.CurrentTurnPlayerIndex;
        }

        public static bool IsAllowed(CrittersGameEngine engine, GameCommand cmd, out string reason)
        {
            reason = null;
            if (engine == null || engine.Players.Count == 0) { reason = "The game hasn't started yet."; return false; }
            if (engine.CurrentPhase == TurnPhase.GameOver) { reason = "The game is over."; return false; }

            if (engine.IsPreGame)
            {
                if (cmd.Type == GameCommandType.RollDice)
                {
                    if (engine.CanRollPreGameDice(cmd.Seat)) return true;
                    reason = "You don't need to roll right now.";
                    return false;
                }
                if (cmd.Type == GameCommandType.PickFaction)
                {
                    int picker = engine.CurrentFactionPicker;
                    if (picker < 0) { reason = "It isn't time to pick factions."; return false; }
                    if (cmd.Seat != picker) { reason = $"It's {engine.Players[picker].DisplayName}'s pick."; return false; }
                    if (!engine.CanPickFaction(cmd.Seat, (FactionId)cmd.A)) { reason = "That faction is already taken."; return false; }
                    return true;
                }
                if (cmd.Type == GameCommandType.SetupPlace)
                {
                    int setter = engine.CurrentSetupPlayer;
                    if (setter < 0) { reason = "It isn't time to place starting pieces."; return false; }
                    if (cmd.Seat != setter) { reason = $"{engine.Players[setter].DisplayName} is setting up."; return false; }
                    if (!engine.CanPlaceSetupTile(cmd.Seat, new HexCoord(cmd.A, cmd.B), out var why)) { reason = why; return false; }
                    return true;
                }
                reason = "The game hasn't started yet.";
                return false;
            }
            if (cmd.Type == GameCommandType.RollDice || cmd.Type == GameCommandType.PickFaction || cmd.Type == GameCommandType.SetupPlace)
            {
                reason = "Setup is already finished.";
                return false;
            }

            int required = RequiredSeat(engine, cmd);
            if (required < 0) { reason = "There's nothing to decide right now."; return false; }
            if (cmd.Seat != required)
            {
                reason = cmd.Type == GameCommandType.BattleDecision || cmd.Type == GameCommandType.ResolveChoice
                    ? $"Waiting on {engine.Players[required].DisplayName} to decide."
                    : $"It's {engine.Players[required].DisplayName}'s turn.";
                return false;
            }
            if (engine.IsBattlePending && cmd.Type != GameCommandType.BattleDecision)
            {
                reason = "Finish the current battle first.";
                return false;
            }
            if (engine.IsChoicePending && cmd.Type != GameCommandType.ResolveChoice)
            {
                reason = $"Waiting on {engine.Players[engine.PendingChoice.PlayerIndex].DisplayName}: {engine.PendingChoice.Title}.";
                return false;
            }
            if (cmd.Type == GameCommandType.ResolveChoice &&
                !engine.CanResolveChoice(cmd.Seat, cmd.A, new HexCoord(cmd.B, cmd.C), out reason))
                return false;
            return true;
        }

        /// <summary>Runs the command on the engine. Returns false when the engine rejected the action as illegal.</summary>
        public static bool Apply(CrittersGameEngine engine, GameCommand cmd)
        {
            engine.BeginRemovalEvent();
            switch (cmd.Type)
            {
                case GameCommandType.AdvancePhase:
                    engine.AdvancePhase();
                    return true;
                case GameCommandType.Move:
                    return engine.TryMoveUnits(new HexCoord(cmd.A, cmd.B), new HexCoord(cmd.C, cmd.D),
                        string.IsNullOrEmpty(cmd.S) ? null : cmd.S.Split(','));
                case GameCommandType.Battle:
                    engine.TryBattleOnTile(new HexCoord(cmd.A, cmd.B), cmd.C - 1); // C = defender seat + 1 (0 = first enemy found)
                    return true; // results (or the pause for a decision) show up through engine events
                case GameCommandType.Loot:
                    return engine.TryLootTile(new HexCoord(cmd.A, cmd.B));
                case GameCommandType.Recruit:
                    return engine.TryRecruit();
                case GameCommandType.CraftBuilding:
                    return engine.TryFactionCraftBuilding(new HexCoord(cmd.A, cmd.B));
                case GameCommandType.Unearth:
                    return engine.TryUnearthRelic(cmd.A);
                case GameCommandType.Assemble:
                    return engine.TryAssembleRelicFromHand(cmd.A);
                case GameCommandType.Disassemble:
                    return engine.TryDisassembleRelicFromHand(cmd.A, cmd.B == 1, cmd.C);
                case GameCommandType.FactionAction:
                {
                    string id = cmd.S ?? "";
                    int option = 0, hash = id.IndexOf('#');
                    if (hash >= 0) { int.TryParse(id.Substring(hash + 1), out option); id = id.Substring(0, hash); }
                    return engine.UseFactionAbility(id, new HexCoord(cmd.A, cmd.B), new HexCoord(cmd.C, cmd.D), option);
                }
                case GameCommandType.CraftEquipment:
                    return engine.TryCraftAndEquipFactionEquipment(cmd.S);
                case GameCommandType.BuyTradedGood:
                    return engine.TryBuyTradedGoodFromCapybaraStall(cmd.A);
                case GameCommandType.BattleDecision:
                    engine.RespondToBattleDecision(cmd.A == 1);
                    return true;
                case GameCommandType.RollDice:
                    return engine.RollPreGameDice(cmd.Seat);
                case GameCommandType.PickFaction:
                    return engine.PickFaction(cmd.Seat, (FactionId)cmd.A);
                case GameCommandType.SetupPlace:
                    return engine.PlaceSetupTile(cmd.Seat, new HexCoord(cmd.A, cmd.B));
                case GameCommandType.UseTableRelic:
                    return engine.UseTableRelic(cmd.Seat, cmd.S, cmd.A, cmd.B);
                case GameCommandType.ResolveChoice:
                    return engine.ResolveChoice(cmd.Seat, cmd.A, new HexCoord(cmd.B, cmd.C));
            }
            return false;
        }

        /// <summary>
        /// A fingerprint of the game state that matters for play. The host sends its value with every command;
        /// a client whose value differs has drifted out of sync. (Logs and camera state are not included.)
        /// </summary>
        public static int Checksum(CrittersGameEngine engine)
        {
            unchecked
            {
                int h = 17;
                void Mix(int v) { h = h * 31 + v; }
                Mix(engine.TurnNumber);
                Mix(engine.CurrentTurnPlayerIndex);
                Mix((int)engine.CurrentPhase);
                Mix(engine.MainPhaseActionsRemaining);
                Mix(engine.RelicDeck.Count);
                Mix(engine.ResourceDeck.Count);
                Mix(engine.ExcavationArea.Count);
                Mix(engine.IsBattlePending ? 1 : 0);
                Mix(engine.FactionPickCursor);
                Mix(engine.SetupCursor);
                Mix(engine.SetupPicks.Count);
                Mix(engine.ExtraBattleActions);
                Mix(engine.QueuedChoices.Count);
                Mix(engine.IsChoicePending ? 1000 + engine.PendingChoice.PlayerIndex * 37 + engine.PendingChoice.Options.Count * 7 + engine.PendingChoice.Tiles.Count : 0);
                foreach (var kv in engine.AbilityUses.OrderBy(k => k.Key, StringComparer.Ordinal)) { Mix(kv.Key.Length); Mix(kv.Value); }
                Mix(engine.PreGameSeatsToRoll.Count);
                foreach (var hist in engine.PreGameRollHistory)
                    foreach (var roll in hist) Mix(roll);
                foreach (var p in engine.Players)
                {
                    Mix(p.Faction != null ? (int)p.Faction.Id : -1);
                    Mix(p.Authority);
                    Mix(p.ResourcePile.Count);
                    foreach (var r in p.ResourcePile) Mix((int)r);
                    Mix(p.RelicHand.Count);
                    Mix(p.TableRelics.Count);
                    Mix(p.PlatypusFaith); Mix(p.PlatyPopeLevel); Mix(p.CrusadeTurnsRemaining);
                    Mix(p.BriaredFlowerThorns); Mix(p.NecromouserCauldronDeadCount);
                    Mix(p.PurchasedTradedGoods.Count); Mix(p.HorseHairBrushReady ? 1 : 0);
                }
                foreach (var tile in engine.Board.Values)
                {
                    Mix(tile.Units.Count);
                    foreach (var u in tile.Units) { Mix(u.OwnerPlayerIndex); Mix(u.CurrentHP); Mix(u.IsGuest ? 1 : 0); }
                    Mix(tile.Building != null ? (int)tile.Building.Type + 1 : 0);
                    Mix(tile.Tokens.Count);
                    Mix((int)tile.Overlay);
                }
                return h;
            }
        }
    }
}
