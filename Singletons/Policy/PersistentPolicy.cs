namespace Singletons.Policy
{
    /// <summary>
    /// Persistent singleton policy: DontDestroyOnLoad + auto-create enabled.
    /// </summary>
    public readonly struct PersistentPolicy : ISingletonPolicy
    {
        public bool PersistAcrossScenes => true;
        public bool AutoCreateIfMissing => true;
    }
}
