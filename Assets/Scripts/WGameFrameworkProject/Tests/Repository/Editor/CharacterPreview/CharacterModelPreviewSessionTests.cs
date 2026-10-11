using System;
using MxFramework.Editor.CharacterPreview;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MxFramework.Tests.Editor.CharacterPreview
{
    public sealed class CharacterModelPreviewSessionTests
    {
        private Scene _scene;
        private CharacterModelPreviewSession _session;
        private CharacterModelPreviewChoice _character;

        [SetUp]
        public void SetUp()
        {
            var characters = CharacterModelPreviewCatalog.DiscoverCharacters();
            if (characters.Length == 0) Assert.Ignore("Requires a repository-imported character package.");
            _character = characters[0];
            _scene = EditorSceneManager.NewPreviewScene();
            Assert.That(CharacterModelPreviewSession.TryCreate(_character, _character.DefaultLoadoutKey, false,
                _scene, out _session, out string diagnostic), Is.True, diagnostic);
        }

        [TearDown]
        public void TearDown()
        {
            _session?.Dispose();
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
        }

        [Test]
        public void EquipmentFailurePreservesAnimatorAndExistingWeapons()
        {
            Animator animator = _session.Animator;
            int weapons = _session.WeaponCount;
            string loadout = _session.LoadoutKey;
            Assert.That(_session.TrySetEquipment("nonexistent-preview-test-loadout", false, out string error), Is.False);
            Assert.That(error, Does.Contain("nonexistent-preview-test-loadout"));
            Assert.That(_session.Animator, Is.SameAs(animator));
            Assert.That(_session.WeaponCount, Is.EqualTo(weapons));
            Assert.That(_session.LoadoutKey, Is.EqualTo(loadout));
        }

        [Test]
        public void EveryLoadoutCanBeAppliedWithoutReplacingAnimatorOrPlayingAnimation()
        {
            Animator animator = _session.Animator;
            var identities = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (var loadout in CharacterModelPreviewCatalog.DiscoverLoadouts(_character))
            {
                Assert.That(identities.Add(loadout.Key), Is.True);
                Assert.That(_session.TrySetEquipment(loadout.Key, false, out string error), Is.True, error);
                Assert.That(_session.Animator, Is.SameAs(animator));
                Assert.That(animator.runtimeAnimatorController, Is.Null);
                Assert.That(_session.TrySetEquipment(loadout.Key, true, out error), Is.True, error);
                Assert.That(_session.WeaponsHidden, Is.True);
            }
            foreach (var component in _session.Instance.GetComponentsInChildren<MonoBehaviour>(true))
                Assert.That(component.enabled, Is.False, component.GetType().FullName);
        }

        [Test]
        public void DisposeRemovesOwnedRootsAndLeavesMainSceneUntouched()
        {
            Scene main = SceneManager.GetActiveScene();
            bool dirty = main.isDirty;
            int roots = main.rootCount;
            var instance = _session.Instance;
            Assert.That(instance.scene, Is.EqualTo(_scene));
            _session.Dispose();
            _session.Dispose();
            Assert.That(instance == null, Is.True);
            Assert.That(_scene.rootCount, Is.Zero);
            Assert.That(main.rootCount, Is.EqualTo(roots));
            Assert.That(main.isDirty, Is.EqualTo(dirty));
        }
    }
}
