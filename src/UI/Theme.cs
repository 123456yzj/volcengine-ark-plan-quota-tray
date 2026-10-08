using System;
using System.Drawing;

namespace ArkLeft
{
    // Identity-free theme choice: which accent preset and whether the content
    // uses the dark surface. Persisted with the other UI preferences (never
    // coordinates, identity, credentials or quota values).
    internal sealed class ThemeChoice
    {
        public int AccentIndex;
        public bool DarkMode;

        public ThemeChoice() { }

        public ThemeChoice(int accentIndex, bool darkMode)
        {
            AccentIndex = ThemeCatalog.Normalize(accentIndex);
            DarkMode = darkMode;
        }

        public override bool Equals(object obj)
        {
            ThemeChoice other = obj as ThemeChoice;
            if (other == null) return false;
            return other.AccentIndex == AccentIndex && other.DarkMode == DarkMode;
        }

        public override int GetHashCode()
        {
            return AccentIndex * 2 + (DarkMode ? 1 : 0);
        }
    }

    // A complete, resolved color set for the whole UI. The default (accent 0,
    // light) values are EXACTLY the historical constants so the shipped look
    // and every existing visual assertion are unchanged; other accents / dark
    // mode are derived from the accent plus the neutral surfaces.
    internal sealed class ThemePalette
    {
        public int AccentIndex;
        public bool DarkMode;

        public Color Navy;
        public Color Primary;
        public Color Teal;
        public Color TealLight;
        public Color OnAccent;
        public Color Canvas;
        public Color Surface;
        public Color Border;
        public Color StrongBorder;
        public Color Divider;
        public Color Muted;
        public Color Weak;
        public Color Secondary;
        public Color Selected;
        public Color Track;
        public Color Sky;
        public Color PrimaryButton;
        public Color PrimaryHover;
        public Color PrimaryPressed;
        public Color Water;
        public Color SecondaryBlue;
        public Color HighlightBlue;
        public Color CircleRing;
        public Color CircleHover;
        public Color CircleNumber;
        public Color CircleCaption;
        public Color CircleBackground;
        public Color Success;
        public Color Warning;
        public Color Error;
    }

    internal static class ThemeCatalog
    {
        public const int DefaultAccentIndex = 0;

        private struct AccentDef
        {
            public string Name;
            public Color Color;
            public AccentDef(string name, Color color) { Name = name; Color = color; }
        }

        // Order matters: index 0 is the historical default accent and is stored
        // in floating-preferences.json, so it must never be reordered.
        private static readonly AccentDef[] Accents = new AccentDef[]
        {
            new AccentDef("默认蓝", Color.FromArgb(58, 131, 247)),
            new AccentDef("青绿", Color.FromArgb(16, 150, 128)),
            new AccentDef("翠绿", Color.FromArgb(38, 162, 92)),
            new AccentDef("紫罗兰", Color.FromArgb(124, 96, 232)),
            new AccentDef("暖橙", Color.FromArgb(224, 122, 38)),
            new AccentDef("玫红", Color.FromArgb(214, 66, 102)),
        };

        public static int Count { get { return Accents.Length; } }

        public static string Name(int index)
        {
            if (index < 0 || index >= Accents.Length) index = DefaultAccentIndex;
            return Accents[index].Name;
        }

        public static Color Accent(int index)
        {
            if (index < 0 || index >= Accents.Length) index = DefaultAccentIndex;
            return Accents[index].Color;
        }

        // Corrupt / stale / out-of-range indices fall back to the default accent
        // instead of fabricating or crashing.
        public static int Normalize(int index)
        {
            return index < 0 || index >= Accents.Length ? DefaultAccentIndex : index;
        }
    }

