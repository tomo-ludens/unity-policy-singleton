#if UNITY_EDITOR || DEVELOPMENT_BUILD || UNITY_ASSERTIONS
#define TEST_IS_DEV
#endif

using System;
using System.Collections;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TomoLudens.PolicySingleton.Tests.Runtime
{
    internal static class TestBuildGuards
    {
#if TEST_IS_DEV
        public const bool IsDev = true;
#else
        public const bool IsDev = false;
#endif
    }

    /// <summary>
    /// Cumulative counter for observing logs by type and count during test execution.
    /// Holds the cumulative value at scope start to verify increments via delta comparison.
    /// </summary>
    internal static class TestLogCounter
    {
        private static int _sLog;
        private static int _sWarning;
        private static int _sError;
        private static int _sAssert;
        private static int _sException;

        private static bool _sInstalled;

        public readonly struct Snapshot
        {
            public readonly int Log;
            public readonly int Warning;
            public readonly int Error;
            public readonly int Assert;
            public readonly int Exception;

            public Snapshot(int log, int warning, int error, int assert, int exception)
            {
                Log = log;
                Warning = warning;
                Error = error;
                Assert = assert;
                Exception = exception;
            }

            public Snapshot Delta(Snapshot baseline)
            {
                return new Snapshot(
                    log: Log - baseline.Log,
                    warning: Warning - baseline.Warning,
                    error: Error - baseline.Error,
                    assert: Assert - baseline.Assert,
                    exception: Exception - baseline.Exception
                );
            }
        }

        public static void Install()
        {
            if (_sInstalled)
            {
                return;
            }

            _sInstalled = true;
            Application.logMessageReceivedThreaded += OnLogMessageReceivedThreaded;
        }

        public static void Uninstall()
        {
            if (!_sInstalled)
            {
                return;
            }

            _sInstalled = false;
            Application.logMessageReceivedThreaded -= OnLogMessageReceivedThreaded;
        }

        public static Snapshot Take()
        {
            return new Snapshot(
                log: Volatile.Read(location: ref _sLog),
                warning: Volatile.Read(location: ref _sWarning),
                error: Volatile.Read(location: ref _sError),
                assert: Volatile.Read(location: ref _sAssert),
                exception: Volatile.Read(location: ref _sException)
            );
        }

        private static void OnLogMessageReceivedThreaded(string condition, string stackTrace, LogType type)
        {
            switch (type)
            {
                case LogType.Error:
                    Interlocked.Increment(location: ref _sError);
                    return;
                case LogType.Assert:
                    Interlocked.Increment(location: ref _sAssert);
                    return;
                case LogType.Exception:
                    Interlocked.Increment(location: ref _sException);
                    return;
                case LogType.Warning:
                    Interlocked.Increment(location: ref _sWarning);
                    return;
                case LogType.Log:
                default:
                    Interlocked.Increment(location: ref _sLog);
                    return;
            }
        }
    }

#if TEST_IS_DEV
    /// <summary>
    /// Scope that locally suppresses expected Error/Exception logs from causing
    /// test failures due to "Unexpected log" detection.
    /// </summary>
    internal readonly struct IgnoreFailingMessagesScope : IDisposable
    {
        private readonly bool _previous;

        public IgnoreFailingMessagesScope(bool enabled)
        {
            _previous = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = enabled;
        }

        public void Dispose()
        {
            LogAssert.ignoreFailingMessages = _previous;
        }
    }
#endif

    [SetUpFixture]
    public sealed class PolicySingletonTestSetup
    {
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            TestLogCounter.Install();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            TestLogCounter.Uninstall();
        }
    }

    public sealed class TestPersistentSingleton : GlobalSingleton<TestPersistentSingleton>
    {
        public int AwakeCount { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            AwakeCount++;
        }
    }

    public sealed class TestSceneSingleton : SceneSingleton<TestSceneSingleton>
    {
        public bool WasInitialized { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            WasInitialized = true;
        }
    }

    // For type mismatch tests - a base class that is NOT sealed
    public class TestBaseSingleton : GlobalSingleton<TestBaseSingleton>
    {
    }

    // Derived class that should be rejected
    public sealed class TestDerivedSingleton : TestBaseSingleton
    {
    }

    // For inactive instance tests
    public sealed class TestInactiveSingleton : GlobalSingleton<TestInactiveSingleton>
    {
    }

    public sealed class TestSoftResetSingleton : GlobalSingleton<TestSoftResetSingleton>
    {
        public int AwakeCalls { get; private set; }

        public int PlaySessionStartCalls { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            AwakeCalls++;
        }

        protected override void OnPlaySessionStart()
        {
            PlaySessionStartCalls++;
        }
    }

    public sealed class TestSingletonWithoutBaseAwake : GlobalSingleton<TestSingletonWithoutBaseAwake>
    {
        public bool AwakeWasCalled { get; private set; }

        protected override void Awake()
        {
            AwakeWasCalled = true;
        }
    }

    public sealed class GameManager : GlobalSingleton<GameManager>
    {
        public int PlayerScore { get; private set; }
        public string CurrentLevel { get; private set; }
        public bool IsGamePaused { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            PlayerScore = 0;
            CurrentLevel = "MainMenu";
            IsGamePaused = false;
        }

        public void AddScore(int points)
        {
            PlayerScore += points;
        }

        public void SetLevel(string levelName)
        {
            CurrentLevel = levelName;
        }

        public void PauseGame()
        {
            IsGamePaused = true;
        }

        public void ResumeGame()
        {
            IsGamePaused = false;
        }
    }

    public sealed class LevelController : SceneSingleton<LevelController>
    {
        public string LevelName { get; private set; }
        public int EnemyCount { get; private set; }
        public bool IsLevelComplete { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            LevelName = "DefaultLevel";
            EnemyCount = 5;
            IsLevelComplete = false;
        }

        public void SetLevelInfo(string levelName, int enemies)
        {
            LevelName = levelName;
            EnemyCount = enemies;
        }

        public void CompleteLevel()
        {
            IsLevelComplete = true;
        }
    }

    [TestFixture]
    public class PersistentSingletonTests
    {
        [TearDown]
        public void TearDown()
        {
            if (TestPersistentSingleton.TryGetInstance(instance: out var instance))
            {
                UnityEngine.Object.DestroyImmediate(obj: instance.gameObject);
            }

            default(TestPersistentSingleton).ResetStaticCacheForTesting();
        }

        [UnityTest]
        public IEnumerator Instance_AutoCreates_WhenNotExists()
        {
            var instance = TestPersistentSingleton.Instance;
            yield return null;

            Assert.IsNotNull(anObject: instance, message: "Instance should be auto-created");
            Assert.AreEqual(expected: 1, actual: instance.AwakeCount, message: "Awake should be called once");
        }

        [UnityTest]
        public IEnumerator Instance_ReturnsSameInstance_OnMultipleAccess()
        {
            var first = TestPersistentSingleton.Instance;
            yield return null;

            var second = TestPersistentSingleton.Instance;
            yield return null;

            Assert.AreSame(expected: first, actual: second, message: "Multiple accesses should return the same instance");
            Assert.AreEqual(expected: 1, actual: first.AwakeCount, message: "Awake should be called only once");
        }

        [UnityTest]
        public IEnumerator TryGetInstance_ReturnsFalse_WhenNotExists()
        {
            bool result = TestPersistentSingleton.TryGetInstance(instance: out var instance);
            yield return null;

            Assert.IsFalse(condition: result, message: "TryGetInstance should return false when no instance exists");
            Assert.IsNull(anObject: instance, message: "Out parameter should be null");
        }

        [UnityTest]
        public IEnumerator TryGetInstance_ReturnsTrue_WhenExists()
        {
            var created = TestPersistentSingleton.Instance;
            yield return null;

            bool result = TestPersistentSingleton.TryGetInstance(instance: out var instance);

            Assert.IsTrue(condition: result, message: "TryGetInstance should return true when instance exists");
            Assert.AreSame(expected: created, actual: instance, message: "Should return the same instance");
        }

        [UnityTest]
        public IEnumerator TryGetInstance_DoesNotAutoCreate()
        {
            TestPersistentSingleton.TryGetInstance(instance: out _);
            yield return null;

            bool exists = TestPersistentSingleton.TryGetInstance(instance: out _);

            Assert.IsFalse(condition: exists, message: "TryGetInstance should not auto-create");
        }

        [UnityTest]
        public IEnumerator Duplicate_IsDestroyed()
        {
            var first = TestPersistentSingleton.Instance;
            yield return null;

            var duplicateGo = new GameObject(name: "Duplicate");
            duplicateGo.AddComponent<TestPersistentSingleton>();
            yield return null;

            Assert.AreSame(expected: first, actual: TestPersistentSingleton.Instance, message: "Original instance should remain");
            Assert.AreEqual(
                expected: 1,
                actual: UnityEngine.Object.FindObjectsByType<TestPersistentSingleton>(sortMode: FindObjectsSortMode.None).Length,
                message: "Only one instance should exist"
            );
        }

        [UnityTest]
        public IEnumerator Instance_HasDontDestroyOnLoad()
        {
            var instance = TestPersistentSingleton.Instance;
            yield return null;

            Assert.IsTrue(
                condition: instance.gameObject.scene.name == "DontDestroyOnLoad" || instance.gameObject.scene.buildIndex == -1,
                message: "Persistent singleton should be in DontDestroyOnLoad scene"
            );
        }
    }

    [TestFixture]
    public class SoftResetTests
    {
        [TearDown]
        public void TearDown()
        {
            if (TestSoftResetSingleton.TryGetInstance(instance: out var instance))
            {
                UnityEngine.Object.DestroyImmediate(obj: instance.gameObject);
            }

            default(TestSoftResetSingleton).ResetStaticCacheForTesting();
        }

        [UnityTest]
        public IEnumerator Reinitializes_PerPlaySession_WhenPlaySessionIdChanges()
        {
            var instance = TestSoftResetSingleton.Instance;
            yield return null;

            Assert.IsNotNull(anObject: instance);
            Assert.AreEqual(expected: 1, actual: instance.AwakeCalls, message: "Awake should run once per GameObject lifetime");
            Assert.AreEqual(expected: 1, actual: instance.PlaySessionStartCalls, message: "OnPlaySessionStart should run on first establishment");

            TestExtensions.AdvancePlaySessionIdForTesting();

            var sameInstance = TestSoftResetSingleton.Instance;
            yield return null;

            Assert.AreSame(expected: instance, actual: sameInstance, message: "Instance should be re-used (not recreated) across play session boundary");
            Assert.AreEqual(expected: 1, actual: sameInstance.AwakeCalls, message: "Awake should not be re-run on play session boundary");
            Assert.AreEqual(expected: 2, actual: sameInstance.PlaySessionStartCalls, message: "OnPlaySessionStart should run once per Play session");
            Assert.AreEqual(
                expected: 1,
                actual: UnityEngine.Object.FindObjectsByType<TestSoftResetSingleton>(sortMode: FindObjectsSortMode.None).Length,
                message: "Only one instance should exist"
            );
        }
    }

    [TestFixture]
    public class SceneSingletonTests
    {
        [TearDown]
        public void TearDown()
        {
            if (TestSceneSingleton.TryGetInstance(instance: out var instance))
            {
                UnityEngine.Object.DestroyImmediate(obj: instance.gameObject);
            }

            default(TestSceneSingleton).ResetStaticCacheForTesting();
        }

        [UnityTest]
        public IEnumerator Instance_ReturnsPlacedInstance()
        {
            var go = new GameObject(name: "TestSceneSingleton");
            var placed = go.AddComponent<TestSceneSingleton>();
            yield return null;

            var instance = TestSceneSingleton.Instance;

            Assert.AreSame(expected: placed, actual: instance, message: "Should return the placed instance");
            Assert.IsTrue(condition: instance.WasInitialized, message: "Awake should be called");
        }

        [UnityTest]
        public IEnumerator TryGetInstance_ReturnsFalse_WhenNotPlaced()
        {
            bool result = TestSceneSingleton.TryGetInstance(instance: out var instance);
            yield return null;

            Assert.IsFalse(condition: result, message: "TryGetInstance should return false when not placed");
            Assert.IsNull(anObject: instance, message: "Instance should be null");
        }

        [UnityTest]
        public IEnumerator TryGetInstance_ReturnsTrue_WhenPlaced()
        {
            var go = new GameObject(name: "TestSceneSingleton");
            go.AddComponent<TestSceneSingleton>();
            yield return null;

            bool result = TestSceneSingleton.TryGetInstance(instance: out var instance);

            Assert.IsTrue(condition: result, message: "TryGetInstance should return true when placed");
            Assert.IsNotNull(anObject: instance, message: "Instance should not be null");
        }

        [UnityTest]
        public IEnumerator SceneSingleton_DoesNotHaveDontDestroyOnLoad()
        {
            var go = new GameObject(name: "TestSceneSingleton");
            go.AddComponent<TestSceneSingleton>();
            yield return null;

            var instance = TestSceneSingleton.Instance;

            Assert.AreNotEqual(expected: "DontDestroyOnLoad", actual: instance.gameObject.scene.name, message: "Scene singleton should NOT be in DontDestroyOnLoad");
        }

        [UnityTest]
        public IEnumerator Duplicate_IsDestroyed()
        {
            var go1 = new GameObject(name: "First");
            var first = go1.AddComponent<TestSceneSingleton>();
            yield return null;

            var go2 = new GameObject(name: "Second");
            go2.AddComponent<TestSceneSingleton>();
            yield return null;

            Assert.AreSame(expected: first, actual: TestSceneSingleton.Instance, message: "First instance should remain");
            Assert.AreEqual(
                expected: 1,
                actual: UnityEngine.Object.FindObjectsByType<TestSceneSingleton>(sortMode: FindObjectsSortMode.None).Length,
                message: "Only one instance should exist"
            );
        }
    }

    [TestFixture]
    public class InactiveInstanceTests
    {
        [TearDown]
        public void TearDown()
        {
            var allInstances = UnityEngine.Object.FindObjectsByType<TestInactiveSingleton>(
                findObjectsInactive: FindObjectsInactive.Include,
                sortMode: FindObjectsSortMode.None
            );

            foreach (var instance in allInstances)
            {
                UnityEngine.Object.DestroyImmediate(obj: instance.gameObject);
            }

            default(TestInactiveSingleton).ResetStaticCacheForTesting();
        }

        [UnityTest]
        public IEnumerator Instance_ThrowsInDev_WhenInactiveInstanceExists()
        {
            var go = new GameObject(name: "InactiveSingleton");
            go.AddComponent<TestInactiveSingleton>();
            go.SetActive(value: false);

            default(TestInactiveSingleton).ResetStaticCacheForTesting();
            yield return null;

#if TEST_IS_DEV
            Assert.Throws<InvalidOperationException>(
                code: () => { _ = TestInactiveSingleton.Instance; },
                message: "Should throw when inactive instance exists - auto-create blocked (dev-only)"
            );
#else
            var instance = TestInactiveSingleton.Instance;
            Assert.IsNotNull(anObject: instance, message: "Should auto-create in non-dev build");
#endif
        }

        [UnityTest]
        public IEnumerator TryGetInstance_ReturnsFalse_WhenOnlyInactiveInstanceExists()
        {
            var go = new GameObject(name: "InactiveSingleton");
            go.AddComponent<TestInactiveSingleton>();
            go.SetActive(value: false);

            default(TestInactiveSingleton).ResetStaticCacheForTesting();
            yield return null;

            bool result = TestInactiveSingleton.TryGetInstance(instance: out var instance);

            Assert.IsFalse(condition: result, message: "TryGetInstance should return false when only inactive exists");
            Assert.IsNull(anObject: instance, message: "Instance should be null");
        }

        [UnityTest]
        public IEnumerator DisabledComponent_ThrowsInDev()
        {
            var go = new GameObject(name: "DisabledComponent");
            var comp = go.AddComponent<TestInactiveSingleton>();
            yield return null;

            comp.enabled = false;
            default(TestInactiveSingleton).ResetStaticCacheForTesting();
            yield return null;

#if TEST_IS_DEV
            Assert.Throws<InvalidOperationException>(
                code: () => { _ = TestInactiveSingleton.Instance; },
                message: "Should throw when component is disabled (dev-only)"
            );
#else
            var instance = TestInactiveSingleton.Instance;
            Assert.IsNull(anObject: instance, message: "Should return null in non-dev build");
#endif
        }
    }

    [TestFixture]
    public class TypeMismatchTests
    {
        [TearDown]
        public void TearDown()
        {
            var baseInstances = UnityEngine.Object.FindObjectsByType<TestBaseSingleton>(
                findObjectsInactive: FindObjectsInactive.Include,
                sortMode: FindObjectsSortMode.None
            );

            foreach (var instance in baseInstances)
            {
                UnityEngine.Object.DestroyImmediate(obj: instance.gameObject);
            }

            default(TestBaseSingleton).ResetStaticCacheForTesting();
        }

        [UnityTest]
        public IEnumerator DerivedClass_IsRejected_AndDestroyed()
        {
            var start = TestLogCounter.Take();

#if TEST_IS_DEV
            using (new IgnoreFailingMessagesScope(enabled: true))
            {
                var go = new GameObject(name: "DerivedSingleton");
                go.AddComponent<TestDerivedSingleton>();
                yield return null;
            }

            var deltaDev = TestLogCounter.Take().Delta(baseline: start);
            Assert.AreEqual(expected: 1, actual: deltaDev.Error, message: "Type mismatch should emit exactly one error log (dev-only)");
            Assert.AreEqual(expected: 0, actual: deltaDev.Exception, message: "No exceptions expected (dev-only)");
            Assert.AreEqual(expected: 0, actual: deltaDev.Assert, message: "No asserts expected (dev-only)");
#else
            var go = new GameObject(name: "DerivedSingleton");
            go.AddComponent<TestDerivedSingleton>();
            yield return null;
#endif

            var instance = TestBaseSingleton.Instance;
            yield return null;

            Assert.IsNotNull(anObject: instance, message: "Should auto-create correct type");
            Assert.AreEqual(expected: typeof(TestBaseSingleton), actual: instance.GetType(), message: "Instance should be exact type, not derived");

            var derivedInstances = UnityEngine.Object.FindObjectsByType<TestDerivedSingleton>(
                findObjectsInactive: FindObjectsInactive.Include,
                sortMode: FindObjectsSortMode.None
            );
            Assert.AreEqual(expected: 0, actual: derivedInstances.Length, message: "Derived singleton instance should be destroyed");
        }

        [UnityTest]
        public IEnumerator BaseClass_IsAccepted()
        {
            var go = new GameObject(name: "BaseSingleton");
            var placed = go.AddComponent<TestBaseSingleton>();
            yield return null;

            var instance = TestBaseSingleton.Instance;

            Assert.AreSame(expected: placed, actual: instance, message: "Base class instance should be accepted");
            Assert.AreEqual(expected: typeof(TestBaseSingleton), actual: instance.GetType());
        }
    }

    [TestFixture]
    public class ThreadSafetyTests
    {
        private const float ThreadTimeoutSeconds = 60f;

        [TearDown]
        public void TearDown()
        {
            if (TestPersistentSingleton.TryGetInstance(instance: out var instance))
            {
                UnityEngine.Object.DestroyImmediate(obj: instance.gameObject);
            }

            default(TestPersistentSingleton).ResetStaticCacheForTesting();
        }

        private static IEnumerator WaitForThread(Thread thread, string context)
        {
            float start = Time.realtimeSinceStartup;

            while (thread.IsAlive)
            {
                if (Time.realtimeSinceStartup - start > ThreadTimeoutSeconds)
                {
                    Assert.Fail(message: $"Timed out waiting for background thread: {context}");
                }

                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator BackgroundThread_Instance_ReturnsNull_And_LogsOnceInDev()
        {
#if !TEST_IS_DEV
            Assert.Ignore(message: "Thread-safety log verification is dev-only.");
            yield break;
#else
            _ = TestPersistentSingleton.Instance;
            yield return null;

            var start = TestLogCounter.Take();
            TestPersistentSingleton backgroundResult = null;

            using (new IgnoreFailingMessagesScope(enabled: true))
            {
                var thread = new Thread(start: () =>
                {
                    backgroundResult = TestPersistentSingleton.Instance;
                });

                thread.Start();
                yield return WaitForThread(thread: thread, context: "BackgroundThread_Instance_ReturnsNull_And_LogsOnceInDev");
            }

            Assert.IsNull(anObject: backgroundResult, message: "Instance should return null from background thread");

            var delta = TestLogCounter.Take().Delta(baseline: start);
            Assert.AreEqual(expected: 1, actual: delta.Error, message: "Background thread Instance access should log exactly one error (dev-only)");
            Assert.AreEqual(expected: 0, actual: delta.Exception, message: "No exceptions expected (dev-only)");
            Assert.AreEqual(expected: 0, actual: delta.Assert, message: "No asserts expected (dev-only)");
#endif
        }

        [UnityTest]
        public IEnumerator BackgroundThread_TryGetInstance_ReturnsFalse_And_LogsOnceInDev()
        {
#if !TEST_IS_DEV
            Assert.Ignore(message: "Thread-safety log verification is dev-only.");
            yield break;
#else
            _ = TestPersistentSingleton.Instance;
            yield return null;

            var start = TestLogCounter.Take();

            bool tryGetResult = true;
            TestPersistentSingleton backgroundInstance = null;

            using (new IgnoreFailingMessagesScope(enabled: true))
            {
                var thread = new Thread(start: () =>
                {
                    tryGetResult = TestPersistentSingleton.TryGetInstance(instance: out backgroundInstance);
                });

                thread.Start();
                yield return WaitForThread(thread: thread, context: "BackgroundThread_TryGetInstance_ReturnsFalse_And_LogsOnceInDev");
            }

            Assert.IsFalse(condition: tryGetResult, message: "TryGetInstance should return false from background thread");
            Assert.IsNull(anObject: backgroundInstance, message: "Instance should be null from background thread");

            var delta = TestLogCounter.Take().Delta(baseline: start);
            Assert.AreEqual(expected: 1, actual: delta.Error, message: "Background thread TryGetInstance should log exactly one error (dev-only)");
            Assert.AreEqual(expected: 0, actual: delta.Exception, message: "No exceptions expected (dev-only)");
            Assert.AreEqual(expected: 0, actual: delta.Assert, message: "No asserts expected (dev-only)");
#endif
        }

        [UnityTest]
        public IEnumerator MainThread_Instance_AccessIsSafe()
        {
            Exception mainThreadException = null;
            TestPersistentSingleton instance = null;

            try
            {
                instance = TestPersistentSingleton.Instance;
            }
            catch (Exception ex)
            {
                mainThreadException = ex;
            }

            Assert.IsNull(anObject: mainThreadException, message: "No exception should be thrown on main thread");
            Assert.IsNotNull(anObject: instance, message: "Instance should be created successfully on main thread");
            Assert.IsInstanceOf<TestPersistentSingleton>(actual: instance, message: "Should return correct type");

            yield return null;
        }

        [UnityTest]
        public IEnumerator MainThread_TryGetInstance_AccessIsSafe()
        {
            var createdInstance = TestPersistentSingleton.Instance;
            yield return null;

            bool result = TestPersistentSingleton.TryGetInstance(instance: out var instance);

            Assert.IsTrue(condition: result, message: "TryGetInstance should return true after creation");
            Assert.IsNotNull(anObject: instance, message: "Instance should be retrieved successfully");
            Assert.AreSame(expected: createdInstance, actual: instance, message: "Should return the same instance");

            yield return null;
        }

        [UnityTest]
        public IEnumerator ThreadSafety_Isolation_BetweenOperations()
        {
            var firstInstance = TestPersistentSingleton.Instance;
            yield return null;

            Assert.IsNotNull(anObject: firstInstance, message: "Should create instance on main thread");

            for (int i = 0; i < 5; i++)
            {
                var instance = TestPersistentSingleton.Instance;
                Assert.AreSame(expected: firstInstance, actual: instance, message: $"Instance call {i} should return same instance on main thread");
                yield return null;
            }

            bool tryResult = TestPersistentSingleton.TryGetInstance(instance: out var tryInstance);
            Assert.IsTrue(condition: tryResult, message: "TryGetInstance should succeed on main thread");
            Assert.AreSame(expected: firstInstance, actual: tryInstance, message: "TryGetInstance should return same instance");
        }

        [UnityTest]
        public IEnumerator ThreadSafety_ValidationLayer_PreventsBackgroundAccess_And_LogsOnceInDev()
        {
#if !TEST_IS_DEV
            Assert.Ignore(message: "Thread-safety log verification is dev-only.");
            yield break;
#else
            _ = TestPersistentSingleton.Instance;
            yield return null;

            var start = TestLogCounter.Take();
            TestPersistentSingleton backgroundResult = null;

            using (new IgnoreFailingMessagesScope(enabled: true))
            {
                var thread = new Thread(start: () =>
                {
                    backgroundResult = TestPersistentSingleton.Instance;
                });

                thread.Start();
                yield return WaitForThread(thread: thread, context: "ThreadSafety_ValidationLayer_PreventsBackgroundAccess_And_LogsOnceInDev");
            }

            Assert.IsNull(anObject: backgroundResult, message: "Background thread access should return null");

            var delta = TestLogCounter.Take().Delta(baseline: start);
            Assert.AreEqual(expected: 1, actual: delta.Error, message: "Validation layer should log exactly one error (dev-only)");
            Assert.AreEqual(expected: 0, actual: delta.Exception, message: "No exceptions expected (dev-only)");
            Assert.AreEqual(expected: 0, actual: delta.Assert, message: "No asserts expected (dev-only)");
#endif
        }

        [Test]
        public void ThreadSafety_MainThreadValidation_DoesNotInterfere()
        {
            Assert.DoesNotThrow(code: () =>
            {
                var instance = TestPersistentSingleton.Instance;
                Assert.IsNotNull(anObject: instance, message: "Instance creation should work on main thread");
            }, message: "Instance access should not throw on main thread");

            Assert.DoesNotThrow(code: () =>
            {
                bool result = TestPersistentSingleton.TryGetInstance(instance: out var instance);
                Assert.IsTrue(condition: result, message: "TryGetInstance should succeed");
                Assert.IsNotNull(anObject: instance, message: "Instance should be retrieved");
            }, message: "TryGetInstance should not throw on main thread");
        }
    }

    [TestFixture]
    public class LifecycleTests
    {
        [TearDown]
        public void TearDown()
        {
            if (TestPersistentSingleton.TryGetInstance(instance: out var instance))
            {
                UnityEngine.Object.DestroyImmediate(obj: instance.gameObject);
            }

            default(TestPersistentSingleton).ResetStaticCacheForTesting();
        }

        [UnityTest]
        public IEnumerator OnDestroy_CleansUpInstance()
        {
            var instance = TestPersistentSingleton.Instance;
            yield return null;

            UnityEngine.Object.DestroyImmediate(obj: instance.gameObject);
            yield return null;

            bool exists = TestPersistentSingleton.TryGetInstance(instance: out _);
            Assert.IsFalse(condition: exists, message: "Instance should not exist after destruction");
        }

        [UnityTest]
        public IEnumerator Instance_CanBeRecreated_AfterDestruction()
        {
            var first = TestPersistentSingleton.Instance;
            yield return null;

            UnityEngine.Object.DestroyImmediate(obj: first.gameObject);
            default(TestPersistentSingleton).ResetStaticCacheForTesting();
            yield return null;

            var second = TestPersistentSingleton.Instance;
            yield return null;

            Assert.IsNotNull(anObject: second, message: "New instance should be created");
            Assert.AreNotSame(expected: first, actual: second, message: "Should be a different instance");
            Assert.AreEqual(expected: 1, actual: second.AwakeCount, message: "New instance should have fresh AwakeCount");
        }
    }

    [TestFixture]
    public class SceneSingletonEdgeCaseTests
    {
        [TearDown]
        public void TearDown()
        {
            if (TestSceneSingleton.TryGetInstance(instance: out var instance))
            {
                UnityEngine.Object.DestroyImmediate(obj: instance.gameObject);
            }

            default(TestSceneSingleton).ResetStaticCacheForTesting();
        }

        [UnityTest]
        public IEnumerator Instance_ThrowsInDev_WhenNotPlaced()
        {
#if TEST_IS_DEV
            Assert.Throws<InvalidOperationException>(
                code: () => { _ = TestSceneSingleton.Instance; },
                message: "Should throw when SceneSingleton is not placed (dev-only)"
            );
#else
            var instance = TestSceneSingleton.Instance;
            Assert.IsNull(anObject: instance, message: "Should return null when not placed (non-dev build)");
#endif
            yield return null;
        }

        [UnityTest]
        public IEnumerator SceneSingleton_DoesNotAutoCreate()
        {
            TestSceneSingleton.TryGetInstance(instance: out var before);
            yield return null;

            TestSceneSingleton.TryGetInstance(instance: out var after);

            Assert.IsNull(anObject: before, message: "Should not exist before");
            Assert.IsNull(anObject: after, message: "Should still not exist - no auto-creation");
        }
    }

    [TestFixture]
    public class PolicyBehaviorTests
    {
        [TearDown]
        public void TearDown()
        {
            if (TestPersistentSingleton.TryGetInstance(instance: out var persistent))
            {
                UnityEngine.Object.DestroyImmediate(obj: persistent.gameObject);
            }

            if (TestSceneSingleton.TryGetInstance(instance: out var scene))
            {
                UnityEngine.Object.DestroyImmediate(obj: scene.gameObject);
            }

            default(TestPersistentSingleton).ResetStaticCacheForTesting();
            default(TestSceneSingleton).ResetStaticCacheForTesting();
        }

        [UnityTest]
        public IEnumerator PersistentPolicy_EnablesAutoCreation()
        {
            var instance = TestPersistentSingleton.Instance;
            yield return null;

            Assert.IsNotNull(anObject: instance, message: "PersistentPolicy should enable auto-creation");
            Assert.AreEqual(expected: "TestPersistentSingleton", actual: instance.gameObject.name, message: "Auto-created object should have correct name");
        }

        [UnityTest]
        public IEnumerator ScenePolicy_DisablesAutoCreation()
        {
#if TEST_IS_DEV
            Assert.Throws<InvalidOperationException>(
                code: () => { _ = TestSceneSingleton.Instance; },
                message: "ScenePolicy should throw exception when not placed (dev-only)"
            );
#else
            var instance = TestSceneSingleton.Instance;
            Assert.IsNull(anObject: instance, message: "ScenePolicy should return null when not placed (non-dev build)");
#endif
            yield return null;
        }

        [UnityTest]
        public IEnumerator PersistentSingleton_SurvivesDontDestroyOnLoad()
        {
            var instance = TestPersistentSingleton.Instance;
            yield return null;

            Assert.IsTrue(
                condition: instance.gameObject.scene.name == "DontDestroyOnLoad" || instance.transform.parent == null,
                message: "Persistent singleton should be in DontDestroyOnLoad scene or root"
            );

            var sameInstance = TestPersistentSingleton.Instance;
            Assert.AreSame(expected: instance, actual: sameInstance, message: "Should return the same instance after DontDestroyOnLoad");
        }
    }

    [TestFixture]
    public class ResourceManagementTests
    {
        [TearDown]
        public void TearDown()
        {
            if (TestPersistentSingleton.TryGetInstance(instance: out var instance))
            {
                UnityEngine.Object.DestroyImmediate(obj: instance.gameObject);
            }

            default(TestPersistentSingleton).ResetStaticCacheForTesting();
        }

        [UnityTest]
        public IEnumerator Singleton_CanBeDestroyed_Safely()
        {
            var instance = TestPersistentSingleton.Instance;
            yield return null;

            Assert.IsNotNull(anObject: instance, message: "Should create instance");

            UnityEngine.Object.DestroyImmediate(obj: instance.gameObject);
            yield return null;

            var newInstance = TestPersistentSingleton.Instance;
            yield return null;

            Assert.IsNotNull(anObject: newInstance, message: "Should create new instance after destruction");
            Assert.AreNotSame(expected: instance, actual: newInstance, message: "Should be different instances");
        }

        [UnityTest]
        public IEnumerator DestroyedInstance_DoesNotAffectNewInstance()
        {
            var firstInstance = TestPersistentSingleton.Instance;
            yield return null;

            Assert.IsNotNull(anObject: firstInstance, message: "Should create first instance");

            UnityEngine.Object.DestroyImmediate(obj: firstInstance.gameObject);
            yield return null;

            var secondInstance = TestPersistentSingleton.Instance;
            yield return null;

            Assert.IsNotNull(anObject: secondInstance, message: "Should create second instance");
            Assert.AreNotSame(expected: firstInstance, actual: secondInstance, message: "Should be different instances");
            Assert.IsFalse(condition: ReferenceEquals(objA: null, objB: secondInstance.gameObject), message: "Second instance GameObject should exist");
        }

        [UnityTest]
        public IEnumerator Instance_CanBeAccessed_DuringDestruction()
        {
            var instance = TestPersistentSingleton.Instance;
            yield return null;

            Assert.IsNotNull(anObject: instance, message: "Should create instance");

            var sameInstance = TestPersistentSingleton.Instance;
            Assert.AreSame(expected: instance, actual: sameInstance, message: "Should return same instance while alive");

            UnityEngine.Object.DestroyImmediate(obj: instance.gameObject);
            yield return null;

            bool result = TestPersistentSingleton.TryGetInstance(instance: out var retrieved);
            Assert.IsFalse(condition: result, message: "TryGetInstance should return false after destruction");
            Assert.IsNull(anObject: retrieved, message: "Retrieved instance should be null after destruction");
        }
    }

    [TestFixture]
    public class PracticalUsageTests
    {
        [TearDown]
        public void TearDown()
        {
            if (GameManager.TryGetInstance(instance: out var gm))
            {
                UnityEngine.Object.DestroyImmediate(obj: gm.gameObject);
            }
            default(GameManager).ResetStaticCacheForTesting();

            if (LevelController.TryGetInstance(instance: out var lc))
            {
                UnityEngine.Object.DestroyImmediate(obj: lc.gameObject);
            }
            default(LevelController).ResetStaticCacheForTesting();
        }

        [UnityTest]
        public IEnumerator GameManager_PersistsAcrossAccess()
        {
            var gm1 = GameManager.Instance;
            yield return null;

            Assert.IsNotNull(anObject: gm1, message: "GameManager should be created");
            Assert.AreEqual(expected: 0, actual: gm1.PlayerScore, message: "Initial score should be 0");
            Assert.AreEqual(expected: "MainMenu", actual: gm1.CurrentLevel, message: "Initial level should be MainMenu");

            gm1.AddScore(points: 100);
            gm1.SetLevel(levelName: "Level1");
            gm1.PauseGame();

            var gm2 = GameManager.Instance;
            yield return null;

            Assert.AreSame(expected: gm1, actual: gm2, message: "Should return same instance");
            Assert.AreEqual(expected: 100, actual: gm2.PlayerScore, message: "Score should persist");
            Assert.AreEqual(expected: "Level1", actual: gm2.CurrentLevel, message: "Level should persist");
            Assert.IsTrue(condition: gm2.IsGamePaused, message: "Pause state should persist");
        }

        [UnityTest]
        public IEnumerator LevelController_RequiresPlacement()
        {
            bool result = LevelController.TryGetInstance(instance: out var controller);
            yield return null;

            Assert.IsFalse(condition: result, message: "Should return false when not placed");
            Assert.IsNull(anObject: controller, message: "Controller should be null");

            var go = new GameObject(name: "LevelController");
            var placedController = go.AddComponent<LevelController>();
            yield return null;

            bool result2 = LevelController.TryGetInstance(instance: out var controller2);
            yield return null;

            Assert.IsTrue(condition: result2, message: "Should return true when placed");
            Assert.AreSame(expected: placedController, actual: controller2, message: "Should return placed instance");
            Assert.AreEqual(expected: "DefaultLevel", actual: controller2.LevelName, message: "Should be initialized");
        }

        [UnityTest]
        public IEnumerator Singleton_StateManagement_WorksCorrectly()
        {
            var gm = GameManager.Instance;
            yield return null;

            Assert.AreEqual(expected: 0, actual: gm.PlayerScore);
            Assert.AreEqual(expected: "MainMenu", actual: gm.CurrentLevel);
            Assert.IsFalse(condition: gm.IsGamePaused);

            gm.AddScore(points: 500);
            gm.SetLevel(levelName: "BossLevel");
            gm.PauseGame();

            Assert.AreEqual(expected: 500, actual: gm.PlayerScore);
            Assert.AreEqual(expected: "BossLevel", actual: gm.CurrentLevel);
            Assert.IsTrue(condition: gm.IsGamePaused);

            gm.AddScore(points: 1000);
            gm.SetLevel(levelName: "Victory");
            gm.ResumeGame();

            Assert.AreEqual(expected: 1500, actual: gm.PlayerScore);
            Assert.AreEqual(expected: "Victory", actual: gm.CurrentLevel);
            Assert.IsFalse(condition: gm.IsGamePaused);
        }

        [UnityTest]
        public IEnumerator SceneSingleton_LevelManagement_WorksCorrectly()
        {
            var go = new GameObject(name: "LevelController");
            var controller = go.AddComponent<LevelController>();
            yield return null;

            Assert.AreEqual(expected: "DefaultLevel", actual: controller.LevelName);
            Assert.AreEqual(expected: 5, actual: controller.EnemyCount);
            Assert.IsFalse(condition: controller.IsLevelComplete);

            controller.SetLevelInfo(levelName: "CastleLevel", enemies: 10);
            controller.SetLevelInfo(levelName: "CastleLevel", enemies: 0);
            controller.CompleteLevel();

            Assert.AreEqual(expected: "CastleLevel", actual: controller.LevelName);
            Assert.AreEqual(expected: 0, actual: controller.EnemyCount);
            Assert.IsTrue(condition: controller.IsLevelComplete);
        }

        [UnityTest]
        public IEnumerator Singleton_InitializationOrder_WorksCorrectly()
        {
            var gm = GameManager.Instance;
            yield return null;

            var go = new GameObject(name: "LevelController");
            var lc = go.AddComponent<LevelController>();
            yield return null;

            Assert.IsNotNull(anObject: gm);
            Assert.IsNotNull(anObject: lc);
            Assert.AreEqual(expected: 0, actual: gm.PlayerScore);
            Assert.AreEqual(expected: "DefaultLevel", actual: lc.LevelName);

            gm.AddScore(points: 100);
            lc.SetLevelInfo(levelName: "OrderedLevel", enemies: 3);

            Assert.AreEqual(expected: 100, actual: gm.PlayerScore);
            Assert.AreEqual(expected: "OrderedLevel", actual: lc.LevelName);
            Assert.AreEqual(expected: 3, actual: lc.EnemyCount);
        }

        [UnityTest]
        public IEnumerator Singleton_ResourceManagement_WorksCorrectly()
        {
            var gm = GameManager.Instance;
            yield return null;

            gm.AddScore(points: 999);
            gm.SetLevel(levelName: "FinalLevel");

            UnityEngine.Object.DestroyImmediate(obj: gm.gameObject);
            yield return null;

            var gm2 = GameManager.Instance;
            yield return null;

            Assert.IsNotNull(anObject: gm2);
            Assert.AreNotSame(expected: gm, actual: gm2, message: "Should be different instance");
            Assert.AreEqual(expected: 0, actual: gm2.PlayerScore, message: "Should have fresh score");
            Assert.AreEqual(expected: "MainMenu", actual: gm2.CurrentLevel, message: "Should have fresh level");
        }
    }

    [TestFixture]
    public class DomainReloadTests
    {
        [TearDown]
        public void TearDown()
        {
            TestExtensions.ResetQuittingFlagForTesting();

            var softResetInstances = UnityEngine.Object.FindObjectsByType<TestSoftResetSingleton>(
                findObjectsInactive: FindObjectsInactive.Include,
                sortMode: FindObjectsSortMode.None
            );
            foreach (var inst in softResetInstances)
            {
                UnityEngine.Object.DestroyImmediate(obj: inst.gameObject);
            }

            var persistentInstances = UnityEngine.Object.FindObjectsByType<TestPersistentSingleton>(
                findObjectsInactive: FindObjectsInactive.Include,
                sortMode: FindObjectsSortMode.None
            );
            foreach (var inst in persistentInstances)
            {
                UnityEngine.Object.DestroyImmediate(obj: inst.gameObject);
            }

            default(TestSoftResetSingleton).ResetStaticCacheForTesting();
            default(TestPersistentSingleton).ResetStaticCacheForTesting();
        }

        [UnityTest]
        public IEnumerator StaticCache_IsInvalidated_WhenPlaySessionIdChanges()
        {
            var instance = TestPersistentSingleton.Instance;
            yield return null;

            Assert.IsNotNull(anObject: instance, message: "Instance should be created");

            TestExtensions.AdvancePlaySessionIdForTesting();

            var sameInstance = TestPersistentSingleton.Instance;
            yield return null;

            Assert.AreSame(expected: instance, actual: sameInstance, message: "Should return same GameObject instance");
        }

        [UnityTest]
        public IEnumerator OnPlaySessionStart_IsCalledAgain_AfterPlaySessionBoundary()
        {
            var instance = TestSoftResetSingleton.Instance;
            yield return null;

            Assert.AreEqual(expected: 1, actual: instance.PlaySessionStartCalls, message: "OnPlaySessionStart should be called once initially");

            TestExtensions.AdvancePlaySessionIdForTesting();
            _ = TestSoftResetSingleton.Instance;
            yield return null;

            Assert.AreEqual(expected: 2, actual: instance.PlaySessionStartCalls, message: "OnPlaySessionStart should be called again after boundary");

            TestExtensions.AdvancePlaySessionIdForTesting();
            _ = TestSoftResetSingleton.Instance;
            yield return null;

            Assert.AreEqual(expected: 3, actual: instance.PlaySessionStartCalls, message: "OnPlaySessionStart should be called for each Play session");
        }

        [UnityTest]
        public IEnumerator AwakeCount_DoesNotIncrease_OnPlaySessionBoundary()
        {
            var instance = TestSoftResetSingleton.Instance;
            yield return null;

            Assert.AreEqual(expected: 1, actual: instance.AwakeCalls, message: "Awake should be called once on creation");

            for (int i = 0; i < 3; i++)
            {
                TestExtensions.AdvancePlaySessionIdForTesting();
                _ = TestSoftResetSingleton.Instance;
                yield return null;
            }

            Assert.AreEqual(expected: 1, actual: instance.AwakeCalls, message: "Awake should NOT be called again on Play session boundary");
        }

        [UnityTest]
        public IEnumerator TryGetInstance_WorksCorrectly_AcrossPlaySessionBoundary()
        {
            var instance = TestPersistentSingleton.Instance;
            yield return null;

            bool resultBefore = TestPersistentSingleton.TryGetInstance(instance: out var retrieved1);
            Assert.IsTrue(condition: resultBefore, message: "Should find instance before boundary");
            Assert.AreSame(expected: instance, actual: retrieved1);

            TestExtensions.AdvancePlaySessionIdForTesting();

            bool resultAfter = TestPersistentSingleton.TryGetInstance(instance: out var retrieved2);
            Assert.IsTrue(condition: resultAfter, message: "Should still find instance after boundary");
            Assert.AreSame(expected: instance, actual: retrieved2, message: "Should return same instance");
        }

        [UnityTest]
        public IEnumerator Instance_ReturnsNull_WhenQuitting()
        {
            var instance = TestPersistentSingleton.Instance;
            yield return null;

            Assert.IsNotNull(anObject: instance, message: "Instance should exist before quitting");

            Core.SingletonRuntime.NotifyQuitting();

            bool result = TestPersistentSingleton.TryGetInstance(instance: out var retrieved);
            Assert.IsFalse(condition: result, message: "TryGetInstance should return false when quitting");
            Assert.IsNull(anObject: retrieved, message: "Retrieved instance should be null when quitting");
        }

        [UnityTest]
        public IEnumerator NewInstance_IsNotCreated_WhenQuitting()
        {
            Core.SingletonRuntime.NotifyQuitting();

            var instance = TestPersistentSingleton.Instance;

            Assert.IsNull(anObject: instance, message: "Should not create instance when quitting");

            var found = UnityEngine.Object.FindAnyObjectByType<TestPersistentSingleton>();
            Assert.IsNull(anObject: found, message: "No instance should exist in scene");

            yield return null;
        }
    }

    [TestFixture]
    public class ParentHierarchyTests
    {
        [TearDown]
        public void TearDown()
        {
            var allObjects = UnityEngine.Object.FindObjectsByType<TestPersistentSingleton>(
                findObjectsInactive: FindObjectsInactive.Include,
                sortMode: FindObjectsSortMode.None
            );
            foreach (var obj in allObjects)
            {
                UnityEngine.Object.DestroyImmediate(obj: obj.gameObject);
            }

            var parents = UnityEngine.Object.FindObjectsByType<GameObject>(sortMode: FindObjectsSortMode.None);
            foreach (var parent in parents)
            {
                if (parent.name.Contains(value: "Parent"))
                {
                    UnityEngine.Object.DestroyImmediate(obj: parent);
                }
            }

            default(TestPersistentSingleton).ResetStaticCacheForTesting();
        }

        [UnityTest]
        public IEnumerator PersistentSingleton_WithParent_IsReparentedToRoot()
        {
            var start = TestLogCounter.Take();

            var parent = new GameObject(name: "ParentObject");
            var child = new GameObject(name: "TestPersistentSingleton");
            child.transform.SetParent(p: parent.transform);

            child.AddComponent<TestPersistentSingleton>();
            yield return null;

            var instance = TestPersistentSingleton.Instance;
            yield return null;

            Assert.IsNull(anObject: instance.transform.parent, message: "Singleton should be reparented to root");

            var delta = TestLogCounter.Take().Delta(baseline: start);

#if TEST_IS_DEV
            Assert.AreEqual(expected: 1, actual: delta.Warning, message: "Reparenting should emit exactly one warning log (dev-only)");
            Assert.AreEqual(expected: 0, actual: delta.Exception, message: "No exceptions expected (dev-only)");
            Assert.AreEqual(expected: 0, actual: delta.Assert, message: "No asserts expected (dev-only)");
#endif
        }

        [UnityTest]
        public IEnumerator AutoCreatedSingleton_HasNoParent()
        {
            var instance = TestPersistentSingleton.Instance;
            yield return null;

            Assert.IsNull(anObject: instance.transform.parent, message: "Auto-created singleton should have no parent");
            Assert.AreEqual(expected: "TestPersistentSingleton", actual: instance.gameObject.name, message: "Auto-created singleton should have type name");
        }
    }

    [TestFixture]
    public class BaseAwakeEnforcementTests
    {
        [TearDown]
        public void TearDown()
        {
            var allObjects = UnityEngine.Object.FindObjectsByType<TestSingletonWithoutBaseAwake>(
                findObjectsInactive: FindObjectsInactive.Include,
                sortMode: FindObjectsSortMode.None
            );
            foreach (var obj in allObjects)
            {
                UnityEngine.Object.DestroyImmediate(obj: obj.gameObject);
            }

            default(TestSingletonWithoutBaseAwake).ResetStaticCacheForTesting();
        }

        [UnityTest]
        public IEnumerator Singleton_LogsError_WhenBaseAwakeNotCalled_InDev()
        {
#if !TEST_IS_DEV
            Assert.Ignore(message: "base.Awake enforcement log verification is dev-only.");
            yield break;
#else
            var start = TestLogCounter.Take();

            using (new IgnoreFailingMessagesScope(enabled: true))
            {
                var go = new GameObject(name: "TestSingletonWithoutBaseAwake");
                var singleton = go.AddComponent<TestSingletonWithoutBaseAwake>();
                yield return null;

                Assert.IsTrue(condition: singleton.AwakeWasCalled, message: "Custom Awake should have been called");
            }

            var delta = TestLogCounter.Take().Delta(baseline: start);
            Assert.AreEqual(expected: 1, actual: delta.Error, message: "Missing base.Awake should emit exactly one error log (dev-only)");
            Assert.AreEqual(expected: 0, actual: delta.Exception, message: "No exceptions expected (dev-only)");
            Assert.AreEqual(expected: 0, actual: delta.Assert, message: "No asserts expected (dev-only)");
#endif
        }
    }

    [TestFixture]
    public class EdgeCaseTests
    {
        [TearDown]
        public void TearDown()
        {
            if (TestSceneSingleton.TryGetInstance(instance: out var scene))
            {
                UnityEngine.Object.DestroyImmediate(obj: scene.gameObject);
            }

            if (TestPersistentSingleton.TryGetInstance(instance: out var persistent))
            {
                UnityEngine.Object.DestroyImmediate(obj: persistent.gameObject);
            }

            default(TestSceneSingleton).ResetStaticCacheForTesting();
            default(TestPersistentSingleton).ResetStaticCacheForTesting();
            TestExtensions.ResetQuittingFlagForTesting();
        }

        [UnityTest]
        public IEnumerator DestroyedInstance_IsProperlyCleanedUp_FromCache()
        {
            var instance = TestPersistentSingleton.Instance;
            yield return null;

            Assert.IsNotNull(anObject: instance);

            UnityEngine.Object.DestroyImmediate(obj: instance.gameObject);

            bool exists = TestPersistentSingleton.TryGetInstance(instance: out var retrieved);
            Assert.IsFalse(condition: exists, message: "TryGetInstance should return false after destruction");
            Assert.IsNull(anObject: retrieved, message: "Retrieved instance should be null");
        }

        [UnityTest]
        public IEnumerator MultipleRapidAccesses_ReturnSameInstance()
        {
            var instances = new TestPersistentSingleton[10];

            for (int i = 0; i < 10; i++)
            {
                instances[i] = TestPersistentSingleton.Instance;
            }

            yield return null;

            for (int i = 1; i < 10; i++)
            {
                Assert.AreSame(expected: instances[0], actual: instances[i], message: $"Access {i} should return same instance");
            }

            Assert.AreEqual(
                expected: 1,
                actual: UnityEngine.Object.FindObjectsByType<TestPersistentSingleton>(sortMode: FindObjectsSortMode.None).Length,
                message: "Only one instance should exist"
            );
        }

        [UnityTest]
        public IEnumerator SceneSingleton_AccessBeforePlacement_ThenPlacement_Works()
        {
            bool beforeResult = TestSceneSingleton.TryGetInstance(instance: out var before);
            Assert.IsFalse(condition: beforeResult, message: "Should not find instance before placement");
            Assert.IsNull(anObject: before);

            yield return null;

            var go = new GameObject(name: "TestSceneSingleton");
            var placed = go.AddComponent<TestSceneSingleton>();
            yield return null;

            bool afterResult = TestSceneSingleton.TryGetInstance(instance: out var after);
            Assert.IsTrue(condition: afterResult, message: "Should find instance after placement");
            Assert.AreSame(expected: placed, actual: after);
        }
    }
}
