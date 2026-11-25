using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using CrittersOfCalamity.Engine;
using CrittersOfCalamity.Model;
using CrittersOfCalamity.Multiplayer;
using CrittersOfCalamity.Visuals;

namespace CrittersOfCalamity.UI
{
    /// <summary>
    /// Turn Guide: a panel that walks the turn player through each phase (Excavation: Unearth, Craft, Assemble,
    /// Disassemble; Main: Move, Battle, Loot, Recruit; plus faction abilities), with End Phase / End Turn.
    /// It can be minimized and reopened. Also: the Activity feed (what everyone is doing) and the winner popup.
    /// </summary>
    public partial class CrittersTabletopHUD
    {
        private enum TileAction { None, MoveFrom, MoveTo, Battle, Loot }

        private GameObject _guideRoot;
        private GameObject _guideTab;
        private Text _guideHeader;
        private Text _guidePhaseBar;
        private Text _guideHint;
        private Transform _guideBody;
        private Button _guideEndButton;
        private Text _guideEndLabel;
        private string _guideExpanded;
        private bool _guideMinimized;
        private TileAction _tileAction = TileAction.None;
        private HexCoord _tileActionOrigin;

        private Text _activityText;
        private readonly List<string> _activityLines = new List<string>();
        private TurnPhase _lastSeenPhase;
        private int _lastSeenTurnPlayer = -1;

        private GameObject _winModal;
        private Text _winText;
        private bool _winShown;

        // ------------------------------------------------------------------
        // BUILD
        // ------------------------------------------------------------------

        private void BuildTurnGuide(Transform canvasParent)
        {
            var root = CreatePanel(canvasParent, "TurnGuide", new Vector2(0.585f, 0.20f), new Vector2(0.805f, 0.74f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.09f, 0.11f, 0.15f, 0.96f));
            _guideRoot = root.gameObject;

            CreateText(root, "Title", "TURN GUIDE", 14, TextAnchor.MiddleLeft,
                new Vector2(0.04f, 0.94f), new Vector2(0.80f, 0.99f), Vector2.zero, Vector2.zero, Palette.AuthorityGold);
            CreateButton(root, "BtnMinimize", "Hide", new Vector2(0.80f, 0.945f), new Vector2(0.97f, 0.99f),
                Palette.FromHex("#3E4A59"), () => { _guideMinimized = true; RefreshTurnGuide(); });

            _guideHeader = CreateText(root, "Header", "", 17, TextAnchor.MiddleLeft,
                new Vector2(0.04f, 0.875f), new Vector2(0.97f, 0.94f), Vector2.zero, Vector2.zero, Color.white);
            _guidePhaseBar = CreateText(root, "Phases", "", 14, TextAnchor.MiddleLeft,
                new Vector2(0.04f, 0.83f), new Vector2(0.97f, 0.875f), Vector2.zero, Vector2.zero, new Color(0.65f, 0.70f, 0.78f));
            _guideHint = CreateText(root, "Hint", "", 13, TextAnchor.UpperLeft,
                new Vector2(0.04f, 0.72f), new Vector2(0.97f, 0.83f), Vector2.zero, Vector2.zero, Palette.ParchmentMap);

            _guideBody = BuildScrollList(root, new Vector2(0.03f, 0.13f), new Vector2(0.97f, 0.715f));

            _guideEndButton = CreateButton(root, "BtnEndPhase", "End Phase", new Vector2(0.06f, 0.02f), new Vector2(0.94f, 0.11f),
                Palette.FromHex("#C87A28"), () =>
                {
                    CancelTileAction();
                    _guideExpanded = null;
                    _mpManager.Submit(GameCommand.Make(GameCommandType.AdvancePhase));
                });
            _guideEndLabel = _guideEndButton.GetComponentInChildren<Text>();
            _guideEndLabel.fontSize = 16;

            var tab = CreateButton(canvasParent, "TurnGuideTab", "Show Turn Guide", new Vector2(0.705f, 0.20f), new Vector2(0.805f, 0.245f),
                Palette.FromHex("#C87A28"), () => { _guideMinimized = false; RefreshTurnGuide(); });
            _guideTab = tab.gameObject;

            _guideRoot.SetActive(false);
            _guideTab.SetActive(false);

            // Winner popup
            var win = CreatePanel(canvasParent, "WinnerPopup", new Vector2(0.30f, 0.33f), new Vector2(0.70f, 0.67f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.12f, 0.10f, 0.05f, 0.98f));
            _winModal = win.gameObject;
            _winText = CreateText(win, "Text", "", 26, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.30f), new Vector2(0.95f, 0.95f),
                Vector2.zero, Vector2.zero, Palette.AuthorityGold);
            CreateButton(win, "BtnLook", "Look at the board", new Vector2(0.08f, 0.06f), new Vector2(0.46f, 0.24f),
                Palette.FromHex("#3E4A59"), () => _winModal.SetActive(false));
            CreateButton(win, "BtnMenu", "Back to Main Menu", new Vector2(0.54f, 0.06f), new Vector2(0.92f, 0.24f),
                Palette.FromHex("#B86B28"), async () =>
                {
                    _winModal.SetActive(false);
                    await _mpManager.LeaveAsync();
                });
            _winModal.SetActive(false);
        }

