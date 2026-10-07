using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace ArkLeft
{
    // Placement of the details panel relative to the always-visible circle.
    // Prefers the left; falls back right / above / below; if nothing fits the
    // circle is temporarily hidden so the details never cover it.
    public enum DetailsPlacement { Left, Right, Above, Below, HideCircle }

    // Pure geometry helper so the fallback rule is unit-testable without a
    // screen. Order: Left, Right, Above, Below; HideCircle when none fits.
    public static class FloatingLayout
    {
        public const int Gap = 8;

        public static DetailsPlacement Choose(Rectangle circle, Rectangle details,
            Rectangle work)
        {
            if (Fits(DetailsPlacement.Left, circle, details, work)) return DetailsPlacement.Left;
            if (Fits(DetailsPlacement.Right, circle, details, work)) return DetailsPlacement.Right;
            if (Fits(DetailsPlacement.Above, circle, details, work)) return DetailsPlacement.Above;
            if (Fits(DetailsPlacement.Below, circle, details, work)) return DetailsPlacement.Below;
            return DetailsPlacement.HideCircle;
        }

        public static Rectangle Bounds(DetailsPlacement p, Rectangle circle, Rectangle details,
            Rectangle work)
        {
            return Clamp(RawBounds(p, circle, details), work);
        }

        private static Rectangle RawBounds(DetailsPlacement p, Rectangle circle, Rectangle details)
        {
            int x, y;
            switch (p)
            {
                case DetailsPlacement.Right: x = circle.Right + Gap; y = circle.Top; break;
                case DetailsPlacement.Above: x = circle.Left; y = circle.Top - details.Height - Gap; break;
                case DetailsPlacement.Below: x = circle.Left; y = circle.Bottom + Gap; break;
                case DetailsPlacement.HideCircle:
                case DetailsPlacement.Left:
                default: x = circle.Left - details.Width - Gap; y = circle.Top; break;
            }
            return new Rectangle(x, y, details.Width, details.Height);
        }

        // A side is usable when its work-area-clamped rectangle stays fully
        // inside the work area (so it does not spill onto another screen) and
        // does NOT intersect the circle. Vertical clamping is expected: a circle
        // near the bottom still allows a LEFT/ABOVE placement that is pulled up.
        private static bool Fits(DetailsPlacement p, Rectangle circle, Rectangle details,
            Rectangle work)
        {
            Rectangle b = Clamp(RawBounds(p, circle, details), work);
            return b.Left >= work.Left && b.Top >= work.Top
                && b.Right <= work.Right && b.Bottom <= work.Bottom
                && !b.IntersectsWith(circle);
        }

        private static Rectangle Clamp(Rectangle b, Rectangle work)
        {
            int x = b.X, y = b.Y;
            if (x + b.Width > work.Right) x = work.Right - b.Width;
            if (y + b.Height > work.Bottom) y = work.Bottom - b.Height;
            if (x < work.Left) x = work.Left;
            if (y < work.Top) y = work.Top;
            return new Rectangle(x, y, b.Width, b.Height);
        }
    }

}
