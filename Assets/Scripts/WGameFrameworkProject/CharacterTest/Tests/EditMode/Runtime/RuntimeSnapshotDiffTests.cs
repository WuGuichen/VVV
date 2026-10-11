using System;
using System.Collections.Generic;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Runtime
{
    public class RuntimeSnapshotDiffTests
    {
        [Test]
        public void Compare_ReturnsAddedRemovedAndModifiedChanges()
        {
            RuntimeChangeSet changeSet = RuntimeSnapshotDiff.Compare(
                Values(
                    Entry("removed", "old"),
                    Entry("same", "same-value"),
                    Entry("modified", "before")),
                Values(
                    Entry("added", "new"),
                    Entry("same", "same-value"),
                    Entry("modified", "after")));

            Assert.IsTrue(changeSet.HasChanges);
            Assert.AreEqual(3, changeSet.Count);
            AssertChange(changeSet.Changes[0], RuntimeChangeKind.Added, "added", string.Empty, "new");
            AssertChange(changeSet.Changes[1], RuntimeChangeKind.Modified, "modified", "before", "after");
            AssertChange(changeSet.Changes[2], RuntimeChangeKind.Removed, "removed", "old", string.Empty);
        }

        [Test]
        public void Compare_OrdersChangesByKey()
        {
            RuntimeChangeSet changeSet = RuntimeSnapshotDiff.Compare(
                Values(Entry("z", "1"), Entry("b", "1")),
                Values(Entry("a", "1"), Entry("b", "2")));

            CollectionAssert.AreEqual(new[] { "a", "b", "z" }, Keys(changeSet.Changes));
            CollectionAssert.AreEqual(
                new[] { RuntimeChangeKind.Added, RuntimeChangeKind.Modified, RuntimeChangeKind.Removed },
                Kinds(changeSet.Changes));
        }

        [Test]
        public void Compare_IgnoresUnchangedValues()
        {
            RuntimeChangeSet changeSet = RuntimeSnapshotDiff.Compare(
                Values(Entry("a", "1"), Entry("b", "2")),
                Values(Entry("b", "2"), Entry("a", "1")));

            Assert.IsFalse(changeSet.HasChanges);
            Assert.AreEqual(0, changeSet.Count);
            CollectionAssert.IsEmpty(changeSet.Changes);
        }

        [Test]
        public void Compare_EmptySnapshotsReturnEmptyChangeSet()
        {
            RuntimeChangeSet changeSet = RuntimeSnapshotDiff.Compare(
                Array.Empty<RuntimeSnapshotValue>(),
                Array.Empty<RuntimeSnapshotValue>());

            Assert.IsFalse(changeSet.HasChanges);
            Assert.AreEqual(0, changeSet.Count);
            CollectionAssert.IsEmpty(changeSet.Changes);
        }

        [Test]
        public void Compare_RequiresInputs()
        {
            Assert.Throws<ArgumentNullException>(() => RuntimeSnapshotDiff.Compare(null, Array.Empty<RuntimeSnapshotValue>()));
            Assert.Throws<ArgumentNullException>(() => RuntimeSnapshotDiff.Compare(Array.Empty<RuntimeSnapshotValue>(), null));
        }

        [Test]
        public void Compare_RejectsDuplicateKeys()
        {
            Assert.Throws<ArgumentException>(() => RuntimeSnapshotDiff.Compare(
                Values(Entry("a", "1"), Entry("a", "2")),
                Array.Empty<RuntimeSnapshotValue>()));

            Assert.Throws<ArgumentException>(() => RuntimeSnapshotDiff.Compare(
                Array.Empty<RuntimeSnapshotValue>(),
                Values(Entry("a", "1"), Entry("a", "2"))));
        }

        [Test]
        public void Compare_RejectsEmptyKeys()
        {
            Assert.Throws<ArgumentException>(() => RuntimeSnapshotDiff.Compare(
                new[] { default(RuntimeSnapshotValue) },
                Array.Empty<RuntimeSnapshotValue>()));
        }

        private static RuntimeSnapshotValue Entry(string key, string value)
        {
            return new RuntimeSnapshotValue(key, value);
        }

        private static RuntimeSnapshotValue[] Values(params RuntimeSnapshotValue[] values)
        {
            return values;
        }

        private static void AssertChange(
            RuntimeChange change,
            RuntimeChangeKind kind,
            string key,
            string beforeValue,
            string afterValue)
        {
            Assert.AreEqual(kind, change.Kind);
            Assert.AreEqual(key, change.Key);
            Assert.AreEqual(beforeValue, change.BeforeValue);
            Assert.AreEqual(afterValue, change.AfterValue);
        }

        private static string[] Keys(IReadOnlyList<RuntimeChange> changes)
        {
            var keys = new string[changes.Count];
            for (int i = 0; i < changes.Count; i++)
            {
                keys[i] = changes[i].Key;
            }

            return keys;
        }

        private static RuntimeChangeKind[] Kinds(IReadOnlyList<RuntimeChange> changes)
        {
            var kinds = new RuntimeChangeKind[changes.Count];
            for (int i = 0; i < changes.Count; i++)
            {
                kinds[i] = changes[i].Kind;
            }

            return kinds;
        }
    }
}
