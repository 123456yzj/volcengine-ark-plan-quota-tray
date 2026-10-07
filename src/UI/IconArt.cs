using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace ArkLeft
{
    // Draws the tray / application icon with GDI+ and releases every native handle.
    internal static class IconArt
    {
        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public static Icon CreateIcon(int size)
        {
            using (Bitmap bmp = Draw(size))
            {
                IntPtr h = bmp.GetHicon();
                try
                {
                    using (Icon tmp = Icon.FromHandle(h))
                    {
                        return (Icon)tmp.Clone();
                    }
                }
                finally
                {
                    DestroyIcon(h);
                }
            }
        }

        private static Bitmap Draw(int size)
        {
            Bitmap bmp = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                Rectangle r = new Rectangle(1, 1, size - 2, size - 2);
                using (SolidBrush bg = new SolidBrush(UiStyle.Primary))
                using (GraphicsPath path = RoundRect(r, (int)(size * 0.28)))
                {
                    g.FillPath(bg, path);
                }

                float w = size;
                float bodyW = w * 0.42f;
                float bodyH = w * 0.40f;
                float cx = w / 2f;
                float top = w * 0.22f;
                using (GraphicsPath drop = new GraphicsPath())
                {
                    drop.AddBezier(cx, top,
                                   cx - bodyW * 0.10f, top + bodyH * 0.55f,
                                   cx - bodyW * 0.55f, top + bodyH * 0.55f,
                                   cx - bodyW * 0.55f, top + bodyH * 1.05f);
                    drop.AddArc(cx - bodyW * 0.55f, top + bodyH * 0.62f, bodyW * 1.10f, bodyH * 0.90f, 150f, 240f);
                    drop.CloseFigure();
                    using (SolidBrush white = new SolidBrush(Color.White))
                        g.FillPath(white, drop);
                }
            }
            return bmp;
        }

        private static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            GraphicsPath p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
