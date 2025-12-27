using UnityEngine;

namespace Foundation.Singletons
{
    /// <summary>
    /// Type-per-singleton base class for MonoBehaviour with soft reset per Play session.
    /// </summary>
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
        /// Returns null while quitting.
        /// </summary>
        public static T Instance
        {
            get
            {
                if (!Application.isPlaying)
                {
                    return Object.FindAnyObjectByType<T>(findObjectsInactive: FindInactivePolicy);
                }

                InvalidateInstanceCacheIfPlaySessionChanged();

                if (SingletonRuntime.IsQuitting) return null;
                if (_instance != null) return _instance;

                _instance = Object.FindAnyObjectByType<T>(findObjectsInactive: FindInactivePolicy);
                if (_instance != null) return _instance;

                var go = new GameObject(name: typeof(T).Name);
                _instance = go.AddComponent<T>();
                return _instance;
            }
        }

        /// <summary>
        /// Gets the singleton instance without creating one.
        /// Returns false while quitting or when no instance exists.
        /// </summary>
        public static bool TryGetInstance(out T instance)
        {
            if (!Application.isPlaying)
            {
                instance = Object.FindAnyObjectByType<T>(findObjectsInactive: FindInactivePolicy);
                return instance != null;
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

        private void Awake()
        {
            if (!Application.isPlaying) return;

            this.InitializeForCurrentPlaySessionIfNeeded();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;

            this.InitializeForCurrentPlaySessionIfNeeded();
        }

        private void OnDestroy()
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

                Destroy(obj: this.gameObject);
                return false;
            }

            // CRTP constraint covers most cases, but intermediate base classes can bypass it.
            var typedThis = this as T;
            if (typedThis == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError(
                    message: $"[{this.GetType().Name}] must inherit SingletonBehaviour<{this.GetType().Name}>, " +
                             $"not SingletonBehaviour<{typeof(T).Name}>.",
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
            // DontDestroyOnLoad requires root.
            if (this.transform.parent != null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning(
                    message: $"[{typeof(T).Name}] Reparented to root for DontDestroyOnLoad.",
                    context: this
                );
#endif
                this.transform.SetParent(parent: null, worldPositionStays: true);
            }

            if (this._isPersistent) return;

            DontDestroyOnLoad(target: this.gameObject);
            this._isPersistent = true;
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
    }
}
