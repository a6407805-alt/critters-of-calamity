using System;
using System.Collections.Generic;
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
    /// with water surrounding the island, a 0-30 Authority Score Track around the edge,
    /// Forest pine groves, Anankian ruin pillars, light-blue Stream channels, Unique Faction Overlay Tiles
    /// (Fallen Leaves, Fire, Miasma, Capybara Stream), Buildings, Tokens, and stylized 3D Critter Miniatures.
    /// </summary>
    public class Tabletop3DBoardRenderer : MonoBehaviour
    {
        public float HexRadius = 1.15f;
        public float TileThickness = 0.25f;

        public HexCoord? SelectedCoord = null;
        public event Action<HexCoord> OnHexClicked;

        private CrittersGameEngine _engine;
        private Transform _boardRoot;
        private Transform _piecesRoot;
        private Mesh _hexCylinderMesh;
        private Material _baseUrpMat;
        private Dictionary<Color, Material> _matCache = new Dictionary<Color, Material>();
        private Dictionary<HexCoord, GameObject> _tileObjects = new Dictionary<HexCoord, GameObject>();
        private GameObject _selectionRing;

        // Camera orbit / pan state sized for the 108-tile Continent board
        private float _camYaw = 0f;
        private float _camPitch = 52f;
        private float _camDistance = 21f;
        private Vector3 _camPivot = Vector3.zero;

        public void BindEngine(CrittersGameEngine engine)
        {
            if (_engine != null)
                _engine.OnStateChanged -= RebuildPieces;

            _engine = engine;
            _engine.OnStateChanged += RebuildPieces;

            EnsureHexMeshAndBaseMaterial();
            BuildStaticTableAndHexGrid();
            RebuildPieces();
        }

        public void FocusCameraOnCoord(HexCoord coord, float targetDistance = 13f)
        {
            _camPivot = coord.ToWorldPosition(HexRadius);
            _camDistance = Mathf.Clamp(targetDistance, 6f, 38f);
        }

        public void ResetCameraToOverview()
        {
            _camPivot = Vector3.zero;
            _camPitch = 53f;
            _camDistance = 21f;
        }

        void Update()
        {
            HandleCameraControls();
            HandleMouseHexSelection();
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
                float panSpeed = Mathf.Lerp(9f, 20f, (_camDistance - 6f) / 32f);

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
                    _camPivot -= (right * delta.x + forward * delta.y) * 0.03f;
                }

                bool pointerOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                if (!pointerOverUi)
                {
                    float scroll = mouse.scroll.ReadValue().y;
                    if (Mathf.Abs(scroll) > 0.01f)
                    {
                        _camDistance = Mathf.Clamp(_camDistance - Mathf.Sign(scroll) * 1.6f, 6f, 38f);
                    }
                }
            }

            _camPivot.x = Mathf.Clamp(_camPivot.x, -18f, 18f);
            _camPivot.z = Mathf.Clamp(_camPivot.z, -18f, 18f);

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

        private void BuildStaticTableAndHexGrid()
        {
            if (_boardRoot != null)
                Destroy(_boardRoot.gameObject);

            _boardRoot = new GameObject("TabletopBoard3D").transform;
            _boardRoot.SetParent(transform, false);
            _tileObjects.Clear();

            // 1. Carved Oak Tabletop, 0-30 Authority Score Track Rim, and Ocean Water around the Continent (Part 1 Line 20 & Part 2 Line 43)
            var table = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(table.GetComponent<Collider>());
            table.name = "AnankianOakTable";
            table.transform.SetParent(_boardRoot, false);
            table.transform.localPosition = new Vector3(0f, -0.38f, 0f);
            table.transform.localScale = new Vector3(36f, 0.25f, 36f);
            table.GetComponent<Renderer>().sharedMaterial = GetMaterial(Palette.TableOak, 0.4f);

            var scoreTrackRim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(scoreTrackRim.GetComponent<Collider>());
            scoreTrackRim.name = "AuthorityScoreTrack0To30";
            scoreTrackRim.transform.SetParent(_boardRoot, false);
            scoreTrackRim.transform.localPosition = new Vector3(0f, -0.10f, 0f);
            scoreTrackRim.transform.localScale = new Vector3(28.5f, 0.06f, 28.5f);
            scoreTrackRim.GetComponent<Renderer>().sharedMaterial = GetMaterial(Palette.ParchmentMap, 0.3f);

            var oceanWater = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(oceanWater.GetComponent<Collider>());
            oceanWater.name = "SurroundingWater";
            oceanWater.transform.SetParent(_boardRoot, false);
            oceanWater.transform.localPosition = new Vector3(0f, -0.04f, 0f);
            oceanWater.transform.localScale = new Vector3(26.2f, 0.05f, 26.2f);
            oceanWater.GetComponent<Renderer>().sharedMaterial = GetMaterial(Palette.FromHex("#2E5D7B"), 0.8f);

            // 30 Authority Score Track Markers around the perimeter of the board (0 to 30)
            for (int a = 0; a <= 30; a++)
            {
                float angle = (a / 31f) * Mathf.PI * 2f;
                var pip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(pip.GetComponent<Collider>());
                pip.name = $"ScorePip_{a}";
                pip.transform.SetParent(_boardRoot, false);
                pip.transform.localPosition = new Vector3(Mathf.Cos(angle) * 13.6f, 0.02f, Mathf.Sin(angle) * 13.6f);
                pip.transform.localScale = (a % 5 == 0) ? new Vector3(0.32f, 0.08f, 0.32f) : new Vector3(0.18f, 0.05f, 0.18f);
                pip.GetComponent<Renderer>().sharedMaterial = GetMaterial((a == 30) ? Palette.AuthorityGold : Palette.TableOak, 0.6f);
            }

            // 2. Selection Highlight Ring
            _selectionRing = new GameObject("HexSelectionRing");
            _selectionRing.transform.SetParent(_boardRoot, false);
            var ringFilter = _selectionRing.AddComponent<MeshFilter>();
            ringFilter.sharedMesh = CreateHexPrismMesh(HexRadius * 1.02f, 0.055f);
            var ringRenderer = _selectionRing.AddComponent<MeshRenderer>();
            ringRenderer.sharedMaterial = GetMaterial(Palette.AuthorityGold, 0.8f);
            _selectionRing.SetActive(false);

            // 3. Build 3D Hex Tiles for the 108-Tile Continent
            foreach (var kvp in _engine.Board)
            {
                var coord = kvp.Key;
                var tileState = kvp.Value;
                Vector3 worldPos = coord.ToWorldPosition(HexRadius);

                var hexGo = new GameObject($"Hex_{coord.q}_{coord.r}");
                hexGo.transform.SetParent(_boardRoot, false);
                hexGo.transform.localPosition = worldPos;

                var relay = hexGo.AddComponent<HexTileClickRelay>();
                relay.Coord = coord;
                relay.BoardRenderer = this;

                var mf = hexGo.AddComponent<MeshFilter>();
                mf.sharedMesh = _hexCylinderMesh;

                var mr = hexGo.AddComponent<MeshRenderer>();
                mr.sharedMaterial = GetTerrainMaterial(tileState.BaseTerrain);

                var mc = hexGo.AddComponent<MeshCollider>();
                mc.sharedMesh = _hexCylinderMesh;

                CreateTerrainDecorations(hexGo.transform, tileState.BaseTerrain);
                _tileObjects[coord] = hexGo;
            }
        }

        private void CreateTerrainDecorations(Transform tileParent, TileTerrainType terrain)
        {
            if (terrain == TileTerrainType.Forest)
            {
                Vector3[] treeOffsets = new Vector3[]
                {
                    new Vector3(-0.56f, TileThickness, 0.44f),
                    new Vector3(0.58f, TileThickness, 0.38f)
                };
                foreach (var pos in treeOffsets)
                {
                    var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Destroy(trunk.GetComponent<Collider>());
                    trunk.transform.SetParent(tileParent, false);
                    trunk.transform.localPosition = pos + new Vector3(0f, 0.08f, 0f);
                    trunk.transform.localScale = new Vector3(0.07f, 0.08f, 0.07f);
                    trunk.GetComponent<Renderer>().sharedMaterial = GetMaterial(Palette.TableOak);

                    var canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Destroy(canopy.GetComponent<Collider>());
                    canopy.transform.SetParent(tileParent, false);
                    canopy.transform.localPosition = pos + new Vector3(0f, 0.22f, 0f);
                    canopy.transform.localScale = new Vector3(0.22f, 0.28f, 0.22f);
                    canopy.GetComponent<Renderer>().sharedMaterial = GetMaterial(Palette.FromHex("#3A6335"));
                }
            }
            else if (terrain == TileTerrainType.Ruin)
            {
                var col = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(col.GetComponent<Collider>());
                col.transform.SetParent(tileParent, false);
                col.transform.localPosition = new Vector3(-0.55f, TileThickness + 0.14f, -0.4f);
                col.transform.localScale = new Vector3(0.14f, 0.14f, 0.14f);
                col.transform.localRotation = Quaternion.Euler(6f, 15f, -5f);
                col.GetComponent<Renderer>().sharedMaterial = GetMaterial(Palette.FromHex("#B0B2B8"));

                var arch = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(arch.GetComponent<Collider>());
                arch.transform.SetParent(tileParent, false);
                arch.transform.localPosition = new Vector3(0.52f, TileThickness + 0.09f, -0.42f);
                arch.transform.localScale = new Vector3(0.18f, 0.12f, 0.10f);
                arch.GetComponent<Renderer>().sharedMaterial = GetMaterial(Palette.FromHex("#A2A5AC"));
            }
        }

        public void RebuildPieces()
        {
            if (_engine == null) return;
            if (_piecesRoot != null)
                Destroy(_piecesRoot.gameObject);

            _piecesRoot = new GameObject("TabletopPieces3D").transform;
            _piecesRoot.SetParent(transform, false);

            foreach (var kvp in _engine.Board)
            {
                var coord = kvp.Key;
                var tile = kvp.Value;
                Vector3 tileCenter = coord.ToWorldPosition(HexRadius) + new Vector3(0f, TileThickness, 0f);

                // 1. Unique Faction Overlay Tile (Part 4 Line 98)
                if (tile.Overlay != FactionOverlayTileType.None)
                {
                    var overlayGo = new GameObject($"Overlay_{tile.Overlay}");
                    overlayGo.transform.SetParent(_piecesRoot, false);
                    overlayGo.transform.localPosition = tileCenter + new Vector3(0f, 0.01f, 0f);
                    var mf = overlayGo.AddComponent<MeshFilter>();
                    mf.sharedMesh = CreateHexPrismMesh(HexRadius * 0.88f, 0.055f);
                    var mr = overlayGo.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = GetOverlayMaterial(tile.Overlay);
                    tileCenter.y += 0.055f;
                }

                // 2. Building (Max 1 per tile, Part 4 Line 97)
                if (tile.Building != null)
                {
                    var owner = _engine.Players[tile.Building.OwnerPlayerIndex];
                    CreateBuildingMiniature(tileCenter + new Vector3(0f, 0f, 0.36f), tile.Building, owner);
                }

                // 3. Tokens (Max 2 per tile, Part 4 Line 97)
                for (int tIdx = 0; tIdx < tile.Tokens.Count; tIdx++)
                {
                    var tk = tile.Tokens[tIdx];
                    float xOff = (tIdx == 0) ? -0.45f : 0.45f;
                    CreateTokenMiniature(tileCenter + new Vector3(xOff, 0.03f, -0.36f), tk);
                }

                // 4. Units & Special Units (Max 6 per faction per tile, Part 10 Line 175)
                int unitCount = tile.Units.Count;
                for (int uIdx = 0; uIdx < unitCount; uIdx++)
                {
                    var u = tile.Units[uIdx];
                    var owner = _engine.Players[u.OwnerPlayerIndex];
                    float angle = (uIdx / (float)Mathf.Max(1, unitCount)) * Mathf.PI * 2f;
                    float radius = unitCount == 1 ? 0f : 0.39f;
                    Vector3 uPos = tileCenter + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius - 0.04f);
                    CreateCritterMiniature(uPos, u, owner, uIdx);
                }
            }
        }

        private void CreateBuildingMiniature(Vector3 pos, BuildingInstance b, PlayerState owner)
        {
            var root = new GameObject($"Building_{b.Type}");
            root.transform.SetParent(_piecesRoot, false);
            root.transform.localPosition = pos;

            var basePlinth = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(basePlinth.GetComponent<Collider>());
            basePlinth.transform.SetParent(root.transform, false);
            basePlinth.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            basePlinth.transform.localScale = new Vector3(0.40f, 0.06f, 0.40f);
            basePlinth.GetComponent<Renderer>().sharedMaterial = GetMaterial(owner.Faction.SecondaryColor);

            var tower = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(tower.GetComponent<Collider>());
            tower.transform.SetParent(root.transform, false);
            tower.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            tower.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
            tower.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            tower.GetComponent<Renderer>().sharedMaterial = GetMaterial(owner.Faction.PrimaryColor);
        }

        private void CreateTokenMiniature(Vector3 pos, TokenInstance tk)
        {
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(disc.GetComponent<Collider>());
            disc.name = $"Token_{tk.Type}";
            disc.transform.SetParent(_piecesRoot, false);
            disc.transform.localPosition = pos;
            disc.transform.localScale = new Vector3(0.25f, 0.035f, 0.25f);

            Color tkColor = tk.Type == TokenType.Coin ? Palette.AuthorityGold :
                            tk.Type == TokenType.TarPit ? Palette.ShadowSlate :
                            _engine.Players[tk.OwnerPlayerIndex].Faction.SecondaryColor;
            disc.GetComponent<Renderer>().sharedMaterial = GetMaterial(tkColor, 0.65f);
        }

        private void CreateCritterMiniature(Vector3 pos, UnitInstance u, PlayerState owner, int idxOnTile)
        {
            var root = new GameObject($"Unit_{u.Definition.Name}");
            root.transform.SetParent(_piecesRoot, false);
            root.transform.localPosition = pos;

            float scaleMult = u.Definition.IsSpecialUnit ? 1.42f : 0.94f;
            root.transform.localScale = Vector3.one * scaleMult;

            var anim = root.AddComponent<IdleCritterAnimator>();
            anim.BaseY = pos.y;
            anim.PhaseOffset = idxOnTile * 1.3f + pos.x;

            Color pedestalCol = u.Definition.IsSpecialUnit ? Palette.AuthorityGold : Palette.ShadowSlate;
            var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(pedestal.GetComponent<Collider>());
            pedestal.transform.SetParent(root.transform, false);
            pedestal.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            pedestal.transform.localScale = new Vector3(0.26f, 0.03f, 0.26f);
            pedestal.GetComponent<Renderer>().sharedMaterial = GetMaterial(pedestalCol, 0.6f);

            // Body
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.16f, 0f);
            body.transform.localScale = new Vector3(0.20f, 0.22f, 0.24f);
            body.GetComponent<Renderer>().sharedMaterial = GetMaterial(owner.Faction.PrimaryColor);

            // Chest / Belly
            var chest = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(chest.GetComponent<Collider>());
            chest.transform.SetParent(root.transform, false);
            chest.transform.localPosition = new Vector3(0f, 0.15f, 0.05f);
            chest.transform.localScale = new Vector3(0.14f, 0.15f, 0.15f);
            chest.GetComponent<Renderer>().sharedMaterial = GetMaterial(owner.Faction.SecondaryColor);

            // Head
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(head.GetComponent<Collider>());
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 0.31f, 0.03f);
            head.transform.localScale = new Vector3(0.16f, 0.15f, 0.16f);
            head.GetComponent<Renderer>().sharedMaterial = GetMaterial(owner.Faction.PrimaryColor);

            // Exaggerated Silhouette Ears / Crest
            for (int side = -1; side <= 1; side += 2)
            {
                var ear = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(ear.GetComponent<Collider>());
                ear.transform.SetParent(root.transform, false);
                ear.transform.localPosition = new Vector3(side * 0.055f, 0.42f, 0.02f);
                ear.transform.localScale = new Vector3(0.045f, 0.11f, 0.03f);
                ear.transform.localRotation = Quaternion.Euler(0f, 0f, -side * 14f);
                ear.GetComponent<Renderer>().sharedMaterial = GetMaterial(owner.Faction.SecondaryColor);
            }

            // Part 11 Line 185: Tracking damage on Special Units & multi-HP units
            if (u.Definition.IsSpecialUnit || u.MaxHP > 1)
            {
                for (int hp = 0; hp < u.CurrentHP; hp++)
                {
                    var pip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(pip.GetComponent<Collider>());
                    pip.transform.SetParent(root.transform, false);
                    float xOffset = (hp - (u.CurrentHP - 1) * 0.5f) * 0.08f;
                    pip.transform.localPosition = new Vector3(xOffset, 0.53f, 0f);
                    pip.transform.localScale = Vector3.one * 0.055f;
                    pip.GetComponent<Renderer>().sharedMaterial = GetMaterial(Palette.AuthorityGold, 0.8f);
                }
            }
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
                case FactionOverlayTileType.Miasma_Hidden:
                    return GetMaterial(Palette.FromHex("#452766"), 0.35f);
                case FactionOverlayTileType.Miasma_Revealed:
                    return GetMaterial(Palette.MiasmaTile, 0.55f);
                case FactionOverlayTileType.Fire:
                    return GetMaterial(Palette.FireTile, 0.6f);
                case FactionOverlayTileType.FallenLeaf_Unflipped:
                    return GetMaterial(Palette.FallenLeafTile, 0.3f);
                case FactionOverlayTileType.FallenLeaf_Trap:
                    return GetMaterial(Palette.FromHex("#9E2A2B"), 0.4f);
                case FactionOverlayTileType.FallenLeaf_Prank:
                    return GetMaterial(Palette.AuthorityGold, 0.7f);
                case FactionOverlayTileType.CapybaraStream:
                    return GetMaterial(Palette.RiverTile, 0.75f);
                default:
                    return GetMaterial(Palette.ParchmentMap);
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
