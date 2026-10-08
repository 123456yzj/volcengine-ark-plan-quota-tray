using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArkLeft
{
    // Runtime: tray icon, context menu, single-instance IPC, silent-start
    // ApplicationContext, refresh orchestrator and first-run marker.
    internal sealed partial class TrayApp : ApplicationContext
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
        // Shared 设置 submenu: content selection and account actions.
        private ToolStripMenuItem _menuSettings;
        private ToolStripMenuItem _menuContent;
        private bool _menuSettingsDropDownRequested;
        // v0.8 UX016 fix: offline-observable intent counters for the failed
        // lock-save tray notification (no real NotifyIcon exists offline).
        private int _lockFailNotifyCount;
        private string _lastLockFailText;
        // v0.9 UX017: distinct intent counter for the failed reduce-motion
        // save notification; the wording must never claim a lock failure.
        private int _motionFailNotifyCount;
        private string _lastMotionFailText;
        // v0.15 UX023: distinct intent counter / wording for the failed
        // 悬浮内容 menu save notification (never the lock / motion wording).
        private int _contentFailNotifyCount;
        private string _lastContentFailText;
        private PopupForm _form;
        private FloatingQuotaForm _floating;
        private QuotaCli _cli;
        private DirectAgentPlan _direct;
        private readonly Func<CancellationToken, Task<CliResult>> _loginAction;
        private readonly Func<CancellationToken, Task<CliResult>> _logoutAction;
        private ToolStripMenuItem _menuLogin;
        private ToolStripMenuItem _menuLogout;
        private CancellationTokenSource _login;
        private Task _loginTask;
        private bool _loggingOut;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly System.Windows.Forms.Timer _runtimeTimer = new System.Windows.Forms.Timer();
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
            Func<FloatingPreferences, bool> prefsSaveOverride,
            Func<CancellationToken, Task<CliResult>> loginOverride = null,
            Func<CancellationToken, Task<CliResult>> logoutOverride = null)
        {
            if (query == null) { _cli = new QuotaCli(null); _direct = new DirectAgentPlan(); }
            _loginAction = loginOverride ?? (_direct == null ? null
                : new Func<CancellationToken, Task<CliResult>>(_direct.LoginAsync));
            _logoutAction = logoutOverride ?? (_direct == null ? null
                : new Func<CancellationToken, Task<CliResult>>(_direct.LogoutAsync));
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
            _form.SetReduceMotion(_floating.ReduceMotion);
            _floating.DetailsRequested += delegate { ShowDetails(); };
            _floating.DragStarted += delegate
            {
                _form.HidePanel();
                if (_menu != null) _menu.Close();
            };
            _floating.SettingsRequested += delegate { OpenSettings(); };
            _floating.ExitRequested += delegate { ExitApp(); };
            // v0.8 UX016 fix: bound right after _floating exists, before any
            // toggle can happen; the handler is safe both before _notify is
            // created and after Dispose nulls it.
            _floating.LockSaveFailed += delegate { OnLockSaveFailed(); };
            // v0.9 UX017: same binding discipline as LockSaveFailed - bound
            // right after _floating exists, handler is _disposed / _notify safe.
            _floating.ReduceMotionChanged += delegate
            {
                _form.SetReduceMotion(_floating.ReduceMotion);
                SyncLockChecked();
            };
            _floating.MotionSaveFailed += delegate { OnMotionSaveFailed(); };
            _controller = new SnapshotController(_form.Model, query ?? _direct.QueryDetailedAsync,
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
            _form.LoginRequested += async delegate { await Login(); };
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
            // The settings submenu exposes content selection and account actions. Preference
            // handlers remain available to existing callers without menu entries.
            _menuSettings = new ToggleMenuItem("设置");
            _menuContent = new ToggleMenuItem("悬浮内容");
            _menuSettings.DropDownItems.Add(_menuContent);
            // v0.8 UX016: checkable "锁定位置" shared with the circle menu —
            // same state / handler inside FloatingQuotaForm; the check is
            // re-synced from the real state on change and on submenu opening.
            _menuLock = new ToolStripMenuItem("锁定位置");
            _menuLock.CheckOnClick = false; // state is owned by FloatingQuotaForm
            _menuLock.Click += delegate { _floating.TogglePositionLocked(); };
            // v0.9 UX017: checkable "减少动画" shared with the circle menu.
            _menuMotion = new ToolStripMenuItem("减少动画");
            _menuMotion.CheckOnClick = false; // state is owned by FloatingQuotaForm
            _menuMotion.Click += delegate { _floating.ToggleReduceMotion(); };
            // v0.12 UX020: "悬浮窗归位" (one shared item for the tray and the
            // circle right-click menu). Explicit re-home: zero query, nothing
            // persisted, the lock never blocks it.
            _menuHome = new ToolStripMenuItem("悬浮窗归位", null,
                delegate { RepositionFloatingHome(); });
            _menuToggle = new ToolStripMenuItem("隐藏悬浮窗", null, delegate { ToggleFloating(); });
            // Explicit native side expansion on click (never a modal).
            _menuSettings.Click += delegate
            {
                _menuSettingsDropDownRequested = true;
            };
            // Rebuild 悬浮内容 from the CURRENT snapshot on every open; a
            // candidate that changes while open is re-validated on click.
            _menuSettings.DropDownOpening += delegate
            {
                _floating.PopulateContentMenu(_menuContent);
                SyncLockChecked();
                UpdateToggleText();
            };
            _menu.Items.Add(_menuSettings);
            _menu.Items.Add(_menuToggle);
            _menu.Items.Add("退出 ark_left", null, delegate { ExitApp(); });
            _menuLogout = new ToolStripMenuItem("登出", null, async delegate { await Logout(); });
            _menuLogin = new ToolStripMenuItem("重新登录", null,
                async delegate { await Login(); });
            _menuSettings.DropDownItems.Add(_menuLogout);
            _menuSettings.DropDownItems.Add(_menuLogin);
            _menu.Items.Insert(2, new ToolStripMenuItem("关于 / 诊断", null, delegate { ShowRuntimeDiagnostics(); }));
            UiStyle.StyleMenu(_menu);
            _floating.SetContextMenuStrip(_menu);
            UiStyle.AttachFloatingMenu(_menu, _menuSettings, _menuContent,
                _floating.CircleSurface);
            _menu.Opening += delegate
            {
                _form.HidePanel();
                _form.SetMenuOpen(true);
                _floating.PopulateContentMenu(_menuContent);
                UpdateToggleText();
                SyncLockChecked();
            };
            _menu.Closed += delegate { _form.SetMenuOpen(false); };
            _floating.PositionLockChanged += delegate { SyncLockChecked(); };
            // A toggle inside the open 设置 submenu must re-sync both checks
            // and the 悬浮内容 list (the opening hook does not fire again).
            _floating.ContentChanged += delegate
            {
                _floating.PopulateContentMenu(_menuContent);
                SyncLockChecked();
            };
            _floating.ContentSaveFailed += delegate { OnContentSaveFailed(); };
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

            // Runtime maintenance runs independently of quota polling and after UI is ready.
            _runtimeTimer.Interval = 15000;
            _runtimeTimer.Tick += async delegate {
                _runtimeTimer.Interval = 3600000;
                if (_disposed || _cli == null) return;
                ArkCliRuntimeManager manager = _cli.RuntimeManager;
                CancellationToken token = _lifetime.Token;
                await Task.Run(() => manager.CheckForUpdateAsync(token));
            };
            _runtimeTimer.Start();


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

        private void ExitApp()
        {
            _poll.Stop();
            _runtimeTimer.Stop(); _lifetime.Cancel();
            if (_login != null) _login.Cancel();
            _controller.Dispose();
            if (_direct != null) _direct.Dispose();
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
                _lifetime.Cancel(); _runtimeTimer.Dispose();
                if (_login != null) _login.Cancel();
                _poll.Dispose();
                if (_controller != null) _controller.Dispose();
                try { if (_notify != null) { _notify.Visible = false; _notify.Dispose(); } } catch (Exception) { }
                try { if (_floating != null) _floating.Dispose(); } catch (Exception) { }
                try { if (_menu != null) _menu.Dispose(); } catch (Exception) { }
                if (_menuLock != null) _menuLock.Dispose();
                if (_menuMotion != null) _menuMotion.Dispose();
                if (_menuHome != null) _menuHome.Dispose();
                try { if (_trayIcon != null) _trayIcon.Dispose(); } catch (Exception) { }
                try { if (_cli != null) _cli.Dispose(); } catch (Exception) { }
                try { if (_direct != null) _direct.Dispose(); } catch (Exception) { }
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
            string packaged = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "docs", "setup.md");
            if (File.Exists(packaged)) return packaged;
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "docs");
            string full = Path.GetFullPath(Path.Combine(dir, "setup.md"));
            return full;
        }

    }

}
