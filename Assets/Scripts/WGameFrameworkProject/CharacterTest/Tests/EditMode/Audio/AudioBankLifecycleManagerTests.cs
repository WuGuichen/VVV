using System.Collections.Generic;
using MxFramework.Audio;
using NUnit.Framework;

namespace MxFramework.Tests.Audio
{
    public sealed class AudioBankLifecycleManagerTests
    {
        [Test]
        public void WarmupAndRelease_ReferenceCountsEachBankOnce()
        {
            var backend = new RecordingBackend();
            using (var manager = new AudioBankLifecycleManager(CreateManifest(), backend))
            {
                Assert.IsTrue(manager.WarmupGroup("bootstrap").Success);
                Assert.IsTrue(manager.WarmupGroup("bootstrap").Success);
                Assert.AreEqual(2, backend.Loaded.Count);
                Assert.AreEqual(2, manager.Snapshot.LoadedBanks);
                Assert.AreEqual(4, manager.Snapshot.TotalReferences);
                AudioBankRuntimeState[] states = manager.CaptureDebugStates();
                Assert.AreEqual(3, states.Length);
                Assert.AreEqual("Master.strings", states[0].StableName);
                Assert.AreEqual(AudioBankScope.AppPersistent, states[0].Scope);
                Assert.AreEqual(2, states[0].References);
                Assert.IsTrue(states[0].Loaded);

                Assert.IsTrue(manager.ReleaseGroup("bootstrap").Success);
                Assert.AreEqual(0, backend.Unloaded.Count);
                Assert.IsTrue(manager.ReleaseGroup("bootstrap").Success);
                Assert.AreEqual(2, backend.Unloaded.Count);
                Assert.AreEqual(0, manager.Snapshot.LoadedBanks);
            }
        }

        [Test]
        public void ScopeUnload_KeepsAppPersistentUntilDispose()
        {
            var backend = new RecordingBackend();
            var manager = new AudioBankLifecycleManager(CreateManifest(), backend);
            Assert.IsTrue(manager.WarmupGroup("bootstrap").Success);
            Assert.IsTrue(manager.WarmupGroup("scene-a").Success);

            Assert.AreEqual(1, manager.UnloadScope(AudioBankScope.Scene));
            Assert.AreEqual("SceneA", backend.Unloaded[0]);
            Assert.AreEqual(2, manager.Snapshot.LoadedBanks);

            manager.Dispose();
            Assert.AreEqual(3, backend.Unloaded.Count);
            Assert.AreEqual(0, manager.Snapshot.LoadedBanks);
        }

        [TestCase(1)]
        [TestCase(3)]
        public void OptionalBank_RecoveryPreservesEveryActiveGroupHolder(int failedWarmups)
        {
            var backend = new RecordingBackend { FailLoadName = "Optional", LoadFailuresRemaining = failedWarmups };
            using (var manager = new AudioBankLifecycleManager(CreateSceneManifest(), backend))
            {
                for (int i = 0; i < failedWarmups; i++)
                    Assert.IsTrue(manager.WarmupGroup("scene").Success);
                Assert.IsTrue(manager.WarmupGroup("scene").Success);
                Assert.AreEqual(failedWarmups + 1, manager.CaptureDebugStates()[1].References);
                Assert.AreEqual(failedWarmups, manager.Snapshot.LoadFailures);

                for (int i = 0; i < failedWarmups; i++)
                {
                    Assert.IsTrue(manager.ReleaseGroup("scene").Success);
                    Assert.IsTrue(backend.Active.Contains("Optional"));
                    Assert.AreEqual(0, backend.Unloaded.Count);
                    Assert.AreEqual((failedWarmups - i) * 2, manager.Snapshot.TotalReferences);
                }
                Assert.IsTrue(manager.ReleaseGroup("scene").Success);
                Assert.AreEqual(2, backend.Unloaded.Count);
                AssertEmpty(manager, backend);
            }
        }

