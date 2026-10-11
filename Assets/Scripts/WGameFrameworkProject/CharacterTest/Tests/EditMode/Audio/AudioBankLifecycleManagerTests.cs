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

            public AudioResult LoadBank(string stableName, bool preloadSamples)
            {
                Loaded.Add(stableName);
                return AudioResult.Ok();
            }

            public AudioResult UnloadBank(string stableName)
            {
                Unloaded.Add(stableName);
                return AudioResult.Ok();
            }
        }
    }
}
