using System.Threading;
using UnityEngine;

namespace Foundation.Singletons
{
    /// <summary>
    /// Manages Play session state for singleton infrastructure.
    /// </summary>
    public static class SingletonRuntime
    {
        private static int _lastBeginFrame = -1;
        private static int _mainThreadId = -1;

        /// <summary>
        /// Increments once per Play session. Used to invalidate singleton caches.
        /// Wraps around from int.MaxValue to int.MinValue (unchecked increment).
        /// </summary>
        public static int PlaySessionId { get; private set; }

        /// <summary>
        /// True while the application is quitting (or Play Mode is exiting in Editor).
        /// </summary>
        public static bool IsQuitting { get; private set; }

        internal static void EnsureInitializedForCurrentPlaySession()
        {
            if (!Application.isPlaying) return;

            // Domain Reload disabled keeps static event subscribers across Play sessions.
            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;

            // If SubsystemRegistration hasn't run for some reason, capture lazily (safe default path).
            if (_mainThreadId == -1)
            {
                TryLazyCaptureMainThreadId(callerContext: "EnsureInitializedForCurrentPlaySession");
            }

#if UNITY_EDITOR
            EnsureEditorHooks();
#endif
        }

        internal static bool AssertMainThread(string callerContext)
        {
            if (!Application.isPlaying) return true;

            // Fail-safe: try to initialize hooks and capture main thread id if it's still uninitialized.
            if (_mainThreadId == -1)
            {
                EnsureInitializedForCurrentPlaySession();

                if (_mainThreadId == -1 && !TryLazyCaptureMainThreadId(callerContext: callerContext))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogError(
                        message: $"[SingletonRuntime] {callerContext} must be called from the main thread, " +
                                 "but the main thread ID is not initialized yet (and could not be captured safely). " +
                                 $"Current thread: {Thread.CurrentThread.ManagedThreadId}."
                    );
#endif
                    return false;
                }
            }

            if (IsMainThread()) return true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError(
                message: $"[SingletonRuntime] {callerContext} must be called from the main thread. " +
                         $"Current thread: {Thread.CurrentThread.ManagedThreadId}, Main thread: {_mainThreadId}."
            );
#endif
            return false;
        }

        [RuntimeInitializeOnLoadMethod(loadType: RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void SubsystemRegistration()
        {
            if (!Application.isPlaying) return;

            _mainThreadId = Thread.CurrentThread.ManagedThreadId;

            BeginNewPlaySession();
        }

        private static void BeginNewPlaySession()
        {
            if (Time.frameCount == _lastBeginFrame) return;

            _lastBeginFrame = Time.frameCount;

            EnsureInitializedForCurrentPlaySession();

            unchecked
            {
                PlaySessionId++;
            }

            IsQuitting = false;
        }

        private static void OnQuitting() => IsQuitting = true;

        private static bool IsMainThread()
        {
            return _mainThreadId != -1 && _mainThreadId == Thread.CurrentThread.ManagedThreadId;
        }

        private static bool TryLazyCaptureMainThreadId(string callerContext)
        {
            if (_mainThreadId != -1) return true;
            if (!Application.isPlaying) return false;

            // Unity installs a SynchronizationContext on the main thread.
            // We only capture lazily when it's present to avoid promoting a background thread.
            if (SynchronizationContext.Current == null) return false;

            _mainThreadId = Thread.CurrentThread.ManagedThreadId;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                message: $"[SingletonRuntime] Main thread ID lazily captured as {_mainThreadId}. " +
                         $"Context: '{callerContext}'."
            );
#endif
            return true;

        }

#if UNITY_EDITOR

        private static bool _editorHooksInstalled;

        private static void EnsureEditorHooks()
        {
            if (_editorHooksInstalled) return;

            _editorHooksInstalled = true;

            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
            {
                IsQuitting = true;
            }
        }
#endif
    }
}
