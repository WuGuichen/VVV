using System;
using System.IO;
using System.Text;
using MxFramework.Config.Levels;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestLevelPublicationTests
    {
        private string _root, _source, _runtime, _index, _expected;
        private byte[] _oldSource, _oldIndex;
        private MxLevelAuthoringDocument _candidate;

        [SetUp] public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "mx-level-publish-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _source = Path.Combine(_root, "source.level.json");
            _runtime = Path.Combine(_root, "runtime");
            _index = Path.Combine(_runtime, "directory.json");
            var old = Document("Assets/Scenes/Before.unity");
            string json = MxLevelAuthoringJson.SaveToJson(old);
            File.WriteAllText(_source, json, new UTF8Encoding(false));
            CharacterTestLevelCatalog.Publish(old, "test", _runtime);
            _candidate = Document("Assets/Scenes/After.unity");
            _expected = CharacterTestLevelCatalog.Hash(json);
            _oldSource = File.ReadAllBytes(_source);
            _oldIndex = File.ReadAllBytes(_index);
        }

        private static MxLevelAuthoringDocument Document(string scene) => new MxLevelAuthoringDocument {
            LevelId = "publication.test", ScenePath = scene, RootTransformRef = LevelTransformReference.FromPath("Root")
        };

        [TearDown] public void TearDown()
        {
            if (File.Exists(_source)) File.SetAttributes(_source, FileAttributes.Normal);
            if (File.Exists(_index)) File.SetAttributes(_index, FileAttributes.Normal);
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        private void AssertOldPublication()
        {
            CollectionAssert.AreEqual(_oldSource, File.ReadAllBytes(_source), "Exact authoring bytes restored.");
            CollectionAssert.AreEqual(_oldIndex, File.ReadAllBytes(_index), "Runtime publication reference must remain unchanged.");
            Assert.That(CharacterTestLevelCatalog.Read(_index).Load("publication.test", _runtime, true, out _).ScenePath,
                Is.EqualTo("Assets/Scenes/Before.unity"));
        }

        [TestCase("BeforeSourceWrite")]
        [TestCase("DuringSourceWrite")]
        [TestCase("AfterSourceWrite")]
        [TestCase("BeforeRuntimeDirectoryCommit")]
        public void PublicationFaultPreservesOldSourceAndRuntime(string phase)
        {
            Assert.Throws<IOException>(() => CharacterTestLevelCatalog.PublishWithAuthoringSource(
                _candidate, "test", _runtime, _source, _expected,
                current => { if (current == phase) throw new IOException("Injected " + phase); }));
            AssertOldPublication();
        }

        [Test] public void ReadOnlySourceRejectsBeforeRuntimeChange()
        {
            File.SetAttributes(_source, FileAttributes.ReadOnly);
            Assert.Throws<IOException>(() => CharacterTestLevelCatalog.PublishWithAuthoringSource(_candidate, "test", _runtime, _source, _expected));
            AssertOldPublication();
        }

        [Test] public void StaleSourceHashKeepsConcurrentAuthoringAndOldRuntime()
        {
            const string concurrent = "Concurrent author edit";
            File.WriteAllText(_source, concurrent);
            Assert.Throws<InvalidOperationException>(() => CharacterTestLevelCatalog.PublishWithAuthoringSource(_candidate, "test", _runtime, _source, _expected));
            Assert.That(File.ReadAllText(_source), Is.EqualTo(concurrent));
            CollectionAssert.AreEqual(_oldIndex, File.ReadAllBytes(_index));
        }

        [Test] public void SourceIsLockedUntilRuntimeCommitAndBothHashesAgree()
        {
            bool blocked = false;
            string hash = CharacterTestLevelCatalog.PublishWithAuthoringSource(_candidate, "test", _runtime, _source, _expected,
                phase => {
                    if (phase != "AfterSourceWrite") return;
                    Assert.Throws<IOException>(() => File.WriteAllText(_source, "Concurrent writer"));
                    blocked = true;
                });
            Assert.That(blocked, Is.True);
            Assert.That(CharacterTestLevelCatalog.Hash(File.ReadAllText(_source)), Is.EqualTo(hash));
            var catalog = CharacterTestLevelCatalog.Read(_index);
            Assert.That(catalog.Levels[0].DocumentHash, Is.EqualTo(hash));
            Assert.That(catalog.Load("publication.test", _runtime, true, out _).ScenePath, Is.EqualTo(_candidate.ScenePath));
        }

        [Test] public void ActualRuntimeDirectoryWriteFailureRestoresAuthoring()
        {
            File.SetAttributes(_index, FileAttributes.ReadOnly);
            var failure = Assert.Catch(() => CharacterTestLevelCatalog.PublishWithAuthoringSource(_candidate, "test", _runtime, _source, _expected));
            Assert.That(failure is IOException || failure is UnauthorizedAccessException, Is.True,
                "The actual filesystem commit must fail, rather than an unrelated implementation assertion.");
            File.SetAttributes(_index, FileAttributes.Normal);
            AssertOldPublication();
        }
    }

    public sealed class CharacterTestLevelAdmissionTests
    {
        [Test] public void LegacyMixedRunWithoutCounterCannotBeReused()
        {
            Assert.That(CharacterTestRuntimeSessionBuilder.NextStageRunId(0, 1), Is.EqualTo(2UL));
            Assert.That(CharacterTestRuntimeSessionBuilder.NextStageRunId(2, 1), Is.EqualTo(3UL));
        }
        [Test] public void ExhaustedRunIdentityRejectsInsteadOfWrapping()
        {
            Assert.Throws<InvalidOperationException>(() => CharacterTestRuntimeSessionBuilder.NextStageRunId(0, ulong.MaxValue));
        }
        [Test] public void EnabledNavigationWithZeroCellSizeRejectsAdmission()
        {
            var error = Assert.Throws<InvalidOperationException>(() => CharacterTestRuntimeSessionBuilder.CompileRequiredLevelNavigation(
                "level.bad", CharacterTestNavigationSceneSettings.CreateForTests(cellSizeMeters: 0), null, out _));
            StringAssert.Contains("level.bad/navigation/input: InvalidCellSize", error.Message);
        }
        [Test] public void MissingNavigationSettingsRejectsAdmission()
        {
            Assert.Throws<InvalidOperationException>(() => CharacterTestRuntimeSessionBuilder.CompileRequiredLevelNavigation("level.bad", null, null, out _));
        }
        [Test] public void ExplicitlyDisabledNavigationAllowsUnavailableSession()
        {
            var input = CharacterTestRuntimeSessionBuilder.CompileRequiredLevelNavigation("level.disabled",
                CharacterTestNavigationSceneSettings.CreateForTests(enabled: false, cellSizeMeters: 0), null, out var result);
            Assert.That(input.HasValue, Is.False);
            var session = CharacterTestNavigationSessionBuilder.Build(input, null, result);
            Assert.DoesNotThrow(() => CharacterTestRuntimeSessionBuilder.ValidateRequiredLevelNavigation("level.disabled", result, session.BuildResult));
        }
        [Test] public void EnabledNavigationGraphFailureRejectsAdmission()
        {
            var input = new CharacterTestNavigationSceneInputResult(CharacterTestNavigationSceneInputStatus.Success);
            var failed = new CharacterTestNavigationSessionBuildResult(CharacterTestNavigationSessionBuildStatus.NoWalkableNodes);
            StringAssert.Contains("NoWalkableNodes", Assert.Throws<InvalidOperationException>(() =>
                CharacterTestRuntimeSessionBuilder.ValidateRequiredLevelNavigation("level.bad", input, failed)).Message);
        }
    }
}
