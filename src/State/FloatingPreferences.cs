using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace ArkLeft
{
    // v0.8 UX016 / v0.9 UX017 / v0.24 UX029: identity-free UI preferences for
    // the floating circle. Deliberately separate from FloatingSettings (the
    // quota display choice): this file only remembers booleans and the theme
    // choice, never coordinates, identity or quota values. Corrupt /
    // wrong-version files are treated as "not configured" and never fabricate
    // a state.
    //
    // Format history: format 1 (UX016) held Version + PositionLocked only.
    // Format 2 (UX017) adds ReduceMotion. Format 3 (UX029) adds AccentIndex +
    // DarkMode. Older formats are migrated IN MEMORY on load (missing fields
    // take their defaults); loading never writes, so the file is upgraded only
    // on the next preference save, which always carries EVERY field.
    public class FloatingPreferences
    {
        public int Version;
        public bool PositionLocked;
        public bool ReduceMotion;
        public int AccentIndex;
        public bool DarkMode;
    }

    public static class FloatingPreferencesStore
    {
        public const int FormatVersion = 3;
        // Legacy formats (read-only migration entries, never written).
        public const int FormatVersion1 = 1;
        public const int FormatVersion2 = 2;
        public const string FileName = "floating-preferences.json";
        // A handful of small fields is tiny; anything larger is corrupt (a
        // runaway / hostile file must not be parsed or trusted).
        public const int MaxFileBytes = 4096;
        private static readonly object Gate = new object();

        public static string FilePath
        {
            get { return Path.Combine(Marker.StateDir(), FileName); }
        }

        public static bool Valid(FloatingPreferences p)
        {
            if (p == null) return false;
            return p.Version == FormatVersion;
        }

        // Strict decode. Null (=> treat as unlocked / not configured) on any
        // of: missing file, malformed JSON, wrong version, wrong-typed fields,
        // missing fields or unknown extra fields. Never returns a partially
        // trusted object. Formats 1 / 2 are migrated in memory.
        public static FloatingPreferences Load()
        {
            try
            {
                lock (Gate)
                {
                    string path = FilePath;
                    if (!File.Exists(path)) return null;
                    // Size gate BEFORE reading: an oversized file is corrupt by
                    // definition, whatever its content looks like.
                    FileInfo info = new FileInfo(path);
                    if (info.Length > MaxFileBytes) return null;
                    string json = File.ReadAllText(path, Encoding.UTF8);
                    JavaScriptSerializer ser = new JavaScriptSerializer();
                    Dictionary<string, object> d =
                        ser.DeserializeObject(json) as Dictionary<string, object>;
                    if (d == null) return null;
                    object v, locked, motion, accent, dark;

                    if (d.Count == 5)
                    {
                        // v0.24 UX029 format 3: exactly the five known fields.
                        if (!d.TryGetValue("Version", out v) || !(v is int)) return null;
                        if ((int)v != FormatVersion) return null;
                        if (!d.TryGetValue("PositionLocked", out locked)
                            || !(locked is bool)) return null;
                        if (!d.TryGetValue("ReduceMotion", out motion)
                            || !(motion is bool)) return null;
                        if (!d.TryGetValue("AccentIndex", out accent)
                            || !(accent is int)) return null;
                        if (!d.TryGetValue("DarkMode", out dark)
                            || !(dark is bool)) return null;
                        FloatingPreferences p3 = new FloatingPreferences();
                        p3.Version = (int)v;
                        p3.PositionLocked = (bool)locked;
                        p3.ReduceMotion = (bool)motion;
                        p3.AccentIndex = (int)accent;
                        p3.DarkMode = (bool)dark;
                        return Valid(p3) ? p3 : null;
                    }
                    if (d.Count == 3)
                    {
                        // Legacy format 2 (strict three fields): migrate in
                        // memory to format 3 with default theme.
                        if (!d.TryGetValue("Version", out v) || !(v is int)) return null;
                        if ((int)v != FormatVersion2) return null;
                        if (!d.TryGetValue("PositionLocked", out locked)
                            || !(locked is bool)) return null;
                        if (!d.TryGetValue("ReduceMotion", out motion)
                            || !(motion is bool)) return null;
                        FloatingPreferences p2 = new FloatingPreferences();
                        p2.Version = FormatVersion;
                        p2.PositionLocked = (bool)locked;
                        p2.ReduceMotion = (bool)motion;
                        return Valid(p2) ? p2 : null;
                    }
                    if (d.Count == 2)
                    {
                        // Legacy format 1 (strict two fields): migrate in
                        // memory to format 3 with defaults.
                        if (!d.TryGetValue("Version", out v) || !(v is int)) return null;
                        if ((int)v != FormatVersion1) return null;
                        if (!d.TryGetValue("PositionLocked", out locked)
                            || !(locked is bool)) return null;
                        FloatingPreferences p1 = new FloatingPreferences();
                        p1.Version = FormatVersion;
                        p1.PositionLocked = (bool)locked;
                        p1.ReduceMotion = false;
                        return Valid(p1) ? p1 : null;
                    }
                    // Any other field count (extra / unknown / missing fields)
                    // is a corrupt file, never silently repaired.
                    return null;
                }
            }
            catch (Exception) { return null; }
        }

        // Atomic write: temp file then File.Replace (Move when no old file),
        // same pattern as FloatingSettingsStore.Save. Failure never degrades
        // an existing valid file; returns false so the caller keeps the old
        // state and tells the user the choice was not remembered.
        public static bool Save(FloatingPreferences p)
        {
            if (!Valid(p)) return false;
            lock (Gate)
            {
                string tmp = null;
                try
                {
                    JavaScriptSerializer ser = new JavaScriptSerializer();
                    byte[] data = Encoding.UTF8.GetBytes(ser.Serialize(p));
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
