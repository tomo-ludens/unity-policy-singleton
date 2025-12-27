namespace Singletons.Policy
{
    /// <summary>
    /// Scene-scoped singleton policy: no DontDestroyOnLoad + auto-create disabled.
    /// </summary>
    public readonly struct SceneScopedPolicy : ISingletonPolicy
    {
        public bool PersistAcrossScenes => false;
        public bool AutoCreateIfMissing => false;
    }
}
