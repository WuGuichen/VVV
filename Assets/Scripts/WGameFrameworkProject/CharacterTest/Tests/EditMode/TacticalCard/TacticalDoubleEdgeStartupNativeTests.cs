using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Real startup builder, publication owner and SaveGame CAS envelope, under temporary roots only.</summary>
    [Category("DoubleEdgeNative")][Parallelizable(ParallelScope.None)]
    public sealed class TacticalDoubleEdgeStartupNativeTests
    {
        private sealed class Store : ICharacterTacticalStateStore
        {
            private readonly ICharacterTacticalStateStore _physical;
            internal bool RejectWrites;
            internal int Writes;
            internal Store(ICharacterTacticalStateStore physical) => _physical = physical;
            public Task<string> LoadAsync() => _physical.LoadAsync();
            public Task SaveAsync(string json)
            {
                Writes++;
                if (RejectWrites) throw new IOException("fixture:startup-save-rejected");
                return _physical.SaveAsync(json);
            }
            public Task ResetAsync(string json) => throw new InvalidOperationException("Startup migration cannot reset progress.");
        }

        [TestCase("success")][TestCase("save-failure")][TestCase("stale-cas")]
        public async Task ActualStartupPublishesMigratedOldProfileOnlyAfterWholeCheckpointSave(string mode)
        {
            string root = Path.Combine(Path.GetTempPath(), "MxDoubleEdgeStartup", Guid.NewGuid().ToString("N"));
            string content = Path.Combine(root, "content"), persistence = Path.Combine(root, "save");
            Directory.CreateDirectory(content);
            string formal = Path.Combine(Application.streamingAssetsPath, "MxFramework/CharacterTest");
            foreach (string path in Directory.GetFiles(formal, "*.json"))
                if (Path.GetFileName(path) != CharacterTacticalAssemblyPublication.FileName)
                    File.Copy(path, Path.Combine(content, Path.GetFileName(path)));
            var physical = await CharacterTacticalSaveStore.CreateAsync(persistence,
                "tactical-slice-progress", "character-tactical-slice-json-v1");
            var store = new Store(physical);
            var progress = new CharacterTacticalProgressStore(store, await store.LoadAsync());
            var initial = await CharacterTacticalServices.OpenCandidateCoreAsync(progress.Create(), content);
            initial.Progress.Retire();
            Assert.IsFalse(initial.InventoryRegistryMigrated);
            string oldPlayer = initial.Player.CaptureProgressJson(), oldPreparation = initial.Preparation.CaptureProgressJson();
            string checkpoint = await store.LoadAsync();
            var openingCandidate = progress.OpenActive(); Assert.IsTrue(openingCandidate.HasSourceProgress);
            File.Copy(Path.GetFullPath(Path.Combine(Application.dataPath,
                "../Authoring/TacticalAccessories/tactical_card_assembly.double-edge-three.v1.json")),
                Path.Combine(content, CharacterTacticalAssemblyPublication.FileName));

            if (mode == "stale-cas") {
                var otherPhysical = await CharacterTacticalSaveStore.CreateAsync(persistence,
                    "tactical-slice-progress", "character-tactical-slice-json-v1");
                var otherProgress = new CharacterTacticalProgressStore(otherPhysical, await otherPhysical.LoadAsync());
                var other = otherProgress.OpenActive(); other.Bind(() => oldPlayer, () => oldPreparation);
                await other.SaveAsync(activate: true); checkpoint = await otherPhysical.LoadAsync();
            }
            store.RejectWrites = mode == "save-failure";
            int writesBefore = store.Writes;
            var opening = new CharacterTacticalServiceOpening(() => CharacterTacticalServices.OpenCandidateCoreAsync(openingCandidate, content));
            Exception failure = null;
            try { await opening.OpenAsync(); } catch (Exception error) { failure = error; }
            Assert.AreEqual(writesBefore + 1, store.Writes, "An old migrated profile must attempt the aggregate save at real startup.");
            var reader = await CharacterTacticalSaveStore.CreateAsync(persistence,
                "tactical-slice-progress", "character-tactical-slice-json-v1");
            string durable = await reader.LoadAsync();
            if (mode == "success") {
                Assert.IsNull(failure); Assert.NotNull(opening.Published); Assert.IsTrue(opening.Published.InventoryRegistryMigrated);
                var saved = new CharacterTacticalProgressStore(reader, durable).OpenActive();
                Assert.AreEqual(opening.Published.Player.CaptureProgressJson(), await saved.PlayerStore.LoadAsync());
                Assert.AreEqual(oldPreparation, await saved.PreparationStore.LoadAsync());
                Assert.AreNotEqual(oldPlayer, await saved.PlayerStore.LoadAsync());
                opening.Published.Progress.Retire();
            } else {
                Assert.NotNull(failure); Assert.IsNull(opening.Published); Assert.IsNotEmpty(opening.Issue);
                Assert.IsTrue(JToken.DeepEquals(JToken.Parse(checkpoint), JToken.Parse(durable)), "The original/latest checkpoint must remain unchanged.");
                Assert.AreEqual(oldPlayer, await progress.OpenActive().PlayerStore.LoadAsync());
                Assert.AreEqual(oldPreparation, await progress.OpenActive().PreparationStore.LoadAsync());
            }
            // Evidence stays in this unique temp root; no user root, static Opening or scene was replaced.
        }
    }
}
