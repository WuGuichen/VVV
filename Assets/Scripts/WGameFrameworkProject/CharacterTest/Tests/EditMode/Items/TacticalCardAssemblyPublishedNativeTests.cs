using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Items;
using MxFramework.Runtime;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace MxFramework.Tests.Items
{
    /// <summary>Actual published composition-root checks, isolated from user profiles and the parked working scene.</summary>
    [Parallelizable(ParallelScope.None)]
    public sealed class TacticalCardAssemblyPublishedNativeTests
    {
        private const string SaveRootVariable = "MX_CHARACTERTEST_TACTICAL_SAVE_ROOT";
        private const string SessionKey = "MxFramework.P1PublishedNative.";
        private string _root;

        [UnitySetUp]
        public IEnumerator EnterIsolatedPublishedScene()
        {
            Assert.IsFalse(EditorApplication.isPlaying);
            string previous = Environment.GetEnvironmentVariable(SaveRootVariable);
            SessionState.SetBool(SessionKey + "HadPreviousRoot", previous != null);
            SessionState.SetString(SessionKey + "PreviousRoot", previous ?? string.Empty);
            // Keep the bootstrap's profile/slot/staging descendants within Windows path limits.
            string parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MxP1"));
            _root = Path.GetFullPath(Path.Combine(parent, Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(_root);
            SessionState.SetString(SessionKey + "Root", _root);
            // Set BEFORE Play: the real scene bootstrap also writes only within this temporary root.
            Environment.SetEnvironmentVariable(SaveRootVariable, Path.Combine(_root, "bootstrap"));
            EditorSceneManager.OpenScene("Assets/Scenes/TacticalCardDemoScene.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode();
            _root = SessionState.GetString(SessionKey + "Root", string.Empty);
            Assert.IsNotEmpty(_root);
            Assert.AreEqual(Path.Combine(_root, "bootstrap"), Environment.GetEnvironmentVariable(SaveRootVariable));
            // EnterPlayMode finishes before the real asynchronous bootstrap publishes its services.
            // The legacy regression must capture a real prior publication for its finally restoration.
            var opening = CharacterTacticalServices.OpenAsync();
            for (int attempt = 0; !opening.IsCompleted && attempt < 1200; attempt++) yield return null;
            Assert.IsTrue(opening.IsCompleted, "The isolated formal bootstrap must become ready.");
            if (opening.IsFaulted) throw opening.Exception.GetBaseException();
            Assert.IsFalse(opening.IsCanceled);
            Assert.NotNull(opening.Result);
        }

        [UnityTearDown]
        public IEnumerator ExitIsolatedPublishedScene()
        {
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
            Environment.SetEnvironmentVariable(SaveRootVariable, SessionState.GetBool(SessionKey + "HadPreviousRoot", false)
                ? SessionState.GetString(SessionKey + "PreviousRoot", string.Empty) : null);
            // Leave reports and the isolated envelopes for inspection; no player data is reset or removed.
        }

        [Test]
        public async Task PublishedMainCardGrant_V6ReopensThroughCompositionRoot_NoAutomaticGrants()
        {
            Assert.IsTrue(Application.isPlaying);
            string root = Path.Combine(_root, "v6-probe");
            var physical = await CharacterTacticalSaveStore.CreateAsync(root, "tactical-slice-progress", "character-tactical-slice-json-v1");
            Assert.IsNull(await physical.LoadAsync());
            var progress = new CharacterTacticalProgressStore(physical, null);
            var candidate = progress.Create();
            var services = await Build(candidate);
            var player = services.Player;
            Assert.IsTrue(player.TryReadOwnedCharacterItems(services.InventoryOwner, out var owned, out string issue), issue);
            Assert.AreEqual(1, owned.Length);
            var role = owned[0]; var item = new ItemInstanceId(role.ItemInstanceId);
            var initial = JsonConvert.DeserializeObject<CharacterTacticalPlayerState>(player.CaptureProgressJson());
            Assert.AreEqual(CharacterTacticalPlayerState.CharacterTacticalSchema, initial.Schema);
            string opponentBefore = JsonConvert.SerializeObject(initial.CharacterItems.Where(value => value.ItemInstanceId != item.Value));
            var definition = player.Catalog.Pool(role.CharacterStableId)?.Cards.FirstOrDefault(value => player.Catalog.ExactCard(value)?.HasActionSlot == true);
            Assert.NotNull(definition, "The actual formal acquisition scope must contain a resolvable behavior main card; no fallback definition.");
            var reference = JsonConvert.DeserializeObject<MxFramework.CharacterApplication.CharacterTacticalCardReference>(JsonConvert.SerializeObject(definition));
            reference.InstanceId = 0;
            long frame = checked(player.LastItemTransactionFrame + 1);
            var request = new CharacterTacticalContentGrantRequest { Kind = CharacterTacticalContentKind.MainCard, RequestId = 10001,
                OwnerId = services.InventoryOwner.Value, CharacterItemInstanceId = item.Value, MainCard = reference,
                ExpectedCharacterRevision = role.Revision, ExpectedPlayerRevision = player.Revision, Frame = frame };
            ulong allocated = 0;
            Run(frame, context => {
                var receipt = player.GrantContentAtTick(request, context, out var grantIssue);
                Assert.IsTrue(receipt.Result.Success, grantIssue); allocated = receipt.CardInstanceId;
                Assert.Greater(allocated, 0UL);
                var replay = player.GrantContentAtTick(request, context, out grantIssue);
                Assert.AreEqual(ItemAcquisitionStatus.AlreadyCommitted, replay.Result.Status, grantIssue);
                Assert.AreEqual(allocated, replay.CardInstanceId);
            });
            Assert.IsTrue(player.TryReadCharacterItem(item, services.InventoryOwner, out var granted, out issue), issue);
            Assert.AreEqual(role.Cards.Count + 1, granted.Cards.Count);
            Assert.AreEqual(role.ActionGrants.Count, granted.ActionGrants.Count);
            Assert.IsFalse(granted.Bindings.Any(value => value.CardInstanceId == allocated));
            Assert.IsTrue(await player.FlushAsync()); Assert.IsFalse(player.HasUnsavedChanges);
            Assert.IsTrue(await services.Preparation.FlushAsync()); await candidate.SaveAsync(activate: true);
            string expectedPlayer = player.CaptureProgressJson(), expectedPreparation = services.Preparation.CaptureProgressJson();
            var v6 = JsonConvert.DeserializeObject<CharacterTacticalPlayerState>(expectedPlayer);
            Assert.AreEqual(CharacterTacticalPlayerState.CardAssemblySchema, v6.Schema); Assert.IsEmpty(v6.AccessoryItems);
            Assert.IsFalse(CharacterTacticalAssemblyRules.RequiresConfiguration(v6), "Main-card acquisition provenance does not fabricate an accessory publication.");
            Assert.AreEqual(opponentBefore, JsonConvert.SerializeObject(v6.CharacterItems.Where(value => value.ItemInstanceId != item.Value)));
            var reopenedPhysical = await CharacterTacticalSaveStore.CreateAsync(root, "tactical-slice-progress", "character-tactical-slice-json-v1");
            var reopenedProgress = new CharacterTacticalProgressStore(reopenedPhysical, await reopenedPhysical.LoadAsync());
            var reopened = await Build(reopenedProgress.OpenActive());
            Assert.AreEqual(expectedPlayer, reopened.Player.CaptureProgressJson());
            Assert.AreEqual(expectedPreparation, reopened.Preparation.CaptureProgressJson());
            Assert.IsTrue(reopened.Player.UsesCharacterItemTacticalProgress);
            Assert.IsTrue(reopened.Player.TryReadCharacterItem(item, services.InventoryOwner, out var restored, out issue), issue);
            Assert.AreEqual(allocated, restored.CardAcquisitions.Single(value => value.RequestId == 10001).CardInstanceId);
            Run(frame, context => {
                var replay = reopened.Player.GrantContentAtTick(request, context, out var replayIssue);
                Assert.AreEqual(ItemAcquisitionStatus.AlreadyCommitted, replay.Result.Status, replayIssue);
                Assert.AreEqual(allocated, replay.CardInstanceId);
            });
            Assert.AreEqual(expectedPlayer, reopened.Player.CaptureProgressJson());
            File.WriteAllText(Path.Combine(_root, "v6-acceptance.json"), JsonConvert.SerializeObject(new {
                passed = true, realBuildCandidate = true, actualPublishedCard = reference.CardId, reference.Revision, reference.ContentHash,
                schema = v6.Schema, characterItem = item.Value, cardInstance = allocated, realSimulationTick = frame,
                sameRequestIdentityRetained = true, exactPlayerAndPreparationRetained = true, opponentUnchanged = true,
                noAutomaticBindingOrLearning = true, actualPlayerSaveTouched = false, saveRoot = root
            }, Formatting.Indented));
        }

        [Test]
        public async Task ExistingPublishedCharacterRegression_InIsolatedFormalScene()
        {
            string previous = CharacterItemPublishedNativeTests.ValidationRoot;
            try
            {
                CharacterItemPublishedNativeTests.ValidationRoot = Path.Combine(_root, "legacy-regression");
                // Same existing regression body; this fixture supplies its isolated scene and save-root lifecycle.
                await new CharacterItemPublishedNativeTests().PublishedNewProfile_LearnBindUnbindReopenAndDeploy_UsesRealItemsAndEnvelope();
            }
            finally { CharacterItemPublishedNativeTests.ValidationRoot = previous; }
        }

        private static Task<CharacterTacticalServiceSet> Build(CharacterTacticalProgressCandidate candidate)
            => (Task<CharacterTacticalServiceSet>)typeof(CharacterTacticalServices).GetMethod("BuildCandidateAsync",
                BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { candidate });
        private static void Run(long frame, Action<RuntimeTickContext> action)
        {
            using var host = new RuntimeHost(new RuntimeHostOptions { ErrorPolicy = RuntimeHostErrorPolicy.FailFast });
            host.RegisterModule(new TickAction(action)); host.Initialize(); host.Start(); host.Tick(frame, 1d / 60d, frame / 60d);
            Assert.IsTrue(host.LastTickAttemptSucceeded);
        }
        private sealed class TickAction : RuntimeModule
        {
            private readonly Action<RuntimeTickContext> _action;
            internal TickAction(Action<RuntimeTickContext> action) : base("p1-native-card-grant", RuntimeTickStage.Simulation, 0) { _action = action; }
            public override void Tick(RuntimeTickContext context) => _action(context);
        }
    }
}
