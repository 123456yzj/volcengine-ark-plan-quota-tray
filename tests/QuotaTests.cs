using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using ArkLeft;

namespace ArkLeft.Tests
{
    // Minimal, dependency-free test runner for the pure parsing/CLI logic.
    // Compiled with the same csc (C# 5) and shares src sources.
    internal static partial class QuotaTests
    {
        private static int _passed;
        private static int _failed;
        private static readonly List<string> _failures = new List<string>();

        [STAThread]
        private static int Main(string[] args)
        {
            if (Environment.GetEnvironmentVariable("ARK_LEFT_TEST_CHILD") == "1") return RuntimeTests.RunChild(args);
            try
            {
                if (args.Length == 1 && args[0] == "--runtime")
                {
                    RuntimeTests.Run(Check);
                    Console.WriteLine("runtime: passed " + _passed + ", failed " + _failed);
                    foreach (string failure in _failures) Console.WriteLine(failure);
                    return _failed == 0 ? 0 : 1;
                }
                if (args.Length == 1 && args[0] == "--direct")
                {
                    DirectCases();
                    Console.WriteLine("direct: passed " + _passed + ", failed " + _failed);
                    foreach (string failure in _failures) Console.WriteLine(failure);
                    return _failed == 0 ? 0 : 1;
                }
                if (args.Length == 1 && args[0] == "--direct-live-ui")
                {
                    DirectLiveUiCases();
                    Console.WriteLine("direct-live-ui: passed " + _passed + ", failed " + _failed);
                    foreach (string failure in _failures) Console.WriteLine(failure);
                    return _failed == 0 ? 0 : 1;
                }
                if (args.Length == 1 && args[0] == "--menu-interaction")
                {
                    MenuInteractionCases();
                    FloatingMenuUX023Cases();
                    Console.WriteLine("menu-interaction: passed " + _passed + ", failed " + _failed);
                    foreach (string failure in _failures) Console.WriteLine(failure);
                    return _failed == 0 ? 0 : 1;
                }
                if (args.Length == 1 && args[0] == "--compact-ui")
                {
                    CompactUiCases();
                    Console.WriteLine("compact-ui: passed " + _passed + ", failed " + _failed);
                    foreach (string failure in _failures) Console.WriteLine(failure);
                    return _failed == 0 ? 0 : 1;
                }
                if (args.Length == 1 && args[0] == "--effective-quota")
                {
                    EffectiveQuotaCases();
                    Console.WriteLine("effective-quota: passed " + _passed + ", failed " + _failed);
                    foreach (string failure in _failures) Console.WriteLine(failure);
                    return _failed == 0 ? 0 : 1;
                }
                if (args.Length == 1 && args[0] == "--floating-amount")
                {
                    EffectiveQuotaCases();
                    FloatingSelectionCases();
                    FloatingDisplayCases();
                    FloatingWindowCases();
                    FloatingTrayAppCases();
                    Console.WriteLine("floating-amount: passed " + _passed + ", failed " + _failed);
                    foreach (string failure in _failures) Console.WriteLine(failure);
                    return _failed == 0 ? 0 : 1;
                }
                if (args.Length == 1 && args[0] == "--theme")
                {
                    FloatingPreferencesUX016Cases();
                    FloatingPreferencesUX017Cases();
                    FloatingSettingsDialogCases();
                    FloatingMenuUX023Cases();
                    ThemeUX029Cases();
                    Console.WriteLine("theme: passed " + _passed + ", failed " + _failed);
                    foreach (string failure in _failures) Console.WriteLine(failure);
                    return _failed == 0 ? 0 : 1;
                }
                if (args.Length == 1 && args[0] == "--floating-antialias")
                {
                    FloatingAntialiasCases();
                    Console.WriteLine("floating-antialias: passed " + _passed + ", failed " + _failed);
                    foreach (string failure in _failures) Console.WriteLine(failure);
                    return _failed == 0 ? 0 : 1;
                }
                if (args.Length == 1 && args[0] == "--floating-antialias-after-login")
                {
                    DirectCases();
                    FloatingAntialiasCases();
                    Console.WriteLine("floating-antialias-after-login: passed " + _passed + ", failed " + _failed);
                    foreach (string failure in _failures) Console.WriteLine(failure);
                    return _failed == 0 ? 0 : 1;
                }
                if (args.Length == 1 && (args[0] == "--app-update" || args[0] == "--app-update-live"))
                {
                    if (args[0] == "--app-update-live") AppUpdateLiveCase();
                    else AppUpdateCases();
                    Console.WriteLine("app-update: passed " + _passed + ", failed " + _failed);
                    foreach (string failure in _failures) Console.WriteLine(failure);
                    return _failed == 0 ? 0 : 1;
                }
                return RunTests();
            }
            catch (Exception ex)
            {
                try { System.IO.File.WriteAllText(
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-crash.txt"),
                    ex.ToString()); } catch (Exception) { }
                Console.Error.WriteLine("unexpected test error: " + ex.Message);
                return 2;
            }
        }

