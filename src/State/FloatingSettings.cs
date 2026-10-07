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
    // Persisted display-target choice for the floating circle. This is NOT the
    // quota cache (quota-cache.dat / format 1 is untouched): it only remembers
    // which product + period the user chose to show. It stores no identity,
    // credentials or quota values. Corrupt / wrong-version files are treated as
    // "not configured" and never fabricate a selection.
    public class FloatingSettings
    {
        public int Version;
        public string ProductKey;   // Product|Edition|Tier (stable across versions)
        public string PeriodLabel;  // period label as returned by the API
    }

    public static class FloatingSettingsStore
    {
        public const int FormatVersion = 1;
        public const string FileName = "floating-settings.json";
        public const int MaxProductKeyLength = 128;
        public const int MaxPeriodLabelLength = 64;
        private static readonly object Gate = new object();

        public static string FilePath
        {
            get { return Path.Combine(Marker.StateDir(), FileName); }
        }

        // Stable, identity-free product key. Edition / tier are included so two
        // different plans that share a product id never collide.
        public static string ProductKey(ProductQuota pq)
        {
            if (pq == null) return null;
            string p = pq.Product == null ? "" : pq.Product;
            string e = pq.Edition == null ? "" : pq.Edition;
            string t = pq.Tier == null ? "" : pq.Tier;
            return p + "|" + e + "|" + t;
        }

        public static bool Valid(FloatingSettings s)
        {
            if (s == null) return false;
            if (s.Version != FormatVersion) return false;
            if (string.IsNullOrEmpty(s.ProductKey)
                || s.ProductKey.Length > MaxProductKeyLength) return false;
            if (string.IsNullOrEmpty(s.PeriodLabel)
                || s.PeriodLabel.Length > MaxPeriodLabelLength) return false;
            return true;
        }

        // Strict decode. Null (=> treat as not configured) on any of: missing
        // file, malformed JSON, wrong version, missing/wrong-typed/empty fields,
        // over-length fields. Never returns a partially trusted object.
        public static FloatingSettings Load()
        {
            try
            {
                lock (Gate)
                {
                    string path = FilePath;
                    if (!File.Exists(path)) return null;
                    string json = File.ReadAllText(path, Encoding.UTF8);
                    JavaScriptSerializer ser = new JavaScriptSerializer();
                    Dictionary<string, object> d =
                        ser.DeserializeObject(json) as Dictionary<string, object>;
                    if (d == null) return null;
                    // Exactly the three known fields: any extra / unknown field
                    // is treated as a corrupt file, never silently ignored.
                    if (d.Count != 3) return null;

                    object v, pk, pl;
                    if (!d.TryGetValue("Version", out v) || !(v is int)) return null;
                    if ((int)v != FormatVersion) return null;
                    if (!d.TryGetValue("ProductKey", out pk) || !(pk is string)) return null;
                    if (!d.TryGetValue("PeriodLabel", out pl) || !(pl is string)) return null;

                    FloatingSettings s = new FloatingSettings();
                    s.Version = (int)v;
                    s.ProductKey = (string)pk;
                    s.PeriodLabel = (string)pl;
                    return Valid(s) ? s : null;
                }
            }
            catch (Exception) { return null; }
        }

        // Atomic write: temp file then File.Replace (Move when no old file).
        // Failure never degrades an existing valid file; returns false so the
        // caller can tell the user the choice was not remembered.
        public static bool Save(FloatingSettings s)
        {
            if (!Valid(s)) return false;
            lock (Gate)
            {
                string tmp = null;
                try
                {
                    JavaScriptSerializer ser = new JavaScriptSerializer();
                    byte[] data = Encoding.UTF8.GetBytes(ser.Serialize(s));
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
            DeleteQuiet(FilePath + ".bak");
            return true;
        }

        public static void Clear()
        {
            lock (Gate)
            {
                DeleteQuiet(FilePath);
                DeleteQuiet(FilePath + ".tmp");
                DeleteQuiet(FilePath + ".bak");
            }
        }

        private static void DeleteQuiet(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch (Exception) { }
        }
    }

}
