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
    internal partial class FloatingQuotaForm
    {
        private ContextMenuStrip BuildDefaultMenu()
        {
            // Match the shared tray menu: content selection, visibility, exit.
            // Keep the existing preference handlers without exposing entries.
            ContextMenuStrip menu = new ContextMenuStrip();
            _settingsItem = new ToggleMenuItem("设置");
            _contentItem = new ToggleMenuItem("悬浮内容");
            _settingsItem.DropDownItems.Add(_contentItem);
            // v0.8 UX016: shared checkable "锁定位置" (same state / handler as
            // the tray menu item; TrayApp syncs via PositionLockChanged).
            _lockItem = new ToolStripMenuItem("锁定位置");
            _lockItem.CheckOnClick = false; // state is owned by the handler
            _lockItem.Click += delegate { TogglePositionLocked(); };
            // v0.9 UX017: checkable "减少动画"; same state / handler as the
            // tray menu item.
            _motionItem = new ToolStripMenuItem("减少动画");
            _motionItem.CheckOnClick = false; // state is owned by the handler
            _motionItem.Click += delegate { ToggleReduceMotion(); };
            // Retain the re-home handler for existing callers.
            _homeItem = new ToolStripMenuItem("悬浮窗归位", null,
                delegate { RepositionCircleHome(); UpdateToggleItemText(); });
            // The visibility toggle is a top-level entry.
            _toggleItem = new ToolStripMenuItem("隐藏悬浮窗", null,
                delegate { ToggleCircleForMenu(); });
            // Explicit native side expansion on click (never a modal). Guarded
            // so an offline PerformClick on a never-shown strip records the
            // intent without creating a real popup window.
            _settingsItem.Click += delegate { RequestSettingsDropDown(); };
            _settingsItem.DropDownOpening += delegate
            {
                PopulateContentMenu(_contentItem);
                SyncPrefMenuChecks();
                UpdateToggleItemText();
            };
            menu.Items.Add(_settingsItem);
            menu.Items.Add(_toggleItem);
            menu.Items.Add("退出 ark_left", null, delegate { RaiseExit(); });
            PopulateContentMenu(_contentItem);
            SyncPrefMenuChecks();
            UpdateToggleItemText();
            UiStyle.AttachFloatingMenu(menu, _settingsItem, _contentItem, _circle);
            return menu;
        }

        // v0.15 UX023: explicit native side expansion for the 设置 submenu. In
        // production the strip is visible so ShowDropDown really opens the
        // native dropdown; offline (never-shown strip) only the intent flag is
        // recorded, so no orphan popup window is created in tests.
        private void RequestSettingsDropDown()
        {
            _settingsDropDownRequested = true;
        }

        // v0.15 UX023: rebuild the 悬浮内容 candidates from the CURRENT
        // snapshot (called on every submenu open and after a selection). A menu
        // opened over an old snapshot is re-validated at click time, so a
        // period that disappears while the menu is open can never be persisted.
        // Zero query, no modal.
        public void PopulateContentMenu(ToolStripMenuItem contentItem)
        {
            if (contentItem == null) return;
            ClearContentItems(contentItem.DropDownItems);
            List<FloatingEntry> candidates = FloatingSelection.SelectableCandidates(_entries);
            if (candidates.Count == 0)
            {
                contentItem.Enabled = false; // no data -> disabled
                return;
            }
            contentItem.Enabled = true;
            int generation = _contentGeneration;
            foreach (FloatingEntry entry in candidates)
            {
                FloatingEntry captured = entry;
                ToolStripMenuItem item = new ToolStripMenuItem(DisplayNames.Period(captured.Label));
                item.CheckOnClick = false; // state is owned by the handler
                item.Checked = IsCurrentContent(captured);
                item.Click += delegate { ApplyContentSelection(captured, generation); };
                contentItem.DropDownItems.Add(item);
            }
            UiStyle.StyleMenuBranch(contentItem);
        }

        // v0.15 UX023: rebuild deterministically releases the old items, but the
        // release is deferred so an item currently raising its own Click handler
        // is never disposed mid-handler (a real crash in WinForms). Removed items
        // go to a single queue and one shared drain is posted; Dispose drains the
        // queue synchronously, and a late posted drain on an empty queue is a
        // harmless no-op.
        private void ClearContentItems(ToolStripItemCollection items)
        {
            if (items == null || items.Count == 0) return;
            for (int i = 0; i < items.Count; i++) _pendingContentItems.Add(items[i]);
            items.Clear();
            RequestContentDrain();
        }

        private void RequestContentDrain()
        {
            if (_pendingContentItems.Count == 0) return;
            if (IsDisposed || Disposing) { DrainPendingContentItems(); return; }
            if (_contentDrainPosted) return; // one posted drain coalesces them all
            try
            {
                IntPtr h = Handle;
                GC.KeepAlive(h);
                if (IsDisposed || Disposing) { DrainPendingContentItems(); return; }
                _contentDrainPosted = true;
                BeginInvoke(new Action(delegate
                {
                    _contentDrainPosted = false;
                    DrainPendingContentItems();
                }));
            }
            catch (Exception) { _contentDrainPosted = false; DrainPendingContentItems(); }
        }

        // Releases every queued item. Safe on an empty queue and safe to call
        // from Dispose; never touches control state, so a late posted callback
        // after the form is gone only drains an (empty) list.
        private void DrainPendingContentItems()
        {
            for (int i = 0; i < _pendingContentItems.Count; i++)
            {
                try { _pendingContentItems[i].Dispose(); } catch (Exception) { }
                _contentItemsReleased++;
            }
            _pendingContentItems.Clear();
        }

        private bool IsCurrentContent(FloatingEntry e)
        {
            return _selected != null && _selected.ProductKey == e.ProductKey
                && _selected.PeriodLabel == e.Label;
        }

        private FloatingEntry FindCurrentContent(string productKey, string label)
        {
            foreach (FloatingEntry e in _entries)
            {
                if (e.ProductKey == productKey && e.Label == label) return e;
            }
            return null;
        }

        // v0.15 UX023: direct menu selection. Saves FIRST; only a successful
        // save flips _stored / _selected. A candidate that vanished or became
        // unavailable while the menu was open is rejected (never saved), and a
        // failed save keeps the old value plus a short readable hint. Zero
        // query, never opens a modal.
        public void ApplyContentSelection(FloatingEntry entry)
        {
            // Direct callers use the current candidate source.
            ApplyContentSelection(entry, _contentGeneration);
        }

        // The captured generation ties the click to the candidate list it was
        // built from: any ApplyModelView (new snapshot OR identity switch, same
        // key included) invalidates it. The entry is then re-validated in the
        // CURRENT entries before anything is persisted.
        public void ApplyContentSelection(FloatingEntry entry, int generation)
        {
            if (entry == null) return;
            if (generation != _contentGeneration)
            {
                ShowLockHint("悬浮内容不可用");
                return;
            }
            FloatingEntry current = FindCurrentContent(entry.ProductKey, entry.Label);
            if (current == null || !current.Selectable)
            {
                ShowLockHint("悬浮内容不可用");
                return;
            }
            if (IsCurrentContent(current)) return; // same target: skip the save
            FloatingSettings s = new FloatingSettings();
            s.Version = FloatingSettingsStore.FormatVersion;
            s.ProductKey = current.ProductKey;
            s.PeriodLabel = current.Label;
            bool saved;
            try { saved = _save != null && _save(s); }
            catch (Exception) { saved = false; }
            if (saved)
            {
                _stored = s;
                _selected = s;
                UpdateCircleDisplay();
                ShowLockHint("已切换悬浮内容");
            }
            else
            {
                ShowLockHint("悬浮内容未保存");
                if (ContentSaveFailed != null) ContentSaveFailed(this, EventArgs.Empty);
            }
            PopulateContentMenu(_contentItem);
            SyncPrefMenuChecks();
            UpdateToggleItemText();
            if (ContentChanged != null) ContentChanged(this, EventArgs.Empty);
        }

        private void ToggleCircleForMenu()
        {
            ToggleCircle();
            UpdateToggleItemText();
        }

        // The show / hide toggle label mirrors the real circle state (kept in
        // sync from the circle VisibleChanged hook and on every submenu open).
        private void UpdateToggleItemText()
        {
            if (_toggleItem == null) return;
            _toggleItem.Text = CircleVisible ? "隐藏悬浮窗" : "显示悬浮窗";
        }

        // Shares the tray menu (TrayApp owns it) with the circle so both show the
        // same button group. The default menu is disposed with the form.
        public void SetContextMenuStrip(ContextMenuStrip menu)
        {
            if (menu == null) return;
            _circle.ContextMenuStrip = menu;
        }

        private void RaiseDetails() { if (DetailsRequested != null) DetailsRequested(this, EventArgs.Empty); }
        private void RaiseSettings() { if (SettingsRequested != null) SettingsRequested(this, EventArgs.Empty); }
        private void RaiseExit() { if (ExitRequested != null) ExitRequested(this, EventArgs.Empty); }

        // ---- v0.8 UX016 position lock ----

        public bool PositionLocked { get { return _positionLocked; } }

        public void TogglePositionLocked()
        {
            SetPositionLocked(!_positionLocked);
        }

        // The lock is persisted BEFORE the state flips: a failed write keeps
        // the old state and menu checks, and shows a short readable hint
        // (never the raw exception, never a claim that it was saved). Same-
        // value calls skip the save entirely.
        public void SetPositionLocked(bool locked)
        {
            if (locked != _positionLocked)
            {
                FloatingPreferences p = new FloatingPreferences();
                p.Version = FloatingPreferencesStore.FormatVersion;
                p.PositionLocked = locked;
                // v0.9 UX017: format 2 saves always carry BOTH flags, so a
                // lock toggle can never overwrite the reduce-motion choice
                // (and vice versa - see SetReduceMotion).
                p.ReduceMotion = _reduceMotion;
                bool saved;
                try { saved = _prefsSave(p); }
                catch (Exception) { saved = false; }
                if (saved)
                {
                    _positionLocked = locked;
                    _circle.PositionLocked = locked;
                    ShowLockHint(locked ? "已锁定位置" : "已解锁位置");
                }
                else
                {
                    ShowLockHint("锁定状态未保存");
                    if (LockSaveFailed != null) LockSaveFailed(this, EventArgs.Empty);
                }
            }
            SyncPrefMenuChecks();
            if (PositionLockChanged != null) PositionLockChanged(this, EventArgs.Empty);
        }

        // v0.9 UX017: syncs BOTH preference checks (lock + reduce motion);
        // called at the same points as before (ctor, build, toggles).
        private void SyncPrefMenuChecks()
        {
            if (_lockItem != null) _lockItem.Checked = _positionLocked;
            if (_motionItem != null) _motionItem.Checked = _reduceMotion;
        }

        // ---- v0.9 UX017 reduce motion ----

        public bool ReduceMotion { get { return _reduceMotion; } }

        public void ToggleReduceMotion()
        {
            SetReduceMotion(!_reduceMotion);
        }

        // Both flags are always saved together (format 2): a reduce-motion
        // save carries the current lock state and vice versa, so neither
        // toggle can overwrite the other. The state flips only after a
        // successful save; a failed save keeps every old preference and both
        // menu checks, shows a short motion-specific hint (never the lock
        // wording, never a raw exception), and raises MotionSaveFailed for
        // the tray notification. Same-value calls skip the
        // save entirely.
        public void SetReduceMotion(bool on)
        {
            if (on != _reduceMotion)
            {
                FloatingPreferences p = new FloatingPreferences();
                p.Version = FloatingPreferencesStore.FormatVersion;
                p.PositionLocked = _positionLocked;
                p.ReduceMotion = on;
                bool saved;
                try { saved = _prefsSave(p); }
                catch (Exception) { saved = false; }
                if (saved)
                {
                    _reduceMotion = on;
                    _circle.SetReduceMotion(on);
                    ShowLockHint(on ? "已减少动画" : "已恢复动画");
                }
                else
                {
                    ShowLockHint("动画设置未保存");
                    if (MotionSaveFailed != null) MotionSaveFailed(this, EventArgs.Empty);
                }
            }
            SyncPrefMenuChecks();
            if (ReduceMotionChanged != null) ReduceMotionChanged(this, EventArgs.Empty);
        }

        private void ShowLockHint(string text)
        {
            _lockHint = text;
        }

        internal string LockHintForTest { get { return _lockHint; } }
        internal bool DefaultMenuLockCheckedForTest
        {
            get { return _lockItem != null && _lockItem.Checked; }
        }
        // v0.9 UX017 test hook: default (circle) menu motion check.
        internal bool DefaultMenuMotionCheckedForTest
        {
            get { return _motionItem != null && _motionItem.Checked; }
        }
        // v0.15 UX023 test hooks: the default menu's top level and its 设置
        // submenu (悬浮内容 chooser + existing toggles).
        internal bool SettingsDropDownRequestedForTest { get { return _settingsDropDownRequested; } }
        // v0.15 UX023: count of rebuilt 悬浮内容 items deterministically released.
        internal int ContentItemsReleasedForTest { get { return _contentItemsReleased; } }
        internal int ContentGenerationForTest { get { return _contentGeneration; } }
        internal int DefaultMenuTopCountForTest { get { return _menu == null ? 0 : _menu.Items.Count; } }
        internal string DefaultMenuTopTextForTest(int i)
        {
            return (_menu != null && i >= 0 && i < _menu.Items.Count) ? _menu.Items[i].Text : null;
        }
        internal int DefaultMenuSettingsCountForTest
        {
            get { return _settingsItem == null ? 0 : _settingsItem.DropDownItems.Count; }
        }
        internal string DefaultMenuSettingsTextForTest(int i)
        {
            return (_settingsItem != null && i >= 0 && i < _settingsItem.DropDownItems.Count)
                ? _settingsItem.DropDownItems[i].Text : null;
        }
        internal void PerformDefaultMenuSettingsForTest()
        {
            if (_settingsItem != null) _settingsItem.PerformClick();
        }
        internal int DefaultMenuContentCountForTest
        {
            get { return _contentItem == null ? 0 : _contentItem.DropDownItems.Count; }
        }
        internal bool DefaultMenuContentEnabledForTest
        {
            get { return _contentItem != null && _contentItem.Enabled; }
        }
        internal string DefaultMenuContentTextForTest(int i)
        {
            return (_contentItem != null && i >= 0 && i < _contentItem.DropDownItems.Count)
                ? _contentItem.DropDownItems[i].Text : null;
        }
        internal bool DefaultMenuContentCheckedForTest(int i)
        {
            if (_contentItem == null || i < 0 || i >= _contentItem.DropDownItems.Count) return false;
            ToolStripMenuItem it = _contentItem.DropDownItems[i] as ToolStripMenuItem;
            return it != null && it.Checked;
        }
        internal void PerformDefaultMenuContentForTest(int i)
        {
            if (_contentItem == null || i < 0 || i >= _contentItem.DropDownItems.Count) return;
            ToolStripMenuItem it = _contentItem.DropDownItems[i] as ToolStripMenuItem;
            if (it != null) it.PerformClick();
        }
        // Expands the default 设置 submenu content (rebuilds 悬浮内容 from the
        // current snapshot) without a real popup window.
        internal void OpenDefaultMenuContentForTest()
        {
            PopulateContentMenu(_contentItem);
        }
        internal string DefaultMenuToggleTextForTest
        {
            get { return _toggleItem == null ? null : _toggleItem.Text; }
        }
        // v0.12 UX020 test hook: a stale details auto-hide flag must be
        // cleared by an explicit re-home.
        internal bool AutoHiddenForDetailsForTest
        {
            get { return _autoHiddenForDetails; }
        }
        // v0.12 UX020 fix hook: the real once-per-session positioning state
        // (ShowCircleAtForTest intentionally does NOT set it, so a fresh
        // form is a genuine not-positioned fixture).
        internal bool PositionedForTest { get { return _positioned; } }

    }
}
