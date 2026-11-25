#if UNITY_EDITOR || DEBUG
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using CrittersOfCalamity.Engine;
using CrittersOfCalamity.Model;
using CrittersOfCalamity.Multiplayer;

namespace CrittersOfCalamity.UI
{
    /// <summary>
    /// Developer hotkeys (Editor and development builds only), for checking visuals quickly:
    ///   F9  start a local 4-player game and play through dice, faction picks and setup automatically
    ///   F10 play 40 random legal actions (moves, abilities, recruits, battles...) to fill the board
    ///   F11 point the camera at the busiest tile (press again for the next busiest)
    ///   F8  hide or show the whole HUD (for clean screenshots of the board)
    /// Everything goes through the normal command path, exactly like button clicks.
    /// </summary>
    public partial class CrittersTabletopHUD
    {
        private static readonly FactionId[] DebugFactionOrder =
            { FactionId.CapybaraCaravan, FactionId.TroubleMakingTanukis, FactionId.HordeOfTheMillions, FactionId.FirestartingSquirrels,
              FactionId.CurlyTail, FactionId.PlatypusPapacy, FactionId.Necromouser, FactionId.BriaredFlower, FactionId.GoatCabal };
        private System.Random _debugRng = new System.Random(7);
        private int _debugFocusIndex;
        private int _debugSetups = (System.Environment.TickCount & 0x7fffffff) % 9;  // different factions each play session
        private bool _debugBusy;

        private void DebugHotkeys()
        {
            var kb = Keyboard.current;
            if (kb == null || _mpManager == null || _debugBusy) return;
            if (kb.f9Key.wasPressedThisFrame) StartCoroutine(DebugAutoStart());
            else if (kb.f10Key.wasPressedThisFrame) StartCoroutine(DebugRandomActions(40));
            else if (kb.f11Key.wasPressedThisFrame) DebugFocusBusiestTile();
            else if (kb.f8Key.wasPressedThisFrame && _canvas != null) _canvas.enabled = !_canvas.enabled;
            else if (kb.f7Key.wasPressedThisFrame) DebugCycleCurlyTailGear();
        }

        private IEnumerator DebugAutoStart()
        {
            _debugBusy = true;
            if (_engine == null || _engine.Players.Count == 0)
            {
                _mpManager.StartLocalLobby(4);
                yield return null;
                _mpManager.StartMatch();
                yield return null;
            }
            int offset = (_debugSetups++ & 0x7fffffff) % DebugFactionOrder.Length;  // each F9 game seats a different set of factions
            int guard = 0;
            while (_engine != null && _engine.IsPreGame && guard++ < 400)
            {
                if (_engine.CurrentPhase == TurnPhase.PreGame_FactionPick)
                {
                    int seat = _engine.CurrentFactionPicker;
                    var pick = Enumerable.Range(0, DebugFactionOrder.Length)
                        .Select(i => DebugFactionOrder[(i + offset) % DebugFactionOrder.Length])
                        .First(f => _engine.CanPickFaction(seat, f));
                    _mpManager.Submit(GameCommand.PickFaction(pick), seat);
                }
                else if (_engine.CurrentPhase == TurnPhase.PreGame_ReverseFactionSetup)
                {
                    var legal = _engine.LegalSetupTiles();
                    if (legal.Count > 0)
                        _mpManager.Submit(GameCommand.SetupPlace(legal[_debugRng.Next(legal.Count)]), _engine.CurrentSetupPlayer);
                }
                else if (_engine.PreGameSeatsToRoll.Count > 0)
                    _mpManager.Submit(GameCommand.RollDice(), _engine.PreGameSeatsToRoll[0]);
                yield return new WaitForSeconds(0.05f);
            }
            _debugBusy = false;
        }

        /// <summary>F7 (local test only): put the next Golden Arms or Armor piece on Curly Tail to check the gear visuals; after all five, strip them.</summary>
        private void DebugCycleCurlyTailGear()
        {
            if (_engine == null) return;
            foreach (var tile in _engine.Board.Values)
                foreach (var u in tile.Units)
                {
                    if (u.Definition.Id != "curly_tail") continue;
                    var all = _engine.Players[u.OwnerPlayerIndex].Faction.Equipment.Where(e => e.TargetUnitId == "curly_tail").ToList();
                    var next = all.FirstOrDefault(e => !u.EquippedItems.Exists(x => x.Id == e.Id));
                    if (next != null) u.EquippedItems.Add(next); else u.EquippedItems.Clear();
                    _boardRenderer?.RebuildPieces();
                    _boardRenderer?.FocusCameraOnCoord(tile.Coord, 5f);
                    return;
                }
        }

