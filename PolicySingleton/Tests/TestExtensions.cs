using TomoLudens.PolicySingleton.Core;
using TomoLudens.PolicySingleton.Policy;

namespace TomoLudens.PolicySingleton.Tests
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
        public static void ResetStaticCacheForTesting<TSingleton, TPolicy>(this SingletonBehaviour<TSingleton, TPolicy> singleton)
            where TSingleton : SingletonBehaviour<TSingleton, TPolicy>
            where TPolicy : struct, ISingletonPolicy
        {
            // Use reflection to access private static fields
            var type = typeof(SingletonBehaviour<TSingleton, TPolicy>);

            // Reset _instance field
            var instanceField = type.GetField(name: "_instance", bindingAttr: System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            instanceField?.SetValue(obj: null, value: null);

            // Reset _cachedPlaySessionId field
            var cachedIdField = type.GetField(name: "_cachedPlaySessionId", bindingAttr: System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            cachedIdField?.SetValue(obj: null, value: -1);
        }

        /// <summary>
        /// Test-only: Resets the quitting flag in SingletonRuntime.
        /// </summary>
        public static void ResetQuittingFlagForTesting()
        {
            // Use reflection to access private static field
            var type = typeof(SingletonRuntime);
            var field = type.GetField(name: "<IsQuitting>k__BackingField", bindingAttr: System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            field?.SetValue(obj: null, value: false);
        }

        /// <summary>
        /// Test-only: Advances PlaySessionId to simulate a new Play session boundary.
        /// </summary>
        public static void AdvancePlaySessionIdForTesting()
        {
            // PlaySessionId is an auto-property: backing field is &lt;PlaySessionId&gt;k__BackingField
            var type = typeof(SingletonRuntime);
            var field = type.GetField(name: "<PlaySessionId>k__BackingField", bindingAttr: System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (field == null) return;

            int current = (int)field.GetValue(obj: null);
            unchecked
            {
                field.SetValue(obj: null, value: current + 1);
            }
        }
    }
}
