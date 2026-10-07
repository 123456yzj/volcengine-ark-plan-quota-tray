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
        private static void ScopeFingerprintCases()
        {
            AuthIdentity a = AuthId("trn:iam::123456789:user/alice", "default", "cn-beijing", "proj-1");
            QueryScope s1 = QueryScope.FromAuth(a);
            QueryScope s2 = QueryScope.FromAuth(a);
            Check("scope.known", s1.IsKnown, true);
            Check("scope.sameFingerprint", s1.Matches(s2), true);
            Check("scope.account", s1.Account, "123456789");
            Check("scope.userId", s1.UserId, "alice");
            Check("scope.isRoot", s1.IsRoot, false);

            AuthIdentity root = AuthId("trn:iam::123456789:root", "default", "cn-beijing", "proj-1");
            Check("scope.rootKnown", QueryScope.FromAuth(root).IsKnown, true);
            Check("scope.rootFlag", QueryScope.FromAuth(root).IsRoot, true);

            AuthIdentity b = AuthId("trn:iam::123456789:user/alice", "default", "cn-beijing", "proj-2");
            Check("scope.diffProjectMismatch", s1.Matches(QueryScope.FromAuth(b)), false);

            AuthIdentity missing = AuthId(null, "default", "cn-beijing", "proj-1");
            Check("scope.missingOwnerUnknown", QueryScope.FromAuth(missing).IsKnown, false);

            // Unsupported TRN shapes are unknown, never "same".
            Check("scope.badTrnShape",
                QueryScope.FromAuth(AuthId("owner-A", "d", "r", "p")).IsKnown, false);
            Check("scope.badTrn5parts",
                QueryScope.FromAuth(AuthId("trn:iam::acct:role/x", "d", "r", "p")).IsKnown, false);

            // parts[2] must be the empty region slot: trn:iam::<account>:...
            Check("scope.nonEmptyRegionSlotUnknown",
                QueryScope.FromAuth(AuthId("trn:iam:cn-bj:123:user/alice", "d", "r", "p")).IsKnown,
                false);
            // A user/ principal without an id is not a valid identity.
            Check("scope.emptyUserIdUnknown",
                QueryScope.FromAuth(AuthId("trn:iam::123:user/", "d", "r", "p")).IsKnown, false);

            Check("scope.nullUnknown", QueryScope.FromAuth(null).IsKnown, false);
        }

        private static void ScopeValidationCases()
        {
            QueryScope scope = QueryScope.FromAuth(
                AuthId("trn:iam::123456789:user/alice", "default", "cn-bj", "p1"));

            ViewerIdentity match = FullViewer("123456789", "alice", "default", "cn-bj", "p1", false);
            Check("scopeValid.same", ScopeValidation.Validate(scope, match), ScopeVerdict.Same);

            // Strict equality, not substring: account 12 must NOT match 123456789.
            ViewerIdentity shortAcct = FullViewer("12", "alice", "default", "cn-bj", "p1", false);
            Check("scopeValid.acctNotSubstring",
                ScopeValidation.Validate(scope, shortAcct), ScopeVerdict.Mismatch);

            // Region / project conflicts must be Mismatch even when account+profile agree.
            Check("scopeValid.regionMismatch",
                ScopeValidation.Validate(scope,
                    FullViewer("123456789", "alice", "default", "cn-sh", "p1", false)),
                ScopeVerdict.Mismatch);
            Check("scopeValid.projectMismatch",
                ScopeValidation.Validate(scope,
                    FullViewer("123456789", "alice", "default", "cn-bj", "p2", false)),
                ScopeVerdict.Mismatch);
            Check("scopeValid.userMismatch",
                ScopeValidation.Validate(scope,
                    FullViewer("123456789", "bob", "default", "cn-bj", "p1", false)),
                ScopeVerdict.Mismatch);
            Check("scopeValid.profileMismatch",
                ScopeValidation.Validate(scope,
                    FullViewer("123456789", "alice", "other", "cn-bj", "p1", false)),
                ScopeVerdict.Mismatch);

            // Root auth vs is_root=false must be Mismatch.
            QueryScope rootScope = QueryScope.FromAuth(
                AuthId("trn:iam::123456789:root", "default", "cn-bj", "p1"));
            Check("scopeValid.rootMismatch",
                ScopeValidation.Validate(rootScope,
                    FullViewer("123456789", "alice", "default", "cn-bj", "p1", false)),
                ScopeVerdict.Mismatch);
            Check("scopeValid.rootSame",
                ScopeValidation.Validate(rootScope,
                    FullViewer("123456789", null, "default", "cn-bj", "p1", true)),
                ScopeVerdict.Same);

            // Sub-user auth vs a viewer that reports root is a principal
            // conflict, even without any user_id.
            ViewerIdentity rootViewer = FullViewer("123456789", null, "default", "cn-bj", "p1", true);
            Check("scopeValid.subUserVsRootMismatch",
                ScopeValidation.Validate(scope, rootViewer), ScopeVerdict.Mismatch);
            // Sub-user auth with is_root explicitly false but a differing user_id
            // must still be Mismatch.
            Check("scopeValid.subUserVsOtherUserMismatch",
                ScopeValidation.Validate(scope,
                    FullViewer("123456789", "bob", "default", "cn-bj", "p1", false)),
                ScopeVerdict.Mismatch);
            // Missing is_root stays Unknown (never Same) for a sub-user.
            ViewerIdentity noRootSub = FullViewer("123456789", "alice", "default", "cn-bj", "p1", false);
            noRootSub.IsRootKnown = false;
            Check("scopeValid.subUserMissingRootUnknown",
                ScopeValidation.Validate(scope, noRootSub), ScopeVerdict.Unknown);

            // Empty / whitespace defining fields are MISSING (Unknown), never a
            // conflict. Each of these would previously have been Mismatch.
            Check("scopeValid.whitespaceAccountUnknown",
                ScopeValidation.Validate(scope,
                    FullViewer("   ", "alice", "default", "cn-bj", "p1", false)),
                ScopeVerdict.Unknown);
            Check("scopeValid.emptyAccountUnknown",
                ScopeValidation.Validate(scope,
                    FullViewer("", "alice", "default", "cn-bj", "p1", false)),
                ScopeVerdict.Unknown);
            Check("scopeValid.whitespaceProfileUnknown",
                ScopeValidation.Validate(scope,
                    FullViewer("123456789", "alice", "  ", "cn-bj", "p1", false)),
                ScopeVerdict.Unknown);
            Check("scopeValid.whitespaceRegionUnknown",
                ScopeValidation.Validate(scope,
                    FullViewer("123456789", "alice", "default", "\t", "p1", false)),
                ScopeVerdict.Unknown);
            Check("scopeValid.whitespaceProjectUnknown",
                ScopeValidation.Validate(scope,
                    FullViewer("123456789", "alice", "default", "cn-bj", " ", false)),
                ScopeVerdict.Unknown);
            Check("scopeValid.whitespaceUserUnknown",
                ScopeValidation.Validate(scope,
                    FullViewer("123456789", "  ", "default", "cn-bj", "p1", false)),
                ScopeVerdict.Unknown);
            // Root auth + whitespace account is Unknown, not a bogus conflict.
            Check("scopeValid.rootWhitespaceAccountUnknown",
                ScopeValidation.Validate(rootScope,
                    FullViewer("  ", null, "default", "cn-bj", "p1", true)),
                ScopeVerdict.Unknown);
            // A non-blank but different value must still be a strict Mismatch.
            Check("scopeValid.blankPaddedAccountMismatch",
                ScopeValidation.Validate(scope,
                    FullViewer(" 123456789 ", "alice", "default", "cn-bj", "p1", false)),
                ScopeVerdict.Mismatch);

            // Missing a defining viewer field => Unknown, never Same.
            ViewerIdentity noRegion = FullViewer("123456789", "alice", "default", null, "p1", false);
            Check("scopeValid.missingRegionUnknown",
                ScopeValidation.Validate(scope, noRegion), ScopeVerdict.Unknown);
            ViewerIdentity noRoot = FullViewer("123456789", "alice", "default", "cn-bj", "p1", false);
            noRoot.IsRootKnown = false;
            Check("scopeValid.missingIsRootUnknown",
                ScopeValidation.Validate(scope, noRoot), ScopeVerdict.Unknown);

            // Same profile but missing account must NOT be Same.
            ViewerIdentity noAccount = FullViewer(null, "alice", "default", "cn-bj", "p1", false);
            Check("scopeValid.noAccountNotSame",
                ScopeValidation.Validate(scope, noAccount), ScopeVerdict.Unknown);

            ViewerIdentity bare = new ViewerIdentity();
            bare.Present = true;
            Check("scopeValid.bareUnknown", ScopeValidation.Validate(scope, bare), ScopeVerdict.Unknown);

            Check("scopeValid.unknownScope",
                ScopeValidation.Validate(QueryScope.Unknown(), match), ScopeVerdict.Unknown);
        }

        private static void PanelModelIdentityCases()
        {
            PanelModel m = new PanelModel();
            AuthIdentity a = AuthId("trn:iam::123456789:user/alice", "default", "cn-beijing", "proj-1");

            // First auth: no prior data, then usage confirms and caches.
            m.BeginQuery();
            m.OnAuthResult(true, a, QuotaStatus.Ok, null);
            QuotaSnapshot snap = QuotaParser.Parse(Wrap(
                Item("agent-plan", null, null, true, Period("5h", "25", null, null, null))));
            PanelView v = m.OnUsageResult(snap, FullViewer("123456789", "alice", "default",
                "cn-beijing", "proj-1", false), ScopeVerdict.Same, null);
            Check("panel.cached", m.Last != null, true);
            Check("panel.showing", v.State, PanelState.ShowingCurrent);

            // UX011: BeginQuery retains the exact existing view while waiting.
            PanelView bq = m.BeginQuery();
            Check("panel.beginQueryKeepsData", bq.Data == snap, true);
            Check("panel.beginQueryState", bq.State, PanelState.ShowingCurrent);

            // Cancellation alone does not prove an identity change.
            PanelView c = m.OnCancelled(null);
            Check("panel.beginCancelKeepsData", c.Data == snap, true);
            Check("panel.beginCancelRetained", m.Last == snap, true);

            // Rebuild a cache for the same-scope continue test.
            m.BeginQuery();
            m.OnAuthResult(true, a, QuotaStatus.Ok, null);
            PanelView v2 = m.OnUsageResult(snap, FullViewer("123456789", "alice", "default",
                "cn-beijing", "proj-1", false), ScopeVerdict.Same, null);
            Check("panel.recached", m.Last != null, true);

            // Same identity keeps history without a process state.
            m.BeginQuery();
            PanelView same = m.OnAuthResult(true, a, QuotaStatus.Ok, null);
            Check("panel.sameRestore", same.Data != null, true);
            Check("panel.sameNoProcessState", same.State, PanelState.ShowingCurrent);

            // Unknown identity cannot prove a new owner: retain history.
            m.BeginQuery();
            PanelView unk = m.OnAuthResult(true, AuthId(null, null, null, null), QuotaStatus.Ok, null);
            Check("panel.unknownKeepsHistory", unk.Data == snap, true);
            Check("panel.unknownRetained", m.Last == snap, true);
        }

        private static void PanelModelStaleCases()
        {
            PanelModel m = new PanelModel();
            AuthIdentity a = AuthId("trn:iam::123456789:user/alice", "d", "r", "p");
            QuotaSnapshot snap = QuotaParser.Parse(Wrap(
                Item("agent-plan", null, null, true, Period("5h", "25", null, null, null))));
            m.OnAuthResult(true, a, QuotaStatus.Ok, null);
            m.OnUsageResult(snap, FullViewer("123456789", "alice", "d", "r", "p", false),
                ScopeVerdict.Same, null);

            // Same A: begin -> auth same -> usage FAILURE keeps last data.
            m.BeginQuery();
            m.OnAuthResult(true, a, QuotaStatus.Ok, null);
            PanelView stale = m.OnUsageFailure(QuotaStatus.Failed, "boom", null);
            Check("panel.staleKeepsData", stale.Data != null, true);
            Check("panel.staleState", stale.State, PanelState.StaleError);

            // Re-cache, then auth failure (loggedIn=false) clears everything.
            m.BeginQuery();
            m.OnAuthResult(true, a, QuotaStatus.Ok, null);
            m.OnUsageResult(snap, FullViewer("123456789", "alice", "d", "r", "p", false),
                ScopeVerdict.Same, null);
            PanelView err = m.OnAuthResult(false, null, QuotaStatus.NotLoggedIn, "未登录");
            Check("panel.authFailClears", m.Last == null, true);
            Check("panel.authFailCopy", err.AllowCopyLogin, true);
            Check("panel.authFailRetry", err.AllowRetry, true);
            Check("panel.authFailGuide", err.AllowOpenGuide, true);
        }

        private static void PanelModelUnknownVerdictCases()
        {
            // Unknown viewer verdict on a successful usage must NOT be cached.
            PanelModel m = new PanelModel();
            AuthIdentity a = AuthId("trn:iam::123456789:user/alice", "d", "r", "p");
            QuotaSnapshot snap = QuotaParser.Parse(Wrap(
                Item("agent-plan", null, null, true, Period("5h", "25", null, null, null))));
            m.OnAuthResult(true, a, QuotaStatus.Ok, null);
            PanelView v = m.OnUsageResult(snap, null, ScopeVerdict.Unknown, null);
            Check("panel.unknownVerdictShows", v.Data != null, true);
            Check("panel.unknownVerdictNotCached", m.Last == null, true);
            Check("panel.unknownVerdictState", v.State, PanelState.ShowingCurrent);

            // And a later failure cannot resurrect it as stale.
            PanelView f = m.OnUsageFailure(QuotaStatus.Failed, "x", null);
            Check("panel.unknownThenFailNoStale", f.Data == null, true);
        }

        private static void PanelModelNullIdentityCases()
        {
            // Logged in but active_profile missing: auth-confirmed with null identity.
            // UX011: unknown auth retains history and cannot persist a new result.
            PanelModel m = new PanelModel();
            AuthIdentity a = AuthId("trn:iam::123456789:user/alice", "d", "r", "p");
            QuotaSnapshot snap = QuotaParser.Parse(Wrap(
                Item("agent-plan", null, null, true, Period("5h", "25", null, null, null))));
            m.OnAuthResult(true, a, QuotaStatus.Ok, null);
            m.OnUsageResult(snap, FullViewer("123456789", "alice", "d", "r", "p", false),
                ScopeVerdict.Same, null);

            m.BeginQuery();
            PanelView auth = m.OnAuthResult(true, null, QuotaStatus.Ok, null);
            Check("panel.nullIdentityData", auth.Data == snap, true);
            PanelView fail = m.OnUsageFailure(QuotaStatus.Failed, "x", null);
            Check("panel.nullIdentityFailKeepsStale", fail.Data == snap, true);
        }

        private static void PanelModelReuseTiedToCurrentQuery()
        {
            // Cache a confirmed same-scope snapshot.
            PanelModel m = new PanelModel();
            AuthIdentity a = AuthId("trn:iam::123456789:user/alice", "d", "r", "p");
            QuotaSnapshot snap = QuotaParser.Parse(Wrap(
                Item("agent-plan", null, null, true, Period("5h", "25", null, null, null))));
            m.OnAuthResult(true, a, QuotaStatus.Ok, null);
            m.OnUsageResult(snap, FullViewer("123456789", "alice", "d", "r", "p", false),
                ScopeVerdict.Same, null);
            Check("panelReuse.cached", m.Last != null, true);
            Check("panelReuse.confirmedScope", m.ConfirmedScope != null, true);

            // BeginQuery clears pending confirmation, while cancellation retains Last.
            m.BeginQuery();
            PanelView c = m.OnCancelled(null);
            Check("panelReuse.cancelBeforeAuthKeepsData", c.Data == snap, true);
            Check("panelReuse.cancelBeforeAuthRetained", m.Last == snap, true);

            // Rebuild cache, then a same-scope auth restores during refresh.
            m.BeginQuery();
            m.OnAuthResult(true, a, QuotaStatus.Ok, null);
            m.OnUsageResult(snap, FullViewer("123456789", "alice", "d", "r", "p", false),
                ScopeVerdict.Same, null);
            m.BeginQuery();
            PanelView same = m.OnAuthResult(true, a, QuotaStatus.Ok, null);
            Check("panelReuse.sameRestores", same.Data != null, true);

            // Now a failure in the same query keeps the restored data.
            PanelView fail = m.OnUsageFailure(QuotaStatus.Failed, "x", null);
            Check("panelReuse.sameFailureKeepsData", fail.Data != null, true);
            Check("panelReuse.sameFailureStale", fail.State, PanelState.StaleError);
        }

        private static void PanelModelAuthHintClearing()
        {
            PanelModel m = new PanelModel();
            AuthIdentity a = AuthId("trn:iam::123456789:user/alice", "d", "r", "p");
            QuotaSnapshot snap = QuotaParser.Parse(Wrap(
                Item("agent-plan", null, null, true, Period("5h", "25", null, null, null))));
            m.OnAuthResult(true, a, QuotaStatus.Ok, null);
            m.OnUsageResult(snap, FullViewer("123456789", "alice", "d", "r", "p", false),
                ScopeVerdict.Same, null);

            // Auth failure clears the stale friendly hint.
            m.BeginQuery();
            PanelView err = m.OnAuthResult(false, null, QuotaStatus.NotLoggedIn, "未登录");
            Check("panelHint.authFailClears", err.IdentityHint, null);
            Check("panelHint.authFailUnknown", err.IdentityUnknown, false);

            // Unknown identity: old hint dropped and "身份未确认" flag is set.
            m.BeginQuery();
            PanelView unk = m.OnAuthResult(true, AuthId(null, null, null, null), QuotaStatus.Ok, null);
            Check("panelHint.unknownClears", unk.IdentityHint, null);
            Check("panelHint.unknownFlag", unk.IdentityUnknown, true);
            Check("panelHint.unknownNoData", unk.Data == null, true);
        }

        private static void PanelStateAfterMismatch()
        {
            PanelModel m = new PanelModel();
            AuthIdentity a = AuthId("trn:iam::123456789:user/alice", "d", "r", "p");
            QuotaSnapshot snap = QuotaParser.Parse(Wrap(
                Item("agent-plan", null, null, true, Period("5h", "25", null, null, null))));
            m.OnAuthResult(true, a, QuotaStatus.Ok, null);
            m.OnUsageResult(snap, FullViewer("123456789", "alice", "d", "r", "p", false),
                ScopeVerdict.Same, null);

            PanelView v = m.OnUsageResult(null, null, ScopeVerdict.Mismatch, null);
            Check("panel.mismatchState", v.State, PanelState.IdentityChanged);
            Check("panel.mismatchClears", m.Last == null, true);
            Check("panel.mismatchRetry", v.AllowRetry, true);
        }

    }
}
