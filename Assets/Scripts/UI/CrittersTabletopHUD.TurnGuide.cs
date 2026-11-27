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
        private enum TileAction { None, MoveFrom, MoveTo, Battle, Loot, Craft, Ability }

        // ability being targeted
        private FactionAbility _ability;
        private bool _abilityHasFirst;
        private HexCoord _abilityFirst, _abilitySecond;
        private List<string> _abilityChoiceList;   // non-null: waiting for the player to pick an option
        // Move: which of your units on the origin tile go (empty = all)
        private readonly HashSet<string> _moveSelection = new HashSet<string>();
        // Battle: several factions on the tile -> pick which one
        private List<int> _battleDefenderChoices;
        private HexCoord _battleTile;
        private bool _guideHighlightsShown;

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
            // The Turn Guide owns the right-hand column, top bar to bottom edge
            var root = CreatePanel(canvasParent, "TurnGuide", new Vector2(0.795f, 0.012f), new Vector2(0.995f, 0.928f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Palette.UIPanelDark);
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
                new Vector2(0.04f, 0.75f), new Vector2(0.97f, 0.83f), Vector2.zero, Vector2.zero, Palette.ParchmentMap);

            _guideBody = BuildScrollList(root, new Vector2(0.03f, 0.095f), new Vector2(0.97f, 0.745f));

            _guideEndButton = CreateButton(root, "BtnEndPhase", "End Phase", new Vector2(0.05f, 0.015f), new Vector2(0.95f, 0.08f),
                Palette.FromHex("#C87A28"), () =>
                {
                    CancelTileAction();
                    _guideExpanded = null;
                    _mpManager.Submit(GameCommand.Make(GameCommandType.AdvancePhase));
                });
            _guideEndLabel = _guideEndButton.GetComponentInChildren<Text>();
            SetFontSize(_guideEndLabel, 16);

            var tab = CreateButton(canvasParent, "TurnGuideTab", "Show Turn Guide", new Vector2(0.895f, 0.012f), new Vector2(0.995f, 0.057f),
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
            SetFontSize(btn.GetComponentInChildren<Text>(), 14);
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

            if (_engine.IsChoicePending)
            {
                var ch = _engine.PendingChoice;
                _guideHint.text = ControlsSeat(ch.PlayerIndex)
                    ? $"<b>{ch.Title}:</b> make your choice in the popup."
                    : $"Waiting on {_engine.Players[ch.PlayerIndex].DisplayName}: {ch.Title}.";
                _guideEndButton.gameObject.SetActive(false);
                return;
            }

            if (_tileAction != TileAction.None || _battleDefenderChoices != null)
            {
                BuildTileActionPrompt(phase);
                _guideEndLabel.text = phase == TurnPhase.MainPhase ? "End Turn" : $"End {PhaseName(phase)}";
                return;
            }

            if (phase == TurnPhase.DrawPhase) BuildDrawOptions(turnPlayer);
            else if (phase == TurnPhase.ExcavationPhase) BuildExcavationOptions(turnPlayer);
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
                    var ub = GuideButton($"   Unearth <b>{card.Name}</b> (discard 1 resource; it goes to your Hand)", new Color(0.18f, 0.24f, 0.32f, 1f),
                        () => _mpManager.Submit(GameCommand.Make(GameCommandType.Unearth, slot)), 40f, p.ResourcePile.Count > 0);
                    AttachCardHover(ub.gameObject, card);
                }
            }

            bool canCraft = _engine.CanCraftBuildingNow(p, out var craftWhy);
            bool comeback = _engine.QualifiesForComeback(p);
            if (p.Faction.PrimaryBuilding != BuildingType.None)
            {
                string label = comeback
                    ? $"<b>Comeback (free craft)</b>: place a {p.Faction.PrimaryBuilding} and 2 basic units"
                    : $"<b>Craft</b> a {p.Faction.PrimaryBuilding} (cost {(p.Faction.Id == FactionId.PlatypusPapacy ? "2 Stone, 2 Wood" : p.Faction.PrimaryBuildingCost.ToString())}){Used(_engine.UsedFactionCraftThisTurn)}";
                GuideButton(label + (canCraft ? " - then click a green tile" : $"<size=14>\n   {craftWhy}</size>"), Palette.FromHex("#4A6B48"),
                    () => { _tileAction = TileAction.Craft; RefreshTurnGuide(); }, 46f, canCraft && _engine.LegalBuildingTiles(p).Count > 0);
            }

            OptionHeader("assemble", $"Assemble a relic from your hand{Used(_engine.UsedAssembleThisTurn)}", "#6B4E8C", !_engine.UsedAssembleThisTurn);
            if (_guideExpanded == "assemble")
            {
                if (p.RelicHand.Count == 0) GuideNote("   Your hand is empty.");
                for (int i = 0; i < p.RelicHand.Count; i++)
                {
                    int idx = i;
                    var card = p.RelicHand[i];
                    var acost = _engine.RelicCostFor(p, card);
                    var ab = GuideButton($"   Assemble <b>{card.Name}</b> (cost {acost})", new Color(0.22f, 0.18f, 0.30f, 1f),
                        () => _mpManager.Submit(GameCommand.Make(GameCommandType.Assemble, idx)), 40f, _engine.CanAfford(p, acost));
                    AttachCardHover(ab.gameObject, card);
                }
            }

            OptionHeader("disassemble", $"Disassemble a relic for resources{Used(_engine.UsedDisassembleThisTurn)}", "#7A4E24", !_engine.UsedDisassembleThisTurn);
            if (_guideExpanded == "disassemble")
            {
                if (p.RelicHand.Count == 0 && p.TableRelics.Count == 0) GuideNote("   No relic cards in your Hand or on your Table.");
                foreach (bool table in new[] { false, true })
                {
                    var pile = table ? p.TableRelics : p.RelicHand;
                    for (int i = 0; i < pile.Count; i++)
                    {
                        int idx = i;
                        bool fromTable = table;
                        var card = pile[i];
                        string where = table ? " <size=14>(from your Table)</size>" : "";
                        if (card.DisassembleChoiceWoodOrStone)
                        {
                            GuideButton($"   Disassemble <b>{card.Name}</b> for 1 Wood{where}", new Color(0.30f, 0.20f, 0.12f, 1f),
                                () => _mpManager.Submit(GameCommand.Disassemble(idx, fromTable, 0)));
                            GuideButton($"   Disassemble <b>{card.Name}</b> for 1 Stone{where}", new Color(0.30f, 0.20f, 0.12f, 1f),
                                () => _mpManager.Submit(GameCommand.Disassemble(idx, fromTable, 1)));
                        }
                        else
                            GuideButton($"   Disassemble <b>{card.Name}</b> (gain {card.DisassembleGain}){where}", new Color(0.30f, 0.20f, 0.12f, 1f),
                                () => _mpManager.Submit(GameCommand.Disassemble(idx, fromTable, 0)));
                    }
                }
            }

            BuildFactionAbilityOptions(p);
            BuildTableRelicOptions(p);
        }

        private void BuildMainOptions(PlayerState p)
        {
            int left = _engine.MainPhaseActionsRemaining;
            int extra = _engine.ExtraBattleActions;
            _guideHint.text = $"<b>{left} of 3 actions left.</b>" + (extra > 0 ? $" Plus <b>{extra} extra Battle</b> from Rally." : "") +
                              " Pick any mix, including the same one more than once. Faction abilities are free.";

            GuideButton("<b>Move</b>: move units from one tile to an adjacent tile", Palette.FromHex("#35688C"),
                () => { _tileAction = TileAction.MoveFrom; RefreshTurnGuide(); }, 44f, left > 0);
            GuideButton("<b>Battle</b>: fight another faction on a tile you share" + (extra > 0 ? " (uses your extra Battle first)" : ""), Palette.FromHex("#9C2F2F"),
                () => { _tileAction = TileAction.Battle; RefreshTurnGuide(); }, 44f, left > 0 || extra > 0);
            GuideButton("<b>Loot</b>: gather from a tile with your units and no enemies", Palette.FromHex("#8A6E2F"),
                () => { _tileAction = TileAction.Loot; RefreshTurnGuide(); }, 44f, left > 0);
            GuideButton("<b>Recruit</b>: place a basic unit at each recruitment building", Palette.FromHex("#4E568C"),
                () => _mpManager.Submit(GameCommand.Make(GameCommandType.Recruit)), 44f, left > 0);

            BuildFactionAbilityOptions(p);
            BuildTableRelicOptions(p);
        }

        /// <summary>Relic cards on your Table that can be used now, on your own turn (Rally, Ceremonial Bow and Arrow).</summary>
        private void BuildTableRelicOptions(PlayerState p)
        {
            bool road = _engine.CanUseTableRelic(p, "abandoned_road");
            bool bow = _engine.CanUseTableRelic(p, "ceremonial_bow_and_arrow");
            if (!road && !bow) return;

            OptionHeader("relics", "Relic cards on your Table you can use now", "#8C5328", true);
            if (_guideExpanded != "relics") return;
            if (road)
                GuideButton("   <b>Rally</b>: discard it for 1 extra Battle action this turn (not one of your 3)",
                    new Color(0.30f, 0.20f, 0.12f, 1f), () => _mpManager.Submit(GameCommand.UseTableRelic("abandoned_road")), 48f);
            if (bow)
            {
                GuideNote("   <b>Ceremonial Bow and Arrow</b>: pick another player's facedown Table card to make them discard it:");
                foreach (var other in _engine.Players.Where(o => o.PlayerIndex != p.PlayerIndex))
                {
                    for (int i = 0; i < other.TableRelics.Count; i++)
                    {
                        int target = other.PlayerIndex, index = i;
                        GuideButton($"      {other.DisplayName}'s facedown card #{i + 1}", new Color(0.30f, 0.20f, 0.12f, 1f),
                            () => _mpManager.Submit(GameCommand.UseTableRelic("ceremonial_bow_and_arrow", target, index)), 36f);
                    }
                }
            }
        }

        private void BuildDrawOptions(PlayerState p)
        {
            _guideHint.text = "You drew your relic card. You have Draw phase abilities you can use now (or end the phase).";
            BuildFactionAbilityOptions(p);
        }

        /// <summary>Faction abilities for the current phase (and general ones such as the Catapult), with why any are unavailable.</summary>
        private void BuildFactionAbilityOptions(PlayerState p)
        {
            var abilities = _engine.GetFactionAbilities(p).Where(a => a.Phase == _engine.CurrentPhase || a.Phase2 == _engine.CurrentPhase).ToList();
            if (abilities.Count == 0) return;
            bool drawPhase = _engine.CurrentPhase == TurnPhase.DrawPhase;
            string key = "faction";
            OptionHeader(key, $"{p.Faction.Name} abilities ({abilities.Count(a => a.Available)} usable now)", "#72428C", true);
            if (_guideExpanded != key && !drawPhase) return;
            foreach (var ab in abilities)
            {
                var a = ab;
                string tag = a.IsGeneral ? " <size=13>(any faction)</size>" : "";
                string text = $"   <b>{a.Name}</b>{tag}<size=14>\n   {a.Summary}" + (a.Available ? "" : $"\n   <color=#E0A060>{a.Blocked}</color>") + "</size>";
                GuideButton(text, a.Available ? new Color(0.30f, 0.18f, 0.38f, 1f) : new Color(0.20f, 0.18f, 0.22f, 1f),
                    () => BeginAbility(a), 30f + 15f * Mathf.Ceil(text.Length / 95f), a.Available);
            }
        }

        private void BeginAbility(FactionAbility a)
        {
            ClearTileState();
            _ability = a;
            if (a.Target == AbilityTarget.None)
            {
                FinishAbilityTargets();
                return;
            }
            _tileAction = TileAction.Ability;
            RefreshTurnGuide();
        }

        /// <summary>All tiles are chosen: ask for an option if there is more than one, else submit.</summary>
        private void FinishAbilityTargets()
        {
            var p = _engine.Players[_engine.CurrentTurnPlayerIndex];
            var choices = _engine.AbilityChoices(p, _ability.Id, _abilityFirst, _abilitySecond);
            if (choices.Count > 1)
            {
                _abilityChoiceList = choices;
                _tileAction = TileAction.Ability;
                RefreshTurnGuide();
                return;
            }
            SubmitAbility(0);
        }

        private void SubmitAbility(int option)
        {
            var id = _ability.Id;
            var c1 = _abilityFirst; var c2 = _abilitySecond;
            ClearTileState();
            _mpManager.Submit(GameCommand.Ability(id, c1, c2, option));
            RefreshTurnGuide();
        }

        private void ClearTileState()
        {
            _tileAction = TileAction.None;
            _ability = null;
            _abilityHasFirst = false;
            _abilityFirst = default(HexCoord);
            _abilitySecond = default(HexCoord);
            _abilityChoiceList = null;
            _moveSelection.Clear();
            _battleDefenderChoices = null;
        }

        /// <summary>The guide while it waits for a tile click (or an option) for Move, Battle, Loot, Craft or an ability.</summary>
        private void BuildTileActionPrompt(TurnPhase phase)
        {
            var p = _engine.Players[_engine.CurrentTurnPlayerIndex];
            if (_battleDefenderChoices != null)
            {
                _guideHint.text = $"<b>Battle at {_battleTile}:</b> more than one faction is here. Who do you attack?";
                foreach (var seat in _battleDefenderChoices)
                {
                    int s2 = seat;
                    GuideButton($"Attack {_engine.Players[seat].DisplayName} ({_engine.Players[seat].Faction.Name})", Palette.FromHex("#9C2F2F"),
                        () => { var t = _battleTile; ClearTileState(); _mpManager.Submit(GameCommand.BattleAgainst(t, s2)); RefreshTurnGuide(); });
                }
            }
            else switch (_tileAction)
            {
                case TileAction.MoveFrom: _guideHint.text = "<b>Move:</b> click the tile your units are on."; break;
                case TileAction.MoveTo:
                {
                    _guideHint.text = $"<b>Move</b> from {_tileActionOrigin}: pick which units go (all are selected to start), then click a green tile.";
                    var tile = _engine.Board[_tileActionOrigin];
                    foreach (var u in tile.Units.Where(x => x.OwnerPlayerIndex == p.PlayerIndex))
                    {
                        var unit = u;
                        bool on = _moveSelection.Count == 0 || _moveSelection.Contains(u.InstanceId);
                        string state = u.CannotMoveThisTurn ? " <size=13>(can't move this turn)</size>" : "";
                        GuideButton($"{(on ? "[x]" : "[  ]")} {u.Definition.Name} ({u.CurrentHP} HP){state}", on ? new Color(0.20f, 0.32f, 0.44f, 1f) : new Color(0.18f, 0.18f, 0.22f, 1f),
                            () => ToggleMoveUnit(unit, tile, p), 32f, !u.CannotMoveThisTurn);
                    }
                    break;
                }
                case TileAction.Battle: _guideHint.text = "<b>Battle:</b> click a green tile where your units and an enemy are."; break;
                case TileAction.Loot: _guideHint.text = "<b>Loot:</b> click a green tile with your units and no enemy pieces."; break;
                case TileAction.Craft: _guideHint.text = $"<b>Craft:</b> click a green tile for your {p.Faction.PrimaryBuilding}."; break;
                case TileAction.Ability:
                    if (_abilityChoiceList != null)
                    {
                        _guideHint.text = $"<b>{_ability.Name}:</b> choose one.";
                        for (int i = 0; i < _abilityChoiceList.Count; i++)
                        {
                            int opt = i;
                            GuideButton(_abilityChoiceList[i], new Color(0.30f, 0.18f, 0.38f, 1f), () => SubmitAbility(opt));
                        }
                    }
                    else
                        _guideHint.text = $"<b>{_ability.Name}:</b> {(_abilityHasFirst ? _ability.Prompt2 : _ability.Prompt1)} (Green tiles are legal.)";
                    break;
            }
            GuideButton("Cancel", Palette.FromHex("#7A2828"), () => { ClearTileState(); RefreshTurnGuide(); });
        }

        private void ToggleMoveUnit(UnitInstance u, HexTileState tile, PlayerState p)
        {
            if (_moveSelection.Count == 0)
                foreach (var x in tile.Units.Where(x => x.OwnerPlayerIndex == p.PlayerIndex && !x.CannotMoveThisTurn)) _moveSelection.Add(x.InstanceId);
            if (!_moveSelection.Remove(u.InstanceId)) _moveSelection.Add(u.InstanceId);
            if (_moveSelection.Count == 0) _moveSelection.Add(u.InstanceId); // keep at least one
            RefreshTurnGuide();
        }

        /// <summary>The tiles to highlight for the current tile action (green).</summary>
        private List<HexCoord> GuideLegalTiles()
        {
            var p = _engine.Players[_engine.CurrentTurnPlayerIndex];
            int me = p.PlayerIndex;
            var board = _engine.Board.Values;
            switch (_tileAction)
            {
                case TileAction.MoveFrom:
                    return board.Where(t => t.Units.Any(u => u.OwnerPlayerIndex == me && !u.CannotMoveThisTurn)).Select(t => t.Coord).ToList();
                case TileAction.MoveTo:
                    return _engine.LegalMoveDestinations(_tileActionOrigin, _moveSelection.ToList());
                case TileAction.Battle:
                    return board.Where(t => t.Units.Any(u => u.OwnerPlayerIndex == me) &&
                        (t.Units.Any(u => u.OwnerPlayerIndex != me) || (t.Building != null && t.Building.OwnerPlayerIndex != me) ||
                         t.Tokens.Any(k => k.OwnerPlayerIndex != me && k.Type != TokenType.Coin) ||
                         (t.OverlayOwnerPlayerIndex >= 0 && t.OverlayOwnerPlayerIndex != me && t.Overlay != FactionOverlayTileType.FallenLeaf_Unflipped &&
                          t.Overlay != FactionOverlayTileType.CapybaraStream && t.Overlay != FactionOverlayTileType.Fire))).Select(t => t.Coord).ToList();
                case TileAction.Loot:
                    return board.Where(t => _engine.CanLootTile(t.Coord)).Select(t => t.Coord).ToList();
                case TileAction.Craft:
                    return _engine.LegalBuildingTiles(p);
                case TileAction.Ability:
                    if (_ability == null || _abilityChoiceList != null) return new List<HexCoord>();
                    return _engine.AbilityTargets(p, _ability.Id, _abilityHasFirst ? (HexCoord?)_abilityFirst : null);
            }
            return new List<HexCoord>();
        }

        /// <summary>Shows green tiles for whatever the guide (or a pending choice) is waiting on.</summary>
        private void RefreshGuideHighlights()
        {
            if (_boardRenderer == null || _engine == null || _engine.Players.Count == 0 || _engine.IsPreGame) return;
            List<HexCoord> legal = null;
            var picked = new List<HexCoord>();
            if (_engine.IsChoicePending)
            {
                var pc = _engine.PendingChoice;
                if (ControlsSeat(pc.PlayerIndex) && pc.Tiles.Count > 0) legal = pc.Tiles;
                else if (ControlsSeat(pc.PlayerIndex) && pc.UnitIds != null && pc.UnitIds.Count > 0)   // choices about units: green where those units stand
                    legal = _engine.Board.Values.Where(t => t.Units.Any(u => pc.UnitIds.Contains(u.InstanceId))).Select(t => t.Coord).ToList();
            }
            else if (_tileAction != TileAction.None && ControlsSeat(_engine.CurrentTurnPlayerIndex))
            {
                legal = GuideLegalTiles();
                if (_tileAction == TileAction.MoveTo) picked.Add(_tileActionOrigin);
                if (_tileAction == TileAction.Ability && _abilityHasFirst) picked.Add(_abilityFirst);
            }
            if (legal != null || picked.Count > 0)
            {
                _boardRenderer.SetHighlights(legal, new Color(0.30f, 0.85f, 0.35f, 1f), picked, Palette.AuthorityGold);
                _guideHighlightsShown = true;
            }
            else if (_guideHighlightsShown)
            {
                _boardRenderer.SetHighlights(null, Color.clear, null, Color.clear);
                _guideHighlightsShown = false;
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

        private void CancelTileAction() => ClearTileState();

        /// <summary>Board clicks while the guide is waiting for a tile. Returns true if the click was used.</summary>
        private bool HandleGuideTileClick(HexCoord coord)
        {
            if (HandleChoiceTileClick(coord)) return true;
            if (_tileAction == TileAction.None) return false;
            var p = _engine.Players[_engine.CurrentTurnPlayerIndex];
            switch (_tileAction)
            {
                case TileAction.MoveFrom:
                    if (!_engine.Board[coord].Units.Any(u => u.OwnerPlayerIndex == p.PlayerIndex)) return true;
                    _tileActionOrigin = coord;
                    _moveSelection.Clear();
                    _tileAction = TileAction.MoveTo;
                    RefreshTurnGuide();
                    return true;
                case TileAction.MoveTo:
                {
                    if (coord.Equals(_tileActionOrigin)) return true;
                    var ids = _moveSelection.ToList();
                    var from = _tileActionOrigin;
                    ClearTileState();
                    _mpManager.Submit(GameCommand.MoveUnits(from, coord, ids));
                    return true;
                }
                case TileAction.Battle:
                {
                    var t = _engine.Board[coord];
                    var defenders = t.Units.Where(u => u.OwnerPlayerIndex != p.PlayerIndex).Select(u => u.OwnerPlayerIndex).Distinct().ToList();
                    ClearTileState();
                    if (defenders.Count > 1)
                    {
                        _battleDefenderChoices = defenders;
                        _battleTile = coord;
                        RefreshTurnGuide();
                    }
                    else _mpManager.Submit(GameCommand.Battle(coord));
                    return true;
                }
                case TileAction.Loot:
                    ClearTileState();
                    _mpManager.Submit(GameCommand.Loot(coord));
                    return true;
                case TileAction.Craft:
                    ClearTileState();
                    _mpManager.Submit(GameCommand.CraftBuilding(coord));
                    return true;
                case TileAction.Ability:
                {
                    if (_ability == null || _abilityChoiceList != null) return true;
                    var legal = _engine.AbilityTargets(p, _ability.Id, _abilityHasFirst ? (HexCoord?)_abilityFirst : null);
                    if (!legal.Contains(coord)) { ShowLocalNotice($"{_ability.Name}: pick one of the green tiles."); return true; }
                    if (!_abilityHasFirst)
                    {
                        _abilityFirst = coord;
                        _abilityHasFirst = true;
                        if (_ability.Target == AbilityTarget.TwoTiles) { RefreshTurnGuide(); return true; }
                    }
                    else _abilitySecond = coord;
                    FinishAbilityTargets();
                    return true;
                }
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
                AddActivity($"<color=#F2B83B>Round {_engine.RoundNumber}, turn {_engine.TurnNumber}: {p.DisplayName}'s turn ({p.Faction.Name})</color>");
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
                        ? $"rolled {_engine.PreGameLastDie1[s]}"
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
                    string id = (cmd.S ?? "").Split('#')[0];
                    var ab = _engine.GetFactionAbilities(_engine.Players[cmd.Seat]).FirstOrDefault(a => a.Id == id);
                    return $"used {(ab != null ? ab.Name : "a faction ability")}";
                }
                case GameCommandType.ResolveChoice: return "made a choice";
                case GameCommandType.CraftEquipment: return $"crafted {cmd.S.Replace('_', ' ')}";
                case GameCommandType.BuyTradedGood: return "bought a Traded Good from the Capybara Trader";
                case GameCommandType.BattleDecision: return cmd.A == 1 ? "used a card in the battle" : "passed on a battle card";
                case GameCommandType.SetupPlace: return $"picked tile ({cmd.A}, {cmd.B}) for setup";
                case GameCommandType.UseTableRelic:
                    return cmd.S == "abandoned_road" ? "used Rally for an extra Battle"
                         : $"used Ceremonial Bow and Arrow on {(cmd.A >= 0 && cmd.A < _engine.Players.Count ? _engine.Players[cmd.A].DisplayName : "another player")}";
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
