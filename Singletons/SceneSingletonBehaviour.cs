using Singletons.Core;
using Singletons.Policy;

namespace Singletons
{
    /// <summary>
    /// Base class for scene-local singletons that are destroyed on scene unload.
    /// </summary>
    /// <typeparam name="T">The concrete singleton type (CRTP pattern).</typeparam>
    /// <remarks>
    /// <para><b>Behavior:</b></para>
    /// <list type="bullet">
    ///   <item>No <c>DontDestroyOnLoad</c>; instance is destroyed when the scene unloads.</item>
    ///   <item>No auto-creation; instance must be pre-placed in the scene.</item>
    ///   <item>Accessing <c>Instance</c> without placement throws (DEV/EDITOR) or returns null (Release).</item>
    /// </list>
    /// <para><b>Access Pattern:</b> Use <c>TryGetInstance(out T)</c> for safe access. Direct <c>Instance</c> assumes the instance exists.</para>
    /// <para><b>Lifecycle Hooks:</b> Override <c>OnSingletonAwake()</c> / <c>OnSingletonDestroy()</c> instead of Awake/OnDestroy.</para>
    /// <para><b>vs PersistentSingletonBehaviour:</b> Use this for scene-specific managers (LevelManager, BattleManager). Use <see cref="PersistentSingletonBehaviour{T}"/> for global managers that must persist.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public sealed class LevelManager : SceneSingletonBehaviour&lt;LevelManager&gt;
    /// {
    ///     protected override void OnSingletonAwake() { /* init */ }
    /// }
    /// // Safe access:
    /// if (LevelManager.TryGetInstance(out var mgr)) { mgr.DoSomething(); }
    /// </code>
    /// </example>
    public abstract class SceneSingletonBehaviour<T> : SingletonBehaviour<T, SceneScopedPolicy> where T : SceneSingletonBehaviour<T>
    {
    }
}
