using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
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

    // Pure bounds computation for the tray-adjacent temporary panel, so the
    // offline smoke test can assert stability without a real screen.
    internal static class PanelPositioner
    {
        public const int LogicalMinWidth = 404;
        public const int LogicalMaxWidth = 440;
        // UX022 v0.14: card-only popup width — the old 440dp window minus its
        // outer whitespace, i.e. roughly the width the cards already had.
        public const int LogicalCardWidth = 406;
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

    // A card that explicitly re-lays-out its children from its OWN actual width.
    internal class CardPanel : Panel
    {
        public CardPanel()
        {
            // UX022 v0.14: the card itself is the focusable detail surface.
            // A plain Panel is not selectable by default; with Selectable +
            // TabStop the details can hand keyboard focus to the real card
            // instead of relying on (now removed) header buttons.
            SetStyle(ControlStyles.Selectable, true);
            TabStop = true;
        }

        public Action<int> Reflow;   // arg = inner width (card width - 2*padX)
        // UX022: raised after every real reflow so form-level in-place
        // decorations (the compact status line) survive ClearCard rebuilds.
        public Action AfterLayout;
        public int PadX;
        private int _laidOutWidth = -1;
        private Region _cardRegion;
        // Already DPI-scaled corner radius. DrawCardBorder uses the SAME value
        // so the clipped region and the drawn border never disagree per DPI.
        private int _regionRadius = 12;

        public int RegionRadius
        {
            get { return _regionRadius; }
            set
            {
                int r = value < 1 ? 1 : value;
                if (r == _regionRadius) return;
                _regionRadius = r;
                ApplyCardRegion();
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ApplyCardRegion();
            RunLayout();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyCardRegion();
            RunLayout();
        }

        private void ApplyCardRegion()
        {
            if (Width < 1 || Height < 1) return;
            try
            {
                Region old = _cardRegion;
                using (GraphicsPath path = UiStyle.RoundedRectangle(
                    new Rectangle(0, 0, Width - 1, Height - 1), _regionRadius))
                    _cardRegion = new Region(path);
                Region = _cardRegion;
                if (old != null) old.Dispose();
            }
            catch (Exception) { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _cardRegion != null)
            {
                _cardRegion.Dispose();
                _cardRegion = null;
            }
            base.Dispose(disposing);
        }

        public void RunLayout()
        {
            int inner = Width - PadX * 2;
            if (inner < 1 || Reflow == null || inner == _laidOutWidth) return;
            _laidOutWidth = inner;
            try { Reflow(inner); } catch (Exception) { }
            if (AfterLayout != null) { try { AfterLayout(); } catch (Exception) { } }
        }

        public void ForceLayout()
        {
            RunLayout();
        }

        // UX022: a same-width / different-DPI reopen must reflow even though
        // the pixel width did not change; the cache is invalidated explicitly.
        public void InvalidateLayout()
        {
            _laidOutWidth = -1;
        }
    }

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

                Color teal = Color.FromArgb(18, 184, 134);
                Color tealDark = Color.FromArgb(12, 145, 105);

                Rectangle r = new Rectangle(1, 1, size - 2, size - 2);
                using (LinearGradientBrush bg = new LinearGradientBrush(r, teal, tealDark, 55f))
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
                    using (SolidBrush white = new SolidBrush(Color.FromArgb(245, 255, 252)))
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

    internal class QuotaBar : Control
    {
        private double _value = -1;

        public QuotaBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw
                     | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        // negative => unknown (bar drawn empty, never implying zero)
        public double Value
        {
            get { return _value; }
            set { _value = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            // Only draw when there is a real pixel to paint; a 0 / negative
            // width or height is a legal no-op (never throws, never implies 0).
            if (Width <= 1 || Height <= 1) return;
            int radius = Math.Max(2, (Height - 1) / 2);
            Rectangle track = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath tp = RoundRect(track, radius))
            using (SolidBrush tb = new SolidBrush(Color.FromArgb(231, 237, 239)))
                g.FillPath(tb, tp);

            if (_value >= 0)
            {
                double v = _value;
                if (v > 100) v = 100;
                // Clamp the fill to the real track width so a narrow bar can
                // never overflow past its own bounds (a fill narrower than the
                // corner diameter used to round up past the track edge).
                int trackW = Width - 1;
                int fillW = (int)Math.Floor(trackW * (v / 100.0));
                if (fillW > trackW) fillW = trackW;
                if (fillW > 0)
                {
                    Rectangle fill = new Rectangle(0, 0, fillW, Height - 1);
                    // A fill narrower than the corner diameter must not bulge
                    // outward: shrink the effective radius to the fill extent.
                    int fr = Math.Min(radius, fillW / 2);
                    if (fr < 1) fr = 1;
                    using (GraphicsPath fp = RoundRect(fill, fr))
                    using (SolidBrush fb = new SolidBrush(UiStyle.Teal))
                        g.FillPath(fb, fp);
                }
            }
        }

        private static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            GraphicsPath p = new GraphicsPath();
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    // Small, testable helper for the tray toggle / focus-loss race.
    // A tray mouse-down captures the ACTUAL current visibility; the following
    // click consumes that capture exactly once. This prevents the click's own
    // WM_ACTIVATE (or a focus-loss during a long press) from hiding the window
    // before the click decides, which used to make the click re-open it.
    // All timing is external (the form's single WinForms.Timer calls Tick), so
    // the sequence of events is fully unit testable. One mechanism only.
    //
    // Trace model:
    //   Toggle(now, actualVisible) : tray mouse-down; captures visibility and
    //                                cancels any queued focus-loss hide.
    //   ConsumeToggle(actualVisible): tray click; one-shot read + reset. Uses
    //                                 the capture when present, else the
    //                                 caller's current visibility.
    //   Evt()        : a focus-loss (WM_ACTIVATE WA_INACTIVE / OnDeactivate).
    //   Tick()       : the delay timer fired.
    //   Cancel()     : window became active/shown again; clear pending.
    internal class HideController
    {
        public const int HideDelayMs = 220;

        public bool Pending { get { return _pending; } }
        public bool HasToggle { get { return _hasToggle; } }
        public int? Deadline { get { return _hasDeadline ? (int?)_deadlineVal : null; } }

        private bool _pending;
        private bool _hasDeadline;
        private int _deadlineVal;
        private bool _hasToggle;
        private bool _toggleVisible;

        // Starts the activate-suppression window and captures the actual
        // visibility at press time. A focus-loss queued just before the press
        // belongs to the same user action, so it is cleared: the click owns the
        // outcome until ConsumeToggle.
        public void Toggle(int now, bool actualVisible)
        {
            _actUntil = now + HideDelayMs;
            _hasActUntil = true;
            _hasToggle = true;
            _toggleVisible = actualVisible;
            _curVisible = actualVisible;
            _pending = false;
            _hasDeadline = false;
        }

        // One-shot consume of the mouse-down capture. Returns the press-time
        // visibility when a Toggle is outstanding, otherwise the caller-provided
        // current visibility. Always resets the one-shot state so a later click
        // without a mouse-down uses the live visibility, not a stale capture.
        public bool ConsumeToggle(bool actualVisible)
        {
            bool v = _hasToggle ? _toggleVisible : actualVisible;
            _hasToggle = false;
            return v;
        }

        // Called on each real show/hide with the resulting visibility.
        public void State(bool visible, int now)
        {
            _curVisible = visible;
            _hasActUntil = false; // show/hide itself is the "active" signal
            _hasToggle = false;    // a real state change invalidates any capture
            _pending = false;
            _hasDeadline = false;
        }

        // A focus-loss event. Queues a pending hide (deadline now + delay) or
        // extends an existing one; never returns a hide directly. While a tray
        // press is outstanding the click owns the outcome, so no hide is queued
        // (a long hold must not hide before mouse-up).
        public void Evt(int now, bool visible)
        {
            _curVisible = visible;
            if (_hasToggle) return;
            if (!visible) { _pending = false; _hasDeadline = false; return; }
            _pending = true;
            _deadlineVal = now + HideDelayMs;
            _hasDeadline = true;
        }

        // Cancel a pending hide (active regained).
        public void Cancel()
        {
            _pending = false;
            _hasDeadline = false;
        }

        // Evaluate at time now. Returns true only when a hide should occur.
        //  - not pending => false
        //  - within/after an activate-suppression window => keep pending, false
        //  - before the deadline => false
        //  - at/after the deadline with no suppression => true (and clear)
        public bool Tick(int now, bool visible)
        {
            _curVisible = visible;
            if (!_pending) return false;
            if (_hasActUntil && unchecked(now - _actUntil) < 0) return false; // still suppressed
            if (!visible) { _pending = false; _hasDeadline = false; return false; }
            if (_hasDeadline && unchecked(now - _deadlineVal) < 0) return false; // not due yet
            _pending = false;
            _hasDeadline = false;
            return true;
        }

        private bool _curVisible;
        private int _actUntil;
        private bool _hasActUntil;
    }

    internal class PopupForm : Form
    {
        private double _scale = 1.0;
        private FlowLayoutPanel _content;
        private ToolTip _tip;
        // UX022: the copy feedback owns a SEPARATE ToolTip so its transient
        // Show() can never overwrite the cards' stored update-time text.
        private ToolTip _feedbackTip;
        // UX022 v0.14: card-only popup. With the fixed header / footer removed,
        // the four details actions live in a details-local context menu on the
        // cards (they reuse the exact old button handlers; see BuildDetailsMenu).
        private ContextMenuStrip _detailsMenu;
        private ToolStripMenuItem _miRefresh;
        private ToolStripMenuItem _miCopy;
        private ToolStripMenuItem _miSettings;
        private ToolStripMenuItem _miClose;
        // UX022: view-level status shown compactly INSIDE the first card
        // (stale / cache / identity-unknown); null when everything is fresh.
        private string _statusNote;
        private bool _statusStrong;
        // UX022: copy feedback is a short visible tooltip; the text is kept
        // for offline assertions and cleared by the one-shot timer.
        private string _copyFeedbackText;
        // UX022: set when the fitted size actually changed while visible; the
        // owner re-anchors the window against the CURRENT circle.
        private bool _pendingReposition;
        // UX022: true when the second (scrollbar) pass engaged.
        private bool _scrolling;

        private readonly PanelModel _model = new PanelModel();
        private PanelView _view;
        private string _displayKey;
        private bool _allowClose;
        private bool _suppressHide;
        private Screen _openScreen;
        private double _openScale = 1.0;
        private bool _chromeReady;
        private int _cardWidth;
        private int _contentWidth;
        private bool _rebuilding;
        private readonly HideController _hide = new HideController();
        private readonly System.Windows.Forms.Timer _hideTimer = new System.Windows.Forms.Timer();
        private bool _menuOpen;
        private bool _dialogOpen;
        // UX015: details focus / scroll captured while the settings modal is up.
        private Control _dialogFocus;
        private Point _dialogScroll;
        private System.Windows.Forms.Timer _clock;
        // UX019 v0.11: footer "复制摘要" clipboard writing is injectable so
        // offline tests record instead of touching the real user clipboard.
        private Action<string> _clipboardSet = new Action<string>(
            delegate(string text) { Clipboard.SetText(text); });
        private System.Windows.Forms.Timer _copyFeedback;

        // Cached fonts are created once (per style) and reused across layouts to
        // avoid leaking GDI handles on repeated refreshes.
        private readonly Dictionary<string, Font> _fontCache = new Dictionary<string, Font>();
        private Panel _introCard;   // the actually-rendered first-run banner (or null)

        public event EventHandler RefreshRequested;
        public event EventHandler CancelRequested;
        public event EventHandler SettingsRequested;

        public bool AllowClose
        {
            get { return _allowClose; }
            set { _allowClose = value; }
        }

        private static readonly Color CardBorder = UiStyle.Border;
        private static readonly Color ContentBg = UiStyle.Canvas;
        private static readonly Color TextDark = UiStyle.Navy;
        private static readonly Color TextMuted = UiStyle.Muted;
        internal static readonly Color WarningColor = Color.FromArgb(232, 89, 12);
        private static readonly Color ErrorColor = Color.FromArgb(217, 72, 15);

        public PopupForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = ContentBg;
            KeyPreview = true;
            AutoScaleMode = AutoScaleMode.None;
            // UX022 v0.14: no outer chrome / border — the content fills the
            // whole window so the card width equals the client width exactly.
            Padding = new Padding(0);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            Text = "ark_left 方舟订阅额度";

            _scale = DpiUtil.GetScale(Screen.PrimaryScreen);

            _tip = new ToolTip();

            _content = new FlowLayoutPanel();
            _content.Dock = DockStyle.Fill;
            _content.FlowDirection = FlowDirection.TopDown;
            _content.WrapContents = false;
            _content.AutoScroll = true;
            _content.BackColor = ContentBg;
            // UX022 v0.14: the cards hug the window — no outer padding and no
            // outer chrome; the 10dp gap exists ONLY between cards (applied in
            // ApplyCardWidths).
            _content.Padding = new Padding(0);

            Controls.Add(_content);

            BuildDetailsMenu();

            _clock = new System.Windows.Forms.Timer();
            _clock.Interval = 15000; // local time labels only; never queries
            _clock.Tick += delegate { TickTimeLabels(); };

            _hideTimer.Interval = HideController.HideDelayMs;
            _hideTimer.Tick += OnHideTimerTick;
        }

        // UX022 v0.14: with the fixed header / footer removed, the details'
        // four actions live in a details-local context menu on the cards. It
        // reuses the EXACT old handlers — refresh (single-flight via
        // RefreshRequested), copy (snapshot-only formatter via
        // OnCopySummaryClicked), settings (single shared modal path via
        // SettingsRequested) and close (HidePanel) — so there is no new query
        // source and no second clipboard write path. Opening the menu is zero
        // query and suppresses focus-loss hiding; closing re-enables it. The
        // copy action is enabled only while a snapshot is rendered. The shared
        // circle / tray menu (8 items) is untouched.
        private void BuildDetailsMenu()
        {
            _detailsMenu = new ContextMenuStrip();
            _detailsMenu.ShowItemToolTips = true;

            _miRefresh = new ToolStripMenuItem("刷新");
            _miRefresh.AccessibleName = "刷新额度";
            _miRefresh.ToolTipText = "重新查询当前额度（Ctrl+R）";
            _miRefresh.Click += delegate { OnRefreshRequested(); };

            _miCopy = new ToolStripMenuItem("复制摘要");
            _miCopy.AccessibleName = "复制摘要";
            _miCopy.ToolTipText = "复制当前额度摘要到剪贴板（Ctrl+C）";
            _miCopy.Enabled = false;   // enabled once a snapshot is rendered
            _miCopy.Click += delegate { OnCopySummaryClicked(); };

            _miSettings = new ToolStripMenuItem("设置");
            _miSettings.AccessibleName = "设置悬浮窗显示";
            _miSettings.Click += delegate { OnSettingsRequested(); };

            _miClose = new ToolStripMenuItem("关闭");
            _miClose.AccessibleName = "关闭面板（不退出）";
            _miClose.Click += delegate { HidePanel(); };

            _detailsMenu.Items.AddRange(new ToolStripItem[]
                { _miRefresh, _miCopy, _miSettings, _miClose });
            UiStyle.StyleMenu(_detailsMenu);

            _detailsMenu.Opening += delegate
            {
                _miCopy.Enabled = _view != null && _view.Data != null;
                _menuOpen = true;
                _hide.Cancel();
                _hideTimer.Stop();
            };
            _detailsMenu.Closed += delegate { _menuOpen = false; };
            _content.ContextMenuStrip = _detailsMenu;
        }

        private Font F(float pt, bool bold)
        {
            string key = pt.ToString(System.Globalization.CultureInfo.InvariantCulture) + (bold ? "b" : "r");
            Font cached;
            if (_fontCache.TryGetValue(key, out cached)) return cached;
            Font f = new Font("Microsoft YaHei UI", pt, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point);
            _fontCache[key] = f;
            return f;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_clock != null) { try { _clock.Stop(); _clock.Dispose(); } catch (Exception) { } }
                if (_hideTimer != null) { try { _hideTimer.Stop(); _hideTimer.Dispose(); } catch (Exception) { } }
                if (_copyFeedback != null) { try { _copyFeedback.Stop(); _copyFeedback.Dispose(); } catch (Exception) { } }
                if (_tip != null) { try { _tip.Dispose(); } catch (Exception) { } }
                if (_feedbackTip != null) { try { _feedbackTip.Dispose(); } catch (Exception) { } _feedbackTip = null; }
                // UX022: the details-local menu and the window Region are both
                // owned here — released deterministically (no GDI / menu leak).
                if (_detailsMenu != null) { try { _detailsMenu.Dispose(); } catch (Exception) { } _detailsMenu = null; }
                if (Region != null) { try { Region.Dispose(); Region = null; } catch (Exception) { } }
                foreach (Font f in _fontCache.Values)
                {
                    try { f.Dispose(); } catch (Exception) { }
                }
                _fontCache.Clear();
            }
            base.Dispose(disposing);
        }

        private int S(int logical)
        {
            return (int)Math.Round(logical * _scale);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!_chromeReady)
            {
                _chromeReady = true;
                double s = DeviceDpi > 0 ? DeviceDpi / 96.0 : _scale;
                if (Math.Abs(s - _scale) > 0.001)
                {
                    // UX022: a real DPI at handle creation differs from the
                    // guess — re-derive the fitted size from the actual scale.
                    _scale = s;
                    RelayoutContent();
                }
            }
        }

        // ---- visibility ----

        public void ShowPanel()
        {
            ShowPanel(Screen.FromPoint(Cursor.Position));
        }

        // Opens the details on an explicit screen (normally the floating
        // circle's screen) so the fixed layout matches that screen's DPI. The
        // parameterless overload keeps the original mouse-screen behaviour.
        public void ShowPanel(Screen screen)
        {
            Screen scr = screen ?? Screen.FromPoint(Cursor.Position);
            _openScreen = scr;
            _openScale = DpiUtil.GetScale(scr);
            if (_content.Controls.Count == 0)
            {
                // First open shows the model's current (loading / waiting)
                // state; opening never queries.
                ApplyModelView(Model.CurrentView);
            }
            if (Math.Abs(_openScale - _scale) > 0.001)
            {
                // UX022: a DPI change must re-derive card padding / fonts /
                // corner radius and the fitted height; the per-card layout
                // cache is invalidated so a same-width card still reflows.
                _scale = _openScale;
                InvalidateCardLayouts();
                RelayoutContent();
            }

            _suppressHide = true;
            try
            {
                // UX022: opening never changes the data, so keep the CURRENT
                // width decision (SizeToFit(false)); a full pass here would
                // re-run the two-pass width flip and rebuild every card on a
                // repeat open. The first build / DPI change already fitted.
                SizeToFit(false);
                if (!Visible) Show();
                _hide.State(true, Environment.TickCount); // clear any pending hide
                _hideTimer.Stop();
                if (!_clock.Enabled) _clock.Start();
                Activate();
                BringToFront();
                // UX022: focus the card (or the first focusable content) on
                // activation; a repeat open that already holds CARD focus is
                // not reset. ContainsFocus alone is true when only the content
                // container holds focus, which must still hand focus to a card.
                if (!CardHasFocus()) FocusFirstContent();
            }
            finally
            {
                _suppressHide = false;
            }
        }

        public void HidePanel()
        {
            _suppressHide = true;
            try
            {
                _hide.State(false, Environment.TickCount);
                _hideTimer.Stop();
                if (Visible) Hide();
                if (_clock.Enabled) _clock.Stop();
            }
            finally
            {
                _suppressHide = false;
            }
        }

        private Screen ActiveScreen()
        {
            return _openScreen ?? Screen.FromPoint(Cursor.Position);
        }

        // UX022 v0.14: the window hugs the cards. Height fitting uses the REAL
        // layout in at most two bounded passes: pass 1 lays every card out at
        // the full clamped width and measures the actual content bottom; only
        // when that overflows the capped height does pass 2 subtract the REAL
        // scrollbar width and re-flow the cards. No infinite resize loop and
        // no guessing from the period count.
        // fullPass: re-evaluate BOTH the width decision and the height
        // (rendered data changed). When false (same-semantics refresh, e.g.
        // only the status row / update time moved), the CURRENT width decision
        // is kept — otherwise the two passes would flip the width (full ↔
        // scrollbar) and REBUILD every card on each refresh, destroying the
        // same-card / same-children guarantees.
        private void SizeToFit()
        {
            SizeToFit(true);
        }

        private void SizeToFit(bool fullPass)
        {
            Rectangle wa = ActiveScreen().WorkingArea;
            int w = LayoutMath.ClampWidth(PanelPositioner.LogicalCardWidth, _scale, wa.Width);
            int maxH = Math.Min(S(560), wa.Height - S(16));
            if (maxH < 1) maxH = 1;

            if (!fullPass && _scrolling)
            {
                // Keep the scrollbar width; only re-measure for the hug.
                ApplyCardWidths(Math.Max(1, w - ScrollbarWidth()));
                int h2 = MeasureCardsHeight();
                Size target2 = new Size(w, Math.Max(1, Math.Min(maxH, h2)));
                if (ClientSize != target2) { ClientSize = target2; if (Visible) _pendingReposition = true; }
                ApplyWindowRegion();
                return;
            }

            // Pass 1: full width, no scrollbar.
            _scrolling = false;
            _content.AutoScroll = false;
            ApplyCardWidths(w);
            int h = MeasureCardsHeight();

            // Pass 2 (bounded): real overflow — subtract the REAL scrollbar.
            if (h > maxH)
            {
                _scrolling = true;
                _content.AutoScroll = true;
                ApplyCardWidths(Math.Max(1, w - ScrollbarWidth()));
                h = maxH;
            }

            Size target = new Size(w, Math.Max(1, h));
            bool resized = ClientSize != target;
            if (resized) ClientSize = target;
            ApplyWindowRegion();
            if (resized && Visible) _pendingReposition = true;
        }

        // Lays every card at the given width; CardPanel reflows from its OWN
        // width. The 10dp gap exists ONLY between cards — never after the
        // last one (no tail gap).
        private void ApplyCardWidths(int w)
        {
            _contentWidth = w;
            _cardWidth = w;
            _content.SuspendLayout();
            try
            {
                int count = _content.Controls.Count;
                for (int i = 0; i < count; i++)
                {
                    Control c = _content.Controls[i];
                    c.Margin = i < count - 1 ? new Padding(0, 0, 0, S(10)) : new Padding(0);
                    c.Width = w;
                }
            }
            finally
            {
                _content.ResumeLayout(true);
            }
        }

        private int MeasureCardsHeight()
        {
            // UX022: sum each card's own measured height plus its vertical
            // margins instead of reading Control.Bottom. Bottom is only valid
            // once the FlowLayoutPanel has positioned its children, but this
            // runs right after ApplyCardWidths (which reflows every card
            // synchronously and sets its Height) and BEFORE the panel's own
            // layout moves them into place. Reading Bottom there uses stale /
            // zero positions, which both under- and over-measured the real
            // content and then flipped the width in a later pass, rebuilding
            // the cards on same-semantics refreshes. TopDown flow advances by
            // Height + Margin.Top + Margin.Bottom (Padding is 0).
            int total = 0;
            for (int i = 0; i < _content.Controls.Count; i++)
            {
                Control c = _content.Controls[i];
                total += c.Height + c.Margin.Top + c.Margin.Bottom;
            }
            return total;
        }

        // UX022: single card, no scroll — the window Region is the card's own
        // rounded rectangle (same DPI-scaled radius), so the popup really looks
        // like just that card. Multi-card / scrolling keeps a plain compact
        // container without any outer frame or region.
        private void ApplyWindowRegion()
        {
            bool rounded = !_scrolling && _content.Controls.Count == 1;
            if (!rounded)
            {
                if (Region != null) { Region.Dispose(); Region = null; }
                return;
            }
            CardPanel card = _content.Controls[0] as CardPanel;
            if (card == null || Width < 1 || Height < 1) return;
            try
            {
                Region old = Region;
                using (GraphicsPath path = UiStyle.RoundedRectangle(
                    new Rectangle(0, 0, Width - 1, Height - 1), card.RegionRadius))
                    Region = new Region(path);
                if (old != null) old.Dispose();
            }
            catch (Exception) { }
        }

        // UX022: first selectable content — the card itself (CardPanel is
        // explicitly selectable) or a focusable control inside it (retry /
        // login / guide action buttons).
        // A bare container focus (the FlowLayoutPanel) is NOT card focus: the
        // form can report ContainsFocus while only that panel is active, and
        // then the card must still take focus.
        private bool CardHasFocus()
        {
            Control active = ActiveControl;
            if (active == null || active == _content) return false;
            for (Control p = active; p != null; p = p.Parent)
            {
                if (p == _content) return true;
            }
            return false;
        }

        private void FocusFirstContent()
        {
            foreach (Control c in _content.Controls)
            {
                if (c.CanSelect) { FocusQuietly(c); return; }
                foreach (Control k in c.Controls)
                {
                    if (k.CanSelect) { FocusQuietly(k); return; }
                }
            }
        }

        private static void FocusQuietly(Control c)
        {
            try { c.Focus(); } catch (Exception) { }
        }

        private void InvalidateCardLayouts()
        {
            foreach (Control c in _content.Controls)
            {
                CardPanel card = c as CardPanel;
                if (card != null) card.InvalidateLayout();
            }
        }

        // UX022: re-derive the rendered cards from the current view (a DPI /
        // scale change must rebuild fonts / paddings / radius / height) and
        // re-fit the window. Zero query; the same snapshot is kept.
        private void RelayoutContent()
        {
            if (_view != null)
            {
                _displayKey = null;   // force a real rebuild
                ApplyModelView(_view);
            }
            else
            {
                SizeToFit();
            }
        }

        // Called by the tray mouse-down BEFORE the click toggles. Captures the
        // ACTUAL visibility at press time, opens the activate-suppression
        // window, cancels any queued focus-loss hide, and stops the hide timer so
        // a long hold cannot hide the panel before mouse-up.
        public void NotifyTrayMouseDown()
        {
            _hide.Toggle(Environment.TickCount, Visible);
            _hideTimer.Stop();
        }

        // The one-shot toggle decision after a tray click. Consumes the capture
        // from the last mouse-down; if no mouse-down preceded this click, the
        // live visibility is used instead of a stale prior capture.
        public bool WantsHideOnTrayClick()
        {
            return _hide.ConsumeToggle(Visible);
        }

        // Suppresses focus-loss hiding while the tray context menu is open.
        public void SetMenuOpen(bool open)
        {
            _menuOpen = open;
            if (open) { _hide.Cancel(); _hideTimer.Stop(); }
        }

        // Suppresses focus-loss hiding while a modal dialog (the shared settings
        // form) is up. Without this, opening the settings from the details
        // header deactivates the details and the hide timer would close it out
        // from under the returned modal. Mirrors SetMenuOpen.
        // UX015: engaging captures the details' active control and scroll
        // offset; releasing proactively restores both so returning from the
        // settings lands back on the original detail state. A repeated engage
        // keeps the FIRST capture (a repeat request must not reset it).
        public void SetDialogOpen(bool open)
        {
            if (open)
            {
                if (_dialogOpen) return;
                _dialogOpen = true;
                try { _dialogFocus = ActiveControl; } catch (Exception) { _dialogFocus = null; }
                try { _dialogScroll = _content.AutoScrollPosition; }
                catch (Exception) { _dialogScroll = Point.Empty; }
                _hide.Cancel(); _hideTimer.Stop();
                return;
            }
            if (!_dialogOpen) return;
            _dialogOpen = false;
            RestoreDialogState();
        }

        // UX015/T042: proactive return to the original details. Order matters:
        // reactivate the panel and restore the effective focus FIRST (focusing
        // can scroll the content on its own), then restore the captured scroll
        // offset LAST — including an explicit restore of zero. A disposing /
        // disposed form is left alone (never reactivated).
        private void RestoreDialogState()
        {
            Control f = _dialogFocus;
            _dialogFocus = null;
            if (IsDisposed || Disposing) return;
            if (Visible) { try { Activate(); } catch (Exception) { } }
            if (f != null && !f.IsDisposed)
            {
                try { ActiveControl = f; } catch (Exception) { }
                if (f.CanFocus) { try { f.Focus(); } catch (Exception) { } }
            }
            try
            {
                Point p = _dialogScroll;
                _content.AutoScrollPosition = new Point(-p.X, -p.Y);
            }
            catch (Exception) { }
        }

        // Shows the slow-response hint by updating only the note/footer fields.
        // Never rebuilds cards, so keyboard focus is preserved.
        public void ShowSlowHint()
        {
            // UX011: progress callbacks must not alter the displayed state.
        }

        // Text-only stage hint; never rebuilds cards (keeps focus).
        public void ShowStageHint(string text)
        {
            // UX011: no process text.
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == 0x0006 /* WM_ACTIVATE */)
            {
                int wa = m.WParam.ToInt32() & 0xFFFF;
                if (wa == 0 /* WA_INACTIVE */) OnFocusLost();
                else { _hide.Cancel(); _hideTimer.Stop(); }
            }
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            OnFocusLost();
        }

        // Single hide mechanism: queue a pending hide and let the one delay timer
        // decide. Coalesced, cancellable, and never permanently stuck.
        private void OnFocusLost()
        {
            if (_suppressHide || _menuOpen || _dialogOpen) return;
            _hide.Evt(Environment.TickCount, Visible);
            if (_hide.Pending && !_hideTimer.Enabled) _hideTimer.Start();
        }

        private void OnHideTimerTick(object sender, EventArgs e)
        {
            _hideTimer.Stop();
            if (_suppressHide || _menuOpen || _dialogOpen) { _hide.Cancel(); return; }
            if (Visible && ContainsFocus) { _hide.Cancel(); return; }
            bool hide = _hide.Tick(Environment.TickCount, Visible);
            if (hide && Visible) HidePanel();
            else if (_hide.Pending) _hideTimer.Start(); // still suppressed: re-check later
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) HidePanel();
        }

        // UX021 v0.13: details-local accelerators. Only the two exact combos
        // Ctrl+R and Ctrl+C are recognized, and only while the details are
        // visible, enabled and contain keyboard focus with no settings modal
        // or shared menu open. Both reuse the exact button click paths —
        // single-flight refresh (a pending query is never queued) and the
        // snapshot-only copy with its fixed safe strings — so there is no new
        // query source and no second clipboard write path. With no snapshot,
        // Ctrl+C is still consumed but writes nothing and never queries.
        // Everything else (Ctrl+Shift / Ctrl+Alt variants, Esc, hidden or
        // unfocused states, modal / menu open) falls through to base
        // processing; no global hotkey and no input injection is involved.
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (Visible && Enabled && ContainsFocus && !IsDisposed && !Disposing
                && !_dialogOpen && !_menuOpen)
            {
                if (keyData == (Keys.Control | Keys.R))
                {
                    OnRefreshRequested();   // single-flight; pending never queues
                    return true;
                }
                if (keyData == (Keys.Control | Keys.C))
                {
                    OnCopySummaryClicked(); // no snapshot: guarded, writes nothing
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_allowClose)
            {
                e.Cancel = true;
                HidePanel();
                return;
            }
            base.OnFormClosing(e);
        }

        private void OnRefreshRequested()
        {
            if (RefreshRequested != null) RefreshRequested(this, EventArgs.Empty);
        }

        private void OnCancelRequested()
        {
            if (CancelRequested != null) CancelRequested(this, EventArgs.Empty);
        }

        private void OnSettingsRequested()
        {
            if (SettingsRequested != null) SettingsRequested(this, EventArgs.Empty);
        }

        // Draws the settings glyph on the header button; rebuilt only when the
        // DPI-scaled size changes (the old bitmap is disposed first).
        // ---- model binding / rendering ----

        public PanelModel Model { get { return _model; } }

        public void ApplyModelView(PanelView v)
        {
            if (v == null) return;
            string key = DisplayKey(v);
            bool changed = key != _displayKey;
            Point scroll = _content.AutoScrollPosition;
            Control focused = ActiveControl;
            _view = v;
            // UX022: the view-level note (stale / cache / identity) is shown
            // as a compact line INSIDE the first card; null when fresh.
            _statusNote = v.State == PanelState.StaleError || v.State == PanelState.CancelledStale
                ? "未能更新，显示上次数据。" : (v.IdentityUnknown ? "身份未完全确认" :
                (v.FromCache ? "上次数据" : null));
            _statusStrong = v.NoteStrong;
            _content.SuspendLayout();
            try
            {
                if (changed && v.Data != null)
                {
                    RenderCards(v.Data);
                }
                else if (changed)
                {
                    ReplaceContent(new List<Control> { BuildMessageCard(
                        string.IsNullOrEmpty(v.Message) ? "暂无数据" : v.Message,
                        v.MessageStrong ? TextDark : TextMuted) });
                    SyncStatusInPlace();
                    SizeToFit();
                }
                else
                {
                    // UX022: same semantics — the status line (and the update
                    // time tooltip) update IN PLACE: no card rebuild, no focus
                    // / scroll reset, and the window still hugs the card if
                    // the status line appeared or disappeared.
                    SyncStatusInPlace();
                    // No full two-pass re-evaluation: keeping the current
                    // width decision prevents the scrollbar pass from
                    // rebuilding the cards on every same-semantics refresh.
                    SizeToFit(false);
                }
                _introCard = null;
                if (_miCopy != null) _miCopy.Enabled = v.Data != null;
                _displayKey = key;
                SyncUpdateTimeTooltip();
            }
            finally
            {
                _content.ResumeLayout(true);
            }
            if (changed)
            {
                // UX022: replay the scroll offset ONLY when the rebuilt content
                // still scrolls. Setting AutoScrollPosition while AutoScroll is
                // off silently shifts the (single) card up and breaks the
                // bottom hug / rounded-card fit, even though the window kept
                // its measured height.
                if (_scrolling && _content.VerticalScroll.Visible)
                    _content.AutoScrollPosition = new Point(-scroll.X, -scroll.Y);
                else
                    _content.AutoScrollPosition = Point.Empty;
            }
            if (focused != null && !focused.IsDisposed && focused.CanFocus) focused.Focus();
            if (Visible) Invalidate(true);
        }

        private static string LastUpdate(DateTime time)
        {
            return time == DateTime.MinValue ? "更新时间未知" : "最后更新 " + DisplayNames.FormatTime(time);
        }

        // UX022 v0.14: the old footer update time now lives on the cards'
        // tooltip. Text-only, in place — no card rebuild, no focus / scroll
        // change.
        private void SyncUpdateTimeTooltip()
        {
            if (_view == null || _view.Data == null) return;
            string text = LastUpdate(_view.Data.FetchedAt);
            for (int i = 0; i < _content.Controls.Count; i++)
            {
                try { _tip.SetToolTip(_content.Controls[i], text); }
                catch (Exception) { }
            }
        }

        // UX022 v0.14: view-level status (stale / cache / identity-unknown)
        // must stay VISIBLE inside the card without a header / footer. One
        // persistent row exists per first card; refreshes only toggle its
        // text / visibility and a bounded height delta in place, so the
        // control tree stays stable (no create / dispose churn, no card
        // rebuild, no flicker) and focus / scroll survive. It carries the
        // "status" tag only while actually shown; hidden it is inert.
        private void SyncStatusInPlace()
        {
            if (_content.Controls.Count == 0) return;
            CardPanel card = _content.Controls[0] as CardPanel;
            if (card == null) return;
            Label st = FindStatusRow(card);
            if (st == null)
            {
                // Create the row once per card layout (Reflow's ClearCard
                // disposes it, so it is re-created there); keep it inert until
                // a note actually needs it.
                st = new Label();
                st.Name = "arkStatusRow";
                st.AutoSize = false;
                st.Visible = false;
                st.SetBounds(0, 0, 1, 1);
                card.Controls.Add(st);
            }
            bool want = !string.IsNullOrEmpty(_statusNote);
            bool active = (string)st.Tag == "status";
            if (want)
            {
                if (!active)
                {
                    st.Font = F(8.25f, false);
                    int oldH = card.Height;
                    st.SetBounds(card.PadX, Math.Max(0, oldH - S(8)),
                        Math.Max(1, card.Width - card.PadX * 2), S(16));
                    card.Height = oldH + S(22);
                    st.Tag = "status";   // marker for FindStatusLabelForTest
                    st.Visible = true;
                }
                st.Text = _statusNote;
                st.ForeColor = _statusStrong ? WarningColor : TextMuted;
            }
            else if (active)
            {
                st.Tag = null;
                st.Visible = false;
                st.Text = null;
                card.Height = Math.Max(1, card.Height - S(22));
            }
        }

        private static Label FindStatusRow(Control card)
        {
            foreach (Control c in card.Controls)
            {
                if (c is Label && c.Name == "arkStatusRow") return (Label)c;
            }
            return null;
        }

        // UX019: the ONLY clipboard write path, invoked strictly from the
        // explicit copy action (since UX022 the details-local menu item; the
        // old footer button was removed with the chrome) and — since UX021
        // v0.13 — the details-local
        // Ctrl+C accelerator (both explicit user actions; auto refresh / open
        // / render never copy). A clipboard failure shows a short fixed inline
        // "复制失败" as a visible short tooltip (never a blocking dialog) and
        // the user can simply retry.
        private void OnCopySummaryClicked()
        {
            if (_view == null || _view.Data == null) return;   // disabled guard
            if (_copyFeedback != null && _copyFeedback.Enabled) _copyFeedback.Stop();
            string summary = null;
            try { summary = QuotaSummary.Build(_view); }
            catch (Exception) { ShowCopyFeedback("复制失败"); return; }
            if (string.IsNullOrEmpty(summary)) return;
            try { _clipboardSet(summary); ShowCopyFeedback("已复制"); }
            catch (Exception) { ShowCopyFeedback("复制失败"); }
        }

        // UX022 v0.14: copy feedback is a short VISIBLE non-blocking tooltip
        // over the cards ("已复制" / "复制失败", ~2s) — there is no footer
        // button text anymore. It never overwrites an error card, never
        // contains raw upstream text and never queries. The one-shot timer
        // clears the recorded state; stopped and disposed deterministically.
        private void ShowCopyFeedback(string text)
        {
            _copyFeedbackText = text;
            try
            {
                Control anchor = _content.Controls.Count > 0
                    ? _content.Controls[0] : (Control)_content;
                if (_feedbackTip == null) _feedbackTip = new ToolTip();
                _feedbackTip.Show(text, anchor,
                    Math.Max(4, anchor.Width - S(90)), S(8), 2000);
            }
            catch (Exception) { }
            if (_copyFeedback == null)
            {
                _copyFeedback = new System.Windows.Forms.Timer();
                _copyFeedback.Interval = 2000;
                _copyFeedback.Tick += delegate
                {
                    _copyFeedback.Stop();
                    _copyFeedbackText = null;
                };
            }
            _copyFeedback.Start();
        }

        // Length-delimited display semantics; deliberately excludes fetchedAt,
        // identity, server timestamps and raw used/total fields not shown by UX011.
        internal static string DisplayKey(PanelView v)
        {
            List<string> fields = new List<string>();
            if (v.Data == null)
            {
                fields.Add(string.IsNullOrEmpty(v.Message) ? "暂无数据" : v.Message);
                fields.Add(v.MessageStrong.ToString());
            }
            else
            {
                fields.Add(v.Data.Products.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
                if (v.Data.Products.Count == 0)
                {
                    fields.Add(string.IsNullOrEmpty(v.Data.Message)
                        ? "当前身份下未发现已订阅的方舟套餐。" : v.Data.Message);
                    fields.Add((v.Data.Status == QuotaStatus.NotLoggedIn || v.Data.Status == QuotaStatus.CliMissing).ToString());
                }
                foreach (ProductQuota p in v.Data.Products)
                {
                    fields.Add(ProductTitle(p));
                    fields.Add(!p.SubscribedKnown ? "订阅状态未知" :
                        (!p.Subscribed && p.Error == null && !p.PeriodErrorPresent && !p.Malformed ? "未订阅" : ""));
                    fields.Add(p.Error ?? (p.Periods.Count == 0 ? "无周期数据。" : ""));
                    fields.Add(p.Periods.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    foreach (PeriodQuota q in p.Periods)
                    {
                        fields.Add(q.LabelDisplay); fields.Add(q.Error);
                        if (q.Error != null) continue;
                        fields.Add(q.PercentKnown ? PercentFormat.RemainingForBar(q.RemainingPercent) : "剩余未知");
                        fields.Add(q.PercentKnown ? q.RemainingPercent.ToString("R", System.Globalization.CultureInfo.InvariantCulture) : "");
                        fields.Add(q.AmountKnown ? DisplayNames.Number(q.RemainingAmount) : "");
                        fields.Add(q.HasReset ? DisplayNames.FormatTime(q.ResetLocal) : "");
                        fields.Add(q.UnknownNote != null && (q.Clamped || !q.PercentKnown) ? q.UnknownNote : null);
                        fields.Add(q.UnknownNote != null && q.Clamped ? "warning" : "muted");
                    }
                    fields.Add("/product");
                }
            }
            System.Text.StringBuilder key = new System.Text.StringBuilder();
            foreach (string f in fields) key.Append(f == null ? -1 : f.Length).Append(':').Append(f);
            return key.ToString();
        }

        private void ReplaceContent(List<Control> next)
        {
            Control[] old = new Control[_content.Controls.Count];
            _content.Controls.CopyTo(old, 0);
            // Build first, attach once under suspended layout, then release old controls.
            _content.Controls.AddRange(next.ToArray());
            foreach (Control c in old) { _content.Controls.Remove(c); c.Dispose(); }
        }

        private static string TitleForState(PanelState s)
        {
            switch (s)
            {
                case PanelState.Loading: return "正在查询…";
                case PanelState.ConfirmingIdentity: return "确认身份…";
                case PanelState.RefreshingSameScope: return "刷新中…";
                case PanelState.StaleError: return "更新失败";
                case PanelState.CancelledStale: return "已取消";
                case PanelState.IdentityChanged: return "身份变化";
                case PanelState.Error: return "查询失败";
                default: return "方舟订阅额度";
            }
        }

        private void BuildActionButtons(PanelView v)
        {
            if (v.AllowRetry)
                _content.Controls.Add(BuildActionButton("重试", delegate { OnRefreshRequested(); }));
            if (v.AllowCopyLogin)
                _content.Controls.Add(BuildActionButton("复制登录命令", delegate { CopyLoginCommand(); }));
            if (v.AllowOpenGuide)
                _content.Controls.Add(BuildActionButton("打开设置指南", delegate { OpenGuide(); }));
        }

        // Session-persistent first-run guidance card with a dismiss button.
        private Panel BuildBannerCard(string text)
        {
            CardPanel card = new CardPanel();
            card.PadX = S(16);
            StyleCard(card, S(12));
            card.BackColor = Color.FromArgb(240, 248, 244);
            // UX022: re-attach the compact status row after every reflow.
            card.AfterLayout = delegate { SyncStatusInPlace(); };
            card.Paint += delegate(object s, PaintEventArgs e) { DrawCardBorder(e, (Control)s); };
            card.Reflow = delegate(int innerW)
            {
                ClearCard(card);
                Label lbl = new Label();
                lbl.Font = F(8.5f, false);
                lbl.ForeColor = Color.FromArgb(20, 90, 70);
                lbl.AutoSize = false;
                lbl.Text = text;
                int textH = MeasureWrappedHeight(text, lbl.Font, innerW);
                lbl.SetBounds(card.PadX, S(8), innerW, textH);
                card.Controls.Add(lbl);
                Button close = new Button();
                close.Text = "知道了";
                close.AccessibleName = "关闭首次运行提示";
                close.TabStop = true;
                close.FlatStyle = FlatStyle.Flat;
                close.FlatAppearance.BorderSize = 0;
                close.BackColor = Color.FromArgb(240, 248, 244);
                close.ForeColor = Color.FromArgb(20, 90, 70);
                close.Font = F(8.25f, false);
                close.SetBounds(card.PadX, S(10) + textH, S(64), S(24));
                close.Click += delegate { DismissIntro(); };
                card.Controls.Add(close);
                card.Height = textH + S(42);
            };
            card.Width = _cardWidth > 0 ? _cardWidth : S(300);
            card.ForceLayout();
            // UX022: no tail gap — ApplyCardWidths puts the gap ONLY between cards.
            card.Margin = new Padding(0);
            return card;
        }

        private void DismissIntro()
        {
            _introCard = null;
        }

        private Panel BuildActionButton(string text, EventHandler onClick)
        {
            CardPanel card = new CardPanel();
            card.PadX = S(12);
            StyleCard(card, S(12));
            card.BackColor = Color.White;
            // UX022: re-attach the compact status row after every reflow.
            card.AfterLayout = delegate { SyncStatusInPlace(); };
            card.Paint += delegate(object s, PaintEventArgs e) { DrawCardBorder(e, (Control)s); };
            card.Reflow = delegate(int innerW)
            {
                ClearCard(card);
                Button b = new Button();
                b.Text = text;
                b.AccessibleName = text;
                b.TabStop = true;
                b.FlatStyle = FlatStyle.Flat;
                b.BackColor = Color.FromArgb(238, 245, 242);
                b.ForeColor = Color.FromArgb(20, 90, 70);
                b.Cursor = Cursors.Hand;
                b.Font = F(9f, false);
                b.SetBounds(card.PadX, S(8), innerW, S(30));
                b.Click += onClick;
                card.Controls.Add(b);
                card.Height = S(46);
            };
            card.Width = _cardWidth > 0 ? _cardWidth : S(300);
            card.ForceLayout();
            // UX022: no tail gap — ApplyCardWidths puts the gap ONLY between cards.
            card.Margin = new Padding(0);
            return card;
        }

        // ---- clipboard / guide (local actions, no network) ----

        private void CopyLoginCommand()
        {
            try
            {
                Clipboard.SetText("arkcli auth login volc-sso");
                // UX022: no footer note line anymore — the action result is a
                // short visible non-blocking tooltip (never a dialog).
                ShowCopyFeedback("已复制登录命令，请在终端粘贴执行。");
            }
            catch (Exception)
            {
                ShowCopyFeedback("剪贴板被占用，复制失败，请手动执行：arkcli auth login volc-sso");
            }
        }

        private void OpenGuide()
        {
            try
            {
                string path = TrayApp.GuidePath();
                if (!System.IO.File.Exists(path))
                {
                    ShowCopyFeedback("未找到设置指南：" + path);
                    return;
                }
                System.Diagnostics.ProcessStartInfo psi =
                    new System.Diagnostics.ProcessStartInfo();
                psi.FileName = "notepad.exe";
                psi.Arguments = "\"" + path + "\"";
                psi.UseShellExecute = false;
                System.Diagnostics.Process.Start(psi);
                ShowCopyFeedback("已用记事本打开：" + path);
            }
            catch (Exception)
            {
                ShowCopyFeedback("无法打开设置指南：" + TrayApp.GuidePath());
            }
        }

        // ---- time labels (text-only; never rebuilds cards) ----

        private void TickTimeLabels()
        {
            if (!Visible || _view == null || _view.Data == null)
            {
                if (_clock.Enabled && !Visible) _clock.Stop();
                return;
            }
            try
            {
                SyncUpdateTimeTooltip();
            }
            catch (Exception) { }
        }

        // ---- rendering ----

        public void SetLoading()
        {
            ApplyModelView(_model.BeginQuery());
        }

        public void Render(QuotaSnapshot snap)
        {
            ApplyModelView(_model.OnUsageResult(snap, null, ScopeVerdict.Same, null));
        }

        public void ApplyAuthResult(QueryOutcome o)
        {
            ApplyModelView(_model.OnAuthResult(
                o.AuthConfirmed, o.Identity, o.Snapshot.Status, o.Snapshot.Message));
        }

        // Auth-progress onset: hide old data and show "确认身份…" without
        // implying a terminal auth failure.
        public void BeginAuth()
        {
            ApplyModelView(_model.BeginQuery());
        }

        // Confirmed auth (logged_in=true) but before usage returns. The model
        // decides whether to restore same-scope data ("更新中") or keep loading.
        public void ApplyAuthProgress(QueryOutcome o)
        {
            ApplyModelView(_model.OnAuthResult(true, o.Identity, QuotaStatus.Ok, null));
        }

        public void SetIntro(string text)
        {
            // UX011 removes the first-run banner; keep this compatibility hook inert.
        }

        // Test hook: whether the first-run banner is actually rendered.
        public bool HasIntroCard { get { return _introCard != null; } }

        public void ApplyUsageResult(QueryOutcome o)
        {
            ApplyModelView(_model.OnUsageResult(o.Snapshot, o.Viewer, o.Verdict, null));
        }

        public void ApplyUsageFailure(QuotaStatus status, string message)
        {
            ApplyModelView(_model.OnUsageFailure(status, message, null));
        }

        public void ApplyCancelled()
        {
            ApplyModelView(_model.OnCancelled(null));
        }

        public bool IsQueryingUi
        {
            get { return _view != null && _view.AllowCancel; }
        }

        private void RebuildContent(QuotaSnapshot snap)
        {
            RenderCards(snap);
        }

        private void RenderCards(QuotaSnapshot snap)
        {
            Screen openScr = ActiveScreen();
            Rectangle wa = openScr.WorkingArea;
            // UX022: the initial card width is the clamped logical card width;
            // SizeToFit (after the cards exist) owns the final two-pass
            // measurement and any scrollbar adjustment.
            _contentWidth = _cardWidth =
                LayoutMath.ClampWidth(PanelPositioner.LogicalCardWidth, _scale, wa.Width);

            BuildCards(snap);
            SyncStatusInPlace();
            SizeToFit();
        }

        private void BuildCards(QuotaSnapshot snap)
        {
            if (_rebuilding) return;
            _rebuilding = true;
            try
            {
                List<Control> next = new List<Control>();

                _content.SuspendLayout();
                try
                {
                    if (snap == null)
                    {
                        next.Add(BuildMessageCard("暂无数据", TextMuted));
                    }
                    else
                    {
                        bool hasCards = false;
                        if (snap.Products != null)
                        {
                            for (int i = 0; i < snap.Products.Count; i++)
                            {
                                next.Add(BuildProductCard(snap.Products[i], null));
                                hasCards = true;
                            }
                        }
                        if (!hasCards)
                        {
                            // Empty products (NoSubscription / unknown status): show
                            // the explicit snapshot message rather than a blank panel.
                            string msg = !string.IsNullOrEmpty(snap.Message)
                                ? snap.Message : "当前身份下未发现已订阅的方舟套餐。";
                            Color c = (snap.Status == QuotaStatus.NotLoggedIn
                                || snap.Status == QuotaStatus.CliMissing) ? TextDark : TextMuted;
                            next.Add(BuildMessageCard(msg, c));
                        }
                    }
                    ReplaceContent(next);
                }
                finally
                {
                    _content.ResumeLayout();
                }
            }
            finally
            {
                _rebuilding = false;
            }
        }

        private void ClearContent()
        {
            Control[] old = new Control[_content.Controls.Count];
            _content.Controls.CopyTo(old, 0);
            _content.Controls.Clear();
            for (int i = 0; i < old.Length; i++)
            {
                try { old[i].Dispose(); } catch (Exception) { }
            }
        }

        private Panel BuildMessageCard(string text, Color color)
        {
            CardPanel card = new CardPanel();
            card.PadX = S(12);
            StyleCard(card, S(12));
            card.BackColor = Color.White;
            // UX022: re-attach the compact status row after every reflow.
            card.AfterLayout = delegate { SyncStatusInPlace(); };
            card.Paint += delegate(object s, PaintEventArgs e) { DrawCardBorder(e, (Control)s); };
            card.Reflow = delegate(int innerW)
            {
                ClearCard(card);
                Label lbl = new Label();
                lbl.Font = F(9.5f, false);
                lbl.ForeColor = color;
                lbl.AutoSize = false;
                lbl.Text = text;
                int textH = MeasureWrappedHeight(text, lbl.Font, innerW);
                lbl.SetBounds(card.PadX, S(12), innerW, textH);
                card.Controls.Add(lbl);
                card.Height = textH + S(24);
            };
            card.Width = _cardWidth > 0 ? _cardWidth : S(300);
            card.ForceLayout();
            // UX022: no tail gap — ApplyCardWidths puts the gap ONLY between cards.
            card.Margin = new Padding(0);
            return card;
        }

        private static void ClearCard(Control card)
        {
            Control[] old = new Control[card.Controls.Count];
            card.Controls.CopyTo(old, 0);
            card.Controls.Clear();
            for (int i = 0; i < old.Length; i++)
            {
                try { old[i].Dispose(); } catch (Exception) { }
            }
        }

        private static int MeasureWrappedHeight(string text, Font font, int width)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            try
            {
                using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                using (StringFormat fmt = new StringFormat())
                {
                    fmt.Trimming = StringTrimming.Word;
                    SizeF sz = g.MeasureString(text, font, Math.Max(1, width), fmt);
                    return (int)Math.Ceiling(sz.Height) + 2;
                }
            }
            catch (Exception)
            {
                return 18;
            }
        }

        private int ScrollbarWidth()
        {
            try { return SystemInformation.VerticalScrollBarWidth; }
            catch (Exception) { return 17; }
        }

        // Applies the DPI-scaled card corner radius once, so the clipped Region
        // (CardPanel) and the drawn border (DrawCardBorder) are always equal.
        private static void StyleCard(CardPanel card, int scale)
        {
            card.RegionRadius = scale;
        }

        private Panel BuildProductCard(ProductQuota pq, RiskSummary risk)
        {
            CardPanel card = new CardPanel();
            card.PadX = S(16);
            StyleCard(card, S(12));
            card.BackColor = Color.White;
            // UX022: re-attach the compact status row after every reflow.
            card.AfterLayout = delegate { SyncStatusInPlace(); };
            card.Paint += delegate(object s, PaintEventArgs e) { DrawCardBorder(e, (Control)s); };

            card.Reflow = delegate(int innerW)
            {
                ClearCard(card);
                int pad = card.PadX;
                int y = S(14);

                Label title = new Label();
                title.Font = F(10.5f, true);
                title.ForeColor = TextDark;
                title.AutoSize = false;
                title.Text = ProductTitle(pq);
                title.SetBounds(pad, y, innerW, S(22));
                card.Controls.Add(title);
                y += S(26);

                if (!pq.SubscribedKnown)
                    y = AddLine(card, "订阅状态未知", TextMuted, pad, y, innerW);
                else if (!pq.Subscribed && pq.Error == null && !pq.PeriodErrorPresent && !pq.Malformed)
                    y = AddLine(card, "未订阅", TextMuted, pad, y, innerW);

                if (pq.Error != null)
                    y = AddLine(card, pq.Error, ErrorColor, pad, y, innerW);
                else if (pq.Periods.Count == 0)
                    y = AddLine(card, "无周期数据。", TextMuted, pad, y, innerW);

                for (int i = 0; i < pq.Periods.Count; i++)
                {
                    y = BuildPeriodRows(card, pq.Periods[i], pad, y, innerW, false);
                    if (i < pq.Periods.Count - 1) y += S(4);
                }

                card.Height = y + S(12);
                card.Tag = pq.Periods.Count;
            };

            card.Width = _cardWidth > 0 ? _cardWidth : S(300);
            card.ForceLayout();
            // UX022: no tail gap — ApplyCardWidths puts the gap ONLY between cards.
            card.Margin = new Padding(0);
            return card;
        }

        private int AddLine(Control card, string text, Color color, int pad, int y, int innerW)
        {
            Label l = MakeLine(text, color, innerW);
            l.SetBounds(pad, y, innerW, S(16));
            card.Controls.Add(l);
            return y + S(18);
        }

        private int BuildPeriodRows(Control card, PeriodQuota p, int pad, int y, int innerW, bool isRisk)
        {
            Label name = new Label();
            name.Font = F(9f, false);
            name.ForeColor = TextMuted;
            name.AutoSize = false;
            name.TextAlign = ContentAlignment.MiddleLeft;
            name.Text = p.LabelDisplay;
            name.SetBounds(pad, y, innerW / 2, S(26));
            card.Controls.Add(name);

            Label pct = new Label();
            pct.AutoSize = false;
            pct.TextAlign = ContentAlignment.MiddleRight;
            if (p.Error != null)
            {
                pct.Font = F(10f, true);
                pct.ForeColor = ErrorColor;
                pct.Text = "获取失败";
            }
            else if (p.PercentKnown)
            {
                pct.Font = F(14f, true);
                pct.ForeColor = TextDark;
                pct.Text = PercentFormat.RemainingForBar(p.RemainingPercent);
            }
            else
            {
                pct.Font = F(10.5f, true);
                pct.ForeColor = TextMuted;
                pct.Text = "剩余未知";
            }
            pct.SetBounds(pad + innerW / 2, y, innerW - innerW / 2, S(26));
            card.Controls.Add(pct);
            y += S(28);

            if (p.Error != null)
            {
                Label e = MakeLine(p.Error, ErrorColor, innerW);
                e.SetBounds(pad, y, innerW, S(16));
                card.Controls.Add(e);
                return y + S(18);
            }

            QuotaBar bar = new QuotaBar();
            bar.Value = p.PercentKnown ? p.RemainingPercent : -1;
            bar.SetBounds(pad, y, innerW, S(9));
            card.Controls.Add(bar);
            y += S(9) + S(6);

            if (p.AmountKnown)
            {
                y = AddLine(card, "剩余 " + DisplayNames.Number(p.RemainingAmount) + " 额度",
                    TextMuted, pad, y, innerW);
            }

            if (p.HasReset)
            {
                Label r = MakeLine("重置 " + DisplayNames.FormatTime(p.ResetLocal), TextMuted, innerW);
                r.SetBounds(pad, y, innerW, S(16));
                card.Controls.Add(r);
                y += S(17);
            }

            if (p.Clamped && p.UnknownNote != null)
            {
                Label n = MakeLine(p.UnknownNote, WarningColor, innerW);
                n.SetBounds(pad, y, innerW, S(16));
                card.Controls.Add(n);
                y += S(17);
            }
            else if (!p.PercentKnown && p.UnknownNote != null)
            {
                Label n = MakeLine(p.UnknownNote, TextMuted, innerW);
                n.SetBounds(pad, y, innerW, S(16));
                card.Controls.Add(n);
                y += S(17);
            }

            return y;
        }

        private static double ClampPercent(double v)
        {
            if (v < 0) return 0;
            if (v > 100) return 100;
            return v;
        }

        private Label MakeLine(string text, Color color, int width)
        {
            Label l = new Label();
            l.Font = F(8.25f, false);
            l.ForeColor = color;
            l.AutoSize = false;
            l.Text = text;
            return l;
        }

        private void DrawCardBorder(PaintEventArgs e, Control card)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            // Use the SAME DPI-scaled radius as the card's clipped Region so the
            // drawn border and the region agree at every DPI (a fixed 12px border
            // on a scaled region left a mismatched corner).
            int radius = card is CardPanel ? ((CardPanel)card).RegionRadius : S(12);
            using (Pen pen = new Pen(CardBorder))
            using (GraphicsPath path = UiStyle.RoundedRectangle(
                new Rectangle(0, 0, card.Width - 1, card.Height - 1), radius))
                e.Graphics.DrawPath(pen, path);
        }

        private static string ProductTitle(ProductQuota pq)
        {
            // The product name already carries "团队版" when applicable; do not
            // repeat the edition, to avoid "团队版 · 团队版".
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append(pq.DisplayName);
            string ed = DisplayNames.Edition(pq.Edition);
            string tier = DisplayNames.Tier(pq.Tier);
            bool teamName = pq.DisplayName != null
                && pq.DisplayName.IndexOf("团队", StringComparison.Ordinal) >= 0;
            if (ed != null && !(teamName && ed == "团队版")) sb.Append(" · ").Append(ed);
            if (tier != null) sb.Append(" · ").Append(tier);
            return sb.ToString();
        }

        // ---- public layout test hooks (used by the offline layout smoke test) ----

        public void BeginLayoutSession(Screen screen)
        {
            _openScreen = screen;
            _openScale = DpiUtil.GetScale(screen);
            _scale = _openScale;
            SizeToFit(false);
            _hide.State(true, Environment.TickCount);
            if (!_clock.Enabled) _clock.Start();
        }

        // Synthetic identity used only by the offline smoke/test hooks. No real
        // account identifier is ever embedded (all-zero account, sample user).
        internal static AuthIdentity SampleIdentity()
        {
            AuthIdentity id = new AuthIdentity();
            id.Present = true;
            id.Name = "sample";
            id.Type = "volc-sso";
            id.OwnerTrn = "trn:iam::000000000000:user/sample";
            id.Region = "cn-beijing";
            id.Project = "default";
            return id;
        }

        public void ForceLoading() { ApplyModelView(_model.BeginQuery()); }
        public void ForceRender(QuotaSnapshot snap)
        {
            _model.OnAuthResult(true, SampleIdentity(), QuotaStatus.Ok, null);
            ApplyModelView(_model.OnUsageResult(snap, null, ScopeVerdict.Same, null));
        }

        public void ForceError(QuotaStatus status, string message)
        {
            ApplyModelView(_model.OnUsageFailure(status, message, null));
        }

        public void ForceIdentityChanged()
        {
            ApplyModelView(_model.OnUsageResult(null, null, ScopeVerdict.Mismatch, null));
        }

        public int CurrentHeight { get { return Height; } }
        public int CurrentWidth { get { return Width; } }
        public int ContentCardCount { get { return _content.Controls.Count; } }
        public Rectangle CurrentBounds { get { return Bounds; } }
        public Screen OpenScreenRef { get { return ActiveScreen(); } }
        public PanelState CurrentState { get { return _view != null ? _view.State : PanelState.Loading; } }

        // Test hook: whether the model still holds a reusable snapshot.
        public bool ModelHasLast { get { return _model.Last != null; } }
        // ---- UX019 / UX022 test hooks (offline; never the real clipboard) ----

        // UX022: the real details-local context menu and its four actions.
        internal ContextMenuStrip DetailsMenuForTest { get { return _detailsMenu; } }
        internal ToolStripMenuItem RefreshMenuItemForTest { get { return _miRefresh; } }
        internal ToolStripMenuItem CopyMenuItemForTest { get { return _miCopy; } }
        internal ToolStripMenuItem SettingsMenuItemForTest { get { return _miSettings; } }
        internal ToolStripMenuItem CloseMenuItemForTest { get { return _miClose; } }
        internal bool MenuOpenForTest { get { return _menuOpen; } }
        internal System.Windows.Forms.ToolTip ToolTipForTest { get { return _tip; } }
        internal System.Windows.Forms.Timer CopyFeedbackTimerForTest { get { return _copyFeedback; } }
        internal string CopyFeedbackTextForTest { get { return _copyFeedbackText; } }

        // Offline tests inject a recorder instead of Clipboard.SetText.
        internal Action<string> ClipboardSetForTest
        {
            get { return _clipboardSet; }
            set { _clipboardSet = value; }
        }

        // Layout matrix injection: a scale change must REBUILD the cards
        // (fonts / paddings / radius / height are scale-derived) and re-fit
        // the window; the per-card layout cache is invalidated so a
        // same-width card still reflows.
        internal void SetScaleForTest(double scale)
        {
            _scale = scale;
            InvalidateCardLayouts();
            RelayoutContent();
        }

        internal FlowLayoutPanel ContentForTest { get { return _content; } }
        internal bool DialogOpenForTest { get { return _dialogOpen; } }
        internal System.Windows.Forms.Control ActiveControlForTest { get { return ActiveControl; } }
        // UX022: consumed by the owner — re-anchors the visible window against
        // the CURRENT circle after a visible size change.
        internal bool ConsumePendingReposition()
        {
            bool pending = _pendingReposition;
            _pendingReposition = false;
            return pending;
        }
        // UX022: the footer update time moved onto the cards' tooltip.
        internal string UpdateTimeTextForTest
        {
            get
            {
                return _content.Controls.Count > 0
                    ? _tip.GetToolTip(_content.Controls[0]) : "";
            }
        }

        public System.Collections.Generic.List<Control> ContentControls
        {
            get
            {
                System.Collections.Generic.List<Control> list =
                    new System.Collections.Generic.List<Control>();
                foreach (Control c in _content.Controls) list.Add(c);
                return list;
            }
        }
    }

    internal static class SyntheticSample
    {
        // Anonymous synthetic data. No real account identifiers are ever stored.
        public const string Json =
            "{\"items\":[" +
            "{\"product\":\"agent-plan\",\"edition\":\"personal\",\"tier\":\"medium\",\"subscribed\":true,\"periods\":[" +
            "{\"label\":\"5h\",\"used\":250,\"total\":1000,\"percent\":25,\"reset_at\":\"2026-10-03T14:43:59+08:00\"}," +
            "{\"label\":\"weekly\",\"used\":700,\"total\":1000,\"percent\":70,\"reset_at\":\"2026-10-05T00:00:00+08:00\"}]}," +
            "{\"product\":\"coding-plan\",\"edition\":\"personal\",\"subscribed\":true,\"periods\":[" +
            "{\"label\":\"monthly\",\"percent\":40,\"reset_at\":\"2026-10-28T23:59:59+08:00\"}]}]}";

        public const string JsonLarge =
            "{\"items\":[" +
            "{\"product\":\"agent-plan\",\"edition\":\"personal\",\"tier\":\"medium\",\"subscribed\":true,\"periods\":[" +
            "{\"label\":\"5h\",\"used\":250,\"total\":1000,\"percent\":25,\"reset_at\":\"2026-10-03T14:43:59+08:00\"}," +
            "{\"label\":\"weekly\",\"used\":700,\"total\":1000,\"percent\":70,\"reset_at\":\"2026-10-05T00:00:00+08:00\"}," +
            "{\"label\":\"monthly\",\"used\":900,\"total\":1000,\"percent\":90,\"reset_at\":\"2026-10-28T23:59:59+08:00\"}]}," +
            "{\"product\":\"agent-plan-team\",\"edition\":\"team\",\"subscribed\":true,\"periods\":[" +
            "{\"label\":\"5h\",\"used\":10,\"total\":100,\"percent\":10,\"reset_at\":\"2026-10-03T14:43:59+08:00\"}," +
            "{\"label\":\"weekly\",\"percent\":55,\"reset_at\":\"2026-10-05T00:00:00+08:00\"}," +
            "{\"label\":\"monthly\",\"percent\":12,\"reset_at\":\"2026-10-28T23:59:59+08:00\"}]," +
            "\"updated_at\":\"2026-10-03T10:00:00+08:00\"}," +
            "{\"product\":\"coding-plan\",\"edition\":\"personal\",\"subscribed\":true,\"periods\":[" +
            "{\"label\":\"monthly\",\"percent\":40,\"reset_at\":\"2026-10-28T23:59:59+08:00\"}," +
            "{\"label\":\"session\",\"percent\":5,\"reset_at\":\"2026-10-03T18:00:00+08:00\"}]}]}";

        public static QuotaSnapshot Build()
        {
            return QuotaParser.Parse(Json);
        }

        public static QuotaSnapshot BuildLarge()
        {
            return QuotaParser.Parse(JsonLarge);
        }
    }

    // Shared runtime/test controller; callers invoke Poll on the same UI thread.
    internal sealed class SnapshotController : IDisposable
    {
        public const int PollIntervalMs = 300000;
        // v0.5 UX013: faster interval while the circle or the details panel is
        // visible; the base PollIntervalMs stays the all-hidden rate.
        public const int VisiblePollIntervalMs = 10000;
        private readonly PanelModel _model;
        private readonly Func<IProgress<QueryProgress>, CancellationToken, Task<QueryOutcome>> _query;
        private readonly Action<PanelView> _commit;
        private readonly Action _show;
        private readonly Func<CachedSnapshot, bool> _save;
        private CancellationTokenSource _active;
        private bool _started;
        private bool _disposed;
        private int _generation;
        public bool Querying { get { return _active != null; } }

        public SnapshotController(PanelModel model,
            Func<IProgress<QueryProgress>, CancellationToken, Task<QueryOutcome>> query,
            Action<PanelView> commit, Action show, Func<CachedSnapshot> load,
            Func<CachedSnapshot, bool> save, Action clear)
        {
            _model = model; _query = query; _commit = commit; _show = show; _save = save;
            _model.EvictPersistent = clear;
            CachedSnapshot cached = load();
            _commit(cached == null ? model.ShowEmptyNoData()
                : model.ShowSnapshot(PersistentStateStore.FromCache(cached), cached.ScopeFingerprint));
        }

        public void Open() { if (!_disposed) _show(); }
        public Task Start()
        {
            if (_started || _disposed) return Task.FromResult(0);
            _started = true;
            return Refresh();
        }
        public Task Poll() { return Refresh(); }
        public async Task Refresh()
        {
            if (_disposed || Querying) return;
            CancellationTokenSource cts = new CancellationTokenSource();
            _active = cts;
            int generation = ++_generation;
            _model.BeginQuery();
            try
            {
                Progress<QueryProgress> progress = new Progress<QueryProgress>(delegate(QueryProgress p)
                {
                    if (_disposed || _active != cts || generation != _generation || p == null
                        || !p.AuthConfirmed || p.Stage != QueryStage.Auth) return;
                    QueryScope scope = QueryScope.FromAuth(p.AuthIdentity);
                    if (scope.IsKnown && _model.ConfirmedFingerprint != null
                        && scope.Fingerprint != _model.ConfirmedFingerprint)
                        _commit(_model.OnAuthResult(true, p.AuthIdentity, QuotaStatus.Ok, null));
                });
                QueryOutcome result;
                try { result = await _query(progress, cts.Token); }
                catch (OperationCanceledException) { result = Failure(QuotaStatus.Cancelled); }
                catch (Exception) { result = Failure(QuotaStatus.Failed); }
                if (_disposed || generation != _generation) return;
                PanelView view = _model.CommitOutcome(result ?? Failure(QuotaStatus.Failed));
                if (view.NewData && view.Persist)
                    _save(PersistentStateStore.ToCache(view.Data, _model.ConfirmedFingerprint));
                _commit(view);
            }
            finally { _active = null; cts.Dispose(); }
        }
        private static QueryOutcome Failure(QuotaStatus status)
        {
            return new QueryOutcome { Snapshot = new QuotaSnapshot {
                Status = status, Message = "未能更新，请稍后重试。" } };
        }
        public void Dispose()
        {
            _disposed = true; ++_generation;
            if (_active != null) _active.Cancel();
        }
    }

    // Runtime: tray icon, context menu, single-instance IPC, silent-start
    // ApplicationContext, refresh orchestrator and first-run marker.
    internal sealed class TrayApp : ApplicationContext
    {
        public const string MutexNameBase = @"Local\ark_left_single_instance";
        public const string ShowEventNameBase = @"Local\ark_left_show_event";
        private const int ShowSignalRetries = 25;

        private NotifyIcon _notify;
        private Icon _trayIcon;
        private ContextMenuStrip _menu;
        private ToolStripMenuItem _menuToggle;
        private ToolStripMenuItem _menuLock;
        // v0.9 UX017: checkable "减少动画" shared menu item (after the lock).
        private ToolStripMenuItem _menuMotion;
        // v0.12 UX020: "悬浮窗归位" shared menu item (after 减少动画).
        private ToolStripMenuItem _menuHome;
        // v0.8 UX016 fix: offline-observable intent counters for the failed
        // lock-save tray notification (no real NotifyIcon exists offline).
        private int _lockFailNotifyCount;
        private string _lastLockFailText;
        // v0.9 UX017: distinct intent counter for the failed reduce-motion
        // save notification; the wording must never claim a lock failure.
        private int _motionFailNotifyCount;
        private string _lastMotionFailText;
        private PopupForm _form;
        private FloatingQuotaForm _floating;
        private QuotaCli _cli;
        private SnapshotController _controller;
        private readonly System.Windows.Forms.Timer _poll = new System.Windows.Forms.Timer();
        private bool _disposed;

        private TrayApp() : this(null, null) { }

        // Offline tests use the production handlers without CLI, state or IPC.
        internal TrayApp(Func<IProgress<QueryProgress>, CancellationToken, Task<QueryOutcome>> query)
            : this(query, null) { }

        // prefsSaveOverride: offline tests may inject a FAILING preference save
        // to exercise the tray-notification path without any real state file.
        internal TrayApp(Func<IProgress<QueryProgress>, CancellationToken, Task<QueryOutcome>> query,
            Func<FloatingPreferences, bool> prefsSaveOverride)
        {
            if (query == null) _cli = new QuotaCli(null);
            _form = new PopupForm();
            // The circle consumes the SAME PanelView. Offline tests inject
            // null-returning / no-op preference callbacks so no real preference
            // file is read or written; the production path uses the store.
            // v0.8 UX016: offline tests also inject no-op preference callbacks
            // so floating-preferences.json is never touched.
            _floating = query == null
                ? new FloatingQuotaForm()
                : new FloatingQuotaForm(delegate { return (FloatingSettings)null; },
                    delegate(FloatingSettings s) { return true; },
                    delegate { return (FloatingPreferences)null; },
                    prefsSaveOverride != null ? prefsSaveOverride
                        : new Func<FloatingPreferences, bool>(
                            delegate(FloatingPreferences p) { return true; }));
            _floating.DetailsRequested += delegate { ShowDetails(); };
            _floating.SettingsRequested += delegate { OpenSettings(); };
            _floating.ExitRequested += delegate { ExitApp(); };
            // v0.8 UX016 fix: bound right after _floating exists, before any
            // toggle can happen; the handler is safe both before _notify is
            // created and after Dispose nulls it.
            _floating.LockSaveFailed += delegate { OnLockSaveFailed(); };
            // v0.9 UX017: same binding discipline as LockSaveFailed - bound
            // right after _floating exists, handler is _disposed / _notify safe.
            _floating.ReduceMotionChanged += delegate { SyncLockChecked(); };
            _floating.MotionSaveFailed += delegate { OnMotionSaveFailed(); };
            _controller = new SnapshotController(_form.Model, query ?? _cli.QueryDetailedAsync,
                delegate(PanelView v)
                {
                    _form.ApplyModelView(v); _floating.ApplyModelView(v);
                    // UX022: when the visible data changed the fitted size
                    // while the details are open, re-anchor against the
                    // CURRENT circle (never the mouse screen).
                    ConsumeDetailsReposition();
                },
                ShowFloating,
                query == null ? (Func<CachedSnapshot>)PersistentStateStore.Load : delegate { return null; },
                query == null ? (Func<CachedSnapshot, bool>)PersistentStateStore.Save : delegate { return true; },
                query == null ? (Action)PersistentStateStore.Clear : delegate { });
            _poll.Interval = SnapshotController.PollIntervalMs;
            _poll.Tick += async delegate { await _controller.Poll(); };
            _form.RefreshRequested += delegate { StartRefresh(); };
            // v0.6 UX015 / v0.14 UX022: the details' settings action (now the
            // card context-menu item; the header button was removed with the
            // chrome) routes to the SAME single settings path as the circle /
            // tray menu (zero query; the modal is guarded inside
            // FloatingQuotaForm). No query, no change to the details timer on
            // return.
            _form.SettingsRequested += delegate { OpenSettings(); };
            // v0.5 UX013: the real circle forwards its own VisibleChanged (the
            // wrapper's 1x1 window visibility never changes), and the details
            // panel forwards after its restore handler so the target interval is
            // computed from the post-close circle state.
            _floating.CircleVisibleChanged += delegate { UpdatePollInterval(); };
            // Restore an auto-hidden circle whenever the details panel closes,
            // whatever path closed it (close button, Esc, WM_CLOSE), then
            // re-evaluate the polling interval.
            _form.VisibleChanged += delegate
            {
                if (!_form.Visible) OnDetailsHidden();
                UpdatePollInterval();
            };

            // The shared button group is built for BOTH production and offline
            // tests so the real menu / settings path is exercised; only the
            // NotifyIcon / tray registration / IPC stay production-only.
            _menu = new ContextMenuStrip();
            _menu.Items.Add("查看全部额度", null, delegate { ShowDetails(); });
            _menu.Items.Add("设置", null, delegate { OpenSettings(); });
            // v0.8 UX016: checkable "锁定位置" shared with the circle menu —
            // same state / handler inside FloatingQuotaForm; the check is
            // re-synced from the real state on change and on menu opening.
            _menuLock = new ToolStripMenuItem("锁定位置");
            _menuLock.CheckOnClick = false; // state is owned by FloatingQuotaForm
            _menuLock.Click += delegate { _floating.TogglePositionLocked(); };
            _menu.Items.Add(_menuLock);
            // v0.9 UX017: checkable "减少动画" shared with the circle menu -
            // same state / handler inside FloatingQuotaForm; inserted right
            // after the lock item per the frozen UX017 order.
            _menuMotion = new ToolStripMenuItem("减少动画");
            _menuMotion.CheckOnClick = false; // state is owned by FloatingQuotaForm
            _menuMotion.Click += delegate { _floating.ToggleReduceMotion(); };
            _menu.Items.Add(_menuMotion);
            // v0.12 UX020: "悬浮窗归位" right after "减少动画" (one shared
            // item for the tray and the circle right-click menu). Explicit
            // re-home: zero query, nothing persisted, the lock never blocks
            // it; the show/hide toggle shifts from index 4 to 5.
            _menuHome = new ToolStripMenuItem("悬浮窗归位", null,
                delegate { RepositionFloatingHome(); });
            _menu.Items.Add(_menuHome);
            _menuToggle = new ToolStripMenuItem("隐藏悬浮窗", null, delegate { ToggleFloating(); });
            _menu.Items.Add(_menuToggle);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add("退出 ark_left", null, delegate { ExitApp(); });
            UiStyle.StyleMenu(_menu);
            _floating.SetContextMenuStrip(_menu);
            _floating.PositionLockChanged += delegate { SyncLockChecked(); };
            SyncLockChecked();

            // Force both handles so BeginInvoke / click simulation work while
            // hidden.
            IntPtr h = _form.Handle;
            IntPtr fh = _floating.Handle;
            GC.KeepAlive(h);
            GC.KeepAlive(fh);

            if (query != null) return; // offline: no NotifyIcon / IPC / real prefs

            _trayIcon = IconArt.CreateIcon(32);
            _notify = new NotifyIcon();
            _notify.Icon = _trayIcon;
            _notify.Text = "ark_left 方舟订阅额度";
            _notify.Visible = true;
            _notify.ContextMenuStrip = _menu;
            _notify.MouseDown += OnTrayMouseDown;
            _notify.MouseClick += OnTrayClick;

            _menu.Opening += delegate
            {
                _form.SetMenuOpen(true);
                UpdateToggleText();
                SyncLockChecked();
            };
            _menu.Closed += delegate { _form.SetMenuOpen(false); };

            // First launch shows the circle (marker still decides silent start).
        }

        private static volatile TrayApp _instance;

        // Single-instance names. Tests may suffix them via ARK_LEFT_INSTANCE_SUFFIX
        // (normally empty) so an isolated test never signals the real user instance.
        public static string MutexName { get { return MutexNameBase + InstanceSuffix; } }
        public static string ShowEventName { get { return ShowEventNameBase + InstanceSuffix; } }

        private static string InstanceSuffix
        {
            get
            {
                try
                {
                    string s = Environment.GetEnvironmentVariable("ARK_LEFT_INSTANCE_SUFFIX");
                    return string.IsNullOrEmpty(s) ? "" : "_" + s;
                }
                catch (Exception) { return ""; }
            }
        }

        // Single-instance flow. Returns true when the signal was delivered.
        public static int Run(string[] args, bool forceShow)
        {
            bool createdNew;
            Mutex mutex = new Mutex(true, MutexName, out createdNew);
            if (!createdNew)
            {
                // Another instance owns the tray; ask it to show, then exit
                // silently (no MessageBox). Any second launch wakes the window;
                // --show only controls whether the first instance opens at start.
                bool signalled = TryShowExisting();
                try { mutex.Dispose(); } catch (Exception) { }
                return signalled ? 0 : 1;
            }
            try
            {
                bool firstRun = !Marker.Exists();

                _instance = new TrayApp();
                _showRegistration = Ipc.Register(ShowEventName,
                    delegate { if (_instance != null) _instance.OnExternalShow(); });

                bool show = forceShow || firstRun;
                if (firstRun) Marker.WriteFirstRun();
                // Queue (do not call directly) so the query runs on the UI thread
                // with a WindowsFormsSynchronizationContext installed by Run.
                if (show) _instance.QueueShow();
                _instance._form.BeginInvoke(new Action(async delegate {
                    _instance._poll.Start();
                    await _instance._controller.Start();
                }));

                Application.Run(_instance);
                _instance.Cleanup();
                return 0;
            }
            finally
            {
                if (_showRegistration != null) { try { _showRegistration.Dispose(); } catch (Exception) { } }
                try { mutex.ReleaseMutex(); } catch (Exception) { }
                try { mutex.Dispose(); } catch (Exception) { }
            }
        }

        private static IDisposable _showRegistration;

        private void QueueShow()
        {
            try { _form.BeginInvoke(new Action(delegate { ShowAndRefresh(); })); }
            catch (Exception) { }
        }

        // Best-effort bounded signal to the owning instance. Returns true when a
        // handle was found and set. Does not depend on --show.
        private static bool TryShowExisting()
        {
            return Ipc.Signal(ShowEventName, ShowSignalRetries, 100);
        }

        private void OnExternalShow()
        {
            if (_form == null) return;
            try
            {
                _form.BeginInvoke(new Action(delegate { ShowAndRefresh(); }));
            }
            catch (Exception) { }
        }

        private void ShowAndRefresh()
        {
            _controller.Open();
        }

        // Default open entry (start / --show / IPC / tray toggle): show ONLY the
        // floating circle, no query. The details panel opens only on the explicit
        // "查看全部额度" action or a circle click.
        private void ShowFloating()
        {
            _floating.ShowCircle();
            if (_form.Visible) _form.HidePanel();
        }

        // Circle left-click / Enter / Space and the "查看全部额度" menu item:
        // show the details WITHOUT any query. Layout never covers the circle.
        private void ShowDetails()
        {
            if (_form.Visible) return;
            // Ensure the circle is initialised/positioned first (a silent marker
            // start has never opened it), so its screen is real. Then let the
            // details complete its layout on the CIRCLE's screen before sizing,
            // and finally place it relative to the circle with the REAL size.
            _floating.ShowCircle();
            _form.ShowPanel(_floating.CircleScreen());
            _floating.PrepareDetails(_form.Size);
            _form.Bounds = _floating.PendingDetailsBounds;
        }

        // UX022 v0.14: consumed after view changes — when the fitted size
        // actually changed while visible, re-run the circle-relative placement
        // with the FINAL size. Zero query; focus / scroll are untouched.
        private void ConsumeDetailsReposition()
        {
            if (!_form.ConsumePendingReposition()) return;
            if (!_form.Visible || _form.IsDisposed || _form.Disposing) return;
            try
            {
                _floating.PrepareDetails(_form.Size);
                _form.Bounds = _floating.PendingDetailsBounds;
            }
            catch (Exception) { }
        }

        // Called when the details panel hides; restores ONLY an auto-hidden
        // circle (a user-hidden circle is never resurrected).
        private void OnDetailsHidden()
        {
            try
            {
                _floating.RestoreAfterDetails();
            }
            catch (Exception) { }
        }

        private void ShowFromMenu()
        {
            ShowDetails();
        }

        // Single shared settings entry for the circle right-click menu, the tray
        // menu and the details-header button. All three use the same
        // single-instance modal inside FloatingQuotaForm (a repeat call
        // activates the existing dialog, never stacks a second one). While the
        // modal is up the details panel's focus-loss hide is suppressed, so
        // returning from the modal never accidentally closes the details.
        // UX015: the visible details panel OWNS the modal (its screen decides
        // DPI / work area, and focus returns to it); otherwise the REAL circle
        // surface owns it (screen / DPI follow the circle — the wrapper form
        // itself is a hidden 1x1 window). With neither visible nothing is
        // forced open. The
        // release is guarded: a repeat request that arrives while the modal is
        // already up returns immediately after activating it — its finally must
        // NOT end the suppression window while the first request is still
        // blocked inside ShowDialog.
        private void OpenSettings()
        {
            IWin32Window owner;
            if (_form.Visible) owner = _form;
            else if (_floating.CircleVisible) owner = _floating.CircleSurface;
            else owner = _floating;
            if (_form.Visible) _form.SetDialogOpen(true);
            try
            {
                _floating.OpenSettings(owner);
            }
            finally
            {
                if (!_floating.SettingsModalOpen) _form.SetDialogOpen(false);
            }
        }

        internal void OpenSettingsForTest() { OpenSettings(); }

        private void ToggleFloating()
        {
            if (_floating.CircleVisible)
            {
                // Hide circle AND its details together (user intent).
                _floating.HideCircle();
                if (_form.Visible) _form.HidePanel();
            }
            else
            {
                _floating.ShowCircle();
            }
            UpdateToggleText();
        }

        // v0.12 UX020: explicit "悬浮窗归位". Zero query, zero persistence;
        // the position lock never blocks it. While the settings modal is up
        // the handler returns without moving anything (a real user cannot
        // open the menu over the modal either — this keeps a programmatic
        // click equally safe and never moves the modal's owner). If the
        // details are visible they are hidden first via the existing
        // HidePanel path, the stale auto-hide flag is cleared through
        // RestoreAfterDetails, and only then does the circle move home — so
        // position and visibility end up consistent and a later details
        // close can never pull the circle back to its old position. Visible
        // changes stay inside the existing UX013 interval rules (the circle
        // VisibleChanged event re-evaluates the poll interval; no immediate
        // query, no timer restructure).
        private void RepositionFloatingHome()
        {
            if (_floating == null || _floating.IsDisposed) return;
            if (_floating.SettingsModalOpen) return;
            if (_form != null && _form.Visible) _form.HidePanel();
            try { _floating.RestoreAfterDetails(); } catch (Exception) { }
            _floating.RepositionCircleHome();
            UpdateToggleText();
        }

        private void UpdateToggleText()
        {
            if (_menuToggle == null || _floating == null) return;
            _menuToggle.Text = _floating.CircleVisible ? "隐藏悬浮窗" : "显示悬浮窗";
        }

        // v0.8 UX016 / v0.9 UX017: the tray checks always mirror the real
        // states (kept on a failed save, updated on a successful one).
        private void SyncLockChecked()
        {
            if (_menuLock == null || _floating == null) return;
            _menuLock.Checked = _floating.PositionLocked;
            if (_menuMotion != null) _menuMotion.Checked = _floating.ReduceMotion;
        }

        // v0.8 UX016 fix: a failed lock save is already visible through the
        // circle tooltip while the circle is shown; when it is hidden, surface
        // a short tray balloon (fixed text, never a raw exception). Never
        // opens the circle / details, never touches polling or queries; a
        // successful toggle never notifies. Offline tests have no real
        // NotifyIcon - they observe the intent through the counters below.
        private void OnLockSaveFailed()
        {
            if (_disposed) return;
            if (_floating != null && _floating.CircleVisible) return;
            _lockFailNotifyCount++;
            _lastLockFailText = "位置锁定状态未保存";
            if (_notify == null) return;
            try
            {
                _notify.ShowBalloonTip(2500, "ark_left", _lastLockFailText,
                    ToolTipIcon.Warning);
            }
            catch (Exception) { }
        }

        // v0.9 UX017: a failed reduce-motion save mirrors the lock path but
        // with DISTINCT fixed wording (never claims the lock failed). Circle
        // visible -> the tooltip path covers it; hidden -> tray balloon
        // intent. Never opens the circle / details, never touches polling or
        // queries; a successful toggle never notifies.
        private void OnMotionSaveFailed()
        {
            if (_disposed) return;
            if (_floating != null && _floating.CircleVisible) return;
            _motionFailNotifyCount++;
            _lastMotionFailText = "减少动画设置未保存";
            if (_notify == null) return;
            try
            {
                _notify.ShowBalloonTip(2500, "ark_left", _lastMotionFailText,
                    ToolTipIcon.Warning);
            }
            catch (Exception) { }
        }

        // v0.5 UX013: while the circle or the details panel is visible, poll at
        // VisiblePollIntervalMs; otherwise fall back to the base interval. Only
        // the Timer interval changes - no immediate query, no timer restart, and
        // a same-value change is skipped so the running countdown is never reset.
        private void UpdatePollInterval()
        {
            if (_disposed) return;
            int interval = (_floating.CircleVisible || _form.Visible)
                ? SnapshotController.VisiblePollIntervalMs
                : SnapshotController.PollIntervalMs;
            if (_poll.Interval != interval) _poll.Interval = interval;
        }

        internal void OpenEntryForTest(string entry)
        {
            HideDetailsForTest();
            _floating.HideCircle();
            if (entry == "firstRun" || entry == "--show") QueueShow();
            else if (entry == "IPC") OnExternalShow();
            else if (entry == "menu")
            {
                // Explicit "查看全部额度": show the circle first (default open
                // entry) then open the details, zero query.
                ShowFloating();
                ShowFromMenu();
            }
            else if (entry == "circle")
            {
                PrepareCircleForTest();
                Point p = _floating.CircleForTest.PointToScreen(new Point(1, 1));
                _floating.CircleForTest.SimulateMouseDownForTest(p);
                _floating.CircleForTest.SimulateMouseUpForTest(p);
            }
            else if (entry == "tray")
            {
                // Exercise the real tray click path from the hidden default:
                // the click deterministically SHOWS the circle (toggle).
                MouseEventArgs e = new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0);
                OnTrayMouseDown(null, e); OnTrayClick(null, e);
            }
            else throw new ArgumentException("Unknown entry");
        }

        internal bool FloatingVisibleForTest { get { return _floating.CircleVisible; } }
        internal bool DetailsVisibleForTest { get { return _form.Visible; } }
        internal PopupForm DetailsFormForTest { get { return _form; } }
        internal FloatingCircleControl CircleForTest { get { return _floating.CircleForTest; } }
        internal Rectangle DetailsBoundsForTest { get { return _form.Bounds; } }
        internal bool SettingsOpenForTest { get { return _floating.SettingsOpenForTest; } }
        internal void HideDetailsForTest() { _form.HidePanel(); }
        internal void ToggleFloatingForTest() { ToggleFloating(); }
        internal int MenuItemCountForTest { get { return _menu == null ? 0 : _menu.Items.Count; } }
        internal string MenuTextForTest(int i)
        {
            return (_menu != null && i >= 0 && i < _menu.Items.Count) ? _menu.Items[i].Text : null;
        }
        internal void PerformMenuForTest(int i)
        {
            if (_menu != null && i >= 0 && i < _menu.Items.Count) _menu.Items[i].PerformClick();
        }
        // v0.8 UX016 test hooks: shared menu check + real lock state.
        internal bool MenuLockCheckedForTest
        {
            get { return _menuLock != null && _menuLock.Checked; }
        }
        internal bool FloatingLockedForTest { get { return _floating.PositionLocked; } }
        internal bool CircleMenuLockCheckedForTest
        {
            get { return _floating.DefaultMenuLockCheckedForTest; }
        }
        internal string LockHintForTest { get { return _floating.LockHintForTest; } }
        // v0.8 UX016 fix: failed-save tray notification intent (offline-safe).
        internal int LockFailNotifyCountForTest { get { return _lockFailNotifyCount; } }
        internal string LastLockFailTextForTest { get { return _lastLockFailText; } }
        // v0.9 UX017 test hooks: shared menu check + real motion state.
        internal bool MenuMotionCheckedForTest
        {
            get { return _menuMotion != null && _menuMotion.Checked; }
        }
        internal bool FloatingReduceMotionForTest { get { return _floating.ReduceMotion; } }
        internal bool CircleMenuMotionCheckedForTest
        {
            get { return _floating.DefaultMenuMotionCheckedForTest; }
        }
        internal int MotionFailNotifyCountForTest { get { return _motionFailNotifyCount; } }
        internal string LastMotionFailTextForTest { get { return _lastMotionFailText; } }

        // Applies synthetic data to both surfaces WITHOUT a query, mirroring the
        // controller commit. Used to drive the real settings path offline.
        internal void ApplyViewForTest(QuotaSnapshot snap)
        {
            PanelModel m = _form.Model;
            m.OnAuthResult(true, PopupForm.SampleIdentity(), QuotaStatus.Ok, null);
            PanelView v = m.OnUsageResult(snap, null, ScopeVerdict.Same, null);
            _form.ApplyModelView(v);
            _floating.ApplyModelView(v);
        }

        internal string SelectedPeriodTextForTest
        {
            get
            {
                FloatingSettings s = _floating.StoredSettingsForTest;
                return s == null ? null : s.PeriodLabel;
            }
        }

        private void PrepareCircleForTest()
        {
            _floating.ShowCircleAtForTest(Screen.PrimaryScreen.WorkingArea, 1.0);
        }

        // Drives exactly one production controller query (offline injected
        // delegate) and returns after it has committed.
        internal void RunQueryForTest()
        {
            try { _controller.Start().Wait(5000); } catch (Exception) { }
            System.Windows.Forms.Application.DoEvents();
        }

        // v0.5 UX013 test hooks: observe / start the SAME production timer (no
        // new timer, no query). StartPollingForTest does not fire the start
        // query path; it only begins the existing _poll timer.
        internal int PollIntervalForTest { get { return _poll.Interval; } }
        internal void StartPollingForTest() { if (!_disposed) _poll.Start(); }

        // Mouse-down captures visibility and opens the activate-suppression window
        // BEFORE any focus-loss from the click can be processed.
        private void OnTrayMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) _floating.TrayDown();
        }

        private void OnTrayClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            bool wantsHide = _floating.WantsHideOnTrayClick();
            if (wantsHide) _floating.HideCircle();
            else
            {
                // Tray left-click toggles the circle only (details stays closed);
                // no query, no dependency on details focus state.
                _floating.ShowCircle();
            }
            if (_form.Visible) _form.HidePanel();
            UpdateToggleText();
        }

        private async void StartRefresh()
        {
            await _controller.Refresh();
        }


        // One UI commit from the authoritative final outcome, independent of Progress.
        internal static void ApplyFinalOutcome(PopupForm form, QueryOutcome outcome)
        {
            if (form == null || outcome == null) return;
            form.ApplyModelView(form.Model.CommitOutcome(outcome));
        }


        private void ExitApp()
        {
            _poll.Stop();
            _controller.Dispose();
            try { if (_cli != null) _cli.KillActive(); } catch (Exception) { }
            if (_notify != null) _notify.Visible = false;
            if (_floating != null)
            {
                _floating.CloseSettings();
                _floating.AllowClose = true;
                try { _floating.Dispose(); } catch (Exception) { }
            }
            if (_form != null)
            {
                _form.AllowClose = true;
                try { _form.Close(); } catch (Exception) { }
            }
            if (_menu != null) { try { _menu.Dispose(); } catch (Exception) { } }
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
                _poll.Dispose();
                if (_controller != null) _controller.Dispose();
                try { if (_notify != null) { _notify.Visible = false; _notify.Dispose(); } } catch (Exception) { }
                try { if (_floating != null) _floating.Dispose(); } catch (Exception) { }
                try { if (_menu != null) _menu.Dispose(); } catch (Exception) { }
                try { if (_trayIcon != null) _trayIcon.Dispose(); } catch (Exception) { }
                try { if (_cli != null) _cli.Dispose(); } catch (Exception) { }
                try { if (_form != null) _form.Dispose(); } catch (Exception) { }
                _notify = null; _menu = null; _trayIcon = null; _cli = null; _form = null;
                _floating = null;
            }
            base.Dispose(disposing);
        }

        private void Cleanup()
        {
            Dispose(true);
        }

        // ---- paths ----

        public static string GuidePath()
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "docs");
            string full = Path.GetFullPath(Path.Combine(dir, "setup.md"));
            return full;
        }

        // ---- offline smoke test ----

        public static int RunSmokeTest()
        {
            int failures = 0;
            bool asserted = false;

            PopupForm f = new PopupForm();
            f.ShowInTaskbar = false;

            System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
            t.Interval = 1600;
            t.Tick += delegate
            {
                t.Stop();
                f.AllowClose = true;
                f.Close();
            };

            f.Shown += delegate
            {
                t.Start();
                try
                {
                    f.BeginLayoutSession(Screen.PrimaryScreen);
                    Screen openScreen = f.OpenScreenRef;
                    Rectangle wa = openScreen.WorkingArea;
                    failures += AssertCardChrome(f, "chrome");

                    f.ForceLoading();
                    failures += AssertInside(f, wa, "loading");
                    failures += AssertCardFit(f, "loading");
                    // UX022: a single message card — the window is that card,
                    // rounded like it, with the update-time tooltip attached.
                    if (f.Region == null)
                        failures++;
                    if (f.ContentCardCount != 1 || f.CopyMenuItemForTest.Enabled)
                        failures++;

                    f.ForceRender(SyntheticSample.BuildLarge());
                    failures += AssertInside(f, wa, "large");
                    failures += AssertInnerControls(f, "large");
                    failures += AssertCardFit(f, "large");
                    if (!f.CopyMenuItemForTest.Enabled)
                        failures++;
                    TrySavePreview(f, "preview.png");

                    List<Control> stable = ControlTree(f.ContentForTest);
                    f.ContentForTest.Controls[0].Focus();
                    f.ContentForTest.AutoScrollPosition = new Point(0, 90);
                    Point scroll = f.ContentForTest.AutoScrollPosition;
                    string time = f.UpdateTimeTextForTest;
                    f.BeginAuth(); f.ShowSlowHint(); f.ShowStageHint("正在检查登录状态");
                    failures += AssertSameControls(stable, ControlTree(f.ContentForTest), "waiting");
                    if (!f.ContentForTest.Controls[0].Focused || f.ContentForTest.AutoScrollPosition != scroll
                        || !f.RefreshMenuItemForTest.Enabled) failures++;
                    QuotaSnapshot same = SyntheticSample.BuildLarge();
                    same.FetchedAt = same.FetchedAt.AddMinutes(10);
                    f.ForceRender(same);
                    failures += AssertSameControls(stable, ControlTree(f.ContentForTest), "same-data-new-time");
                    if (f.ContentForTest.AutoScrollPosition != scroll
                        || !f.ContentForTest.Controls[0].Focused) failures++;
                    time = f.UpdateTimeTextForTest;
                    f.ForceError(QuotaStatus.Timeout, "失败");
                    failures += AssertSameControls(stable, ControlTree(f.ContentForTest), "failure");
                    if (f.UpdateTimeTextForTest != time || f.ContentForTest.AutoScrollPosition != scroll
                        || !f.ContentForTest.Controls[0].Focused) failures++;
                    f.ShowPanel();
                    failures += AssertSameControls(stable, ControlTree(f.ContentForTest), "same-size-open");
                    foreach (Control c in ControlTree(f))
                    {
                        foreach (string forbidden in new string[] { "最紧张", "团队套餐：", "服务端数据更新",
                            "正在查询", "刷新中", "确认身份", "仍在进行", "秒后重置", "知道了", "已用 " })
                            if (c.Text.Contains(forbidden)) { failures++; Console.Error.WriteLine("noise: " + c.Text); }
                    }

                    // Error / stale / identity-change states for reviewer reading.
                    f.ForceError(QuotaStatus.Failed, "查询失败，请稍后重试。");
                    failures += AssertInside(f, wa, "error");
                    failures += AssertCardFit(f, "error");
                    TrySavePreview(f, "preview-error.png");

                    f.ForceIdentityChanged();
                    failures += AssertInside(f, wa, "identity");
                    TrySavePreview(f, "preview-identity.png");

                    // UX022: the empty / not-logged-in shape is a message card.
                    f.ForceError(QuotaStatus.NotLoggedIn, "当前未登录方舟账号。");
                    failures += AssertInside(f, wa, "empty");
                    failures += AssertCardFit(f, "empty");
                    if (f.Region == null)
                        failures++;
                    TrySavePreview(f, "preview-empty.png");

                    // UX011: a first-run request must not insert a banner.
                    f.ForceRender(SyntheticSample.Build());
                    f.SetIntro("已在托盘运行。若看不到图标，请点击任务栏的“显示隐藏的图标”（^）查找。");
                    if (f.HasIntroCard)
                    {
                        failures++;
                        Console.Error.WriteLine("intro: removed banner was rendered");
                    }
                    failures += AssertInside(f, wa, "intro");
                    TrySavePreview(f, "preview-intro.png");

                    if (f.AllowClose) failures++;
                    f.Close();
                    if (f.Visible) failures++;

                    f.ShowPanel();
                    f.ForceRender(SyntheticSample.Build());
                    failures += AssertInside(f, f.OpenScreenRef.WorkingArea, "reshow");
                    failures += AssertInnerControls(f, "reshow");
                    failures += AssertCardFit(f, "reshow");

                    // UX022: cross-DPI reopen — fonts / paddings / radius /
                    // height re-derive from the scale; nothing overflows.
                    f.SetScaleForTest(2.0);
                    failures += AssertInside(f, f.OpenScreenRef.WorkingArea, "dpi200");
                    failures += AssertCardFit(f, "dpi200");
                    TrySavePreview(f, "preview-dpi200.png");
                    f.SetScaleForTest(1.0);

                    // Final-state handling must be driven by the outcome alone,
                    // independent of Progress ordering: apply the shared final
                    // handler with NO auth-progress callback first, in both
                    // success and auth-failure shapes.
                    f.BeginAuth();
                    QueryOutcome okOutcome = new QueryOutcome();
                    okOutcome.AuthConfirmed = true;
                    okOutcome.Identity = PopupForm.SampleIdentity();
                    okOutcome.AuthScope = QueryScope.FromAuth(okOutcome.Identity);
                    okOutcome.Verdict = ScopeVerdict.Same;
                    okOutcome.Snapshot = SyntheticSample.Build();
                    TrayApp.ApplyFinalOutcome(f, okOutcome);
                    if (f.CurrentState != PanelState.ShowingCurrent)
                    {
                        failures++;
                        Console.Error.WriteLine("finalhandler.noProgressSuccess: state "
                            + f.CurrentState);
                    }

                    f.BeginAuth();
                    QueryOutcome authFail = new QueryOutcome();
                    authFail.AuthConfirmed = false;
                    authFail.Snapshot = new QuotaSnapshot();
                    authFail.Snapshot.Status = QuotaStatus.NotLoggedIn;
                    authFail.Snapshot.Message = "当前未登录方舟账号。";
                    TrayApp.ApplyFinalOutcome(f, authFail);
                    if (f.CurrentState != PanelState.Error)
                    {
                        failures++;
                        Console.Error.WriteLine("finalhandler.authFail: state " + f.CurrentState);
                    }

                    // Cancelled after auth confirmed this query: no same-scope
                    // cache is re-confirmed by the cancel itself, so data is
                    // cleared (never resurrected from a prior query).
                    f.BeginAuth();
                    QueryOutcome cancelled = new QueryOutcome();
                    cancelled.AuthConfirmed = true;
                    cancelled.Identity = PopupForm.SampleIdentity();
                    cancelled.Snapshot = new QuotaSnapshot();
                    cancelled.Snapshot.Status = QuotaStatus.Cancelled;
                    cancelled.Snapshot.Message = "查询已取消。";
                    TrayApp.ApplyFinalOutcome(f, cancelled);
                    if (f.CurrentState != PanelState.Error)
                    {
                        failures++;
                        Console.Error.WriteLine("finalhandler.authCancel: state " + f.CurrentState);
                    }

                    // UX011: unconfirmed cancellation cannot prove a new identity;
                    // the independent final handler preserves history and time.
                    f.ForceRender(SyntheticSample.Build());
                    if (!f.ModelHasLast)
                    {
                        failures++;
                        Console.Error.WriteLine("finalhandler.precondition: no cached data");
                    }
                    f.BeginAuth();
                    QueryOutcome cancelUnconfirmed = new QueryOutcome();
                    cancelUnconfirmed.AuthConfirmed = false;
                    cancelUnconfirmed.Snapshot = new QuotaSnapshot();
                    cancelUnconfirmed.Snapshot.Status = QuotaStatus.Cancelled;
                    cancelUnconfirmed.Snapshot.Message = "查询已取消。";
                    TrayApp.ApplyFinalOutcome(f, cancelUnconfirmed);
                    if (!f.ModelHasLast)
                    {
                        failures++;
                        Console.Error.WriteLine("finalhandler.unconfirmedCancel: history lost");
                    }
                    if (f.CurrentState != PanelState.CancelledStale)
                    {
                        failures++;
                        Console.Error.WriteLine("finalhandler.unconfirmedCancel: state "
                            + f.CurrentState);
                    }

                    f.ForceRender(SyntheticSample.Build());
                    f.SetIntro(null);

                    // v0.4: production floating circle + settings dialog. Uses
                    // synthetic data only; writes no preference file.
                    failures += SmokeFloating();

                    asserted = true;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("smoke exception: " + ex.Message);
                    failures++;
                }
            };

            Application.Run(f);
            t.Dispose();
            f.Dispose();

            if (!asserted)
            {
                Console.Error.WriteLine("smoke test did not complete assertions");
                return 1;
            }
            if (failures > 0)
            {
                Console.Error.WriteLine("smoke layout failures: " + failures);
                return 1;
            }
            return 0;
        }

        private static int AssertInside(PopupForm f, Rectangle wa, string label)
        {
            int bad = 0;
            Rectangle b = f.CurrentBounds;
            if (b.Width > wa.Width) { bad++; Console.Error.WriteLine(label + ": width overflow"); }
            if (b.Height > wa.Height) { bad++; Console.Error.WriteLine(label + ": height overflow"); }
            if (b.Left < wa.Left) { bad++; Console.Error.WriteLine(label + ": left out"); }
            if (b.Top < wa.Top) { bad++; Console.Error.WriteLine(label + ": top out"); }
            if (b.Right > wa.Right) { bad++; Console.Error.WriteLine(label + ": right out"); }
            if (b.Bottom > wa.Bottom) { bad++; Console.Error.WriteLine(label + ": bottom out"); }
            return bad;
        }

        private static List<Control> ControlTree(Control root)
        {
            List<Control> result = new List<Control>();
            foreach (Control c in root.Controls) { result.Add(c); result.AddRange(ControlTree(c)); }
            return result;
        }

        private static int AssertSameControls(List<Control> before, List<Control> after, string label)
        {
            bool same = before.Count == after.Count;
            for (int i = 0; same && i < before.Count; i++) same = ReferenceEquals(before[i], after[i]);
            if (!same) Console.Error.WriteLine(label + ": controls rebuilt");
            return same ? 0 : 1;
        }

        // v0.14 UX022 card-only chrome: the popup has exactly ONE direct child
        // (the card content), a details-local context menu with EXACTLY the
        // four reused actions, and copy stays disabled without a snapshot.
        private static int AssertCardChrome(PopupForm f, string label)
        {
            int bad = 0;
            if (f.Controls.Count != 1 || f.Controls[0] != f.ContentForTest
                || f.ContentForTest.Padding.All != 0)
            {
                bad++;
                Console.Error.WriteLine(label + ": unexpected chrome (must be content-only)");
            }
            if (f.DetailsMenuForTest == null || f.DetailsMenuForTest.Items.Count != 4
                || f.RefreshMenuItemForTest == null || f.CopyMenuItemForTest == null
                || f.SettingsMenuItemForTest == null || f.CloseMenuItemForTest == null)
            {
                bad++;
                Console.Error.WriteLine(label + ": details menu must have the 4 actions");
            }
            if (f.CopyMenuItemForTest != null && f.CopyMenuItemForTest.Enabled)
            {
                bad++;
                Console.Error.WriteLine(label + ": copy enabled without snapshot");
            }
            return bad;
        }

        // v0.14 UX022: the window HUGS the cards — every card spans the full
        // client width, gaps exist only BETWEEN cards (no tail gap), and
        // without scrolling the last card's bottom is exactly the client
        // bottom.
        private static int AssertCardFit(PopupForm f, string label)
        {
            int bad = 0;
            FlowLayoutPanel content = f.ContentForTest;
            bool scrolling = content.AutoScroll && content.VerticalScroll.Visible;
            for (int i = 0; i < content.Controls.Count; i++)
            {
                Control card = content.Controls[i];
                if (card.Left != 0 || card.Width != content.ClientSize.Width)
                {
                    bad++;
                    Console.Error.WriteLine(label + ": card " + i + " does not span the width ("
                        + card.Bounds + " client " + content.ClientSize + ")");
                }
                if (i < content.Controls.Count - 1 && card.Margin.Bottom <= 0)
                {
                    bad++;
                    Console.Error.WriteLine(label + ": card " + i + " missing inter-card gap");
                }
                if (i == content.Controls.Count - 1 && card.Margin.Bottom != 0)
                {
                    bad++;
                    Console.Error.WriteLine(label + ": card " + i + " has a tail gap");
                }
                if (!scrolling && i == content.Controls.Count - 1
                    && card.Bottom + card.Margin.Bottom != content.ClientSize.Height)
                {
                    bad++;
                    Console.Error.WriteLine(label + ": card " + i + " bottom does not hug the window ("
                        + card.Bottom + " + " + card.Margin.Bottom + " vs " + content.ClientSize.Height + ")");
                }
            }
            if (content.HorizontalScroll.Visible)
            {
                bad++;
                Console.Error.WriteLine(label + ": horizontal overflow");
            }
            return bad;
        }

        private static int AssertInnerControls(PopupForm f, string label)
        {
            int bad = 0;
            foreach (Control card in f.ContentControls)
            {
                foreach (Control c in card.Controls)
                {
                    if (c.Left < 0 || c.Top < 0
                        || c.Right > card.ClientSize.Width + 1
                        || c.Bottom > card.ClientSize.Height + 1)
                    {
                        bad++;
                        Console.Error.WriteLine(label + ": child out of card bounds ("
                            + c.GetType().Name + " " + c.Bounds + " in " + card.ClientSize + ")");
                    }
                }
            }
            return bad;
        }

        private static void TrySavePreview(PopupForm f, string file)
        {
            try
            {
                string dir = AppDomain.CurrentDomain.BaseDirectory;
                string path = Path.Combine(dir, file);
                using (Bitmap bmp = new Bitmap(f.Width, f.Height))
                {
                    f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
                    bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
            catch (Exception)
            {
                // preview is a reviewer aid only
            }
        }

        // Offline smoke for the floating circle + settings dialog. Synthetic data
        // only: injected null/no-op preference callbacks mean no real state file
        // is read or written, and no query / network is ever triggered.
        private static int SmokeFloating()
        {
            int bad = 0;
            try
            {
                using (FloatingQuotaForm floating = new FloatingQuotaForm(
                    delegate { return (FloatingSettings)null; },
                    delegate(FloatingSettings s) { return true; },
                    delegate { return (FloatingPreferences)null; },
                    delegate(FloatingPreferences p) { return true; }))
                {
                    IntPtr h = floating.Handle; GC.KeepAlive(h);

                    PanelModel model = new PanelModel();
                    model.OnAuthResult(true, PopupForm.SampleIdentity(), QuotaStatus.Ok, null);
                    PanelView view = model.OnUsageResult(
                        SyntheticSample.BuildLarge(), null, ScopeVerdict.Same, null);
                    floating.ApplyModelView(view);

                    Rectangle wa = Screen.PrimaryScreen.WorkingArea;
                    floating.ShowCircleAtForTest(wa, 1.0);
                    Application.DoEvents();
                    if (!floating.CircleVisible) { bad++; Console.Error.WriteLine("floating: not visible"); }
                    FloatingCircleControl circle = floating.CircleForTest;
                    if (circle.PercentTextForTest != "75%")
                    {
                        bad++;
                        Console.Error.WriteLine("floating.percent: got " + circle.PercentTextForTest
                            + " want 75%");
                    }
                    if (!circle.PercentKnownForTest)
                    {
                        bad++;
                        Console.Error.WriteLine("floating.percentKnown mismatch");
                    }
                    if (!circle.WaveRunningForTest)
                    {
                        bad++;
                        Console.Error.WriteLine("floating.wave: expected running");
                    }
                    if (!circle.CaptionForTest.Contains("5 小时"))
                    {
                        bad++;
                        Console.Error.WriteLine("floating.caption: " + circle.CaptionForTest);
                    }
                    if (circle.ProductCaptionForTest.Length == 0
                        || circle.PeriodCaptionForTest != "5 小时")
                    {
                        bad++;
                        Console.Error.WriteLine("floating.captionLines: product="
                            + circle.ProductCaptionForTest + " period=" + circle.PeriodCaptionForTest);
                    }
                    if (!CircleInside(floating.CircleForTest.Bounds, wa))
                    {
                        bad++;
                        Console.Error.WriteLine("floating: bounds outside work area");
                    }
                    TrySaveControlPreview(circle, "preview-floating.png");

                    // v0.9 UX017: reduce motion stops the wave timer and
                    // repaints a static surface through the production
                    // handler; display values stay; toggling back resumes.
                    // Injected no-op preference save, zero query.
                    floating.SetReduceMotion(true);
                    Application.DoEvents();
                    if (circle.WaveRunningForTest)
                    {
                        bad++;
                        Console.Error.WriteLine("floating.reduceMotion: wave still running");
                    }
                    if (circle.PercentTextForTest != "75%")
                    {
                        bad++;
                        Console.Error.WriteLine("floating.reduceMotion: percent changed");
                    }
                    using (Bitmap staticWave = SnapshotControl(circle))
                    {
                        TrySaveBitmap(staticWave, "preview-floating-static.png",
                            circle.Region);
                    }
                    floating.SetReduceMotion(false);
                    Application.DoEvents();
                    if (!circle.WaveRunningForTest)
                    {
                        bad++;
                        Console.Error.WriteLine("floating.reduceMotion: wave not resumed");
                    }

                    // 0 / 100 / unknown render endpoints. Verify non-text water /
                    // no-water pixels rather than trusting the text alone.
                    bad += SmokeCircleEndpoints(floating);
                    bad += SmokeCircleScale();

                    // Settings dialog (production Drawing) with an injected
                    // no-op save; never touches the real preference file.
                    List<FloatingEntry> candidates = FloatingSelection.SelectableCandidates(
                        FloatingSelection.Build(SyntheticSample.BuildLarge()));
                    using (FloatingSettingsForm dlg = new FloatingSettingsForm(
                        candidates, null, delegate(FloatingSettings s) { return true; }))
                    {
                        if (dlg.Controls.Count == 0) { bad++; Console.Error.WriteLine("settings: no controls"); }
                        dlg.Show();
                        Application.DoEvents();
                        TrySaveControlPreview(dlg, "preview-settings.png");
                        dlg.Close();
                    }

                    // Lifecycle: hiding stops the wave and disposes cleanly.
                    floating.HideCircleForTest();
                    Application.DoEvents();
                    if (floating.CircleVisible) { bad++; Console.Error.WriteLine("floating: hide failed"); }
                    if (circle.WaveRunningForTest) { bad++; Console.Error.WriteLine("floating: wave not stopped on hide"); }
                }
            }
            catch (Exception ex)
            {
                bad++;
                Console.Error.WriteLine("floating smoke exception: " + ex.Message);
            }
            return bad;
        }

        // 0 empty / 100 full / unknown neutral: verify pixel regions that cannot
        // be satisfied by the text alone.
        private static int SmokeCircleEndpoints(FloatingQuotaForm floating)
        {
            int bad = 0;
            try
            {
                Rectangle wa = Screen.PrimaryScreen.WorkingArea;
                FloatingCircleControl circle = floating.CircleForTest;
                circle.ShowAt(wa, 1.0);

                circle.SetDisplay(EndpointDisplay(100, true, "100%"));
                Application.DoEvents();
                using (Bitmap full = SnapshotControl(circle))
                {
                    // Near the top, well left of the centered text, the water must
                    // reach the fill: no dark-background "notch" remains.
                    if (!IsWater(full.GetPixel(full.Width / 4, (int)(full.Height * 0.14)))) bad++;
                    if (!IsWater(full.GetPixel(full.Width / 4, (int)(full.Height * 0.85)))) bad++;
                    TrySaveBitmap(full, "preview-floating-100.png", circle.Region);
                }

                circle.SetDisplay(EndpointDisplay(0, true, "0%"));
                Application.DoEvents();
                using (Bitmap empty = SnapshotControl(circle))
                {
                    // No water anywhere away from the text.
                    if (IsWater(empty.GetPixel(empty.Width / 4, (int)(empty.Height * 0.30)))) bad++;
                    if (IsWater(empty.GetPixel(empty.Width / 4, (int)(empty.Height * 0.80)))) bad++;
                    TrySaveBitmap(empty, "preview-floating-0.png", circle.Region);
                }

                circle.SetDisplay(EndpointDisplay(0, false, "剩余未知"));
                Application.DoEvents();
                using (Bitmap unknown = SnapshotControl(circle))
                {
                    if (IsWater(unknown.GetPixel(unknown.Width / 4, (int)(unknown.Height * 0.50)))) bad++;
                    TrySaveBitmap(unknown, "preview-floating-unknown.png", circle.Region);
                }
                if (bad > 0) Console.Error.WriteLine("floating endpoints: " + bad);
            }
            catch (Exception ex)
            {
                bad++;
                Console.Error.WriteLine("floating endpoints exception: " + ex.Message);
            }
            return bad;
        }

        // Scale == 2 must enlarge BOTH the bounds and the fonts (rendered text
        // must grow), and 1% must paint only a sliver of water.
        private static int SmokeCircleScale()
        {
            int bad = 0;
            try
            {
                Rectangle wa = Screen.PrimaryScreen.WorkingArea;
                using (FloatingQuotaForm a = new FloatingQuotaForm(
                    delegate { return (FloatingSettings)null; },
                    delegate(FloatingSettings s) { return true; }))
                {
                    IntPtr h1 = a.Handle; GC.KeepAlive(h1);
                    a.ShowCircleAtForTest(new Rectangle(0, 0, 2000, 2000), 1.0);
                    a.CircleForTest.SetDisplay(EndpointDisplay(1, true, "1%"));
                    Application.DoEvents();
                    Rectangle b1 = a.CircleForTest.Bounds;
                    using (Bitmap at1 = SnapshotControl(a.CircleForTest))
                    {
                        // 0% reference: SAME control, size and focus state, so
                        // the ring stroke is identical in both snapshots and any
                        // bottom-band delta is water, never ring color coupling.
                        a.CircleForTest.SetDisplay(EndpointDisplay(0, true, "0%"));
                        Application.DoEvents();
                        using (Bitmap at0 = SnapshotControl(a.CircleForTest))
                        {
                            // Sample near the vertical center line so the pixel
                            // is inside the ellipse; x = width/4 is outside it
                            // near the bottom (navy background instead).
                            int cx1 = at1.Width / 2;
                            int hh = at1.Height;
                            Color mid1 = at1.GetPixel(cx1, (int)(hh * 0.55));
                            Color bot1 = at1.GetPixel(cx1, hh - 2);
                            Color bot0 = at0.GetPixel(cx1, hh - 2);
                            if (IsWater(mid1))
                            {
                                bad++;
                                Console.Error.WriteLine("floating scale 1pct middle is water: " + mid1);
                            }
                            // 1% water is a ~1px sliver that only exists under
                            // the ring stroke, so the old "bottom G > top G"
                            // check compared ring edge pixels and broke once the
                            // focused/hover ring (bright teal) covered both
                            // sample points. Compare the SAME bottom pixel
                            // against the 0% reference instead: the ring is
                            // identical in both snapshots, so a clear G-channel
                            // rise is real water (measured ~65, threshold 20).
                            if (!(bot1.G > bot0.G + 20))
                            {
                                bad++;
                                Console.Error.WriteLine("floating scale 1pct no water vs 0pct reference: 1pct="
                                    + bot1 + " 0pct=" + bot0);
                            }
                        }
                    }

                    using (FloatingQuotaForm b = new FloatingQuotaForm(
                        delegate { return (FloatingSettings)null; },
                        delegate(FloatingSettings s) { return true; }))
                    {
                        IntPtr h2 = b.Handle; GC.KeepAlive(h2);
                        b.ShowCircleAtForTest(new Rectangle(0, 0, 2000, 2000), 2.0);
                        b.CircleForTest.SetDisplay(EndpointDisplay(75, true, "75%"));
                        Application.DoEvents();
                        Rectangle b2 = b.CircleForTest.Bounds;
                        if (b2.Width != b1.Width * 2)
                        {
                            bad++;
                            Console.Error.WriteLine("floating scale bounds: " + b1.Width + " -> " + b2.Width);
                        }
                        // A scale-2 filled circle must have water across the middle.
                        using (Bitmap at2 = SnapshotControl(b.CircleForTest))
                        {
                            if (!IsWater(at2.GetPixel(at2.Width / 4, (int)(at2.Height * 0.45)))) bad++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                bad++;
                Console.Error.WriteLine("floating scale exception: " + ex.Message);
            }
            return bad;
        }

        private static FloatingDisplay EndpointDisplay(double percent, bool known, string text)
        {
            FloatingDisplay d = new FloatingDisplay();
            d.HasData = true;
            d.PercentKnown = known;
            d.Percent = percent;
            d.PercentText = text;
            d.ProductCaption = "Agent Plan";
            d.PeriodCaption = "5 小时";
            return d;
        }

        private static Bitmap SnapshotControl(Control c)
        {
            Bitmap bmp = new Bitmap(Math.Max(1, c.Width), Math.Max(1, c.Height));
            try { c.DrawToBitmap(bmp, new Rectangle(0, 0, c.Width, c.Height)); } catch (Exception) { }
            return bmp;
        }

        // A water pixel is clearly blue-cyan: much higher blue AND green than
        // red. This must NOT match the dark navy background (16,42,74), whose
        // green is only slightly above red.
        private static bool IsWater(Color p)
        {
            return p.B > p.R + 40 && p.G > p.R + 40 && p.G > 80;
        }

        private static void TrySaveBitmap(Bitmap bmp, string file)
        {
            TrySaveBitmap(bmp, file, null);
        }

        // Saves a snapshot. When a Region is supplied the saved PNG is clipped
        // to it (transparent corners for the circular window); pixel assertions
        // keep using the raw in-memory bitmap.
        private static void TrySaveBitmap(Bitmap bmp, string file, Region clip)
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, file);
                if (clip == null)
                {
                    bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                    return;
                }
                using (Bitmap outBmp = new Bitmap(bmp.Width, bmp.Height))
                using (Graphics g = Graphics.FromImage(outBmp))
                {
                    g.Clear(Color.Transparent);
                    using (Region r = clip.Clone())
                    {
                        r.Intersect(g.VisibleClipBounds);
                        g.SetClip(r, System.Drawing.Drawing2D.CombineMode.Replace);
                        g.DrawImageUnscaled(bmp, 0, 0);
                    }
                    outBmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
            catch (Exception) { }
        }

        private static bool CircleInside(Rectangle b, Rectangle wa)
        {
            return b.Left >= wa.Left && b.Top >= wa.Top
                && b.Right <= wa.Right && b.Bottom <= wa.Bottom;
        }

        private static void TrySaveControlPreview(Control c, string file)
        {
            try
            {
                if (c.Width < 1 || c.Height < 1) return;
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, file);
                using (Bitmap bmp = new Bitmap(c.Width, c.Height))
                {
                    // DrawToBitmap ignores a window Region, so clip to the
                    // elliptical region here to produce a transparent-corner
                    // circular PNG; non-circular controls (dialog) render as-is.
                    if (c.Region != null)
                    {
                        using (Graphics g = Graphics.FromImage(bmp))
                        using (Region clip = c.Region.Clone())
                        {
                            g.Clear(Color.Transparent);
                            clip.Intersect(g.VisibleClipBounds);
                            g.SetClip(clip, System.Drawing.Drawing2D.CombineMode.Replace);
                            using (Bitmap raw = new Bitmap(c.Width, c.Height))
                            {
                                c.DrawToBitmap(raw, new Rectangle(0, 0, c.Width, c.Height));
                                g.DrawImageUnscaled(raw, 0, 0);
                            }
                        }
                    }
                    else
                    {
                        c.DrawToBitmap(bmp, new Rectangle(0, 0, c.Width, c.Height));
                    }
                    bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
            catch (Exception) { }
        }
    }

    // First-run marker + test-isolatable state directory. The marker contains
    // only a date/version line: never identity, quota or credentials.
    internal static class Marker
    {
        public const string StateDirEnv = "ARK_LEFT_STATE_DIR";

        public static string StateDir()
        {
            string custom = Environment.GetEnvironmentVariable(StateDirEnv);
            if (!string.IsNullOrEmpty(custom)) return custom;
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(local)) local = Path.GetTempPath();
            return Path.Combine(local, "ark_left");
        }

        public static string MarkerPath()
        {
            return Path.Combine(StateDir(), "first-run.done");
        }

        public static bool Exists()
        {
            try { return File.Exists(MarkerPath()); }
            catch (Exception) { return false; }
        }

        public static bool ShouldShowIntro()
        {
            return !Exists();
        }

        public static void WriteFirstRun()
        {
            try
            {
                Directory.CreateDirectory(StateDir());
                string line = "version=" + System.Reflection.Assembly.GetExecutingAssembly()
                    .GetName().Version + " date=" + DateTime.Now.ToString("yyyy-MM-dd");
                File.WriteAllText(MarkerPath(), line);
            }
            catch (Exception) { }
        }
    }
}
