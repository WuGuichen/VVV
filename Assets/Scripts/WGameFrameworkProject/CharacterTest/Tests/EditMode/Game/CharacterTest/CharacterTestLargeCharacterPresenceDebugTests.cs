using System;
using System.IO;
using System.Linq;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Game.CharacterTest;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestLargeCharacterPresenceDebugTests
    {
        private static readonly string[] ExpectedVolumeIds =
        {
            "presence.000003.core.neck",
            "presence.000003.core.tail_root",
            "presence.000003.core.torso"
        };

        [Test]
        public void Dragon14Fixture_ProjectsV4PresenceAndCenterDebtAsContactAuthority()
        {
            CharacterImportedGeometryBinding binding = LoadDragon14Package().Geometry;
            CharacterTestLargeCharacterPresenceDebugSnapshot snapshot =
                CharacterTestLargeCharacterPresenceDebug.Project(
                    entityId: 14,
                    characterStableId: "char.000003",
                    binding,
                    hardRootRadiusMillimeters: 1300,
                    hardRootHeightMillimeters: 4200,
                    hardRootCenterYMillimeters: 2100,
                    animatedHurtboxCount: 18,
                    animatedHurtboxRevision: 7);

            Assert.AreEqual("mx.characterGeometryBinding.v4", binding.Format);
            Assert.AreEqual("4.0", binding.SchemaVersion);
            Assert.AreEqual(CharacterBodyPresenceMode.Authored, binding.BodyPresenceMode);
            Assert.AreEqual(1, binding.BodyPresenceRevision);
            Assert.That(binding.BodyPresenceContentHash, Does.StartWith("sha256:"));
            CollectionAssert.AreEqual(
                ExpectedVolumeIds,
                binding.BodyPresenceVolumes.Select(volume => volume.VolumeId).ToArray());
            Assert.That(binding.BodyPresenceVolumes, Has.Length.EqualTo(3));
            Assert.That(binding.BodyPresenceVolumes, Has.All.Matches<CharacterBodyPresenceRuntimeBinding>(
                volume => volume.ReferenceFrame == CharacterBodyPresenceReferenceFrame.ModelRoot
                    && volume.Shape == CharacterBodyPresenceShape.Capsule
                    && volume.SkinMillimeters == 50
                    && volume.PressureScaleMilli == 1000
                    && volume.Enabled));

            Assert.AreEqual(0f, binding.AuthorCapsuleCenterX);
            Assert.AreEqual(0.5f, binding.AuthorCapsuleCenterZ);
            Assert.AreEqual(0f, binding.RuntimeConsumedCapsuleCenterX);
            Assert.AreEqual(0f, binding.RuntimeConsumedCapsuleCenterZ);
            Assert.IsFalse(binding.RuntimeConsumesAuthorCapsuleCenterXZ);
            Assert.IsTrue(snapshot.HasUnconsumedAuthorCenterXZ);
            Assert.IsTrue(snapshot.GameplayAffecting);

            CharacterTestDeveloperDebugCard card =
                CharacterTestLargeCharacterPresenceDebug.CreateCard(snapshot);
            string projection = string.Join(
                "\n",
                card.Lines.Select(line => line.Name + "=" + line.Value));
            Assert.That(projection, Does.Contain("Hard Root="));
            Assert.That(projection, Does.Contain("Body Presence="));
            Assert.That(projection, Does.Contain("Animated Hurtbox="));
            Assert.That(projection, Does.Contain(
                CharacterTestLargeCharacterPresenceDebug.CenterDiagnosticCode));
            Assert.That(projection, Does.Contain("source=(0, 0.5) runtime-consumed=(0, 0)"));
            Assert.That(projection, Does.Contain("Gameplay=affecting"));
            Assert.That(projection, Does.Contain(
                "Projection=read-only diagnostics; no authority writes"));
            Assert.That(projection, Does.Contain(
                "Excluded=" + CharacterTestLargeCharacterPresenceDebug.ExcludedGeometry));
        }

        [Test]
        public void Dragon14Fixture_ExcludesAppendagesAndKeepsNaturalAttackTraceWindowsSeparate()
        {
            CharacterImportedGeometryBinding binding = LoadDragon14Package().Geometry;
            string joinedIds = string.Join(",", binding.BodyPresenceVolumes.Select(volume => volume.VolumeId));
            Assert.That(joinedIds, Does.Not.Contain("wing"));
            Assert.That(joinedIds, Does.Not.Contain("tail_tip"));
            Assert.That(joinedIds, Does.Not.Contain("head_tip"));
            Assert.That(joinedIds, Does.Not.Contain("trace").IgnoreCase);

            JObject actions = JObject.Parse(File.ReadAllText(ProjectPath(
                "Authoring/CharacterPackages/character-000003/config/character_actions.json")));
            JObject[] hitTraceWindows = actions["actions"]
                .Children<JObject>()
                .SelectMany(action => action["combatTrack"] is JObject combatTrack
                    ? combatTrack["events"]?.Children<JObject>() ?? Enumerable.Empty<JObject>()
                    : Enumerable.Empty<JObject>())
                .Where(value => string.Equals(
                    (string)value["kind"],
                    "HitTraceWindow",
                    StringComparison.Ordinal))
                .ToArray();

            Assert.That(hitTraceWindows, Has.Length.EqualTo(5));
            Assert.That(hitTraceWindows, Has.All.Matches<JObject>(window =>
            {
                JObject hitVolume = window["hitVolume"] as JObject;
                return hitVolume != null
                    && string.Equals(
                        (string)hitVolume["sourceKind"],
                        "BakedSocketCapsule",
                        StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace((string)hitVolume["startSocketId"])
                    && !string.IsNullOrWhiteSpace((string)hitVolume["endSocketId"])
                    && !window.ToString().Contains("BodyPresence", StringComparison.OrdinalIgnoreCase);
            }));
        }

        private static CharacterImportedPackage LoadDragon14Package()
        {
            return CharacterImportedPackageJson.LoadFromDirectory(ProjectPath(
                "Assets/MxFrameworkGenerated/CharacterPackages/000003"));
        }

        private static string ProjectPath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), relativePath));
        }
    }
}