        private static int RunTests()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("ark_left tests (synthetic fixtures only)");
            Console.WriteLine("========================================");

            PercentWins();
            ZeroIsValid();
            MissingIsUnknown();
            UsedTotalFallback();
            TotalZero();
            OutOfBoundsClamp();
            MalformedAndShape();
            PartialErrors();
            UnsubscribedAndEmpty();
            ErrorBucketNotUnsubscribed();
            MissingSubscribedIsMalformed();
            NullItemAndNullPeriod();
            PeriodErrorPartial();
            ItemUpdatedAt();
            ErrorTextNotLeaked();
            CliAuthGate();
            CliUsageExitCode();
            CliTimeoutCancelDispose();
            DirectCases();
            AppUpdateCases();
            RuntimeTests.Run(Check);
            DisplayNameMapping();
            LayoutMathBounds();
            PercentFormatCases();
            EffectiveQuotaCases();
            RelativeFormatCases();
            RiskSummaryCases();
            ScopeFingerprintCases();
            ScopeValidationCases();
            PanelModelIdentityCases();
            PanelModelStaleCases();
            PanelModelUnknownVerdictCases();
            PanelModelNullIdentityCases();
            PanelModelReuseTiedToCurrentQuery();
            PanelModelAuthHintClearing();
            HideControllerCases();
            PanelPositionerCases();
            IdentityParseCases();
            IdentityDisplayCases();
            PanelStateAfterMismatch();
            ViewEmptyProductsCases();
            IpcSignalCases();
            MarkerIsolationCases();
            ProgressStageCases();
            PersistentSnapshotCases();
            SnapshotControllerCases();
            SnapshotProgressCases();
            PopupDisplayCases();
            ActualOpenEntryCases();
            ActualRefreshButtonCases();
            FloatingSelectionCases();
            FloatingSettingsStoreCases();
            FloatingDisplayCases();
            FloatingWindowCases();
            FloatingTrayAppCases();
            FloatingSettingsDialogCases();
            FloatingLayoutCases();
            FloatingSettingsModalCases();
            FloatingPrepareDetailsCases();
            PollIntervalUX013Cases();
            FloatingSettingsUX015Cases();
            FloatingPreferencesUX016Cases();
            FloatingPreferencesUX017Cases();
            QuotaSummaryCases();
            CopySummaryUX019Cases();
            RepositionHomeUX020Cases();
            DetailsShortcutUX021Cases();
            CardOnlyUX022Cases();
            FloatingMenuUX023Cases();
            ThemeUX029Cases();

            Console.WriteLine();
            Console.WriteLine("passed: " + _passed + ", failed: " + _failed);
            if (_failed > 0)
            {
                Console.WriteLine("failures:");
                for (int i = 0; i < _failures.Count; i++)
                    Console.WriteLine("  - " + _failures[i]);
                return 1;
            }
            return 0;
        }

        // ---- cases ----

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

        private static void PumpToggleMessages(int milliseconds)
        {
            DateTime until = DateTime.UtcNow.AddMilliseconds(milliseconds);
            do
            {
                Application.DoEvents();
                System.Threading.Thread.Sleep(10);
            } while (DateTime.UtcNow < until);
        }

