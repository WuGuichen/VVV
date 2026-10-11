using System;
using System.IO;
using System.Threading.Tasks;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Persistence;
using MxFramework.Persistence.FileSystem;
using MxFramework.Platform;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests
{
    public sealed class CharacterTacticalSaveStoreTests
    {
        private string _root;
        [SetUp] public void Setup()
        {
            _root = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/TacticalContent/SaveTests", Guid.NewGuid().ToString("N")));
        }
        [TearDown] public void Cleanup()
        {
            string owner = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/TacticalContent/SaveTests")) + Path.DirectorySeparatorChar;
            if (_root != null && _root.StartsWith(owner, StringComparison.OrdinalIgnoreCase) && Directory.Exists(_root)) Directory.Delete(_root, true);
        }
        [Test] public async Task ActualFileSystemSaveAndResetSurviveNewStoreInstances()
        {
            var first = await CharacterTacticalSaveStore.CreateAsync(_root);
            Assert.IsNull(await first.LoadAsync());
            await first.SaveAsync("{\"owned\":[\"a\"]}");
            var second = await CharacterTacticalSaveStore.CreateAsync(_root);
            Assert.AreEqual("{\"owned\":[\"a\"]}", await second.LoadAsync());
            await second.ResetAsync("{\"owned\":[]}");
            var third = await CharacterTacticalSaveStore.CreateAsync(_root);
            Assert.AreEqual("{\"owned\":[]}", await third.LoadAsync());
        }
        [Test] public async Task CorruptHeadRecoversPreviousGenerationAndCommitsAgainstOriginalHead()
        {
            var store = await CharacterTacticalSaveStore.CreateAsync(_root);
            await store.LoadAsync();
            await store.SaveAsync("{\"owned\":[\"one\"]}");
            await store.SaveAsync("{\"owned\":[\"two\"]}");
            CorruptGeneration("tactical-player", 2);
            var recovered = await CharacterTacticalSaveStore.CreateAsync(_root);
            var stale = await CharacterTacticalSaveStore.CreateAsync(_root);
            Assert.AreEqual("{\"owned\":[\"one\"]}", await recovered.LoadAsync());
            Assert.AreEqual("{\"owned\":[\"one\"]}", await stale.LoadAsync());
            await recovered.SaveAsync("{\"owned\":[\"three\"]}");
            Assert.IsTrue(File.Exists(GenerationPath("tactical-player", 3)));
            Assert.ThrowsAsync<InvalidOperationException>(() => stale.SaveAsync("{\"owned\":[\"stale\"]}"));
            Assert.AreEqual("{\"owned\":[\"three\"]}", await (await CharacterTacticalSaveStore.CreateAsync(_root)).LoadAsync());
        }

        [Test] public async Task CorruptOtherSlotDoesNotBlockExistingOrMissingTargetSlot()
        {
            var player = await CharacterTacticalSaveStore.CreateAsync(_root);
            await player.LoadAsync();
            await player.SaveAsync("player");
            var other = await CharacterTacticalSaveStore.CreateAsync(_root, "tactical-battle-preparation", "preparation-test-v1");
            await other.LoadAsync();
            await other.SaveAsync("preparation");
            CorruptGeneration("tactical-battle-preparation", 1);
            Assert.AreEqual("player", await (await CharacterTacticalSaveStore.CreateAsync(_root)).LoadAsync());
            var missing = await CharacterTacticalSaveStore.CreateAsync(_root, "new-test-slot", "new-test-v1");
            Assert.IsNull(await missing.LoadAsync());
            await missing.SaveAsync("new");
            Assert.AreEqual("new", await (await CharacterTacticalSaveStore.CreateAsync(_root, "new-test-slot", "new-test-v1")).LoadAsync());
        }

        [Test] public async Task UnrecoverableCorruptionNeverBecomesANewSlotOrRewritesFiles()
        {
            var store = await CharacterTacticalSaveStore.CreateAsync(_root);
            await store.LoadAsync(); await store.SaveAsync("one"); await store.SaveAsync("two");
            CorruptGeneration("tactical-player", 1); CorruptGeneration("tactical-player", 2);
            byte[] first = File.ReadAllBytes(GenerationPath("tactical-player", 1));
            byte[] second = File.ReadAllBytes(GenerationPath("tactical-player", 2));
            var reload = await CharacterTacticalSaveStore.CreateAsync(_root);
            Assert.ThrowsAsync<InvalidOperationException>(() => reload.LoadAsync());
            CollectionAssert.AreEqual(first, File.ReadAllBytes(GenerationPath("tactical-player", 1)));
            CollectionAssert.AreEqual(second, File.ReadAllBytes(GenerationPath("tactical-player", 2)));
            Assert.IsFalse(File.Exists(GenerationPath("tactical-player", 3)));
        }

        [Test] public async Task TombstoneLoadPreservesGenerationForExplicitRecreation()
        {
            var store = await CharacterTacticalSaveStore.CreateAsync(_root);
            await store.LoadAsync(); await store.SaveAsync("one");
            string root = _root;
            await Task.Run(() =>
            {
                var location = PlatformLocation.TryCreate(PlatformLocationKind.FileSystemPath, root, true, true, true);
                var factory = FileSystemSaveGameStoreFactory.TryCreate(location.Location);
                Assert.IsTrue(factory.Success, factory.Error.ToString());
                var operation = factory.Factory.CreateStore().Delete(new SaveGameDeleteRequest(
                    new SaveGameProfileId("character-test"), new SaveGameSlotId("tactical-player"), new SaveGameGeneration(1),
                    DateTime.UtcNow, new SaveGameBuildDescriptor("1", "character-test", "tactical-v1", "references-v1", "tactical-player")));
                while (!operation.IsDone) operation.Poll();
                Assert.IsTrue(operation.TryGetResult(out _), operation.Error.ToString());
            });
            var reload = await CharacterTacticalSaveStore.CreateAsync(_root);
            Assert.IsNull(await reload.LoadAsync());
            await reload.SaveAsync("three");
            Assert.IsTrue(File.Exists(GenerationPath("tactical-player", 3)));
            Assert.AreEqual("three", await (await CharacterTacticalSaveStore.CreateAsync(_root)).LoadAsync());
        }

        private string GenerationPath(string slotId, ulong generation)
        {
            Assert.IsTrue(FileSystemSaveGameLayout.TryGetSlotDirectory(_root, new SaveGameProfileId("character-test"),
                new SaveGameSlotId(slotId), BclFileSystemBackend.Instance, out string directory, out var error), error.ToString());
            return Path.Combine(directory, FileSystemGenerationLayout.Format(new SaveGameGeneration(generation)));
        }
        private void CorruptGeneration(string slotId, ulong generation)
        {
            string path = GenerationPath(slotId, generation);
            byte[] bytes = File.ReadAllBytes(path); bytes[bytes.Length - 1] ^= 1;
            File.WriteAllBytes(path, bytes);
        }

        [Test] public async Task ConcurrentStaleWriterCannotOverwriteCommittedProgress()
        {
            var first = await CharacterTacticalSaveStore.CreateAsync(_root);
            var second = await CharacterTacticalSaveStore.CreateAsync(_root);
            await first.LoadAsync(); await second.LoadAsync();
            await first.SaveAsync("{\"owned\":[\"a\"]}");
            Assert.ThrowsAsync<InvalidOperationException>(() => second.SaveAsync("{\"owned\":[\"b\"]}"));
            Assert.AreEqual("{\"owned\":[\"a\"]}", await (await CharacterTacticalSaveStore.CreateAsync(_root)).LoadAsync());
        }
    }
}
