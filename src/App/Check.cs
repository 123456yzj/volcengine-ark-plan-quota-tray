using System;
using System.Text;
using System.Threading;
using ArkLeft;

namespace ArkLeft.Check
{
    // Console diagnostic: performs the real read-only query and prints a
    // SANITIZED summary (product / period / remaining state) to stdout.
    // Never prints the viewer object, account IDs, API keys or any credential.
    internal static class Program
    {
        private static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            CliMode mode = null;
            if (args.Length == 1 && args[0] == "--runtime-download") return VerifyRuntimeDownload();
            if (args.Length == 1 && args[0] == "--runtime-release")
            {
                try
                {
                    string architecture = ArkCliRuntimeConfig.Architecture();
                    ArkCliRelease release = new ArkCliReleaseClient().LatestAsync(architecture, CancellationToken.None).GetAwaiter().GetResult();
                    Console.WriteLine("release_version=" + release.Version);
                    Console.WriteLine("release_architecture=" + architecture);
                    Console.WriteLine("release_digest=" + (release.Digest ?? "unavailable"));
                    return 0;
                }
                catch (Exception) { Console.WriteLine("status=RuntimeUpdateUnavailable"); return 3; }
            }
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--help", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("ark_left check: 真实查询并输出脱敏摘要。");
                    Console.WriteLine("用法: ark_left-check.exe");
                    return 0;
                }
            }

            QuotaCli cli = new QuotaCli(mode);
            try { cli.RuntimeManager.EnsureRuntimeAsync(CancellationToken.None).GetAwaiter().GetResult(); }
            catch (Exception) { }
            CliResolveResult res = QuotaCli.Resolve(cli.RuntimeManager);
            Console.WriteLine("cli_found=" + (res.IsUsable ? "true" : "false"));
            if (!res.IsUsable)
            {
                if (res.Found && res.Message != null) Console.WriteLine("cli_note=" + res.Message);
                Console.WriteLine("status=" + res.Error);
                cli.Dispose();
                return 3;
            }

            QuotaSnapshot snap;
            ScopeVerdict verdict;
            try
            {
                // Detailed entry point so the sanitized summary can also report
                // the auth-scope vs usage-viewer verdict. Only the verdict label
                // is printed: never the fingerprint, viewer object or any ID.
                QueryOutcome outcome = cli.QueryDetailedAsync(null, CancellationToken.None).Result;
                snap = outcome.Snapshot;
                verdict = outcome.Verdict;
            }
            catch (Exception)
            {
                Console.WriteLine("status=Failed");
                return 3;
            }
            finally
            {
                cli.Dispose();
            }

            // Same / Unknown / Mismatch only; no fingerprint, viewer or IDs.
            Console.WriteLine("scope_verdict=" + VerdictName(verdict));
            Console.WriteLine("status=" + snap.Status);
            Console.WriteLine("fetched=" + DisplayNames.FormatFetched(snap.FetchedAt));
            if (snap.Products != null)
            {
                for (int i = 0; i < snap.Products.Count; i++)
                {
                    ProductQuota pq = snap.Products[i];
                    Console.WriteLine("product=" + pq.DisplayName
                        + " code=" + (pq.Product ?? "?")
                        + " state=" + ProductState(pq));
                    if (pq.Error != null)
                        Console.WriteLine("  error=" + pq.Error);
                    for (int j = 0; j < pq.Periods.Count; j++)
                    {
                        PeriodQuota p = pq.Periods[j];
                        EffectivePeriodQuota effective = QuotaDisplay.Effective(pq, p);
                        Console.WriteLine("  period=" + p.LabelDisplay
                             + " remaining=" + Remaining(pq, p)
                             + " source=" + Source(pq, p)
                            + " amount=" + (effective.AmountKnown ? DisplayNames.Number(effective.RemainingAmount) : "unknown")
                            + " reset=" + (p.HasReset ? DisplayNames.FormatTime(p.ResetLocal) : "none")
                            + (p.Clamped ? " clamped=true" : ""));
                    }
                }
            }

            // A scope Mismatch is a hard diagnostic failure: the returned data
            // does not belong to the auth identity. Surface it via a non-zero
            // exit even when the quota payload itself parsed.
            if (verdict == ScopeVerdict.Mismatch) return 4;

            if (snap.Status == QuotaStatus.Ok) return 0;
            if (snap.Status == QuotaStatus.NoSubscription) return 0;
            if (snap.Status == QuotaStatus.PartialError) return 1;
            if (snap.Status == QuotaStatus.NotLoggedIn) return 2;
            return 3;
        }

        private static string VerdictName(ScopeVerdict v)
        {
            switch (v)
            {
                case ScopeVerdict.Same: return "Same";
                case ScopeVerdict.Mismatch: return "Mismatch";
                default: return "Unknown";
            }
        }

        // Standalone release/download/signature smoke diagnostic; no auth commands.
        private static int VerifyRuntimeDownload()
        {
            using (CancellationTokenSource timeout = new CancellationTokenSource(330000))
            using (ArkCliInvoker invoker = new ArkCliInvoker())
            {
                try
                {
                    Console.WriteLine("phase=release");
                    string architecture = ArkCliRuntimeConfig.Architecture();
                    ArkCliRelease release = new ArkCliReleaseClient().LatestAsync(architecture, timeout.Token).GetAwaiter().GetResult();
                    string dir = new ArkCliRuntimeStore(ArkCliRuntimeStore.DefaultRoot()).DownloadsDirectory;
                    System.IO.Directory.CreateDirectory(dir);
                    string part = System.IO.Path.Combine(dir, "verified-" + Guid.NewGuid().ToString("N") + ".exe.part");
                    Console.WriteLine("phase=download");
                    new ArkCliDownloader().DownloadAsync(release, part, timeout.Token).GetAwaiter().GetResult();
                    Console.WriteLine("phase=verify");
                    new ArkCliBinaryVerifier(invoker).VerifyAsync(part, release.Version, release.Digest, timeout.Token).GetAwaiter().GetResult();
                    Console.WriteLine("download_verified=true version=" + release.Version + " sha256=matched signature=trusted smoke=passed");
                    return 0;
                }
                catch (OperationCanceledException) { Console.WriteLine("status=Cancelled deadline=330s"); return 3; }
                catch (ArkCliRuntimeException e) { Console.WriteLine("status=" + e.Category); return 3; }
                catch (Exception) { Console.WriteLine("status=RuntimeUpdateUnavailable"); return 3; }
            }
        }

        private static string ProductState(ProductQuota pq)
        {
            if (pq.Error != null) return "error";
            if (pq.Malformed) return "malformed";
            if (pq.SubscribedKnown) return pq.Subscribed ? "subscribed" : "unsubscribed";
            return "unknown";
        }

        private static string Remaining(ProductQuota product, PeriodQuota p)
        {
            if (p.Error != null) return "error";
            EffectivePeriodQuota effective = QuotaDisplay.Effective(product, p);
            if (!effective.PercentKnown) return "unknown";
            // Same rendering as the UI: true 0 => "已用尽", 0<v<1 => "<1%",
            // otherwise at most one decimal. Avoids the old "0%" that hid a
            // tiny-but-nonzero remaining amount.
            return PercentFormat.Remaining(effective.RemainingPercent);
        }

        private static string Source(ProductQuota product, PeriodQuota p)
        {
            if (p.Error != null) return "error";
            EffectivePeriodQuota effective = QuotaDisplay.Effective(product, p);
            if (!effective.PercentKnown) return "unknown";
            return p.Clamped ? "clamped" : "ok";
        }
    }
}
