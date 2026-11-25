using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using CrittersOfCalamity.Engine;
using CrittersOfCalamity.Model;
using CrittersOfCalamity.Multiplayer;
using CrittersOfCalamity.Visuals;

namespace CrittersOfCalamity.UI
{
    /// <summary>
    /// Information screens: the rulebook (split into its Parts and one entry per faction), tabs for the factions
    /// in this game, a hover tooltip for any piece, a click-for-details popup, the supply list, and the setup banner.
    /// Everything here works for every player, whoever owns the piece.
    /// </summary>
    public partial class CrittersTabletopHUD
    {
        private class RulebookSection
        {
            public string Title;
            public string Body;
        }

        private GameObject _infoModal;
        private Transform _infoTabsRoot;
        private Transform _infoListRoot;
        private Text _infoBodyText;
        private ScrollRect _infoBodyScroll;
        private bool _infoShowingRulebook = true;
        private int _infoSelectedSection;
        private int _infoSelectedPlayer;
        private List<RulebookSection> _rulebook;

        private GameObject _tooltipRoot;
        private Text _tooltipText;
        private PieceRef _lastHovered;

        private GameObject _pieceModal;
        private Text _pieceModalText;
        private bool _clickUsedByAction;

        private GameObject _setupBanner;
        private Text _setupBannerText;
        private bool _setupHighlightsShown;

        // ------------------------------------------------------------------
        // BUILD
        // ------------------------------------------------------------------

        private void BuildInfoScreens(Transform canvasParent)
        {
            // Rulebook & Factions window
            var modal = CreatePanel(canvasParent, "InfoModal", new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.92f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.07f, 0.09f, 0.12f, 0.98f));
            _infoModal = modal.gameObject;

            CreateButton(modal, "TabRulebook", "Rulebook", new Vector2(0.01f, 0.935f), new Vector2(0.13f, 0.99f),
                Palette.FromHex("#8C5328"), () => { _infoShowingRulebook = true; RefreshInfoModal(); });
            CreateButton(modal, "TabFactions", "Factions in this game", new Vector2(0.14f, 0.935f), new Vector2(0.30f, 0.99f),
                Palette.FromHex("#2E7D5B"), () => { _infoShowingRulebook = false; RefreshInfoModal(); });
            CreateButton(modal, "BtnLeave", "Leave Game", new Vector2(0.74f, 0.935f), new Vector2(0.86f, 0.99f),
                Palette.FromHex("#5A2A2A"), async () => { _infoModal.SetActive(false); await _mpManager.LeaveAsync(); });
            CreateButton(modal, "BtnClose", "Close", new Vector2(0.87f, 0.935f), new Vector2(0.99f, 0.99f),
                Palette.FromHex("#3E4A59"), () => _infoModal.SetActive(false));

            // Player tabs for the factions view
            var tabsGo = new GameObject("PlayerTabs");
            tabsGo.transform.SetParent(modal, false);
            var tabsRt = tabsGo.AddComponent<RectTransform>();
            tabsRt.anchorMin = new Vector2(0.31f, 0.935f);
            tabsRt.anchorMax = new Vector2(0.73f, 0.99f);
            tabsRt.offsetMin = Vector2.zero;
            tabsRt.offsetMax = Vector2.zero;
            var tabsLayout = tabsGo.AddComponent<HorizontalLayoutGroup>();
            tabsLayout.spacing = 6f;
            tabsLayout.childControlWidth = true;
            tabsLayout.childControlHeight = true;
            tabsLayout.childForceExpandWidth = true;
            tabsLayout.childForceExpandHeight = true;
            _infoTabsRoot = tabsGo.transform;

            _infoListRoot = BuildScrollList(modal, new Vector2(0.01f, 0.01f), new Vector2(0.24f, 0.925f));

