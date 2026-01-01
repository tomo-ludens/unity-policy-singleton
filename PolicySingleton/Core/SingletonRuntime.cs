using System.Threading;
using UnityEngine;

namespace TomoLudens.PolicySingleton.Core
{
    /// <summary>
    /// Runtime state shared by singleton infrastructure.
    /// </summary>
    /// <remarks>
    /// PlaySessionId is used to invalidate stale static caches when Domain Reload is disabled.
    /// </remarks>
    internal static class SingletonRuntime
    {
        private const int UninitializedMainThreadId = -1;

        private static int _mainThreadId = UninitializedMainThreadId;
        private static int _lastBeginFrame = -1;

        public static int PlaySessionId { get; private set; }
        public static bool IsQuitting { get; private set; }

        private static bool IsMainThread
            => _mainThreadId != UninitializedMainThreadId
               && _mainThreadId == Thread.CurrentThread.ManagedThreadId;

        internal static void EnsureInitializedForCurrentPlaySession()
        {
            // Must only run on the Unity main thread.
            if (!Application.isPlaying) return;

            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;
        }

        /// <summary>
        /// Returns true if the caller is on the main thread.
        /// On failure, it MUST emit an Error log containing "must be called from the main thread".
        /// </summary>
        internal static bool ValidateMainThread(string callerContext)
        {
            // Fast path: if main thread is known, do NOT touch Unity APIs on background threads.
            if (_mainThreadId != UninitializedMainThreadId)
            {
                if (IsMainThread) return true;

                LogMainThreadViolation(callerContext: callerContext, reason: null);
                return false;
            }

            // Slow path: main thread not initialized yet.
            try
            {
                // In EditMode or outside play, allow (no threading guarantees needed).
                if (!Application.isPlaying) return true;

                // This runs on the main thread via SubsystemRegistration; if we're here, it may be early.
                EnsureInitializedForCurrentPlaySession();

                // If still uninitialized, try a safe capture only when a Unity sync context exists.
                TryCaptureMainThreadIdIfSafe();

                if (_mainThreadId != UninitializedMainThreadId)
                {
                    if (IsMainThread) return true;

                    LogMainThreadViolation(callerContext: callerContext, reason: null);
                    return false;
                }

                // No main thread id available: still treat as violation to keep behavior deterministic.
                LogMainThreadViolation(callerContext: callerContext, reason: "main thread id is not initialized yet");
                return false;
            }
            catch (UnityException)
            {
                // Unity API touched from a background thread: keep contract by logging.
                LogMainThreadViolation(callerContext: callerContext, reason: "Unity API access from a background thread");
                return false;
            }
        }

        internal static void NotifyQuitting() => IsQuitting = true;

        [RuntimeInitializeOnLoadMethod(loadType: RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void SubsystemRegistration()
        {
            if (!Application.isPlaying) return;

            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            BeginNewPlaySession();
        }

        private static void BeginNewPlaySession()
        {
            // Guard against multiple calls in the same frame.
            if (Time.frameCount == _lastBeginFrame) return;

            _lastBeginFrame = Time.frameCount;

            EnsureInitializedForCurrentPlaySession();

            unchecked { PlaySessionId++; }
            IsQuitting = false;
        }

        private static void OnQuitting() => NotifyQuitting();

        private static void TryCaptureMainThreadIdIfSafe()
        {
            if (_mainThreadId != UninitializedMainThreadId) return;

            // Heuristic: Unity main thread typically has a non-null SynchronizationContext.
            // Avoid capturing on background threads.
            if (SynchronizationContext.Current == null) return;

            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        private static void LogMainThreadViolation(string callerContext, string reason)
        {
            int current = Thread.CurrentThread.ManagedThreadId;
            int main = _mainThreadId;

            if (string.IsNullOrEmpty(value: reason))
            {
                SingletonLogger.LogError(message: $"{callerContext} must be called from the main thread.\nCurrent thread: {current}, Main thread: {main}.");
                return;
            }

            SingletonLogger.LogError(message: $"{callerContext} must be called from the main thread ({reason}).\nCurrent thread: {current}, Main thread: {main}.");
        }
    }
}
