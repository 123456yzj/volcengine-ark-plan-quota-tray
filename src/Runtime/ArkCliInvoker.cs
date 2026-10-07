using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ArkLeft
{
    // Native process transport only. No credentials, shell shims or business rules.
    public sealed class ArkCliInvoker : IDisposable
    {
        private const int DrainGraceMs = 1500;
        private readonly object _gate = new object();
        private readonly List<Process> _active = new List<Process>();
        private bool _disposed;

        public bool HasActiveProcesses { get { lock (_gate) return _active.Count != 0; } }

        // Shares the process-registration gate with activation/cleanup.
        public bool WhenIdle(Action action)
        {
            lock (_gate)
            {
                if (_disposed || _active.Count != 0) return false;
                action();
                return true;
            }
        }

        public static ProcessStartInfo BuildStartInfo(string exe, string arguments)
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = exe;
            psi.Arguments = arguments;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardInput = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;
            psi.EnvironmentVariables["ARKCLI_NO_UPDATE_NOTIFIER"] = "1";
            psi.EnvironmentVariables.Remove("ARKCLI_CALLER_TYPE");
            psi.EnvironmentVariables.Remove("ARKCLI_CALLER_NAME");
            psi.EnvironmentVariables.Remove("ARKCLI_SKILL_NAME");
            return psi;
        }

        public async Task<CliResult> RunAsync(string exe, string arguments, int timeoutMs, CancellationToken token)
        {
            CliResult result = new CliResult();
            Process proc = new Process();
            proc.StartInfo = BuildStartInfo(exe, arguments);
            lock (_gate)
            {
                if (_disposed) { proc.Dispose(); result.Failure = "disposed"; return result; }
                if (token.IsCancellationRequested) { proc.Dispose(); result.Cancelled = true; return result; }
                try
                {
                    if (!proc.Start()) throw new InvalidOperationException();
                    _active.Add(proc);
                }
                catch (Exception)
                {
                    proc.Dispose(); result.Failure = "start-failed"; return result;
                }
            }
            result.Started = true;
            Task<string> stdout = null, stderr = null;
            try
            {
                // SSO is a browser workflow; stdin EOF prevents an invisible terminal prompt.
                proc.StandardInput.Close();
                stdout = proc.StandardOutput.ReadToEndAsync();
                stderr = proc.StandardError.ReadToEndAsync();
                DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
                while (!proc.HasExited)
                {
                    if (token.IsCancellationRequested) { result.Cancelled = true; KillQuiet(proc); break; }
                    if (DateTime.UtcNow >= deadline) { result.TimedOut = true; KillQuiet(proc); break; }
                    await Task.Delay(50).ConfigureAwait(false);
                }
                proc.WaitForExit(2000);
                if (!result.Cancelled && !result.TimedOut) result.ExitCode = proc.ExitCode;
                Task drain = Task.WhenAll(stdout, stderr);
                if (await Task.WhenAny(drain, Task.Delay(DrainGraceMs)).ConfigureAwait(false) != drain)
                    result.Failure = "drain-timeout";
                else if (drain.IsFaulted || drain.IsCanceled) result.Failure = "process-error";
            }
            catch (Exception) { result.Failure = "process-error"; KillQuiet(proc); }
            finally
            {
                lock (_gate) _active.Remove(proc);
                proc.Dispose();
            }
            result.StdOut = ReadCompleted(stdout);
            result.StdErr = ReadCompleted(stderr);
            return result;
        }

        private static string ReadCompleted(Task<string> task)
        {
            return task != null && task.Status == TaskStatus.RanToCompletion ? task.Result : "";
        }
        private static void KillQuiet(Process proc)
        {
            try { if (!proc.HasExited) proc.Kill(); } catch (Exception) { }
        }
        public void KillActive()
        {
            lock (_gate) foreach (Process proc in _active) KillQuiet(proc);
        }
        public void Dispose()
        {
            lock (_gate)
            {
                _disposed = true;
                foreach (Process proc in _active) KillQuiet(proc);
            }
        }
    }
}
