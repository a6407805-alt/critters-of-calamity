using System;
using UnityEngine;
using CrittersOfCalamity.Engine;
using CrittersOfCalamity.Model;

namespace CrittersOfCalamity.Visuals
{
    /// <summary>
    /// Builds the painted miniatures from primitives, one silhouette per species so every faction reads when filled
    /// black (craft floor: silhouette first, taper and tilt, two-tone minimum). The faction's primary color is the
    /// body (it tells you who owns the piece); its secondary color is the belly and trim; Palette accents do eyes,
    /// paws and props. Special units are larger and carry a signature prop. Pieces face the camera (-Z).
    /// </summary>
    public static class CritterModels
    {
        /// <summary>Material lookup supplied by the renderer (shared, cached URP Lit materials).</summary>
        public static Func<Color, float, Material> Mat;

        static Transform P(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Vector3 euler, Color color, float smooth = 0.35f)
        {
            var go = GameObject.CreatePrimitive(type);
            var col = go.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.GetComponent<Renderer>().sharedMaterial = Mat(color, smooth);
            return go.transform;
        }

        static Transform S(Transform p, Vector3 pos, Vector3 scale, Color c) => P(p, PrimitiveType.Sphere, pos, scale, Vector3.zero, c);
        static Transform C(Transform p, Vector3 pos, Vector3 scale, Vector3 euler, Color c) => P(p, PrimitiveType.Cube, pos, scale, euler, c);
        static Transform Cap(Transform p, Vector3 pos, Vector3 scale, Vector3 euler, Color c) => P(p, PrimitiveType.Capsule, pos, scale, euler, c);
        static Transform Cyl(Transform p, Vector3 pos, Vector3 scale, Vector3 euler, Color c, float smooth = 0.35f) => P(p, PrimitiveType.Cylinder, pos, scale, euler, c, smooth);

        static void Eyes(Transform p, float y, float z, float spread, float size = 0.03f)
        {
            S(p, new Vector3(-spread, y, z), Vector3.one * size, Palette.InkAccent);
            S(p, new Vector3(spread, y, z), Vector3.one * size, Palette.InkAccent);
        }

        static void Legs(Transform p, float spreadX, float frontZ, float backZ, float height, float thick, Color c)
        {
            foreach (var x in new[] { -spreadX, spreadX })
                foreach (var z in new[] { frontZ, backZ })
                {
                    Cap(p, new Vector3(x, height * 0.5f, z), new Vector3(thick, height * 0.5f, thick), new Vector3(z < 0 ? -4f : 4f, 0f, x < 0 ? 3f : -3f), c);
                    S(p, new Vector3(x * 1.03f, 0.02f, z - 0.01f), Vector3.one * thick * 0.85f, Palette.InkAccent); // paw tip (0.8x taper)
                }
        }

        static void Crown(Transform p, Vector3 at, float size)
        {
            Cyl(p, at, new Vector3(size, size * 0.18f, size), Vector3.zero, Palette.AuthorityGold, 0.85f);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                C(p, at + new Vector3(Mathf.Cos(a) * size * 0.42f, size * 0.22f, Mathf.Sin(a) * size * 0.42f),
                  new Vector3(size * 0.16f, size * 0.32f, size * 0.16f), new Vector3(0f, 45f, 0f), Palette.AuthorityGold);
            }
        }

