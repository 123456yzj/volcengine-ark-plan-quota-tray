using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArkLeft
{
    internal sealed partial class TrayApp
    {
        private AppUpdateResult _appUpdateResult;
        private bool _appUpdateChecking;
        private string _notifiedAppVersion;
        private int _appUpdateNotifyCount;
        private AppUpdateForm _aboutDialog;

        private async Task CheckAppUpdate(bool manual)
        {
            if (_disposed || _lifetime.IsCancellationRequested || _appUpdateChecking) return;
            _appUpdateChecking = true;
            if (_aboutDialog != null) _aboutDialog.ShowResult(_appUpdateResult, true);
            try
            {
                AppUpdateResult result = await _checkAppUpdate(_lifetime.Token);
                if (_disposed || _lifetime.IsCancellationRequested) return;
                _appUpdateResult = result ?? new AppUpdateResult { Status = AppUpdateStatus.Failed };
                if (!manual && _appUpdateResult.Status == AppUpdateStatus.Available)
                {
                    string version = AppUpdateClient.DisplayVersion(_appUpdateResult.Version);
                    if (_notifiedAppVersion != version)
                    {
                        _notifiedAppVersion = version; _appUpdateNotifyCount++;
                        if (_notify != null)
                            try { _notify.ShowBalloonTip(6000, "ark_left 有新版本",
                                "发现 v" + version + "，打开“关于 / 诊断”下载更新。", ToolTipIcon.Info); }
                            catch (Exception) { }
                    }
                }
            }
            catch (Exception)
            {
                if (!_disposed && !_lifetime.IsCancellationRequested)
                    _appUpdateResult = new AppUpdateResult { Status = AppUpdateStatus.Failed };
            }
            finally
            {
                _appUpdateChecking = false;
                if (!_disposed && _aboutDialog != null) _aboutDialog.ShowResult(_appUpdateResult, false);
            }
        }

        private void OpenAppRelease()
        {
            if (_disposed || _appUpdateResult == null || _appUpdateResult.Status != AppUpdateStatus.Available) return;
            try { _openAppRelease(_appUpdateResult.ReleaseUrl); }
            catch (Exception) { if (_aboutDialog != null) _aboutDialog.ShowDownloadFailure(); }
        }

        private void ShowAboutDiagnostics()
        {
            if (_disposed) return;
            if (_aboutDialog != null) { _aboutDialog.Activate(); return; }
            using (AppUpdateForm dialog = new AppUpdateForm(_appVersion,
                delegate { return CheckAppUpdate(true); }, OpenAppRelease,
                _cli == null ? null : new Action(ShowRuntimeDiagnostics)))
            {
                _aboutDialog = dialog;
                dialog.ShowResult(_appUpdateResult, _appUpdateChecking);
                dialog.Shown += async delegate {
                    if (_appUpdateResult == null) await CheckAppUpdate(true);
                };
                _form.SetDialogOpen(true);
                try { dialog.ShowDialog(_floating.CircleSurface); }
                finally
                {
                    _aboutDialog = null;
                    if (!_disposed) _form.SetDialogOpen(false);
                }
            }
        }
    }
}
