using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace ArkLeft
{
    internal sealed class DirectSession
    {
        public int Version = 1;
        public string RefreshToken;
        public string Binding;
    }

    internal sealed class DirectSts
    {
        public string AccessKey;
        public string SecretKey;
        public string SessionToken;
        public DateTime ExpiresUtc;
    }

    internal interface IDirectSessionStore
    {
        DirectSession Load();
        bool Save(DirectSession session);
        void Clear();
    }

    // Only a refresh token and an opaque login binding are persisted. STS and
    // authorization codes never enter this file or the quota snapshot cache.
    internal sealed class DirectSessionStore : IDirectSessionStore
    {
        private readonly string _path;
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ark_left.direct-agent-plan.v1");
        public DirectSessionStore() : this(Path.Combine(Marker.StateDir(), "direct-session.dat")) { }
        internal DirectSessionStore(string path) { _path = path; }

        internal static byte[] Encode(DirectSession session)
        {
            byte[] plain = Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(session));
            try { return ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser); }
            finally { Array.Clear(plain, 0, plain.Length); }
        }
        internal static DirectSession Decode(byte[] data)
        {
            byte[] plain = ProtectedData.Unprotect(data, Entropy, DataProtectionScope.CurrentUser);
            try
            {
                DirectSession result = new JavaScriptSerializer().Deserialize<DirectSession>(Encoding.UTF8.GetString(plain));
                Guid binding;
                if (result == null || result.Version != 1 || string.IsNullOrWhiteSpace(result.RefreshToken)
                    || !Guid.TryParseExact(result.Binding, "N", out binding)) return null;
                return result;
            }
            finally { Array.Clear(plain, 0, plain.Length); }
        }
        public DirectSession Load()
        {
            try { return File.Exists(_path) && new FileInfo(_path).Length <= 65536 ? Decode(File.ReadAllBytes(_path)) : null; }
            catch (Exception) { return null; }
        }
        public bool Save(DirectSession session)
        {
            string temp = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                byte[] encrypted = Encode(session);
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                File.WriteAllBytes(temp, encrypted);
                if (File.Exists(_path)) File.Replace(temp, _path, null);
                else File.Move(temp, _path);
                return true;
            }
            catch (Exception) { return false; }
            finally { try { if (File.Exists(temp)) File.Delete(temp); } catch (Exception) { } }
        }
        public void Clear()
        {
            try { if (File.Exists(_path)) File.Delete(_path); }
            catch (Exception) { throw new DirectFailure(QuotaStatus.CredentialStorageFailed, "无法清除旧登录凭证，请检查本地状态目录。"); }
        }
    }
}
