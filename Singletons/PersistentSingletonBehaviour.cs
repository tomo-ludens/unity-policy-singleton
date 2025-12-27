using Singletons.Core;
using Singletons.Policy;

namespace Singletons
{
    /// <summary>
    /// Persistent singleton: survives scene loads (DontDestroyOnLoad) with auto-creation.
    /// </summary>
    public abstract class PersistentSingletonBehaviour<T> : SingletonBehaviour<T, PersistentPolicy> where T : PersistentSingletonBehaviour<T>
    {
    }
}