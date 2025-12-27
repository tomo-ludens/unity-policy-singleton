namespace Singletons.Policy
{
    /// <summary>
    /// Singleton policy interface.
    /// </summary>
    public interface ISingletonPolicy
    {
        bool PersistAcrossScenes { get; }
        bool AutoCreateIfMissing { get; }
    }
}
