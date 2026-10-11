using NUnit.Framework;
using MxFramework.Core.Collections;

namespace MxFramework.Tests.Core.Collections
{
    public class TestListItem : IUnsortListItem
    {
        public int Id;
        public int Index { get; set; }
        public bool MarkDelete { get; set; }

        public TestListItem(int id) { Id = id; Index = -1; MarkDelete = false; }
    }

    public class UnsortListTests
    {
        [Test]
        public void Add_IncrementsCount()
        {
            var list = new UnsortList<TestListItem>();
            list.Add(new TestListItem(1));
            list.Add(new TestListItem(2));

            Assert.AreEqual(2, list.Count);
        }

        [Test]
        public void Add_SetsCorrectIndex()
        {
            var list = new UnsortList<TestListItem>();
            var a = new TestListItem(10);
            var b = new TestListItem(20);
            list.Add(a);
            list.Add(b);

            Assert.AreEqual(0, a.Index);
            Assert.AreEqual(1, b.Index);
        }

        [Test]
        public void Remove_SwapLast_MaintainsCount()
        {
            var list = new UnsortList<TestListItem>();
            var a = new TestListItem(1);
            var b = new TestListItem(2);
            var c = new TestListItem(3);
            list.Add(a); list.Add(b); list.Add(c);

            list.Remove(b); // index 1
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual(3, list[1].Id); // c swapped into b's slot
            Assert.AreEqual(1, c.Index);     // c's index updated
            Assert.AreEqual(-1, b.Index);    // removed item is invalidated
        }

        [Test]
        public void RemoveDelayed_ThenOptimize_CompactsCorrectly()
        {
            var list = new UnsortList<TestListItem>();
            var a = new TestListItem(1);
            var b = new TestListItem(2);
            var c = new TestListItem(3);
            var d = new TestListItem(4);
            list.Add(a); list.Add(b); list.Add(c); list.Add(d);

            list.RemoveDelayed(b); // mark b
            list.RemoveDelayed(d); // mark d
            list.Optimize();

            Assert.AreEqual(2, list.Count);
            Assert.AreEqual(1, list[0].Id);
            Assert.AreEqual(3, list[1].Id);
            Assert.AreEqual(0, a.Index);
            Assert.AreEqual(1, c.Index);
            Assert.AreEqual(-1, b.Index);
            Assert.AreEqual(-1, d.Index);
        }

        [Test]
        public void Optimize_NoMarked_IsNoOp()
        {
            var list = new UnsortList<TestListItem>();
            list.Add(new TestListItem(1));
            list.Add(new TestListItem(2));
            list.Optimize();

            Assert.AreEqual(2, list.Count);
        }

        [Test]
        public void Clear_ResetsCount()
        {
            var list = new UnsortList<TestListItem>();
            var a = new TestListItem(1);
            var b = new TestListItem(2);
            list.Add(a);
            list.Add(b);
            list.Clear();

            Assert.AreEqual(0, list.Count);
            Assert.AreEqual(-1, a.Index);
            Assert.AreEqual(-1, b.Index);
            Assert.IsFalse(a.MarkDelete);
            Assert.IsFalse(b.MarkDelete);
        }

        [Test]
        public void Replace_SetsIndexAndClearsDeleteMark()
        {
            var list = new UnsortList<TestListItem>();
            list.Add(new TestListItem(1));
            var replacement = new TestListItem(2) { MarkDelete = true };

            list.Replace(0, replacement);

            Assert.AreEqual(0, replacement.Index);
            Assert.IsFalse(replacement.MarkDelete);
            Assert.AreEqual(2, list[0].Id);
        }

        [Test]
        public void Add_BeyondCapacity_GrowsCorrectly()
        {
            var list = new UnsortList<TestListItem>(2);
            list.Add(new TestListItem(1));
            list.Add(new TestListItem(2));
            list.Add(new TestListItem(3)); // triggers growth

            Assert.AreEqual(3, list.Count);
        }
    }
}
