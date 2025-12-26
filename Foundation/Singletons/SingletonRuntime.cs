using UnityEngine;

namespace Foundation.Singletons
{
    /// <summary>
    /// Manages Play session state for singleton infrastructure.
    /// </summary>
    public static class SingletonRuntime
    {
        /// <summary>
        /// Increments once per Play session. Used to invalidate singleton caches.
        /// </summary>
        public static int PlaySessionId { get; private set; }

        /// <summary>
        /// True while the application is quitting.
        /// </summary>
        public static bool IsQuitting { get; private set; }

        // Time.realtimeSinceStartupAsDouble resets to 0 when entering Play Mode.
        // Used to detect new Play session even when static state persists (Domain Reload disabled).
        private static double _lastRealtimeSinceStartup = double.PositiveInfinity;
        private static bool _hasEverInitialized;

        [RuntimeInitializeOnLoadMethod(loadType: RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize() => EnsureInitializedForCurrentPlaySession();

        /// <summary>
        /// Ensures runtime state is initialized. Idempotent.
        /// </summary>
        internal static void EnsureInitializedForCurrentPlaySession()
        {
            if (!Application.isPlaying) return;

            var now = Time.realtimeSinceStartupAsDouble;
            var isNewSession = now < _lastRealtimeSinceStartup;

            if (!_hasEverInitialized || isNewSession)
            {
                _hasEverInitialized = true;

                if (PlaySessionId < int.MaxValue) PlaySessionId++;

                IsQuitting = false;

                // Unsubscribe first: Domain Reload disabled keeps static event subscribers.
                Application.quitting -= OnQuitting;
                Application.quitting += OnQuitting;
            }

            _lastRealtimeSinceStartup = now;
        }

        private static void OnQuitting() => IsQuitting = true;
    }
}
