using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using ArkLeft;

namespace ArkLeft.Tests
{
    internal static partial class QuotaTests
    {
        private static void CliAuthGate()
        {
            // Success requires auth logged_in=true. Test transport flows through
            // the SAME gate as the real path.
            CliMode ok = new CliMode();
            ok.AuthJson = "{\"logged_in\":true}";
            ok.UsageJson = Wrap(Item("agent-plan", null, null, true, Period("5h", "10", null, null, null)));
            CliResultTransport("cli.ok", new QuotaCli(ok), QuotaStatus.Ok);

            // logged_in=false -> NotLoggedIn
            CliMode no = new CliMode();
            no.AuthJson = "{\"logged_in\":false}";
            no.UsageJson = "{\"items\":[]}";
            CliResultTransport("cli.notLogged", new QuotaCli(no), QuotaStatus.NotLoggedIn);

            // missing logged_in -> FormatError (not usage)
            CliMode missing = new CliMode();
            missing.AuthJson = "{\"auth_method\":\"sso\"}";
            missing.UsageJson = Wrap(Item("agent-plan", null, null, true, Period("5h", "10", null, null, null)));
            CliResultTransport("cli.missingBool", new QuotaCli(missing), QuotaStatus.FormatError);

            // auth non-zero exit -> Failed (must NOT proceed to usage)
            CliMode authFail = new CliMode();
            authFail.AuthExitCode = 3;
            authFail.UsageJson = Wrap(Item("agent-plan", null, null, true, Period("5h", "10", null, null, null)));
            CliResultTransport("cli.authFail", new QuotaCli(authFail), QuotaStatus.Failed);
        }

        private static void CliUsageExitCode()
        {
            // usage non-zero exit must not be masked as Ok even with JSON on stdout.
            CliMode usageFail = new CliMode();
            usageFail.AuthJson = "{\"logged_in\":true}";
            usageFail.UsageJson = Wrap(Item("agent-plan", null, null, true, Period("5h", "10", null, null, null)));
            usageFail.UsageExitCode = 1;
            CliResultTransport("cli.usageFail", new QuotaCli(usageFail), QuotaStatus.Failed);

            // auth timeout -> Timeout
            CliMode to = new CliMode();
            to.AuthExitCode = 124;
            CliResultTransport("cli.timeout", new QuotaCli(to), QuotaStatus.Timeout);

            // auth cancelled -> Cancelled
            CliMode cancelled = new CliMode();
            cancelled.AuthJson = "{\"logged_in\":true}";
            cancelled.DelayMs = 5000;
            System.Threading.CancellationTokenSource cts = new System.Threading.CancellationTokenSource();
            cts.Cancel();
            CliResultTransportToken("cli.cancelled", new QuotaCli(cancelled), QuotaStatus.Cancelled, cts.Token);

            // disposed -> Failed, and no new process may start
            CliMode disposed = new CliMode();
            disposed.AuthJson = "{\"logged_in\":true}";
            disposed.UsageJson = "{\"items\":[]}";
            QuotaCli dc = new QuotaCli(disposed);
            dc.Dispose();
            CliResultTransport("cli.disposed", dc, QuotaStatus.Failed);
            Check("cli.disposedNoStart", TryStartAfterDispose(dc), true);
        }

        private static void CliTimeoutCancelDispose()
        {
            // Test-mode timeout for usage (auth ok).
            CliMode u = new CliMode();
            u.AuthJson = "{\"logged_in\":true}";
            u.UsageExitCode = 124;
            CliResultTransport("cli.usageTimeout", new QuotaCli(u), QuotaStatus.Timeout);
        }

    }
}
