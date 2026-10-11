using MxFramework.AI;
using NUnit.Framework;

namespace MxFramework.Tests.AI
{
    public class AiWorldStateTests
    {
        [Test]
        public void WorldState_SetAndReadTypedFact()
        {
            var world = new AiWorldState();
            var key = new AiFactKey("enemy.visible");

            world.SetValue(key, true);

            Assert.IsTrue(world.TryGetValue(key, out bool value));
            Assert.IsTrue(value);
            Assert.IsTrue(world.Contains(key));
        }

        [Test]
        public void WorldState_CloneDoesNotMutateOriginal()
        {
            var key = new AiFactKey("has.weapon");
            var world = new AiWorldState();
            world.SetValue(key, false);

            IAiWorldState clone = world.Clone();
            clone.SetValue(key, true);

            Assert.IsTrue(world.TryGetValue(key, out bool original));
            Assert.IsFalse(original);
            Assert.IsTrue(clone.TryGetValue(key, out bool cloned));
            Assert.IsTrue(cloned);
        }
    }
}
