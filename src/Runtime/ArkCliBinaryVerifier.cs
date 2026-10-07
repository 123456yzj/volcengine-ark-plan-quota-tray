using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace ArkLeft
{
    public interface IArkCliBinaryVerifier
    {
        Task VerifyAsync(string path, string version, string digest, CancellationToken token);
    }

    public sealed class ArkCliBinaryVerifier : IArkCliBinaryVerifier
    {
        private readonly ArkCliInvoker _invoker;
        public ArkCliBinaryVerifier(ArkCliInvoker invoker) { _invoker = invoker; }
        public static string Sha256(string path)
        {
            using (FileStream file = File.OpenRead(path))
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "").ToLowerInvariant();
        }
        public static void VerifyDigest(string path, string digest)
        {
            string actual = "sha256:" + Sha256(path);
            if (digest != null && (!System.Text.RegularExpressions.Regex.IsMatch(digest, @"^sha256:[a-fA-F0-9]{64}$")
                || !string.Equals(actual, digest, StringComparison.OrdinalIgnoreCase)))
                throw new ArkCliRuntimeException(ArkCliRuntimeError.RuntimeIntegrityFailed);
        }
        public async Task VerifyAsync(string path, string version, string digest, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!File.Exists(path)) throw new ArkCliRuntimeException(ArkCliRuntimeError.RuntimeMissing);
            // Hash/signature work happens on the caller's background task.
            VerifyDigest(path, digest);
            if (!VerifySignature(path)) throw new ArkCliRuntimeException(ArkCliRuntimeError.RuntimeSignatureInvalid);
            CliResult result = await _invoker.RunAsync(path, "--version", 10000, token).ConfigureAwait(false);
            if (result.Cancelled) throw new OperationCanceledException(token);
            if (!result.Started) throw new ArkCliRuntimeException(ArkCliRuntimeError.RuntimeStartFailed);
            ArkCliVersion reported = ArkCliVersion.FromOutput(result.StdOut);
            if (result.TimedOut || result.Failure != null || result.ExitCode != 0 || reported == null || reported.ToString() != version
                || reported.CompareTo(ArkCliVersion.Parse(ArkCliRuntimeConfig.MinimumSupportedVersion)) < 0)
                throw new ArkCliRuntimeException(ArkCliRuntimeError.RuntimeIncompatible);
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct TrustFile
        {
            public uint Size;
            [MarshalAs(UnmanagedType.LPWStr)] public string Path;
            public IntPtr FileHandle, KnownSubject;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct TrustData
        {
            public uint Size;
            public IntPtr PolicyCallback, SipCallback;
            public uint UiChoice, RevocationChecks, UnionChoice;
            public IntPtr File;
            public uint StateAction;
            public IntPtr StateData;
            [MarshalAs(UnmanagedType.LPWStr)] public string UrlReference;
            public uint ProviderFlags, UiContext;
        }
        [DllImport("wintrust.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern int WinVerifyTrust(IntPtr hwnd, ref Guid action, ref TrustData data);
        public static bool VerifySignature(string path)
        {
            TrustFile file = new TrustFile { Size = (uint)Marshal.SizeOf(typeof(TrustFile)), Path = path };
            IntPtr memory = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TrustFile)));
            TrustData data = new TrustData {
                Size = (uint)Marshal.SizeOf(typeof(TrustData)), UiChoice = 2,
                UnionChoice = 1, File = memory, StateAction = 1,
                // Cache-only URL retrieval allows an already trusted binary to work offline.
                // Chain/signature validation remains enabled; no pinned certificate thumbprint.
                ProviderFlags = 0x1000 };
            Guid action = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
            Marshal.StructureToPtr(file, memory, false);
            try { return WinVerifyTrust(new IntPtr(-1), ref action, ref data) == 0; }
            catch (Exception) { return false; }
            finally
            {
                data.StateAction = 2;
                try { WinVerifyTrust(new IntPtr(-1), ref action, ref data); } catch (Exception) { }
                Marshal.DestroyStructure(memory, typeof(TrustFile));
                Marshal.FreeHGlobal(memory);
            }
        }
    }
}
