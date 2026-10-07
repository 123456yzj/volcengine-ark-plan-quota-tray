using System;
using System.Drawing;

namespace ArkLeft
{
    // Pure bounds computation for the tray-adjacent temporary panel, so the
    // offline smoke test can assert stability without a real screen.
    internal static class PanelPositioner
    {
        public const int LogicalMinWidth = 404;
        public const int LogicalMaxWidth = 440;
        // Two-line period rows need room for the label, a broad bar, and the
        // right-aligned percentage/amount without wrapping at normal DPI.
        public const int LogicalCardWidth = 340;
        public const int LogicalDesiredHeight = 560;
        public const int LogicalMinHeight = 420;
        public const int Margin = 8;

        public static Rectangle Bounds(Rectangle workArea, double scale)
        {
            int w = LayoutMath.ClampWidth(LogicalMaxWidth, scale, workArea.Width);
            int h = LayoutMath.ClampHeight(LogicalDesiredHeight, scale, workArea.Height, LogicalMinHeight);
            int x = workArea.Right - w - (int)Math.Round(Margin * scale);
            int y = workArea.Bottom - h - (int)Math.Round(Margin * scale);
            if (x < workArea.Left) x = workArea.Left;
            if (y < workArea.Top) y = workArea.Top;
            if (x + w > workArea.Right) x = workArea.Right - w;
            if (y + h > workArea.Bottom) y = workArea.Bottom - h;
            return new Rectangle(x, y, w, h);
        }

        public static bool FitsInside(Rectangle bounds, Rectangle workArea)
        {
            return bounds.Left >= workArea.Left && bounds.Top >= workArea.Top
                && bounds.Right <= workArea.Right && bounds.Bottom <= workArea.Bottom;
        }
    }
}
