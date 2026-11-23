using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using CrittersOfCalamity.Model;
using CrittersOfCalamity.Engine;
using CrittersOfCalamity.Visuals;
using CrittersOfCalamity.Multiplayer;

namespace CrittersOfCalamity.UI
{
    /// <summary>
    /// Full uGUI Tabletop Interface (no OnGUI) for Eben's Critters of Calamity:
    /// - Top Bar: 4-Player Authority Scoreboard (race to 30 Authority), Turn/Phase Banner, Multiplayer Room Code
    /// - Left Panel: 5-Card Excavation Area (Unearth button per slot), Relic & Resource Deck counters, Selected Hex Inspector
    /// - Right Panel: Active Player's Faction Board (Meters, Species Traits, Units, Battle Log)
    /// - Bottom Bar: Active Player's Resource Pile, Relic Hand (Assemble / Disassemble), and Phase Actions (Move, Battle, Loot, Recruit, Signature Ability, Next Phase)
    /// - Modal Overlay: 11-Faction Draft & Rulebook Codex / Multiplayer Room Lobby
    /// </summary>
    public class CrittersTabletopHUD : MonoBehaviour
    {
        private CrittersGameEngine _engine;
        private Tabletop3DBoardRenderer _boardRenderer;
        private CrittersMultiplayerSessionManager _mpManager;

        private Font _uiFont;
        private Canvas _canvas;

        // UI References
        private Text _topStatusText;
        private Text[] _playerScoreBadges = new Text[4];
        private Text _selectedHexText;
        private Text _factionBoardText;
        private Text _gameLogText;
        private Text _handAndResourcesText;
        private Transform _excavationButtonsRoot;
        private Transform _handButtonsRoot;
        private GameObject _lobbyCodexModal;
        private Text _codexDetailText;

        // Move action two-click state
        private bool _awaitingMoveDestination = false;
        private HexCoord _moveOriginCoord;

        // Faction draft slots for 4 players
        private FactionId[] _draftedFactions = new FactionId[4]
        {
            FactionId.CurlyTail,
            FactionId.HordeOfTheMillions,
            FactionId.PlatypusPapacy,
            FactionId.BriaredFlower
        };
        private int _editingPlayerSlot = 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBootstrapScene()
        {
            if (FindObjectsByType<CrittersTabletopHUD>().Length > 0)
                return;

            var rootGo = new GameObject("CrittersOfCalamity_GameController");
            DontDestroyOnLoad(rootGo);

            var mp = rootGo.AddComponent<CrittersMultiplayerSessionManager>();
            var board = rootGo.AddComponent<Tabletop3DBoardRenderer>();
            var hud = rootGo.AddComponent<CrittersTabletopHUD>();
            hud.Initialize(board, mp);
        }

        public void Initialize(Tabletop3DBoardRenderer boardRenderer, CrittersMultiplayerSessionManager mpManager)
        {
            _boardRenderer = boardRenderer;
            _mpManager = mpManager;
            _uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_uiFont == null)
                _uiFont = Font.CreateDynamicFontFromOSFont("Arial", 14);

            EnsureEventSystem();
            BuildCanvasHierarchy();

            _engine = new CrittersGameEngine();
            _engine.OnStateChanged += RefreshHUD;
            _boardRenderer.OnHexClicked += HandleHexClicked;
            _mpManager.OnSessionUpdated += RefreshHUD;

            StartMatchWithDraftedFactions();
        }

        private void StartMatchWithDraftedFactions()
        {
            _awaitingMoveDestination = false;
            _engine.StartNewMatch(_draftedFactions.ToList(), new List<string>
            {
                "Player 1", "Player 2", "Player 3", "Player 4"
            }, mapRadius: 4);
            _boardRenderer.BindEngine(_engine);
            RefreshHUD();
        }

        private void HandleHexClicked(HexCoord coord)
        {
            if (_awaitingMoveDestination)
            {
                _awaitingMoveDestination = false;
                if (!_engine.TryMoveUnits(_moveOriginCoord, coord))
                {
                    _engine.Log($"Move from {_moveOriginCoord} to {coord} was not legal.");
                }
            }
            RefreshHUD();
        }

