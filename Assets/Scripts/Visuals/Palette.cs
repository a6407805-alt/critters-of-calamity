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
        public static readonly Color GlacierTile     = FromHex("#B8E3F8"); // Glacier Kingdom frost crystal blue
        public static readonly Color FallenLeafTile  = FromHex("#C97A36"); // Trouble-Making Tanukis autumn leaf amber

        // Accents & Resources
        public static readonly Color AuthorityGold   = FromHex("#F2B83B"); // Gold resource & Authority crown accent
        public static readonly Color KeyLightWarm    = FromHex("#FFE8C8"); // Warm afternoon key light
        public static readonly Color AmbientSkyCool  = FromHex("#7892AB"); // Cool sky bounce
        public static readonly Color AmbientEquator  = FromHex("#8C8275"); // Neutral mid
        public static readonly Color AmbientGround   = FromHex("#42362B"); // Dark warm ground bounce

        public static Color FromHex(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out var color))
                return color;
            return new Color(0.9f, 0.83f, 0.71f);
        }
    }
}
