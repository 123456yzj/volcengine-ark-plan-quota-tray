using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ArkLeft
{
    internal static class DpiUtil
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

        public static double GetScale(Screen scr)
        {
            try
            {
                POINT p;
                p.X = scr.WorkingArea.Left + 1;
                p.Y = scr.WorkingArea.Top + 1;
                IntPtr mon = MonitorFromPoint(p, 2 /* NEAREST */);
                if (mon != IntPtr.Zero)
                {
                    uint dx, dy;
                    if (GetDpiForMonitor(mon, 0 /* EFFECTIVE */, out dx, out dy) == 0 && dx > 0)
                        return dx / 96.0;
                }
            }
            catch (Exception) { }
            try
            {
                using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) return g.DpiX / 96.0;
            }
            catch (Exception) { }
            return 1.0;
        }
    }
}
