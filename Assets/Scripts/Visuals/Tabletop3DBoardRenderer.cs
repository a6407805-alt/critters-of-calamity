using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using CrittersOfCalamity.Model;
using CrittersOfCalamity.Engine;

namespace CrittersOfCalamity.Visuals
{
    /// <summary>
    /// Procedural 3D Tabletop Hexagonal Board Renderer using URP Lit materials from Palette.cs.
    /// Renders the canonical 108-Tile Continent Map (90 Forest, 14 Stream, 4 Ruin - Part 2 Line 43)
    /// with water surrounding the island and a wooden rim around the edge,
    /// Forest pine groves, Anankian ruin pillars, light-blue Stream channels, Unique Faction Overlay Tiles
    /// (Fallen Leaves, Fire, Miasma, Capybara Stream), Buildings, Tokens, and stylized 3D Critter Miniatures.
    /// </summary>
    public class Tabletop3DBoardRenderer : MonoBehaviour
    {
        public float HexRadius = 1.9f;   // big enough for 12 units, 2 tokens and a building on one tile
        public float TileThickness = 0.25f;

        public HexCoord? SelectedCoord = null;
        public event Action<HexCoord> OnHexClicked;
        /// <summary>Raised after OnHexClicked when the click landed on a piece (unit, building, token or faction tile).</summary>
        public event Action<PieceRef> OnPieceClicked;
        /// <summary>The piece under the mouse right now (null if none or the mouse is over the HUD).</summary>
        public PieceRef HoveredPiece { get; private set; }

        private Transform _highlightRoot;

        private CrittersGameEngine _engine;
        private Transform _boardRoot;
        private Transform _piecesRoot;
        private Mesh _hexCylinderMesh;
        private Material _baseUrpMat;
        private Dictionary<Color, Material> _matCache = new Dictionary<Color, Material>();
        private Dictionary<HexCoord, GameObject> _tileObjects = new Dictionary<HexCoord, GameObject>();
        private Dictionary<HexCoord, GameObject> _tileDecos = new Dictionary<HexCoord, GameObject>();
        private GameObject _selectionRing;

        // Camera orbit / pan state sized for the 108-tile Continent board
        private float _camYaw = 0f;
        private float _camPitch = 52f;
        private float _camDistance = 34f;
        private Vector3 _camPivot = new Vector3(6f, 0f, 0f);
        /// <summary>The overview looks a little right of center so the board sits in the space left of the Turn Guide column.</summary>
        private Vector3 OverviewPivot => new Vector3(3.6f * K, 0f, 0f);

        public void BindEngine(CrittersGameEngine engine)
        {
            if (_engine != null)
                _engine.OnStateChanged -= RebuildPieces;

            _engine = engine;
            _engine.OnStateChanged += RebuildPieces;

            EnsureHexMeshAndBaseMaterial();
            BuildStaticTableAndHexGrid();
            RebuildPieces();
            ResetCameraToOverview();
        }

        public void FocusCameraOnCoord(HexCoord coord, float targetDistance = 13f)
        {
            _camPivot = coord.ToWorldPosition(HexRadius);
            _camDistance = Mathf.Clamp(targetDistance * K, 6f * K, 38f * K);
        }

        public void ResetCameraToOverview()
        {
            _camPivot = OverviewPivot;
            _camPitch = 53f;
            _camDistance = 21f * K;
        }

        void Update()
        {
            HandleCameraControls();
            UpdateHover();
            HandleMouseHexSelection();
        }

