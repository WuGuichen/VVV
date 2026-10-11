using MxFramework.Story;
using NUnit.Framework;

namespace MxFramework.Tests.Story
{
    public class StoryBlackboardTests
    {
        [Test]
        public void CopyOrderedSortsByNamespaceThenId()
        {
            var blackboard = new StoryBlackboard();
            blackboard.Set(new StoryFactKey(2, 10), StoryValue.FromInt32(20));
            blackboard.Set(new StoryFactKey(1, 30), StoryValue.FromInt32(13));
            blackboard.Set(new StoryFactKey(1, 20), StoryValue.FromInt32(12));

            var buffer = new StoryFactEntry[3];
            StoryFactCopyResult result = blackboard.CopyOrdered(buffer);

            Assert.IsTrue(result.Complete);
            Assert.AreEqual(3, result.RequiredCount);
            Assert.AreEqual(new StoryFactKey(1, 20), buffer[0].Key);
            Assert.AreEqual(new StoryFactKey(1, 30), buffer[1].Key);
            Assert.AreEqual(new StoryFactKey(2, 10), buffer[2].Key);
        }

        [Test]
        public void CopyOrderedReportsRequiredCountWhenBufferTooSmall()
        {
            var blackboard = new StoryBlackboard();
            blackboard.Set(new StoryFactKey(0, 2), StoryValue.FromBool(true));
            blackboard.Set(new StoryFactKey(0, 1), StoryValue.FromBool(false));

            var buffer = new StoryFactEntry[1];
            StoryFactCopyResult result = blackboard.CopyOrdered(buffer);

            Assert.IsFalse(result.Complete);
            Assert.AreEqual(2, result.RequiredCount);
            Assert.AreEqual(1, result.WrittenCount);
            Assert.AreEqual(new StoryFactKey(0, 1), buffer[0].Key);
        }

        [Test]
        public void SetRejectsInvalidKeys()
        {
            var blackboard = new StoryBlackboard();

            Assert.Throws<System.ArgumentOutOfRangeException>(() => blackboard.Set(new StoryFactKey(-1, 1), StoryValue.FromInt32(1)));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => blackboard.Set(new StoryFactKey(0, 0), StoryValue.FromInt32(1)));
        }
    }
}
