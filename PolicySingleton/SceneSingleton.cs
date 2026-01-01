using TomoLudens.PolicySingleton.Core;
using TomoLudens.PolicySingleton.Policy;

namespace TomoLudens.PolicySingleton
{
    /// <summary>
    /// Base class for scene-local singletons that are destroyed on scene unload.
    /// </summary>
    /// <typeparam name="T">The concrete singleton type (must be sealed).</typeparam>
    /// <remarks>
    /// <para><b>Behavior:</b> No auto-creation (must be pre-placed), no <c>DontDestroyOnLoad</c>. Missing instance throws (DEV) or returns null (Release).</para>
    /// <para><b>Access:</b> Prefer <c>TryGetInstance(out T)</c> for safe access.</para>
    /// <para><b>Lifecycle:</b> Override <c>Awake</c>/<c>OnDestroy</c> for initialization/cleanup; base calls are required (checked at runtime via OnEnable).</para>
    /// <para><b>vs Persistent:</b> Use <see cref="GlobalSingleton{T}"/> for global managers that must persist.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public sealed class LevelManager : SceneSingleton&lt;LevelManager&gt;
    /// {
    ///     protected override void Awake()
    ///     {
    ///         base.Awake(); // Required
    ///         // Your initialization here
    ///     }
    /// }
    /// // Safe access:
    /// if (LevelManager.TryGetInstance(out var mgr)) { mgr.DoSomething(); }
    /// </code>
    /// </example>
    public abstract class SceneSingleton<T> : SingletonBehaviour<T, SceneScopedPolicy>
        where T : SceneSingleton<T>
    {
    }
}