        private void UpdateHover()
        {
            HoveredPiece = null;
            var mouse = Mouse.current;
            var cam = Camera.main;
            if (mouse == null || cam == null) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, 150f))
                HoveredPiece = hit.collider.GetComponentInParent<PieceRef>();
        }

        /// <summary>Marks tiles on the board (for example, legal setup tiles). Pass null to clear.</summary>
        public void SetHighlights(IEnumerable<HexCoord> legal, Color legalColor, IEnumerable<HexCoord> picked, Color pickedColor)
        {
            if (_highlightRoot != null) Destroy(_highlightRoot.gameObject);
            _highlightRoot = null;
            if (_engine == null || (legal == null && picked == null)) return;

            _highlightRoot = new GameObject("TileHighlights").transform;
            _highlightRoot.SetParent(transform, false);
            var mesh = CreateHexPrismMesh(HexRadius * 0.80f, 0.02f);
            void Mark(HexCoord c, Color col)
            {
                if (!_engine.Board.ContainsKey(c)) return;
                var go = new GameObject($"Highlight_{c}");
                go.transform.SetParent(_highlightRoot, false);
                go.transform.localPosition = c.ToWorldPosition(HexRadius) + new Vector3(0f, TileThickness + 0.08f, 0f);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = GetMaterial(col, 0.2f);
            }
            if (legal != null) foreach (var c in legal) Mark(c, legalColor);
            if (picked != null) foreach (var c in picked) Mark(c, pickedColor);
        }

        private static void TagPiece(GameObject go, PieceKind kind, HexCoord coord, Vector3 colliderCenter, Vector3 colliderSize,
            UnitInstance unit = null, BuildingInstance building = null, TokenInstance token = null)
        {
            var box = go.AddComponent<BoxCollider>();
            box.center = colliderCenter;
            box.size = colliderSize;
            var tag = go.AddComponent<PieceRef>();
            tag.Kind = kind;
            tag.Coord = coord;
            tag.Unit = unit;
            tag.Building = building;
            tag.Token = token;
        }

        private void HandleCameraControls()
        {
            var cam = Camera.main;
            if (cam == null) return;

            var kb = Keyboard.current;
            var mouse = Mouse.current;

            float dt = Time.deltaTime;
            if (kb != null)
            {
                Vector3 forward = Quaternion.Euler(0f, _camYaw, 0f) * Vector3.forward;
                Vector3 right = Quaternion.Euler(0f, _camYaw, 0f) * Vector3.right;
                float panSpeed = Mathf.Lerp(9f, 20f, (_camDistance / K - 6f) / 32f) * K;

                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) _camPivot += forward * (panSpeed * dt);
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) _camPivot -= forward * (panSpeed * dt);
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) _camPivot += right * (panSpeed * dt);
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) _camPivot -= right * (panSpeed * dt);
                if (kb.qKey.isPressed) _camYaw -= 68f * dt;
                if (kb.eKey.isPressed) _camYaw += 68f * dt;
                if (kb.spaceKey.wasPressedThisFrame) ResetCameraToOverview();
            }

            if (mouse != null)
            {
                if (mouse.rightButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    _camYaw += delta.x * 0.25f;
                    _camPitch = Mathf.Clamp(_camPitch - delta.y * 0.2f, 22f, 82f);
                }
                else if (mouse.middleButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    Vector3 forward = Quaternion.Euler(0f, _camYaw, 0f) * Vector3.forward;
                    Vector3 right = Quaternion.Euler(0f, _camYaw, 0f) * Vector3.right;
                    _camPivot -= (right * delta.x + forward * delta.y) * 0.03f * K;
                }

                bool pointerOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                if (!pointerOverUi)
                {
                    float scroll = mouse.scroll.ReadValue().y;
                    if (Mathf.Abs(scroll) > 0.01f)
                    {
                        _camDistance = Mathf.Clamp(_camDistance - Mathf.Sign(scroll) * 1.6f * K, 6f * K, 38f * K);
                    }
                }
            }

            _camPivot.x = Mathf.Clamp(_camPivot.x, -18f * K, 18f * K);
            _camPivot.z = Mathf.Clamp(_camPivot.z, -18f * K, 18f * K);

            Quaternion rot = Quaternion.Euler(_camPitch, _camYaw, 0f);
            cam.transform.position = _camPivot - (rot * Vector3.forward * _camDistance);
            cam.transform.rotation = rot;
        }

        private void HandleMouseHexSelection()
        {
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            var cam = Camera.main;
            if (cam == null) return;

            Vector2 mPos = mouse.position.ReadValue();
            Ray ray = cam.ScreenPointToRay(mPos);
            if (Physics.Raycast(ray, out RaycastHit hit, 150f))
            {
                // A click on a piece counts as a click on its tile, then reports the piece.
                var piece = hit.collider.GetComponentInParent<PieceRef>();
                if (piece != null)
                {
                    SelectHex(piece.Coord);
                    OnPieceClicked?.Invoke(piece);
                    return;
                }
                var relay = hit.collider.GetComponentInParent<HexTileClickRelay>();
                if (relay != null)
                {
                    SelectHex(relay.Coord);
                }
            }
        }

        public void SelectHex(HexCoord coord)
        {
            SelectedCoord = coord;
            if (_selectionRing != null && _engine != null && _engine.Board.ContainsKey(coord))
            {
                _selectionRing.SetActive(true);
                _selectionRing.transform.position = coord.ToWorldPosition(HexRadius) + new Vector3(0f, TileThickness + 0.05f, 0f);
            }
            OnHexClicked?.Invoke(coord);
        }

        private void EnsureHexMeshAndBaseMaterial()
        {
            if (_hexCylinderMesh == null)
            {
                _hexCylinderMesh = CreateHexPrismMesh(HexRadius * 0.96f, TileThickness);
            }
            if (_baseUrpMat == null)
            {
                var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
                Shader shader = rp != null && rp.defaultMaterial != null
                    ? rp.defaultMaterial.shader
                    : Shader.Find("Universal Render Pipeline/Lit");
                _baseUrpMat = new Material(shader);
                _baseUrpMat.SetFloat("_Smoothness", 0.35f);
            }
        }

        public Material GetMaterial(Color color, float smoothness = 0.35f)
        {
            EnsureHexMeshAndBaseMaterial();
            if (!_matCache.TryGetValue(color, out var mat))
            {
                mat = new Material(_baseUrpMat);
                mat.color = color;
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Smoothness"))
                    mat.SetFloat("_Smoothness", smoothness);
                _matCache[color] = mat;
            }
            return mat;
        }

        /// <summary>How much bigger tiles are than the original 1.15 board (environment and camera scale with it).</summary>
        private float K => HexRadius / 1.15f;

        private void BuildStaticTableAndHexGrid()
        {
            if (_boardRoot != null)
                Destroy(_boardRoot.gameObject);

            _boardRoot = new GameObject("TabletopBoard3D").transform;
            _boardRoot.SetParent(transform, false);
            _tileObjects.Clear();
            _tileDecos.Clear();
            CritterModels.Mat = GetMaterial;

            // 1. Carved oak tabletop, brown border rim and the dark blue water ring around the Continent
            var table = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(table.GetComponent<Collider>());
            table.name = "AnankianOakTable";
            table.transform.SetParent(_boardRoot, false);
            table.transform.localPosition = new Vector3(0f, -0.38f, 0f);
            table.transform.localScale = new Vector3(36f * K, 0.25f, 36f * K);
            table.GetComponent<Renderer>().sharedMaterial = GetMaterial(Palette.TableOak, 0.4f);

            var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(rim.GetComponent<Collider>());
            rim.name = "BoardBorder";
            rim.transform.SetParent(_boardRoot, false);
            rim.transform.localPosition = new Vector3(0f, -0.10f, 0f);
            rim.transform.localScale = new Vector3(28.5f * K, 0.06f, 28.5f * K);
            rim.GetComponent<Renderer>().sharedMaterial = GetMaterial(Palette.ParchmentMap, 0.3f);

            var oceanWater = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(oceanWater.GetComponent<Collider>());
            oceanWater.name = "SurroundingWater";
            oceanWater.transform.SetParent(_boardRoot, false);
            oceanWater.transform.localPosition = new Vector3(0f, -0.04f, 0f);
            oceanWater.transform.localScale = new Vector3(26.2f * K, 0.05f, 26.2f * K);
            oceanWater.GetComponent<Renderer>().sharedMaterial = GetMaterial(Palette.OceanDeep, 0.8f);

            // 2. Selection ring
            _selectionRing = new GameObject("HexSelectionRing");
            _selectionRing.transform.SetParent(_boardRoot, false);
            _selectionRing.AddComponent<MeshFilter>().sharedMesh = CreateHexPrismMesh(HexRadius * 1.02f, 0.055f);
            _selectionRing.AddComponent<MeshRenderer>().sharedMaterial = GetMaterial(Palette.AuthorityGold, 0.8f);
            _selectionRing.SetActive(false);

            // 3. The 108 tiles. Terrain props sit in a "Deco" child so a faction tile can replace the whole look.
            foreach (var kvp in _engine.Board)
            {
                var coord = kvp.Key;
                var tileState = kvp.Value;

                var hexGo = new GameObject($"Hex_{coord.q}_{coord.r}");
                hexGo.transform.SetParent(_boardRoot, false);
                hexGo.transform.localPosition = coord.ToWorldPosition(HexRadius);

                var relay = hexGo.AddComponent<HexTileClickRelay>();
                relay.Coord = coord;
                relay.BoardRenderer = this;
                hexGo.AddComponent<MeshFilter>().sharedMesh = _hexCylinderMesh;
                hexGo.AddComponent<MeshRenderer>().sharedMaterial = GetTerrainMaterial(tileState.BaseTerrain);
                hexGo.AddComponent<MeshCollider>().sharedMesh = _hexCylinderMesh;

                var deco = new GameObject("Deco").transform;
                deco.SetParent(hexGo.transform, false);
                CreateTerrainDecorations(deco, tileState.BaseTerrain);
                _tileObjects[coord] = hexGo;
                _tileDecos[coord] = deco.gameObject;
            }
        }

        // Tile layout (pointy-top hex, tips toward +Z/back and -Z/front, camera looks from the front):
        // building at the back tip, two token spots either side of it, units in faction blocks across the front,
        // terrain props in the left/right back corners. Room for 12 units (two full factions), 2 tokens and a building.
        private Vector3 BuildingSpot => new Vector3(0f, 0f, 0.52f * K);
        private Vector3 TokenSpot(int i) => new Vector3((i == 0 ? -0.52f : 0.52f) * K, 0f, 0.42f * K);
        private Vector3[] PropSpots => new[] { new Vector3(-0.45f * K, 0f, 0.70f * K), new Vector3(0.45f * K, 0f, 0.70f * K) };
        // unit grid: 4 columns x 3 rows across the front of the tile (12 spots)
        private static readonly float[] UnitCols = { -0.57f, -0.19f, 0.19f, 0.57f };
        private static readonly float[] UnitRows = { 0.08f, -0.26f, -0.58f };
        private const float UnitScaleK = 0.85f;

        private Transform Prop(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Vector3 euler, Color c, float smooth = 0.35f)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.GetComponent<Renderer>().sharedMaterial = GetMaterial(c, smooth);
            return go.transform;
        }

        private void CreateTerrainDecorations(Transform parent, TileTerrainType terrain)
        {
            float top = TileThickness;
            int i = 0;
            foreach (var spot in PropSpots)
            {
                var holder = new GameObject("Props").transform;
                holder.SetParent(parent, false);
                holder.localPosition = spot + new Vector3(0f, top, 0f);
                holder.localScale = Vector3.one * K * 0.9f;
                var at = Vector3.zero;
                if (terrain == TileTerrainType.Forest)
                {
                    // small pine: trunk + two tapered canopy tiers, tilted a few degrees
                    float tilt = i == 0 ? 4f : -5f;
                    Prop(holder, PrimitiveType.Cylinder, at + new Vector3(0f, 0.08f, 0f), new Vector3(0.07f, 0.08f, 0.07f), new Vector3(0f, 0f, tilt), Palette.TableOak);
                    Prop(holder, PrimitiveType.Sphere, at + new Vector3(0f, 0.24f, 0f), new Vector3(0.26f, 0.22f, 0.26f), new Vector3(0f, 0f, tilt), Palette.CanopyGreen);
                    Prop(holder, PrimitiveType.Sphere, at + new Vector3(0f, 0.38f, 0f), new Vector3(0.17f, 0.18f, 0.17f), new Vector3(0f, 0f, tilt), Palette.CanopyLight);
                }
                else if (terrain == TileTerrainType.Ruin)
                {
                    if (i == 0)
                        Prop(holder, PrimitiveType.Cylinder, at + new Vector3(0f, 0.16f, 0f), new Vector3(0.14f, 0.16f, 0.14f), new Vector3(6f, 15f, -5f), Palette.RuinStoneLight);
                    else
                        Prop(holder, PrimitiveType.Cube, at + new Vector3(0f, 0.07f, 0f), new Vector3(0.24f, 0.12f, 0.12f), new Vector3(0f, 25f, 8f), Palette.RuinStoneDark);
                }
                else if (terrain == TileTerrainType.Stream)
                {
                    Prop(holder, PrimitiveType.Cylinder, at + new Vector3(0f, 0.01f, 0f), new Vector3(0.18f, 0.008f, 0.18f), Vector3.zero, Palette.LilyPad, 0.6f);
                    Prop(holder, PrimitiveType.Cube, at + new Vector3(0.12f, 0.09f, 0.05f), new Vector3(0.02f, 0.18f, 0.02f), new Vector3(0f, 0f, 8f), Palette.LilyPad);
                }
                i++;
            }
        }

        /// <summary>A faction tile replaces the tile it covers (Part 4): new top color, the old props hidden, its own props shown.</summary>
        private void ApplyOverlayLook(HexTileState tile)
        {
            if (!_tileObjects.TryGetValue(tile.Coord, out var hexGo)) return;
            bool overlay = tile.Overlay != FactionOverlayTileType.None;
            hexGo.GetComponent<MeshRenderer>().sharedMaterial = overlay ? GetOverlayMaterial(tile.Overlay) : GetTerrainMaterial(tile.BaseTerrain);
            if (_tileDecos.TryGetValue(tile.Coord, out var deco))
                deco.SetActive(!overlay || tile.Overlay == FactionOverlayTileType.Miasma_Hidden);
            if (!overlay) return;

            var root = new GameObject($"FactionTile_{tile.Overlay}").transform;
            root.SetParent(_piecesRoot, false);
            root.localPosition = tile.Coord.ToWorldPosition(HexRadius) + new Vector3(0f, TileThickness, 0f);
            float top = 0f;
            int n = 0;
            foreach (var spot in PropSpots)
            {
                var holder = new GameObject("Props").transform;
                holder.SetParent(root, false);
                holder.localPosition = spot + new Vector3(0f, top, 0f);
                holder.localScale = Vector3.one * K * 0.9f;
                var at = Vector3.zero;
                switch (tile.Overlay)
                {
                    case FactionOverlayTileType.Fire:
                        Prop(holder, PrimitiveType.Sphere, at + new Vector3(0f, 0.10f, 0f), new Vector3(0.20f, 0.22f, 0.20f), Vector3.zero, Palette.FireTile, 0.6f);
                        Prop(holder, PrimitiveType.Sphere, at + new Vector3(0f, 0.24f, 0f), new Vector3(0.13f, 0.20f, 0.13f), Vector3.zero, Palette.CarrotOrange, 0.6f);
                        Prop(holder, PrimitiveType.Sphere, at + new Vector3(0f, 0.36f, 0f), new Vector3(0.07f, 0.14f, 0.07f), Vector3.zero, Palette.EmberYellow, 0.7f);
                        Prop(holder, PrimitiveType.Cylinder, at + new Vector3(0.1f, 0.04f, 0.1f), new Vector3(0.05f, 0.08f, 0.05f), new Vector3(70f, 30f, 0f), Palette.InkAccent);
                        break;
                    case FactionOverlayTileType.FallenLeaf_Unflipped:
                    case FactionOverlayTileType.FallenLeaf_Prank:
                    case FactionOverlayTileType.FallenLeaf_Trap:
                        for (int l = 0; l < 3; l++)
                            Prop(holder, PrimitiveType.Cube, at + new Vector3((l - 1) * 0.12f, 0.02f + l * 0.01f, (l % 2) * 0.09f),
                                 new Vector3(0.14f, 0.01f, 0.08f), new Vector3(0f, n * 70f + l * 47f, l * 6f), l == 1 ? Palette.LeafRust : Palette.FallenLeafTile);
                        if (tile.Overlay == FactionOverlayTileType.FallenLeaf_Trap && n == 0)
                            for (int j = 0; j < 4; j++) // snapped trap jaws
                                Prop(holder, PrimitiveType.Cube, at + new Vector3((j - 1.5f) * 0.07f, 0.06f, -0.12f), new Vector3(0.03f, 0.09f, 0.03f), new Vector3(20f, 0f, 0f), Palette.InkAccent);
                        break;
                    case FactionOverlayTileType.Miasma_Revealed:
                        Prop(holder, PrimitiveType.Sphere, at + new Vector3(0f, 0.14f, 0f), new Vector3(0.22f, 0.12f, 0.22f), Vector3.zero, Palette.MiasmaMist, 0.2f);
                        Prop(holder, PrimitiveType.Sphere, at + new Vector3(0.08f, 0.26f, 0.05f), new Vector3(0.13f, 0.08f, 0.13f), Vector3.zero, Palette.MiasmaMist, 0.2f);
                        Prop(holder, PrimitiveType.Cube, at + new Vector3(-0.05f, 0.05f, -0.08f), new Vector3(0.06f, 0.10f, 0.03f), new Vector3(0f, 20f, 6f), Palette.BoneWhite); // grave marker
                        break;
                    case FactionOverlayTileType.CapybaraStream:
                        Prop(holder, PrimitiveType.Cylinder, at + new Vector3(0f, 0.01f, 0f), new Vector3(0.18f, 0.008f, 0.18f), Vector3.zero, Palette.LilyPad, 0.6f);
                        Prop(holder, PrimitiveType.Cube, at + new Vector3(0.12f, 0.09f, 0.05f), new Vector3(0.02f, 0.18f, 0.02f), new Vector3(0f, 0f, 8f), Palette.LilyPad);
                        break;
                }
                n++;
            }
            TagPiece(root.gameObject, PieceKind.FactionTile, tile.Coord, new Vector3(0f, 0.03f, 0.45f * K), new Vector3(1.6f * K, 0.06f, 0.6f * K));
        }

        public void RebuildPieces()
        {
            if (_engine == null) return;
            if (_piecesRoot != null)
                Destroy(_piecesRoot.gameObject);

            _piecesRoot = new GameObject("TabletopPieces3D").transform;
            _piecesRoot.SetParent(transform, false);
            CritterModels.Mat = GetMaterial;

            foreach (var kvp in _engine.Board)
            {
                var coord = kvp.Key;
                var tile = kvp.Value;
                Vector3 tileCenter = coord.ToWorldPosition(HexRadius) + new Vector3(0f, TileThickness, 0f);

                // 1. Faction tile: replaces the tile's look (Part 4)
                ApplyOverlayLook(tile);

                // 2. Building (max 1 per tile)
                if (tile.Building != null)
                {
                    var owner = _engine.Players[tile.Building.OwnerPlayerIndex];
                    var bGo = CreateBuildingMiniature(tileCenter + BuildingSpot, tile.Building, owner);
                    TagPiece(bGo, PieceKind.Building, coord, new Vector3(0f, 0.25f, 0f), new Vector3(0.6f, 0.6f, 0.6f), building: tile.Building);
                }

                // 3. Tokens (max 2 per tile)
                for (int tIdx = 0; tIdx < tile.Tokens.Count && tIdx < 2; tIdx++)
                {
                    var tk = tile.Tokens[tIdx];
                    var tGo = CreateTokenMiniature(tileCenter + TokenSpot(tIdx) + new Vector3(0f, 0.02f, 0f), tk);
                    TagPiece(tGo, PieceKind.Token, coord, new Vector3(0f, 0.1f, 0f), new Vector3(0.5f, 0.3f, 0.5f), token: tk);
                }

                // 4. Units: each faction gets its own columns of the 4x3 grid (two full factions = 12 units)
                var owners = tile.Units.Select(x => x.OwnerPlayerIndex).Distinct().OrderBy(x => x).ToList();
                int nf = Mathf.Max(1, owners.Count);
                for (int bi = 0; bi < owners.Count; bi++)
                {
                    var mine = tile.Units.Where(x => x.OwnerPlayerIndex == owners[bi])
                                         .OrderByDescending(x => x.Definition.IsSpecialUnit).ToList();
                    // which grid columns this faction uses
                    float[] cols;
                    if (nf == 1) cols = new[] { -0.38f, 0f, 0.38f };
                    else if (nf == 2) cols = bi == 0 ? new[] { UnitCols[0], UnitCols[1] } : new[] { UnitCols[2], UnitCols[3] };
                    else cols = new[] { UnitCols[Mathf.Min(bi, 3)] };
                    int perRow = cols.Length;
                    int rowsNeeded = Mathf.CeilToInt(mine.Count / (float)perRow);
                    float shrink = rowsNeeded > 3 ? 3f / rowsNeeded : 1f;
                    float unitScale = UnitScaleK * K * shrink * (nf > 2 ? 0.8f : 1f);
                    for (int ui = 0; ui < mine.Count; ui++)
                    {
                        int col = ui % perRow, row = ui / perRow;
                        float rz = rowsNeeded > 3 ? (UnitRows[0] - row * (0.66f / (rowsNeeded - 1))) : UnitRows[Mathf.Min(row, 2)];
                        float cx = cols[col];
                        if (nf == 1 && mine.Count == 1) cx = 0f;
                        var pos = tileCenter + new Vector3(cx * K, 0f, rz * K);
                        var u = mine[ui];
                        var uGo = CreateCritterMiniature(pos, u, _engine.Players[u.OwnerPlayerIndex], ui, unitScale);
                        TagPiece(uGo, PieceKind.Unit, coord, new Vector3(0f, 0.25f, 0f), new Vector3(0.4f, 0.55f, 0.4f), unit: u);
                    }
                }
            }
        }

        private GameObject CreateBuildingMiniature(Vector3 pos, BuildingInstance b, PlayerState owner)
        {
            var root = new GameObject($"Building_{b.Type}");
            root.transform.SetParent(_piecesRoot, false);
            root.transform.localPosition = pos;
            root.transform.localScale = Vector3.one * K * 1.15f;
            var t = root.transform;
            Color a = owner.Faction.PrimaryColor, s = owner.Faction.SecondaryColor;
            Prop(t, PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0f), new Vector3(0.46f, 0.02f, 0.46f), Vector3.zero, a, 0.5f); // owner base

            switch (b.Type)
            {
                case BuildingType.WarParty: // war tent with banner
                    Prop(t, PrimitiveType.Cube, new Vector3(0f, 0.14f, 0f), new Vector3(0.26f, 0.26f, 0.26f), new Vector3(45f, 0f, 0f), s);
                    Prop(t, PrimitiveType.Cube, new Vector3(0.14f, 0.30f, 0.05f), new Vector3(0.015f, 0.34f, 0.015f), Vector3.zero, Palette.WoodPlank);
                    Prop(t, PrimitiveType.Cube, new Vector3(0.20f, 0.40f, 0.05f), new Vector3(0.12f, 0.08f, 0.01f), Vector3.zero, a);
                    break;
                case BuildingType.Burrow: // earth mound with a dark hole
                    Prop(t, PrimitiveType.Sphere, new Vector3(0f, 0.08f, 0f), new Vector3(0.40f, 0.20f, 0.36f), Vector3.zero, Palette.TableOak);
                    Prop(t, PrimitiveType.Sphere, new Vector3(0f, 0.09f, -0.15f), new Vector3(0.16f, 0.12f, 0.06f), Vector3.zero, Palette.InkAccent);
                    break;
                case BuildingType.TheCitadel: // white tower with gold dome
                    Prop(t, PrimitiveType.Cylinder, new Vector3(0f, 0.22f, 0f), new Vector3(0.22f, 0.22f, 0.22f), Vector3.zero, Palette.HolyWhite);
                    Prop(t, PrimitiveType.Sphere, new Vector3(0f, 0.46f, 0f), new Vector3(0.24f, 0.18f, 0.24f), Vector3.zero, Palette.AuthorityGold, 0.8f);
                    Prop(t, PrimitiveType.Cube, new Vector3(0f, 0.60f, 0f), new Vector3(0.02f, 0.12f, 0.02f), Vector3.zero, Palette.AuthorityGold);
                    break;
                case BuildingType.Shrine: // little torii gate
                    Prop(t, PrimitiveType.Cube, new Vector3(-0.12f, 0.17f, 0f), new Vector3(0.035f, 0.32f, 0.035f), Vector3.zero, a);
                    Prop(t, PrimitiveType.Cube, new Vector3(0.12f, 0.17f, 0f), new Vector3(0.035f, 0.32f, 0.035f), Vector3.zero, a);
                    Prop(t, PrimitiveType.Cube, new Vector3(0f, 0.34f, 0f), new Vector3(0.36f, 0.035f, 0.05f), Vector3.zero, a);
                    Prop(t, PrimitiveType.Cube, new Vector3(0f, 0.28f, 0f), new Vector3(0.28f, 0.025f, 0.04f), Vector3.zero, Palette.InkAccent);
                    break;
                case BuildingType.Bunker: // log dugout
                    for (int i = 0; i < 3; i++)
                        Prop(t, PrimitiveType.Capsule, new Vector3(0f, 0.05f + i * 0.07f, 0f), new Vector3(0.07f, 0.17f - i * 0.03f, 0.07f), new Vector3(0f, 0f, 90f), Palette.WoodPlank);
                    Prop(t, PrimitiveType.Cube, new Vector3(0f, 0.09f, -0.06f), new Vector3(0.10f, 0.06f, 0.02f), Vector3.zero, Palette.InkAccent);
                    break;
                case BuildingType.Wallow: // muddy pool with a reed
                    Prop(t, PrimitiveType.Cylinder, new Vector3(0f, 0.035f, 0f), new Vector3(0.38f, 0.02f, 0.30f), Vector3.zero, Palette.RiverTile, 0.85f);
                    Prop(t, PrimitiveType.Cylinder, new Vector3(0f, 0.03f, 0f), new Vector3(0.42f, 0.015f, 0.34f), Vector3.zero, Palette.TableOak);
                    Prop(t, PrimitiveType.Cube, new Vector3(0.16f, 0.14f, 0.06f), new Vector3(0.02f, 0.22f, 0.02f), new Vector3(0f, 0f, 8f), Palette.LilyPad);
                    break;
                case BuildingType.PricklyThicket: // bramble of thorny stalks
                    for (int i = 0; i < 5; i++)
                    {
                        float ang = i * 72f;
                        var dir = Quaternion.Euler(0f, ang, 0f) * Vector3.forward * 0.09f;
                        Prop(t, PrimitiveType.Capsule, new Vector3(dir.x, 0.13f, dir.z), new Vector3(0.035f, 0.12f, 0.035f), new Vector3(dir.z * 160f, ang, -dir.x * 160f), s);
                    }
                    Prop(t, PrimitiveType.Sphere, new Vector3(0f, 0.22f, 0f), Vector3.one * 0.08f, Vector3.zero, a);
                    break;
                default:
                    Prop(t, PrimitiveType.Cube, new Vector3(0f, 0.18f, 0f), new Vector3(0.25f, 0.25f, 0.25f), new Vector3(0f, 45f, 0f), a);
                    break;
            }
            return root;
        }

        /// <summary>Tokens are discs with a raised emblem so each kind reads at a glance.</summary>
        private GameObject CreateTokenMiniature(Vector3 pos, TokenInstance tk)
        {
            var root = new GameObject($"Token_{tk.Type}");
            root.transform.SetParent(_piecesRoot, false);
            root.transform.localPosition = pos;
            root.transform.localScale = Vector3.one * K * 1.0f;
            var t = root.transform;
            var owner = _engine.Players[tk.OwnerPlayerIndex];
            Color own = owner.Faction.PrimaryColor;

            Color disc = tk.Type == TokenType.Coin ? Palette.AuthorityGold
                       : tk.Type == TokenType.TarPit ? Palette.TarBlack
                       : tk.Type == TokenType.HolyRelic ? Palette.HolyWhite
                       : owner.Faction.SecondaryColor;
            Prop(t, PrimitiveType.Cylinder, Vector3.zero + new Vector3(0f, 0.02f, 0f), new Vector3(0.30f, 0.02f, 0.30f), Vector3.zero, disc, 0.65f);
            Prop(t, PrimitiveType.Cylinder, new Vector3(0f, 0.01f, 0f), new Vector3(0.33f, 0.012f, 0.33f), Vector3.zero, own, 0.5f); // owner ring

            switch (tk.Type)
            {
                case TokenType.SupplyLine: // forward chevron
                    Prop(t, PrimitiveType.Cube, new Vector3(-0.04f, 0.05f, 0f), new Vector3(0.14f, 0.03f, 0.035f), new Vector3(0f, 45f, 0f), own);
                    Prop(t, PrimitiveType.Cube, new Vector3(0.04f, 0.05f, 0f), new Vector3(0.14f, 0.03f, 0.035f), new Vector3(0f, -45f, 0f), own);
                    break;
                case TokenType.Gluttony: // carrot
                    Prop(t, PrimitiveType.Capsule, new Vector3(0f, 0.06f, 0f), new Vector3(0.05f, 0.08f, 0.05f), new Vector3(90f, 30f, 0f), Palette.CarrotOrange);
                    Prop(t, PrimitiveType.Cube, new Vector3(0.05f, 0.07f, 0.08f), new Vector3(0.02f, 0.02f, 0.06f), new Vector3(0f, 40f, 0f), Palette.LilyPad);
                    break;
                case TokenType.Catapult: // frame, arm and cup; pips show uses left
                    Prop(t, PrimitiveType.Cube, new Vector3(0f, 0.05f, 0f), new Vector3(0.16f, 0.04f, 0.10f), Vector3.zero, Palette.WoodPlank);
                    Prop(t, PrimitiveType.Cube, new Vector3(0f, 0.11f, 0.02f), new Vector3(0.025f, 0.025f, 0.18f), new Vector3(-30f, 0f, 0f), Palette.WoodPlank);
                    Prop(t, PrimitiveType.Sphere, new Vector3(0f, 0.16f, 0.09f), Vector3.one * 0.05f, Vector3.zero, Palette.InkAccent);
                    for (int i = 0; i < tk.RemainingUses; i++)
                        Prop(t, PrimitiveType.Sphere, new Vector3(-0.09f + i * 0.05f, 0.05f, -0.10f), Vector3.one * 0.035f, Vector3.zero, Palette.AuthorityGold);
                    break;
                case TokenType.HolyRelic: // gold cross
                    Prop(t, PrimitiveType.Cube, new Vector3(0f, 0.09f, 0f), new Vector3(0.03f, 0.13f, 0.03f), Vector3.zero, Palette.AuthorityGold, 0.85f);
                    Prop(t, PrimitiveType.Cube, new Vector3(0f, 0.11f, 0f), new Vector3(0.09f, 0.03f, 0.03f), Vector3.zero, Palette.AuthorityGold, 0.85f);
                    break;
                case TokenType.Coin: // gold coin with a square hole
                    Prop(t, PrimitiveType.Cube, new Vector3(0f, 0.035f, 0f), new Vector3(0.07f, 0.01f, 0.07f), new Vector3(0f, 45f, 0f), Palette.InkAccent);
                    break;
                case TokenType.TarPit: // glossy bubbles
                    Prop(t, PrimitiveType.Sphere, new Vector3(-0.04f, 0.04f, 0.02f), new Vector3(0.09f, 0.05f, 0.09f), Vector3.zero, Palette.TarBlack, 0.95f);
                    Prop(t, PrimitiveType.Sphere, new Vector3(0.05f, 0.04f, -0.03f), new Vector3(0.06f, 0.04f, 0.06f), Vector3.zero, Palette.TarBlack, 0.95f);
                    break;
            }
            return root;
        }

        private GameObject CreateCritterMiniature(Vector3 pos, UnitInstance u, PlayerState owner, int idxOnTile, float scale)
        {
            var root = new GameObject($"Unit_{u.Definition.Name}");
            root.transform.SetParent(_piecesRoot, false);
            root.transform.localPosition = pos;
            root.transform.localScale = Vector3.one * scale * (u.Definition.IsSpecialUnit ? 1.35f : 1f);

            var anim = root.AddComponent<IdleCritterAnimator>();
            anim.BaseY = pos.y;
            anim.PhaseOffset = idxOnTile * 1.3f + pos.x;
            anim.BobAmplitude = 0.012f * scale;  // gentle breathing bob (~2% of height)
            anim.BobFrequency = 3.8f;            // ~0.6 Hz

            CritterModels.Build(root.transform, u, owner);
            return root;
        }

        private Material GetTerrainMaterial(TileTerrainType terrain)
        {
            switch (terrain)
            {
                case TileTerrainType.Forest: return GetMaterial(Palette.ForestTile, 0.3f);
                case TileTerrainType.Ruin:   return GetMaterial(Palette.RuinTile, 0.25f);
                case TileTerrainType.Stream: return GetMaterial(Palette.RiverTile, 0.7f);
                default:                     return GetMaterial(Palette.ForestTile);
            }
        }

        private Material GetOverlayMaterial(FactionOverlayTileType overlay)
        {
            switch (overlay)
            {
                case FactionOverlayTileType.Miasma_Hidden:        return GetMaterial(Palette.MiasmaHidden, 0.35f);
                case FactionOverlayTileType.Miasma_Revealed:      return GetMaterial(Palette.MiasmaTile, 0.55f);
                case FactionOverlayTileType.Fire:                 return GetMaterial(Palette.CharredGround, 0.3f);
                case FactionOverlayTileType.FallenLeaf_Unflipped: return GetMaterial(Palette.FallenLeafTile, 0.3f);
                case FactionOverlayTileType.FallenLeaf_Trap:      return GetMaterial(Palette.TrapLeafRed, 0.4f);
                case FactionOverlayTileType.FallenLeaf_Prank:     return GetMaterial(Palette.AuthorityGold, 0.5f);
                case FactionOverlayTileType.CapybaraStream:       return GetMaterial(Palette.RiverTile, 0.75f);
                default:                                          return GetMaterial(Palette.ParchmentMap);
            }
        }

        private static Mesh CreateHexPrismMesh(float radius, float height)
        {
            var mesh = new Mesh { name = "PointyHexPrism" };
            var verts = new List<Vector3>();
            var tris = new List<int>();

            verts.Add(new Vector3(0f, height, 0f));
            verts.Add(new Vector3(0f, 0f, 0f));

            for (int i = 0; i < 6; i++)
            {
                float angleRad = Mathf.Deg2Rad * (60f * i - 30f);
                float x = radius * Mathf.Cos(angleRad);
                float z = radius * Mathf.Sin(angleRad);
                verts.Add(new Vector3(x, height, z));
            }
            for (int i = 0; i < 6; i++)
            {
                float angleRad = Mathf.Deg2Rad * (60f * i - 30f);
                float x = radius * Mathf.Cos(angleRad);
                float z = radius * Mathf.Sin(angleRad);
                verts.Add(new Vector3(x, 0f, z));
            }

            for (int i = 0; i < 6; i++)
            {
                int next = (i + 1) % 6;
                tris.Add(0);
                tris.Add(2 + next);
                tris.Add(2 + i);

                tris.Add(1);
                tris.Add(8 + i);
                tris.Add(8 + next);

                int t1 = 2 + i;
                int t2 = 2 + next;
                int b1 = 8 + i;
                int b2 = 8 + next;

                tris.Add(t1);
                tris.Add(t2);
                tris.Add(b1);

                tris.Add(t2);
                tris.Add(b2);
                tris.Add(b1);
            }

            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
