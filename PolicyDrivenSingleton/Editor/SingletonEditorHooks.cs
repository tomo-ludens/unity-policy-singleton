using TomoLudens.PolicySingleton.Core;
using UnityEditor;

namespace TomoLudens.PolicySingleton.Editor
{
    /// <summary>
    /// Registers Editor event hooks for singleton infrastructure.
    /// </summary>
    [InitializeOnLoad]
    internal static class SingletonEditorHooks
    {
        static SingletonEditorHooks()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                SingletonRuntime.NotifyQuitting();
            }
        }
    }
}
