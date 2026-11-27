using UnityEngine;

namespace CrittersOfCalamity.Visuals
{
    /// <summary>
    /// Art Bible for Critters of Calamity:
    /// Style: Tactile stylized 3D tabletop board game: carved wooden & stone hex tiles, painted miniature critters, warm lantern light over an ancient Anankian map table.
    /// Mood: Warm sunbeam and table-lamp key light (#FFE8C8) over the Continent map with cool forest-canopy shadow bounce (#4E6478).
    /// </summary>
    public static class Palette
    {
        // Tabletop & Environment
        public static readonly Color TableOak        = FromHex("#6E5338"); // Warm carved oak tabletop
        public static readonly Color ParchmentMap    = FromHex("#E5D5B5"); // Ancient Anankian map border & UI backing
        public static readonly Color ShadowSlate     = FromHex("#2C3847"); // Deep slate shadow & abyss

        // Canonical Hex Tile Colors (Rulebook Part 5: Forest=Green, Ruin=Gray, River=Light Blue)
        public static readonly Color ForestTile      = FromHex("#4E7C48"); // Lush emerald moss green
        public static readonly Color RuinTile        = FromHex("#8C8E94"); // Weathered Anankian carved stone gray
        public static readonly Color RiverTile       = FromHex("#68B5D6"); // Clear river cyan-blue

        // Unique Faction Overlay Tiles (Rulebook Part 3 Material 6)
        public static readonly Color MiasmaTile      = FromHex("#6B3FA0"); // Necromouser creeping purple miasma
        public static readonly Color FireTile        = FromHex("#E85D2A"); // Firestarting Squirrels blazing ember orange
        public static readonly Color FallenLeafTile  = FromHex("#B98A3A"); // Trouble-Making Tanukis autumn leaf amber

        // Accents & Resources
        public static readonly Color AuthorityGold   = FromHex("#F2B83B"); // Gold resource & Authority crown accent
        public static readonly Color KeyLightWarm    = FromHex("#FFE8C8"); // Warm afternoon key light
        public static readonly Color AmbientSkyCool  = FromHex("#7892AB"); // Cool sky bounce
        public static readonly Color AmbientEquator  = FromHex("#8C8275"); // Neutral mid
        public static readonly Color AmbientGround   = FromHex("#42362B"); // Dark warm ground bounce

        // Miniature & prop details (every color in the board traces back here)
        public static readonly Color OceanDeep       = FromHex("#2E5D7B"); // Water ring around the Continent
        public static readonly Color CanopyGreen     = FromHex("#3A6335"); // Pine canopy
        public static readonly Color CanopyLight     = FromHex("#5C8A4A"); // Sunlit canopy tips
        public static readonly Color RuinStoneLight  = FromHex("#B0B2B8"); // Carved pillar stone
        public static readonly Color RuinStoneDark   = FromHex("#A2A5AC"); // Fallen arch stone
        public static readonly Color LilyPad         = FromHex("#4F8F4A"); // Stream lily pads and reeds
        public static readonly Color InkAccent       = FromHex("#2A2320"); // Eyes, noses, paw tips, masks
        public static readonly Color BoneWhite       = FromHex("#E8E0CC"); // Skeletons, tusks, cotton tails
        public static readonly Color EmberYellow     = FromHex("#F7C948"); // Flame tips
        public static readonly Color CharredGround   = FromHex("#5A3A2A"); // Ground under Fire Tiles
        public static readonly Color FireGround      = FromHex("#D9481C"); // Fire Tile: reddish orange
        public static readonly Color MiasmaHidden    = FromHex("#452766"); // Facedown Miasma Tile
        public static readonly Color MiasmaMist      = FromHex("#9B7BC9"); // Miasma wisps
        public static readonly Color TrapLeafRed     = FromHex("#9E2A2B"); // Flipped Trap Leaf
        public static readonly Color LeafRust        = FromHex("#A4532A"); // Scattered leaves
        public static readonly Color TarBlack        = FromHex("#1A1714"); // Tar Pit
        public static readonly Color CarrotOrange    = FromHex("#E8822C"); // Gluttony carrot
        public static readonly Color WoodPlank       = FromHex("#8A6440"); // Catapult frame
        public static readonly Color HolyWhite       = FromHex("#F4EEDC"); // Holy Relic, Pope vestments

        // HUD (slate panels from ShadowSlate, warm parchment text, gold accent)
        public static readonly Color UIPanelDark     = WithAlpha(FromHex("#1A222D"), 0.94f); // bars and drawers
        public static readonly Color UIPanelMid      = FromHex("#2A3442");                   // badges, tabs, neutral buttons
        public static readonly Color UIScrim         = WithAlpha(FromHex("#2C3847"), 0.70f); // translucent backing over the board
        public static readonly Color UICard          = FromHex("#3A3026");                   // relic cards in hand
        public static readonly Color UIBattle        = WithAlpha(FromHex("#3A1E22"), 0.95f); // battle result toast
        public static readonly Color UIButtonBlue    = FromHex("#355870");
        public static readonly Color UIButtonTeal    = FromHex("#2E7D6B");
        public static readonly Color UIButtonWarm    = FromHex("#8C5328");
        public static readonly Color UITextMuted     = FromHex("#AAB8C6");

        public static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        public static Color FromHex(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out var color))
                return color;
            return new Color(0.9f, 0.83f, 0.71f);
        }
    }
}