        private static void LogMenuFailureState(int previousFailures, ContextMenuStrip menu,
            ToolStripMenuItem settings, ToolStripMenuItem content, Control circle)
        {
            if (_failed == previousFailures) return;
            Console.Error.WriteLine("menu state: cursor=" + Cursor.Position
                + " active=" + (Form.ActiveForm == null ? "null" : Form.ActiveForm.Text)
                + " circle=" + circle.Bounds + " root=" + menu.Bounds
                + " rootVisible=" + menu.Visible + " settings=" + settings.DropDown.Bounds
                + " settingsVisible=" + settings.DropDown.Visible + " content=" + content.DropDown.Bounds
                + " contentVisible=" + content.DropDown.Visible);
        }

        private static void CheckMenuGeometry(string tag, string level, ToolStripDropDown drop,
            Rectangle work, Control circle)
        {
            int before = _failed;
            Check(tag + "." + level + ".visible", drop.Visible, true);
            if (drop.Visible)
            {
                Check(tag + "." + level + ".onScreen", work.Contains(drop.Bounds), true);
                Check(tag + "." + level + ".avoidsCircle",
                    drop.Bounds.IntersectsWith(circle.Bounds), false);
            }
            if (_failed != before)
                Console.Error.WriteLine(tag + "." + level + " state: circle=" + circle.Bounds
                    + " bounds=" + drop.Bounds + " visible=" + drop.Visible
                    + " autoClose=" + drop.AutoClose + " cursor=" + Cursor.Position
                    + " active=" + (Form.ActiveForm == null ? "null" : Form.ActiveForm.Text));
        }

        // Helper form that applies arbitrary data with injected no-op prefs.
        private sealed class FloatingQuotaFormForData : FloatingQuotaForm
        {
            public FloatingQuotaFormForData(QuotaSnapshot snap) : this(snap, null) { }
            public FloatingQuotaFormForData(QuotaSnapshot snap, FloatingSettings stored)
                // v0.8 UX016: no-op preference callbacks keep these tests
                // hermetic regardless of the real machine state.
                : base(delegate { return stored; }, delegate(FloatingSettings s) { return true; },
                    delegate { return (FloatingPreferences)null; },
                    delegate(FloatingPreferences p) { return true; })
            {
                IntPtr h = Handle; GC.KeepAlive(h);
                PanelModel model = new PanelModel();
                model.OnAuthResult(true, PopupForm.SampleIdentity(), QuotaStatus.Ok, null);
                ApplyModelView(model.OnUsageResult(snap, null, ScopeVerdict.Same, null));
            }
        }

        private static PanelView EmptyView()
        {
            PanelModel m = new PanelModel();
            return m.ShowEmptyNoData();
        }

        private static QuotaSnapshot AllUnknownSnapshot()
        {
            return QuotaParser.Parse(Wrap(
                Item("agent-plan", "personal", "medium", true, Period("weekly", null, null, null, null))));
        }

        // percent is the USED percentage; remaining = clamp(100 - percent).
        private static QuotaSnapshot PercentSnapshot(double usedPercent)
        {
            return QuotaParser.Parse(Wrap(
                Item("agent-plan", "personal", "medium", true,
                    Period("monthly", usedPercent.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        null, null, null))));
        }

        private static PeriodQuota EffectivePeriod(string label, double amount, double total)
        {
            PeriodQuota p = new PeriodQuota();
            p.Label = label;
            p.AmountKnown = true; p.RemainingAmount = amount;
            p.TotalKnown = true; p.Total = total;
            p.PercentKnown = true; p.RemainingPercent = amount / total * 100.0;
            return p;
        }

        private static ViewerIdentity FullViewer(string account, string userId, string profile,
            string region, string project, bool isRoot)
        {
            ViewerIdentity v = new ViewerIdentity();
            v.Present = true;
            v.AccountId = account;
            v.UserId = userId;
            v.Profile = profile;
            v.Region = region;
            v.ProjectName = project;
            v.IsRoot = isRoot;
            v.IsRootKnown = true;
            return v;
        }

