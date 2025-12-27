using Singletons.Core;
using Singletons.Policy;

namespace Singletons
{
    /// <summary>
    /// Base class for application-lifetime singletons that persist across all scene loads.
    /// </summary>
    /// <typeparam name="T">The concrete singleton type (CRTP pattern).</typeparam>
    /// <remarks>
    /// <para><b>Behavior:</b></para>
    /// <list type="bullet">
    ///   <item>Applies <c>DontDestroyOnLoad</c> automatically; survives scene transitions.</item>
    ///   <item>Auto-creates instance on first <c>Instance</c> access if none exists.</item>
    ///   <item>Duplicates are detected and destroyed with a warning.</item>
    /// </list>
    /// <para><b>Access Pattern:</b> <c>T.Instance</c> auto-creates if missing; returns null only while quitting or from background thread.</para>
    /// <para><b>Lifecycle Hooks:</b> Override <c>OnSingletonAwake()</c> / <c>OnSingletonDestroy()</c> instead of Awake/OnDestroy.</para>
    /// <para><b>vs SceneSingletonBehaviour:</b> Use this for global managers (AudioManager, SaveSystem). Use <see cref="SceneSingletonBehaviour{T}"/> for scene-local managers that reset on scene reload.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public sealed class AudioManager : PersistentSingletonBehaviour&lt;AudioManager&gt;
    /// {
    ///     protected override void OnSingletonAwake() { /* init */ }
    /// }
    /// </code>
    /// </example>
    public abstract class PersistentSingletonBehaviour<T> : SingletonBehaviour<T, PersistentPolicy> where T : PersistentSingletonBehaviour<T>
    {
    }
}
