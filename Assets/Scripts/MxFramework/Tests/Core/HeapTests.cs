using NUnit.Framework;
using MxFramework.Core.Collections;

namespace MxFramework.Tests.Core.Collections
{
    public class TestHeapItem : IHeapItem<TestHeapItem>
    {
        public int Value;
        public int HeapIndex { get; set; }

        public TestHeapItem(int value) { Value = value; HeapIndex = -1; }

        public int CompareTo(TestHeapItem other) => Value.CompareTo(other.Value);
    }

    public class HeapTests
    {
        [Test]
        public void Push_Pop_ReturnsMinFirst()
        {
            var heap = new Heap<TestHeapItem>(10);
            heap.Push(new TestHeapItem(5));
            heap.Push(new TestHeapItem(3));
            heap.Push(new TestHeapItem(7));

            Assert.AreEqual(3, heap.Top.Value);
            Assert.AreEqual(3, heap.Count);

            heap.Pop();
            Assert.AreEqual(5, heap.Top.Value);
            Assert.AreEqual(2, heap.Count);

            heap.Pop();
            Assert.AreEqual(7, heap.Top.Value);
        }

        [Test]
        public void MaxHeap_ReturnsMaxFirst()
        {
            var heap = new Heap<TestHeapItem>(10, isMaxHeap: true);
            heap.Push(new TestHeapItem(3));
            heap.Push(new TestHeapItem(7));
            heap.Push(new TestHeapItem(5));

            Assert.AreEqual(7, heap.Top.Value);
        }

        [Test]
        public void IsEmpty_WhenNew_ReturnsTrue()
        {
            var heap = new Heap<TestHeapItem>(10);
            Assert.IsTrue(heap.IsEmpty);
        }

        [Test]
        public void IsFull_WhenAtCapacity_ReturnsTrue()
        {
            var heap = new Heap<TestHeapItem>(3);
            heap.Push(new TestHeapItem(1));
            heap.Push(new TestHeapItem(2));
            heap.Push(new TestHeapItem(3));

            Assert.IsTrue(heap.IsFull);
        }

        [Test]
        public void Clear_ResetsCount()
        {
            var heap = new Heap<TestHeapItem>(10);
            var first = new TestHeapItem(1);
            var second = new TestHeapItem(2);
            heap.Push(first);
            heap.Push(second);
            heap.Clear();

            Assert.AreEqual(0, heap.Count);
            Assert.IsTrue(heap.IsEmpty);
            Assert.AreEqual(-1, first.HeapIndex);
            Assert.AreEqual(-1, second.HeapIndex);
        }

        [Test]
        public void Top_WhenEmpty_Throws()
        {
            var heap = new Heap<TestHeapItem>(10);
            Assert.Throws<System.InvalidOperationException>(() => _ = heap.Top);
        }

        [Test]
        public void Contains_ReturnsTrue_WhenItemPushed()
        {
            var heap = new Heap<TestHeapItem>(10);
            var item = new TestHeapItem(42);
            heap.Push(item);

            Assert.IsTrue(heap.Contains(item));
        }

        [Test]
        public void Contains_ReturnsFalse_AfterPop()
        {
            var heap = new Heap<TestHeapItem>(10);
            var item = new TestHeapItem(42);
            heap.Push(item);
            heap.Pop();

            Assert.IsFalse(heap.Contains(item));
            Assert.AreEqual(-1, item.HeapIndex);
        }

        [Test]
        public void LargePushPop_MaintainsHeapProperty()
        {
            var heap = new Heap<TestHeapItem>(100);
            for (int i = 99; i >= 0; i--)
                heap.Push(new TestHeapItem(i));

            for (int expected = 0; expected < 100; expected++)
            {
                Assert.AreEqual(expected, heap.Top.Value);
                heap.Pop();
            }
            Assert.IsTrue(heap.IsEmpty);
        }
    }
}