        private IEnumerator DebugRandomActions(int count)
        {
            _debugBusy = true;
            for (int i = 0; i < count && _engine != null && !_engine.IsPreGame && _engine.CurrentPhase != TurnPhase.GameOver; i++)
            {
                var cmd = DebugPickCommand();
                _mpManager.Submit(cmd, GameCommandExecutor.RequiredSeat(_engine, cmd));
                yield return new WaitForSeconds(0.05f);
            }
            _debugBusy = false;
        }

        private GameCommand DebugPickCommand()
        {
            var r = _debugRng;
            if (_engine.IsBattlePending) return GameCommand.BattleDecision(r.Next(2) == 0);
            if (_engine.IsChoicePending)
            {
                var c = _engine.PendingChoice;
                if (c.Tiles.Count > 0) return GameCommand.Choice(CrittersGameEngine.ChooseTile, c.Tiles[r.Next(c.Tiles.Count)]);
                if (c.Options.Count > 0) return GameCommand.Choice(r.Next(c.Options.Count), default(HexCoord));
                return GameCommand.Choice(CrittersGameEngine.ChooseSkip, default(HexCoord));
            }
            var me = _engine.Players[_engine.CurrentTurnPlayerIndex];
            int roll = r.Next(10);
            if (roll < 4)
            {
                var abil = _engine.GetFactionAbilities(me).Where(a => a.Available).ToList();
                if (abil.Count > 0)
                {
                    var ab = abil[r.Next(abil.Count)];
                    HexCoord t1 = default(HexCoord), t2 = default(HexCoord);
                    if (ab.Target != AbilityTarget.None) { var tg = _engine.AbilityTargets(me, ab.Id, null); if (tg.Count > 0) t1 = tg[r.Next(tg.Count)]; }
                    if (ab.Target == AbilityTarget.TwoTiles) { var tg = _engine.AbilityTargets(me, ab.Id, t1); if (tg.Count > 0) t2 = tg[r.Next(tg.Count)]; }
                    var ch = _engine.AbilityChoices(me, ab.Id, t1, t2);
                    return GameCommand.Ability(ab.Id, t1, t2, ch.Count > 1 ? r.Next(ch.Count) : 0);
                }
            }
            if (_engine.CurrentPhase == TurnPhase.MainPhase && _engine.MainPhaseActionsRemaining > 0)
            {
                var mine = _engine.Board.Values.Where(t => t.Units.Any(u => u.OwnerPlayerIndex == me.PlayerIndex)).ToList();
                if (roll < 7 && mine.Count > 0)
                {
                    var from = mine[r.Next(mine.Count)];
                    var dests = _engine.LegalMoveDestinations(from.Coord);
                    if (dests.Count > 0) return GameCommand.Move(from.Coord, dests[r.Next(dests.Count)]);
                }
                if (roll == 7) return GameCommand.Make(GameCommandType.Recruit);
                if (roll == 8 && mine.Count > 0) return GameCommand.Battle(mine[r.Next(mine.Count)].Coord);
            }
            if (_engine.CurrentPhase == TurnPhase.ExcavationPhase && roll < 7)
            {
                var tiles = _engine.LegalBuildingTiles(me);
                if (tiles.Count > 0 && !_engine.UsedFactionCraftThisTurn) return GameCommand.CraftBuilding(tiles[r.Next(tiles.Count)]);
            }
            return GameCommand.Make(GameCommandType.AdvancePhase);
        }

        private void DebugFocusBusiestTile()
        {
            if (_engine == null || _engine.Players.Count == 0) return;
            var tiles = _engine.Board.Values.OrderByDescending(t => t.Units.Count + t.Tokens.Count + (t.Building != null ? 1 : 0)).ToList();
            var t0 = tiles[_debugFocusIndex++ % Mathf.Min(6, tiles.Count)];
            _boardRenderer.FocusCameraOnCoord(t0.Coord, 7f);
        }
    }
}
#endif
