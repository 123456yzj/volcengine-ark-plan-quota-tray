using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace ArkLeft
{
    // Identity parsed from `arkcli auth status --format json`.
    // Real schema (verified by the main agent):
    //   { "logged_in": true,
    //     "active_profile": { "name", "type", "owner_trn", "region", "project" } }
    // No nested viewer.profile here; the usage `viewer.profile` is a STRING.
    public class AuthIdentity
    {
        public bool Present;
        public string Name;
        public string Type;
        public string OwnerTrn;
        public string Region;
        public string Project;
        // Opaque binding of a direct browser-login session. Never displayed.
        public string DirectSessionBinding;
    }

    // Identity parsed from `usage plan --format json` viewer object.
    // Real schema: viewer = { account_id, user_id, profile(string), tenant,
    //                        region, project_name, is_root }.
    public class ViewerIdentity
    {
        public bool Present;
        public string AccountId;
        public string UserId;
        public string Profile;
        public string Tenant;
        public string Region;
        public string ProjectName;
        public bool IsRoot;
        public bool IsRootKnown;
    }

    public enum ScopeVerdict { Unknown, Same, Mismatch }

    // In-memory identity scope. The fingerprint is SHA256 over the auth
    // owner_trn + profile name + region + project. It is NEVER persisted or
    // displayed. Missing required identity => unknown => no cache reuse.
    //
    // owner_trn real format (verified):
    //   trn:iam::<account>:root
    //   trn:iam::<account>:user/<id>
    // Unsupported formats are treated as unknown (never same).
    public class QueryScope
    {
        public bool IsKnown;
        public string Fingerprint;
        public string OwnerTrn;
        public string ProfileName;
        public string Region;
        public string Project;
        public string Account;   // parsed from owner_trn
        public bool IsRoot;      // owner_trn principal == root
        public string UserId;    // owner_trn principal == user/<id>

        public static QueryScope Unknown()
        {
            return new QueryScope();
        }

        public static QueryScope FromAuth(AuthIdentity a)
        {
            if (a == null) return Unknown();
            if (!string.IsNullOrEmpty(a.DirectSessionBinding))
            {
                Guid binding;
                if (!Guid.TryParseExact(a.DirectSessionBinding, "N", out binding)) return Unknown();
                return new QueryScope { IsKnown = true, ProfileName = a.Name,
                    Region = a.Region, Project = a.Project,
                    Fingerprint = Sha256Hex("direct-agent-plan\n" + a.DirectSessionBinding + "\ncn-beijing\npersonal") };
            }
            if (string.IsNullOrEmpty(a.OwnerTrn) || string.IsNullOrEmpty(a.Name)
                || string.IsNullOrEmpty(a.Region) || string.IsNullOrEmpty(a.Project))
                return Unknown();

            string account;
            bool isRoot;
            string userId;
            if (!TryParseOwnerTrn(a.OwnerTrn, out account, out isRoot, out userId))
                return Unknown();

            QueryScope s = new QueryScope();
            s.IsKnown = true;
            s.OwnerTrn = a.OwnerTrn;
            s.ProfileName = a.Name;
            s.Region = a.Region;
            s.Project = a.Project;
            s.Account = account;
            s.IsRoot = isRoot;
            s.UserId = userId;
            s.Fingerprint = Sha256Hex(a.OwnerTrn + "\n" + a.Name + "\n" + a.Region + "\n" + a.Project);
            return s;
        }

        public bool Matches(QueryScope other)
        {
            return IsKnown && other != null && other.IsKnown
                   && string.Equals(Fingerprint, other.Fingerprint, StringComparison.Ordinal);
        }

        // trn:iam::<account>:root | trn:iam::<account>:user/<id>
        public static bool TryParseOwnerTrn(string trn, out string account, out bool isRoot,
            out string userId)
        {
            account = null; isRoot = false; userId = null;
            if (string.IsNullOrEmpty(trn)) return false;
            string[] parts = trn.Split(':');
            if (parts.Length != 5) return false;
            if (parts[0] != "trn" || parts[1] != "iam") return false;
            // Real format keeps the region slot empty: trn:iam::<account>:...
            if (parts[2].Length != 0) return false;
            if (string.IsNullOrEmpty(parts[3])) return false;
            account = parts[3];

            string principal = parts[4];
            if (principal == "root") { isRoot = true; return true; }
            if (principal != null && principal.StartsWith("user/", StringComparison.Ordinal))
            {
                userId = principal.Substring("user/".Length);
                if (string.IsNullOrEmpty(userId)) return false;
                return true;
            }
            return false;
        }

        private static string Sha256Hex(string input)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
                StringBuilder sb = new StringBuilder(bytes.Length * 2);
                for (int i = 0; i < bytes.Length; i++)
                    sb.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }
    }

    // Cross-checks the auth-derived scope against the usage viewer object.
    //  - A concrete conflict in account / profile / region / project / principal
    //    => Mismatch.
    //  - Otherwise Same ONLY when every identity-defining viewer field is present
    //    and consistent (account, profile, region, project_name, is_root, and the
    //    root/user principal). Missing a defining field => Unknown, never Same.
    // No ID is ever fabricated or substring-matched.
    public static class ScopeValidation
    {
        public static ScopeVerdict Validate(QueryScope authScope, ViewerIdentity v)
        {
            if (authScope == null || !authScope.IsKnown) return ScopeVerdict.Unknown;
            if (v == null || !v.Present) return ScopeVerdict.Unknown;

            // Presence is decided by non-blank content: an empty OR whitespace
            // string is a MISSING field (Unknown), never a conflict. Non-blank
            // values are compared exactly and left unmodified (no trim) so a
            // real ID can never be accidentally substring/whitespace-matched.
            bool hasAccount = !string.IsNullOrWhiteSpace(v.AccountId);
            bool hasProfile = !string.IsNullOrWhiteSpace(v.Profile);
            bool hasRegion = !string.IsNullOrWhiteSpace(v.Region);
            bool hasProject = !string.IsNullOrWhiteSpace(v.ProjectName);
            bool hasUser = !string.IsNullOrWhiteSpace(v.UserId);

            // --- conflicts (strict equality, never substring) ---
            if (hasAccount && !string.Equals(v.AccountId, authScope.Account, StringComparison.Ordinal))
                return ScopeVerdict.Mismatch;

            if (hasProfile && !string.Equals(v.Profile, authScope.ProfileName, StringComparison.Ordinal))
                return ScopeVerdict.Mismatch;

            if (hasRegion && !string.Equals(v.Region, authScope.Region, StringComparison.Ordinal))
                return ScopeVerdict.Mismatch;

            if (hasProject && !string.Equals(v.ProjectName, authScope.Project, StringComparison.Ordinal))
                return ScopeVerdict.Mismatch;

            if (authScope.IsRoot)
            {
                if (v.IsRootKnown && v.IsRoot != true) return ScopeVerdict.Mismatch;
            }
            else
            {
                // Sub-user auth vs a viewer that explicitly reports root is a
                // concrete principal conflict, regardless of whether user_id was
                // returned. Missing is_root stays Unknown (handled below).
                if (v.IsRootKnown && v.IsRoot == true) return ScopeVerdict.Mismatch;
                if (hasUser && !string.Equals(v.UserId, authScope.UserId, StringComparison.Ordinal))
                    return ScopeVerdict.Mismatch;
            }

            // --- completeness required for a definitive Same ---
            if (!hasAccount || !hasProfile || !hasRegion || !hasProject || !v.IsRootKnown)
                return ScopeVerdict.Unknown;

            if (authScope.IsRoot)
            {
                if (v.IsRoot != true) return ScopeVerdict.Unknown;
            }
            else
            {
                if (!hasUser) return ScopeVerdict.Unknown;
            }

            return ScopeVerdict.Same;
        }
    }

    // Parses auth status / usage viewer JSON. Pure, no disk, no display.
    public static class IdentityParse
    {
        public static bool TryParseAuth(string json, out bool loggedIn, out AuthIdentity identity)
        {
            loggedIn = false;
            identity = null;
            if (string.IsNullOrEmpty(json)) return false;
            try
            {
                JavaScriptSerializer ser = new JavaScriptSerializer();
                object rootRaw = ser.DeserializeObject(json);
                Dictionary<string, object> d = rootRaw as Dictionary<string, object>;
                if (d == null) return false;

                object raw;
                if (!d.TryGetValue("logged_in", out raw) || raw == null) return false;
                if (!(raw is bool)) return false;
                loggedIn = (bool)raw;

                object ap;
                if (d.TryGetValue("active_profile", out ap) && ap != null)
                {
                    Dictionary<string, object> p = ap as Dictionary<string, object>;
                    if (p != null)
                    {
                        AuthIdentity id = new AuthIdentity();
                        id.Name = Str(p, "name");
                        id.Type = Str(p, "type");
                        id.OwnerTrn = Str(p, "owner_trn");
                        id.Region = Str(p, "region");
                        id.Project = Str(p, "project");
                        id.Present = id.Name != null || id.Type != null || id.OwnerTrn != null
                                     || id.Region != null || id.Project != null;
                        if (id.Present) identity = id;
                    }
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static ViewerIdentity ParseViewer(Dictionary<string, object> root)
        {
            if (root == null) return null;
            object vr;
            if (!root.TryGetValue("viewer", out vr) || vr == null) return null;
            Dictionary<string, object> v = vr as Dictionary<string, object>;
            if (v == null) return null;

            ViewerIdentity id = new ViewerIdentity();
            id.AccountId = Str(v, "account_id");
            id.UserId = Str(v, "user_id");
            id.Profile = Str(v, "profile");
            id.Tenant = Str(v, "tenant");
            id.Region = Str(v, "region");
            id.ProjectName = Str(v, "project_name");
            object ir;
            if (v.TryGetValue("is_root", out ir) && ir is bool)
            {
                id.IsRoot = (bool)ir;
                id.IsRootKnown = true;
            }
            id.Present = id.AccountId != null || id.UserId != null || id.Profile != null
                         || id.Tenant != null || id.Region != null || id.ProjectName != null;
            return id;
        }

        private static string Str(Dictionary<string, object> d, string key)
        {
            object raw;
            if (!d.TryGetValue(key, out raw) || raw == null) return null;
            return raw as string;
        }
    }

    // Friendly, privacy-safe identity description for the UI. Never shows
    // owner_trn / account_id / user name; shows a mapped profile type, region and
    // a length-limited, user-chosen profile name.
    public static class IdentityDisplay
    {
        public const int MaxProfileNameChars = 18;

        // active_profile.type maps to a platform/product label, not the auth
        // method. Unknown values fall back to the (length-limited) raw value.
        public static string FriendlyType(string type)
        {
            if (string.IsNullOrEmpty(type)) return null;
            switch (type.ToLowerInvariant())
            {
                case "agent-plan": return "Agent Plan";
                case "coding-plan": return "Coding Plan";
                case "team": return "团队";
                case "platform": return "平台";
                case "personal": return "个人";
                default: return Truncate(type, MaxProfileNameChars);
            }
        }

        public static string LimitProfileName(string name)
        {
            return Truncate(name, MaxProfileNameChars);
        }

        public static string Describe(AuthIdentity a)
        {
            if (a == null) return null;
            StringBuilder sb = new StringBuilder();
            string type = FriendlyType(a.Type);
            if (type != null) sb.Append(type);
            if (!string.IsNullOrEmpty(a.Region))
            {
                if (sb.Length > 0) sb.Append(" · ");
                sb.Append("区域 ").Append(a.Region);
            }
            string name = LimitProfileName(a.Name);
            if (!string.IsNullOrEmpty(name))
            {
                if (sb.Length > 0) sb.Append(" · ");
                sb.Append("配置 ").Append(name);
            }
            return sb.Length == 0 ? null : sb.ToString();
        }

        public static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
            return s.Substring(0, max) + "…";
        }
    }
}
