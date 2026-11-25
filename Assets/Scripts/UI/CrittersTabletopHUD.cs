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
    /// Full uGUI Tabletop Interface (no OnGUI) for Eben's Critters of Calamity - Core Rulebook: Eight Faction Edition:
    /// - Top Bar: 4-Player Authority Scoreboard (click any player badge to focus camera on their stronghold), Turn/Phase Banner, Capybara Stall toggle, 8-Faction Codex & Room Lobby toggle
    /// - Left Panel (Collapsible): 5-Card Excavation Area (Unearth button per slot), Relic & Resource Deck counters, Selected Hex Inspector (showing 6-unit stacking limit)
    /// - Right Panel (Collapsible): Active Player's Faction Board (Meters, Species Traits, Table Relics, Battle Log)
    /// - Bottom Bar: Active Player's Resource Pile, Relic Hand with SEPARATE [Assemble] and [Disassemble] buttons per card, and Phase Actions (Craft Building, Move, Battle, Loot, Recruit, Faction Abilities Drawer, Next Phase)
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
        private RectTransform _leftPanelRt;
        private RectTransform _rightPanelRt;
        private Text _selectedHexText;
        private Text _factionBoardText;
        private Text _gameLogText;
        private Text _handAndResourcesText;
        private Transform _excavationButtonsRoot;
        private Transform _handButtonsRoot;

        // Modals
        private GameObject _lobbyCodexModal;
        private Text _codexDetailText;
        private InputField _joinCodeInput;

        private GameObject _factionActionsModal;
        private Transform _factionActionsListRoot;
        private Text _factionActionsHeaderText;

        private GameObject _stallModal;
        private Transform _stallButtonsRoot;

        private GameObject _battlePopupModal;
        private Text _battlePopupText;

        // Move action two-click state
        private bool _awaitingMoveDestination = false;
        private HexCoord _moveOriginCoord;

        // Faction draft slots for 4 players (defaulting to the first 4 Core Factions)
        private FactionId[] _draftedFactions = new FactionId[4]
        {
            FactionId.CurlyTail,
            FactionId.HordeOfTheMillions,
            FactionId.PlatypusPapacy,
            FactionId.TroubleMakingTanukis
        };
        private int _editingPlayerSlot = 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBootstrapScene()
        {
            var existing = FindObjectsByType<CrittersTabletopHUD>();
            if (existing.Length > 0)
            {
                existing[0].EnsureInitialized();
                return;
            }

            var rootGo = new GameObject("CrittersOfCalamity_GameController");
            DontDestroyOnLoad(rootGo);

            var mp = rootGo.AddComponent<CrittersMultiplayerSessionManager>();
            var board = rootGo.AddComponent<Tabletop3DBoardRenderer>();
            var hud = rootGo.AddComponent<CrittersTabletopHUD>();
            hud.Initialize(board, mp);
        }

        void Start()
        {
            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (_engine != null) return;
            var board = GetComponent<Tabletop3DBoardRenderer>() ?? gameObject.AddComponent<Tabletop3DBoardRenderer>();
            var mp = GetComponent<CrittersMultiplayerSessionManager>() ?? gameObject.AddComponent<CrittersMultiplayerSessionManager>();
            Initialize(board, mp);
        }

        public void Initialize(Tabletop3DBoardRenderer boardRenderer, CrittersMultiplayerSessionManager mpManager)
        {
            if (_engine != null) return;

            _boardRenderer = boardRenderer;
            _mpManager = mpManager;
            _uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_uiFont == null)
                _uiFont = Font.CreateDynamicFontFromOSFont("Arial", 14);

            EnsureEventSystem();
            BuildCanvasHierarchy();

            _engine = new CrittersGameEngine();
            _engine.OnStateChanged += RefreshHUD;
            _engine.OnBattleResolved += ShowBattleReportPopup;
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
            });
            _boardRenderer.BindEngine(_engine);
            _boardRenderer.ResetCameraToOverview();
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

            // 1. TOP BAR: 4-Player Authority Track (30 Authority to Win) + Quick Camera & Menu Buttons
            var topBar = CreatePanel(canvasGo.transform, "TopBar",
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, 0f), new Vector2(0f, 78f), new Color(0.10f, 0.13f, 0.18f, 0.95f));

            _topStatusText = CreateText(topBar, "TopStatusText", "CRITTERS OF CALAMITY — Core Rulebook (8 Factions)", 14, TextAnchor.MiddleLeft,
                new Vector2(0f, 0f), new Vector2(0.28f, 1f), new Vector2(14f, 0f), new Vector2(-6f, 0f), Palette.AuthorityGold);

            for (int i = 0; i < 4; i++)
            {
                int capturedIdx = i;
                float xMin = 0.285f + i * 0.115f;
                float xMax = xMin + 0.111f;
                var badgeBtn = CreateButton(topBar, $"PlayerBadge_{i}", $"P{i + 1}",
                    new Vector2(xMin, 0.10f), new Vector2(xMax, 0.90f), new Color(0.17f, 0.21f, 0.27f, 0.96f), () =>
                    {
                        FocusCameraOnPlayer(capturedIdx);
                    });
                _playerScoreBadges[i] = badgeBtn.GetComponentInChildren<Text>();
            }

            CreateButton(topBar, "BtnResetCam", "Overview Cam [Space]",
                new Vector2(0.75f, 0.15f), new Vector2(0.825f, 0.85f), Palette.FromHex("#355870"), () =>
                {
                    _boardRenderer.ResetCameraToOverview();
                });

            CreateButton(topBar, "BtnCapybaraStall", "Capybara Trader Stall",
                new Vector2(0.83f, 0.15f), new Vector2(0.905f, 0.85f), Palette.FromHex("#2E7D6B"), () =>
                {
                    ToggleModal(_stallModal);
                    RefreshStallModal();
                });

            CreateButton(topBar, "BtnLobbyCodex", "8 Factions / Room Lobby",
                new Vector2(0.91f, 0.15f), new Vector2(0.995f, 0.85f), Palette.FromHex("#8C5328"), () =>
                {
                    ToggleModal(_lobbyCodexModal);
                    RefreshCodexModal();
                });

            // 2. LEFT PANEL: 5-Card Excavation Area & Selected Hex Inspector
            _leftPanelRt = CreatePanel(canvasGo.transform, "LeftPanel",
                new Vector2(0f, 0.19f), new Vector2(0.175f, 0.92f), new Vector2(0f, 0.5f),
                Vector2.zero, Vector2.zero, new Color(0.11f, 0.14f, 0.19f, 0.90f));

            CreateText(_leftPanelRt, "ExcavationHeader", "EXCAVATION AREA (5 Relics)", 13, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.93f), new Vector2(1f, 0.99f), Vector2.zero, Vector2.zero, Palette.AuthorityGold);

            var excRootGo = new GameObject("ExcavationSlots");
            excRootGo.transform.SetParent(_leftPanelRt, false);
            var excRt = excRootGo.AddComponent<RectTransform>();
            excRt.anchorMin = new Vector2(0.03f, 0.41f);
            excRt.anchorMax = new Vector2(0.97f, 0.92f);
            excRt.offsetMin = Vector2.zero;
            excRt.offsetMax = Vector2.zero;
            var excLayout = excRootGo.AddComponent<VerticalLayoutGroup>();
            excLayout.spacing = 4f;
            excLayout.childControlHeight = true;
            excLayout.childControlWidth = true;
            excLayout.childForceExpandHeight = true;
            excLayout.childForceExpandWidth = true;
            _excavationButtonsRoot = excRootGo.transform;

            _selectedHexText = CreateText(_leftPanelRt, "SelectedHexInspector", "Click any 3D Hex Tile to inspect terrain, buildings, tokens, and units.", 12, TextAnchor.UpperLeft,
                new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.39f), Vector2.zero, Vector2.zero, Palette.ParchmentMap);

            // 3. RIGHT PANEL: Faction Board, Meters & Live Battle / Turn Log
            _rightPanelRt = CreatePanel(canvasGo.transform, "RightPanel",
                new Vector2(0.81f, 0.19f), new Vector2(1f, 0.92f), new Vector2(1f, 0.5f),
                Vector2.zero, Vector2.zero, new Color(0.11f, 0.14f, 0.19f, 0.90f));

            _factionBoardText = CreateText(_rightPanelRt, "FactionBoardText", "Faction Board", 12, TextAnchor.UpperLeft,
                new Vector2(0.04f, 0.43f), new Vector2(0.96f, 0.98f), Vector2.zero, Vector2.zero, Color.white);

            _gameLogText = CreateText(_rightPanelRt, "GameLogText", "Turn & Battle Log", 11, TextAnchor.LowerLeft,
                new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.41f), Vector2.zero, Vector2.zero, Palette.FromHex("#D0DCE8"));

            // 4. BOTTOM BAR: Active Player's Resource Pile, Relic Hand & Turn Phase Actions
            var bottomBar = CreatePanel(canvasGo.transform, "BottomBar",
                new Vector2(0f, 0f), new Vector2(1f, 0.185f), new Vector2(0.5f, 0f),
                Vector2.zero, Vector2.zero, new Color(0.09f, 0.12f, 0.16f, 0.95f));

            _handAndResourcesText = CreateText(bottomBar, "HandAndResourcesText", "Resources & Hand", 12, TextAnchor.UpperLeft,
                new Vector2(0.01f, 0.66f), new Vector2(0.55f, 0.97f), Vector2.zero, Vector2.zero, Palette.ParchmentMap);

            // Hand Relic card boxes with SEPARATE [Assemble] and [Disassemble] buttons
            var handRootGo = new GameObject("HandButtonsRow");
            handRootGo.transform.SetParent(bottomBar, false);
            var handRt = handRootGo.AddComponent<RectTransform>();
            handRt.anchorMin = new Vector2(0.01f, 0.05f);
            handRt.anchorMax = new Vector2(0.55f, 0.64f);
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
            CreateButton(bottomBar, "BtnCraftBuilding", "2. Excavation: Craft Building",
                new Vector2(0.56f, 0.53f), new Vector2(0.665f, 0.93f), Palette.FromHex("#4A6B48"), () =>
                {
                    if (_boardRenderer.SelectedCoord.HasValue)
                        _engine.TryFactionCraftBuilding(_boardRenderer.SelectedCoord.Value);
                    else
                        _engine.Log("Click a legal 3D hex tile first to craft your faction's building.");
                    RefreshHUD();
                });

            CreateButton(bottomBar, "BtnMove", "3. Main: Move Units",
                new Vector2(0.67f, 0.53f), new Vector2(0.775f, 0.93f), Palette.FromHex("#35688C"), () =>
                {
                    if (_boardRenderer.SelectedCoord.HasValue)
                    {
                        _moveOriginCoord = _boardRenderer.SelectedCoord.Value;
                        _awaitingMoveDestination = true;
                        _engine.Log($"Move Origin set to {_moveOriginCoord}. Now click an adjacent destination hex tile!");
                        RefreshHUD();
                    }
                    else
                    {
                        _engine.Log("Click the hex tile holding your units first, then click 'Move Units'.");
                        RefreshHUD();
                    }
                });

            CreateButton(bottomBar, "BtnBattle", "3. Main: Battle (2d6)",
                new Vector2(0.78f, 0.53f), new Vector2(0.885f, 0.93f), Palette.FromHex("#9C2F2F"), () =>
                {
                    if (_boardRenderer.SelectedCoord.HasValue)
                        _engine.TryBattleOnTile(_boardRenderer.SelectedCoord.Value);
                    else
                        _engine.Log("Select a hex tile with both friendly and enemy pieces to initiate a Battle!");
                    RefreshHUD();
                });

            CreateButton(bottomBar, "BtnToggleSidebars", "Hide/Show Side Panels",
                new Vector2(0.89f, 0.53f), new Vector2(0.992f, 0.93f), Palette.FromHex("#3E4A59"), () =>
                {
                    bool show = !_leftPanelRt.gameObject.activeSelf;
                    _leftPanelRt.gameObject.SetActive(show);
                    _rightPanelRt.gameObject.SetActive(show);
                });

            CreateButton(bottomBar, "BtnLoot", "3. Main: Loot Tile",
                new Vector2(0.56f, 0.07f), new Vector2(0.665f, 0.47f), Palette.FromHex("#8A6E2F"), () =>
                {
                    if (_boardRenderer.SelectedCoord.HasValue)
                        _engine.TryLootTile(_boardRenderer.SelectedCoord.Value);
                    else
                        _engine.Log("Select a hex tile occupied by your units (and no enemies) to Loot.");
                    RefreshHUD();
                });

            CreateButton(bottomBar, "BtnRecruit", "3. Main: Recruit Units",
                new Vector2(0.67f, 0.07f), new Vector2(0.775f, 0.47f), Palette.FromHex("#4E568C"), () =>
                {
                    _engine.TryRecruit();
                    RefreshHUD();
                });

            CreateButton(bottomBar, "BtnFactionActionsDrawer", "Faction Abilities & Summons...",
                new Vector2(0.78f, 0.07f), new Vector2(0.885f, 0.47f), Palette.FromHex("#72428C"), () =>
                {
                    ToggleModal(_factionActionsModal);
                    RefreshFactionActionsModal();
                });

            CreateButton(bottomBar, "BtnAdvancePhase", "NEXT PHASE >>",
                new Vector2(0.89f, 0.07f), new Vector2(0.992f, 0.47f), Palette.FromHex("#C87A28"), () =>
                {
                    _awaitingMoveDestination = false;
                    _engine.AdvancePhase();
                    RefreshHUD();
                });

            // 5. MODALS
            BuildLobbyCodexModal(canvasGo.transform);
            BuildFactionActionsModal(canvasGo.transform);
            BuildStallModal(canvasGo.transform);
            BuildBattlePopupModal(canvasGo.transform);
        }

        private void ToggleModal(GameObject targetModal)
        {
            bool nextState = !targetModal.activeSelf;
            if (_lobbyCodexModal != null) _lobbyCodexModal.SetActive(false);
            if (_factionActionsModal != null) _factionActionsModal.SetActive(false);
            if (_stallModal != null) _stallModal.SetActive(false);
            if (_battlePopupModal != null) _battlePopupModal.SetActive(false);
            targetModal.SetActive(nextState);
        }

        private void FocusCameraOnPlayer(int playerIndex)
        {
            if (_engine == null || playerIndex < 0 || playerIndex >= _engine.Players.Count) return;
            var tile = _engine.Board.Values.FirstOrDefault(t =>
                (t.Building != null && t.Building.OwnerPlayerIndex == playerIndex) ||
                t.Units.Any(u => u.OwnerPlayerIndex == playerIndex));
            if (tile != null)
            {
                _boardRenderer.SelectHex(tile.Coord);
                _boardRenderer.FocusCameraOnCoord(tile.Coord, 13f);
            }
        }

        private void BuildLobbyCodexModal(Transform canvasParent)
        {
            var modalRt = CreatePanel(canvasParent, "LobbyCodexModal",
                new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.90f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, new Color(0.08f, 0.10f, 0.14f, 0.98f));
            _lobbyCodexModal = modalRt.gameObject;

            CreateText(modalRt, "ModalTitle",
                "CRITTERS OF CALAMITY — CORE RULEBOOK: EIGHT FACTION EDITION (108-TILE BOARD)",
                16, TextAnchor.MiddleLeft, new Vector2(0.02f, 0.92f), new Vector2(0.55f, 0.99f), Vector2.zero, Vector2.zero, Palette.AuthorityGold);

            CreateButton(modalRt, "BtnHostRoom", "Host Online Room",
                new Vector2(0.47f, 0.925f), new Vector2(0.59f, 0.985f), Palette.FromHex("#2E7D5B"), async () =>
                {
                    await _mpManager.HostGroupRoomAsync();
                    RefreshCodexModal();
                });

            // Join Code InputField + Join Button
            var inputGo = new GameObject("JoinCodeInput");
            inputGo.transform.SetParent(modalRt, false);
            var inputRt = inputGo.AddComponent<RectTransform>();
            inputRt.anchorMin = new Vector2(0.595f, 0.925f);
            inputRt.anchorMax = new Vector2(0.685f, 0.985f);
            inputRt.offsetMin = Vector2.zero;
            inputRt.offsetMax = Vector2.zero;
            var inputImg = inputGo.AddComponent<Image>();
            inputImg.color = new Color(0.18f, 0.22f, 0.28f, 1f);
            _joinCodeInput = inputGo.AddComponent<InputField>();
            var txtComp = CreateText(inputGo.transform, "Text", "ROOM-CODE", 12, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(4f, 2f), new Vector2(-4f, -2f), Color.white);
            _joinCodeInput.textComponent = txtComp;
            _joinCodeInput.text = "ENTER-CODE";

            CreateButton(modalRt, "BtnJoinRoom", "Join Code",
                new Vector2(0.69f, 0.925f), new Vector2(0.765f, 0.985f), Palette.FromHex("#3A6B8C"), async () =>
                {
                    await _mpManager.JoinGroupRoomByCodeAsync(_joinCodeInput.text);
                    RefreshCodexModal();
                });

            CreateButton(modalRt, "BtnStartNewMatch", "Start Match on 108-Tile Board",
                new Vector2(0.77f, 0.925f), new Vector2(0.915f, 0.985f), Palette.FromHex("#B86B28"), () =>
                {
                    StartMatchWithDraftedFactions();
                    _lobbyCodexModal.SetActive(false);
                });

            CreateButton(modalRt, "BtnCloseModal", "Close [X]",
                new Vector2(0.92f, 0.925f), new Vector2(0.985f, 0.985f), Palette.FromHex("#7A2828"), () =>
                {
                    _lobbyCodexModal.SetActive(false);
                });

            // Player slot selector buttons (P1..P4) + Roll 2d6 Draft Order
            for (int pSlot = 0; pSlot < 4; pSlot++)
            {
                int capturedSlot = pSlot;
                float xMin = 0.02f + pSlot * 0.10f;
                CreateButton(modalRt, $"BtnSlot_{pSlot}", $"Assign Seat {pSlot + 1}",
                    new Vector2(xMin, 0.84f), new Vector2(xMin + 0.095f, 0.90f), Palette.FromHex("#3A4B63"), () =>
                    {
                        _editingPlayerSlot = capturedSlot;
                        RefreshCodexModal();
                    });
            }

            CreateButton(modalRt, "BtnRollDraftOrder", "Roll 2d6 Draft & Turn Order",
                new Vector2(0.43f, 0.84f), new Vector2(0.62f, 0.90f), Palette.FromHex("#6B4E8C"), () =>
                {
                    _engine.Log("Rolled 2d6 for Faction Draft & Turn Order (Reverse Setup Order Active)!");
                    RefreshCodexModal();
                });

            // 8 Core Faction Picker Buttons on Left of Modal
            var allFactions = RulebookDatabase.GetAllFactions();
            for (int fIdx = 0; fIdx < allFactions.Count; fIdx++)
            {
                var fac = allFactions[fIdx];
                float yMax = 0.80f - fIdx * 0.092f;
                float yMin = yMax - 0.082f;
                CreateButton(modalRt, $"BtnFac_{fac.Id}",
                    $"{fac.Name} (Difficulty {fac.DifficultyRatingOutOf10}/10)",
                    new Vector2(0.02f, yMin), new Vector2(0.40f, yMax), fac.PrimaryColor * 0.78f, () =>
                    {
                        _draftedFactions[_editingPlayerSlot] = fac.Id;
                        RefreshCodexModal(fac);
                    });
            }

            _codexDetailText = CreateText(modalRt, "CodexDetailText", "", 12, TextAnchor.UpperLeft,
                new Vector2(0.42f, 0.03f), new Vector2(0.98f, 0.82f), Vector2.zero, Vector2.zero, Palette.ParchmentMap);

            _lobbyCodexModal.SetActive(false);
        }

        private void BuildFactionActionsModal(Transform canvasParent)
        {
            var modalRt = CreatePanel(canvasParent, "FactionActionsModal",
                new Vector2(0.24f, 0.22f), new Vector2(0.76f, 0.82f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, new Color(0.10f, 0.12f, 0.17f, 0.98f));
            _factionActionsModal = modalRt.gameObject;

            _factionActionsHeaderText = CreateText(modalRt, "Header", "FACTION ABILITIES, TOKENS, TILES, EQUIPMENT & SUMMONS",
                15, TextAnchor.MiddleLeft, new Vector2(0.03f, 0.88f), new Vector2(0.84f, 0.98f), Vector2.zero, Vector2.zero, Palette.AuthorityGold);

            CreateButton(modalRt, "BtnClose", "Close [X]",
                new Vector2(0.86f, 0.89f), new Vector2(0.97f, 0.97f), Palette.FromHex("#7A2828"), () =>
                {
                    _factionActionsModal.SetActive(false);
                });

            var listGo = new GameObject("ActionsList");
            listGo.transform.SetParent(modalRt, false);
            var listRt = listGo.AddComponent<RectTransform>();
            listRt.anchorMin = new Vector2(0.04f, 0.05f);
            listRt.anchorMax = new Vector2(0.96f, 0.85f);
            listRt.offsetMin = Vector2.zero;
            listRt.offsetMax = Vector2.zero;
            var vLayout = listGo.AddComponent<VerticalLayoutGroup>();
            vLayout.spacing = 8f;
            vLayout.childControlHeight = true;
            vLayout.childControlWidth = true;
            vLayout.childForceExpandHeight = true;
            vLayout.childForceExpandWidth = true;
            _factionActionsListRoot = listGo.transform;

            _factionActionsModal.SetActive(false);
        }

        private void RefreshFactionActionsModal()
        {
            if (_engine == null || _factionActionsListRoot == null) return;
            foreach (Transform c in _factionActionsListRoot) Destroy(c.gameObject);

            var p = _engine.Players[_engine.CurrentTurnPlayerIndex];
            var targetCoord = _boardRenderer.SelectedCoord ?? new HexCoord(0, 0);
            _factionActionsHeaderText.text = $"<b>{p.DisplayName} ({p.Faction.Name})</b> — Faction Actions (Target Hex: {targetCoord})";

            var actions = _engine.GetAvailableFactionActions(p);
            for (int i = 0; i < actions.Count; i++)
            {
                int capturedIdx = i;
                CreateLayoutButton(_factionActionsListRoot, $"FacAct_{i}",
                    $"<b>[{i + 1}] {actions[i]}</b>",
                    p.Faction.PrimaryColor * 0.75f, () =>
                    {
                        var coord = _boardRenderer.SelectedCoord ?? new HexCoord(0, 0);
                        if (!_engine.ExecuteSpecificFactionAction(capturedIdx, coord))
                            _engine.Log($"Could not execute '{actions[capturedIdx]}' — check resource/meter requirements or selected hex.");
                        RefreshFactionActionsModal();
                        RefreshHUD();
                    });
            }

            foreach (var eq in p.Faction.Equipment)
            {
                var capturedEq = eq;
                CreateLayoutButton(_factionActionsListRoot, $"EqCraft_{eq.Id}",
                    $"<b>[Craft Equipment] {eq.Name}</b> (Cost: {eq.Cost}) -> {eq.EffectText}",
                    new Color(0.28f, 0.24f, 0.16f, 0.96f), () =>
                    {
                        if (!_engine.TryCraftAndEquipFactionEquipment(capturedEq.Id))
                            _engine.Log($"Cannot craft/equip '{capturedEq.Name}' (need {capturedEq.Cost} and target unit '{capturedEq.TargetUnitId}' on board).");
                        RefreshFactionActionsModal();
                        RefreshHUD();
                    });
            }
        }

        private void BuildStallModal(Transform canvasParent)
        {
            var modalRt = CreatePanel(canvasParent, "CapybaraStallModal",
                new Vector2(0.22f, 0.26f), new Vector2(0.78f, 0.78f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, new Color(0.10f, 0.13f, 0.17f, 0.98f));
            _stallModal = modalRt.gameObject;

            CreateText(modalRt, "Header", "THE CAPYBARA CARAVAN — 3-CARD TRADED GOODS STALL",
                15, TextAnchor.MiddleLeft, new Vector2(0.03f, 0.87f), new Vector2(0.85f, 0.97f), Vector2.zero, Vector2.zero, Palette.AuthorityGold);

            CreateButton(modalRt, "BtnClose", "Close [X]",
                new Vector2(0.87f, 0.88f), new Vector2(0.97f, 0.96f), Palette.FromHex("#7A2828"), () =>
                {
                    _stallModal.SetActive(false);
                });

            var stallGo = new GameObject("StallSection");
            stallGo.transform.SetParent(modalRt, false);
            var stallRt = stallGo.AddComponent<RectTransform>();
            stallRt.anchorMin = new Vector2(0.04f, 0.08f);
            stallRt.anchorMax = new Vector2(0.96f, 0.84f);
            stallRt.offsetMin = Vector2.zero;
            stallRt.offsetMax = Vector2.zero;
            var sLayout = stallGo.AddComponent<VerticalLayoutGroup>();
            sLayout.spacing = 10f;
            sLayout.childControlHeight = true;
            sLayout.childControlWidth = true;
            sLayout.childForceExpandHeight = true;
            sLayout.childForceExpandWidth = true;
            _stallButtonsRoot = stallGo.transform;

            _stallModal.SetActive(false);
        }

        private void RefreshStallModal()
        {
            if (_engine == null) return;
            foreach (Transform c in _stallButtonsRoot) Destroy(c.gameObject);

            for (int i = 0; i < _engine.TradedGoodsStall.Count; i++)
            {
                int slot = i;
                var tg = _engine.TradedGoodsStall[i];
                CreateLayoutButton(_stallButtonsRoot, $"Stall_{i}",
                    $"<b>[Buy Traded Good] {tg.Title}</b>\n{tg.EffectText}\n(Purchasing grants +1 Authority to Buyer & +1 Authority to Capybara Caravan, and places 1 Basic Unit as a Guest at a Wallow!)",
                    Palette.FromHex("#3A6B6C"), () =>
                    {
                        if (!_engine.TryBuyTradedGoodFromCapybaraStall(slot))
                            _engine.Log($"Cannot buy '{tg.Title}' (requires Capybara Caravan in match and {tg.Cost}).");
                        RefreshStallModal();
                        RefreshHUD();
                    });
            }
        }

        private void BuildBattlePopupModal(Transform canvasParent)
        {
            var modalRt = CreatePanel(canvasParent, "BattlePopupModal",
                new Vector2(0.28f, 0.28f), new Vector2(0.72f, 0.76f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, new Color(0.14f, 0.09f, 0.10f, 0.98f));
            _battlePopupModal = modalRt.gameObject;

            CreateText(modalRt, "Header", "5-PHASE 2D6 BATTLE RESOLUTION REPORT (PART 12)",
                16, TextAnchor.MiddleCenter, new Vector2(0.03f, 0.86f), new Vector2(0.97f, 0.98f), Vector2.zero, Vector2.zero, Palette.AuthorityGold);

            _battlePopupText = CreateText(modalRt, "Body", "", 13, TextAnchor.MiddleLeft,
                new Vector2(0.05f, 0.18f), new Vector2(0.95f, 0.84f), Vector2.zero, Vector2.zero, Color.white);

            CreateButton(modalRt, "BtnDismissBattle", "Continue Turn [OK]",
                new Vector2(0.35f, 0.04f), new Vector2(0.65f, 0.15f), Palette.FromHex("#8C3B2B"), () =>
                {
                    _battlePopupModal.SetActive(false);
                });

            _battlePopupModal.SetActive(false);
        }

        private void ShowBattleReportPopup(BattleReport r)
        {
            if (r == null || _battlePopupModal == null) return;
            var atk = _engine.Players[r.AttackerPlayerIndex];
            var def = _engine.Players[r.DefenderPlayerIndex];

            var sb = new StringBuilder();
            sb.AppendLine($"<b>Battle Hex:</b> {r.TileCoord}");
            sb.AppendLine($"<b>Phase 1 (Attack Declaration):</b> {atk.DisplayName} ({atk.Faction.Name}) attacked {def.DisplayName} ({def.Faction.Name})");
            sb.AppendLine($"<b>Phase 2 (2d6 Dice Roll):</b> Attacker rolled {r.AttackerDice1}+{r.AttackerDice2} (Mod: {r.AttackerDiceBonus:+#;-#;0}) | Defender rolled {r.DefenderDice1}+{r.DefenderDice2} (Mod: {r.DefenderDiceBonus:+#;-#;0})");
            sb.AppendLine($"<b>Phase 3 (Totals):</b> Attacker Total = <b>{r.AttackerTotal}</b> (ATK Points: {r.AttackerUnitAttackSum}) vs Defender Total = <b>{r.DefenderTotal}</b> (DEF Points: {r.DefenderUnitDefenseSum})");
            if (r.IsTie)
            {
                sb.AppendLine("<b>Phase 4 (Tie):</b> Equal Totals! Each side takes 1 HP of damage and the battle ends immediately.");
            }
            else
            {
                var win = _engine.Players[r.WinnerPlayerIndex];
                sb.AppendLine($"<b>Phase 4 (Damage Calculation):</b> <b>{win.DisplayName} WINS!</b> Damage Dealt: <b>{r.WinnerDamageDealt} HP</b> (Capped at 1 per winning unit) | Mandatory Loser Damage: <b>{r.MandatoryLoserDamageDealt} HP</b>");
            }
            sb.AppendLine($"<b>Phase 5 (Battle Resolution - 0 HP Units Removed):</b> {(r.DestroyedUnitNames.Count > 0 ? string.Join(", ", r.DestroyedUnitNames) : "None (Damage absorbed by multi-HP Special Units, Selfless Nature, or Thorn Armor)")}");

            _battlePopupText.text = sb.ToString();
            _battlePopupModal.SetActive(true);
        }

        private void RefreshCodexModal(FactionDef inspectFaction = null)
        {
            if (_codexDetailText == null) return;
            var allFactions = RulebookDatabase.GetAllFactions();
            if (inspectFaction == null)
                inspectFaction = allFactions.First(f => f.Id == _draftedFactions[_editingPlayerSlot]);

            var sb = new StringBuilder();
            sb.AppendLine($"<b>MULTIPLAYER ROOM:</b> {_mpManager.StatusMessage} (Join Code: <b>{_mpManager.ActiveJoinCode}</b>)");
            sb.AppendLine($"<b>CANONICAL BOARD:</b> {_engine.Board.Count} Tiles (90 Forest, 14 Stream, 4 Ruin) | Max 6 Units/Faction/Tile | Reverse Turn-Order Setup");
            sb.AppendLine($"<b>CURRENT 4-PLAYER TABLE DRAFT</b> (Editing Seat {_editingPlayerSlot + 1}):");
            for (int i = 0; i < 4; i++)
            {
                var f = allFactions.First(x => x.Id == _draftedFactions[i]);
                string marker = (i == _editingPlayerSlot) ? ">> " : "   ";
                sb.AppendLine($"{marker}Seat {i + 1}: <b>{f.Name}</b> (Difficulty {f.DifficultyRatingOutOf10}/10)");
            }
            sb.AppendLine("------------------------------------------------------------------");
            sb.AppendLine($"<b>{inspectFaction.Name.ToUpperInvariant()}</b> — Difficulty {inspectFaction.DifficultyRatingOutOf10}/10 (Recommended for New Players)");
            sb.AppendLine(inspectFaction.ShortSummary);
            sb.AppendLine($"\n<b>Setup:</b> {inspectFaction.SetupInstructions}");
            sb.AppendLine($"<b>Buildings:</b> {inspectFaction.BuildingRules}");
            sb.AppendLine("\n<b>Species Traits:</b>");
            foreach (var tr in inspectFaction.SpeciesTraits)
                sb.AppendLine($" • {tr}");
            sb.AppendLine("\n<b>Units & Special Units (ATK/DEF/HP):</b>");
            foreach (var u in inspectFaction.Units)
                sb.AppendLine($" • <b>{u.Name}</b> ({u.BaseAttack}/{u.BaseDefense}/{u.BaseMaxHP}) [x{u.SupplyCount}]: {u.AbilitiesText}");
            if (inspectFaction.MaterialsManifest.Count > 0)
                sb.AppendLine($"\n<b>Rulebook Materials List:</b> {string.Join(" · ", inspectFaction.MaterialsManifest)}");

            _codexDetailText.text = sb.ToString();
        }

        public void RefreshHUD()
        {
            if (_engine == null || _engine.Players.Count == 0) return;

            var activePlayer = _engine.Players[_engine.CurrentTurnPlayerIndex];

            // 1. Top Bar
            string moveHint = _awaitingMoveDestination ? $" | [CLICK DESTINATION HEX FROM {_moveOriginCoord}]" : "";
            _topStatusText.text = $"<b>TURN {_engine.TurnNumber} — {activePlayer.DisplayName} ({activePlayer.Faction.Name})</b>\n" +
                                  $"Phase: <b>{_engine.CurrentPhase}</b> (Main Actions: {_engine.MainPhaseActionsRemaining}/3) | Board: {_engine.Board.Count} Tiles{moveHint}";

            for (int i = 0; i < _engine.Players.Count && i < 4; i++)
            {
                var p = _engine.Players[i];
                bool isTurn = (i == _engine.CurrentTurnPlayerIndex);
                string turnStar = isTurn ? "★ " : "";
                _playerScoreBadges[i].text = $"{turnStar}<b>{p.DisplayName}: {p.Faction.Name}</b>\n" +
                                             $"Authority: <b>{p.Authority}/30</b> | Res: {p.ResourcePile.Count} | Hand: {p.RelicHand.Count}";
                _playerScoreBadges[i].color = isTurn ? Palette.AuthorityGold : Color.white;
            }

            // 2. Left Panel: Excavation Area (5 faceup Relic cards)
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
                            _engine.Log($"Cannot Unearth '{card.Name}' (need 2. Excavation Phase, unused Unearth, and {card.Cost}).");
                        RefreshHUD();
                    });
            }

            // Selected Hex Inspector
            if (_boardRenderer.SelectedCoord.HasValue && _engine.Board.TryGetValue(_boardRenderer.SelectedCoord.Value, out var tile))
            {
                var sbHex = new StringBuilder();
                sbHex.AppendLine($"<b>SELECTED HEX {tile.Coord}</b> — Terrain: <b>{tile.EffectiveTerrain}</b>{(tile.IsBoardEdge ? " (Edge)" : "")}");
                if (tile.Overlay != FactionOverlayTileType.None)
                    sbHex.AppendLine($"Unique Faction Tile: <b>{tile.Overlay}</b>");
                if (tile.Building != null)
                    sbHex.AppendLine($"Building (1/1): <b>{tile.Building.Type}</b> ({_engine.Players[tile.Building.OwnerPlayerIndex].Faction.Name})");
                if (tile.Tokens.Count > 0)
                    sbHex.AppendLine($"Tokens ({tile.Tokens.Count}/2): {string.Join(", ", tile.Tokens.Select(t => t.Type.ToString()))}");
                sbHex.AppendLine($"Looted This Turn: {(tile.LootedThisTurn ? "Yes" : "No")}");
                sbHex.AppendLine($"<b>Units on Tile ({tile.Units.Count} total | Max 6/faction):</b>");
                foreach (var u in tile.Units)
                {
                    var uOwner = _engine.Players[u.OwnerPlayerIndex];
                    string tags = (u.TrapLeafTurnsRemaining > 0 ? " [TRAP LEAF]" : "") + (u.IsGuest ? " [GUEST]" : "");
                    sbHex.AppendLine($" • [{uOwner.Faction.Name}] <b>{u.Definition.Name}</b> ({u.EffectiveAttack}/{u.EffectiveDefense}/{u.CurrentHP}HP){tags}");
                }
                _selectedHexText.text = sbHex.ToString();
            }
            else
            {
                _selectedHexText.text = $"<b>108-TILE CONTINENT BOARD & DECKS</b>\n" +
                                        $"Board: 90 Forest · 14 Stream · 4 Ruin (0-30 Authority Track)\n" +
                                        $"Relic Deck: {_engine.RelicDeck.Count}/40 (Discard: {_engine.RelicDiscardPile.Count})\n" +
                                        $"Resource Deck: {_engine.ResourceDeck.Count}/50 (Burned: {_engine.BurnedResources.Count})\n" +
                                        $"Forage Deck: 8 (Reshuffled each draw) | Traded Goods: {_engine.TradedGoodsDeck.Count}/8\n\n" +
                                        "Camera: WASD/Arrows or Middle-Mouse Pan, Right-Click Orbit, Scroll Zoom, Space Reset.";
            }

            // 3. Right Panel: Active Player Faction Board & Meters + Log
            var sbFac = new StringBuilder();
            sbFac.AppendLine($"<b>{activePlayer.Faction.Name.ToUpperInvariant()} — FACTION BOARD</b>");
            sbFac.AppendLine(GetFactionMeterSummary(activePlayer));
            sbFac.AppendLine($"<b>Building ({activePlayer.Faction.PrimaryBuilding}):</b> Cost {activePlayer.Faction.PrimaryBuildingCost}");
            sbFac.AppendLine("<b>Species Traits:</b>");
            foreach (var tr in activePlayer.Faction.SpeciesTraits.Take(4))
                sbFac.AppendLine($" • {tr}");
            if (activePlayer.TableRelics.Count > 0)
                sbFac.AppendLine($"<b>Table Relics (In Play):</b> {string.Join(", ", activePlayer.TableRelics.Select(r => r.Name))}");
            _factionBoardText.text = sbFac.ToString();

            var recentLogs = _engine.GameLog.Skip(Mathf.Max(0, _engine.GameLog.Count - 10));
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
                $"  |  <b>Excavation Actions:</b> Unearth:{(_engine.UsedUnearthThisTurn ? "✓" : "○")} Craft:{(_engine.UsedFactionCraftThisTurn ? "✓" : "○")} Assemble:{(_engine.UsedAssembleThisTurn ? "✓" : "○")} Disassemble:{(_engine.UsedDisassembleThisTurn ? "✓" : "○")}";

            foreach (Transform child in _handButtonsRoot)
                Destroy(child.gameObject);

            for (int hIdx = 0; hIdx < activePlayer.RelicHand.Count; hIdx++)
            {
                int capturedIdx = hIdx;
                var card = activePlayer.RelicHand[hIdx];
                CreateRelicHandCardWidget(_handButtonsRoot, card, capturedIdx);
            }
        }

        private void CreateRelicHandCardWidget(Transform parent, RelicCardDef card, int handIdx)
        {
            var cardBox = new GameObject($"RelicCard_{handIdx}");
            cardBox.transform.SetParent(parent, false);
            cardBox.AddComponent<RectTransform>();
            var bg = cardBox.AddComponent<Image>();
            bg.color = new Color(0.20f, 0.18f, 0.14f, 0.98f);

            CreateText(cardBox.transform, "Title", $"<b>{card.Name}</b>\n<size=10>{card.EffectText}</size>",
                11, TextAnchor.UpperLeft, new Vector2(0.04f, 0.36f), new Vector2(0.96f, 0.96f), Vector2.zero, Vector2.zero, Palette.ParchmentMap);

            // Separate [Assemble] button
            CreateButton(cardBox.transform, "BtnAssemble", $"Assemble ({card.Cost})",
                new Vector2(0.03f, 0.04f), new Vector2(0.49f, 0.34f),
                card.HasCraftedEffect ? Palette.FromHex("#2E6B47") : new Color(0.3f, 0.3f, 0.3f, 0.5f), () =>
                {
                    if (!_engine.TryAssembleRelicFromHand(handIdx))
                        _engine.Log($"Cannot Assemble '{card.Name}' (need 2. Excavation Phase, unused Assemble, crafted effect, and {card.Cost}).");
                    RefreshHUD();
                });

            // Separate [Disassemble] button
            CreateButton(cardBox.transform, "BtnDisassemble", $"Disassemble (+{card.DisassembleGain})",
                new Vector2(0.51f, 0.04f), new Vector2(0.97f, 0.34f), Palette.FromHex("#7A4E24"), () =>
                {
                    if (!_engine.TryDisassembleRelicFromHand(handIdx))
                        _engine.Log($"Cannot Disassemble '{card.Name}' (need 2. Excavation Phase and unused Disassemble).");
                    RefreshHUD();
                });
        }

        private string GetFactionMeterSummary(PlayerState p)
        {
            switch (p.Faction.Id)
            {
                case FactionId.CurlyTail:
                    return $"<b>Meters:</b> Thirst for Glory Lv.{p.CurlyTailThirstForGlory}/4 | Hunger Lv.{p.CurlyTailHunger}/4 | Momentum Lv.{p.CurlyTailMomentum}/4";
                case FactionId.HordeOfTheMillions:
                    return $"<b>Forage Deck:</b> 8 Cards (3 Plague, 5 Rummage — reshuffled each draw)";
                case FactionId.PlatypusPapacy:
                    return $"<b>Faith Meter:</b> {p.PlatypusFaith}/5 | <b>Platy-Pope Level:</b> {p.PlatyPopeLevel}/5 | Crusade Turns: {p.CrusadeTurnsRemaining}";
                case FactionId.TroubleMakingTanukis:
                    return $"<b>Fallen Leaves Stack:</b> {p.FallenLeavesStack.Count}/13 tiles remaining";
                case FactionId.Necromouser:
                    return $"<b>Cauldron Dead Units:</b> {p.NecromouserCauldronDeadCount} | Special Summons: {p.NecromouserSpecialSummonsUsed}/4 | Awakened: {p.NecromouserAwakened}";
                case FactionId.FirestartingSquirrels:
                    return $"<b>Crafted Acorn Firestarters:</b> {p.SquirrelsCraftedFirestarters} | Burned Area: {_engine.BurnedResources.Count}";
                case FactionId.CapybaraCaravan:
                    return $"<b>Traded Goods Stall:</b> {_engine.TradedGoodsStall.Count}/3 cards faceup";
                case FactionId.BriaredFlower:
                    return $"<b>Thorn Meter:</b> {p.BriaredFlowerThorns}/6 Thorns ({(p.BriaredFlowerThorns == 0 ? "FRENZIED BLOOM ACTIVE!" : "Thorned Body")})";
                default:
                    return $"<b>Difficulty:</b> {p.Faction.DifficultyRatingOutOf10}/10";
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

            var txt = CreateText(go.transform, "Label", label, 11, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(3f, 2f), new Vector2(-3f, -2f), Color.white);
            txt.raycastTarget = false;
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

            var txt = CreateText(go.transform, "Label", label, 11, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, new Vector2(6f, 2f), new Vector2(-6f, -2f), Color.white);
            txt.raycastTarget = false;
            return btn;
        }
    }
}
