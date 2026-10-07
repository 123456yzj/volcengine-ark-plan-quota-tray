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
        private static void ViewEmptyProductsCases()
        {
            // NoSubscription with empty items must still carry a message to show.
            QuotaSnapshot s = QuotaParser.Parse("{\"items\":[]}");
            Check("view.noSubMessage", !string.IsNullOrEmpty(s.Message), true);
            Check("view.noSubStatus", s.Status, QuotaStatus.NoSubscription);
        }

        private static void IdentityParseCases()
        {
            bool loggedIn;
            AuthIdentity id;
            bool ok = IdentityParse.TryParseAuth(
                "{\"logged_in\":true,\"active_profile\":{\"name\":\"default\",\"type\":\"volc-sso\","
                + "\"owner_trn\":\"trn:iam::123:user/u\",\"region\":\"cn-beijing\",\"project\":\"proj-1\"}}",
                out loggedIn, out id);
            Check("authParse.ok", ok, true);
            Check("authParse.loggedIn", loggedIn, true);
            Check("authParse.name", id != null ? id.Name : null, "default");
            Check("authParse.type", id != null ? id.Type : null, "volc-sso");

            // Missing logged_in -> false (format error upstream).
            Check("authParse.missing", IdentityParse.TryParseAuth("{\"auth_method\":\"sso\"}",
                out loggedIn, out id), false);

            // A valid synthetic TRN parses through to a known scope.
            AuthIdentity trn = AuthId("trn:iam::123456789:user/alice", "d", "r", "p");
            Check("authParse.scopeKnown", QueryScope.FromAuth(trn).IsKnown, true);

            // viewer.profile is a STRING, not nested.
            System.Web.Script.Serialization.JavaScriptSerializer ser =
                new System.Web.Script.Serialization.JavaScriptSerializer();
            System.Collections.Generic.Dictionary<string, object> root =
                ser.DeserializeObject("{\"viewer\":{\"account_id\":\"123456789\",\"user_id\":\"alice\","
                + "\"profile\":\"default\",\"tenant\":\"t\",\"region\":\"cn-bj\","
                + "\"project_name\":\"proj-1\",\"is_root\":false},\"items\":[]}")
                as System.Collections.Generic.Dictionary<string, object>;
            ViewerIdentity v = IdentityParse.ParseViewer(root);
            Check("viewer.profileString", v.Profile, "default");
            Check("viewer.accountId", v.AccountId, "123456789");
            Check("viewer.userId", v.UserId, "alice");
            Check("viewer.region", v.Region, "cn-bj");
            Check("viewer.projectName", v.ProjectName, "proj-1");
            Check("viewer.isRoot", v.IsRootKnown && v.IsRoot == false, true);
        }

        private static void IdentityDisplayCases()
        {
            AuthIdentity a = new AuthIdentity();
            a.Present = true;
            a.Type = "agent-plan";
            a.Region = "cn-beijing";
            a.Name = "default";
            a.OwnerTrn = "trn:iam::SECRETACCOUNT:user/SECRETUSER";
            string d = IdentityDisplay.Describe(a);
            Check("display.hasType", d.IndexOf("Agent Plan", StringComparison.Ordinal) >= 0, true);
            Check("display.hasRegion", d.IndexOf("cn-beijing", StringComparison.Ordinal) >= 0, true);
            Check("display.noOwnerTrn", d.IndexOf("SECRETACCOUNT", StringComparison.Ordinal) < 0, true);
            Check("display.noUserName", d.IndexOf("SECRETUSER", StringComparison.Ordinal) < 0, true);

            // Type mapping is product/platform, not the auth method string.
            Check("display.mapAgent", IdentityDisplay.FriendlyType("agent-plan"), "Agent Plan");
            Check("display.mapCoding", IdentityDisplay.FriendlyType("coding-plan"), "Coding Plan");
            Check("display.mapTeam", IdentityDisplay.FriendlyType("team"), "团队");
            Check("display.mapPlatform", IdentityDisplay.FriendlyType("platform"), "平台");
            Check("display.mapSsoNotSpecial", IdentityDisplay.FriendlyType("volc-sso"), "volc-sso");

            // Long custom profile names are length-limited.
            string limited = IdentityDisplay.LimitProfileName(new string('x', 80));
            Check("display.limitedLen", limited.Length <= IdentityDisplay.MaxProfileNameChars + 1, true);
        }

        private static void IpcSignalCases()
        {
            // Uses an isolated event name, never the real singleton name.
            string name = "Local\\ark_left_test_" + Guid.NewGuid().ToString("N");
            bool got = false;
            using (Ipc.Register(name, delegate { got = true; }))
            {
                bool signalled = Ipc.Signal(name, 20, 50);
                Check("ipc.signalled", signalled, true);
                for (int i = 0; i < 40 && !got; i++) System.Threading.Thread.Sleep(50);
            }
            Check("ipc.callbackFired", got, true);

            // Signalling a non-existent event must fail after bounded retries.
            bool none = Ipc.Signal("Local\\ark_left_test_missing_" + Guid.NewGuid().ToString("N"), 2, 1);
            Check("ipc.missingNoBlock", none, false);
        }

        private static void MarkerIsolationCases()
        {
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "ark_left_test_" + Guid.NewGuid().ToString("N"));
            string old = Environment.GetEnvironmentVariable(Marker.StateDirEnv);
            try
            {
                Environment.SetEnvironmentVariable(Marker.StateDirEnv, dir);
                Check("marker.isolatedPath", Marker.StateDir(), dir);
                Check("marker.notYet", Marker.Exists(), false);
                Marker.WriteFirstRun();
                Check("marker.after", Marker.Exists(), true);
                string content = System.IO.File.ReadAllText(Marker.MarkerPath());
                Check("marker.noSecret", content.IndexOf("SECRET", StringComparison.Ordinal) < 0, true);
                Check("marker.noQuota", content.IndexOf("percent", StringComparison.OrdinalIgnoreCase) < 0, true);
            }
            finally
            {
                Environment.SetEnvironmentVariable(Marker.StateDirEnv, old);
                try { if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, true); }
                catch (Exception) { }
            }
        }

        private static void ProgressStageCases()
        {
            // Auth-confirmed progress carries the identity; usage/done stages fire.
            CliMode mode = new CliMode();
            mode.AuthJson = "{\"logged_in\":true,\"active_profile\":{\"name\":\"d\",\"type\":\"agent-plan\","
                + "\"owner_trn\":\"trn:iam::123456789:user/alice\",\"region\":\"cn-bj\",\"project\":\"p1\"}}";
            mode.UsageJson = Wrap(Item("agent-plan", null, null, true,
                Period("5h", "25", null, null, null)));
            QuotaCli cli = new QuotaCli(mode);
            ProgressCollector pc = new ProgressCollector();
            QueryOutcome o = cli.QueryDetailedAsync(pc, System.Threading.CancellationToken.None).Result;
            Check("progress.authConfirmed", o.AuthConfirmed, true);
            Check("progress.scopeKnown", o.AuthScope != null && o.AuthScope.IsKnown, true);
            Check("progress.identityName", o.Identity != null ? o.Identity.Name : null, "d");
            Check("progress.hasAuthIdentity", pc.SawAuthIdentity, true);
            Check("progress.hasAuthConfirmedFlag", pc.SawAuthConfirmed, true);
            Check("progress.sawUsage", pc.SawUsage, true);
            Check("progress.sawDone", pc.SawDone, true);
            Check("progress.status", o.Snapshot.Status, QuotaStatus.Ok);

            // Logged in but active_profile missing: AuthConfirmed must still fire
            // (identity null) so the UI can clear the old cache.
            CliMode noProfile = new CliMode();
            noProfile.AuthJson = "{\"logged_in\":true}";
            noProfile.UsageJson = "{\"items\":[]}";
            ProgressCollector pc2 = new ProgressCollector();
            QueryOutcome o2 = new QuotaCli(noProfile).QueryDetailedAsync(pc2,
                System.Threading.CancellationToken.None).Result;
            Check("progress.noProfileAuthConfirmed", o2.AuthConfirmed, true);
            Check("progress.noProfileNullIdentity", o2.Identity == null, true);
            Check("progress.noProfileFlagFired", pc2.SawAuthConfirmed, true);
            Check("progress.noProfileScopeUnknown", !(o2.AuthScope != null && o2.AuthScope.IsKnown), true);

            // Cancelled auth -> snapshot Cancelled, no auth confirm.
            CliMode c = new CliMode();
            c.AuthJson = "{\"logged_in\":true}";
            c.DelayMs = 3000;
            System.Threading.CancellationTokenSource cts =
                new System.Threading.CancellationTokenSource();
            cts.Cancel();
            QueryOutcome co = new QuotaCli(c).QueryDetailedAsync(null, cts.Token).Result;
            Check("progress.cancelled", co.Snapshot.Status, QuotaStatus.Cancelled);
            Check("progress.cancelledNotConfirmed", co.AuthConfirmed, false);
        }

    }
}
