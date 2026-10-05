using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ArkLeft
{
    internal sealed class ToggleMenuItem : ToolStripMenuItem
    {
        private bool _mousePress;
        private bool _wasOpen;
        internal Control FloatingCircle;
        internal Func<bool> IsCircleMenu;

        public ToggleMenuItem(string text) : base(text) { }

        protected override Point DropDownLocation
        {
            get
            {
                Point native = base.DropDownLocation;
                if (FloatingCircle == null || IsCircleMenu == null || !IsCircleMenu()
                    || !FloatingCircle.Visible) return native;
                return UiStyle.SubmenuLocation(this, FloatingCircle, native);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _mousePress = true;
                _wasOpen = DropDown.Visible;
                if (_wasOpen) return;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            try { base.OnMouseUp(e); }
            finally { _mousePress = false; }
        }

        protected override void OnClick(EventArgs e)
        {
            bool close = _mousePress ? _wasOpen : DropDown.Visible;
            base.OnClick(e);
            if (Owner == null || !Owner.Visible || !HasDropDownItems) return;
            if (close) HideDropDown();
            else ShowDropDown();
        }
    }

    internal static class UiStyle
    {
        public static readonly Color Navy = Color.FromArgb(25, 49, 70);
        public static readonly Color Teal = Color.FromArgb(12, 145, 117);
        public static readonly Color TealLight = Color.FromArgb(226, 244, 238);
        public static readonly Color Canvas = Color.FromArgb(244, 247, 249);
        public static readonly Color Border = Color.FromArgb(222, 230, 234);
        public static readonly Color Muted = Color.FromArgb(101, 117, 128);

        public static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            if (d <= 0 || bounds.Width < d || bounds.Height < d)
            {
                path.AddRectangle(bounds);
                return path;
            }
            path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void StyleButton(Button button, bool primary)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = primary ? Teal : Border;
            button.FlatAppearance.MouseOverBackColor = primary
                ? Color.FromArgb(10, 128, 103) : Color.FromArgb(235, 241, 243);
            button.FlatAppearance.MouseDownBackColor = primary
                ? Color.FromArgb(8, 111, 90) : Color.FromArgb(225, 233, 236);
            button.BackColor = primary ? Teal : Color.White;
            button.ForeColor = primary ? Color.White : Navy;
            button.Cursor = Cursors.Hand;
            button.MinimumSize = new Size(0, 32);
        }

        public static void StyleMenu(ContextMenuStrip menu)
        {
            menu.BackColor = Color.White;
            menu.ForeColor = Navy;
            menu.Padding = new Padding(4);
            menu.Renderer = new ToolStripProfessionalRenderer(new MenuColors());
            menu.ShowImageMargin = false;
            foreach (ToolStripItem item in menu.Items)
            {
                item.Padding = new Padding(8, 5, 8, 5);
            }
        }

        public static void AttachFloatingMenu(ContextMenuStrip menu,
            ToolStripMenuItem settings, ToolStripMenuItem content, Control circle)
        {
            bool circleOpening = false;
            ToggleMenuItem settingsToggle = settings as ToggleMenuItem;
            ToggleMenuItem contentToggle = content as ToggleMenuItem;
            if (settingsToggle != null)
            {
                settingsToggle.FloatingCircle = circle;
                settingsToggle.IsCircleMenu = delegate { return circleOpening; };
            }
            if (contentToggle != null)
            {
                contentToggle.FloatingCircle = circle;
                contentToggle.IsCircleMenu = delegate { return circleOpening; };
            }
            menu.Opening += delegate
            {
                circleOpening = ReferenceEquals(menu.SourceControl, circle) && circle.Visible;
                settings.DropDownDirection = content.DropDownDirection =
                    ToolStripDropDownDirection.Default;
            };
            menu.Closed += delegate { circleOpening = false; };
            menu.Opened += delegate
            {
                if (!circleOpening) return;
                Rectangle work = Screen.FromControl(circle).WorkingArea;
                Rectangle anchor = circle.Bounds;
                bool left = work.Right - anchor.Right < menu.Width + FloatingLayout.Gap
                    && (anchor.Left - work.Left >= menu.Width + FloatingLayout.Gap
                        || anchor.Left - work.Left > work.Right - anchor.Right);
                DetailsPlacement side = left ? DetailsPlacement.Left : DetailsPlacement.Right;
                Rectangle bounds = FloatingLayout.Bounds(side, anchor,
                    new Rectangle(Point.Empty, menu.Size), work);
                if (bounds.IntersectsWith(anchor))
                    bounds = FloatingLayout.Bounds(FloatingLayout.Choose(anchor,
                        new Rectangle(Point.Empty, menu.Size), work), anchor,
                        new Rectangle(Point.Empty, menu.Size), work);
                menu.Location = bounds.Location;
            };
            settings.DropDownOpening += delegate
            {
                if (circleOpening) ChooseSubmenuDirection(settings, circle);
            };
            content.DropDownOpening += delegate
            {
                if (circleOpening) ChooseSubmenuDirection(content, circle);
            };
        }

        private static void ChooseSubmenuDirection(ToolStripMenuItem item, Control circle)
        {
            if (item.Owner == null) return;
            Rectangle work = Screen.FromControl(circle).WorkingArea;
            Rectangle parent = item.Owner.Bounds;
            Size size = item.DropDown.GetPreferredSize(Size.Empty);
            int y = item.Owner.PointToScreen(item.Bounds.Location).Y;
            y = Math.Max(work.Top, Math.Min(y, work.Bottom - size.Height));
            Rectangle rightBounds = new Rectangle(parent.Right, y, size.Width, size.Height);
            Rectangle leftBounds = new Rectangle(parent.Left - size.Width, y, size.Width, size.Height);
            bool rightClear = work.Contains(rightBounds) && !rightBounds.IntersectsWith(circle.Bounds);
            bool leftClear = work.Contains(leftBounds) && !leftBounds.IntersectsWith(circle.Bounds);
            bool left = !rightClear && (leftClear || (work.Right - parent.Right < size.Width
                && (parent.Left - work.Left >= size.Width
                    || parent.Left - work.Left > work.Right - parent.Right)));
            item.DropDownDirection = left ? ToolStripDropDownDirection.Left
                : ToolStripDropDownDirection.Right;
        }

        internal static Point SubmenuLocation(ToolStripMenuItem item, Control circle, Point native)
        {
            Rectangle work = Screen.FromControl(circle).WorkingArea;
            Size size = item.DropDown.GetPreferredSize(Size.Empty);
            int x = Math.Max(work.Left, Math.Min(native.X, work.Right - size.Width));
            int y = Math.Max(work.Top, Math.Min(native.Y, work.Bottom - size.Height));
            if (new Rectangle(x, y, size.Width, size.Height).IntersectsWith(circle.Bounds))
            {
                int above = circle.Top - size.Height - FloatingLayout.Gap;
                int below = circle.Bottom + FloatingLayout.Gap;
                if (above >= work.Top && (below + size.Height > work.Bottom
                    || circle.Top - work.Top >= work.Bottom - circle.Bottom)) y = above;
                else if (below + size.Height <= work.Bottom) y = below;
                else if (above >= work.Top) y = above;
                else
                {
                    int otherX = x < circle.Left ? circle.Right + FloatingLayout.Gap
                        : circle.Left - size.Width - FloatingLayout.Gap;
                    if (otherX >= work.Left && otherX + size.Width <= work.Right)
                        x = otherX;
                }
            }
            return new Point(x, y);
        }

        // Small drawn "settings" glyph for the details header (a gear: a disc
        // with tooth bumps and a transparent hub). Drawn at the requested size
        // so it scales with DPI. No font / image asset dependency; the caller
        // owns the returned Bitmap and must dispose it.
        public static Bitmap CreateSettingIcon(int size, Color color)
        {
            if (size < 8) size = 8;
            Bitmap bmp = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                float c = size / 2f;
                float outer = size * 0.40f;
                float inner = size * 0.20f;
                float tooth = size * 0.115f;
                using (SolidBrush b = new SolidBrush(color))
                {
                    for (int i = 0; i < 8; i++)
                    {
                        double a = Math.PI * i / 4.0;
                        float x = c + (float)Math.Cos(a) * (outer * 0.74f);
                        float y = c + (float)Math.Sin(a) * (outer * 0.74f);
                        g.FillEllipse(b, x - tooth, y - tooth, tooth * 2, tooth * 2);
                    }
                    g.FillEllipse(b, c - outer, c - outer, outer * 2, outer * 2);
                }
                // Punch a transparent hub so the glyph reads as a gear on any
                // background (header is white; no white-on-white assumption).
                g.CompositingMode = CompositingMode.SourceCopy;
                using (SolidBrush hole = new SolidBrush(Color.Transparent))
                    g.FillEllipse(hole, c - inner, c - inner, inner * 2, inner * 2);
                g.CompositingMode = CompositingMode.SourceOver;
            }
            return bmp;
        }

        private sealed class MenuColors : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground { get { return Color.White; } }
            public override Color MenuBorder { get { return Border; } }
            public override Color MenuItemSelected { get { return TealLight; } }
            public override Color MenuItemBorder { get { return Color.FromArgb(204, 229, 220); } }
            public override Color SeparatorDark { get { return Border; } }
            public override Color SeparatorLight { get { return Color.White; } }
        }
    }
}
