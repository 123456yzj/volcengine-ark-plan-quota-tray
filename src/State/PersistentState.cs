using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace ArkLeft
{
    // Cross-restart snapshot of the LAST SUCCESSFUL display. It stores the full
    // display semantics needed to re-render honestly (subscription known flags,
    // product/period errors, numeric known flags, times) and an irreversible
    // SHA256 identity scope used solely to verify cache ownership. It NEVER
    // stores credentials or raw auth / viewer identifiers.
    //
    // Format version 1 (frozen in docs/contracts.md): file = 12-byte plaintext
    // header (8-byte magic "ARKLFT01" + LE int32 version), then a DPAPI
    // (CurrentUser) protected UTF-8 JSON payload. The header allows an old /
    // foreign format to be rejected WITHOUT invoking DPAPI, and the payload is
    // protected so no plaintext quota data touches disk.
    public class CachedSnapshot
    {
        public int Version;
        public string ScopeFingerprint;   // opaque SHA256, ownership check only
        public string Message;            // fixed safe status text (never upstream)
        public string FetchedAt;          // round-trip "o" local time
        public List<CachedProduct> Products = new List<CachedProduct>();
    }

    public class CachedProduct
    {
        public string Product;
        public string Edition;
        public string Tier;
        public bool Subscribed;           // meaningful only when SubscribedKnown
        public bool SubscribedKnown;
        public bool Malformed;
        public bool PeriodErrorPresent;
        public bool HasError;
        public bool HasUpdated;
        public string UpdatedLocal;
        public List<CachedPeriod> Periods = new List<CachedPeriod>();
    }

    public class CachedPeriod
    {
        public string Label;
        public bool Error;
        public bool PercentKnown;
        public double RemainingPercent;
        public bool Clamped;
        public bool UsedKnown;
        public double Used;
        public bool TotalKnown;
        public double Total;
        public bool AmountKnown;
        public double RemainingAmount;
        public bool HasReset;
        public string ResetLocal;
        public bool HasUpdated;
        public string UpdatedLocal;
        public string UnknownNote;
    }

    // DPAPI box for the cache payload. Uses the framework's ProtectedData
    // (CurrentUser), which wraps CryptProtectData and avoids hand-written
    // P/Invoke. Pure functions; all failures return null.
    internal static class CacheCrypto
    {
        private static readonly byte[] Magic =
            Encoding.ASCII.GetBytes("ARKLFT01"); // length 8
        private const int HeaderBytes = 12;      // 8 magic + 4 version (LE)

        public static byte[] Protect(int version, byte[] plain)
        {
            if (plain == null) return null;
            try
            {
                byte[] prot = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
                if (prot == null) return null;
                byte[] outBytes = new byte[HeaderBytes + prot.Length];
                Array.Copy(Magic, 0, outBytes, 0, Magic.Length);
                outBytes[8] = (byte)version;
                outBytes[9] = (byte)(version >> 8);
                outBytes[10] = (byte)(version >> 16);
                outBytes[11] = (byte)(version >> 24);
                Array.Copy(prot, 0, outBytes, HeaderBytes, prot.Length);
                return outBytes;
            }
            catch (Exception) { return null; }
        }

        public static byte[] Unprotect(int expectedVersion, byte[] boxed)
        {
            if (boxed == null || boxed.Length <= HeaderBytes) return null;
            try
            {
                for (int i = 0; i < Magic.Length; i++)
                    if (boxed[i] != Magic[i]) return null;
                int version = boxed[8] | (boxed[9] << 8) | (boxed[10] << 16) | (boxed[11] << 24);
                if (version != expectedVersion) return null;
                byte[] prot = new byte[boxed.Length - HeaderBytes];
                Array.Copy(boxed, HeaderBytes, prot, 0, prot.Length);
                return ProtectedData.Unprotect(prot, null, DataProtectionScope.CurrentUser);
            }
            catch (Exception) { return null; }
        }
    }

    // Reads / writes the persistent snapshot under Marker.StateDir(). All
    // failures (corrupt file, DPAPI failure, IO error, old version) are ignored
    // by returning null: the app keeps running with an empty state and never
    // fabricates a 0. The atomic write never degrades an existing valid file.
    public static class PersistentStateStore
    {
        public const int FormatVersion = 1;
        public const string FileName = "quota-cache.dat";
        private static readonly object Gate = new object();

        public static string FilePath
        {
            get { return Path.Combine(Marker.StateDir(), FileName); }
        }

        // Builds the complete cache from a successful snapshot. Returns null when
        // there is no known fingerprint or the snapshot is a failure (failures
        // are never persisted). Only Ok / PartialError / NoSubscription qualify.
        public static CachedSnapshot ToCache(QuotaSnapshot snap, string fingerprint)
        {
            if (snap == null || string.IsNullOrEmpty(fingerprint)) return null;
            if (snap.Status != QuotaStatus.Ok && snap.Status != QuotaStatus.PartialError
                && snap.Status != QuotaStatus.NoSubscription) return null;

            CachedSnapshot c = new CachedSnapshot();
            c.Version = FormatVersion;
            c.ScopeFingerprint = fingerprint;
            c.Message = snap.Message;
            c.FetchedAt = snap.FetchedAt.ToString("o", CultureInfo.InvariantCulture);
            if (snap.Products != null)
            {
                for (int i = 0; i < snap.Products.Count; i++)
                {
                    ProductQuota pq = snap.Products[i];
                    if (pq == null) continue;
                    CachedProduct cp = new CachedProduct();
                    cp.Product = pq.Product;
                    cp.Edition = pq.Edition;
                    cp.Tier = pq.Tier;
                    cp.Subscribed = pq.Subscribed;
                    cp.SubscribedKnown = pq.SubscribedKnown;
                    cp.Malformed = pq.Malformed;
                    cp.PeriodErrorPresent = pq.PeriodErrorPresent;
                    cp.HasError = pq.Error != null;
                    cp.HasUpdated = pq.HasUpdated;
                    cp.UpdatedLocal = pq.HasUpdated
                        ? pq.UpdatedLocal.ToString("o", CultureInfo.InvariantCulture) : null;
                    for (int j = 0; j < pq.Periods.Count; j++)
                    {
                        PeriodQuota p = pq.Periods[j];
                        if (p == null) continue;
                        CachedPeriod cpp = new CachedPeriod();
                        cpp.Label = p.Label;
                        cpp.Error = p.Error != null;
                        cpp.PercentKnown = p.PercentKnown;
                        cpp.RemainingPercent = p.RemainingPercent;
                        cpp.Clamped = p.Clamped;
                        cpp.UsedKnown = p.UsedKnown;
                        cpp.Used = p.Used;
                        cpp.TotalKnown = p.TotalKnown;
                        cpp.Total = p.Total;
                        cpp.AmountKnown = p.AmountKnown;
                        cpp.RemainingAmount = p.RemainingAmount;
                        cpp.HasReset = p.HasReset;
                        cpp.ResetLocal = p.HasReset
                            ? p.ResetLocal.ToString("o", CultureInfo.InvariantCulture) : null;
                        cpp.HasUpdated = p.HasUpdated;
                        cpp.UpdatedLocal = p.HasUpdated
                            ? p.UpdatedLocal.ToString("o", CultureInfo.InvariantCulture) : null;
                        cpp.UnknownNote = p.UnknownNote;
                        cp.Periods.Add(cpp);
                    }
                    c.Products.Add(cp);
                }
            }
            return c;
        }

        // Rebuilds a renderable snapshot from the cache. Missing / invalid values
        // stay unknown (never 0). An invalid fetched_at is left at DateTime.MinValue
        // (NEVER DateTime.Now) so a corrupt date cannot pretend to be fresh. Never
        // throws; returns null only for a null input.
        public static QuotaSnapshot FromCache(CachedSnapshot c)
        {
            if (c == null) return null;
            QuotaSnapshot snap = new QuotaSnapshot();
            snap.Status = QuotaStatus.Ok;
            DateTime fetched;
            if (TryParseRoundtrip(c.FetchedAt, out fetched)) snap.FetchedAt = fetched;
            else snap.FetchedAt = DateTime.MinValue;

            bool hasProducts = false;
            bool anyError = false;
            if (c.Products != null)
            {
                for (int i = 0; i < c.Products.Count; i++)
                {
                    CachedProduct cp = c.Products[i];
                    if (cp == null) continue;
                    ProductQuota pq = new ProductQuota();
                    pq.Product = cp.Product;
                    pq.DisplayName = DisplayNames.Product(cp.Product);
                    pq.Edition = cp.Edition;
                    pq.Tier = cp.Tier;
                    pq.Subscribed = cp.Subscribed;
                    pq.SubscribedKnown = cp.SubscribedKnown;
                    pq.Malformed = cp.Malformed;
                    pq.PeriodErrorPresent = cp.PeriodErrorPresent;
                    if (cp.HasError) { pq.Error = "该产品查询失败，请稍后重试。"; anyError = true; }
                    if (cp.Malformed || cp.PeriodErrorPresent) anyError = true;
                    DateTime updated;
                    if (cp.HasUpdated && TryParseRoundtrip(cp.UpdatedLocal, out updated))
                    {
                        pq.HasUpdated = true;
                        pq.UpdatedLocal = updated;
                    }
                    if (cp.Periods != null)
                    {
                        for (int j = 0; j < cp.Periods.Count; j++)
                        {
                            CachedPeriod cpp = cp.Periods[j];
                            if (cpp == null) continue;
                            PeriodQuota p = new PeriodQuota();
                            p.Label = cpp.Label;
                            p.LabelDisplay = DisplayNames.Period(cpp.Label);
                            if (cpp.Error)
                            {
                                p.Error = "该周期查询失败。";
                                anyError = true;
                            }
                            {
                                p.PercentKnown = cpp.PercentKnown;
                                p.RemainingPercent = cpp.RemainingPercent;
                                p.Clamped = cpp.Clamped;
                                p.UsedKnown = cpp.UsedKnown;
                                p.Used = cpp.Used;
                                p.TotalKnown = cpp.TotalKnown;
                                p.Total = cpp.Total;
                                p.AmountKnown = cpp.AmountKnown;
                                p.RemainingAmount = cpp.RemainingAmount;
                                DateTime reset;
                                if (cpp.HasReset && TryParseRoundtrip(cpp.ResetLocal, out reset))
                                {
                                    p.HasReset = true;
                                    p.ResetLocal = reset;
                                }
                                DateTime pu;
                                if (cpp.HasUpdated && TryParseRoundtrip(cpp.UpdatedLocal, out pu))
                                {
                                    p.HasUpdated = true;
                                    p.UpdatedLocal = pu;
                                }
                                p.UnknownNote = cpp.UnknownNote;
                            }
                            pq.Periods.Add(p);
                        }
                    }
                    snap.Products.Add(pq);
                    hasProducts = true;
                }
            }

            if (!hasProducts)
            {
                snap.Status = QuotaStatus.NoSubscription;
                snap.Message = string.IsNullOrEmpty(c.Message)
                    ? "当前身份下未发现已订阅的方舟套餐。" : c.Message;
            }
            else if (anyError)
            {
                snap.Status = QuotaStatus.PartialError;
                snap.Message = "部分额度数据获取失败，已展示可用部分。";
            }
            else
            {
                snap.Status = QuotaStatus.Ok;
                snap.Message = string.IsNullOrEmpty(c.Message) ? "已获取当前订阅额度。" : c.Message;
            }
            return snap;
        }

        // Pure encoding: JSON -> UTF8 -> DPAPI(CurrentUser) -> magic+version box.
        // Null on any failure. The version is carried in the plaintext header.
        public static byte[] Encode(CachedSnapshot c)
        {
            if (c == null) return null;
            try
            {
                JavaScriptSerializer ser = new JavaScriptSerializer();
                string json = ser.Serialize(c);
                byte[] plain = Encoding.UTF8.GetBytes(json);
                return CacheCrypto.Protect(FormatVersion, plain);
            }
            catch (Exception) { return null; }
        }

        // Pure decoding: header check -> DPAPI -> UTF8 -> JSON. Null for corrupt /
        // old / foreign data (including a cache written by another Windows user).
        // Never throws.
        public static CachedSnapshot Decode(byte[] boxed)
        {
            byte[] plain = CacheCrypto.Unprotect(FormatVersion, boxed);
            if (plain == null) return null;
            try
            {
                string json = Encoding.UTF8.GetString(plain);
                JavaScriptSerializer ser = new JavaScriptSerializer();
                if (!CompleteObject(ser.DeserializeObject(json), typeof(CachedSnapshot))) return null;
                CachedSnapshot c = ser.Deserialize<CachedSnapshot>(json);
                if (c == null) return null;
                if (c.Version != FormatVersion) return null;
                if (!System.Text.RegularExpressions.Regex.IsMatch(c.ScopeFingerprint ?? "", "\\A[0-9a-f]{64}\\z")) return null;
                if (c.Products == null) return null;
                for (int i = 0; i < c.Products.Count; i++)
                {
                    if (c.Products[i] == null || c.Products[i].Periods == null) return null;
                    foreach (CachedPeriod p in c.Products[i].Periods)
                    {
                        if (p == null) return null;
                        if (p.PercentKnown && (!Finite(p.RemainingPercent) || p.RemainingPercent < 0 || p.RemainingPercent > 100)) return null;
                        if (p.UsedKnown && (!Finite(p.Used) || p.Used < 0)) return null;
                        if (p.TotalKnown && (!Finite(p.Total) || p.Total < 0)) return null;
                        if (p.AmountKnown && (!Finite(p.RemainingAmount) || p.RemainingAmount < 0)) return null;
                    }
                }
                return c;
            }
            catch (Exception) { return null; }
        }

        // Atomic save: encode fully first (never touch the old file on encode
        // failure), write a temp file, then File.Replace with a .bak. Replace
        // failure leaves the old target untouched; never delete it to retry.
        public static bool Save(CachedSnapshot c)
        {
            if (c == null) return false;
            byte[] data = Encode(c);
            if (data == null || Decode(data) == null) return false;

            lock (Gate)
            {
                string tmp = null;
                try
                {
                    string path = FilePath;
                    tmp = path + ".tmp";
                    string bak = path + ".bak";
                    Directory.CreateDirectory(Marker.StateDir());
                    DeleteQuiet(tmp);
                    File.WriteAllBytes(tmp, data);

                    if (File.Exists(path))
                    {
                        try
                        {
                            DeleteQuiet(bak);
                            File.Replace(tmp, path, bak, true);
                            // tmp consumed by Replace; nothing else to do.
                        }
                        catch (Exception)
                        {
                            DeleteQuiet(tmp);
                            return false;
                        }
                    }
                    else
                    {
                        File.Move(tmp, path);
                    }
                }
                catch (Exception)
                {
                    DeleteQuiet(tmp);
                    return false;
                }
            }

            CleanupAux();
            return true;
        }

        public static CachedSnapshot Load()
        {
            try { lock (Gate) { return LoadFrom(FilePath); } }
            catch (Exception) { return null; }
        }

        // Fallback read of the backup left by File.Replace. Used only when the
        // primary file is missing / unreadable, so a crash mid-rotation still
        // yields the previous valid snapshot.
        public static CachedSnapshot LoadBackup()
        {
            lock (Gate) { return LoadFrom(FilePath + ".bak"); }
        }

        // Primary then backup. Returns null when neither is a valid snapshot.
        public static CachedSnapshot LoadAny()
        {
            lock (Gate)
            {
                CachedSnapshot c = LoadFrom(FilePath);
                if (c != null) return c;
                return LoadFrom(FilePath + ".bak");
            }
        }

        // Removes the primary, temp and backup files (best-effort).
        public static void Clear()
        {
            lock (Gate)
            {
                DeleteQuiet(FilePath);
                DeleteQuiet(FilePath + ".tmp");
                DeleteQuiet(FilePath + ".bak");
            }
        }

        private static void CleanupAux()
        {
            lock (Gate) { DeleteQuiet(FilePath + ".bak"); }
        }

        private static CachedSnapshot LoadFrom(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                return Decode(File.ReadAllBytes(path));
            }
            catch (Exception) { return null; }
        }

        private static void DeleteQuiet(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch (Exception) { }
        }

        private static bool TryParseRoundtrip(string s, out DateTime local)
        {
            local = DateTime.MinValue;
            if (string.IsNullOrEmpty(s)) return false;
            try
            {
                DateTime parsed;
                if (DateTime.TryParseExact(s, "o", CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind, out parsed))
                {
                    local = parsed.Kind == DateTimeKind.Utc ? parsed.ToLocalTime() : parsed;
                    return true;
                }
            }
            catch (Exception) { }
            return false;
        }

        private static bool Finite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool CompleteObject(object value, Type type)
        {
            Dictionary<string, object> obj = value as Dictionary<string, object>;
            if (obj == null) return false;
            foreach (System.Reflection.FieldInfo field in type.GetFields())
            {
                object member;
                if (!obj.TryGetValue(field.Name, out member)) return false;
                if (field.FieldType == typeof(bool) && !(member is bool)) return false;
                if (field.FieldType == typeof(string) && member != null && !(member is string)) return false;
                if (field.FieldType == typeof(int) && !(member is int)) return false;
                if (field.FieldType == typeof(double) && !(member is int) && !(member is long)
                    && !(member is double) && !(member is decimal)) return false;
                if (field.FieldType.IsGenericType)
                {
                    System.Collections.IEnumerable list = member as System.Collections.IEnumerable;
                    if (list == null || member is string || member is Dictionary<string, object>) return false;
                    foreach (object item in list)
                        if (!CompleteObject(item, field.FieldType.GetGenericArguments()[0])) return false;
                }
            }
            return true;
        }
    }
}
