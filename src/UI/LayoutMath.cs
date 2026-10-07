using System;

namespace ArkLeft
{
    // Pure layout math so it can be unit-tested without a screen.
    internal static class LayoutMath
    {
        public static int DesiredHeight(int chrome, int content, int minContent, int workHeight)
        {
            int desired = chrome + content;
            int cap = (int)(workHeight * 0.7);
            if (desired > cap) desired = cap;
            int min = chrome + minContent;
            if (min > workHeight) min = workHeight;
            if (desired < min) desired = min;
            if (desired > workHeight) desired = workHeight;
            if (desired < 1) desired = 1;
            return desired;
        }

        // Fixed temporary-panel size, converged into the work area.
        public static int ClampWidth(int logical, double scale, int workWidth)
        {
            int w = (int)Math.Round(logical * scale);
            if (workWidth > 0 && w > workWidth) w = workWidth;
            if (w < 1) w = 1;
            return w;
        }

        public static int ClampHeight(int desiredLogical, double scale, int workHeight, int minLogical)
        {
            int h = (int)Math.Round(desiredLogical * scale);
            int min = (int)Math.Round(minLogical * scale);
            if (workHeight > 0 && h > workHeight) h = workHeight;
            if (h < min) h = min;
            if (workHeight > 0 && h > workHeight) h = workHeight;
            if (h < 1) h = 1;
            return h;
        }
    }
}
