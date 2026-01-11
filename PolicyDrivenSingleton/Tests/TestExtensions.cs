using PolicyDrivenSingleton.Core;
using PolicyDrivenSingleton.Policy;

namespace PolicyDrivenSingleton.Tests
{
    /// <summary>
    /// Test-only extension methods for singleton testing.
    /// These methods are only available in test assemblies and do not pollute production code.
    /// </summary>
    internal static class TestExtensions
    {
        /// <summary>
        /// Test-only: Resets static cache for this singleton type.
        /// </summary>
        public static void ResetStaticCacheForTesting<TSingleton, TPolicy>(this SingletonBehaviour<TSingleton, TPolicy> _)
            where TSingleton : SingletonBehaviour<TSingleton, TPolicy>
            where TPolicy : struct, ISingletonPolicy
        {
            SingletonBehaviour<TSingleton, TPolicy>.ResetStaticCacheForTesting();
        }

        /// <summary>
        /// Test-only: Resets the quitting flag in SingletonRuntime.
        /// </summary>
        public static void ResetQuittingFlagForTesting() => SingletonRuntime.ClearQuittingFlag();

        /// <summary>
        /// Test-only: Advances PlaySessionId to simulate a new Play session boundary.
        /// </summary>
        public static void AdvancePlaySessionIdForTesting() => SingletonRuntime.AdvancePlaySessionIdForTesting();
    }
}
