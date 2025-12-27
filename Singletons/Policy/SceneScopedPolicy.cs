namespace Singletons.Policy
{
    /// <summary>
    /// Policy for scene-local singletons that are destroyed on scene unload.
    /// </summary>
    /// <remarks>
    /// <para><b>Configuration:</b></para>
    /// <list type="bullet">
    ///   <item><see cref="PersistAcrossScenes"/> = false — No <c>DontDestroyOnLoad</c>; destroyed with the scene.</item>
    ///   <item><see cref="AutoCreateIfMissing"/> = false — Throws (DEV/EDITOR) or returns null (Release) if accessed without pre-placed instance.</item>
    /// </list>
    /// <para><b>Use Cases:</b> LevelManager, SceneUIController, BattleManager — managers that should reset when the scene reloads.</para>
    /// <para><b>Access Pattern:</b> Use <c>TryGetInstance</c> for safe access. Direct <c>Instance</c> throws if no instance is placed in the scene.</para>
    /// <para><b>vs PersistentPolicy:</b> Use this when state should reset on scene change. Use <see cref="PersistentPolicy"/> for global managers that must persist.</para>
    /// </remarks>
    /// <seealso cref="Singletons.SceneSingletonBehaviour{T}"/>
    public readonly struct SceneScopedPolicy : ISingletonPolicy
    {
        /// <summary>
        /// Always <c>false</c>. Instance is destroyed when its scene unloads.
        /// </summary>
        public bool PersistAcrossScenes => false;

        /// <summary>
        /// Always <c>false</c>. Requires pre-placed instance; no auto-creation.
        /// </summary>
        public bool AutoCreateIfMissing => false;
    }
}
