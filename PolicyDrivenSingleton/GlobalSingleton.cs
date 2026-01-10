using TomoLudens.PolicySingleton.Core;
using TomoLudens.PolicySingleton.Policy;

namespace TomoLudens.PolicySingleton
{
    /// <summary>
    /// Base class for application-lifetime singletons that persist across all scene loads.
    /// </summary>
    /// <typeparam name="T">The concrete singleton type (must be sealed).</typeparam>
    /// <remarks>
    /// <para><b>Behavior:</b> Auto-creates on first access, applies <c>DontDestroyOnLoad</c>, destroys duplicates.</para>
    /// <para><b>Lifecycle:</b> Override <c>Awake</c>/<c>OnDestroy</c> for initialization/cleanup; base calls are required (checked at runtime via OnEnable).</para>
    /// <para><b>vs Scene:</b> Use <see cref="SceneSingleton{T}"/> for scene-local managers that reset on reload.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public sealed class AudioManager : GlobalSingleton&lt;AudioManager&gt;
    /// {
    ///     protected override void Awake()
    ///     {
    ///         base.Awake(); // Required
    ///         // Your initialization here
    ///     }
    /// }
    /// </code>
    /// </example>
    public abstract class GlobalSingleton<T>: SingletonBehaviour<T, PersistentPolicy>
        where T : GlobalSingleton<T>
    {
    }
}