            // Body: scrollable text
            var viewGo = new GameObject("BodyView");
            viewGo.transform.SetParent(modal, false);
            var viewRt = viewGo.AddComponent<RectTransform>();
            viewRt.anchorMin = new Vector2(0.25f, 0.01f);
            viewRt.anchorMax = new Vector2(0.99f, 0.925f);
            viewRt.offsetMin = Vector2.zero;
            viewRt.offsetMax = Vector2.zero;
            viewGo.AddComponent<Image>().color = new Color(0.10f, 0.12f, 0.16f, 1f);
            viewGo.AddComponent<RectMask2D>();
            _infoBodyScroll = viewGo.AddComponent<ScrollRect>();
            _infoBodyScroll.horizontal = false;
            _infoBodyScroll.scrollSensitivity = 30f;
            _infoBodyScroll.movementType = ScrollRect.MovementType.Clamped;
            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(viewGo.transform, false);
            var bodyRt = bodyGo.AddComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0f, 1f);
            bodyRt.anchorMax = new Vector2(1f, 1f);
            bodyRt.pivot = new Vector2(0.5f, 1f);
            bodyRt.offsetMin = new Vector2(16f, 0f);
            bodyRt.offsetMax = new Vector2(-16f, 0f);
            _infoBodyText = bodyGo.AddComponent<Text>();
            _infoBodyText.font = _uiFont;
            _infoBodyText.fontSize = 16;
            _infoBodyText.color = new Color(0.92f, 0.90f, 0.85f);
            _infoBodyText.supportRichText = true;
            _infoBodyText.alignment = TextAnchor.UpperLeft;
            _infoBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _infoBodyText.verticalOverflow = VerticalWrapMode.Overflow;
            bodyGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _infoBodyScroll.content = bodyRt;
            _infoBodyScroll.viewport = viewRt;
            _infoModal.SetActive(false);

            // Hover tooltip (bottom-left corner of the board, out of the way)
            var tip = CreatePanel(canvasParent, "PieceTooltip", new Vector2(0.18f, 0.195f), new Vector2(0.575f, 0.30f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.05f, 0.06f, 0.08f, 0.88f));
            _tooltipRoot = tip.gameObject;
            _tooltipText = CreateText(tip, "Text", "", 14, TextAnchor.UpperLeft, Vector2.zero, Vector2.one,
                new Vector2(10f, 6f), new Vector2(-10f, -6f), Color.white);
            _tooltipText.raycastTarget = false;
            tip.GetComponent<Image>().raycastTarget = false;
            _tooltipRoot.SetActive(false);

            // Click-for-details popup
            var pm = CreatePanel(canvasParent, "PieceDetails", new Vector2(0.30f, 0.18f), new Vector2(0.70f, 0.86f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.08f, 0.10f, 0.14f, 0.98f));
            _pieceModal = pm.gameObject;
            _pieceModalText = CreateText(pm, "Text", "", 15, TextAnchor.UpperLeft, new Vector2(0.04f, 0.10f), new Vector2(0.96f, 0.97f),
                Vector2.zero, Vector2.zero, Color.white);
            CreateButton(pm, "BtnClose", "Close", new Vector2(0.35f, 0.02f), new Vector2(0.65f, 0.08f),
                Palette.FromHex("#3E4A59"), () => _pieceModal.SetActive(false));
            _pieceModal.SetActive(false);

