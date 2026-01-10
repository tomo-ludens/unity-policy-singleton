using System;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace PolicyDrivenSingleton.Core
{
    /// <summary>
    /// Singleton infrastructure logger (dev-only).
    /// </summary>
    /// <remarks>
    /// Calls are omitted at the call site unless at least one of these symbols is defined:
    /// UNITY_EDITOR, DEVELOPMENT_BUILD, UNITY_ASSERTIONS.
    /// </remarks>
    internal static class SingletonLogger
    {
        private const string EditorSymbol = "UNITY_EDITOR";
        private const string DevBuildSymbol = "DEVELOPMENT_BUILD";
        private const string AssertionsSymbol = "UNITY_ASSERTIONS";

        private const string InfraTag = "PolicySingleton";

        private static class TypeTagCache<T>
        {
            internal static readonly string Value = typeof(T).FullName ?? typeof(T).Name;
        }

        [Conditional(conditionString: EditorSymbol), Conditional(conditionString: DevBuildSymbol), Conditional(conditionString: AssertionsSymbol)]
        public static void Log<T>(string message, UnityEngine.Object context = null)
            => Debug.Log(message: $"[{TypeTagCache<T>.Value}] {message}", context: context);

        [Conditional(conditionString: EditorSymbol), Conditional(conditionString: DevBuildSymbol), Conditional(conditionString: AssertionsSymbol)]
        public static void LogWarning(string message, UnityEngine.Object context = null)
            => Debug.LogWarning(message: $"[{InfraTag}] {message}", context: context);

        [Conditional(conditionString: EditorSymbol), Conditional(conditionString: DevBuildSymbol), Conditional(conditionString: AssertionsSymbol)]
        public static void LogWarning<T>(string message, UnityEngine.Object context = null)
            => Debug.LogWarning(message: $"[{TypeTagCache<T>.Value}] {message}", context: context);

        [Conditional(conditionString: EditorSymbol), Conditional(conditionString: DevBuildSymbol), Conditional(conditionString: AssertionsSymbol)]
        public static void LogError(string message, UnityEngine.Object context = null)
            => Debug.LogError(message: $"[{InfraTag}] {message}", context: context);

        [Conditional(conditionString: EditorSymbol), Conditional(conditionString: DevBuildSymbol), Conditional(conditionString: AssertionsSymbol)]
        public static void LogError<T>(string message, UnityEngine.Object context = null)
            => Debug.LogError(message: $"[{TypeTagCache<T>.Value}] {message}", context: context);

        [Conditional(conditionString: EditorSymbol), Conditional(conditionString: DevBuildSymbol), Conditional(conditionString: AssertionsSymbol)]
        public static void ThrowInvalidOperation<T>(string message)
            => throw new InvalidOperationException(message: $"[{TypeTagCache<T>.Value}] {message}");
    }
}
