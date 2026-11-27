using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using CrittersOfCalamity.Engine;
using CrittersOfCalamity.Model;
using CrittersOfCalamity.Multiplayer;
using CrittersOfCalamity.Visuals;

namespace CrittersOfCalamity.UI
{
    /// <summary>
    /// The screen before the first turn: roll dice for faction pick order (Part 3), pick factions from cards,
    /// then roll dice for turn order (Part 5). Dice tumble on screen before showing the real roll.
    /// </summary>
    public partial class CrittersTabletopHUD
    {
        private GameObject _preGameRoot;
        private Text _preGameTitle;
        private Text _preGameSubtitle;
        private Transform _preGameDiceRow;
        private GameObject _factionPickArea;
        private Transform _factionGridRoot;
        private Text _factionDetailText;
        private Button _factionPickButton;
        private Text _factionPickButtonLabel;
        private FactionId _inspectedFaction = FactionId.CurlyTail;

        private readonly Dictionary<int, float> _diceAnimEnd = new Dictionary<int, float>();
        private readonly Dictionary<int, Text[]> _diceFaces = new Dictionary<int, Text[]>();
        private const float DiceAnimSeconds = 0.9f;
        /// <summary>After a roll-off finishes, the results stay up this long so everyone can see them.</summary>
        private const float RollResultHoldSeconds = 3f;
        private float _rollHoldUntil;
        private bool _rollHoldWasDraft;
        private bool _prevPhaseWasRoll;
        private bool _prevPhaseWasDraft;

        private void BuildPreGamePanel(Transform canvasParent)
        {
            var root = CreatePanel(canvasParent, "PreGame", new Vector2(0.12f, 0.08f), new Vector2(0.88f, 0.92f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.07f, 0.09f, 0.13f, 0.97f));
            _preGameRoot = root.gameObject;

            _preGameTitle = CreateText(root, "Title", "", 30, TextAnchor.MiddleCenter,
                new Vector2(0.03f, 0.90f), new Vector2(0.97f, 0.98f), Vector2.zero, Vector2.zero, Palette.AuthorityGold);
            _preGameSubtitle = CreateText(root, "Subtitle", "", 17, TextAnchor.MiddleCenter,
                new Vector2(0.03f, 0.84f), new Vector2(0.97f, 0.90f), Vector2.zero, Vector2.zero, Palette.ParchmentMap);

            // Dice cards, one per player
            var diceGo = new GameObject("DiceRow");
            diceGo.transform.SetParent(root, false);
            var diceRt = diceGo.AddComponent<RectTransform>();
            diceRt.anchorMin = new Vector2(0.03f, 0.60f);
            diceRt.anchorMax = new Vector2(0.97f, 0.83f);
            diceRt.offsetMin = Vector2.zero;
            diceRt.offsetMax = Vector2.zero;
            var diceLayout = diceGo.AddComponent<HorizontalLayoutGroup>();
            diceLayout.spacing = 12f;
            diceLayout.childControlWidth = true;
            diceLayout.childControlHeight = true;
            diceLayout.childForceExpandWidth = true;
            diceLayout.childForceExpandHeight = true;
            _preGameDiceRow = diceGo.transform;

            // Faction cards (left) and the selected faction's details (right)
            var pick = CreatePanel(root, "FactionPick", new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.58f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.10f, 0.12f, 0.17f, 0.95f));
            _factionPickArea = pick.gameObject;

            var gridGo = new GameObject("FactionGrid");
            gridGo.transform.SetParent(pick, false);
            var gridRt = gridGo.AddComponent<RectTransform>();
            gridRt.anchorMin = new Vector2(0.01f, 0.02f);
            gridRt.anchorMax = new Vector2(0.47f, 0.98f);
            gridRt.offsetMin = Vector2.zero;
            gridRt.offsetMax = Vector2.zero;
            var grid = gridGo.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(330f, 92f);
            grid.spacing = new Vector2(8f, 8f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            _factionGridRoot = gridGo.transform;

            _factionDetailText = CreateText(pick, "Detail", "", 16, TextAnchor.UpperLeft,
                new Vector2(0.50f, 0.16f), new Vector2(0.99f, 0.98f), Vector2.zero, Vector2.zero, Color.white);
            // Long faction write-ups shrink to fit their box instead of spilling over the cards or the Pick button.
            _factionDetailText.resizeTextForBestFit = true;
            _factionDetailText.resizeTextMinSize = 10;
            _factionDetailText.resizeTextMaxSize = 16 + FontBump;
            _factionDetailText.verticalOverflow = VerticalWrapMode.Truncate;
            _factionDetailText.horizontalOverflow = HorizontalWrapMode.Wrap;
            gridGo.AddComponent<RectMask2D>();
            _factionPickButton = CreateButton(pick, "BtnPick", "Pick", new Vector2(0.62f, 0.02f), new Vector2(0.88f, 0.13f),
                Palette.FromHex("#B86B28"), () =>
                {
                    int picker = _engine.CurrentFactionPicker;
                    if (picker >= 0) _mpManager.Submit(GameCommand.PickFaction(_inspectedFaction), picker);
                });
            _factionPickButtonLabel = _factionPickButton.GetComponentInChildren<Text>();
            SetFontSize(_factionPickButtonLabel, 18);

            _preGameRoot.SetActive(false);
        }