        [Test]
        public void OptionalBank_AlwaysFailsWithoutLeakingGroupOwnership()
        {
            var backend = new RecordingBackend { FailLoadName = "Optional", LoadFailuresRemaining = 3 };
            using (var manager = new AudioBankLifecycleManager(CreateSceneManifest(), backend))
            {
                Assert.IsTrue(manager.WarmupGroup("scene").Success);
                Assert.IsTrue(manager.WarmupGroup("scene").Success);
                Assert.AreEqual(2, manager.Snapshot.TotalReferences);
                Assert.IsTrue(manager.ReleaseGroup("scene").Success);
                Assert.IsTrue(manager.ReleaseGroup("scene").Success);
                AssertEmpty(manager, backend);

                Assert.IsTrue(manager.WarmupGroup("scene").Success);
                Assert.IsTrue(manager.WarmupGroup("scene").Success);
                Assert.AreEqual(2, manager.CaptureDebugStates()[1].References);
                manager.Dispose();
                AssertEmpty(manager, backend);
            }
        }

        [Test]
        public void OptionalOnlyGroup_AllFailedHoldersCanBeReleased()
        {
            var backend = new RecordingBackend { FailLoadName = "Optional", LoadFailuresRemaining = 2 };
            var manifest = CreateManifest(new AudioBankDefinition("Optional", "Optional.bank", "a", 1,
                false, "scene", AudioBankScope.Scene, false));
            using (var manager = new AudioBankLifecycleManager(manifest, backend))
            {
                Assert.IsTrue(manager.WarmupGroup("scene").Success);
                Assert.IsTrue(manager.WarmupGroup("scene").Success);
                AssertEmpty(manager, backend);
                Assert.IsTrue(manager.ReleaseGroup("scene").Success);
                Assert.IsTrue(manager.ReleaseGroup("scene").Success);
                Assert.IsTrue(manager.WarmupGroup("scene").Success);
                Assert.AreEqual(1, manager.Snapshot.TotalReferences);
                Assert.IsTrue(manager.ReleaseGroup("scene").Success);
                AssertEmpty(manager, backend);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RequiredFailure_RollsBackSuccessfulAcquisitions(bool optionalFails)
        {
            var backend = new RecordingBackend
            {
                FailLoadName = "Required", LoadFailuresRemaining = 1,
                AlwaysFailOptional = optionalFails
            };
            using (var manager = new AudioBankLifecycleManager(CreateSceneManifest(optionalFirst: true), backend))
            {
                Assert.IsTrue(manager.WarmupGroup("scene").Failed);
                Assert.AreEqual(optionalFails ? 0 : 1, backend.Unloaded.Count);
                AssertEmpty(manager, backend);
                backend.AlwaysFailOptional = false;
                Assert.IsTrue(manager.WarmupGroup("scene").Success);
                Assert.AreEqual(2, manager.Snapshot.TotalReferences);
                Assert.IsTrue(manager.ReleaseGroup("scene").Success);
                AssertEmpty(manager, backend);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RequiredFailure_RollbackPreservesPreviousHolders(bool rollbackUnloadFails)
        {
            var backend = new RecordingBackend { FailLoadName = "Optional", LoadFailuresRemaining = 2 };
            var manifest = CreateManifest(
                new AudioBankDefinition("Persistent", "Persistent.bank", "a", 1, true, "scene", AudioBankScope.AppPersistent, false),
                new AudioBankDefinition("Optional", "Optional.bank", "b", 1, false, "scene", AudioBankScope.AppPersistent, false),
                new AudioBankDefinition("Required", "Required.bank", "c", 1, true, "scene", AudioBankScope.Scene, false));
            using (var manager = new AudioBankLifecycleManager(manifest, backend))
            {
                Assert.IsTrue(manager.WarmupGroup("scene").Success);
                Assert.IsTrue(manager.WarmupGroup("scene").Success);
                Assert.AreEqual(1, manager.UnloadScope(AudioBankScope.Scene));
                backend.FailLoadName = "Required";
                backend.LoadFailuresRemaining = 1;
                backend.FailUnloadName = "Optional";
                backend.UnloadFailuresRemaining = rollbackUnloadFails ? 1 : 0;

                Assert.IsTrue(manager.WarmupGroup("scene").Failed);
                Assert.AreEqual(2, manager.CaptureDebugStates()[0].References);
                Assert.AreEqual(rollbackUnloadFails ? 2 : 0, manager.CaptureDebugStates()[1].References);
                Assert.AreEqual(rollbackUnloadFails ? 4 : 2, manager.Snapshot.TotalReferences);
                Assert.IsTrue(manager.ReleaseGroup("scene").Success);
                Assert.IsTrue(backend.Active.Contains("Persistent"));
                Assert.AreEqual(rollbackUnloadFails, backend.Active.Contains("Optional"));
                Assert.IsTrue(manager.ReleaseGroup("scene").Success);
                AssertEmpty(manager, backend);
            }
        }

        [Test]
        public void ReleaseFailure_RetainsLoadedBankForRetry()
        {
            var backend = new RecordingBackend { FailUnloadName = "Optional", UnloadFailuresRemaining = 1 };
            using (var manager = new AudioBankLifecycleManager(CreateSceneManifest(), backend))
            {
                Assert.IsTrue(manager.WarmupGroup("scene").Success);
                Assert.IsTrue(manager.ReleaseGroup("scene").Failed);
                Assert.AreEqual(1, manager.Snapshot.LoadedBanks);
                Assert.AreEqual(1, manager.Snapshot.TotalReferences);
                Assert.AreEqual(1, manager.Snapshot.UnloadFailures);
                Assert.IsTrue(backend.Active.Contains("Optional"));
                Assert.IsTrue(manager.ReleaseGroup("scene").Success);
                AssertEmpty(manager, backend);
            }
        }

        [Test]
        public void WarmupAfterReleaseFailure_ReusesRetainedBankWithoutExtraReference()
        {
            var backend = new RecordingBackend { FailUnloadName = "Optional", UnloadFailuresRemaining = 1 };
            using (var manager = new AudioBankLifecycleManager(CreateSceneManifest(), backend))
            {
                Assert.IsTrue(manager.WarmupGroup("scene").Success);
                Assert.IsTrue(manager.ReleaseGroup("scene").Failed);
                Assert.IsTrue(manager.WarmupGroup("scene").Success);
                Assert.AreEqual(3, backend.Loaded.Count);
                Assert.AreEqual(2, manager.Snapshot.TotalReferences);
                Assert.IsTrue(manager.ReleaseGroup("scene").Success);
                AssertEmpty(manager, backend);
            }
        }

        [Test]
        public void RollbackUnloadFailure_RemainsRetryableWithoutSuccessfulWarmup()
        {
            var backend = new RecordingBackend
            {
                FailLoadName = "Required", LoadFailuresRemaining = 1,
                FailUnloadName = "Optional", UnloadFailuresRemaining = 1
            };
            using (var manager = new AudioBankLifecycleManager(CreateSceneManifest(optionalFirst: true), backend))
            {
                Assert.IsTrue(manager.WarmupGroup("scene").Failed);
                Assert.AreEqual(1, manager.Snapshot.TotalReferences);
                Assert.IsTrue(manager.ReleaseGroup("scene").Success);
                AssertEmpty(manager, backend);
                Assert.IsTrue(manager.WarmupGroup("scene").Success);
                Assert.AreEqual(2, manager.Snapshot.TotalReferences);
                Assert.IsTrue(manager.ReleaseGroup("scene").Success);
                AssertEmpty(manager, backend);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ScopeUnload_ClearsOwnershipIncludingFailedOptionalBanks(bool optionalRecovers)
        {
            var backend = new RecordingBackend
            {
                FailLoadName = "Optional", LoadFailuresRemaining = optionalRecovers ? 1 : 2
            };
            using (var manager = new AudioBankLifecycleManager(CreateSceneManifest(), backend))
            {
                Assert.IsTrue(manager.WarmupGroup("scene").Success);
                Assert.IsTrue(manager.WarmupGroup("scene").Success);
                Assert.AreEqual(optionalRecovers ? 2 : 1, manager.UnloadScope(AudioBankScope.Scene));
                AssertEmpty(manager, backend);
                Assert.IsTrue(manager.ReleaseGroup("scene").Success);
                Assert.IsTrue(manager.WarmupGroup("scene").Success);
                Assert.AreEqual(2, manager.Snapshot.TotalReferences);
                Assert.IsTrue(manager.ReleaseGroup("scene").Success);
                AssertEmpty(manager, backend);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ForcedUnloadFailure_CanBeRetried(bool dispose)
        {
            var backend = new RecordingBackend
            {
                FailLoadName = "Optional", LoadFailuresRemaining = 1,
                FailUnloadName = "Optional", UnloadFailuresRemaining = 1
            };
            var manager = new AudioBankLifecycleManager(CreateSceneManifest(), backend);
            Assert.IsTrue(manager.WarmupGroup("scene").Success);
            Assert.IsTrue(manager.WarmupGroup("scene").Success);
            if (dispose)
                manager.Dispose();
            else
                Assert.AreEqual(1, manager.UnloadScope(AudioBankScope.Scene));
            Assert.AreEqual(1, manager.Snapshot.LoadedBanks);
            Assert.AreEqual(1, manager.Snapshot.TotalReferences);
            Assert.AreEqual(1, manager.Snapshot.UnloadFailures);
            if (dispose)
                manager.Dispose();
            else
                Assert.AreEqual(1, manager.UnloadScope(AudioBankScope.Scene));
            AssertEmpty(manager, backend);
        }

        [TestCase(null)]
        [TestCase(" ")]
        [TestCase("unknown")]
        public void InvalidGroup_IsRejectedWithoutBackendCalls(string group)
        {
            var backend = new RecordingBackend();
            using (var manager = new AudioBankLifecycleManager(CreateSceneManifest(), backend))
            {
                Assert.AreEqual(AudioErrorCode.RequestRejected, manager.WarmupGroup(group).ErrorCode);
                Assert.AreEqual(AudioErrorCode.RequestRejected, manager.ReleaseGroup(group).ErrorCode);
                Assert.IsTrue(manager.ReleaseGroup("scene").Success);
                Assert.AreEqual(0, backend.Loaded.Count);
                Assert.AreEqual(0, backend.Unloaded.Count);
                AssertEmpty(manager, backend);
            }
        }

        private static void AssertEmpty(AudioBankLifecycleManager manager, RecordingBackend backend)
        {
            Assert.AreEqual(0, manager.Snapshot.LoadedBanks);
            Assert.AreEqual(0, manager.Snapshot.TotalReferences);
            Assert.AreEqual(0, backend.Active.Count);
        }

        private static AudioBankManifest CreateSceneManifest(bool optionalFirst = false)
        {
            var required = new AudioBankDefinition("Required", "Required.bank", "a", 1, true, "scene", AudioBankScope.Scene, false);
            var optional = new AudioBankDefinition("Optional", "Optional.bank", "b", 1, false, "scene", AudioBankScope.Scene, false);
            return optionalFirst ? CreateManifest(optional, required) : CreateManifest(required, optional);
        }

        private static AudioBankManifest CreateManifest(params AudioBankDefinition[] banks)
        {
            return new AudioBankManifest("Desktop", "p", "c", "m", "s", string.Empty, banks);
        }

        private static AudioBankManifest CreateManifest()
        {
            return new AudioBankManifest("Desktop", "p", "c", "m", "s", string.Empty, new[]
            {
                new AudioBankDefinition("Master.strings", "Master.strings.bank", "a", 1, true, "bootstrap", AudioBankScope.AppPersistent, false),
                new AudioBankDefinition("Master", "Master.bank", "b", 1, true, "bootstrap", AudioBankScope.AppPersistent, true),
                new AudioBankDefinition("SceneA", "SceneA.bank", "c", 1, true, "scene-a", AudioBankScope.Scene, true)
            });
        }

        private sealed class RecordingBackend : IAudioBankBackend
        {
            public readonly List<string> Loaded = new List<string>();
            public readonly List<string> Unloaded = new List<string>();
            public readonly HashSet<string> Active = new HashSet<string>();
            public string FailLoadName;
            public int LoadFailuresRemaining;
            public bool AlwaysFailOptional;
            public string FailUnloadName;
            public int UnloadFailuresRemaining;

            public AudioResult LoadBank(string stableName, bool preloadSamples)
            {
                if ((stableName == FailLoadName && LoadFailuresRemaining-- > 0)
                    || (stableName == "Optional" && AlwaysFailOptional))
                    return AudioResult.Fail(AudioErrorCode.BackendUnavailable, "Injected Bank load failure.");
                Assert.IsTrue(Active.Add(stableName), "Bank was already loaded: " + stableName);
                Loaded.Add(stableName);
                return AudioResult.Ok();
            }

            public AudioResult UnloadBank(string stableName)
            {
                if (stableName == FailUnloadName && UnloadFailuresRemaining-- > 0)
                    return AudioResult.Fail(AudioErrorCode.BackendUnavailable, "Injected Bank unload failure.");
                Assert.IsTrue(Active.Remove(stableName), "Bank was not loaded: " + stableName);
                Unloaded.Add(stableName);
                return AudioResult.Ok();
            }
        }
    }
}