        /// <summary>Builds the miniature for a unit under root (root's position and scale are set by the caller).</summary>
        public static void Build(Transform root, UnitInstance u, PlayerState owner)
        {
            Color a = owner.Faction.PrimaryColor, b = owner.Faction.SecondaryColor;
            bool special = u.Definition.IsSpecialUnit;

            // Pedestal: faction color ring so ownership reads at any zoom; specials get a gold rim
            Cyl(root, new Vector3(0f, 0.015f, 0f), new Vector3(0.36f, 0.015f, 0.36f), Vector3.zero, a, 0.5f);
            if (special) Cyl(root, new Vector3(0f, 0.032f, 0f), new Vector3(0.30f, 0.006f, 0.30f), Vector3.zero, Palette.AuthorityGold, 0.85f);
            var body = new GameObject("Model").transform;
            body.SetParent(root, false);
            body.localPosition = new Vector3(0f, 0.035f, 0f);

            switch (u.Definition.Id)
            {
                case "warband": if (!SculptedModel(body, "Models/Warband", a, b, 0.55f, u)) Boar(body, a, b, false); break;
                case "curly_tail": if (!SculptedModel(body, "Models/CurlyTail", a, b, 0.66f, u)) Boar(body, a, b, true); break;
                case "millions": if (!SculptedModel(body, "Models/Millions", a, b, 0.50f, u)) Rabbit(body, a, b); break;
                case "king_of_the_millions":
                    if (!SculptedModel(body, "Models/KingOfTheMillions", a, b, 0.68f, u)) { Rabbit(body, a, b); Crown(body, new Vector3(0f, 0.43f, -0.07f), 0.13f); }
                    break;
                case "pilgrim": if (!SculptedModel(body, "Models/Pilgrim", a, b, 0.56f, u)) { Platypus(body, a, b); Hood(body, b); } break;
                case "crusader": if (!SculptedModel(body, "Models/Crusader", a, b, 0.50f, u)) { Platypus(body, a, b); Crusader(body, a, b); } break;
                case "platy_pope": if (!SculptedModel(body, "Models/PlatyPope", a, b, 0.72f, u)) { Platypus(body, a, b); Mitre(body); } break;
                case "tanuki": Tanuki(body, a, b); break;
                case "band_of_troublemakers": Tanuki(body, a, b); BellyDrum(body, b); break;
                case "skeleton": Mouse(body, Palette.BoneWhite, Palette.BoneWhite, a); break;
                case "necromouser": Mouse(body, a, b, b); NecroHood(body, a, b); break;
                case "the_bloat": Bloat(body, a, b); break;
                case "the_vanguard": Mouse(body, a, b, b); Shield(body, b, new Vector3(-0.13f, 0.14f, -0.12f)); Helmet(body, b, 0.30f); break;
                case "the_brute": Brute(body, a, b); break;
                case "squirrel": Squirrel(body, a, b); break;
                case "pyromaniac": Squirrel(body, a, b); Flame(body, new Vector3(0f, 0.42f, -0.06f), 0.10f); break;
                case "capybara": Capybara(body, a, b); break;
                case "capybara_trader": if (!SculptedModel(body, "Models/CapybaraTrader", a, b, 0.62f)) { Capybara(body, a, b); Pack(body, b); } break;
                case "riverkeeper": Capybara(body, a, b); S(body, new Vector3(0f, 0.33f, -0.16f), Vector3.one * 0.09f, Palette.CarrotOrange); break;
                case "thorned_seed": Seed(body, a, b); break;
                case "blooming_briar": Briar(body, a, b, owner.BriaredFlowerThorns); break;
                default: Mouse(body, a, b, b); break;
            }

            // HP pips over multi-HP units (Part 11: damage stays until healed; hover shows exact numbers)
            if (special || u.MaxHP > 1)
                for (int hp = 0; hp < u.MaxHP; hp++)
                {
                    float x = (hp - (u.MaxHP - 1) * 0.5f) * 0.075f;
                    S(root, new Vector3(x, 0.62f, 0f), Vector3.one * 0.05f, hp < u.CurrentHP ? Palette.AuthorityGold : Palette.ShadowSlate);
                }
        }