        /// <summary>Can this screen act for that seat? (Online: only your own seat. One computer: anyone.)</summary>
        private bool ControlsSeat(int seat) => !_mpManager.IsOnline || seat == _mpManager.LocalSeat;

        private void RefreshPreGame()
        {
            if (_preGameRoot == null) return;
            if (_engine == null || _engine.Players.Count == 0) { _preGameRoot.SetActive(false); return; }

            // When a roll-off finishes, keep the dice on screen for a few seconds so everyone sees the result.
            bool rollPhase = _engine.CurrentPhase == TurnPhase.PreGame_DraftRoll || _engine.CurrentPhase == TurnPhase.PreGame_TurnOrderRoll;
            if (_prevPhaseWasRoll && !rollPhase)
            {
                float lastAnim = _diceAnimEnd.Count > 0 ? _diceAnimEnd.Values.Max() : Time.unscaledTime;
                _rollHoldUntil = Mathf.Max(Time.unscaledTime, lastAnim) + RollResultHoldSeconds;
                _rollHoldWasDraft = _prevPhaseWasDraft;
                StartCoroutine(EndRollHoldLater());
            }
            _prevPhaseWasRoll = rollPhase;
            _prevPhaseWasDraft = _engine.CurrentPhase == TurnPhase.PreGame_DraftRoll;
            bool holding = Time.unscaledTime < _rollHoldUntil;

            bool show = holding || (_engine.IsPreGame &&
                        _engine.CurrentPhase != TurnPhase.PreGame_ReverseFactionSetup); // setup uses the board, with a banner
            _preGameRoot.SetActive(show);
            if (!show) return;
            _preGameRoot.transform.SetAsLastSibling();

            bool picking = !holding && _engine.CurrentPhase == TurnPhase.PreGame_FactionPick;
            bool draftRoll = _engine.CurrentPhase == TurnPhase.PreGame_DraftRoll;

            if (holding)
            {
                var ranking = Enumerable.Range(0, _engine.Players.Count)
                    .OrderByDescending(i => i < _engine.PreGameLastDie1.Count ? _engine.PreGameLastDie1[i] : 0).ToList();
                _preGameTitle.text = _rollHoldWasDraft ? "FACTION PICK ORDER ROLLED" : "TURN ORDER ROLLED";
                _preGameSubtitle.text = _rollHoldWasDraft
                    ? $"Pick order: {string.Join("  >  ", _engine.FactionPickOrder.Select(i => _engine.Players[i].DisplayName))}"
                    : $"{_engine.Players[_engine.CurrentTurnPlayerIndex].DisplayName} rolled highest and goes first.";
            }
            else if (draftRoll)
            {
                _preGameTitle.text = "ROLL FOR FACTION PICK ORDER";
                _preGameSubtitle.text = "Everyone rolls the 3-sided die. The highest roll picks a faction first. Ties roll again.";
            }
            else if (picking)
            {
                int picker = _engine.CurrentFactionPicker;
                string order = string.Join("  >  ", _engine.FactionPickOrder.Select((s, i) =>
                    i < _engine.FactionPickCursor ? $"{_engine.Players[s].DisplayName} ({_engine.Players[s].Faction.Name})"
                    : i == _engine.FactionPickCursor ? $"<b>{_engine.Players[s].DisplayName}</b>" : _engine.Players[s].DisplayName));
                _preGameTitle.text = ControlsSeat(picker)
                    ? $"{_engine.Players[picker].DisplayName.ToUpperInvariant()}, PICK YOUR FACTION"
                    : $"{_engine.Players[picker].DisplayName.ToUpperInvariant()} IS PICKING A FACTION";
                _preGameSubtitle.text = $"Pick order: {order}";
            }
            else
            {
                _preGameTitle.text = "ROLL FOR TURN ORDER";
                _preGameSubtitle.text = "Everyone rolls the 3-sided die again. The highest roll takes the first turn, then play goes clockwise. Ties roll again.";
            }

            BuildDiceCards(showDice: !picking);
            _factionPickArea.SetActive(picking);
            if (picking) RefreshFactionPick();
        }