        /// <summary>A vertical, scrollable list. Returns the content transform to add rows to.</summary>
        private Transform BuildScrollList(Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var viewGo = new GameObject("ScrollView");
            viewGo.transform.SetParent(parent, false);
            var viewRt = viewGo.AddComponent<RectTransform>();
            viewRt.anchorMin = anchorMin;
            viewRt.anchorMax = anchorMax;
            viewRt.offsetMin = Vector2.zero;
            viewRt.offsetMax = Vector2.zero;
            var viewImg = viewGo.AddComponent<Image>();
            viewImg.color = new Color(0f, 0f, 0f, 0.15f);
            viewGo.AddComponent<RectMask2D>();
            var scroll = viewGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 25f;

            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(viewGo.transform, false);
            var contentRt = contentGo.AddComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.offsetMin = Vector2.zero;
            contentRt.offsetMax = Vector2.zero;
            var layout = contentGo.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 5f;
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            var fitter = contentGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = contentRt;
            scroll.viewport = viewRt;
            return contentGo.transform;
        }

        private Button GuideButton(string label, Color color, UnityEngine.Events.UnityAction onClick, float height = 40f, bool enabled = true)
        {
            var btn = CreateLayoutButton(_guideBody, "Opt", label, color, onClick);
            btn.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            btn.interactable = enabled;
            btn.GetComponentInChildren<Text>().fontSize = 14;
            return btn;
        }