        private static QueryOutcome SuccessfulOutcome(AuthIdentity identity, QuotaSnapshot snapshot)
        {
            return new QueryOutcome { AuthConfirmed = true, Identity = identity,
                AuthScope = QueryScope.FromAuth(identity), Verdict = ScopeVerdict.Same, Snapshot = snapshot };
        }

        private sealed class QueuedContext : System.Threading.SynchronizationContext
        {
            private readonly Queue<Action> _queue = new Queue<Action>();
            public override void Post(System.Threading.SendOrPostCallback callback, object state)
            {
                _queue.Enqueue(delegate { callback(state); });
            }
            public void Drain() { while (_queue.Count != 0) _queue.Dequeue()(); }
        }

        private sealed class ProgressCollector : IProgress<QueryProgress>
        {
            public bool SawUsage;
            public bool SawDone;
            public bool SawAuthConfirmed;
            private bool _authIdentity;
            public void Report(QueryProgress value)
            {
                if (value.Stage == QueryStage.Usage) SawUsage = true;
                if (value.Stage == QueryStage.Done) SawDone = true;
                if (value.Stage == QueryStage.Auth && value.AuthConfirmed) SawAuthConfirmed = true;
                if (value.Stage == QueryStage.Auth && value.AuthIdentity != null) _authIdentity = true;
            }
            public bool SawAuthIdentity { get { return _authIdentity; } }
        }

        private static AuthIdentity AuthId(string owner, string name, string region, string project)
        {
            AuthIdentity a = new AuthIdentity();
            a.Present = true;
            a.OwnerTrn = owner;
            a.Name = name;
            a.Region = region;
            a.Project = project;
            return a;
        }

        // ---- helpers ----

        private static QuotaSnapshot Parse(string json)
        {
            return QuotaParser.Parse(json);
        }

        private static void CliResultTransport(string name, QuotaCli cli, QuotaStatus expected)
        {
            CliResultTransportToken(name, cli, expected, System.Threading.CancellationToken.None);
        }

        private static void CliResultTransportToken(string name, QuotaCli cli, QuotaStatus expected,
            System.Threading.CancellationToken token)
        {
            QuotaSnapshot s = cli.QueryAsync(token).Result;
            Check(name, s.Status, expected);
        }

        // After Dispose, starting a query must not start a process and must fail.
        private static bool TryStartAfterDispose(QuotaCli cli)
        {
            QuotaSnapshot s = cli.QueryAsync(System.Threading.CancellationToken.None).Result;
            return s.Status == QuotaStatus.Failed;
        }

        private static string Wrap(params string[] items)
        {
            return "{\"items\":[" + string.Join(",", items) + "]}";
        }

