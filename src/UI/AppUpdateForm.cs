using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArkLeft
{
    internal sealed class AppUpdateForm : Form
    {
        private readonly Label _status;
        internal readonly Button CheckButton;
        internal readonly Button DownloadButton;

        internal AppUpdateForm(Version current, Func<Task> check, Action download, Action diagnostics)
        {
            Text = "ark_left 关于 / 诊断";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = SystemFonts.MessageBoxFont;
            ClientSize = new Size(440, 260);
            Label version = new Label { AutoSize = true, Location = new Point(20, 20),
                Text = "ark_left 当前版本：v" + AppUpdateClient.DisplayVersion(current) };
            Controls.Add(version);
            _status = new Label { AutoSize = false, Bounds = new Rectangle(20, 55, 400, 50),
                Text = "尚未检查应用更新。", AccessibleName = "应用更新状态" };
            Controls.Add(_status);
            CheckButton = new Button { Text = "检查应用更新", Bounds = new Rectangle(20, 110, 130, 32) };
            CheckButton.Click += async delegate { await check(); };
            Controls.Add(CheckButton);
            DownloadButton = new Button { Text = "打开下载页面", Enabled = false,
                Bounds = new Rectangle(165, 110, 130, 32) };
            DownloadButton.Click += delegate { download(); };
            Controls.Add(DownloadButton);
            Controls.Add(new Label { AutoSize = false, Bounds = new Rectangle(20, 155, 400, 38),
                Text = "启动后自动检测，运行期间每 24 小时检查一次。\r\n下载后先退出旧版，再运行安装包。" });
            Button runtime = new Button { Text = "ArkCLI 组件诊断", Enabled = diagnostics != null,
                Bounds = new Rectangle(20, 210, 150, 32) };
            runtime.Click += delegate { if (diagnostics != null) diagnostics(); };
            Controls.Add(runtime);
            Button close = new Button { Text = "关闭", DialogResult = DialogResult.Cancel,
                Bounds = new Rectangle(310, 210, 110, 32) };
            Controls.Add(close); CancelButton = close;
        }

        internal void ShowResult(AppUpdateResult result, bool checking)
        {
            if (IsDisposed || Disposing) return;
            CheckButton.Enabled = !checking;
            DownloadButton.Enabled = !checking && result != null && result.Status == AppUpdateStatus.Available;
            if (checking) _status.Text = "正在检查应用更新…";
            else if (result == null) _status.Text = "尚未检查应用更新。";
            else if (result.Status == AppUpdateStatus.Available)
                _status.Text = "发现新版本 v" + AppUpdateClient.DisplayVersion(result.Version) + "，可打开下载页面更新。";
            else if (result.Status == AppUpdateStatus.Current) _status.Text = "当前已是最新版本。";
            else if (result.Status == AppUpdateStatus.Cancelled) _status.Text = "更新检查已取消，可重试。";
            else _status.Text = "无法检查应用更新，请检查网络后重试。";
        }

        internal void ShowDownloadFailure()
        {
            if (!IsDisposed) _status.Text = "无法打开浏览器，请稍后重试打开下载页面。";
        }

        internal string StatusForTest { get { return _status.Text; } }
    }
}
