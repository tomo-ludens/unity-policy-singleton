namespace Singletons.Policy
{
    /// <summary>
    /// Policy for application-lifetime singletons that persist across all scene loads.
    /// </summary>
    /// <remarks>
    /// <para><b>Configuration:</b></para>
    /// <list type="bullet">
    ///   <item><see cref="PersistAcrossScenes"/> = true — Applies <c>DontDestroyOnLoad</c>, auto-reparents to root if needed.</item>
    ///   <item><see cref="AutoCreateIfMissing"/> = true — Creates instance on first <c>Instance</c> access if none exists.</item>
    /// </list>
    /// <para><b>Use Cases:</b> AudioManager, SaveSystem, NetworkManager, Analytics — any manager required throughout the entire game session.</para>
    /// <para><b>Access Pattern:</b> <c>T.Instance</c> auto-creates if missing; returns null only while quitting or from background thread.</para>
    /// <para><b>vs SceneScopedPolicy:</b> Use this when the singleton must survive scene transitions. Use <see cref="SceneScopedPolicy"/> for scene-local managers that should reset on scene reload.</para>
    /// </remarks>
    /// <seealso cref="Singletons.PersistentSingletonBehaviour{T}"/>
    public readonly struct PersistentPolicy : ISingletonPolicy
    {
        /// <summary>
        /// Always <c>true</c>. Instance survives scene loads via <c>DontDestroyOnLoad</c>.
        /// </summary>
        public bool PersistAcrossScenes => true;

        /// <summary>
        /// Always <c>true</c>. Missing instance is auto-created on first access.
        /// </summary>
        public bool AutoCreateIfMissing => true;
    }
}
