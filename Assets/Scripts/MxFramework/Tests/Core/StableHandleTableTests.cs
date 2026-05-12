using NUnit.Framework;
using MxFramework.Core.Handles;

namespace MxFramework.Tests.Core.Handles
{
    public class StableHandleTableTests
    {
        [Test]
        public void Add_TryGet_ReturnsStoredValue()
        {
            var table = new StableHandleTable<string>();

            StableHandle handle = table.Add("value");

            Assert.IsTrue(handle.IsValid);
            Assert.IsTrue(table.TryGet(handle, out string value));
            Assert.AreEqual("value", value);
            Assert.AreEqual(1, table.ActiveCount);
            Assert.AreEqual(0, table.FreeCount);
        }

        [Test]
        public void Remove_InvalidatesHandle()
        {
            var table = new StableHandleTable<string>();
            StableHandle handle = table.Add("value");

            Assert.IsTrue(table.Remove(handle));

            Assert.IsFalse(table.TryGet(handle, out string value));
            Assert.IsNull(value);
            Assert.AreEqual(0, table.ActiveCount);
            Assert.AreEqual(1, table.FreeCount);
        }

        [Test]
        public void Add_AfterRemove_ReusesSlotAndRejectsStaleHandle()
        {
            var table = new StableHandleTable<string>();
            StableHandle oldHandle = table.Add("old");
            Assert.IsTrue(table.Remove(oldHandle));

            StableHandle newHandle = table.Add("new");

            Assert.AreEqual(oldHandle.Index, newHandle.Index);
            Assert.AreNotEqual(oldHandle.Generation, newHandle.Generation);
            Assert.IsFalse(table.TryGet(oldHandle, out string oldValue));
            Assert.IsNull(oldValue);
            Assert.IsTrue(table.TryGet(newHandle, out string newValue));
            Assert.AreEqual("new", newValue);
        }

        [Test]
        public void DefaultHandle_IsInvalid()
        {
            var handle = default(StableHandle);
            var table = new StableHandleTable<int>();

            Assert.IsFalse(handle.IsValid);
            Assert.IsFalse(table.TryGet(handle, out int value));
            Assert.AreEqual(0, value);
            Assert.IsFalse(table.Remove(handle));
        }

        [Test]
        public void Clear_InvalidatesExistingHandles()
        {
            var table = new StableHandleTable<int>();
            StableHandle first = table.Add(10);
            StableHandle second = table.Add(20);

            table.Clear();

            Assert.IsFalse(table.TryGet(first, out int firstValue));
            Assert.IsFalse(table.TryGet(second, out int secondValue));
            Assert.AreEqual(0, firstValue);
            Assert.AreEqual(0, secondValue);
            Assert.AreEqual(0, table.ActiveCount);
            Assert.AreEqual(2, table.FreeCount);
        }

        [Test]
        public void Counts_TrackActiveAndFreeSlots()
        {
            var table = new StableHandleTable<int>();
            StableHandle first = table.Add(1);
            StableHandle second = table.Add(2);
            StableHandle third = table.Add(3);

            Assert.IsTrue(table.Remove(second));

            Assert.AreEqual(3, table.Capacity);
            Assert.AreEqual(2, table.ActiveCount);
            Assert.AreEqual(1, table.FreeCount);

            StableHandle reused = table.Add(4);
            Assert.AreEqual(second.Index, reused.Index);
            Assert.AreEqual(3, table.ActiveCount);
            Assert.AreEqual(0, table.FreeCount);

            Assert.IsTrue(table.Remove(first));
            Assert.IsTrue(table.Remove(third));

            StableHandleTableSnapshot snapshot = table.GetSnapshot();
            Assert.AreEqual(3, snapshot.Capacity);
            Assert.AreEqual(1, snapshot.ActiveCount);
            Assert.AreEqual(2, snapshot.FreeCount);
        }
    }
}
