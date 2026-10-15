using UnityEngine;

namespace TheLastFortress
{
    /// <summary>Working palette from design/README.md.</summary>
    public static class Palette
    {
        public static readonly Color Grass = Hex(0x87935D);
        public static readonly Color DarkGrass = Hex(0x62724A);
        public static readonly Color Timber = Hex(0x996D43);
        public static readonly Color TimberShadow = Hex(0x634830);
        public static readonly Color Limestone = Hex(0xD8CCAE);
        public static readonly Color WaterDay = Hex(0x507E98);
        public static readonly Color NightAmbient = Hex(0x314B70);
        public static readonly Color EnemyBody = Hex(0x303538);
        public static readonly Color EnemyCloth = Hex(0xAE5747);
        public static readonly Color Amber = Hex(0xE9B45F);
        public static readonly Color RouteCyan = Hex(0x79C7C7);
        public static readonly Color WarningCoral = Hex(0xE67961);
        public static readonly Color HudSurface = Hex(0x25323A);
        public static readonly Color HudText = Hex(0xF0E7D5);

        public static Color Hex(int rgb, float alpha = 1f) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);

        public static Color WithAlpha(this Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
