using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArkLeft
{
    internal sealed partial class TrayApp
    {
        internal void OpenSettingsForTest() { OpenSettings(); }

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
        // v0.15 UX023 test hooks: shared 设置 submenu / 悬浮内容 chooser and
        // the distinct failed-content-save tray intent.
        internal bool MenuSettingsDropDownRequestedForTest
        {
            get { return _menuSettingsDropDownRequested; }
        }
        internal ToolStripMenuItem MenuSettingsForTest { get { return _menuSettings; } }
        internal ContextMenuStrip MenuForTest { get { return _menu; } }
        internal int MenuSettingsCountForTest
        {
            get { return _menuSettings == null ? 0 : _menuSettings.DropDownItems.Count; }
        }
        internal string MenuSettingsTextForTest(int i)
        {
            return (_menuSettings != null && i >= 0 && i < _menuSettings.DropDownItems.Count)
                ? _menuSettings.DropDownItems[i].Text : null;
        }
        internal void PerformMenuSettingsClickForTest()
        {
            if (_menuSettings != null) _menuSettings.PerformClick();
        }
        internal void OpenMenuContentForTest()
        {
            _floating.PopulateContentMenu(_menuContent);
        }
        internal int MenuContentCountForTest
        {
            get { return _menuContent == null ? 0 : _menuContent.DropDownItems.Count; }
        }
        internal bool MenuContentEnabledForTest
        {
            get { return _menuContent != null && _menuContent.Enabled; }
        }
        internal string MenuContentTextForTest(int i)
        {
            return (_menuContent != null && i >= 0 && i < _menuContent.DropDownItems.Count)
                ? _menuContent.DropDownItems[i].Text : null;
        }
        internal bool MenuContentCheckedForTest(int i)
        {
            if (_menuContent == null || i < 0 || i >= _menuContent.DropDownItems.Count) return false;
            ToolStripMenuItem it = _menuContent.DropDownItems[i] as ToolStripMenuItem;
            return it != null && it.Checked;
        }
        internal void PerformMenuContentForTest(int i)
        {
            if (_menuContent == null || i < 0 || i >= _menuContent.DropDownItems.Count) return;
            ToolStripMenuItem it = _menuContent.DropDownItems[i] as ToolStripMenuItem;
            if (it != null) it.PerformClick();
        }
        internal void PerformMenuLockForTest() { if (_menuLock != null) _menuLock.PerformClick(); }
        internal void PerformMenuMotionForTest() { if (_menuMotion != null) _menuMotion.PerformClick(); }
        internal void PerformMenuHomeForTest() { if (_menuHome != null) _menuHome.PerformClick(); }
        internal void PerformMenuToggleForTest() { if (_menuToggle != null) _menuToggle.PerformClick(); }
        internal void ShowDetailsForTest() { ShowDetails(); }
        internal int ContentFailNotifyCountForTest { get { return _contentFailNotifyCount; } }
        internal string LastContentFailTextForTest { get { return _lastContentFailText; } }

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

        internal Task LoginForTest() { return Login(); }
        internal Task LogoutForTest() { return Logout(); }
        internal Task RefreshForTest() { return _controller.Refresh(); }
        internal Task CheckAppUpdateForTest(bool manual) { return CheckAppUpdate(manual); }
        internal AppUpdateResult AppUpdateResultForTest { get { return _appUpdateResult; } }
        internal bool AppUpdateCheckingForTest { get { return _appUpdateChecking; } }
        internal int AppUpdateNotifyCountForTest { get { return _appUpdateNotifyCount; } }
        internal void OpenAppReleaseForTest() { OpenAppRelease(); }
        internal AppUpdateForm AboutDialogForTest { get { return _aboutDialog; } }
    }
}