        private void EnsureEventSystem()
        {
            if (FindObjectsByType<EventSystem>().Length == 0)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<EventSystem>();
                esGo.AddComponent<InputSystemUIInputModule>();
            }
        }

        private void BuildCanvasHierarchy()
        {
            var canvasGo = new GameObject("TabletopHUD_Canvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            // 1. TOP BAR: 4-Player Authority Track (30 Authority to Win) + Lobby/Codex Toggle
            var topBar = CreatePanel(canvasGo.transform, "TopBar",
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, 0f), new Vector2(0f, 82f), new Color(0.11f, 0.14f, 0.19f, 0.94f));

            _topStatusText = CreateText(topBar, "TopStatusText", "CRITTERS OF CALAMITY — Race to 30 Authority", 16, TextAnchor.MiddleLeft,
                new Vector2(0f, 0f), new Vector2(0.32f, 1f), new Vector2(16f, 0f), new Vector2(-8f, 0f), Palette.AuthorityGold);

            for (int i = 0; i < 4; i++)
            {
                float xMin = 0.32f + i * 0.135f;
                float xMax = xMin + 0.13f;
                var badgePanel = CreatePanel(topBar, $"PlayerBadge_{i}",
                    new Vector2(xMin, 0.12f), new Vector2(xMax, 0.88f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, Vector2.zero, new Color(0.18f, 0.22f, 0.28f, 0.95f));
                _playerScoreBadges[i] = CreateText(badgePanel, $"BadgeText_{i}", $"P{i + 1}", 13, TextAnchor.MiddleCenter,
                    Vector2.zero, Vector2.one, new Vector2(4f, 2f), new Vector2(-4f, -2f), Color.white);
            }

            CreateButton(topBar, "BtnLobbyCodex", "11 Factions / Room Lobby",
                new Vector2(0.87f, 0.15f), new Vector2(0.99f, 0.85f), Palette.FromHex("#8C5328"), () =>
                {
                    _lobbyCodexModal.SetActive(!_lobbyCodexModal.activeSelf);
                    RefreshCodexModal();
                });

            // 2. LEFT PANEL: 5-Card Excavation Area & Selected Hex Inspector
            var leftPanel = CreatePanel(canvasGo.transform, "LeftPanel",
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(0f, 60f), new Vector2(315f, -295f), new Color(0.12f, 0.15f, 0.20f, 0.91f));

            CreateText(leftPanel, "ExcavationHeader", "EXCAVATION AREA (5 Face-Up Relics)", 14, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.92f), new Vector2(1f, 0.99f), Vector2.zero, Vector2.zero, Palette.AuthorityGold);

            var excRootGo = new GameObject("ExcavationSlots");
            excRootGo.transform.SetParent(leftPanel, false);
            var excRt = excRootGo.AddComponent<RectTransform>();
            excRt.anchorMin = new Vector2(0.03f, 0.42f);
            excRt.anchorMax = new Vector2(0.97f, 0.91f);
            excRt.offsetMin = Vector2.zero;
            excRt.offsetMax = Vector2.zero;
            var excLayout = excRootGo.AddComponent<VerticalLayoutGroup>();
            excLayout.spacing = 4f;
            excLayout.childControlHeight = true;
            excLayout.childControlWidth = true;
            excLayout.childForceExpandHeight = true;
            excLayout.childForceExpandWidth = true;
            _excavationButtonsRoot = excRootGo.transform;

            _selectedHexText = CreateText(leftPanel, "SelectedHexInspector", "Click any 3D Hex Tile to inspect terrain, buildings, tokens, and units.", 13, TextAnchor.UpperLeft,
                new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.40f), Vector2.zero, Vector2.zero, Palette.ParchmentMap);

            // 3. RIGHT PANEL: Faction Board, Meters & Live Battle / Turn Log
            var rightPanel = CreatePanel(canvasGo.transform, "RightPanel",
                new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
                new Vector2(0f, 60f), new Vector2(360f, -295f), new Color(0.12f, 0.15f, 0.20f, 0.91f));

            _factionBoardText = CreateText(rightPanel, "FactionBoardText", "Faction Board", 12, TextAnchor.UpperLeft,
                new Vector2(0.04f, 0.44f), new Vector2(0.96f, 0.98f), Vector2.zero, Vector2.zero, Color.white);

            _gameLogText = CreateText(rightPanel, "GameLogText", "Turn & Battle Log", 12, TextAnchor.LowerLeft,
                new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.42f), Vector2.zero, Vector2.zero, Palette.FromHex("#D0DCE8"));

            // 4. BOTTOM BAR: Active Player's Resource Pile, Relic Hand & Turn Phase Actions
            var bottomBar = CreatePanel(canvasGo.transform, "BottomBar",
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 0f), new Vector2(0f, 205f), new Color(0.10f, 0.13f, 0.17f, 0.95f));

            _handAndResourcesText = CreateText(bottomBar, "HandAndResourcesText", "Resources & Hand", 13, TextAnchor.UpperLeft,
                new Vector2(0.01f, 0.52f), new Vector2(0.54f, 0.96f), Vector2.zero, Vector2.zero, Palette.ParchmentMap);

            // Hand Relic card Assemble/Disassemble buttons row
            var handRootGo = new GameObject("HandButtonsRow");
            handRootGo.transform.SetParent(bottomBar, false);
            var handRt = handRootGo.AddComponent<RectTransform>();
            handRt.anchorMin = new Vector2(0.01f, 0.06f);
            handRt.anchorMax = new Vector2(0.54f, 0.50f);
            handRt.offsetMin = Vector2.zero;
            handRt.offsetMax = Vector2.zero;
            var handLayout = handRootGo.AddComponent<HorizontalLayoutGroup>();
            handLayout.spacing = 6f;
            handLayout.childControlHeight = true;
            handLayout.childControlWidth = true;
            handLayout.childForceExpandHeight = true;
            handLayout.childForceExpandWidth = true;
            _handButtonsRoot = handRootGo.transform;

            // Phase Action Buttons (Excavation & Main Phase)
            CreateButton(bottomBar, "BtnCraftBuilding", "2nd Phase: Faction Craft Building",
                new Vector2(0.55f, 0.54f), new Vector2(0.69f, 0.92f), Palette.FromHex("#4A6B48"), () =>
                {
                    if (_boardRenderer.SelectedCoord.HasValue)
                        _engine.TryFactionCraftBuilding(_boardRenderer.SelectedCoord.Value);
                    else
                        _engine.Log("Select a legal 3D hex tile first to craft your faction's building.");
                    RefreshHUD();
                });

            CreateButton(bottomBar, "BtnMove", "3rd Phase: Move Units",
                new Vector2(0.70f, 0.54f), new Vector2(0.81f, 0.92f), Palette.FromHex("#35688C"), () =>
                {
                    if (_boardRenderer.SelectedCoord.HasValue)
                    {
                        _moveOriginCoord = _boardRenderer.SelectedCoord.Value;
                        _awaitingMoveDestination = true;
                        _engine.Log($"Move Origin set to {_moveOriginCoord}. Now click an adjacent destination hex tile!");
                        RefreshHUD();
                    }
                });

            CreateButton(bottomBar, "BtnBattle", "3rd Phase: Battle (2d6)",
                new Vector2(0.82f, 0.54f), new Vector2(0.93f, 0.92f), Palette.FromHex("#9C2F2F"), () =>
                {
                    if (_boardRenderer.SelectedCoord.HasValue)
                        _engine.TryBattleOnTile(_boardRenderer.SelectedCoord.Value);
                    else
                        _engine.Log("Select a hex tile with both friendly and enemy pieces to initiate a Battle!");
                    RefreshHUD();
                });

            CreateButton(bottomBar, "BtnLoot", "3rd Phase: Loot Tile",
                new Vector2(0.55f, 0.08f), new Vector2(0.66f, 0.46f), Palette.FromHex("#8A6E2F"), () =>
                {
                    if (_boardRenderer.SelectedCoord.HasValue)
                        _engine.TryLootTile(_boardRenderer.SelectedCoord.Value);
                    else
                        _engine.Log("Select a hex tile occupied by your units (and no enemies) to Loot.");
                    RefreshHUD();
                });

            CreateButton(bottomBar, "BtnRecruit", "3rd Phase: Recruit Units",
                new Vector2(0.67f, 0.08f), new Vector2(0.78f, 0.46f), Palette.FromHex("#4E568C"), () =>
                {
                    _engine.TryRecruit();
                    RefreshHUD();
                });

            CreateButton(bottomBar, "BtnSignatureAbility", "Free Action: Faction Ability",
                new Vector2(0.79f, 0.08f), new Vector2(0.90f, 0.46f), Palette.FromHex("#72428C"), () =>
                {
                    var coord = _boardRenderer.SelectedCoord ?? new HexCoord(0, 0);
                    _engine.TryUseFactionSignatureAbility(coord);
                    RefreshHUD();
                });

            CreateButton(bottomBar, "BtnAdvancePhase", "NEXT PHASE >>",
                new Vector2(0.91f, 0.08f), new Vector2(0.99f, 0.46f), Palette.FromHex("#C87A28"), () =>
                {
                    _awaitingMoveDestination = false;
                    _engine.AdvancePhase();
                    RefreshHUD();
                });

            // 5. MODAL: 11-Faction Draft Codex & Multiplayer Room Lobby
            BuildLobbyCodexModal(canvasGo.transform);
        }

        private void BuildLobbyCodexModal(Transform canvasParent)
        {
            var modalRt = CreatePanel(canvasParent, "LobbyCodexModal",
                new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.88f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, new Color(0.09f, 0.11f, 0.15f, 0.98f));
            _lobbyCodexModal = modalRt.gameObject;

            CreateText(modalRt, "ModalTitle",
                "CRITTERS OF CALAMITY — 11-FACTION CODEX, DRAFT & MULTIPLAYER GROUP LOBBY",
                18, TextAnchor.MiddleLeft, new Vector2(0.02f, 0.91f), new Vector2(0.70f, 0.99f), Vector2.zero, Vector2.zero, Palette.AuthorityGold);

            CreateButton(modalRt, "BtnHostRoom", "Host Online Group Room",
                new Vector2(0.58f, 0.92f), new Vector2(0.73f, 0.98f), Palette.FromHex("#2E7D5B"), async () =>
                {
                    await _mpManager.HostGroupRoomAsync();
                    RefreshCodexModal();
                });

            CreateButton(modalRt, "BtnStartNewMatch", "Start Match With Selected Factions",
                new Vector2(0.74f, 0.92f), new Vector2(0.91f, 0.98f), Palette.FromHex("#B86B28"), () =>
                {
                    StartMatchWithDraftedFactions();
                    _lobbyCodexModal.SetActive(false);
                });

            CreateButton(modalRt, "BtnCloseModal", "Close [X]",
                new Vector2(0.92f, 0.92f), new Vector2(0.99f, 0.98f), Palette.FromHex("#7A2828"), () =>
                {
                    _lobbyCodexModal.SetActive(false);
                });

            // Player slot selector buttons (P1..P4)
            for (int pSlot = 0; pSlot < 4; pSlot++)
            {
                int capturedSlot = pSlot;
                float xMin = 0.02f + pSlot * 0.11f;
                CreateButton(modalRt, $"BtnSlot_{pSlot}", $"Assign Seat {pSlot + 1}",
                    new Vector2(xMin, 0.83f), new Vector2(xMin + 0.105f, 0.89f), Palette.FromHex("#3A4B63"), () =>
                    {
                        _editingPlayerSlot = capturedSlot;
                        RefreshCodexModal();
                    });
            }

            // 11 Faction Picker Buttons on Left of Modal
            var allFactions = RulebookDatabase.GetAllFactions();
            for (int fIdx = 0; fIdx < allFactions.Count; fIdx++)
            {
                var fac = allFactions[fIdx];
                float yMax = 0.80f - fIdx * 0.068f;
                float yMin = yMax - 0.062f;
                CreateButton(modalRt, $"BtnFac_{fac.Id}",
                    $"{fac.Name} ({fac.DifficultyRatingOutOf10}/10 - {fac.Tier})",
                    new Vector2(0.02f, yMin), new Vector2(0.42f, yMax), fac.PrimaryColor * 0.75f, () =>
                    {
                        _draftedFactions[_editingPlayerSlot] = fac.Id;
                        RefreshCodexModal(fac);
                    });
            }

            _codexDetailText = CreateText(modalRt, "CodexDetailText", "", 13, TextAnchor.UpperLeft,
                new Vector2(0.44f, 0.04f), new Vector2(0.98f, 0.88f), Vector2.zero, Vector2.zero, Palette.ParchmentMap);

            _lobbyCodexModal.SetActive(false);
        }

        private void RefreshCodexModal(FactionDef inspectFaction = null)
        {
            if (_codexDetailText == null) return;
            var allFactions = RulebookDatabase.GetAllFactions();
            if (inspectFaction == null)
                inspectFaction = allFactions.First(f => f.Id == _draftedFactions[_editingPlayerSlot]);

            var sb = new StringBuilder();
            sb.AppendLine($"<b>MULTIPLAYER ROOM:</b> {_mpManager.StatusMessage} (Code: {_mpManager.ActiveJoinCode})");
            sb.AppendLine($"<b>CURRENT 4-PLAYER TABLE DRAFT</b> (Editing Seat {_editingPlayerSlot + 1}):");
            for (int i = 0; i < 4; i++)
            {
                var f = allFactions.First(x => x.Id == _draftedFactions[i]);
                string marker = (i == _editingPlayerSlot) ? ">> " : "   ";
                sb.AppendLine($"{marker}Seat {i + 1}: <b>{f.Name}</b> (Difficulty {f.DifficultyRatingOutOf10}/10)");
            }
            sb.AppendLine("------------------------------------------------------------------");
            sb.AppendLine($"<b>{inspectFaction.Name.ToUpperInvariant()}</b> — {inspectFaction.Tier} ({inspectFaction.DifficultyRatingOutOf10}/10)");
            sb.AppendLine(inspectFaction.ShortSummary);
            sb.AppendLine($"\n<b>Setup:</b> {inspectFaction.SetupInstructions}");
            sb.AppendLine($"<b>Buildings:</b> {inspectFaction.BuildingRules}");
            sb.AppendLine("\n<b>Species Traits:</b>");
            foreach (var tr in inspectFaction.SpeciesTraits)
                sb.AppendLine($" • {tr}");
            sb.AppendLine("\n<b>Units & Special Units (ATK/DEF/HP):</b>");
            foreach (var u in inspectFaction.Units)
                sb.AppendLine($" • <b>{u.Name}</b> ({u.BaseAttack}/{u.BaseDefense}/{u.BaseMaxHP}) [x{u.SupplyCount}]: {u.AbilitiesText}");
            sb.AppendLine("\n<b>Ways Opponents Score Authority:</b>");
            foreach (var s in inspectFaction.EnemyAuthorityScoring)
                sb.AppendLine($" • {s}");

            _codexDetailText.text = sb.ToString();
        }

        public void RefreshHUD()
        {
            if (_engine == null || _engine.Players.Count == 0) return;

            var activePlayer = _engine.Players[_engine.CurrentTurnPlayerIndex];

            // 1. Top Bar
            string moveHint = _awaitingMoveDestination ? $" | [SELECT DESTINATION HEX FROM {_moveOriginCoord}]" : "";
            _topStatusText.text = $"<b>TURN {_engine.TurnNumber} — {activePlayer.DisplayName} ({activePlayer.Faction.Name})</b>\n" +
                                  $"Phase: <b>{_engine.CurrentPhase}</b> (Main Actions Left: {_engine.MainPhaseActionsRemaining}/3){moveHint}";

            for (int i = 0; i < _engine.Players.Count && i < 4; i++)
            {
                var p = _engine.Players[i];
                bool isTurn = (i == _engine.CurrentTurnPlayerIndex);
                string turnStar = isTurn ? "★ " : "";
                _playerScoreBadges[i].text = $"{turnStar}<b>{p.DisplayName}: {p.Faction.Name}</b>\n" +
                                             $"Authority: <b>{p.Authority}/30</b> | Res: {p.ResourcePile.Count} | Hand: {p.RelicHand.Count}";
                _playerScoreBadges[i].color = isTurn ? Palette.AuthorityGold : Color.white;
            }

            // 2. Left Panel: Excavation Area (5 face-up Relic cards)
            foreach (Transform child in _excavationButtonsRoot)
                Destroy(child.gameObject);

            for (int i = 0; i < _engine.ExcavationArea.Count; i++)
            {
                int slotIdx = i;
                var card = _engine.ExcavationArea[i];
                CreateLayoutButton(_excavationButtonsRoot, $"ExcSlot_{i}",
                    $"<b>[Unearth] {card.Name}</b> (Cost: {card.Cost})\n{card.EffectText}",
                    new Color(0.20f, 0.25f, 0.32f, 0.95f), () =>
                    {
                        if (!_engine.TryUnearthRelic(slotIdx))
                            _engine.Log($"Cannot Unearth '{card.Name}' (need 2nd Excavation Phase, unused Unearth, and {card.Cost}).");
                        RefreshHUD();
                    });
            }

            // Selected Hex Inspector
            if (_boardRenderer.SelectedCoord.HasValue && _engine.Board.TryGetValue(_boardRenderer.SelectedCoord.Value, out var tile))
            {
                var sbHex = new StringBuilder();
                sbHex.AppendLine($"<b>SELECTED HEX {tile.Coord}</b> — Terrain: <b>{tile.EffectiveTerrain}</b>");
                if (tile.Overlay != FactionOverlayTileType.None)
                    sbHex.AppendLine($"Overlay Tile: <b>{tile.Overlay}</b>");
                if (tile.Building != null)
                    sbHex.AppendLine($"Building: <b>{tile.Building.Type}</b> ({_engine.Players[tile.Building.OwnerPlayerIndex].Faction.Name})");
                if (tile.Tokens.Count > 0)
                    sbHex.AppendLine($"Tokens ({tile.Tokens.Count}/2): {string.Join(", ", tile.Tokens.Select(t => t.Type.ToString()))}");
                sbHex.AppendLine($"Looted This Turn: {(tile.LootedThisTurn ? "Yes" : "No")}");
                sbHex.AppendLine($"<b>Units on Tile ({tile.Units.Count}):</b>");
                foreach (var u in tile.Units)
                {
                    var uOwner = _engine.Players[u.OwnerPlayerIndex];
                    sbHex.AppendLine($" • [{uOwner.Faction.Name}] <b>{u.Definition.Name}</b> ({u.EffectiveAttack}/{u.EffectiveDefense}/{u.CurrentHP}HP)");
                }
                _selectedHexText.text = sbHex.ToString();
            }
            else
            {
                _selectedHexText.text = $"<b>DECKS & CONTROLS</b>\nRelic Deck: {_engine.RelicDeck.Count}/40 (Discard: {_engine.RelicDiscardPile.Count})\n" +
                                        $"Resource Deck: {_engine.ResourceDeck.Count}/50 (Burned: {_engine.BurnedResources.Count})\n\n" +
                                        "Camera: WASD / Arrows to Pan, Right-Click Drag to Orbit, Scroll to Zoom.\nLeft-Click any 3D Hex Tile to inspect or target actions.";
            }

            // 3. Right Panel: Active Player Faction Board & Meters + Log
            var sbFac = new StringBuilder();
            sbFac.AppendLine($"<b>{activePlayer.Faction.Name.ToUpperInvariant()} — FACTION BOARD</b>");
            sbFac.AppendLine(GetFactionMeterSummary(activePlayer));
            sbFac.AppendLine($"<b>Building ({activePlayer.Faction.PrimaryBuilding}):</b> Cost {activePlayer.Faction.PrimaryBuildingCost}");
            sbFac.AppendLine("<b>Species Traits:</b>");
            foreach (var tr in activePlayer.Faction.SpeciesTraits.Take(4))
                sbFac.AppendLine($" • {tr}");
            if (activePlayer.FaceUpRelics.Count > 0)
                sbFac.AppendLine($"<b>Crafted Face-Up Relics:</b> {string.Join(", ", activePlayer.FaceUpRelics.Select(r => r.Name))}");
            _factionBoardText.text = sbFac.ToString();

            var recentLogs = _engine.GameLog.Skip(Mathf.Max(0, _engine.GameLog.Count - 11));
            _gameLogText.text = "<b>TABLETOP EVENT & BATTLE LOG:</b>\n" + string.Join("\n", recentLogs);

            // 4. Bottom Bar: Active Player's Resource Pile & Relic Hand
            int wCount = activePlayer.ResourcePile.Count(r => r == ResourceType.Wood);
            int sCount = activePlayer.ResourcePile.Count(r => r == ResourceType.Stone);
            int gCount = activePlayer.ResourcePile.Count(r => r == ResourceType.Gold);
            int cCount = activePlayer.ResourcePile.Count(r => r == ResourceType.Charcoal);
            _handAndResourcesText.text =
                $"<b>{activePlayer.DisplayName}'s Resource Pile ({activePlayer.ResourcePile.Count} cards):</b> " +
                $"Wood: <b>{wCount}</b> | Stone: <b>{sCount}</b> | Gold: <b>{gCount}</b>" +
                (cCount > 0 ? $" | Charcoal: <b>{cCount}</b>" : "") +
                $"\n<b>Relic Hand ({activePlayer.RelicHand.Count} cards)</b> — Click a card during 2nd Excavation Phase to Assemble (Left) or Disassemble for Resources (Right):";

            foreach (Transform child in _handButtonsRoot)
                Destroy(child.gameObject);

            for (int hIdx = 0; hIdx < activePlayer.RelicHand.Count; hIdx++)
            {
                int capturedIdx = hIdx;
                var card = activePlayer.RelicHand[hIdx];
                CreateLayoutButton(_handButtonsRoot, $"HandCard_{hIdx}",
                    $"<b>{card.Name}</b>\n[Assemble: {card.Cost}]\n[Disassemble: +{card.DisassembleGain}]",
                    new Color(0.24f, 0.21f, 0.16f, 0.96f), () =>
                    {
                        // Assemble if possible, otherwise Disassemble for resources
                        if (!_engine.TryAssembleRelicFromHand(capturedIdx))
                        {
                            if (!_engine.TryDisassembleRelicFromHand(capturedIdx))
                                _engine.Log($"Could not Assemble or Disassemble '{card.Name}' right now.");
                        }
                        RefreshHUD();
                    });
            }
        }

        private string GetFactionMeterSummary(PlayerState p)
        {
            switch (p.Faction.Id)
            {
                case FactionId.Necromouser:
                    return $"<b>Cauldron Dead Units:</b> {p.NecromouserCauldronDeadCount} | Special Summons: {p.NecromouserSpecialSummonsUsed}/4 | Awakened: {p.NecromouserAwakened}";
                case FactionId.GoatCabal:
                    return $"<b>Purgatory Card:</b> {p.GoatPurgatoryCount} Purged Goats";
                case FactionId.CurlyTail:
                    return $"<b>Meters:</b> Thirst for Glory Lv.{p.CurlyTailThirstForGlory}/4 | Hunger Lv.{p.CurlyTailHunger}/4 | Momentum Lv.{p.CurlyTailMomentum}/4";
                case FactionId.FirestartingSquirrels:
                    return $"<b>Crafted Acorn Firestarters:</b> {p.SquirrelsCraftedFirestarters}";
                case FactionId.PlatypusPapacy:
                    return $"<b>Faith Meter:</b> {p.PlatypusFaith}/5 | <b>Platy-Pope Level:</b> {p.PlatyPopeLevel}/5 | Crusade Turns: {p.CrusadeTurnsRemaining}";
                case FactionId.CarnivorousQueen:
                    return $"<b>Evolution Stage:</b> {p.QueenStage}/4 (Feeding: {p.QueenFeeding}/7) | <b>Rage Meter:</b> {p.QueenRage}/15";
                case FactionId.GlacierKingdom:
                    return $"<b>Blizzard Meter Active:</b> {p.GlacierBlizzardActive}";
                case FactionId.BriaredFlower:
                    return $"<b>Thorn Meter:</b> {p.BriaredFlowerThorns}/6 Thorns ({(p.BriaredFlowerThorns == 0 ? "FRENZIED BLOOM ACTIVE!" : "Thorned Body")})";
                default:
                    return $"<b>Difficulty:</b> {p.Faction.DifficultyRatingOutOf10}/10 ({p.Faction.Tier})";
            }
        }

        // =====================================================================
        // uGUI FACTORY HELPERS
        // =====================================================================

        private RectTransform CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            var img = go.AddComponent<Image>();
            img.color = color;
            return rt;
        }

        private Text CreateText(Transform parent, string name, string content, int fontSize, TextAnchor align, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            var txt = go.AddComponent<Text>();
            txt.font = _uiFont;
            txt.text = content;
            txt.fontSize = fontSize;
            txt.alignment = align;
            txt.color = color;
            txt.supportRichText = true;
            return txt;
        }

        private Button CreateButton(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, Color bgColor, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.color = bgColor;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            CreateText(go.transform, "Label", label, 12, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(4f, 2f), new Vector2(-4f, -2f), Color.white);
            return btn;
        }

        private Button CreateLayoutButton(Transform parent, string name, string label, Color bgColor, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            var img = go.AddComponent<Image>();
            img.color = bgColor;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            CreateText(go.transform, "Label", label, 11, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, new Vector2(6f, 2f), new Vector2(-6f, -2f), Color.white);
            return btn;
        }
    }
}