        /// <summary>
        /// Uses a sculpted model from Resources (made in Blender from the concept art, see Tools/Blender) when it exists.
        /// Its materials are named by role and recolored here, so the model still wears the owner's faction colors
        /// and the Palette accents. Returns false if the model isn't in the project.
        /// </summary>
        static bool SculptedModel(Transform parent, string resource, Color a, Color b, float scale, UnitInstance unit = null)
        {
            var prefab = Resources.Load<GameObject>(resource);
            if (prefab == null) return false;
            // The FBX root keeps whatever axis-conversion rotation/scale Unity gave it (a single-mesh FBX carries it on the
            // root itself), so it goes under a holder that does the turning and sizing.
            var holder = new GameObject(prefab.name);
            holder.transform.SetParent(parent, false);
            holder.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // modelled facing Blender -Y; turn to face the camera
            var go = UnityEngine.Object.Instantiate(prefab, holder.transform, false);
            go.transform.localPosition = Vector3.zero;
            foreach (var col in go.GetComponentsInChildren<Collider>()) UnityEngine.Object.Destroy(col);
            // Equipment meshes are named "Gear_<equipment id>" and only show while that item is equipped
            foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
                if (child.name.StartsWith("Gear_"))
                {
                    string id = child.name.Substring(5);
                    child.gameObject.SetActive(unit != null && unit.EquippedItems.Exists(e => e.Id == id));
                }
            // Size from the bare model's measured bounds (FBX unit scale varies by exporter): longest side = scale.
            // Gear is left out so forging a sword doesn't shrink the piece.
            var rs = Array.FindAll(go.GetComponentsInChildren<Renderer>(true), r => !r.name.StartsWith("Gear_"));
            if (rs.Length > 0)
            {
                var bounds = rs[0].bounds;
                foreach (var r in rs) bounds.Encapsulate(r.bounds);
                float parentScale = Mathf.Max(0.0001f, parent.lossyScale.x);
                float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)) / parentScale;
                if (longest > 0.0001f) holder.transform.localScale = Vector3.one * (scale / longest);
                // stand it on the base: lowest point of the bare model at the holder's floor
                float minY = float.MaxValue;
                foreach (var r in rs) minY = Mathf.Min(minY, r.bounds.min.y);
                float floor = parent.position.y;
                holder.transform.position += Vector3.up * (floor - minY);
            }
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    string n = mats[i] != null ? mats[i].name : "";
                    Color c = n.StartsWith("Body") ? a
                            : n.StartsWith("Belly") ? Color.Lerp(a, Palette.ParchmentMap, 0.35f)
                            : n.StartsWith("Blanket") ? b
                            : n.StartsWith("Cloth") ? Color.Lerp(a, Palette.InkAccent, 0.55f)
                            : n.StartsWith("Leather") ? Color.Lerp(Palette.InkAccent, Palette.WoodPlank, 0.5f)
                            : n.StartsWith("Bone") ? Palette.BoneWhite
                            : n.StartsWith("Robe") ? Palette.HolyWhite
                            : n.StartsWith("Bill") ? Color.Lerp(Palette.InkAccent, Palette.ShadowSlate, 0.4f)
                            : n.StartsWith("Iron") ? Palette.RuinStoneLight
                            : n.StartsWith("Nose") ? new Color(0.85f, 0.54f, 0.54f)
                            : n.StartsWith("Royal") ? Palette.TrapLeafRed
                            : n.StartsWith("Carrot") ? Palette.CarrotOrange
                            : n.StartsWith("Leaf") ? Palette.LilyPad
                            : n.StartsWith("Wood") ? Palette.WoodPlank
                            : n.StartsWith("Rope") ? Palette.ParchmentMap
                            : n.StartsWith("Gold") ? Palette.AuthorityGold
                            : Palette.InkAccent;                   // Quilt, Ink
                    mats[i] = Mat(c, n.StartsWith("Gold") ? 0.85f : 0.35f);
                }
                r.sharedMaterials = mats;
            }
            return true;
        }

        // ---------------- species ----------------

        static void Boar(Transform p, Color a, Color b, bool warlord)
        {
            S(p, new Vector3(0f, 0.17f, 0.02f), new Vector3(0.26f, 0.22f, 0.32f), a);                     // barrel body
            S(p, new Vector3(0f, 0.15f, -0.06f), new Vector3(0.18f, 0.15f, 0.18f), b);                    // belly
            S(p, new Vector3(0f, 0.24f, -0.15f), new Vector3(0.18f, 0.17f, 0.17f), a);                    // head
            Cyl(p, new Vector3(0f, 0.22f, -0.25f), new Vector3(0.09f, 0.04f, 0.09f), new Vector3(90f, 0f, 0f), b); // snout
            S(p, new Vector3(0f, 0.22f, -0.29f), new Vector3(0.05f, 0.04f, 0.02f), Palette.InkAccent);
            C(p, new Vector3(-0.05f, 0.19f, -0.27f), new Vector3(0.018f, 0.06f, 0.018f), new Vector3(-20f, 0f, 15f), Palette.BoneWhite); // tusks
            C(p, new Vector3(0.05f, 0.19f, -0.27f), new Vector3(0.018f, 0.06f, 0.018f), new Vector3(-20f, 0f, -15f), Palette.BoneWhite);
            C(p, new Vector3(-0.07f, 0.33f, -0.12f), new Vector3(0.06f, 0.08f, 0.02f), new Vector3(0f, 0f, 25f), a); // ears
            C(p, new Vector3(0.07f, 0.33f, -0.12f), new Vector3(0.06f, 0.08f, 0.02f), new Vector3(0f, 0f, -25f), a);
            C(p, new Vector3(0f, 0.30f, 0.0f), new Vector3(0.03f, 0.06f, 0.20f), new Vector3(-8f, 0f, 0f), Palette.InkAccent); // bristle ridge
            S(p, new Vector3(0f, 0.24f, 0.18f), Vector3.one * 0.05f, b);                                  // curly tail knot
            Eyes(p, 0.27f, -0.23f, 0.045f, 0.025f);
            Legs(p, 0.08f, -0.08f, 0.11f, 0.08f, 0.055f, a);
            if (warlord)
            {
                Crown(p, new Vector3(0f, 0.36f, -0.15f), 0.12f);
                C(p, new Vector3(0.17f, 0.2f, -0.04f), new Vector3(0.02f, 0.26f, 0.02f), new Vector3(0f, 0f, -10f), Palette.WoodPlank);     // axe haft
                C(p, new Vector3(0.20f, 0.32f, -0.04f), new Vector3(0.10f, 0.07f, 0.015f), new Vector3(0f, 0f, -10f), Palette.AuthorityGold); // golden blade
            }
        }

        static void Rabbit(Transform p, Color a, Color b)
        {
            S(p, new Vector3(0f, 0.15f, 0.03f), new Vector3(0.22f, 0.24f, 0.24f), a);
            S(p, new Vector3(0f, 0.13f, -0.04f), new Vector3(0.14f, 0.16f, 0.12f), b);
            S(p, new Vector3(0f, 0.30f, -0.06f), new Vector3(0.15f, 0.14f, 0.15f), a);
            // the long ears are the silhouette: ~30% past realistic
            C(p, new Vector3(-0.04f, 0.47f, -0.04f), new Vector3(0.045f, 0.22f, 0.025f), new Vector3(-6f, 0f, 9f), a);
            C(p, new Vector3(0.04f, 0.47f, -0.04f), new Vector3(0.045f, 0.22f, 0.025f), new Vector3(-6f, 0f, -9f), a);
            C(p, new Vector3(-0.04f, 0.47f, -0.054f), new Vector3(0.024f, 0.17f, 0.01f), new Vector3(-6f, 0f, 9f), b);
            C(p, new Vector3(0.04f, 0.47f, -0.054f), new Vector3(0.024f, 0.17f, 0.01f), new Vector3(-6f, 0f, -9f), b);
            S(p, new Vector3(0f, 0.28f, -0.14f), new Vector3(0.035f, 0.025f, 0.02f), Palette.InkAccent);
            S(p, new Vector3(0f, 0.14f, 0.16f), Vector3.one * 0.08f, Palette.BoneWhite);                  // cotton tail
            Eyes(p, 0.32f, -0.12f, 0.04f, 0.022f);
            S(p, new Vector3(-0.06f, 0.03f, -0.08f), new Vector3(0.06f, 0.04f, 0.09f), b);                // feet
            S(p, new Vector3(0.06f, 0.03f, -0.08f), new Vector3(0.06f, 0.04f, 0.09f), b);
        }

        static void Platypus(Transform p, Color a, Color b)
        {
            S(p, new Vector3(0f, 0.12f, 0.02f), new Vector3(0.26f, 0.16f, 0.32f), a);                     // low flat body
            S(p, new Vector3(0f, 0.10f, -0.04f), new Vector3(0.18f, 0.10f, 0.20f), b);
            S(p, new Vector3(0f, 0.19f, -0.14f), new Vector3(0.15f, 0.13f, 0.14f), a);
            C(p, new Vector3(0f, 0.17f, -0.26f), new Vector3(0.13f, 0.03f, 0.13f), new Vector3(-6f, 0f, 0f), Palette.InkAccent); // duck bill
            C(p, new Vector3(0f, 0.07f, 0.24f), new Vector3(0.13f, 0.025f, 0.16f), new Vector3(10f, 0f, 0f), b);  // paddle tail
            Eyes(p, 0.22f, -0.20f, 0.045f, 0.022f);
            S(p, new Vector3(-0.1f, 0.02f, -0.1f), new Vector3(0.08f, 0.02f, 0.07f), Palette.InkAccent);  // webbed feet
            S(p, new Vector3(0.1f, 0.02f, -0.1f), new Vector3(0.08f, 0.02f, 0.07f), Palette.InkAccent);
        }

        static void Hood(Transform p, Color b) => S(p, new Vector3(0f, 0.24f, -0.12f), new Vector3(0.17f, 0.12f, 0.16f), Palette.HolyWhite);

        static void Crusader(Transform p, Color a, Color b)
        {
            Cyl(p, new Vector3(0f, 0.28f, -0.14f), new Vector3(0.15f, 0.04f, 0.15f), Vector3.zero, Palette.RuinStoneLight, 0.7f);  // helmet
            C(p, new Vector3(0f, 0.34f, -0.14f), new Vector3(0.02f, 0.08f, 0.02f), Vector3.zero, Palette.RuinStoneLight);          // spike
            C(p, new Vector3(-0.15f, 0.14f, -0.06f), new Vector3(0.03f, 0.16f, 0.13f), new Vector3(0f, -12f, 0f), Palette.HolyWhite); // shield
            C(p, new Vector3(-0.168f, 0.15f, -0.06f), new Vector3(0.01f, 0.10f, 0.025f), new Vector3(0f, -12f, 0f), Palette.AuthorityGold);
            C(p, new Vector3(-0.168f, 0.17f, -0.06f), new Vector3(0.01f, 0.025f, 0.08f), new Vector3(0f, -12f, 0f), Palette.AuthorityGold);
        }

        static void Mitre(Transform p)
        {
            C(p, new Vector3(0f, 0.36f, -0.14f), new Vector3(0.10f, 0.17f, 0.10f), new Vector3(0f, 45f, 0f), Palette.HolyWhite);   // tall mitre
            C(p, new Vector3(0f, 0.36f, -0.195f), new Vector3(0.02f, 0.15f, 0.005f), Vector3.zero, Palette.AuthorityGold);
            C(p, new Vector3(0.16f, 0.24f, -0.06f), new Vector3(0.018f, 0.42f, 0.018f), Vector3.zero, Palette.AuthorityGold);      // crozier
            S(p, new Vector3(0.16f, 0.46f, -0.06f), Vector3.one * 0.05f, Palette.AuthorityGold);
        }

        static void Tanuki(Transform p, Color a, Color b)
        {
            S(p, new Vector3(0f, 0.16f, 0.02f), new Vector3(0.24f, 0.24f, 0.24f), a);
            S(p, new Vector3(0f, 0.14f, -0.05f), new Vector3(0.16f, 0.16f, 0.12f), b);
            S(p, new Vector3(0f, 0.31f, -0.06f), new Vector3(0.18f, 0.16f, 0.16f), a);
            S(p, new Vector3(0f, 0.31f, -0.12f), new Vector3(0.15f, 0.05f, 0.06f), Palette.InkAccent);   // bandit mask
            S(p, new Vector3(0f, 0.27f, -0.15f), new Vector3(0.06f, 0.05f, 0.05f), b);                   // muzzle
            S(p, new Vector3(-0.07f, 0.40f, -0.05f), new Vector3(0.06f, 0.06f, 0.03f), Palette.InkAccent); // round ears
            S(p, new Vector3(0.07f, 0.40f, -0.05f), new Vector3(0.06f, 0.06f, 0.03f), Palette.InkAccent);
            Cap(p, new Vector3(0.09f, 0.17f, 0.18f), new Vector3(0.11f, 0.12f, 0.11f), new Vector3(55f, 0f, -20f), a);            // bushy tail
            S(p, new Vector3(0.14f, 0.25f, 0.27f), Vector3.one * 0.08f, Palette.InkAccent);
            S(p, new Vector3(-0.07f, 0.03f, -0.06f), new Vector3(0.06f, 0.04f, 0.07f), Palette.InkAccent);
            S(p, new Vector3(0.07f, 0.03f, -0.06f), new Vector3(0.06f, 0.04f, 0.07f), Palette.InkAccent);
            S(p, new Vector3(-0.035f, 0.32f, -0.15f), Vector3.one * 0.02f, Palette.BoneWhite);           // eye glints
            S(p, new Vector3(0.035f, 0.32f, -0.15f), Vector3.one * 0.02f, Palette.BoneWhite);
        }

        static void BellyDrum(Transform p, Color b)
        {
            Cyl(p, new Vector3(0f, 0.15f, -0.13f), new Vector3(0.18f, 0.03f, 0.18f), new Vector3(90f, 0f, 0f), b);
            C(p, new Vector3(0.14f, 0.25f, -0.16f), new Vector3(0.015f, 0.12f, 0.015f), new Vector3(0f, 0f, -35f), Palette.WoodPlank);
            S(p, new Vector3(0.17f, 0.30f, -0.16f), Vector3.one * 0.035f, Palette.BoneWhite);
        }

        static void Mouse(Transform p, Color a, Color b, Color ear)
        {
            S(p, new Vector3(0f, 0.14f, 0.03f), new Vector3(0.20f, 0.20f, 0.26f), a);
            S(p, new Vector3(0f, 0.25f, -0.10f), new Vector3(0.14f, 0.13f, 0.17f), a);
            S(p, new Vector3(0f, 0.23f, -0.20f), new Vector3(0.05f, 0.05f, 0.07f), a);                    // pointed snout
            S(p, new Vector3(0f, 0.23f, -0.24f), Vector3.one * 0.025f, Palette.InkAccent);
            // big round ears are the mouse silhouette
            Cyl(p, new Vector3(-0.08f, 0.36f, -0.07f), new Vector3(0.11f, 0.01f, 0.11f), new Vector3(80f, 0f, -20f), ear);
            Cyl(p, new Vector3(0.08f, 0.36f, -0.07f), new Vector3(0.11f, 0.01f, 0.11f), new Vector3(80f, 0f, 20f), ear);
            Cap(p, new Vector3(0f, 0.10f, 0.24f), new Vector3(0.02f, 0.12f, 0.02f), new Vector3(70f, 0f, 0f), a);  // thin tail
            Eyes(p, 0.28f, -0.17f, 0.035f, 0.022f);
            Legs(p, 0.06f, -0.06f, 0.10f, 0.06f, 0.04f, a);
        }

        static void NecroHood(Transform p, Color a, Color b)
        {
            S(p, new Vector3(0f, 0.27f, -0.08f), new Vector3(0.18f, 0.17f, 0.20f), Palette.InkAccent);    // hood
            S(p, new Vector3(0f, 0.12f, 0.03f), new Vector3(0.25f, 0.22f, 0.29f), Palette.InkAccent);     // robe
            C(p, new Vector3(-0.15f, 0.22f, -0.06f), new Vector3(0.018f, 0.44f, 0.018f), Vector3.zero, Palette.BoneWhite); // staff
            S(p, new Vector3(-0.15f, 0.45f, -0.06f), Vector3.one * 0.06f, Palette.MiasmaMist);
        }

        static void Bloat(Transform p, Color a, Color b)
        {
            S(p, new Vector3(0f, 0.22f, 0f), new Vector3(0.40f, 0.38f, 0.40f), a);                        // swollen ball
            S(p, new Vector3(0f, 0.20f, -0.08f), new Vector3(0.26f, 0.24f, 0.26f), b);
            S(p, new Vector3(0.1f, 0.34f, -0.14f), Vector3.one * 0.06f, Palette.MiasmaMist);              // boils
            S(p, new Vector3(-0.13f, 0.28f, -0.12f), Vector3.one * 0.05f, Palette.MiasmaMist);
            Cyl(p, new Vector3(-0.08f, 0.44f, 0f), new Vector3(0.09f, 0.01f, 0.09f), new Vector3(80f, 0f, -20f), a);
            Cyl(p, new Vector3(0.08f, 0.44f, 0f), new Vector3(0.09f, 0.01f, 0.09f), new Vector3(80f, 0f, 20f), a);
            Eyes(p, 0.30f, -0.19f, 0.05f, 0.03f);
        }

        static void Shield(Transform p, Color b, Vector3 at)
        {
            Cyl(p, at, new Vector3(0.16f, 0.015f, 0.16f), new Vector3(90f, 0f, 90f), Palette.RuinStoneLight, 0.6f);
            Cyl(p, at + new Vector3(-0.01f, 0f, 0f), new Vector3(0.07f, 0.016f, 0.07f), new Vector3(90f, 0f, 90f), b);
        }

        static void Helmet(Transform p, Color b, float y)
        {
            S(p, new Vector3(0f, y, -0.10f), new Vector3(0.16f, 0.09f, 0.18f), Palette.RuinStoneLight);
            C(p, new Vector3(0f, y + 0.06f, -0.10f), new Vector3(0.02f, 0.05f, 0.16f), Vector3.zero, b);  // crest
        }

        static void Brute(Transform p, Color a, Color b)
        {
            Mouse(p, a, b, b);
            S(p, new Vector3(0f, 0.20f, -0.02f), new Vector3(0.34f, 0.18f, 0.22f), a);                    // hulking shoulders
            S(p, new Vector3(-0.18f, 0.14f, -0.06f), new Vector3(0.09f, 0.16f, 0.09f), a);                // big fists
            S(p, new Vector3(0.18f, 0.14f, -0.06f), new Vector3(0.09f, 0.16f, 0.09f), a);
            C(p, new Vector3(0.22f, 0.24f, -0.08f), new Vector3(0.025f, 0.30f, 0.025f), new Vector3(0f, 0f, -8f), Palette.RuinStoneLight); // sword
        }

        static void Squirrel(Transform p, Color a, Color b)
        {
            S(p, new Vector3(0f, 0.17f, 0f), new Vector3(0.17f, 0.24f, 0.18f), a);                        // upright body
            S(p, new Vector3(0f, 0.15f, -0.05f), new Vector3(0.11f, 0.16f, 0.09f), b);
            S(p, new Vector3(0f, 0.33f, -0.03f), new Vector3(0.14f, 0.13f, 0.14f), a);
            C(p, new Vector3(-0.045f, 0.43f, -0.02f), new Vector3(0.03f, 0.08f, 0.02f), new Vector3(0f, 0f, 12f), a); // tufted ears
            C(p, new Vector3(0.045f, 0.43f, -0.02f), new Vector3(0.03f, 0.08f, 0.02f), new Vector3(0f, 0f, -12f), a);
            // huge S-curled tail: the squirrel silhouette
            Cap(p, new Vector3(0f, 0.16f, 0.15f), new Vector3(0.12f, 0.13f, 0.12f), new Vector3(-30f, 0f, 0f), a);
            Cap(p, new Vector3(0f, 0.36f, 0.20f), new Vector3(0.13f, 0.12f, 0.13f), new Vector3(20f, 0f, 0f), a);
            S(p, new Vector3(0f, 0.47f, 0.14f), Vector3.one * 0.10f, b);
            S(p, new Vector3(0f, 0.30f, -0.10f), Vector3.one * 0.025f, Palette.InkAccent);
            Eyes(p, 0.35f, -0.09f, 0.04f, 0.02f);
            S(p, new Vector3(0f, 0.20f, -0.10f), Vector3.one * 0.05f, Palette.WoodPlank);                 // acorn in paws
        }

        static void Flame(Transform p, Vector3 at, float size)
        {
            S(p, at, new Vector3(size, size * 1.1f, size), Palette.FireTile);
            S(p, at + new Vector3(0f, size * 0.6f, 0f), new Vector3(size * 0.7f, size, size * 0.7f), Palette.CarrotOrange);
            S(p, at + new Vector3(0f, size * 1.2f, 0f), new Vector3(size * 0.4f, size * 0.7f, size * 0.4f), Palette.EmberYellow);
        }

        static void Capybara(Transform p, Color a, Color b)
        {
            Cap(p, new Vector3(0f, 0.16f, 0.02f), new Vector3(0.24f, 0.18f, 0.24f), new Vector3(90f, 0f, 0f), a); // barrel body
            S(p, new Vector3(0f, 0.12f, -0.02f), new Vector3(0.18f, 0.12f, 0.24f), b);
            C(p, new Vector3(0f, 0.24f, -0.20f), new Vector3(0.15f, 0.14f, 0.18f), new Vector3(8f, 0f, 0f), a);   // big blunt head
            C(p, new Vector3(0f, 0.22f, -0.30f), new Vector3(0.12f, 0.09f, 0.04f), Vector3.zero, b);              // square muzzle
            S(p, new Vector3(-0.035f, 0.24f, -0.32f), Vector3.one * 0.022f, Palette.InkAccent);
            S(p, new Vector3(0.035f, 0.24f, -0.32f), Vector3.one * 0.022f, Palette.InkAccent);
            S(p, new Vector3(-0.06f, 0.32f, -0.16f), Vector3.one * 0.035f, a);                                    // tiny ears
            S(p, new Vector3(0.06f, 0.32f, -0.16f), Vector3.one * 0.035f, a);
            Eyes(p, 0.28f, -0.24f, 0.06f, 0.02f);
            Legs(p, 0.08f, -0.10f, 0.12f, 0.07f, 0.05f, a);
        }

        static void Pack(Transform p, Color b)
        {
            C(p, new Vector3(0f, 0.32f, 0.06f), new Vector3(0.16f, 0.13f, 0.15f), new Vector3(0f, 5f, 0f), Palette.WoodPlank);   // trade pack
            C(p, new Vector3(0f, 0.40f, 0.06f), new Vector3(0.18f, 0.03f, 0.17f), new Vector3(0f, 5f, 0f), b);
            Cyl(p, new Vector3(0f, 0.36f, -0.19f), new Vector3(0.16f, 0.012f, 0.16f), Vector3.zero, Palette.AuthorityGold);      // sun hat brim
            Cyl(p, new Vector3(0f, 0.39f, -0.19f), new Vector3(0.09f, 0.03f, 0.09f), Vector3.zero, Palette.AuthorityGold);
        }

        static void Seed(Transform p, Color a, Color b)
        {
            S(p, new Vector3(0f, 0.07f, 0f), new Vector3(0.14f, 0.12f, 0.14f), Palette.WoodPlank);        // seed bulb
            Cap(p, new Vector3(0f, 0.16f, 0f), new Vector3(0.025f, 0.06f, 0.025f), Vector3.zero, b);       // sprout
            C(p, new Vector3(-0.04f, 0.21f, 0f), new Vector3(0.07f, 0.012f, 0.035f), new Vector3(0f, 0f, 25f), b);
            C(p, new Vector3(0.04f, 0.21f, 0f), new Vector3(0.07f, 0.012f, 0.035f), new Vector3(0f, 0f, -25f), b);
            S(p, new Vector3(0f, 0.25f, 0f), Vector3.one * 0.04f, a);                                      // thorn bud
        }

        static void Briar(Transform p, Color a, Color b, int thorns)
        {
            Cap(p, new Vector3(0f, 0.20f, 0f), new Vector3(0.06f, 0.20f, 0.06f), new Vector3(0f, 0f, 4f), b); // stem
            for (int i = 0; i < 3; i++)
                C(p, new Vector3((i - 1) * 0.07f, 0.08f, 0.02f), new Vector3(0.10f, 0.012f, 0.05f), new Vector3(0f, i * 60f, 18f), b); // leaves
            S(p, new Vector3(0f, 0.44f, 0f), Vector3.one * 0.10f, Palette.EmberYellow);                    // flower heart
            for (int i = 0; i < 6; i++)
            {
                float ang = i * 60f * Mathf.Deg2Rad;
                S(p, new Vector3(Mathf.Cos(ang) * 0.10f, 0.44f, Mathf.Sin(ang) * 0.10f - 0.01f), new Vector3(0.10f, 0.05f, 0.08f), a); // petals
            }
            // thorns ring the stem: one spike per Thorn left (0 = Frenzied Bloom)
            for (int i = 0; i < thorns; i++)
            {
                float ang = i * 60f;
                float y = 0.14f + (i % 3) * 0.07f;
                var dir = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;
                C(p, new Vector3(dir.x * 0.05f, y, dir.z * 0.05f), new Vector3(0.02f, 0.02f, 0.09f), new Vector3(0f, ang, 0f), a);
            }
        }
    }
}
