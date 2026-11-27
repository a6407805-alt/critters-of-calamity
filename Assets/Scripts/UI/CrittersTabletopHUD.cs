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
    public partial class CrittersTabletopHUD : MonoBehaviour
    {
        private CrittersGameEngine _engine;
        private Tabletop3DBoardRenderer _boardRenderer;
        private CrittersMultiplayerSessionManager _mpManager;

        private Font _uiFont;
        private Canvas _canvas;
        private GameObject _stallButton;
        private GameObject _activityPanel;
        private GameObject _bottomBar;

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


        private GameObject _stallModal;
        private Transform _stallButtonsRoot;

        private GameObject _battlePopupModal;
        private Text _battlePopupText;

        private GameObject _battleDecisionModal;
        private Text _battleDecisionHeaderText;
        private Text _battleDecisionBodyText;

        // Move action two-click state
        private bool _awaitingMoveDestination = false;
        private HexCoord _moveOriginCoord;


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

            _engine = new CrittersGameEngine(); // empty until a match starts
            _boardRenderer.OnHexClicked += HandleHexClicked;
            _boardRenderer.OnPieceClicked += HandlePieceClicked;
            _mpManager.OnSessionUpdated += HandleSessionUpdated;
            _mpManager.OnMatchStarted += HandleMatchStarted;
            _mpManager.OnCommandApplied += HandleCommandApplied;
            _mpManager.OnLocalNotice += ShowLocalNotice;
            _mpManager.OnReturnedToMenu += HandleReturnedToMenu;

            ShowMainMenu();
        }

        /// <summary>Every computer builds the same match from the same seed; this hooks the HUD and board up to it.</summary>
        private void HandleMatchStarted(MatchStartInfo info)
        {
            ResetActivity();
            _guideMinimized = false;
            BindToEngine(_mpManager.Engine);
            HideMainMenu();
        }

        private void BindToEngine(CrittersGameEngine engine)
        {
            if (_engine != null)
            {
                _engine.OnStateChanged -= RefreshHUD;
                _engine.OnBattleResolved -= ShowBattleReportPopup;
                _engine.OnBattleDecisionRequired -= ShowBattleDecisionModal;
                _engine.OnAnnouncement -= OnEngineAnnouncement;
            }
            _engine = engine;
            _engine.OnStateChanged += RefreshHUD;
            _engine.OnBattleResolved += ShowBattleReportPopup;
            _engine.OnBattleDecisionRequired += ShowBattleDecisionModal;
            _engine.OnAnnouncement += OnEngineAnnouncement;
            _announcements.Clear();

            _awaitingMoveDestination = false;
            _boardRenderer.BindEngine(_engine);
            _boardRenderer.ResetCameraToOverview();
            RefreshHUD();
        }

        private void HandleSessionUpdated()
        {
            RefreshMainMenu();
            RefreshHUD();
        }

        private void HandleReturnedToMenu(string reason)
        {
            if (_battleDecisionModal != null) _battleDecisionModal.SetActive(false);
            if (_battlePopupModal != null) _battlePopupModal.SetActive(false);
            ShowMainMenu();
        }

        /// <summary>A message just for this player (not part of the shared game).</summary>
        private void ShowLocalNotice(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (_engine != null && _engine.Players.Count > 0)
            {
                _engine.Log($"[You] {message}");
                AddActivity($"<color=#E07070>{message}</color>");
                RefreshHUD();
            }
            else
            {
                RefreshMainMenu();
            }
        }

        /// <summary>Tells the player who sent an action when the rules didn't allow it.</summary>
        private void HandleCommandApplied(GameCommand cmd, bool ok)
        {
            bool mine = !_mpManager.IsOnline || cmd.Seat == _mpManager.LocalSeat;
            if (!ok && mine)
            {
                string why;
                switch (cmd.Type)
                {
                    case GameCommandType.Move: why = $"Move from ({cmd.A}, {cmd.B}) to ({cmd.C}, {cmd.D}) isn't legal."; break;
                    case GameCommandType.Loot: why = "Can't Loot there (you need units on the tile, no enemy pieces, and it can't have been looted this turn)."; break;
                    case GameCommandType.Recruit: why = "Can't Recruit right now (Main phase, an action left, and a recruitment building with room)."; break;
                    case GameCommandType.CraftBuilding: why = "Can't craft your building there (Excavation phase, unused Craft action, the cost, and a legal tile)."; break;
                    case GameCommandType.Unearth: why = "Can't Unearth that card (Excavation phase, unused Unearth, and its cost)."; break;
                    case GameCommandType.Assemble: why = "Can't Assemble that card (Excavation phase, unused Assemble, and its cost)."; break;
                    case GameCommandType.Disassemble: why = "Can't Disassemble that card (Excavation phase and unused Disassemble)."; break;
                    case GameCommandType.FactionAction: why = "That faction action can't be used right now (check costs, meters and the selected tile)."; break;
                    case GameCommandType.CraftEquipment: why = $"Can't craft '{cmd.S}' right now (cost and target unit on the board)."; break;
                    case GameCommandType.BuyTradedGood: why = "Can't buy that Traded Good (needs the Capybara Trader nearby and its cost)."; break;
                    default: why = "That action isn't allowed right now."; break;
                }
                _engine.Log($"[You] {why}");
                AddActivity($"<color=#E07070>{why}</color>");
            }
            RecordActivity(cmd, ok);
            if (cmd.Type == GameCommandType.RollDice && ok) PlayDiceRollAnimation(cmd.Seat);
            RefreshHUD();
            if (_stallModal != null && _stallModal.activeSelf) RefreshStallModal();
        }

        private void HandleHexClicked(HexCoord coord)
        {
            _clickUsedByAction = false;
            if (HandleSetupTileClick(coord) || HandleGuideTileClick(coord))
            {
                _clickUsedByAction = true; // don't also open the piece details popup
                RefreshHUD();
                return;
            }
            if (_awaitingMoveDestination)
            {
                _awaitingMoveDestination = false;
                _mpManager.Submit(GameCommand.Move(_moveOriginCoord, coord));
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

            // Layout (1920x1080 reference): a slim top bar; the Turn Guide owns the right-hand column; the board keeps
            // the middle. Your faction board and the table info are drawers on the left (closed by default); a small
            // Activity feed sits bottom-left; your resources and hand are a compact strip along the bottom.

            // 1. TOP BAR: turn and phase, the players' Authority, camera and menu buttons
            var topBar = CreatePanel(canvasGo.transform, "TopBar",
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, 0f), new Vector2(0f, 64f), Palette.UIPanelDark);

            _topStatusText = CreateText(topBar, "TopStatusText", "CRITTERS OF CALAMITY", 14, TextAnchor.MiddleLeft,
                new Vector2(0f, 0f), new Vector2(0.24f, 1f), new Vector2(14f, 0f), new Vector2(-6f, 0f), Palette.AuthorityGold);

            for (int i = 0; i < 4; i++)
            {
                int capturedIdx = i;
                float xMin = 0.245f + i * 0.116f;
                float xMax = xMin + 0.112f;
                var badgeBtn = CreateButton(topBar, $"PlayerBadge_{i}", $"P{i + 1}",
                    new Vector2(xMin, 0.10f), new Vector2(xMax, 0.90f), Palette.UIPanelMid, () => FocusCameraOnPlayer(capturedIdx));
                _playerScoreBadges[i] = badgeBtn.GetComponentInChildren<Text>();
            }

            CreateButton(topBar, "BtnResetCam", "Overview [Space]",
                new Vector2(0.715f, 0.15f), new Vector2(0.785f, 0.85f), Palette.UIButtonBlue, () => _boardRenderer.ResetCameraToOverview());
            _stallButton = CreateButton(topBar, "BtnCapybaraStall", "Trader Stall",
                new Vector2(0.79f, 0.15f), new Vector2(0.86f, 0.85f), Palette.UIButtonTeal, () =>
                {
                    ToggleModal(_stallModal);
                    RefreshStallModal();
                }).gameObject;
            CreateButton(topBar, "BtnRulebook", "Rulebook & Factions",
                new Vector2(0.865f, 0.15f), new Vector2(0.995f, 0.85f), Palette.UIButtonWarm, () => ToggleInfoModal());

            // 2. LEFT DRAWER TABS (just under the top bar)
            CreateButton(canvasGo.transform, "TabMyFaction", "My Faction",
                new Vector2(0.005f, 0.895f), new Vector2(0.075f, 0.932f), Palette.UIPanelMid, () => ToggleDrawer(_rightPanelRt));
            CreateButton(canvasGo.transform, "TabTable", "Table & Decks",
                new Vector2(0.078f, 0.895f), new Vector2(0.148f, 0.932f), Palette.UIPanelMid, () => ToggleDrawer(_leftPanelRt));

            // 2a. Table drawer: Excavation Area, decks and the selected tile
            _leftPanelRt = CreatePanel(canvasGo.transform, "TableDrawer",
                new Vector2(0.005f, 0.36f), new Vector2(0.215f, 0.89f), new Vector2(0f, 0.5f),
                Vector2.zero, Vector2.zero, Palette.UIPanelDark);

            CreateText(_leftPanelRt, "ExcavationHeader", "EXCAVATION AREA", 13, TextAnchor.MiddleCenter,
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

            _selectedHexText = CreateText(_leftPanelRt, "SelectedHexInspector", "", 12, TextAnchor.UpperLeft,
                new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.39f), Vector2.zero, Vector2.zero, Palette.ParchmentMap);

            // 2b. My Faction drawer: meters, Table relics, supply, and the full log
            _rightPanelRt = CreatePanel(canvasGo.transform, "FactionDrawer",
                new Vector2(0.005f, 0.36f), new Vector2(0.215f, 0.89f), new Vector2(0f, 0.5f),
                Vector2.zero, Vector2.zero, Palette.UIPanelDark);

            _factionBoardText = CreateText(_rightPanelRt, "FactionBoardText", "Faction Board", 12, TextAnchor.UpperLeft,
                new Vector2(0.04f, 0.42f), new Vector2(0.96f, 0.98f), Vector2.zero, Vector2.zero, Palette.ParchmentMap);
            _gameLogText = CreateText(_rightPanelRt, "GameLogText", "", 10, TextAnchor.LowerLeft,
                new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.40f), Vector2.zero, Vector2.zero, Palette.UITextMuted);

            _leftPanelRt.gameObject.SetActive(false);
            _rightPanelRt.gameObject.SetActive(false);

            // 3. ACTIVITY FEED: what everyone is doing (always visible, small, bottom-left)
            var activity = CreatePanel(canvasGo.transform, "ActivityFeed",
                new Vector2(0.005f, 0.115f), new Vector2(0.255f, 0.345f), new Vector2(0f, 0f),
                Vector2.zero, Vector2.zero, Palette.UIScrim);
            _activityPanel = activity.gameObject;
            _activityText = CreateText(activity, "ActivityText", "<b>ACTIVITY</b>", 12, TextAnchor.LowerLeft,
                new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.97f), Vector2.zero, Vector2.zero, Palette.ParchmentMap);
            _activityText.verticalOverflow = VerticalWrapMode.Truncate;
            _activityPanel.SetActive(false);

            // 4. BOTTOM STRIP: the viewing player's resources and relic hand (actions live in the Turn Guide)
            var bottomBar = CreatePanel(canvasGo.transform, "BottomBar",
                new Vector2(0f, 0f), new Vector2(0.79f, 0.108f), new Vector2(0.5f, 0f),
                Vector2.zero, Vector2.zero, Palette.UIPanelDark);
            _bottomBar = bottomBar.gameObject;

            _handAndResourcesText = CreateText(bottomBar, "HandAndResourcesText", "", 13, TextAnchor.MiddleLeft,
                new Vector2(0.008f, 0.64f), new Vector2(0.99f, 0.97f), Vector2.zero, Vector2.zero, Palette.ParchmentMap);

            var handRootGo = new GameObject("HandRow");
            handRootGo.transform.SetParent(bottomBar, false);
            var handRt = handRootGo.AddComponent<RectTransform>();
            handRt.anchorMin = new Vector2(0.008f, 0.06f);
            handRt.anchorMax = new Vector2(0.99f, 0.62f);
            handRt.offsetMin = Vector2.zero;
            handRt.offsetMax = Vector2.zero;
            var handLayout = handRootGo.AddComponent<HorizontalLayoutGroup>();
            handLayout.spacing = 6f;
            handLayout.childControlHeight = true;
            handLayout.childControlWidth = false;
            handLayout.childForceExpandHeight = true;
            handLayout.childForceExpandWidth = false;
            _handButtonsRoot = handRootGo.transform;

            // 5. MODALS
            BuildLobbyCodexModal(canvasGo.transform);
            BuildStallModal(canvasGo.transform);
            BuildBattlePopupModal(canvasGo.transform);
            BuildBattleDecisionModal(canvasGo.transform);
            BuildTurnGuide(canvasGo.transform);
            BuildInfoScreens(canvasGo.transform);
            BuildChoicePopup(canvasGo.transform);
            BuildPopups(canvasGo.transform);
            BuildPreGamePanel(canvasGo.transform);
            BuildMainMenu(canvasGo.transform);
        }

        /// <summary>Opens one left drawer (closing the other), or closes it if it was open.</summary>
        private void ToggleDrawer(RectTransform drawer)
        {
            bool open = !drawer.gameObject.activeSelf;
            _leftPanelRt.gameObject.SetActive(false);
            _rightPanelRt.gameObject.SetActive(false);
            drawer.gameObject.SetActive(open);
            if (open) RefreshHUD();
        }

        private void ToggleModal(GameObject targetModal)
        {
            bool nextState = !targetModal.activeSelf;
            if (_lobbyCodexModal != null) _lobbyCodexModal.SetActive(false);
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

            CreateButton(modalRt, "BtnCloseModal", "Close [X]",
                new Vector2(0.92f, 0.925f), new Vector2(0.985f, 0.985f), Palette.FromHex("#7A2828"), () =>
                {
                    _lobbyCodexModal.SetActive(false);
                });

            // 8 Core Faction Picker Buttons on Left of Modal
            var allFactions = RulebookDatabase.GetAllFactions();
            for (int fIdx = 0; fIdx < allFactions.Count; fIdx++)
            {
                var fac = allFactions[fIdx];
                float step = 0.78f / allFactions.Count;
                float yMax = 0.80f - fIdx * step;
                float yMin = yMax - step * 0.9f;
                CreateButton(modalRt, $"BtnFac_{fac.Id}",
                    $"{fac.Name} (Difficulty {fac.DifficultyRatingOutOf10}/10)",
                    new Vector2(0.02f, yMin), new Vector2(0.40f, yMax), fac.PrimaryColor * 0.78f, () =>
                    {
                        RefreshCodexModal(fac);
                    });
            }

            _codexDetailText = CreateText(modalRt, "CodexDetailText", "", 12, TextAnchor.UpperLeft,
                new Vector2(0.42f, 0.03f), new Vector2(0.98f, 0.82f), Vector2.zero, Vector2.zero, Palette.ParchmentMap);

            _lobbyCodexModal.SetActive(false);
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
                        _mpManager.Submit(GameCommand.Make(GameCommandType.BuyTradedGood, slot));
                        RefreshStallModal();
                        RefreshHUD();
                    });
            }
        }

        private void BuildBattlePopupModal(Transform canvasParent)
        {
            // A battle result is a toast under the top bar: it never blocks the board and hides itself
            var modalRt = CreatePanel(canvasParent, "BattleResultToast",
                new Vector2(0.27f, 0.74f), new Vector2(0.73f, 0.925f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, Palette.UIBattle);
            _battlePopupModal = modalRt.gameObject;

            CreateText(modalRt, "Header", "BATTLE RESULT", 14, TextAnchor.MiddleLeft,
                new Vector2(0.03f, 0.80f), new Vector2(0.80f, 0.97f), Vector2.zero, Vector2.zero, Palette.AuthorityGold);
            _battlePopupText = CreateText(modalRt, "Body", "", 13, TextAnchor.UpperLeft,
                new Vector2(0.03f, 0.05f), new Vector2(0.97f, 0.80f), Vector2.zero, Vector2.zero, Palette.ParchmentMap);
            CreateButton(modalRt, "BtnDismissBattle", "Close",
                new Vector2(0.86f, 0.80f), new Vector2(0.985f, 0.97f), Palette.UIPanelMid, () => _battlePopupModal.SetActive(false));

            _battlePopupModal.SetActive(false);
        }

        private GameObject _battleDecisionUse, _battleDecisionPass;
        private Transform _battleDecisionOptions;

        private void BuildBattleDecisionModal(Transform canvasParent)
        {
            var modalRt = CreatePanel(canvasParent, "BattleDecisionModal",
                new Vector2(0.30f, 0.34f), new Vector2(0.70f, 0.70f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, new Color(0.10f, 0.08f, 0.14f, 0.98f));
            _battleDecisionModal = modalRt.gameObject;

            _battleDecisionHeaderText = CreateText(modalRt, "Header", "", 16, TextAnchor.MiddleCenter,
                new Vector2(0.03f, 0.80f), new Vector2(0.97f, 0.97f), Vector2.zero, Vector2.zero, Palette.AuthorityGold);

            _battleDecisionBodyText = CreateText(modalRt, "Body", "", 13, TextAnchor.MiddleLeft,
                new Vector2(0.05f, 0.26f), new Vector2(0.95f, 0.78f), Vector2.zero, Vector2.zero, Color.white);

            _battleDecisionUse = CreateButton(modalRt, "BtnUse", "Use It",
                new Vector2(0.12f, 0.05f), new Vector2(0.46f, 0.20f), Palette.FromHex("#2F7A3E"), () => AnswerBattleDecision(true)).gameObject;
            _battleDecisionPass = CreateButton(modalRt, "BtnPass", "Pass",
                new Vector2(0.54f, 0.05f), new Vector2(0.88f, 0.20f), Palette.FromHex("#7A2828"), () => AnswerBattleDecision(false)).gameObject;
            var optGo = new GameObject("Options");
            optGo.transform.SetParent(modalRt, false);
            var optRt = optGo.AddComponent<RectTransform>();
            optRt.anchorMin = new Vector2(0.04f, 0.04f);
            optRt.anchorMax = new Vector2(0.96f, 0.40f);
            optRt.offsetMin = optRt.offsetMax = Vector2.zero;
            var optGrid = optGo.AddComponent<GridLayoutGroup>();
            optGrid.cellSize = new Vector2(150f, 34f);
            optGrid.spacing = new Vector2(6f, 6f);
            optGrid.childAlignment = TextAnchor.MiddleCenter;
            _battleDecisionOptions = optGo.transform;

            _battleDecisionModal.SetActive(false);
        }

        private void ShowBattleDecisionModal(BattleDecision d)
        {
            if (d == null || _battleDecisionModal == null) return;
            if (_mpManager.IsOnline && d.PlayerIndex != _mpManager.LocalSeat) return; // only the player being asked sees the prompt
            var p = _engine.Players[d.PlayerIndex];
            bool pickOne = d.Options.Count > 0;
            _battleDecisionHeaderText.text = pickOne ? $"{p.DisplayName} ({p.Faction.Name}): {d.Title}" : $"{p.DisplayName} ({p.Faction.Name}): use {d.Title}?";
            _battleDecisionBodyText.text = d.Prompt;
            _battleDecisionUse.SetActive(!pickOne);
            _battleDecisionPass.SetActive(!pickOne);
            var bodyRt = _battleDecisionBodyText.rectTransform;
            bodyRt.anchorMin = new Vector2(0.05f, pickOne ? 0.42f : 0.26f);
            foreach (Transform c in _battleDecisionOptions) Destroy(c.gameObject);
            for (int i = 0; i < d.Options.Count; i++)
            {
                int captured = i;
                var b = CreateLayoutButton(_battleDecisionOptions, $"Opt{i}", d.Options[i], Palette.FromHex("#7A4A28"), () =>
                {
                    _battleDecisionModal.SetActive(false);
                    _mpManager.Submit(GameCommand.BattleOption(captured));
                    RefreshHUD();
                });
                SetFontSize(b.GetComponentInChildren<Text>(), 14);
            }
            _battleDecisionModal.transform.SetAsLastSibling();
            _battleDecisionModal.SetActive(true);
        }

        private void AnswerBattleDecision(bool use)
        {
            // Hide first: answering can immediately raise the next decision, which re-opens this modal.
            _battleDecisionModal.SetActive(false);
            _mpManager.Submit(GameCommand.BattleDecision(use));
            RefreshHUD();
        }

        private void ShowBattleReportPopup(BattleReport r)
        {
            if (r == null || _battlePopupModal == null) return;
            var atk = _engine.Players[r.AttackerPlayerIndex];
            var def = _engine.Players[r.DefenderPlayerIndex];

            var sb = new StringBuilder();
            sb.AppendLine($"<b>{atk.DisplayName}</b> ({atk.Faction.Name}) attacked <b>{def.DisplayName}</b> ({def.Faction.Name}) at {r.TileCoord}.");
            sb.AppendLine($"Dice (3-sided): {r.AttackerDice1}{Bonus(r.AttackerDiceBonus)} vs {r.DefenderDice1}{Bonus(r.DefenderDiceBonus)}.   " +
                          $"Totals: <b>{r.AttackerTotal}</b> (Attack {r.AttackerUnitAttackSum}) vs <b>{r.DefenderTotal}</b> (Defense {r.DefenderUnitDefenseSum}).");
            if (r.IsTie) sb.AppendLine("<b>Tie:</b> each side takes 1 HP.");
            else sb.AppendLine($"<b>{_engine.Players[r.WinnerPlayerIndex].DisplayName} wins</b>: {r.WinnerDamageDealt} HP dealt, {r.MandatoryLoserDamageDealt} HP back from the loser.");
            sb.Append(r.DestroyedUnitNames.Count > 0 ? $"Removed: {string.Join(", ", r.DestroyedUnitNames)}." : "No units removed.");
            _battlePopupText.text = sb.ToString();
            _battlePopupModal.SetActive(true);
            _battlePopupModal.transform.SetAsLastSibling();
            if (_battleToastRoutine != null) StopCoroutine(_battleToastRoutine);
            _battleToastRoutine = StartCoroutine(HideBattleToastLater());
        }

        private Coroutine _battleToastRoutine;
        private System.Collections.IEnumerator HideBattleToastLater()
        {
            yield return new WaitForSeconds(8f);
            if (_battlePopupModal != null) _battlePopupModal.SetActive(false);
        }

        private static string Bonus(int b) => b > 0 ? $" (+{b})" : b < 0 ? $" ({b})" : "";

        private void RefreshCodexModal(FactionDef inspectFaction = null)
        {
            if (_codexDetailText == null) return;
            var allFactions = RulebookDatabase.GetAllFactions();
            if (inspectFaction == null)
                inspectFaction = _engine.Players.Count > 0 ? _engine.Players[_engine.CurrentTurnPlayerIndex].Faction : allFactions[0];

            var sb = new StringBuilder();
            if (_mpManager.IsOnline)
                sb.AppendLine($"<b>ONLINE ROOM:</b> join code <b>{_mpManager.ActiveJoinCode}</b>");
            sb.AppendLine("<b>PLAYERS IN THIS GAME:</b>");
            foreach (var pl in _engine.Players)
                sb.AppendLine($"   {pl.DisplayName}: <b>{pl.Faction.Name}</b> (Difficulty {pl.Faction.DifficultyRatingOutOf10}/10)");
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

            RefreshPreGame();
            RefreshTurnGuide();
            RefreshWinPopup();
            RefreshSetupBanner();
            RefreshChoicePopup();
            RefreshGuideHighlights();
            RefreshCrusadePanel();
            if (_infoModal != null && _infoModal.activeSelf && !_infoShowingRulebook) RefreshInfoModal();

            if (_activityPanel != null) _activityPanel.SetActive(!_engine.IsPreGame);
            if (_bottomBar != null) _bottomBar.SetActive(!_engine.IsPreGame);
            if (_engine.IsPreGame)
            {
                _topStatusText.text = $"<b>CRITTERS OF CALAMITY</b>\nSetting up: {(_engine.CurrentPhase == TurnPhase.PreGame_ReverseFactionSetup ? "Placing starting pieces" : PhaseName(_engine.CurrentPhase))}";
                for (int i = 0; i < 4; i++)
                {
                    bool exists = i < _engine.Players.Count;
                    _playerScoreBadges[i].transform.parent.gameObject.SetActive(exists);
                    if (!exists) continue;
                    var pp = _engine.Players[i];
                    _playerScoreBadges[i].text = $"<b>{pp.DisplayName}</b>\n{(pp.Faction != null ? pp.Faction.Name : "No faction yet")}";
                    _playerScoreBadges[i].color = Color.white;
                }
                return;
            }

            if (_stallButton != null) _stallButton.SetActive(_engine.Players.Any(p => p.Faction != null && p.Faction.Id == FactionId.CapybaraCaravan));

            var activePlayer = _engine.Players[_engine.CurrentTurnPlayerIndex];
            int viewSeat = Mathf.Clamp(_mpManager.ViewingSeat, 0, _engine.Players.Count - 1);
            var viewer = _engine.Players[viewSeat]; // whose hand and Resource Pile this screen shows

            // 1. Top Bar
            string moveHint = _awaitingMoveDestination ? $" | [CLICK DESTINATION HEX FROM {_moveOriginCoord}]" : "";
            if (_engine.IsBattlePending && _engine.PendingBattle.CurrentDecision != null)
                moveHint += $" | [BATTLE PAUSED: waiting on {_engine.Players[_engine.PendingBattle.CurrentDecision.PlayerIndex].DisplayName}]";
            if (_mpManager.IsOnline)
                moveHint += $" | You: {viewer.DisplayName} ({viewer.Faction.Name})" + (_mpManager.OutOfSync ? " | OUT OF SYNC" : "");
            _topStatusText.text = $"<b>ROUND {_engine.RoundNumber}, TURN {_engine.TurnNumber}: {activePlayer.DisplayName} ({activePlayer.Faction.Name})</b>\n" +
                                  $"{PhaseName(_engine.CurrentPhase)}" + (_engine.CurrentPhase == TurnPhase.MainPhase ? $" ({_engine.MainPhaseActionsRemaining}/3 actions left)" : "") + moveHint;

            for (int i = 0; i < 4; i++)
                _playerScoreBadges[i].transform.parent.gameObject.SetActive(i < _engine.Players.Count);
            for (int i = 0; i < _engine.Players.Count && i < 4; i++)
            {
                var p = _engine.Players[i];
                bool isTurn = (i == _engine.CurrentTurnPlayerIndex);
                string turnMark = isTurn ? "> " : "";
                string facColor = ColorUtility.ToHtmlStringRGB(p.Faction.PrimaryColor);
                _playerScoreBadges[i].text = $"{turnMark}<b>{p.DisplayName}</b> <color=#{facColor}>{p.Faction.Name}</color>\n" +
                                             $"<size=16><b>{p.Authority}</b>/30 Authority</size>  <size=12>Res {p.ResourcePile.Count} · Hand {p.RelicHand.Count}</size>";
                _playerScoreBadges[i].color = isTurn ? Palette.AuthorityGold : Color.white;
            }

            // 2. Left Panel: Excavation Area (5 faceup Relic cards)
            foreach (Transform child in _excavationButtonsRoot)
                Destroy(child.gameObject);

            for (int i = 0; i < _engine.ExcavationArea.Count; i++)
            {
                int slotIdx = i;
                var card = _engine.ExcavationArea[i];
                var excBtn = CreateLayoutButton(_excavationButtonsRoot, $"ExcSlot_{i}",
                    $"<b>[Unearth] {card.Name}</b> (Cost: {card.Cost})\n{card.EffectText}",
                    new Color(0.20f, 0.25f, 0.32f, 0.95f), () =>
                    {
                        _mpManager.Submit(GameCommand.Make(GameCommandType.Unearth, slotIdx));
                        RefreshHUD();
                    });
                var excLabel = excBtn.GetComponentInChildren<Text>();
                SetFontSize(excLabel, 15);
                excLabel.resizeTextMinSize = 11;
                AttachCardHover(excBtn.gameObject, card);   // hovering shows the full card
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
                                        $"Board: 90 Forest (green) · 14 Stream (light blue) · 4 Ruin (gray)\n" +
                                        $"Relic Deck: {_engine.RelicDeck.Count}/40 (Discard: {_engine.RelicDiscardPile.Count})\n" +
                                        $"Resource Deck: {_engine.ResourceDeck.Count}/50 (Burned: {_engine.BurnedResources.Count})\n" +
                                        $"Forage Deck: 8 (Reshuffled each draw) | Traded Goods: {_engine.TradedGoodsDeck.Count}/8\n\n" +
                                        "Camera: WASD/Arrows or Middle-Mouse Pan, Right-Click Orbit, Scroll Zoom, Space Reset.";
            }

            // 3. Right Panel: YOUR faction board (meters, traits, Table relics, supply) + Activity + Log
            var sbFac = new StringBuilder();
            sbFac.AppendLine($"<b>{(_mpManager.IsOnline ? "YOUR FACTION" : viewer.DisplayName.ToUpperInvariant())}: {viewer.Faction.Name.ToUpperInvariant()}</b>");
            sbFac.AppendLine(GetFactionMeterSummary(viewer));
            if (viewer.TableRelics.Count > 0)
                sbFac.AppendLine($"<b>Table relics (facedown):</b> {string.Join(", ", viewer.TableRelics.Select(r => r.Name))}");
            sbFac.AppendLine("<b>Supply:</b>");
            sbFac.Append(SupplySummary(viewer));
            _factionBoardText.text = sbFac.ToString();

            var recentLogs = _engine.GameLog.Skip(Mathf.Max(0, _engine.GameLog.Count - 10));
            _gameLogText.text = "<b>TABLETOP EVENT & BATTLE LOG:</b>\n" + string.Join("\n", recentLogs);

            // 4. Bottom Bar: Active Player's Resource Pile & Relic Hand
            int wCount = viewer.ResourcePile.Count(r => r == ResourceType.Wood);
            int sCount = viewer.ResourcePile.Count(r => r == ResourceType.Stone);
            int gCount = viewer.ResourcePile.Count(r => r == ResourceType.Gold);
            int cCount = viewer.ResourcePile.Count(r => r == ResourceType.Charcoal);
            _handAndResourcesText.text =
                $"<b>{(_mpManager.IsOnline ? "You" : viewer.DisplayName)}</b>   " +
                $"<color=#{ColorUtility.ToHtmlStringRGB(Palette.WoodPlank)}>Wood</color> <b>{wCount}</b>    " +
                $"<color=#{ColorUtility.ToHtmlStringRGB(Palette.RuinStoneLight)}>Stone</color> <b>{sCount}</b>    " +
                $"<color=#{ColorUtility.ToHtmlStringRGB(Palette.AuthorityGold)}>Gold</color> <b>{gCount}</b>" +
                (cCount > 0 || viewer.Faction.Id == FactionId.FirestartingSquirrels ? $"    <color=#{ColorUtility.ToHtmlStringRGB(Palette.FireTile)}>Charcoal</color> <b>{cCount}</b>" : "") +
                $"      <size=14>Hand: {viewer.RelicHand.Count} card(s)" + (viewer.PurchasedTradedGoods.Count > 0 ? $"   Traded Goods: {string.Join(", ", viewer.PurchasedTradedGoods.Select(g => g.Title))}" : "") + "</size>";

            foreach (Transform child in _handButtonsRoot)
                Destroy(child.gameObject);
            HideCardPreview();

            for (int hIdx = 0; hIdx < viewer.RelicHand.Count; hIdx++)
            {
                int capturedIdx = hIdx;
                var card = viewer.RelicHand[hIdx];
                CreateRelicHandCardWidget(_handButtonsRoot, card, capturedIdx);
            }
            // Crafted cards sit on your Table: they show here next to your Hand, marked CRAFTED.
            for (int tIdx = 0; tIdx < viewer.TableRelics.Count; tIdx++)
                CreateRelicHandCardWidget(_handButtonsRoot, viewer.TableRelics[tIdx], 100 + tIdx, crafted: true);
            for (int fIdx = 0; fIdx < viewer.SquirrelsCraftedFirestarters; fIdx++)
                CreateRelicHandCardWidget(_handButtonsRoot, AcornFirestarterCard, 200 + fIdx, crafted: true);
        }

        /// <summary>The Firestarting Squirrels' Acorn Firestarter, shown as a crafted card.</summary>
        private static readonly RelicCardDef AcornFirestarterCard = new RelicCardDef
        {
            Id = "acorn_firestarter", Name = "Acorn Firestarter", Cost = new ResourceCost(wood: 1, charcoal: 1),
            DisassembleGain = new ResourceCost(),
            EffectText = "Crafted card (Firestarting Squirrels). Once per Main phase, discard it to place a Fire Tile on a forest tile with a Squirrel."
        };

        /// <summary>A compact hand card: name, cost and effect. (Assemble and Disassemble are in the Turn Guide.)</summary>
        private void CreateRelicHandCardWidget(Transform parent, RelicCardDef card, int handIdx, bool crafted = false)
        {
            var cardBox = new GameObject($"RelicCard_{handIdx}");
            cardBox.transform.SetParent(parent, false);
            cardBox.AddComponent<RectTransform>();
            cardBox.AddComponent<LayoutElement>().preferredWidth = 230f;
            var bg = cardBox.AddComponent<Image>();
            bg.color = crafted ? new Color(0.16f, 0.30f, 0.18f, 0.97f) : Palette.UICard;
            string tag = crafted ? "<color=#9BE07A><b>CRAFTED</b></color>  " : "";
            var t = CreateText(cardBox.transform, "Text", $"{tag}<b>{card.Name}</b>  <size=14>({card.Cost})</size>\n<size=14>{card.EffectText}</size>",
                14, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(6f, 3f), new Vector2(-6f, -3f), Palette.ParchmentMap);
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;

            // Hovering a card pops up the full card above the hand
            var trigger = cardBox.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => ShowCardPreview(card, cardBox.GetComponent<RectTransform>(), crafted));
            trigger.triggers.Add(enter);
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => HideCardPreview());
            trigger.triggers.Add(exit);
        }

        /// <summary>Hovering this UI element pops up the full relic card (name, cost, effect, disassemble).</summary>
        private void AttachCardHover(GameObject go, RelicCardDef card)
        {
            var trig = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => ShowCardPreview(card, go.GetComponent<RectTransform>()));
            trig.triggers.Add(enter);
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => HideCardPreview());
            trig.triggers.Add(exit);
        }

        private GameObject _cardPreview;
        private Text _cardPreviewText;

        private void ShowCardPreview(RelicCardDef card, RectTransform anchor, bool crafted = false)
        {
            if (_cardPreview == null)
            {
                var rt = CreatePanel(_canvas.transform, "CardPreview", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0.5f, 0f),
                    Vector2.zero, new Vector2(340f, 250f), Palette.UICard);
                _cardPreview = rt.gameObject;
                var outline = _cardPreview.AddComponent<Outline>();
                outline.effectColor = Palette.AuthorityGold;
                outline.effectDistance = new Vector2(2f, -2f);
                _cardPreviewText = CreateText(rt, "Text", "", 15, TextAnchor.UpperLeft, Vector2.zero, Vector2.one,
                    new Vector2(14f, 12f), new Vector2(-14f, -12f), Palette.ParchmentMap);
                _cardPreviewText.raycastTarget = false;
                _cardPreview.GetComponent<Image>().raycastTarget = false;
            }
            var sb = new StringBuilder();
            sb.AppendLine($"<size=20><b>{card.Name}</b></size>" + (crafted ? "   <color=#9BE07A>CRAFTED (on your Table)</color>" : ""));
            sb.AppendLine($"<color=#{ColorUtility.ToHtmlStringRGB(Palette.AuthorityGold)}>Cost: {card.Cost}</color>");
            sb.AppendLine();
            sb.AppendLine(card.EffectText);
            sb.AppendLine();
            if (card.Id != "acorn_firestarter")
                sb.Append(card.DisassembleChoiceWoodOrStone ? "<b>Disassemble:</b> gain 1 Wood or 1 Stone (your choice)" : $"<b>Disassemble:</b> gain {card.DisassembleGain}");
            _cardPreviewText.text = sb.ToString();

            // place it just above the hovered card, kept on screen
            var prt = (RectTransform)_cardPreview.transform;
            var canvasRt = (RectTransform)_canvas.transform;
            var corners = new Vector3[4];
            anchor.GetWorldCorners(corners);
            Vector2 topMid = canvasRt.InverseTransformPoint((corners[1] + corners[2]) * 0.5f);
            float halfW = canvasRt.rect.width * 0.5f;
            float x = Mathf.Clamp(topMid.x, -halfW + 180f, halfW - 180f) + halfW;
            float y = topMid.y + canvasRt.rect.height * 0.5f + 10f;
            prt.anchoredPosition = new Vector2(x, y);
            _cardPreview.SetActive(true);
            _cardPreview.transform.SetAsLastSibling();
        }

        private void HideCardPreview()
        {
            if (_cardPreview != null) _cardPreview.SetActive(false);
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
                case FactionId.GoatCabal:
                {
                    var demons = string.Join(" | ", CrittersGameEngine.DemonIds.Select(id =>
                    {
                        string name = p.Faction.Units.First(u => u.Id == id).Name;
                        int st = _engine.DemonState(p, id);
                        string state = st == CrittersGameEngine.DemonBound ? "<color=#D2462F>BOUND</color>"
                                     : st == CrittersGameEngine.DemonUnbound ? "unbound (rebind: Purged only)"
                                     : st == CrittersGameEngine.DemonRemoved ? "removed" : "face down";
                        return $"{name}: {state}";
                    }));
                    return $"<b>Purgatory:</b> {p.GoatPurged} Purged | <b>Grimoire:</b> {p.Grimoire.Count} left, {p.GrimoireDiscard.Count} milled" +
                           (string.IsNullOrEmpty(p.LastGrimoireText) ? "" : $"\n<b>Last milled:</b> {p.LastGrimoireText}") +
                           $"\n<b>Demons:</b> {demons}";
                }
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
            // Text runs a couple of points larger than its box was designed for; if it doesn't fit, it shrinks back.
            txt.fontSize = fontSize + FontBump;
            txt.resizeTextForBestFit = true;
            txt.resizeTextMaxSize = fontSize + FontBump;
            txt.resizeTextMinSize = Mathf.Max(8, fontSize - 2);
            txt.alignment = align;
            txt.color = color;
            txt.supportRichText = true;
            return txt;
        }

        private const int FontBump = 2;

        /// <summary>Sets a text's size (plus the same bump CreateText gives) and keeps its shrink-to-fit range in step.</summary>
        private static void SetFontSize(Text t, int size)
        {
            if (t == null) return;
            t.fontSize = size + FontBump;
            t.resizeTextMaxSize = size + FontBump;
            t.resizeTextMinSize = Mathf.Max(8, size - 2);
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
