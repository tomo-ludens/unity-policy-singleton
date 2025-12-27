using UnityEngine;

namespace Foundation.Singletons
{
    /// <summary>
    /// Type-per-singleton base class for MonoBehaviour with soft reset per Play session.
    /// </summary>
    /// <remarks>
    /// All public API (Instance, TryGetInstance) must be called from the main thread.
    /// </remarks>
    public abstract class SingletonBehaviour<T> : MonoBehaviour where T : SingletonBehaviour<T>
    {
        private const int UninitializedPlaySessionId = -1;
        private const FindObjectsInactive FindInactivePolicy = FindObjectsInactive.Exclude;

        // ReSharper disable once StaticMemberInGenericType
        private static T _instance;

        // ReSharper disable once StaticMemberInGenericType
        private static int _cachedPlaySessionId = UninitializedPlaySessionId;

        private int _initializedPlaySessionId = UninitializedPlaySessionId;
        private bool _isPersistent;

        /// <summary>
        /// Returns the singleton instance. Auto-creates if missing (Play Mode only).
        /// Returns null while quitting or if called from a background thread.
        /// </summary>
        /// <remarks>Must be called from main thread.</remarks>
        public static T Instance
        {
            get
            {
                if (!Application.isPlaying)
                {
                    return Object.FindAnyObjectByType<T>(findObjectsInactive: FindInactivePolicy);
                }

                if (!SingletonRuntime.AssertMainThread(callerContext: $"{typeof(T).Name}.Instance"))
                {
                    return null;
                }

                InvalidateInstanceCacheIfPlaySessionChanged();

                if (SingletonRuntime.IsQuitting) return null;
                if (_instance != null) return _instance;

                _instance = Object.FindAnyObjectByType<T>(findObjectsInactive: FindInactivePolicy);
                if (_instance != null) return _instance;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                var any = Object.FindAnyObjectByType<T>(findObjectsInactive: FindObjectsInactive.Include);
                if (any != null && !any.isActiveAndEnabled)
                {
                    Debug.LogWarning(
                        message: $"[{typeof(T).Name}] No ACTIVE instance found, but an INACTIVE instance exists ('{any.name}'). " +
                                 "Policy is Exclude, so a NEW instance will be auto-created (risk: later duplicate destruction).",
                        context: any
                    );
                }
#endif
                _instance = CreateInstance();
                return _instance;
            }
        }

        /// <summary>
        /// Gets the singleton instance without creating one.
        /// Returns false while quitting, when no instance exists, or if called from a background thread.
        /// </summary>
        /// <remarks>Must be called from main thread.</remarks>
        public static bool TryGetInstance(out T instance)
        {
            if (!Application.isPlaying)
            {
                instance = Object.FindAnyObjectByType<T>(findObjectsInactive: FindInactivePolicy);
                return instance != null;
            }

            if (!SingletonRuntime.AssertMainThread(callerContext: $"{typeof(T).Name}.TryGetInstance"))
            {
                instance = null;
                return false;
            }

            InvalidateInstanceCacheIfPlaySessionChanged();

            if (SingletonRuntime.IsQuitting)
            {
                instance = null;
                return false;
            }

            if (_instance != null)
            {
                instance = _instance;
                return true;
            }

            _instance = Object.FindAnyObjectByType<T>(findObjectsInactive: FindInactivePolicy);
            instance = _instance;
            return instance != null;
        }

        protected void Awake()
        {
            if (!Application.isPlaying) return;

            this.InitializeForCurrentPlaySessionIfNeeded();
        }

        protected void OnEnable()
        {
            if (!Application.isPlaying) return;

            this.InitializeForCurrentPlaySessionIfNeeded();
        }

        protected void OnDestroy()
        {
            if (!ReferenceEquals(objA: _instance, objB: this)) return;

            _instance = null;
            this.OnSingletonDestroy();
        }

        /// <summary>
        /// Called once per Play session after singleton is established.
        /// </summary>
        protected virtual void OnSingletonAwake()
        {
        }

        /// <summary>
        /// Called when singleton instance is destroyed.
        /// </summary>
        protected virtual void OnSingletonDestroy()
        {
        }

        private static T CreateInstance()
        {
            var go = new GameObject(name: typeof(T).Name);
            DontDestroyOnLoad(target: go);

            var instance = go.AddComponent<T>();
            instance._isPersistent = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                message: $"[{typeof(T).Name}] Auto-created (no active instance found; inactive objects excluded).",
                context: instance
            );
#endif
            return instance;
        }

        private static void InvalidateInstanceCacheIfPlaySessionChanged()
        {
            if (!Application.isPlaying) return;

            SingletonRuntime.EnsureInitializedForCurrentPlaySession();

            var current = SingletonRuntime.PlaySessionId;
            if (_cachedPlaySessionId == current) return;

            _cachedPlaySessionId = current;
            _instance = null;
        }

        private void InitializeForCurrentPlaySessionIfNeeded()
        {
            InvalidateInstanceCacheIfPlaySessionChanged();

            if (SingletonRuntime.IsQuitting)
            {
                Destroy(obj: this.gameObject);
                return;
            }

            if (!this.TryEstablishAsInstance()) return;

            var currentPlaySessionId = SingletonRuntime.PlaySessionId;
            if (this._initializedPlaySessionId == currentPlaySessionId) return;

            this.EnsurePersistent();

            this._initializedPlaySessionId = currentPlaySessionId;
            this.OnSingletonAwake();
        }

        private bool TryEstablishAsInstance()
        {
            if (_instance != null)
            {
                if (ReferenceEquals(objA: _instance, objB: this)) return true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning(
                    message: $"[{typeof(T).Name}] Duplicate detected. Existing='{_instance.name}', destroying '{this.name}'.",
                    context: this
                );
#endif
                Destroy(obj: this.gameObject);
                return false;
            }

            if (this.GetType() != typeof(T))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError(
                    message: $"[{typeof(T).Name}] Type mismatch detected. Expected='{typeof(T).Name}', Actual='{this.GetType().Name}', destroying '{this.name}'.",
                    context: this
                );
#endif
                Destroy(obj: this.gameObject);
                return false;
            }

            var typedThis = this as T;
            if (typedThis == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError(
                    message: $"[{typeof(T).Name}] Internal cast failure. Expected='{typeof(T).Name}', destroying '{this.name}'.",
                    context: this
                );
#endif
                Destroy(obj: this.gameObject);
                return false;
            }

            _instance = typedThis;
            return true;
        }

        private void EnsurePersistent()
        {
            // Already persistent (e.g., auto-created instance).
            if (this._isPersistent) return;

            // DontDestroyOnLoad requires root.
            if (this.transform.parent != null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning(
                    message: $"[{typeof(T).Name}] Reparented to root for DontDestroyOnLoad (was under '{this.transform.parent.name}').",
                    context: this
                );
#endif
                this.transform.SetParent(parent: null, worldPositionStays: true);
            }

            DontDestroyOnLoad(target: this.gameObject);
            this._isPersistent = true;
        }
    }
}
