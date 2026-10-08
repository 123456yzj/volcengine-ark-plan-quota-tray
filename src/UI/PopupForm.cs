using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ArkLeft
{
    internal partial class PopupForm : Form
    {
        private double _scale = 1.0;
        private FlowLayoutPanel _content;
        // UX022 v0.14: card-only popup. The details panel has no local context
        // menu (the card right-click menu was removed in v0.24 UX029) and no
        // tooltips; the shared circle / tray menu owns settings / theme.
        // UX022: view-level status shown compactly INSIDE the first card
        // (stale / cache / identity-unknown); null when everything is fresh.
        private string _statusNote;
        private bool _statusStrong;
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
        private bool _reduceMotion;

        // Cached fonts are created once (per style) and reused across layouts to
        // avoid leaking GDI handles on repeated refreshes.
        private readonly Dictionary<string, Font> _fontCache = new Dictionary<string, Font>();
        private Panel _introCard;   // the actually-rendered first-run banner (or null)

        public event EventHandler RefreshRequested;
        public event EventHandler LoginRequested;

        public bool AllowClose
        {
            get { return _allowClose; }
            set { _allowClose = value; }
        }

        private static Color CardBorder { get { return UiStyle.Border; } }
        private static Color ContentBg { get { return UiStyle.Canvas; } }
        private static Color TextDark { get { return UiStyle.Navy; } }
        private static Color TextMuted { get { return UiStyle.Muted; } }
        internal static Color WarningColor { get { return UiStyle.Warning; } }
        private static Color ErrorColor { get { return UiStyle.Error; } }

        public PopupForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = ContentBg;            KeyPreview = true;
            AutoScaleMode = AutoScaleMode.None;
            // Reserve one physical pixel for the restrained outer frame.
            Padding = new Padding(1);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            Text = "ark_left 方舟订阅额度";

            _scale = DpiUtil.GetScale(Screen.PrimaryScreen);

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

            _clock = new System.Windows.Forms.Timer();
            _clock.Interval = 15000; // local time labels only; never queries
            _clock.Tick += delegate { TickTimeLabels(); };

            _hideTimer.Interval = HideController.HideDelayMs;
            _hideTimer.Tick += OnHideTimerTick;
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

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (ClientSize.Width < 2 || ClientSize.Height < 2) return;
            using (Pen pen = new Pen(CardBorder))
                e.Graphics.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        }

        // UX022: a refresh that changes the displayed values replaces the card
        // controls (ReplaceContent) and re-fits the window (ClientSize). The
        // form double-buffers only its OWN painting; the card panels are
        // separate child windows, so their erase / repaint during a rebuild or
        // resize flashes. WS_EX_COMPOSITED composites this window AND its child
        // windows off-screen in one pass, removing the visible flicker without
        // changing any layout or display semantics.
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
                return cp;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_clock != null) { try { _clock.Stop(); _clock.Dispose(); } catch (Exception) { } }
                if (_hideTimer != null) { try { _hideTimer.Stop(); _hideTimer.Dispose(); } catch (Exception) { } }
                if (Region != null) { try { Region.Dispose(); Region = null; } catch (Exception) { } }
                foreach (Font f in _fontCache.Values)
                {
                    try { f.Dispose(); } catch (Exception) { }
                }
                _fontCache.Clear();
            }
            base.Dispose(disposing);
        }
    }
}
