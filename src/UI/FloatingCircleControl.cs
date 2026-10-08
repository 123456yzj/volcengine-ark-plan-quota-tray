using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace ArkLeft
{
    // Immutable render instruction for the circle control. HasData=false means
    // "暂无数据"; PercentKnown=false (with HasData) means "剩余未知" and no wave.
    internal class FloatingDisplay
    {
        public bool HasData;
        public bool PercentKnown;
        public double Percent;
        public string PercentText;
        public bool AmountKnown;
        public double RemainingAmount;
        public string Tooltip;
    }

    // The self-painting circle. Top-most, border-less, no taskbar item, with an
    // elliptical Region and per-pixel alpha so the edge is smooth and clicks
    // outside the ellipse really pass through. Owns
    // only a light repaint timer (never a query). All GDI objects / fonts / the
    // region and timer are released in Dispose.
    internal class FloatingCircleControl : Form
    {
        public event EventHandler DetailsRequested;
        public event EventHandler DragStarted;
        // Raised when the circle is hidden by an explicit user action (Esc /
        // WM_CLOSE). The owner clears its layout auto-hide flag so a later
        // details-close cannot pull the circle back.
        public event EventHandler ExplicitHideRequested;

        private const int LogicalDiameter = 112;
        private const int LogicalMargin = 16;
        private const int DragThresholdLogical = 4;

        private readonly Timer _wave = new Timer();
        private Font _percentFont;
        private Font _captionFont;
        private Region _region;
        private double _scale = 1.0;
        private float _phase;
        private bool _allowClose;
        private bool _hovered;

        private bool _hasData;
        private bool _percentKnown;
        private double _percent;
        private string _percentText = "暂无数据";
        private bool _amountKnown;
        private double _amount;
        private string _amountText = "";

        private bool _down;
        private bool _dragging;
        private Point _downScreen;
        private Point _downLocation;
        private bool _hasTrayDown;
        private bool _trayDownVisible;
        // v0.8 UX016: when locked, mouse gestures never move the circle; the
        // drag threshold is still detected so a release after a
        // threshold-crossing gesture is swallowed instead of opening details.
        private bool _positionLocked;
        // v0.9 UX017: when set, the wave animation timer stays stopped and the
        // surface paints a static wave; display values are never changed.
        private bool _reduceMotion;
        // v0.9 UX017: set in Dispose; the wave timer must never be restarted
        // after the control is disposed.
        private bool _waveDead;

        public bool AllowClose
        {
            get { return _allowClose; }
            set { _allowClose = value; }
        }

        // v0.8 UX016: the lock only blocks dragging. Left click / Enter /
        // Space (details), the right-click menu and show / hide keep working.
        // Toggling mid-gesture releases capture and resets the gesture so a
        // stale drag can never continue under the new state (and cannot
        // mis-open details on release).
        public bool PositionLocked
        {
            get { return _positionLocked; }
            set
            {
                if (_positionLocked == value) return;
                _positionLocked = value;
                if (_down)
                {
                    _down = false;
                    _dragging = false;
                    Capture = false;
                }
            }
        }

        // v0.9 UX017: reduce motion freezes the wave animation. The timer is
        // stopped and the surface is repainted once as a static wave; the
        // shown ratio / percent (0 / 100 / unknown) is untouched. State is
        // owned by the handler (FloatingQuotaForm), mirroring the lock.
        public bool ReduceMotion { get { return _reduceMotion; } }

        public void SetReduceMotion(bool on)
        {
            if (_reduceMotion == on) return;
            _reduceMotion = on;
            UpdateWaveState();
            if (Visible) Invalidate(); // immediate static repaint, no timer
        }

        private void RaiseExplicitHide()
        {
            if (ExplicitHideRequested != null) ExplicitHideRequested(this, EventArgs.Empty);
        }

        public FloatingCircleControl()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            // Override WinForms' default minimum top-level window width so a
            // compact circle stays square at 100% DPI.
            MinimumSize = new Size(1, 1);
            KeyPreview = true;
            BackColor = Color.White;
            DoubleBuffered = true;
            Text = "方舟剩余额度";
            AccessibleName = "方舟剩余额度悬浮圆圈";
            AccessibleDescription = "暂无数据";
            BuildFonts(1.0);
            _wave.Interval = 60;
            _wave.Tick += delegate { _phase += 0.22f; if (Visible) Invalidate(); };
            MouseDown += OnCircleMouseDown;
            MouseMove += OnCircleMouseMove;
            MouseUp += OnCircleMouseUp;
            MouseEnter += delegate { _hovered = true; Invalidate(); };
            MouseLeave += delegate { _hovered = false; Invalidate(); };
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }

        // ---- placement ----

        public static Rectangle InitialBounds(Rectangle workArea, double scale)
        {
            if (scale <= 0) scale = 1.0;
            int d = (int)Math.Round(LogicalDiameter * scale);
            int m = (int)Math.Round(LogicalMargin * scale);
            int x = workArea.Right - d - m;
            int y = workArea.Bottom - d - m;
            return ClampTo(new Rectangle(x, y, d, d), workArea);
        }

        public static Rectangle ClampTo(Rectangle b, Rectangle workArea)
        {
            int x = b.X, y = b.Y;
            if (x < workArea.Left) x = workArea.Left;
            if (y < workArea.Top) y = workArea.Top;
            if (b.Width <= workArea.Width && x + b.Width > workArea.Right)
                x = workArea.Right - b.Width;
            if (b.Height <= workArea.Height && y + b.Height > workArea.Bottom)
                y = workArea.Bottom - b.Height;
            return new Rectangle(x, y, b.Width, b.Height);
        }

        public void ShowAt(Rectangle workArea, double scale)
        {
            _scale = scale <= 0 ? 1.0 : scale;
            BuildFonts(_scale);
            Bounds = InitialBounds(workArea, _scale);
            if (!Visible) Show();
            ApplyRegion();
            UpdateWaveState();
            Invalidate();
        }

        // v0.12 UX020: explicit "归位" (re-home). Unlike the once-per-session
        // first placement inside FloatingQuotaForm.ShowCircle, this ALWAYS
        // applies the given work area / scale (never ignores them), cancels
        // any in-progress drag first (releasing capture so a stale mouse move
        // can neither drag the circle back nor mis-open details) and shows
        // the circle. The position lock does not block it; nothing persists.
        public void MoveHome(Rectangle workArea, double scale)
        {
            ResetDragGesture();
            ShowAt(workArea, scale);
        }

        // v0.12 UX020: same gesture reset as the mid-gesture lock toggle —
        // no capture, no pending down / drag.
        internal void ResetDragGesture()
        {
            _down = false;
            _dragging = false;
            try { Capture = false; } catch (Exception) { }
        }

        // Fonts scale with DPI so the text keeps its relative size; rebuilt on
        // ShowAt (per open screen) and released in Dispose.
        private void BuildFonts(double scale)
        {
            Font pf = null, cf = null;
            try
            {
                pf = new Font("Microsoft YaHei UI", (float)(20.0 * scale), FontStyle.Bold,
                    GraphicsUnit.Point);
                cf = new Font("Microsoft YaHei UI", (float)(8.0 * scale), FontStyle.Regular,
                    GraphicsUnit.Point);
            }
            catch (Exception) { }
            Font oldP = _percentFont, oldC = _captionFont;
            // Only dispose an old font once its replacement was created; a
            // failed creation must not leave a disposed font in use.
            if (pf != null)
            {
                _percentFont = pf;
                if (oldP != null) { try { oldP.Dispose(); } catch (Exception) { } }
            }
            if (cf != null)
            {
                _captionFont = cf;
                if (oldC != null) { try { oldC.Dispose(); } catch (Exception) { } }
            }
        }

        public bool CircleVisible { get { return Visible; } }

        // Tray toggle support: the mouse-down captures the circle's actual
        // visibility (the circle never hides on focus loss, so no timer / race
        // handling is needed); the click consumes the capture exactly once.
        public void NotifyTrayMouseDown()
        {
            _hasTrayDown = true;
            _trayDownVisible = Visible;
        }

        public bool WantsHideOnTrayClick()
        {
            bool v = _hasTrayDown ? _trayDownVisible : Visible;
            _hasTrayDown = false;
            return v;
        }

        // ---- display ----

        public void SetDisplay(FloatingDisplay d)
        {
            _hasData = d != null && d.HasData;
            _percentKnown = d != null && d.PercentKnown;
            _percent = d == null ? 0 : d.Percent;
            _percentText = d == null || string.IsNullOrEmpty(d.PercentText)
                ? "暂无数据" : d.PercentText;
            _amountKnown = _hasData && d.AmountKnown;
            _amount = d == null ? 0 : d.RemainingAmount;
            _amountText = !_hasData ? "" : _amountKnown
                ? DisplayNames.Number(_amount) + " AFP" : "AFP 未知";
            string description = d == null ? "暂无数据" : (d.Tooltip ?? "");
            if (_hasData) description += "\n" + _amountText;
            AccessibleDescription = description;
            UpdateWaveState();
            if (Visible) Invalidate();
        }

        private void UpdateWaveState()
        {
            // v0.9 UX017: a disposed circle never restarts its timer, and
            // reduce motion keeps the wave static. Everything else unchanged:
            // the wave only runs while visible with known 0 < percent < 100.
            if (_waveDead) return;
            bool run = !_reduceMotion && Visible && _hasData && _percentKnown
                && _percent > 0 && _percent < 100;
            if (run)
            {
                if (!_wave.Enabled) _wave.Start();
            }
            else if (_wave.Enabled)
            {
                _wave.Stop();
            }
        }

        // ---- drag / click ----

        private void OnCircleMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                // The shared ContextMenuStrip is shown by the default WM_CONTEXTMENU
                // handling; TrayApp owns that menu (correct show/hide text).
                return;
            }
            if (e.Button != MouseButtons.Left) return;
            _down = true;
            _dragging = false;
            _downScreen = PointToScreen(e.Location);
            _downLocation = Location;
            Capture = true;
        }

        private void OnCircleMouseMove(object sender, MouseEventArgs e)
        {
            if (!_down) return;
            Point cur = PointToScreen(e.Location);
            int dx = cur.X - _downScreen.X;
            int dy = cur.Y - _downScreen.Y;
            int threshold = (int)Math.Round(DragThresholdLogical * _scale);
            if (!_dragging && (Math.Abs(dx) > threshold || Math.Abs(dy) > threshold))
            {
                _dragging = true;
                if (!_positionLocked && DragStarted != null) DragStarted(this, EventArgs.Empty);
            }
            if (!_dragging) return;
            if (_positionLocked) return; // locked: never move (release still swallowed)
            Rectangle b = new Rectangle(_downLocation.X + dx, _downLocation.Y + dy,
                Width, Height);
            Screen scr = Screen.FromPoint(new Point(
                b.Left + b.Width / 2, b.Top + b.Height / 2));
            Bounds = ClampTo(b, scr.WorkingArea);
        }

        private void OnCircleMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || !_down) return;
            _down = false;
            Capture = false;
            if (_dragging) { _dragging = false; return; } // drag never opens details
            if (DetailsRequested != null) DetailsRequested(this, EventArgs.Empty);
        }

        // ---- keys / lifecycle ----

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
            {
                RaiseExplicitHide();
                Hide();
            }
            else if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                if (DetailsRequested != null) DetailsRequested(this, EventArgs.Empty);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_allowClose)
            {
                e.Cancel = true;
                RaiseExplicitHide();
                Hide();
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            UpdateWaveState();
            if (Visible)
            {
                ApplyRegion();
                Invalidate();
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyRegion();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ApplyRegion();
            Invalidate();
        }

        private void ApplyRegion()
        {
            try
            {
                using (GraphicsPath path = EllipsePath())
                {
                    Region old = _region;
                    _region = new Region(path);
                    Region = _region;
                    if (old != null) old.Dispose();
                }
            }
            catch (Exception) { }
        }

        private GraphicsPath EllipsePath()
        {
            GraphicsPath p = new GraphicsPath();
            // Leave room for the resampling filter's partially covered pixels;
            // layered-window alpha still passes clicks through transparent pixels.
            p.AddEllipse(-1, -1, Math.Max(1, Width + 1), Math.Max(1, Height + 1));
            return p;
        }

        // ---- painting ----

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00080000; // WS_EX_LAYERED: preserve edge coverage.
                return cp;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeSize { public int Width, Height; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BlendFunction { public byte Operation, Flags, Alpha, Format; }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(IntPtr window, IntPtr destinationDC,
            ref NativePoint destination, ref NativeSize size, IntPtr sourceDC,
            ref NativePoint source, int colorKey, ref BlendFunction blend, int flags);
        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr dc);

        private void PresentFrame(Bitmap frame)
        {
            IntPtr dc = CreateCompatibleDC(IntPtr.Zero);
            if (dc == IntPtr.Zero) throw new System.ComponentModel.Win32Exception();
            IntPtr bitmap = IntPtr.Zero, old = IntPtr.Zero;
            try
            {
                bitmap = frame.GetHbitmap(Color.FromArgb(0));
                old = SelectObject(dc, bitmap);
                NativePoint destination = new NativePoint { X = Left, Y = Top };
                NativePoint source = new NativePoint();
                NativeSize size = new NativeSize { Width = frame.Width, Height = frame.Height };
                BlendFunction blend = new BlendFunction { Alpha = 255, Format = 1 };
                if (!UpdateLayeredWindow(Handle, IntPtr.Zero, ref destination, ref size,
                    dc, ref source, 0, ref blend, 2))
                    throw new System.ComponentModel.Win32Exception();
            }
            finally
            {
                if (old != IntPtr.Zero) SelectObject(dc, old);
                if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
                DeleteDC(dc);
            }
        }

        internal Bitmap RenderFrameForTest()
        {
            return RenderFrame();
        }

        private Bitmap RenderFrame()
        {
            // Supersample the silhouette as well as the water and text. The
            // inset keeps every partially covered pixel inside the native region.
            const int samples = 3;
            using (Bitmap surface = new Bitmap(Width * samples, Height * samples,
                PixelFormat.Format32bppPArgb))
            {
                using (Graphics g = Graphics.FromImage(surface))
                using (GraphicsPath clip = new GraphicsPath())
                {
                    g.Clear(Color.Transparent);
                    g.ScaleTransform(samples, samples);
                    clip.AddEllipse(1.5f, 1.5f, Math.Max(1, Width - 3), Math.Max(1, Height - 3));
                    g.SetClip(clip);
                    PaintCircle(g);
                }
                Bitmap frame = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(frame))
                {
                    g.CompositingMode = CompositingMode.SourceCopy;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(surface, new Rectangle(0, 0, Width, Height),
                        0, 0, surface.Width, surface.Height, GraphicsUnit.Pixel);
                }
                return frame;
            }
        }

        private Rectangle AmountBounds
        {
            get
            {
                float w = Width - 1, h = Height - 1;
                return new Rectangle((int)(w * 0.08f), (int)(h * 0.59f),
                    (int)(w * 0.84f), (int)(h * 0.16f));
            }
        }

        private string FittedAmountText
        {
            get { return PopupForm.FitAmountText(_amountText, _amountKnown
                ? _amount : double.NaN, AmountBounds.Width, _captionFont, "AFP"); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_waveDead || Disposing || IsDisposed) return;
            using (Bitmap frame = RenderFrame())
            {
                e.Graphics.DrawImageUnscaled(frame, 0, 0);
            }
        }

        protected override void OnInvalidated(InvalidateEventArgs e)
        {
            base.OnInvalidated(e);
            // UpdateLayeredWindow owns the displayed surface; subsequent
            // invalidations need an explicit upload even without WM_PAINT.
            if (_waveDead || Disposing || IsDisposed || !IsHandleCreated || !Visible
                || _percentFont == null || _captionFont == null)
                return;
            using (Bitmap frame = RenderFrame()) PresentFrame(frame);
        }

        private void PaintCircle(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using (SolidBrush background = new SolidBrush(Color.White))
                g.FillRectangle(background, 0, 0, Width, Height);

            float w = Width - 1, h = Height - 1;
            // 0 = empty, 100 = fully filled (solid water, no sine notch);
            // intermediate values draw layered waves.
            if (_hasData && _percentKnown && _percent >= 100)
            {
                using (SolidBrush full = new SolidBrush(UiStyle.Water))
                    g.FillRectangle(full, 0, 0, Width, Height);
            }
            else if (_hasData && _percentKnown && _percent > 0)
            {
                float level = (float)(_percent / 100.0);
                if (level > 1f) level = 1f;
                DrawWaves(g, w, h, level);
            }

            using (Pen ring = new Pen(Focused ? UiStyle.Primary
                : _hovered ? UiStyle.CircleHover : UiStyle.CircleRing,
                Math.Max(1.5f, w * (_hovered || Focused ? 0.022f : 0.012f))))
            {
                float inset = 1.5f + ring.Width / 2f;
                g.DrawEllipse(ring, inset, inset, Width - inset * 2, Height - inset * 2);
            }

            StringFormat center = new StringFormat();
            center.Alignment = StringAlignment.Center;
            center.LineAlignment = StringAlignment.Center;
            using (SolidBrush white = new SolidBrush(UiStyle.CircleNumber))
                g.DrawString(_percentText, _percentFont, white,
                    _hasData ? new RectangleF(0, h * 0.24f, w, h * 0.32f)
                        : new RectangleF(0, 0, w, h), center);
            if (_amountText.Length > 0)
            {
                center.FormatFlags = StringFormatFlags.NoWrap;
                center.Trimming = StringTrimming.EllipsisCharacter;
                using (SolidBrush caption = new SolidBrush(UiStyle.CircleCaption))
                    g.DrawString(FittedAmountText, _captionFont, caption, AmountBounds, center);
            }
            center.Dispose();
        }

        private void DrawWaves(Graphics g, float w, float h, float level)
        {
            float top = h * (1f - level);
            // Near the extremes the available span on one side of the waterline
            // is tiny; limit the amplitude so 1% never paints a large volume.
            float maxAmp = Math.Min(top, h - top) * 0.5f;
            if (maxAmp < 0f) maxAmp = 0f;
            for (int layer = 0; layer < 3; layer++)
            {
                float amp = h * (layer == 0 ? 0.028f : 0.020f);
                if (amp > maxAmp) amp = maxAmp;
                float baseTop = top + (layer == 2 ? amp * 0.4f : -amp * 0.3f);
                float phaseX = _phase * (layer == 0 ? 1f : -1.15f) + layer * 1.7f;
                float step = Math.Max(2f, w / 40f);
                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddLine(0, h, 0, WaveY(baseTop, amp, phaseX, 0, w));
                    for (float x = 0; x < w; x += step)
                    {
                        float x2 = Math.Min(w, x + step);
                        path.AddLine(x, WaveY(baseTop, amp, phaseX, x, w),
                            x2, WaveY(baseTop, amp, phaseX, x2, w));
                    }
                    path.AddLine(w, WaveY(baseTop, amp, phaseX, w, w), w, h);
                    path.CloseFigure();
                    Color c = layer == 0 ? UiStyle.HighlightBlue
                        : layer == 1 ? UiStyle.SecondaryBlue : UiStyle.Water;
                    using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, path);
                }
            }
        }

        private static float WaveY(float baseTop, float amp, float phase, float x, float w)
        {
            if (w <= 0) return baseTop;
            double t = (x / w) * Math.PI * 3.0 + phase;
            return baseTop + (float)Math.Sin(t) * amp;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _waveDead = true; // v0.9 UX017: never restart after dispose
                try { _wave.Stop(); _wave.Dispose(); } catch (Exception) { }
                try { if (_region != null) { _region.Dispose(); _region = null; } }
                catch (Exception) { }
                try { _percentFont.Dispose(); } catch (Exception) { }
                try { _captionFont.Dispose(); } catch (Exception) { }
            }
            base.Dispose(disposing);
        }

        // ---- test hooks ----

        internal bool WaveRunningForTest { get { return _wave.Enabled; } }
        internal string PercentTextForTest { get { return _percentText; } }
        internal bool PercentKnownForTest { get { return _percentKnown; } }
        internal string AmountTextForTest { get { return _amountText; } }
        internal string FittedAmountTextForTest { get { return FittedAmountText; } }
        internal Rectangle AmountBoundsForTest { get { return AmountBounds; } }
        internal Font AmountFontForTest { get { return _captionFont; } }
        internal string TooltipForTest { get { return AccessibleDescription; } }
        internal bool DraggingForTest { get { return _dragging; } }

        // Dispatch through the PRODUCTION mouse handlers so the real drag
        // threshold / bounds-clamp logic is exercised (screen coords only; never
        // moves the OS cursor).
        internal void SimulateMouseDownForTest(Point screenPoint)
        {
            MouseEventArgs e = new MouseEventArgs(MouseButtons.Left, 1,
                screenPoint.X - Left, screenPoint.Y - Top, 0);
            OnCircleMouseDown(this, e);
        }

        internal void SimulateMouseMoveForTest(Point screenPoint)
        {
            MouseEventArgs e = new MouseEventArgs(MouseButtons.Left, 0,
                screenPoint.X - Left, screenPoint.Y - Top, 0);
            OnCircleMouseMove(this, e);
        }

        internal void SimulateMouseUpForTest(Point screenPoint)
        {
            MouseEventArgs e = new MouseEventArgs(MouseButtons.Left, 1,
                screenPoint.X - Left, screenPoint.Y - Top, 0);
            OnCircleMouseUp(this, e);
        }
    }

}
