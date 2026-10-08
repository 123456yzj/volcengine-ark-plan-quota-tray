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
    // Floating circle window. Consumes the SAME PanelView as the details panel
    // (no extra controller) and owns selection / settings. Events let TrayApp
    // wire the shared context menu; opening the settings dialog is guarded so a
    // second request cannot stack a duplicate and ExitApp disposes it.
    internal partial class FloatingQuotaForm : Form
    {
        // Kept in the circle's accessibility description, without hover popups.
        internal const string CircleHint = "点击查看全部额度，右键设置";

        private readonly FloatingCircleControl _circle;
        private readonly Func<FloatingSettings> _load;
        private readonly Func<FloatingSettings, bool> _save;
        // v0.8 UX016: injectable preference load / save (offline tests use
        // null-returning / no-op callbacks; production uses the store).
        private readonly Func<FloatingPreferences> _prefsLoad;
        private readonly Func<FloatingPreferences, bool> _prefsSave;
        private ContextMenuStrip _menu;
        private ToolStripMenuItem _lockItem;
        // v0.9 UX017: checkable "减少动画" item, right after the lock item.
        private ToolStripMenuItem _motionItem;
        // v0.15 UX023: default menu (used when no owner shares one) mirrors the
        // new top level [设置(子菜单), 退出]: the 设置 submenu holds the
        // 悬浮内容 chooser plus the existing toggles.
        private ToolStripMenuItem _settingsItem;
        private ToolStripMenuItem _contentItem;
        private ToolStripMenuItem _homeItem;
        private ToolStripMenuItem _toggleItem;
        // v0.24 UX029: first-level 主题 item on the default circle menu.
        private ToolStripMenuItem _themeItem;
        // v0.15 UX023: observable "设置 clicked -> native side dropdown" intent
        // (no real popup is created offline, where the strip is never shown).
        private bool _settingsDropDownRequested;
        // v0.15 UX023: bumped whenever ApplyModelView replaces the candidate
        // list, so a menu opened over an older snapshot can never save an item
        // that is no longer backed by the current entries (same key included).
        private int _contentGeneration;
        // Deterministic release bookkeeping for the rebuilt 悬浮内容 items.
        private int _contentItemsReleased;
        // Removed 悬浮内容 items awaiting a safe release. A single shared,
        // coalesced drain is posted (never one dangling closure per rebuild);
        // Dispose drains synchronously so nothing is left undisposed.
        private readonly List<ToolStripItem> _pendingContentItems = new List<ToolStripItem>();
        private bool _contentDrainPosted;
        // Candidate / identity signature of the last built menu; an equivalent
        // refresh (e.g. the 10s poll) must NOT invalidate an open selection.
        private string _contentSignature;
        private string _contentIdentitySignature;
        private FloatingSettings _stored;
        private FloatingSettings _selected;
        private List<FloatingEntry> _entries = new List<FloatingEntry>();
        private PanelView _view;
        private ThemeDialogForm _themeDialog;
        private bool _positioned;
        private bool _autoHiddenForDetails;
        private bool _positionLocked;
        // v0.9 UX017: reduce-motion flag restored from / saved with the prefs.
        private bool _reduceMotion;
        // v0.24 UX029: accent preset + dark mode, restored from / saved with
        // the same preference file. The resolved palette lives in UiStyle.
        private int _accentIndex;
        private bool _darkMode;
        private string _lockHint;

        public event EventHandler DetailsRequested;
        public event EventHandler DragStarted;
        public event EventHandler ThemeRequested;
        public event EventHandler ExitRequested;
        // v0.8 UX016: raised after any lock toggle attempt (success or save
        // failure) so the tray menu re-syncs its check against the REAL
        // current state.
        public event EventHandler PositionLockChanged;
        // v0.8 UX016 fix: raised ONLY when a lock save fails. The owner uses
        // it to surface a short tray notification. The hint text itself
        // stays short and readable - never a raw exception.
        public event EventHandler LockSaveFailed;
        // v0.9 UX017: raised after any reduce-motion toggle attempt (success
        // or save failure) so the tray menu re-syncs its check against the
        // REAL current state.
        public event EventHandler ReduceMotionChanged;
        // v0.9 UX017: raised ONLY when a reduce-motion save fails. The owner
        // surfaces a DISTINCT short tray notification (never the lock-failure
        // wording), regardless of circle visibility.
        public event EventHandler MotionSaveFailed;
        // v0.15 UX023: raised ONLY when a 悬浮内容 menu save fails. The owner
        // surfaces a DISTINCT short tray notification (never the lock / motion
        // wording), regardless of circle visibility.
        public event EventHandler ContentSaveFailed;
        // v0.15 UX023: raised after any 悬浮内容 menu selection attempt so a
        // shared (tray) submenu can rebuild its checks from the real state.
        public event EventHandler ContentChanged;
        // v0.5 UX013: forwards the real circle's VisibleChanged. The wrapper's
        // own (1x1) window visibility does not track the circle, so callers must
        // subscribe here to observe show / hide transitions.
        public event EventHandler CircleVisibleChanged;
        // v0.24 UX029: raised after a theme change is PERSISTED (never on a
        // live preview) so the owner can repaint the details panel and rebuild
        // the tray icon from the new palette.
        public event EventHandler ThemeChanged;

        public FloatingQuotaForm() : this(null, null, null, null) { }

        // load == null / save == null -> production store. Offline tests inject
        // null-returning / no-op callbacks so no real preference file is touched.
        public FloatingQuotaForm(Func<FloatingSettings> load,
            Func<FloatingSettings, bool> save) : this(load, save, null, null) { }

        // prefsLoad == null / prefsSave == null -> production preference store
        // (FloatingPreferencesStore). A corrupt / missing file means unlocked;
        // construction never writes.
        public FloatingQuotaForm(Func<FloatingSettings> load,
            Func<FloatingSettings, bool> save,
            Func<FloatingPreferences> prefsLoad,
            Func<FloatingPreferences, bool> prefsSave)
        {
            _load = load ?? FloatingSettingsStore.Load;
            _save = save ?? FloatingSettingsStore.Save;
            _prefsLoad = prefsLoad ?? FloatingPreferencesStore.Load;
            _prefsSave = prefsSave ?? FloatingPreferencesStore.Save;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(1, 1);
            Text = "方舟剩余额度";
            _circle = new FloatingCircleControl();
            _circle.VisibleChanged += delegate
            {
                UpdateToggleItemText();
                if (CircleVisibleChanged != null) CircleVisibleChanged(this, EventArgs.Empty);
            };
            _circle.DetailsRequested += delegate
            {
                if (DetailsRequested != null) DetailsRequested(this, EventArgs.Empty);
            };
            _circle.DragStarted += delegate
            {
                if (_menu != null) _menu.Close();
                if (DragStarted != null) DragStarted(this, EventArgs.Empty);
            };
            _menu = BuildDefaultMenu();
            _circle.ContextMenuStrip = _menu;
            _circle.AllowClose = false;
            _circle.ExplicitHideRequested += delegate { _autoHiddenForDetails = false; };

            _stored = _load();
            _selected = _stored;
            // v0.8 UX016: restore the position lock (default unlocked; corrupt
            // files fall back to unlocked). Read-only: no write on start.
            FloatingPreferences prefs = _prefsLoad();
            _positionLocked = prefs != null && prefs.PositionLocked;
            // v0.9 UX017: restore reduce motion the same way (default off; a
            // legacy format 1 file arrives migrated to the format 2 shape by
            // the store). Read-only: no write on start.
            _reduceMotion = prefs != null && prefs.ReduceMotion;
            // v0.24 UX029: restore the theme (default accent 0 / light; a
            // legacy format 1 / 2 file arrives migrated to the format 3 shape
            // by the store). Existing files never carried an accent index, so
            // normalization also guards a corrupt value.
            _accentIndex = ThemeCatalog.Normalize(prefs == null ? 0 : prefs.AccentIndex);
            _darkMode = prefs != null && prefs.DarkMode;
            UiStyle.Apply(_accentIndex, _darkMode);
            _circle.PositionLocked = _positionLocked;
            _circle.SetReduceMotion(_reduceMotion);
            SyncPrefMenuChecks();
        }

        private Rectangle _pendingDetailsBounds;

        // ---- settings ----

        // v0.24 UX029: the first-level 主题 menu item opens the standalone theme
        // dialog. The caller supplies the window that OWNS the modal — the
        // details panel when relevant, the floating window from the circle /
        // tray. Repeat requests only activate the existing single instance; a
        // disposing form never resurrects it. Opening / saving / cancelling
        // never queries.
        public void OpenThemeDialog()
        {
            OpenThemeDialog(null);
        }

        public void OpenThemeDialog(IWin32Window owner)
        {
            if (IsDisposed || Disposing) return;
            if (_themeDialog != null && !_themeDialog.IsDisposed)
            {
                try { _themeDialog.Activate(); } catch (Exception) { }
                return;
            }
            Screen scr = null;
            try
            {
                Control oc = owner as Control;
                if (oc != null && !oc.IsDisposed) scr = Screen.FromControl(oc);
                else if (owner != null && owner.Handle != IntPtr.Zero)
                    scr = Screen.FromHandle(owner.Handle);
            }
            catch (Exception) { }
            if (scr == null) scr = Screen.PrimaryScreen ?? Screen.AllScreens[0];
            ThemeDialogForm dlg = new ThemeDialogForm(DpiUtil.GetScale(scr), scr.WorkingArea,
                ApplyThemePreview, SaveTheme, _accentIndex, _darkMode);
            _themeDialog = dlg;
            try
            {
                dlg.ShowDialog(owner ?? (IWin32Window)this);
            }
            finally
            {
                _themeDialog = null;
                try { dlg.Dispose(); } catch (Exception) { }
            }
        }

        public void CloseThemeDialog()
        {
            if (_themeDialog != null && !_themeDialog.IsDisposed)
            {
                try { _themeDialog.Close(); } catch (Exception) { }
            }
            _themeDialog = null;
        }

        // ---- v0.24 UX029 theme ----

        public int AccentIndex { get { return _accentIndex; } }
        public bool DarkMode { get { return _darkMode; } }

        // Live preview from the settings dialog: repaints the current surfaces
        // with the chosen palette WITHOUT persisting and WITHOUT re-deriving
        // the circle display. The dialog re-saves or reverts on close.
        private void ApplyThemePreview(int accentIndex, bool darkMode)
        {
            if (!UiStyle.Apply(accentIndex, darkMode)) return;
            RefreshThemeSurfaces();
            if (ThemeChanged != null) ThemeChanged(this, EventArgs.Empty);
        }

        // Persists the theme through the same preference file as the lock /
        // motion flags (format 3), then commits it in memory and announces the
        // change so the owner can repaint the details panel / tray icon. A
        // failed save keeps the OLD committed values and reports false.
        private bool SaveTheme(int accentIndex, bool darkMode)
        {
            accentIndex = ThemeCatalog.Normalize(accentIndex);
            FloatingPreferences p = new FloatingPreferences();
            p.Version = FloatingPreferencesStore.FormatVersion;
            p.PositionLocked = _positionLocked;
            p.ReduceMotion = _reduceMotion;
            p.AccentIndex = accentIndex;
            p.DarkMode = darkMode;
            bool saved;
            try { saved = _prefsSave(p); }
            catch (Exception) { saved = false; }
            if (!saved) return false;
            _accentIndex = accentIndex;
            _darkMode = darkMode;
            UiStyle.Apply(_accentIndex, _darkMode);
            RefreshThemeSurfaces();
            if (ThemeChanged != null) ThemeChanged(this, EventArgs.Empty);
            return true;
        }

        // Repaints the controls this form owns. The details panel / tray icon
        // are refreshed by the owner through ThemeChanged.
        private void RefreshThemeSurfaces()
        {
            try { UiStyle.StyleMenu(_menu); } catch (Exception) { }
            PopulateContentMenu(_contentItem);
            Invalidate(true);
            if (_circle != null && !_circle.IsDisposed) _circle.Invalidate();
        }

        // ---- test hooks ----

        internal void ShowCircleAtForTest(Rectangle workArea, double scale)
        {
            _circle.ShowAt(workArea, scale);
        }

        internal void HideCircleForTest() { HideCircle(); }
        internal int EntryCountForTest { get { return _entries.Count; } }
        internal FloatingEntry SelectedEntryForTest
        {
            get { return FloatingSelection.Find(_entries, _selected); }
        }
        internal FloatingSettings StoredSettingsForTest { get { return _stored; } }
        internal FloatingCircleControl CircleForTest { get { return _circle; } }
        public Rectangle CircleBounds { get { return _circle.Bounds; } }
        // UX015/T042: the REAL circle surface. The shell opens the settings
        // modal owned by the window that actually carries the circle, so the
        // dialog's screen, DPI and placement follow the circle — the wrapper
        // form itself is a hidden 1x1 window whose location follows nothing.
        public System.Windows.Forms.Control CircleSurface
        {
            get { return _circle; }
        }
        // v0.24 UX029: true while the single theme dialog is up. TrayApp decides
        // from this when its details-suppression window may really end: a
        // repeat request that merely activated the dialog must not end it.
        public bool ThemeDialogOpen
        {
            get { return _themeDialog != null && !_themeDialog.IsDisposed; }
        }
        internal bool ThemeDialogOpenForTest
        {
            get { return ThemeDialogOpen; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CloseThemeDialog();
                // Release every queued 悬浮内容 item before the menus go, so no
                // removed item survives Dispose (a late posted drain then sees an
                // empty queue and exits safely).
                DrainPendingContentItems();
                if (_menu != null) { try { _menu.Dispose(); } catch (Exception) { } _menu = null; }
                if (_lockItem != null) _lockItem.Dispose();
                if (_motionItem != null) _motionItem.Dispose();
                if (_homeItem != null) _homeItem.Dispose();
                if (_themeItem != null) _themeItem.Dispose();
                if (_circle != null) { try { _circle.Dispose(); } catch (Exception) { } }
            }
            base.Dispose(disposing);
        }
    }
}
