using System;
using System.Threading;
using UnityEngine;

namespace PolicyDrivenSingleton.Core
{
    /// <summary>
    /// Manages singleton lifecycle state: Play session boundaries, quitting detection, and main-thread validation.
    /// </summary>
    /// <remarks>
    /// <para>Captures main-thread ID and SynchronizationContext at SubsystemRegistration for cross-thread posting.</para>
    /// <para>PlaySessionId increments each domain reload or Enter Play Mode, enabling stale-cache detection.</para>
    /// </remarks>
    internal static class SingletonRuntime
    {
        private static int _playSessionId = 1;
        private static int _isQuitting;

        private static int _mainThreadId;
        private static SynchronizationContext _mainThreadSyncContext;

        public static int PlaySessionId => Volatile.Read(location: ref _playSessionId);
        public static bool IsQuitting => Volatile.Read(location: ref _isQuitting) != 0;

        internal static void EnsureInitializedForCurrentPlaySession()
        {
            // Lazy initialization for cases where RuntimeInitializeOnLoadMethod hasn't run yet
            if (Volatile.Read(location: ref _mainThreadId) == 0)
            {
                Volatile.Write(location: ref _mainThreadId, value: Thread.CurrentThread.ManagedThreadId);
            }

            TryCaptureMainThreadContextIfOnMainThread();
        }

        internal static void ClearQuittingFlag()
            => Volatile.Write(location: ref _isQuitting, value: 0);

        /// <summary>
        /// Called from Unity's Application.quitting handler.
        /// Marks the runtime as "quitting" so singleton access can be blocked safely during shutdown.
        /// </summary>
        internal static void NotifyQuitting()
            => Volatile.Write(location: ref _isQuitting, value: 1);

        /// <summary>
        /// Validates current thread is main thread. Logs error if called from background thread.
        /// </summary>
        /// <returns><c>true</c> if on main thread; otherwise <c>false</c>.</returns>
        internal static bool ValidateMainThread(string callerContext)
        {
            // Ensure main thread ID is captured (handles case where RuntimeInitializeOnLoadMethod hasn't run yet)
            EnsureInitializedForCurrentPlaySession();

            if (IsMainThread())
            {
                return true;
            }

            SingletonLogger.LogError(
                message: $"Main-thread-only API '{callerContext}' was called from background thread (id={Thread.CurrentThread.ManagedThreadId})."
            );

            return false;
        }

        /// <summary>
        /// Posts action to main thread. Executes immediately if already on main thread.
        /// </summary>
        /// <returns><c>true</c> if posted/executed; <c>false</c> if action is null or SyncContext unavailable.</returns>
        internal static bool TryPostToMainThread(Action action, string callerContext = null)
        {
            if (action == null)
            {
                return false;
            }

            if (IsMainThread())
            {
                action();
                return true;
            }

            var sc = Volatile.Read(location: ref _mainThreadSyncContext);
            if (sc == null)
            {
                SingletonLogger.LogError(
                    message: $"Cannot post to main thread: SynchronizationContext not captured. Caller='{callerContext ?? "(unspecified)"}'."
                );
                return false;
            }

            sc.Post(
                d: _ =>
                {
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(exception: ex);
                    }
                },
                state: null
            );

            return true;
        }

#if UNITY_INCLUDE_TESTS
        /// <summary>
        /// Test-only: Increments PlaySessionId to simulate a new Play session boundary.
        /// </summary>
        internal static void AdvancePlaySessionIdForTesting()
        {
            Interlocked.Increment(location: ref _playSessionId);
        }
#endif

        private static bool IsMainThread()
        {
            int captured = Volatile.Read(location: ref _mainThreadId);
            return captured != 0 && Thread.CurrentThread.ManagedThreadId == captured;
        }

        private static void TryCaptureMainThreadContextIfOnMainThread()
        {
            if (!IsMainThread())
            {
                return;
            }

            var sc = SynchronizationContext.Current;
            if (sc != null && Volatile.Read(location: ref _mainThreadSyncContext) == null)
            {
                Volatile.Write(location: ref _mainThreadSyncContext, value: sc);
            }
        }

        [RuntimeInitializeOnLoadMethod(loadType: RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnSubsystemRegistration()
        {
            ClearQuittingFlag();

            // Increment to mark new Play session (initial value is 1, so first session becomes 2)
            Interlocked.Increment(location: ref _playSessionId);
            Volatile.Write(location: ref _mainThreadId, value: Thread.CurrentThread.ManagedThreadId);

            Application.quitting -= NotifyQuitting;
            Application.quitting += NotifyQuitting;

            TryCaptureMainThreadContextIfOnMainThread();
        }

        [RuntimeInitializeOnLoadMethod(loadType: RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void AfterAssembliesLoaded()
        {
            TryCaptureMainThreadContextIfOnMainThread();
        }
    }
}