        private IEnumerator EndRollHoldLater()
        {
            while (Time.unscaledTime < _rollHoldUntil) yield return new WaitForSecondsRealtime(0.1f);
            RefreshPreGame();
            RefreshHUD();
        }

        private void BuildDiceCards(bool showDice)
        {
            foreach (Transform child in _preGameDiceRow) Destroy(child.gameObject);
            _diceFaces.Clear();

            for (int seat = 0; seat < _engine.Players.Count; seat++)
            {
                var player = _engine.Players[seat];
                var card = CreatePanel(_preGameDiceRow, $"DiceCard_{seat}", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                    Vector2.zero, Vector2.zero,
                    player.Faction != null ? player.Faction.PrimaryColor * 0.45f + new Color(0.08f, 0.08f, 0.08f, 0.95f)
                                           : new Color(0.16f, 0.19f, 0.25f, 0.95f));

                string you = _mpManager.IsOnline && seat == _mpManager.LocalSeat ? " (you)" : "";
                string faction = player.Faction != null ? $"\n<size=19><b>{player.Faction.Name}</b></size>" : "";
                CreateText(card, "Name", $"<b>{player.DisplayName}</b>{you}{faction}", 17, TextAnchor.UpperCenter,
                    new Vector2(0.02f, 0.64f), new Vector2(0.98f, 0.97f), Vector2.zero, Vector2.zero, Color.white);

                if (!showDice)
                {
                    bool picked = player.Faction != null;
                    bool isPicking = _engine.CurrentFactionPicker == seat;
                    CreateText(card, "Status", picked ? "Ready" : isPicking ? "Picking..." : "Waiting to pick", 15,
                        TextAnchor.MiddleCenter, new Vector2(0.02f, 0.10f), new Vector2(0.98f, 0.55f), Vector2.zero, Vector2.zero,
                        isPicking ? Palette.AuthorityGold : Palette.ParchmentMap);
                    continue;
                }

                var hist = seat < _engine.PreGameRollHistory.Count ? _engine.PreGameRollHistory[seat] : new List<int>();
                bool hasRolled = hist.Count > 0;
                bool animating = _diceAnimEnd.TryGetValue(seat, out float end) && end > Time.unscaledTime;
                int d1 = seat < _engine.PreGameLastDie1.Count ? _engine.PreGameLastDie1[seat] : 0;
                var face1 = MakeDie(card, "Die1", new Vector2(0.36f, 0.30f), new Vector2(0.64f, 0.62f), hasRolled ? d1.ToString() : "?");
                _diceFaces[seat] = new[] { face1 };

                bool mustRoll = _engine.CanRollPreGameDice(seat);
                bool tied = _engine.IsSeatTied(seat);
                string status = animating ? "Rolling..."
                    : mustRoll && tied ? $"Tied at {hist.Last()}! Roll again"
                    : mustRoll ? "Needs to roll"
                    : hasRolled ? $"Rolled: <b>{d1}</b>" + (hist.Count > 1 ? $"  (rolls: {string.Join(", ", hist)})" : "")
                    : "";

                if (mustRoll && !animating && ControlsSeat(seat))
                {
                    int captured = seat;
                    var rollBtn = CreateButton(card, "BtnRoll", tied ? "Roll Again" : "Roll Dice", new Vector2(0.15f, 0.04f), new Vector2(0.85f, 0.26f),
                        Palette.FromHex("#B86B28"), () => _mpManager.Submit(GameCommand.RollDice(), captured));
                    SetFontSize(rollBtn.GetComponentInChildren<Text>(), 16);
                }
                else
                {
                    CreateText(card, "Status", status, 15, TextAnchor.MiddleCenter, new Vector2(0.02f, 0.03f), new Vector2(0.98f, 0.27f),
                        Vector2.zero, Vector2.zero, mustRoll ? Palette.AuthorityGold : Palette.ParchmentMap);
                }
            }
        }

