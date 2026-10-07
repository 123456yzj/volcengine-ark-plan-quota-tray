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
        public static readonly Color Navy = Color.FromArgb(36, 65, 92);
        public static readonly Color Primary = Color.FromArgb(58, 131, 247);
        public static readonly Color Teal = Primary;
        public static readonly Color TealLight = Color.FromArgb(234, 242, 255);
        public static readonly Color Canvas = Color.FromArgb(248, 251, 255);
        public static readonly Color Border = Color.FromArgb(216, 227, 240);
        public static readonly Color StrongBorder = Color.FromArgb(196, 212, 232);
        public static readonly Color Divider = Color.FromArgb(229, 238, 245);
        public static readonly Color Muted = Color.FromArgb(108, 129, 149);
        public static readonly Color Weak = Color.FromArgb(145, 162, 178);
        public static readonly Color Secondary = Color.FromArgb(242, 247, 255);
        public static readonly Color Selected = Color.FromArgb(216, 232, 255);
        public static readonly Color Track = Color.FromArgb(220, 233, 247);
        public static readonly Color Sky = Primary;
        public static readonly Color PrimaryButton = Primary;
        public static readonly Color PrimaryHover = Color.FromArgb(47, 114, 232);
        public static readonly Color PrimaryPressed = Color.FromArgb(40, 100, 211);
        public static readonly Color Water = Color.FromArgb(90, 154, 248);
        public static readonly Color SecondaryBlue = Color.FromArgb(109, 166, 250);
        public static readonly Color HighlightBlue = Color.FromArgb(187, 215, 253);
        public static readonly Color CircleRing = Color.FromArgb(175, 199, 226);
        public static readonly Color CircleHover = Color.FromArgb(108, 159, 226);
        public static readonly Color CircleNumber = Color.FromArgb(24, 62, 99);
        public static readonly Color CircleCaption = Color.FromArgb(69, 98, 122);
        public static readonly Color Success = Color.FromArgb(34, 197, 94);
        public static readonly Color Warning = Color.FromArgb(246, 166, 35);
        public static readonly Color Error = Color.FromArgb(224, 91, 101);

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
            button.FlatAppearance.BorderColor = primary ? PrimaryButton : Border;
            button.FlatAppearance.MouseOverBackColor = primary
                ? PrimaryHover : Secondary;
            button.FlatAppearance.MouseDownBackColor = primary
                ? PrimaryPressed : Selected;
            button.BackColor = primary ? PrimaryButton : Color.White;
            button.ForeColor = primary ? Color.White : Navy;
            button.Cursor = Cursors.Hand;
            button.MinimumSize = new Size(0, 32);
            ModernButton modern = button as ModernButton;
            if (modern != null) modern.Primary = primary;
        }

        public static void StyleMenu(ContextMenuStrip menu)
        {
            menu.ShowImageMargin = false;
            StyleMenuLevel(menu);
        }

        internal static void StyleMenuLevel(ToolStripDropDown menu)
        {
            ContextMenuStrip root = menu as ContextMenuStrip;
            if (root != null) root.ShowImageMargin = false;
            menu.BackColor = Color.White;
            menu.ForeColor = Navy;
            menu.Padding = new Padding(4);
            menu.Renderer = new ModernMenuRenderer();
            foreach (ToolStripItem item in menu.Items)
            {
                item.Padding = new Padding(8, 5, 8, 5);
                item.ForeColor = Navy;
                ToolStripMenuItem child = item as ToolStripMenuItem;
                if (child != null && child.HasDropDownItems) StyleMenuLevel(child.DropDown);
            }
        }

        internal static void StyleMenuBranch(ToolStripMenuItem item)
        {
            ToolStripDropDown root = item.DropDown;
            while (root.OwnerItem != null && root.OwnerItem.Owner is ToolStripDropDown)
                root = (ToolStripDropDown)root.OwnerItem.Owner;
            StyleMenuLevel(root);
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
            public override Color MenuItemBorder { get { return TealLight; } }
            public override Color MenuItemSelectedGradientBegin { get { return TealLight; } }
            public override Color MenuItemSelectedGradientEnd { get { return TealLight; } }
            public override Color MenuItemPressedGradientBegin { get { return Selected; } }
            public override Color MenuItemPressedGradientMiddle { get { return Selected; } }
            public override Color MenuItemPressedGradientEnd { get { return Selected; } }
            public override Color CheckBackground { get { return Selected; } }
            public override Color CheckSelectedBackground { get { return Selected; } }
            public override Color CheckPressedBackground { get { return Selected; } }
            public override Color ButtonCheckedGradientBegin { get { return TealLight; } }
            public override Color ButtonCheckedGradientMiddle { get { return TealLight; } }
            public override Color ButtonCheckedGradientEnd { get { return TealLight; } }
            public override Color SeparatorDark { get { return Divider; } }
            public override Color SeparatorLight { get { return Color.White; } }
        }

        private sealed class ModernMenuRenderer : ToolStripProfessionalRenderer
        {
            internal ModernMenuRenderer() : base(new MenuColors()) { RoundedEdges = false; }
            protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }
            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                ToolStripMenuItem item = e.Item as ToolStripMenuItem;
                Color fill = e.Item.Pressed || (item != null && item.Checked)
                    ? Selected : e.Item.Selected ? TealLight : Color.White;
                using (SolidBrush brush = new SolidBrush(fill))
                    e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.Item.Size));
            }
            protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
            {
                e.ArrowColor = e.Item.Enabled ? Navy : Weak;
                base.OnRenderArrow(e);
            }
            protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
            {
                Rectangle r = e.ImageRectangle;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen pen = new Pen(e.Item.Enabled ? Primary : Weak, 1.8f))
                    e.Graphics.DrawLines(pen, new PointF[] {
                        new PointF(r.Left + r.Width * 0.2f, r.Top + r.Height * 0.5f),
                        new PointF(r.Left + r.Width * 0.43f, r.Top + r.Height * 0.72f),
                        new PointF(r.Left + r.Width * 0.8f, r.Top + r.Height * 0.28f) });
            }
            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = e.Item.Enabled ? Navy : Weak;
                base.OnRenderItemText(e);
            }
        }
    }

    // Button semantics (default action, keyboard, accessibility and Click) stay
    // with WinForms; only the surface is drawn here.
    internal class ModernButton : Button
    {
        internal bool Primary;
        private bool _hover, _pressed;
        public ModernButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }
        protected override void OnMouseEnter(EventArgs e) { _hover = true; base.OnMouseEnter(e); Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { _hover = _pressed = false; base.OnMouseLeave(e); Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) _pressed = true; base.OnMouseDown(e); Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; base.OnMouseUp(e); Invalidate(); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) _pressed = true; base.OnKeyDown(e); Invalidate(); }
        protected override void OnKeyUp(KeyEventArgs e) { _pressed = false; base.OnKeyUp(e); Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { _pressed = _hover = false; base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent == null ? Color.White : Parent.BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color fill = !Enabled ? (Primary ? UiStyle.Track : UiStyle.Canvas)
                : _pressed ? FlatAppearance.MouseDownBackColor
                : _hover ? FlatAppearance.MouseOverBackColor : BackColor;
            int radius = Math.Max(2, (int)Math.Round(Height * 6.0 / 34.0));
            using (GraphicsPath path = UiStyle.RoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), radius))
            {
                using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, path);
                if (FlatAppearance.BorderSize > 0)
                    using (Pen p = new Pen(Primary ? fill : UiStyle.Border)) g.DrawPath(p, path);
            }
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, Enabled ? ForeColor : UiStyle.Weak,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            if (Focused && ShowFocusCues)
                ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(ClientRectangle, -5, -5), ForeColor, fill);
        }
    }

    internal class ModernComboBox : ComboBox
    {
        private bool _hover;
        public ModernComboBox()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            FlatStyle = FlatStyle.Flat;
        }
        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            ItemHeight = Math.Max(18, Font.Height + 6);
        }
        protected override void OnMouseEnter(EventArgs e) { _hover = true; base.OnMouseEnter(e); Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; base.OnMouseLeave(e); Invalidate(); }
        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            bool selected = (e.State & DrawItemState.Selected) != 0;
            bool closed = (e.State & DrawItemState.ComboBoxEdit) != 0;
            selected = selected && !closed;
            Color bg = !Enabled ? UiStyle.Canvas : selected ? UiStyle.Selected : Color.White;
            using (SolidBrush b = new SolidBrush(bg)) e.Graphics.FillRectangle(b, e.Bounds);
            string text = e.Index >= 0 && e.Index < Items.Count ? Items[e.Index].ToString() : Text;
            Rectangle bounds = e.Bounds; bounds.X += 4; bounds.Width -= 8;
            TextRenderer.DrawText(e.Graphics, text, Font, bounds, !Enabled ? UiStyle.Weak
                : selected ? UiStyle.Primary : UiStyle.Navy, TextFormatFlags.Left
                | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            if (!closed && (e.State & DrawItemState.Focus) != 0) e.DrawFocusRectangle();
        }
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            bool print = m.Msg == 0x0317 || m.Msg == 0x0318;
            if ((m.Msg != 0x000F && !print) || Width < 2 || Height < 2) return;
            using (Graphics g = print ? Graphics.FromHdc(m.WParam) : CreateGraphics())
            using (Pen p = new Pen(Focused || DroppedDown ? UiStyle.Primary
                : _hover ? UiStyle.CircleHover : UiStyle.StrongBorder))
            {
                int arrowWidth = SystemInformation.VerticalScrollBarWidth + 2;
                Rectangle arrow = new Rectangle(Width - arrowWidth - 1, 1, arrowWidth, Height - 2);
                using (SolidBrush bg = new SolidBrush(DroppedDown ? UiStyle.Selected
                    : _hover ? UiStyle.TealLight : UiStyle.Secondary))
                    g.FillRectangle(bg, arrow);
                int cx = arrow.Left + arrow.Width / 2, cy = Height / 2;
                using (Pen ink = new Pen(Enabled ? UiStyle.Primary : UiStyle.Weak, 1.5f))
                    g.DrawLines(ink, new Point[] { new Point(cx - 4, cy - 2), new Point(cx, cy + 2), new Point(cx + 4, cy - 2) });
                g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            }
        }
    }
}
