using System;
using Singletons.Policy;
using UnityEngine;

namespace Singletons.Core
{
    /// <summary>
    /// Core singleton implementation driven by policy.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item>All public API must be called from the main thread.</item>
    ///   <item>In DEV/EDITOR, missing instance with auto-create disabled throws.</item>
    ///   <item>Override Awake/OnEnable/OnDestroy requires calling base method.</item>
    ///   <item>Prefer OnSingletonAwake/OnSingletonDestroy over Awake/OnDestroy override.</item>
    /// </list>
    /// </remarks>
    public abstract class SingletonBehaviour<T, TPolicy> : MonoBehaviour
        where T : SingletonBehaviour<T, TPolicy>
        where TPolicy : struct, ISingletonPolicy
    {
        private const int UninitializedPlaySessionId = -1;
        private const FindObjectsInactive FindInactivePolicy = FindObjectsInactive.Exclude;

        private static readonly TPolicy Policy = default;

        // ReSharper disable once StaticMemberInGenericType
        private static T _instance;

        // ReSharper disable once StaticMemberInGenericType
        private static int _cachedPlaySessionId = UninitializedPlaySessionId;

        private int _initializedPlaySessionId = UninitializedPlaySessionId;
        private bool _isPersistent;

        /// <summary>
        /// Returns the singleton instance.
        /// Auto-creates if missing and policy allows (Play Mode only).
        /// </summary>
        public static T Instance
        {
            get
            {
                if (!Application.isPlaying)
                {
                    return AsExactType(
                        candidate: FindAnyObjectByType<T>(findObjectsInactive: FindInactivePolicy),
                        callerContext: $"{typeof(T).Name}.Instance[EditMode]"
                    );
                }

                if (!SingletonRuntime.AssertMainThread(callerContext: $"{typeof(T).Name}.Instance"))
                {
                    return null;
                }

                InvalidateInstanceCacheIfPlaySessionChanged();

                if (SingletonRuntime.IsQuitting) return null;
                if (_instance != null) return _instance;

                var candidate = FindAnyObjectByType<T>(findObjectsInactive: FindInactivePolicy);
                _instance = AsExactType(candidate: candidate, callerContext: $"{typeof(T).Name}.Instance[PlayMode]");
                if (_instance != null) return _instance;

                if (!Policy.AutoCreateIfMissing)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    throw new InvalidOperationException(
                        message: $"[{typeof(T).Name}] No instance found and auto-creation is disabled by policy.\n" +
                                 "Place an active instance in the scene."
                    );
#else
                    return null;
#endif
                }

                AssertNoInactiveInstanceExists();

                _instance = CreateInstance();
                return _instance;
            }
        }

        /// <summary>
        /// Gets the singleton instance without creating one.
        /// </summary>
        public static bool TryGetInstance(out T instance)
        {
            if (!Application.isPlaying)
            {
                instance = FindAnyObjectByType<T>(findObjectsInactive: FindInactivePolicy);
                instance = AsExactType(candidate: instance, callerContext: $"{typeof(T).Name}.TryGetInstance[EditMode]");
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

            var candidate = FindAnyObjectByType<T>(findObjectsInactive: FindInactivePolicy);
            candidate = AsExactType(candidate: candidate, callerContext: $"{typeof(T).Name}.TryGetInstance[PlayMode]");
            if (candidate != null)
            {
                _instance = candidate;
                instance = _instance;
                return true;
            }

            instance = null;
            return false;
        }

        /// <summary>
        /// Override requires calling base.Awake(). Prefer OnSingletonAwake().
        /// </summary>
        protected virtual void Awake()
        {
            if (!Application.isPlaying) return;

            this.InitializeForCurrentPlaySessionIfNeeded();
        }

        /// <summary>
        /// Override requires calling base.OnEnable().
        /// </summary>
        protected virtual void OnEnable()
        {
            if (!Application.isPlaying) return;

            this.InitializeForCurrentPlaySessionIfNeeded();
        }

        /// <summary>
        /// Override requires calling base.OnDestroy(). Prefer OnSingletonDestroy().
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (!ReferenceEquals(objA: _instance, objB: this)) return;

            _instance = null;
            this.OnSingletonDestroy();
        }

        /// <summary>
        /// Called once per Play session after singleton is established.
        /// </summary>
        protected virtual void OnSingletonAwake() { }

        /// <summary>
        /// Called when singleton instance is destroyed.
        /// </summary>
        protected virtual void OnSingletonDestroy() { }

        private static T CreateInstance()
        {
            var go = new GameObject(name: typeof(T).Name);

            if (Policy.PersistAcrossScenes)
            {
                DontDestroyOnLoad(target: go);
            }

            var instance = go.AddComponent<T>();
            instance._isPersistent = Policy.PersistAcrossScenes;

            // Ensure initialization even if derived Awake doesn't call base.
            instance.InitializeForCurrentPlaySessionIfNeeded();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                message: $"[{typeof(T).Name}] Auto-created.",
                context: instance
            );
#endif
            return instance;
        }

        private static T AsExactType(T candidate, string callerContext)
        {
            if (candidate == null) return null;

            if (candidate.GetType() == typeof(T))
            {
                return candidate;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError(
                message: $"[{typeof(T).Name}] Type mismatch found via '{callerContext}'.\n" +
                         $"Expected EXACT type '{typeof(T).Name}', but found '{candidate.GetType().Name}'.",
                context: candidate
            );
#endif

            if (Application.isPlaying)
            {
                Destroy(obj: candidate.gameObject);
            }

            return null;
        }

        private static void AssertNoInactiveInstanceExists()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var allInstances = FindObjectsByType<T>(findObjectsInactive: FindObjectsInactive.Include, sortMode: FindObjectsSortMode.None);

            foreach (var instance in allInstances)
            {
                if (!instance.isActiveAndEnabled)
                {
                    throw new InvalidOperationException(
                        message: $"[{typeof(T).Name}] Auto-create BLOCKED: inactive instance exists " +
                                 $"('{instance.name}', type: '{instance.GetType().Name}'). " +
                                 "Enable it or remove from scene."
                    );
                }
            }
#endif
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
                    message: $"[{typeof(T).Name}] Type mismatch. Expected='{typeof(T).Name}', Actual='{this.GetType().Name}', destroying '{this.name}'.",
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
                    message: $"[{typeof(T).Name}] Cast failure. Destroying '{this.name}'.",
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
            if (!Policy.PersistAcrossScenes) return;
            if (this._isPersistent) return;

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

            DontDestroyOnLoad(target: this.gameObject);
            this._isPersistent = true;
        }
    }
}