    internal static class ThemePaletteFactory
    {
        public static ThemePalette Build(int accentIndex, bool dark)
        {
            accentIndex = ThemeCatalog.Normalize(accentIndex);
            ThemePalette p = new ThemePalette();
            p.AccentIndex = accentIndex;
            p.DarkMode = dark;

            if (dark)
            {
                p.Navy = Color.FromArgb(232, 236, 242);      // text on surface
                p.Canvas = Color.FromArgb(28, 31, 36);
                p.Surface = Color.FromArgb(40, 44, 52);
                p.Border = Color.FromArgb(64, 70, 80);
                p.StrongBorder = Color.FromArgb(86, 94, 106);
                p.Divider = Color.FromArgb(54, 59, 68);
                p.Muted = Color.FromArgb(150, 158, 170);
                p.Weak = Color.FromArgb(116, 124, 136);
                p.Success = Color.FromArgb(52, 199, 110);
                p.Warning = Color.FromArgb(240, 176, 64);
                p.Error = Color.FromArgb(232, 106, 116);
                p.OnAccent = Color.White;
            }
            else
            {
                p.Navy = Color.FromArgb(36, 65, 92);
                p.Canvas = Color.FromArgb(248, 251, 255);
                p.Surface = Color.White;
                p.Border = Color.FromArgb(216, 227, 240);
                p.StrongBorder = Color.FromArgb(196, 212, 232);
                p.Divider = Color.FromArgb(229, 238, 245);
                p.Muted = Color.FromArgb(108, 129, 149);
                p.Weak = Color.FromArgb(145, 162, 178);
                p.Success = Color.FromArgb(34, 197, 94);
                p.Warning = Color.FromArgb(246, 166, 35);
                p.Error = Color.FromArgb(224, 91, 101);
                p.OnAccent = Color.White;
            }

            Color accent = ThemeCatalog.Accent(accentIndex);
            p.Primary = accent;
            p.Teal = accent;
            p.Sky = accent;
            p.PrimaryButton = accent;
            p.CircleBackground = p.Surface;

            if (accentIndex == ThemeCatalog.DefaultAccentIndex && !dark)
            {
                // Frozen historical constants: keeps the shipped default pixel
                // exact and every existing visual assertion valid.
                p.TealLight = Color.FromArgb(234, 242, 255);
                p.Secondary = Color.FromArgb(242, 247, 255);
                p.Selected = Color.FromArgb(216, 232, 255);
                p.Track = Color.FromArgb(220, 233, 247);
                p.PrimaryHover = Color.FromArgb(47, 114, 232);
                p.PrimaryPressed = Color.FromArgb(40, 100, 211);
                p.Water = Color.FromArgb(90, 154, 248);
                p.SecondaryBlue = Color.FromArgb(109, 166, 250);
                p.HighlightBlue = Color.FromArgb(187, 215, 253);
                p.CircleRing = Color.FromArgb(175, 199, 226);
                p.CircleHover = Color.FromArgb(108, 159, 226);
                p.CircleNumber = Color.FromArgb(24, 62, 99);
                p.CircleCaption = Color.FromArgb(69, 98, 122);
                return p;
            }

            Color surface = p.Surface;
            p.TealLight = Mix(surface, accent, dark ? 0.22 : 0.10);
            p.Secondary = Mix(surface, accent, dark ? 0.14 : 0.055);
            p.Selected = Mix(surface, accent, dark ? 0.32 : 0.18);
            p.Track = Mix(surface, accent, dark ? 0.26 : 0.12);
            p.PrimaryHover = Shade(accent, dark ? 1.12 : 0.90);
            p.PrimaryPressed = Shade(accent, dark ? 0.86 : 0.80);
            p.Water = Mix(accent, Color.White, dark ? 0.30 : 0.18);
            p.SecondaryBlue = Mix(accent, Color.White, 0.35);
            p.HighlightBlue = Mix(accent, Color.White, 0.65);
            p.CircleRing = Mix(accent, surface, 0.62);
            p.CircleHover = Mix(accent, surface, 0.30);
            p.CircleNumber = dark ? Color.FromArgb(236, 240, 246) : Color.FromArgb(24, 62, 99);
            p.CircleCaption = dark ? Color.FromArgb(176, 184, 196) : Color.FromArgb(69, 98, 122);
            return p;
        }

        // Linear interpolation: t=0 -> a, t=1 -> b.
        private static Color Mix(Color a, Color b, double t)
        {
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            return Color.FromArgb(
                Clamp(a.R + (b.R - a.R) * t),
                Clamp(a.G + (b.G - a.G) * t),
                Clamp(a.B + (b.B - a.B) * t));
        }

        private static Color Shade(Color c, double factor)
        {
            return Color.FromArgb(
                Clamp(c.R * factor), Clamp(c.G * factor), Clamp(c.B * factor));
        }

        private static int Clamp(double value)
        {
            int v = (int)Math.Round(value);
            if (v < 0) return 0;
            if (v > 255) return 255;
            return v;
        }
    }
}
