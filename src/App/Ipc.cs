using System;
using System.Threading;

namespace ArkLeft
{
    // Small, testable named-event IPC used to wake the first instance. Kept
    // separate so the duplicate-start mechanism can be unit tested with an
    // isolated event name (never the real singleton name in tests).
    internal static class Ipc
    {
        // Best-effort bounded signal. Never blocks indefinitely and never throws.
        public static bool Signal(string name, int retries, int delayMs)
        {
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    EventWaitHandle ev;
                    if (EventWaitHandle.TryOpenExisting(name, out ev))
                    {
                        using (ev) { ev.Set(); }
                        return true;
                    }
                }
                catch (Exception) { }
                if (i < retries - 1 && delayMs > 0)
                {
                    try { Thread.Sleep(delayMs); } catch (Exception) { }
                }
            }
            return false;
        }

        // Registers a waiter for a named event. Returns a handle that keeps the
        // registration alive; dispose it to stop listening.
        public static IDisposable Register(string name, Action onSet)
        {
            EventWaitHandle ev = new EventWaitHandle(false, EventResetMode.AutoReset, name);
            RegisteredWaitHandle reg = ThreadPool.RegisterWaitForSingleObject(
                ev, delegate { try { onSet(); } catch (Exception) { } },
                null, Timeout.Infinite, false);
            return new Registration(ev, reg);
        }

        private sealed class Registration : IDisposable
        {
            private EventWaitHandle _event;
            private RegisteredWaitHandle _reg;
            private bool _disposed;

            public Registration(EventWaitHandle ev, RegisteredWaitHandle reg)
            {
                _event = ev; _reg = reg;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                try { if (_reg != null) _reg.Unregister(null); } catch (Exception) { }
                try { if (_event != null) _event.Close(); } catch (Exception) { }
                _reg = null; _event = null;
            }
        }
    }
}
