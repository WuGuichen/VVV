using System;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestLevelWorldPresentationNativeTests
    {
        private static CharacterTestWorldObjectSceneCache Cache(Transform transform, int id)
        {
            var cache = new CharacterTestWorldObjectSceneCache();
            cache.Configure(new[] {new CharacterTestWorldObjectSceneSource.MovableInstance(true, 1, id,
                "fixture.object", transform, CharacterTestWorldObjectInstanceScaleSource.SceneTransform)},
                Array.Empty<CharacterTestWorldObjectSceneSource.StaticColliderInstance>());
            return cache;
        }
        private static CharacterTestMovableNaturalObjectState State(int id, float x) => new CharacterTestMovableNaturalObjectState(
            CharacterTestMovableNaturalObjectSpawnSpec.FromMeters("fixture.object",id,"fixture",1,default,
                Vector3.one,new Vector3(x,0,0)));

        [Test] public void SelectedNumericIdSyncsAndPreviousLevelNumericIdCannotMoveItsView()
        {
            var view = new GameObject("Temporary level world view");
            try {
                var old = Cache(view.transform, 41); var selected = Cache(view.transform, 42);
                var bridge = CharacterTestRuntimeSessionBuilder.CreateLevelWorldViewBridge(selected, old,
                    () => throw new InvalidOperationException("Legacy fallback must not be read."));
                Assert.That(bridge.Sync(new[]{State(41,5)}),Is.EqualTo(0));
                Assert.That(view.transform.position.x,Is.EqualTo(0));
                Assert.That(bridge.Sync(new[]{State(42,3)}),Is.EqualTo(1));
                Assert.That(view.transform.position.x,Is.EqualTo(3));
            } finally { UnityEngine.Object.DestroyImmediate(view); }
        }
        [Test] public void SelectedEmptyLevelDoesNotResurrectSerializedOrLegacyMovables()
        {
            var view = new GameObject("Temporary empty level view");
            try {
                var bridge = CharacterTestRuntimeSessionBuilder.CreateLevelWorldViewBridge(new CharacterTestWorldObjectSceneCache(),
                    Cache(view.transform,41),()=>throw new InvalidOperationException("No legacy fallback for a selected empty level."));
                Assert.That(bridge.MarkerCount,Is.EqualTo(0));
                Assert.That(bridge.Sync(new[]{State(41,5)}),Is.EqualTo(0));
                Assert.That(view.transform.position.x,Is.EqualTo(0));
            } finally { UnityEngine.Object.DestroyImmediate(view); }
        }
    }
}
