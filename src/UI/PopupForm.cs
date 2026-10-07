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
        private bool _reduceMotion;

        // Cached fonts are created once (per style) and reused across layouts to
        // avoid leaking GDI handles on repeated refreshes.
        private readonly Dictionary<string, Font> _fontCache = new Dictionary<string, Font>();
        private Panel _introCard;   // the actually-rendered first-run banner (or null)

        public event EventHandler RefreshRequested;
        public event EventHandler CancelRequested;
        public event EventHandler SettingsRequested;
        public event EventHandler LoginRequested;

        public bool AllowClose
        {
            get { return _allowClose; }
            set { _allowClose = value; }
        }

        private static readonly Color CardBorder = UiStyle.Border;
        private static readonly Color ContentBg = UiStyle.Canvas;
        private static readonly Color TextDark = UiStyle.Navy;
        private static readonly Color TextMuted = UiStyle.Muted;
        internal static readonly Color WarningColor = UiStyle.Warning;
        private static readonly Color ErrorColor = UiStyle.Error;

        public PopupForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = ContentBg;
            KeyPreview = true;
            AutoScaleMode = AutoScaleMode.None;
            // Reserve one physical pixel for the restrained outer frame.
            Padding = new Padding(1);
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
    }
}
