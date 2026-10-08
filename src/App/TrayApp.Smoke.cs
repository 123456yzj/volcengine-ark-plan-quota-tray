using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ArkLeft
{
    internal sealed partial class TrayApp
    {
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
                    // The message card sits inside the rectangular outer frame.
                    if (f.Region != null)
                        failures++;
                    if (f.ContentCardCount != 1)
                        failures++;

                    f.ForceRender(SyntheticSample.BuildLarge());
                    failures += AssertInside(f, wa, "large");
                    failures += AssertInnerControls(f, "large");
                    failures += AssertCardFit(f, "large");
                    TrySavePreview(f, "preview.png");

                    List<Control> stable = ControlTree(f.ContentForTest);
                    f.ContentForTest.Controls[0].Focus();
                    f.ContentForTest.AutoScrollPosition = new Point(0, 90);
                    Point scroll = f.ContentForTest.AutoScrollPosition;
                    string time = f.UpdateTimeTextForTest;
                    f.BeginAuth(); f.ShowSlowHint(); f.ShowStageHint("正在检查登录状态");
                    failures += AssertSameControls(stable, ControlTree(f.ContentForTest), "waiting");
                    if (!f.ContentForTest.Controls[0].Focused || f.ContentForTest.AutoScrollPosition != scroll)
                        failures++;
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
                    if (f.Region != null)
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
                    QuotaSnapshot modern = SyntheticSample.BuildLarge();
                    modern.Products.RemoveRange(1, modern.Products.Count - 1);
                    f.ForceRender(modern);
                    TrySavePreview(f, "preview-detail.png");

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
        // (the card content) and no details context menu.
        private static int AssertCardChrome(PopupForm f, string label)
        {
            int bad = 0;
            if (f.Controls.Count != 1 || f.Controls[0] != f.ContentForTest
                || f.ContentForTest.Padding.All != 0)
            {
                bad++;
                Console.Error.WriteLine(label + ": unexpected chrome (must be content-only)");
            }
            foreach (Control c in ControlTree(f))
            {
                if (c.ContextMenuStrip == null && c.ContextMenu == null) continue;
                bad++;
                Console.Error.WriteLine(label + ": unexpected details context menu");
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
    }
}