        private static string Item(string product, string edition, string tier, bool subscribed, string periods)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{\"product\":\"").Append(product).Append("\"");
            if (edition != null) sb.Append(",\"edition\":\"").Append(edition).Append("\"");
            if (tier != null) sb.Append(",\"tier\":\"").Append(tier).Append("\"");
            sb.Append(",\"subscribed\":").Append(subscribed ? "true" : "false");
            sb.Append(",\"periods\":[").Append(periods).Append("]}");
            return sb.ToString();
        }

        // Builds a period object from optional numeric/string values so the test
        // source contains no fragile nested escapes.
        private static string Period(string label, string percent, string used, string total, string resetAt)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{\"label\":\"").Append(label).Append("\"");
            if (percent != null) sb.Append(",\"percent\":").Append(percent);
            if (used != null) sb.Append(",\"used\":").Append(used);
            if (total != null) sb.Append(",\"total\":").Append(total);
            if (resetAt != null) sb.Append(",\"reset_at\":\"").Append(resetAt).Append("\"");
            sb.Append("}");
            return sb.ToString();
        }

        // ---- UX021 v0.13: details-local Ctrl+R / Ctrl+C accelerators ----

        // Reflects the protected Control.ProcessCmdKey so a key can be passed
        // through the REAL pre-processing chain: invoked on the FOCUSED CHILD
        // control it travels the normal parent chain up to the form override
        // (verified offline: a plain probe form sees the child-routed call),
        // and on the form itself for direct guard checks. No SendKeys, no
        // global hotkey and no synthetic OS input is used anywhere; real
        // focus is established with plain Control.Focus() on shown windows.
        private static readonly System.Reflection.MethodInfo ProcessCmdKeyMethod =
            typeof(System.Windows.Forms.Control).GetMethod("ProcessCmdKey",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);

        private static bool DispatchCmdKey(System.Windows.Forms.Control target,
            System.Windows.Forms.Keys keyData)
        {
            System.Windows.Forms.Message msg = System.Windows.Forms.Message.Create(
                target.Handle, 0x0100, IntPtr.Zero, IntPtr.Zero);
            object[] args = new object[] { msg, keyData };
            return (bool)ProcessCmdKeyMethod.Invoke(target, args);
        }

        // UX022: the window hugs the cards — flush left edge, no horizontal
        // overflow, gaps only BETWEEN cards, and (without scrolling) the last
        // card's bottom exactly equals the client bottom.
        private static void CheckCardLayout(PopupForm form, string tag)
        {
            System.Windows.Forms.FlowLayoutPanel content = form.ContentForTest;
            Check(tag + ".cardsPresent", content.Controls.Count > 0, true);
            if (content.Controls.Count == 0) return;
            Check(tag + ".noHorizScroll", content.HorizontalScroll.Visible, false);
            int firstWidth = content.Controls[0].Width;
            int lastBottom = 0;
            for (int i = 0; i < content.Controls.Count; i++)
            {
                System.Windows.Forms.Control card = content.Controls[i];
                Check(tag + ".flushLeft", card.Left, 0);
                Check(tag + ".sameWidth", card.Width, firstWidth);
                Check(tag + ".insideWidth", card.Right <= content.ClientSize.Width, true);
                Check(tag + ".widthPositive", card.Width > 0, true);
                if (i < content.Controls.Count - 1)
                    Check(tag + ".gapBetween", card.Margin.Bottom > 0, true);
                else
                {
                    Check(tag + ".noTailGap", card.Margin.Bottom, 0);
                    lastBottom = card.Bottom;
                }
            }
            bool scrolling = content.AutoScroll && content.VerticalScroll.Visible;
            if (!scrolling)
                Check(tag + ".lastHugsBottom", lastBottom, content.ClientSize.Height);
        }

        private static PanelView BuildLargeView()
        {
            PanelModel model = new PanelModel();
            model.OnAuthResult(true, PopupForm.SampleIdentity(), QuotaStatus.Ok, null);
            return model.OnUsageResult(SyntheticSample.BuildLarge(), null, ScopeVerdict.Same, null);
        }

        private static PanelView BuildSingleView()
        {
            PanelModel model = new PanelModel();
            model.OnAuthResult(true, PopupForm.SampleIdentity(), QuotaStatus.Ok, null);
            return model.OnUsageResult(PercentSnapshot(10), null, ScopeVerdict.Same, null);
        }

        // T-057: a real PanelModel-produced view for a given owner, with the
        // same friendly identity descriptor regardless of owner_trn. Used to
        // prove the scope fingerprint - not the hint - drives menu invalidation.
        private static PanelView OwnerScopeView(string ownerTrn, QuotaSnapshot snap)
        {
            PanelModel model = new PanelModel();
            AuthIdentity id = PopupForm.SampleIdentity();
            id.OwnerTrn = ownerTrn;
            model.OnAuthResult(true, id, QuotaStatus.Ok, null);
            return model.OnUsageResult(snap, null, ScopeVerdict.Same, null);
        }

        private static void Check(string name, object actual, object expected)
        {
            bool ok = Equals(actual, expected)
                || (actual != null && expected != null
                    && actual.GetType() != expected.GetType()
                    && Convert.ToString(actual) == Convert.ToString(expected));
            if (ok) { _passed++; }
            else
            {
                _failed++;
                _failures.Add(name + ": expected [" + expected + "] got [" + actual + "]");
            }
        }

        private static void CheckClose(string name, double actual, double expected)
        {
            if (Math.Abs(actual - expected) < 0.0001) { _passed++; }
            else
            {
                _failed++;
                _failures.Add(name + ": expected " + expected + " got " + actual);
            }
        }
    }
}
