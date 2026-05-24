using MxFramework.Gameplay;
using MxFramework.Story.GameplayBridge;
using NUnit.Framework;

namespace MxFramework.Tests.StoryGameplayBridge
{
    public sealed class StoryBeatGameplayLocatorTests
    {
        [Test]
        public void ResolvesStableComponentEntityRef()
        {
            var world = new GameplayComponentWorld();
            GameplayEntityId entity = world.CreateEntity();
            var locator = new StoryBeatGameplayLocator();
            locator.RegisterBeatEntity(1001, 2001, StoryGameplayEntityRef.ComponentEntity(entity));

            StoryGameplayEntityResolutionResult result = locator.ResolveBeat(1001, 2001, world);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(entity, result.ComponentEntityId);
            Assert.AreEqual(0, result.LegacyRuntimeEntityId);
        }

        [Test]
        public void MissingEntityReturnsDiagnostic()
        {
            var locator = new StoryBeatGameplayLocator();

            StoryGameplayEntityResolutionResult result = locator.ResolveBeat(1001, 2001, new GameplayComponentWorld());

            Assert.IsFalse(result.Success);
            Assert.AreEqual(StoryGameplayBridgeDiagnosticCode.MissingEntityRef, result.Diagnostic.Code);
        }

        [Test]
        public void StaleComponentEntityReturnsDiagnostic()
        {
            var world = new GameplayComponentWorld();
            GameplayEntityId entity = world.CreateEntity();
            Assert.IsTrue(world.DestroyEntity(entity));
            var locator = new StoryBeatGameplayLocator();
            locator.RegisterBeatEntity(1001, 2001, StoryGameplayEntityRef.ComponentEntity(entity));

            StoryGameplayEntityResolutionResult result = locator.ResolveBeat(1001, 2001, world);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(StoryGameplayBridgeDiagnosticCode.StaleEntityRef, result.Diagnostic.Code);
        }
    }
}
