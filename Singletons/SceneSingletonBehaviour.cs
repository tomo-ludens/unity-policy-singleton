using Singletons.Core;
using Singletons.Policy;

namespace Singletons
{
    /// <summary>
    /// Scene-scoped singleton: destroyed on scene unload, no auto-creation.
    /// Instance access without a placed instance throws in DEV/EDITOR.
    /// </summary>
    public abstract class SceneSingletonBehaviour<T> : SingletonBehaviour<T, SceneScopedPolicy> where T : SceneSingletonBehaviour<T>
    {
    }
}