        private Text MakeDie(Transform parent, string name, Vector2 min, Vector2 max, string value)
        {
            var die = CreatePanel(parent, name, min, max, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.96f, 0.94f, 0.88f, 1f));
            return CreateText(die, "Face", value, 34, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.15f, 0.12f, 0.10f, 1f));
        }

        /// <summary>Called on every computer when someone rolls: tumble their dice, then show the real result.</summary>
        private void PlayDiceRollAnimation(int seat)
        {
            _diceAnimEnd[seat] = Time.unscaledTime + DiceAnimSeconds;
            StartCoroutine(AnimateDice(seat));
        }

        private IEnumerator AnimateDice(int seat)
        {
            RefreshPreGame();
            while (_diceAnimEnd.TryGetValue(seat, out float end) && Time.unscaledTime < end)
            {
                if (_diceFaces.TryGetValue(seat, out var faces))
                    foreach (var f in faces)
                        if (f != null) f.text = Random.Range(1, 4).ToString();
                yield return new WaitForSecondsRealtime(0.07f);
            }
            _diceAnimEnd.Remove(seat);
            RefreshPreGame();
            RefreshHUD();
        }

        private void RefreshFactionPick()
        {
            foreach (Transform child in _factionGridRoot) Destroy(child.gameObject);
            var all = RulebookDatabase.GetAllFactions();
            // Size the faction cards to fit their half of the panel (two columns), so they never run under the details.
            var gridLayout = _factionGridRoot.GetComponent<GridLayoutGroup>();
            var gridRect = ((RectTransform)_factionGridRoot).rect;
            int rows = Mathf.CeilToInt(all.Count / 2f);
            if (gridRect.width > 10f && gridRect.height > 10f)
                gridLayout.cellSize = new Vector2((gridRect.width - gridLayout.spacing.x) / 2f - 1f,
                                                  Mathf.Min(92f, (gridRect.height - gridLayout.spacing.y * (rows - 1)) / rows - 1f));
            int picker = _engine.CurrentFactionPicker;

            // Default the details view to the first faction that's still available.
            var inspected = all.FirstOrDefault(f => f.Id == _inspectedFaction);
            if (inspected == null || TakenBy(inspected.Id) != null)
            {
                var firstFree = all.FirstOrDefault(f => TakenBy(f.Id) == null);
                if (firstFree != null) _inspectedFaction = firstFree.Id;
                inspected = all.First(f => f.Id == _inspectedFaction);
            }

            foreach (var f in all)
            {
                var captured = f.Id;
                var owner = TakenBy(f.Id);
                string label = owner != null
                    ? $"<b>{f.Name}</b>\n<size=20><b>Taken by {owner.DisplayName}</b></size>"
                    : $"<b>{f.Name}</b>\n<size=14>Difficulty {f.DifficultyRatingOutOf10}/10</size>";
                Color bg = owner != null ? new Color(0.22f, 0.22f, 0.22f, 0.8f)
                    : f.Id == _inspectedFaction ? f.PrimaryColor * 0.85f + new Color(0.1f, 0.1f, 0.1f, 1f)
                    : f.PrimaryColor * 0.55f + new Color(0.05f, 0.05f, 0.05f, 1f);
                var btn = CreateLayoutButton(_factionGridRoot, $"Fac_{f.Id}", label, bg, () =>
                {
                    _inspectedFaction = captured;
                    RefreshFactionPick();
                });
                SetFontSize(btn.GetComponentInChildren<Text>(), 16);
            }

            var sb = new StringBuilder();
            sb.AppendLine($"<size=24><b>{inspected.Name}</b></size>   <i>Difficulty {inspected.DifficultyRatingOutOf10}/10</i>");
            sb.AppendLine();
            sb.AppendLine(inspected.ShortSummary);
            sb.AppendLine();
            sb.AppendLine($"<i>{inspected.LoreDescription}</i>");
            sb.AppendLine();
            if (inspected.SpeciesTraits.Count > 0)
            {
                sb.AppendLine("<b>Species traits:</b>");
                foreach (var t in inspected.SpeciesTraits.Take(5)) sb.AppendLine($" • {t}");
            }
            sb.AppendLine($"\n<b>Units:</b> {string.Join(", ", inspected.Units.Select(u => $"{u.Name} ({u.BaseAttack}/{u.BaseDefense}/{u.BaseMaxHP})"))}");
            _factionDetailText.text = sb.ToString();

            bool available = TakenBy(inspected.Id) == null;
            bool myPick = picker >= 0 && ControlsSeat(picker);
            _factionPickButton.gameObject.SetActive(myPick);
            _factionPickButton.interactable = available;
            _factionPickButtonLabel.text = available ? $"Pick {inspected.Name}" : "Already taken";
        }

        private PlayerState TakenBy(FactionId id) => _engine.Players.FirstOrDefault(p => p.Faction != null && p.Faction.Id == id);
    }
}