            // Setup banner (top of the board, so the board stays clickable)
            var banner = CreatePanel(canvasParent, "SetupBanner", new Vector2(0.20f, 0.78f), new Vector2(0.80f, 0.915f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.08f, 0.10f, 0.14f, 0.95f));
            _setupBanner = banner.gameObject;
            _setupBannerText = CreateText(banner, "Text", "", 16, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one,
                new Vector2(12f, 4f), new Vector2(-12f, -4f), Color.white);
            _setupBanner.SetActive(false);
        }

        private void ToggleInfoModal()
        {
            bool show = !_infoModal.activeSelf;
            _infoModal.SetActive(show);
            if (show)
            {
                _infoModal.transform.SetAsLastSibling();
                RefreshInfoModal();
            }
        }

        // ------------------------------------------------------------------
        // RULEBOOK & FACTIONS WINDOW
        // ------------------------------------------------------------------

        private void RefreshInfoModal()
        {
            if (_infoModal == null || !_infoModal.activeSelf) return;
            foreach (Transform c in _infoListRoot) Destroy(c.gameObject);
            foreach (Transform c in _infoTabsRoot) Destroy(c.gameObject);

            if (_infoShowingRulebook)
            {
                var sections = LoadRulebook();
                _infoSelectedSection = Mathf.Clamp(_infoSelectedSection, 0, Mathf.Max(0, sections.Count - 1));
                for (int i = 0; i < sections.Count; i++)
                {
                    int idx = i;
                    bool faction = sections[i].Title.StartsWith("Faction");
                    var btn = CreateLayoutButton(_infoListRoot, $"Sec_{i}", (faction ? "    " : "") + sections[i].Title,
                        i == _infoSelectedSection ? Palette.FromHex("#8C5328") : new Color(0.16f, 0.19f, 0.25f, 1f),
                        () => { _infoSelectedSection = idx; RefreshInfoModal(); _infoBodyScroll.verticalNormalizedPosition = 1f; });
                    btn.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;
                    btn.GetComponentInChildren<Text>().fontSize = 14;
                }
                _infoBodyText.text = sections.Count > 0 ? FormatRulebookSection(sections[_infoSelectedSection]) : "The rulebook couldn't be loaded.";
                return;
            }

            // Factions in this game: one tab per player
            if (_engine == null || _engine.Players.Count == 0)
            {
                _infoBodyText.text = "Start a game to see the factions being played. The full faction rules are in the Rulebook tab (Part 17).";
                return;
            }
            _infoSelectedPlayer = Mathf.Clamp(_infoSelectedPlayer, 0, _engine.Players.Count - 1);
            for (int i = 0; i < _engine.Players.Count; i++)
            {
                int idx = i;
                var pl = _engine.Players[i];
                string label = pl.Faction != null ? $"{pl.DisplayName}: {pl.Faction.Name}" : pl.DisplayName;
                Color col = pl.Faction != null ? pl.Faction.PrimaryColor * 0.7f + new Color(0.05f, 0.05f, 0.05f, 1f) : new Color(0.2f, 0.2f, 0.25f, 1f);
                if (i == _infoSelectedPlayer) col += new Color(0.15f, 0.15f, 0.15f, 0f);
                var tab = CreateLayoutButton(_infoTabsRoot, $"PTab_{i}", label, col, () => { _infoSelectedPlayer = idx; RefreshInfoModal(); });
                tab.GetComponentInChildren<Text>().fontSize = 13;
                tab.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleCenter;
            }
            var p = _engine.Players[_infoSelectedPlayer];
            _infoBodyText.text = p.Faction == null ? $"{p.DisplayName} hasn't picked a faction yet." : FactionOverview(p);

            // Left list: quick jump to each faction's full rules in the rulebook
            foreach (var pl in _engine.Players.Where(x => x.Faction != null))
            {
                var fac = pl.Faction;
                var btn = CreateLayoutButton(_infoListRoot, $"Rules_{fac.Id}", $"Full rules: {fac.Name}", new Color(0.16f, 0.19f, 0.25f, 1f), () =>
                {
                    var sections = LoadRulebook();
                    int idx = sections.FindIndex(sx => sx.Title.Contains(fac.Name.Replace("The ", "").Split(' ')[0]));
                    _infoShowingRulebook = true;
                    if (idx >= 0) _infoSelectedSection = idx;
                    RefreshInfoModal();
                });
                btn.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;
                btn.GetComponentInChildren<Text>().fontSize = 13;
            }
        }

        private string FactionOverview(PlayerState p)
        {
            var f = p.Faction;
            var sb = new StringBuilder();
            sb.AppendLine($"<size=26><b>{f.Name}</b></size>   played by <b>{p.DisplayName}</b>   (difficulty {f.DifficultyRatingOutOf10}/10)");
            sb.AppendLine($"<b>Authority:</b> {p.Authority} of 30");
            sb.AppendLine();
            sb.AppendLine("<b><color=#F2B83B>STRATEGY</color></b>");
            sb.AppendLine(f.ShortSummary);
            sb.AppendLine($"<i>{f.LoreDescription}</i>");
            sb.AppendLine();
            sb.AppendLine("<b><color=#F2B83B>HOW THEY SCORE AUTHORITY</color></b>");
            sb.AppendLine(f.HowToScoreAuthority);
            sb.AppendLine();
            sb.AppendLine("<b><color=#F2B83B>UNITS</color></b>  (Attack / Defense / HP)");
            foreach (var u in f.Units)
            {
                sb.AppendLine($"<b>{u.Name}</b> {(u.IsSpecialUnit ? "(special unit)" : "(basic unit)")}  {u.BaseAttack}/{u.BaseDefense}/{u.BaseMaxHP}");
                if (!string.IsNullOrEmpty(u.SummoningCondition)) sb.AppendLine($"   How it enters play: {u.SummoningCondition}");
                if (!string.IsNullOrEmpty(u.AbilitiesText)) sb.AppendLine($"   {u.AbilitiesText}");
            }
            sb.AppendLine();
            sb.AppendLine("<b><color=#F2B83B>SPECIES TRAITS</color></b>");
            foreach (var t in f.SpeciesTraits) sb.AppendLine($" • {t}");
            sb.AppendLine();
            sb.AppendLine($"<b><color=#F2B83B>BUILDING</color></b>\n{f.BuildingRules}");
            if (f.Equipment.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("<b><color=#F2B83B>EQUIPMENT</color></b>");
                foreach (var e in f.Equipment) sb.AppendLine($" • <b>{e.Name}</b> (cost {e.Cost}): {e.EffectText}");
            }
            sb.AppendLine();
            sb.AppendLine("<b><color=#F2B83B>PIECES</color></b>");
            sb.AppendLine(string.Join(" · ", f.MaterialsManifest));
            sb.AppendLine();
            sb.AppendLine("<b><color=#F2B83B>HOW OPPONENTS SCORE AUTHORITY FROM THEM</color></b>");
            foreach (var w in f.EnemyAuthorityScoring) sb.AppendLine($" • {w}");
            sb.AppendLine();
            sb.AppendLine("<b><color=#F2B83B>THEIR SUPPLY RIGHT NOW</color></b>");
            sb.Append(SupplySummary(p));
            return sb.ToString();
        }

        /// <summary>Splits the bundled rulebook into its Parts, with one entry per faction (Part 17).</summary>
        private List<RulebookSection> LoadRulebook()
        {
            if (_rulebook != null) return _rulebook;
            _rulebook = new List<RulebookSection>();
            var asset = Resources.Load<TextAsset>("Rulebook/CoreRulebook");
            if (asset == null) return _rulebook;

            var current = new RulebookSection { Title = "Introduction" };
            var body = new StringBuilder();
            bool skipping = false;
            var header = new Regex(@"^(Part \d+:.*|Faction \d+:.*|Appendix [A-Z]:.*)$");
            foreach (var raw in asset.text.Replace("\r", "").Split('\n'))
            {
                string line = raw.TrimEnd();
                if (header.IsMatch(line))
                {
                    if (!skipping) { current.Body = body.ToString(); _rulebook.Add(current); }
                    body.Clear();
                    skipping = line.StartsWith("Appendix"); // content kept for a later edition isn't shown in the game
                    string title = line;
                    if (title.StartsWith("Faction") && title.Contains(":")) title = title.Split(':')[0] + ": " + title.Substring(title.IndexOf(':') + 1).Trim().Split(':')[0];
                    current = new RulebookSection { Title = title.Length > 60 ? title.Substring(0, 60) : title };
                    continue;
                }
                if (skipping) continue;
                // Drop the note that points at the appendix.
                if (line.Contains("(see Appendix A)")) line = Regex.Replace(line, @"\s*Three factions not included.*?\(see Appendix A\)\.", "");
                body.AppendLine(line);
            }
            if (!skipping) { current.Body = body.ToString(); _rulebook.Add(current); }

            // Keep factions in faction order (the file lists Faction 5 after Faction 4's appendix note).
            var parts = _rulebook.Where(s => !s.Title.StartsWith("Faction")).ToList();
            var factions = _rulebook.Where(s => s.Title.StartsWith("Faction")).OrderBy(s => FactionNumber(s.Title)).ToList();
            int part17 = parts.FindIndex(s => s.Title.StartsWith("Part 17"));
            if (part17 >= 0) parts.InsertRange(part17 + 1, factions); else parts.AddRange(factions);
            _rulebook = parts;
            return _rulebook;
        }

        private static int FactionNumber(string title)
        {
            var m = Regex.Match(title, @"Faction (\d+)");
            return m.Success ? int.Parse(m.Groups[1].Value) : 99;
        }

        private static string FormatRulebookSection(RulebookSection s)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"<size=24><b>{s.Title}</b></size>");
            sb.AppendLine();
            foreach (var raw in s.Body.Split('\n'))
            {
                string line = raw.TrimEnd();
                if (line.Length == 0) { sb.AppendLine(); continue; }
                // Short lines without a full stop are sub-headings in the rulebook.
                bool heading = line.Length < 48 && !line.EndsWith(".") && !line.StartsWith("•") && !char.IsDigit(line[0]);
                sb.AppendLine(heading ? $"\n<b><color=#F2B83B>{line}</color></b>" : line);
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------
        // HOVER TOOLTIP AND CLICK-FOR-DETAILS
        // ------------------------------------------------------------------

        void Update()
        {
            if (_boardRenderer == null || _tooltipRoot == null) return;
            var hovered = _boardRenderer.HoveredPiece;
            bool inGame = _engine != null && _engine.Players.Count > 0;
            if (!inGame || hovered == null)
            {
                if (_tooltipRoot.activeSelf) _tooltipRoot.SetActive(false);
                _lastHovered = null;
                return;
            }
            if (hovered != _lastHovered || Time.frameCount % 15 == 0)
            {
                _tooltipText.text = DescribePiece(hovered, full: false);
                _lastHovered = hovered;
            }
            if (!_tooltipRoot.activeSelf)
            {
                _tooltipRoot.SetActive(true);
                _tooltipRoot.transform.SetAsLastSibling();
            }
        }

        private void HandlePieceClicked(PieceRef piece)
        {
            bool usedByAction = _clickUsedByAction;
            _clickUsedByAction = false;
            if (usedByAction || piece == null || _engine == null || _engine.Players.Count == 0) return;
            _pieceModalText.text = DescribePiece(piece, full: true);
            _pieceModal.SetActive(true);
            _pieceModal.transform.SetAsLastSibling();
        }

        private string OwnerLabel(int ownerIndex)
        {
            if (ownerIndex < 0 || ownerIndex >= _engine.Players.Count) return "";
            var o = _engine.Players[ownerIndex];
            return o.Faction != null ? $"{o.DisplayName}, {o.Faction.Name}" : o.DisplayName;
        }

        private string DescribePiece(PieceRef piece, bool full)
        {
            var sb = new StringBuilder();
            switch (piece.Kind)
            {
                case PieceKind.Unit:
                {
                    var u = piece.Unit;
                    if (u == null) break;
                    var def = u.Definition;
                    int atk = _engine.DisplayAttack(u), dfn = _engine.DisplayDefense(u);
                    sb.AppendLine($"<b>{def.Name}</b> ({OwnerLabel(u.OwnerPlayerIndex)})");
                    sb.AppendLine($"Attack <b>{atk}</b>   Defense <b>{dfn}</b>   HP <b>{u.CurrentHP}/{u.MaxHP}</b>" +
                                  (atk != def.BaseAttack || dfn != def.BaseDefense || u.MaxHP != def.BaseMaxHP ? $"   <size=12>(base {def.BaseAttack}/{def.BaseDefense}/{def.BaseMaxHP})</size>" : ""));
                    var tags = new List<string>();
                    if (u.IsGuest) tags.Add("Guest");
                    if (u.TrapLeafTurnsRemaining > 0 || u.CannotMoveThisTurn) tags.Add("can't move this turn");
                    if (u.EquippedItems.Count > 0) tags.Add("equipped: " + string.Join(", ", u.EquippedItems.Select(e => e.Name)));
                    if (tags.Count > 0) sb.AppendLine(string.Join(" · ", tags));
                    string meters = UnitMeterSummary(u);
                    if (!string.IsNullOrEmpty(meters)) sb.AppendLine(meters);

                    if (full)
                    {
                        sb.AppendLine();
                        sb.AppendLine($"<b>Type:</b> {(def.IsSpecialUnit ? "Special unit" : "Basic unit")}   <b>Tile:</b> {piece.Coord}");
                        if (!string.IsNullOrEmpty(def.SummoningCondition)) sb.AppendLine($"<b>How it enters play:</b> {def.SummoningCondition}");
                        if (!string.IsNullOrEmpty(def.AbilitiesText)) sb.AppendLine($"\n<b>Abilities and traits:</b>\n{def.AbilitiesText}");
                        foreach (var e in u.EquippedItems) sb.AppendLine($"\n<b>{e.Name}:</b> {e.EffectText}");
                        var owner = _engine.Players[u.OwnerPlayerIndex];
                        if (def.IsSpecialUnit && owner.Faction != null)
                        {
                            var enemy = owner.Faction.EnemyAuthorityScoring.FirstOrDefault(w => w.Contains(def.Name));
                            if (enemy != null) sb.AppendLine($"\n<b>If removed by an enemy:</b> {enemy}");
                        }
                    }
                    break;
                }
                case PieceKind.Building:
                {
                    var b = piece.Building;
                    if (b == null) break;
                    var owner = _engine.Players[b.OwnerPlayerIndex];
                    sb.AppendLine($"<b>{SplitWords(b.Type.ToString())}</b> (building, {OwnerLabel(b.OwnerPlayerIndex)})");
                    bool isolated = !_engine.Board[piece.Coord].Units.Any(x => x.OwnerPlayerIndex == b.OwnerPlayerIndex);
                    sb.AppendLine(isolated ? "Isolated: an enemy on this tile can remove it with a Battle action (1 Authority)." : "Protected: its owner has units here.");
                    if (full && owner.Faction != null) sb.AppendLine($"\n{owner.Faction.BuildingRules}");
                    break;
                }
                case PieceKind.Token:
                {
                    var t = piece.Token;
                    if (t == null) break;
                    sb.AppendLine($"<b>{SplitWords(t.Type.ToString())} Token</b> ({OwnerLabel(t.OwnerPlayerIndex)})");
                    if (t.Type == TokenType.Catapult) sb.AppendLine($"Uses left: {t.RemainingUses}");
                    if (t.Type == TokenType.Coin) sb.AppendLine("Coin Tokens can't be removed; Tanukis carry them to a Shrine for 2 Authority.");
                    if (full) sb.AppendLine($"\n{TokenRulesText(t.Type)}");
                    break;
                }
                case PieceKind.FactionTile:
                {
                    var tile = _engine.Board[piece.Coord];
                    sb.AppendLine($"<b>{OverlayName(tile)}</b> (faction tile, {OwnerLabel(tile.OverlayOwnerPlayerIndex)})");
                    if (tile.Overlay == FactionOverlayTileType.FallenLeaf_Unflipped)
                    {
                        bool mine = ControlsSeat(tile.OverlayOwnerPlayerIndex) && _mpManager.IsOnline || (!_mpManager.IsOnline && _engine.CurrentTurnPlayerIndex == tile.OverlayOwnerPlayerIndex);
                        sb.AppendLine(mine ? $"Facedown. Only you know: it's a <b>{(tile.LeafIsTrap ? "Trap Leaf" : "Prank Leaf")}</b>."
                                           : "Facedown: a Prank Leaf or a Trap Leaf. It flips when an enemy group ends a move here.");
                    }
                    if (full) sb.AppendLine($"\nTile: {piece.Coord}, {tile.EffectiveTerrain}");
                    break;
                }
            }
            if (!full) sb.Append("<size=11><color=#8899AA>Click for full details</color></size>");
            return sb.ToString();
        }

        /// <summary>Short meter readout for special units that have meters (shown on hover).</summary>
        private string UnitMeterSummary(UnitInstance u)
        {
            var p = _engine.Players[u.OwnerPlayerIndex];
            switch (u.Definition.Id)
            {
                case "curly_tail":
                    return $"<color=#F2B83B>Thirst for Glory Lv {p.CurlyTailThirstForGlory} · Hunger Lv {p.CurlyTailHunger} · Momentum Lv {p.CurlyTailMomentum}</color>";
                case "platy_pope":
                    return $"<color=#F2B83B>Platy-Pope Lv {p.PlatyPopeLevel} · Faith {p.PlatypusFaith}/5" + (p.CrusadeTurnsRemaining > 0 ? $" · Crusade: {p.CrusadeTurnsRemaining} turns left" : "") + "</color>";
                case "blooming_briar":
                    return $"<color=#F2B83B>Thorns {p.BriaredFlowerThorns}/6" + (p.BriaredFlowerThorns == 0 ? " · FRENZIED BLOOM" : "") + "</color>";
                case "necromouser":
                    return $"<color=#F2B83B>Cauldron: {p.NecromouserCauldronDeadCount} Dead · Special summons used {p.NecromouserSpecialSummonsUsed}/4</color>";
            }
            return null;
        }

        private static string TokenRulesText(TokenType type)
        {
            switch (type)
            {
                case TokenType.Gluttony: return "While a Millions unit is here, enemy units can't use a Move action to leave this tile.";
                case TokenType.Catapult: return "Its owner can launch up to 3 Millions (or the King) up to 3 tiles in a straight line, twice in total.";
                case TokenType.HolyRelic: return "During the Papacy's End phase, each Holy Relic Token gives 1 Faith.";
                case TokenType.SupplyLine: return "Linked War Parties and Supply Line Tokens let Curly Tail's units travel between them in one Move.";
                case TokenType.Coin: return "Tanukis on this tile hold the coin and carry it when they move.";
                case TokenType.TarPit: return "Fire Tiles here deal 1 extra HP. Enemies need two Main phase actions to leave this tile.";
            }
            return "";
        }

        private static string OverlayName(HexTileState t)
        {
            switch (t.Overlay)
            {
                case FactionOverlayTileType.FallenLeaf_Unflipped: return "Fallen Leaves";
                case FactionOverlayTileType.FallenLeaf_Trap: return "Fallen Leaves (Trap Leaf, flipped)";
                case FactionOverlayTileType.FallenLeaf_Prank: return "Fallen Leaves (Prank Leaf, flipped)";
                case FactionOverlayTileType.Miasma_Hidden: return "Miasma Tile (facedown)";
                case FactionOverlayTileType.Miasma_Revealed: return "Miasma Tile";
                case FactionOverlayTileType.Fire: return "Fire Tile";
                case FactionOverlayTileType.CapybaraStream: return "Capybara Stream Tile";
            }
            return t.Overlay.ToString();
        }

        private static string SplitWords(string s) => Regex.Replace(s, "(?<=[a-z])(?=[A-Z])", " ");

        // ------------------------------------------------------------------
        // SUPPLY
        // ------------------------------------------------------------------

        /// <summary>What a player still has in their supply (pieces not on the board).</summary>
        private string SupplySummary(PlayerState p)
        {
            if (p.Faction == null) return "";
            var sb = new StringBuilder();
            var units = _engine.Board.Values.SelectMany(t => t.Units).Where(u => u.OwnerPlayerIndex == p.PlayerIndex).ToList();
            foreach (var def in p.Faction.Units)
            {
                int onBoard = units.Count(u => u.Definition.Id == def.Id);
                sb.AppendLine($"{def.Name}: {Mathf.Max(0, def.SupplyCount - onBoard)} of {def.SupplyCount} in supply");
            }
            int bCap = RulebookDatabase.BuildingSupply(p.Faction.PrimaryBuilding);
            int bOn = _engine.Board.Values.Count(t => t.Building != null && t.Building.OwnerPlayerIndex == p.PlayerIndex);
            if (bCap > 0) sb.AppendLine($"{SplitWords(p.Faction.PrimaryBuilding.ToString())}: {Mathf.Max(0, bCap - bOn)} of {bCap} in supply");
            foreach (var kv in RulebookDatabase.TokenSupply(p.Faction.Id))
            {
                int on = _engine.Board.Values.Sum(t => t.Tokens.Count(x => x.Type == kv.Key && x.OwnerPlayerIndex == p.PlayerIndex));
                sb.AppendLine($"{SplitWords(kv.Key.ToString())} Tokens: {Mathf.Max(0, kv.Value - on)} of {kv.Value} in supply");
            }
            foreach (var kv in RulebookDatabase.FactionTileSupply(p.Faction.Id))
            {
                if (kv.Key == "Fallen Leaves")
                {
                    int left = p.FallenLeavesStack.Count;
                    bool owner = _mpManager.IsOnline ? p.PlayerIndex == _mpManager.LocalSeat : p.PlayerIndex == _engine.CurrentTurnPlayerIndex;
                    sb.AppendLine(owner
                        ? $"Fallen Leaves tiles: {left} of {kv.Value} in supply ({p.FallenLeavesStack.Count(x => !x)} Prank, {p.FallenLeavesStack.Count(x => x)} Trap)"
                        : $"Fallen Leaves tiles: {left} of {kv.Value} in supply");
                    continue;
                }
                FactionOverlayTileType[] kinds = kv.Key == "Miasma" ? new[] { FactionOverlayTileType.Miasma_Hidden, FactionOverlayTileType.Miasma_Revealed }
                    : kv.Key == "Fire" ? new[] { FactionOverlayTileType.Fire } : new[] { FactionOverlayTileType.CapybaraStream };
                int onBoard = _engine.Board.Values.Count(t => kinds.Contains(t.Overlay) && t.OverlayOwnerPlayerIndex == p.PlayerIndex);
                sb.AppendLine($"{kv.Key} tiles: {Mathf.Max(0, kv.Value - onBoard)} of {kv.Value} in supply");
            }
            if (p.Faction.Id == FactionId.Necromouser)
                sb.AppendLine($"Cauldron: {p.NecromouserCauldronDeadCount} Dead");
            return sb.ToString();
        }

        // ------------------------------------------------------------------
        // SETUP: placing starting pieces
        // ------------------------------------------------------------------

        private void RefreshSetupBanner()
        {
            if (_setupBanner == null) return;
            bool setup = _engine != null && _engine.Players.Count > 0 && _engine.CurrentPhase == TurnPhase.PreGame_ReverseFactionSetup;
            _setupBanner.SetActive(setup);
            if (!setup)
            {
                if (_setupHighlightsShown) { _boardRenderer.SetHighlights(null, Color.clear, null, Color.clear); _setupHighlightsShown = false; }
                return;
            }

            int seat = _engine.CurrentSetupPlayer;
            if (seat < 0) return;
            var p = _engine.Players[seat];
            int need = _engine.CurrentSetupTilesNeeded;
            var kind = _engine.CurrentSetupKind;
            string what = kind == SetupStepKind.CapybaraStreams ? "Capybara streams (special setup)"
                        : kind == SetupStepKind.TanukiCoins ? "Tanuki Coin Tokens" : $"{p.Faction.Name} starting pieces";
            string pickNote = kind == SetupStepKind.Faction
                ? "Green tiles are legal; click a picked (gold) tile again to un-pick it."
                : "Green tiles are legal; each click places one right away.";
            string order = string.Join("  >  ", _engine.SetupOrder.Select((s, i) =>
                i < _engine.SetupCursor ? $"{_engine.Players[s].DisplayName} (done)" : i == _engine.SetupCursor ? $"<b>{_engine.Players[s].DisplayName}</b>" : _engine.Players[s].DisplayName));

            if (ControlsSeat(seat))
            {
                _setupBannerText.text =
                    $"<size=20><b>{p.DisplayName}, place your {what}</b></size>\n" +
                    $"{_engine.CurrentSetupInstructions}\n" +
                    $"Placed: <b>{_engine.SetupPicks.Count} of {need}</b>. {pickNote}\n" +
                    $"<size=12>Setup order: {order}</size>";
                _boardRenderer.SetHighlights(_engine.LegalSetupTiles(), new Color(0.30f, 0.85f, 0.35f, 1f), _engine.SetupPicks, Palette.AuthorityGold);
            }
            else
            {
                _setupBannerText.text =
                    $"<size=20><b>{p.DisplayName} is placing their {what}</b></size>\n" +
                    $"{_engine.CurrentSetupInstructions}\n<size=12>Setup order (reverse turn order): {order}</size>";
                _boardRenderer.SetHighlights(null, Color.clear, _engine.SetupPicks, Palette.AuthorityGold);
            }
            _setupHighlightsShown = true;
            _setupBanner.transform.SetAsLastSibling();
        }

        /// <summary>Board clicks during setup pick tiles for the player who's setting up.</summary>
        private bool HandleSetupTileClick(HexCoord coord)
        {
            if (_engine == null || _engine.CurrentPhase != TurnPhase.PreGame_ReverseFactionSetup) return false;
            int seat = _engine.CurrentSetupPlayer;
            if (seat < 0 || !ControlsSeat(seat)) return true; // not your setup: ignore the click
            _mpManager.Submit(GameCommand.SetupPlace(coord), seat);
            return true;
        }
    }
}