        private void GuideNote(string text)
        {
            var t = CreateText(_guideBody, "Note", text, 13, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.75f, 0.78f, 0.82f));
            t.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;
        }

        // ------------------------------------------------------------------
        // REFRESH
        // ------------------------------------------------------------------

        private static string PhaseName(TurnPhase phase)
        {
            switch (phase)
            {
                case TurnPhase.DrawPhase: return "Draw Phase";
                case TurnPhase.ExcavationPhase: return "Excavation Phase";
                case TurnPhase.MainPhase: return "Main Phase";
                case TurnPhase.EndPhase: return "End Phase";
                case TurnPhase.GameOver: return "Game Over";
                case TurnPhase.PreGame_DraftRoll: return "Rolling for pick order";
                case TurnPhase.PreGame_FactionPick: return "Picking factions";
                case TurnPhase.PreGame_TurnOrderRoll: return "Rolling for turn order";
                default: return phase.ToString();
            }
        }

        private void RefreshTurnGuide()
        {
            if (_guideRoot == null) return;
            bool inGame = _engine != null && _engine.Players.Count > 0 && !_engine.IsPreGame && _engine.CurrentPhase != TurnPhase.GameOver;
            _guideRoot.SetActive(inGame && !_guideMinimized);
            _guideTab.SetActive(inGame && _guideMinimized);
            if (!inGame || _guideMinimized) return;

            foreach (Transform c in _guideBody) Destroy(c.gameObject);

            var turnPlayer = _engine.Players[_engine.CurrentTurnPlayerIndex];
            bool myTurn = ControlsSeat(_engine.CurrentTurnPlayerIndex);
            var phase = _engine.CurrentPhase;

            _guideHeader.text = myTurn
                ? (_mpManager.IsOnline ? "<b>Your turn</b>" : $"<b>{turnPlayer.DisplayName}'s turn</b>") + $" ({turnPlayer.Faction.Name})"
                : $"<b>{turnPlayer.DisplayName}</b> is taking their turn";
            _guidePhaseBar.text = string.Join("  >  ", new[] { TurnPhase.DrawPhase, TurnPhase.ExcavationPhase, TurnPhase.MainPhase, TurnPhase.EndPhase }
                .Select(p => p == phase ? $"<color=#F2B83B><b>{PhaseName(p).Replace(" Phase", "")}</b></color>"
                    : (int)p < (int)phase ? $"{PhaseName(p).Replace(" Phase", "")} (done)" : PhaseName(p).Replace(" Phase", "")));

            // A battle waiting on someone's decision blocks everything else.
            if (_engine.IsBattlePending)
            {
                var d = _engine.PendingBattle.CurrentDecision;
                _guideHint.text = d != null
                    ? $"Battle in progress. Waiting on {_engine.Players[d.PlayerIndex].DisplayName} to decide about {d.Title}."
                    : "Battle in progress...";
                _guideEndButton.gameObject.SetActive(false);
                return;
            }

            if (!myTurn)
            {
                _guideHint.text = phase == TurnPhase.MainPhase
                    ? $"{turnPlayer.DisplayName} is in the {PhaseName(phase)} with {_engine.MainPhaseActionsRemaining} of 3 actions left. Watch the Activity panel to see what they do."
                    : $"{turnPlayer.DisplayName} is in the {PhaseName(phase)}. Watch the Activity panel to see what they do.";
                _guideEndButton.gameObject.SetActive(false);
                return;
            }

            _guideEndButton.gameObject.SetActive(true);

            if (_tileAction != TileAction.None)
            {
                _guideHint.text = _tileAction == TileAction.MoveFrom ? "<b>Move:</b> click the tile your units are on."
                    : _tileAction == TileAction.MoveTo ? $"<b>Move:</b> now click the adjacent tile to move to (from {_tileActionOrigin})."
                    : _tileAction == TileAction.Battle ? "<b>Battle:</b> click the tile where your units and the enemy are."
                    : "<b>Loot:</b> click a tile with your units and no enemy pieces.";
                GuideButton("Cancel", Palette.FromHex("#7A2828"), () => { CancelTileAction(); RefreshTurnGuide(); });
                _guideEndLabel.text = phase == TurnPhase.MainPhase ? "End Turn" : $"End {PhaseName(phase)}";
                return;
            }

            if (phase == TurnPhase.ExcavationPhase) BuildExcavationOptions(turnPlayer);
            else if (phase == TurnPhase.MainPhase) BuildMainOptions(turnPlayer);
            else _guideHint.text = $"{PhaseName(phase)} resolves automatically.";

            _guideEndLabel.text = phase == TurnPhase.MainPhase ? "End Turn (End Phase runs automatically)" : $"End {PhaseName(phase)}";
        }

        private void BuildExcavationOptions(PlayerState p)
        {
            _guideHint.text = "Take each of these once, in any order (or skip them). Click an option to see your choices.";

            OptionHeader("unearth", $"Unearth a relic from the Excavation Area{Used(_engine.UsedUnearthThisTurn)}", "#35688C", !_engine.UsedUnearthThisTurn);
            if (_guideExpanded == "unearth")
            {
                for (int i = 0; i < _engine.ExcavationArea.Count; i++)
                {
                    int slot = i;
                    var card = _engine.ExcavationArea[i];
                    GuideButton($"   Unearth <b>{card.Name}</b> (cost {card.Cost})", new Color(0.18f, 0.24f, 0.32f, 1f),
                        () => _mpManager.Submit(GameCommand.Make(GameCommandType.Unearth, slot)), 40f, _engine.CanAfford(p, card.Cost));
                }
            }

            OptionHeader("craft", $"Craft{Used(_engine.UsedFactionCraftThisTurn)}", "#4A6B48", !_engine.UsedFactionCraftThisTurn);
            if (_guideExpanded == "craft")
            {
                var sel = _boardRenderer.SelectedCoord;
                string where = sel.HasValue ? $"on tile {sel.Value}" : "(click a tile on the board first)";
                GuideButton($"   Build a {p.Faction.PrimaryBuilding} {where} (cost {p.Faction.PrimaryBuildingCost})",
                    new Color(0.18f, 0.26f, 0.18f, 1f),
                    () => { if (_boardRenderer.SelectedCoord.HasValue) _mpManager.Submit(GameCommand.CraftBuilding(_boardRenderer.SelectedCoord.Value)); },
                    40f, sel.HasValue);
                foreach (var eq in p.Faction.Equipment)
                {
                    string id = eq.Id;
                    GuideButton($"   Craft <b>{eq.Name}</b> (cost {eq.Cost}): {eq.EffectText}", new Color(0.26f, 0.22f, 0.14f, 1f),
                        () => _mpManager.Submit(GameCommand.CraftEquipment(id)), 52f, _engine.CanAfford(p, eq.Cost));
                }
            }

            OptionHeader("assemble", $"Assemble a relic from your hand{Used(_engine.UsedAssembleThisTurn)}", "#6B4E8C", !_engine.UsedAssembleThisTurn);
            if (_guideExpanded == "assemble")
            {
                if (p.RelicHand.Count == 0) GuideNote("   Your hand is empty.");
                for (int i = 0; i < p.RelicHand.Count; i++)
                {
                    int idx = i;
                    var card = p.RelicHand[i];
                    GuideButton($"   Assemble <b>{card.Name}</b> (cost {card.Cost})", new Color(0.22f, 0.18f, 0.30f, 1f),
                        () => _mpManager.Submit(GameCommand.Make(GameCommandType.Assemble, idx)), 40f, _engine.CanAfford(p, card.Cost));
                }
            }

            OptionHeader("disassemble", $"Disassemble a relic for resources{Used(_engine.UsedDisassembleThisTurn)}", "#7A4E24", !_engine.UsedDisassembleThisTurn);
            if (_guideExpanded == "disassemble")
            {
                if (p.RelicHand.Count == 0) GuideNote("   Your hand is empty.");
                for (int i = 0; i < p.RelicHand.Count; i++)
                {
                    int idx = i;
                    var card = p.RelicHand[i];
                    GuideButton($"   Disassemble <b>{card.Name}</b> (gain {card.DisassembleGain})", new Color(0.30f, 0.20f, 0.12f, 1f),
                        () => _mpManager.Submit(GameCommand.Make(GameCommandType.Disassemble, idx)));
                }
            }

            BuildFactionAbilityOptions(p);
        }

        private void BuildMainOptions(PlayerState p)
        {
            int left = _engine.MainPhaseActionsRemaining;
            _guideHint.text = $"<b>{left} of 3 actions left.</b> Pick any mix, including the same one more than once. Faction abilities are free.";

            GuideButton("<b>Move</b>: move units from one tile to an adjacent tile", Palette.FromHex("#35688C"),
                () => { _tileAction = TileAction.MoveFrom; RefreshTurnGuide(); }, 44f, left > 0);
            GuideButton("<b>Battle</b>: fight another faction on a tile you share", Palette.FromHex("#9C2F2F"),
                () => { _tileAction = TileAction.Battle; RefreshTurnGuide(); }, 44f, left > 0);
            GuideButton("<b>Loot</b>: gather from a tile with your units and no enemies", Palette.FromHex("#8A6E2F"),
                () => { _tileAction = TileAction.Loot; RefreshTurnGuide(); }, 44f, left > 0);
            GuideButton("<b>Recruit</b>: place a basic unit at each recruitment building", Palette.FromHex("#4E568C"),
                () => _mpManager.Submit(GameCommand.Make(GameCommandType.Recruit)), 44f, left > 0);

            BuildFactionAbilityOptions(p);
        }

        private void BuildFactionAbilityOptions(PlayerState p)
        {
            var actions = _engine.GetAvailableFactionActions(p);
            if (actions.Count == 0) return;
            OptionHeader("faction", $"{p.Faction.Name} abilities", "#72428C", true);
            if (_guideExpanded != "faction") return;
            var sel = _boardRenderer.SelectedCoord;
            GuideNote(sel.HasValue ? $"   Abilities that target a tile use the selected tile {sel.Value}."
                                   : "   If an ability targets a tile, click that tile on the board first.");
            for (int i = 0; i < actions.Count; i++)
            {
                int idx = i;
                GuideButton($"   {actions[i]}", new Color(0.24f, 0.16f, 0.30f, 1f), () =>
                    _mpManager.Submit(GameCommand.FactionAction(idx, _boardRenderer.SelectedCoord ?? new HexCoord(0, 0))), 48f);
            }
        }

        private void OptionHeader(string key, string label, string hexColor, bool available)
        {
            bool open = _guideExpanded == key;
            GuideButton($"{(open ? "[-]" : "[+]")} <b>{label}</b>", Palette.FromHex(hexColor), () =>
            {
                _guideExpanded = open ? null : key;
                RefreshTurnGuide();
            }, 42f, available);
        }

        private static string Used(bool used) => used ? " (used)" : "";

        private void CancelTileAction() => _tileAction = TileAction.None;

        /// <summary>Board clicks while the guide is waiting for a tile. Returns true if the click was used.</summary>
        private bool HandleGuideTileClick(HexCoord coord)
        {
            switch (_tileAction)
            {
                case TileAction.MoveFrom:
                    _tileActionOrigin = coord;
                    _tileAction = TileAction.MoveTo;
                    RefreshTurnGuide();
                    return true;
                case TileAction.MoveTo:
                    _tileAction = TileAction.None;
                    _mpManager.Submit(GameCommand.Move(_tileActionOrigin, coord));
                    return true;
                case TileAction.Battle:
                    _tileAction = TileAction.None;
                    _mpManager.Submit(GameCommand.Battle(coord));
                    return true;
                case TileAction.Loot:
                    _tileAction = TileAction.None;
                    _mpManager.Submit(GameCommand.Loot(coord));
                    return true;
            }
            return false;
        }

        // ------------------------------------------------------------------
        // ACTIVITY FEED
        // ------------------------------------------------------------------

        private void RecordActivity(GameCommand cmd, bool ok)
        {
            if (_engine == null || _engine.Players.Count == 0) return;
            if (cmd.Seat >= 0 && cmd.Seat < _engine.Players.Count && (ok || cmd.Type == GameCommandType.Battle || cmd.Type == GameCommandType.BattleDecision))
            {
                string who = _engine.Players[cmd.Seat].DisplayName;
                string what = DescribeCommand(cmd);
                if (!string.IsNullOrEmpty(what))
                {
                    string when = _engine.IsPreGame || cmd.Type == GameCommandType.PickFaction || cmd.Type == GameCommandType.RollDice
                        ? "" : $" <color=#8899AA>({PhaseName(_lastSeenPhase).Replace(" Phase", "")})</color>";
                    AddActivity($"<b>{who}</b> {what}{when}");
                }
            }
            NoteTurnAndPhaseChanges();
        }

        private void NoteTurnAndPhaseChanges()
        {
            if (_engine == null || _engine.Players.Count == 0 || _engine.IsPreGame) { if (_engine != null) _lastSeenPhase = _engine.CurrentPhase; return; }
            if (_engine.CurrentTurnPlayerIndex != _lastSeenTurnPlayer)
            {
                var p = _engine.Players[_engine.CurrentTurnPlayerIndex];
                AddActivity($"<color=#F2B83B>Turn {_engine.TurnNumber}: {p.DisplayName}'s turn ({p.Faction.Name})</color>");
            }
            else if (_engine.CurrentPhase != _lastSeenPhase && _engine.CurrentPhase != TurnPhase.GameOver)
            {
                AddActivity($"<color=#8899AA>{_engine.Players[_engine.CurrentTurnPlayerIndex].DisplayName} moved to the {PhaseName(_engine.CurrentPhase)}</color>");
            }
            _lastSeenTurnPlayer = _engine.CurrentTurnPlayerIndex;
            _lastSeenPhase = _engine.CurrentPhase;
        }

        private string DescribeCommand(GameCommand cmd)
        {
            switch (cmd.Type)
            {
                case GameCommandType.RollDice:
                {
                    int s = cmd.Seat;
                    return s < _engine.PreGameLastDie1.Count && _engine.PreGameLastDie1[s] > 0
                        ? $"rolled {_engine.PreGameLastDie1[s]} + {_engine.PreGameLastDie2[s]} = {_engine.PreGameLastDie1[s] + _engine.PreGameLastDie2[s]}"
                        : "rolled the dice";
                }
                case GameCommandType.PickFaction: return $"picked {_engine.Players[cmd.Seat].Faction?.Name}";
                case GameCommandType.AdvancePhase: return $"ended the {PhaseName(_lastSeenPhase)}";
                case GameCommandType.Move: return $"moved units from ({cmd.A}, {cmd.B}) to ({cmd.C}, {cmd.D})";
                case GameCommandType.Battle: return $"started a battle at ({cmd.A}, {cmd.B})";
                case GameCommandType.Loot: return $"looted ({cmd.A}, {cmd.B})";
                case GameCommandType.Recruit: return "recruited";
                case GameCommandType.CraftBuilding: return $"built a {_engine.Players[cmd.Seat].Faction.PrimaryBuilding} at ({cmd.A}, {cmd.B})";
                case GameCommandType.Unearth: return "unearthed a relic";
                case GameCommandType.Assemble: return "assembled a relic";
                case GameCommandType.Disassemble: return "disassembled a relic";
                case GameCommandType.FactionAction:
                {
                    var actions = _engine.GetAvailableFactionActions(_engine.Players[cmd.Seat]);
                    string name = cmd.A >= 0 && cmd.A < actions.Count ? actions[cmd.A].Split('(', ':')[0].Trim() : "a faction ability";
                    return $"used {name}";
                }
                case GameCommandType.CraftEquipment: return $"crafted {cmd.S.Replace('_', ' ')}";
                case GameCommandType.BuyTradedGood: return "bought a Traded Good from the Capybara Trader";
                case GameCommandType.BattleDecision: return cmd.A == 1 ? "used a card in the battle" : "passed on a battle card";
            }
            return null;
        }

        private void AddActivity(string line)
        {
            _activityLines.Add(line);
            while (_activityLines.Count > 14) _activityLines.RemoveAt(0);
            if (_activityText != null) _activityText.text = "<b>ACTIVITY</b>\n" + string.Join("\n", _activityLines);
        }

        private void ResetActivity()
        {
            _activityLines.Clear();
            _lastSeenTurnPlayer = -1;
            _winShown = false;
            _guideExpanded = null;
            _tileAction = TileAction.None;
            if (_activityText != null) _activityText.text = "<b>ACTIVITY</b>";
        }

        // ------------------------------------------------------------------
        // WINNER
        // ------------------------------------------------------------------

        private void RefreshWinPopup()
        {
            if (_winModal == null || _engine == null) return;
            if (_engine.WinnerPlayerIndex < 0 || _engine.CurrentPhase != TurnPhase.GameOver)
            {
                if (_winShown && _engine.Players.Count == 0) _winModal.SetActive(false);
                return;
            }
            if (_winShown) return;
            _winShown = true;
            var w = _engine.Players[_engine.WinnerPlayerIndex];
            _winText.text = $"<size=40><b>{w.DisplayName} wins!</b></size>\n\n{w.Faction.Name} reached {w.Authority} Authority.";
            _winModal.SetActive(true);
            _winModal.transform.SetAsLastSibling();
            AddActivity($"<color=#F2B83B><b>{w.DisplayName} wins with {w.Authority} Authority!</b></color>");
        }
    }
}
