using System;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Runtime
{
    public class RuntimeContextMapTests
    {
        [Test]
        public void Set_ThenTryGet_ReturnsTypedValue()
        {
            var map = new RuntimeContextMap();
            var key = new ContextKey<int>("score");

            map.Set(key, 42);

            int value;
            Assert.IsTrue(map.TryGet(key, out value));
            Assert.AreEqual(42, value);
            Assert.IsTrue(map.Contains(key));
            Assert.AreEqual(1, map.Count);
        }

        [Test]
        public void TryGet_MissingKey_ReturnsFalseAndDefaultValue()
        {
            var map = new RuntimeContextMap();
            var key = new ContextKey<string>("missing");

            string value;
            Assert.IsFalse(map.TryGet(key, out value));
            Assert.IsNull(value);
            Assert.IsFalse(map.Contains(key));
        }

        [Test]
        public void Remove_RemovesOnlyMatchingTypedKey()
        {
            var map = new RuntimeContextMap();
            var intKey = new ContextKey<int>("shared");
            var stringKey = new ContextKey<string>("shared");
            map.Set(intKey, 7);
            map.Set(stringKey, "seven");

            Assert.IsTrue(map.Remove(intKey));
            Assert.IsFalse(map.Contains(intKey));

            string value;
            Assert.IsTrue(map.TryGet(stringKey, out value));
            Assert.AreEqual("seven", value);
            Assert.AreEqual(1, map.Count);
        }

        [Test]
        public void Clear_RemovesAllValues()
        {
            var map = new RuntimeContextMap();
            map.Set(new ContextKey<int>("score"), 42);
            map.Set(new ContextKey<string>("name"), "test");

            map.Clear();

            Assert.AreEqual(0, map.Count);
            Assert.AreEqual(0, map.CreateSnapshot().Entries.Count);
        }

        [Test]
        public void SameIdWithDifferentValueTypes_DoesNotCollide()
        {
            var map = new RuntimeContextMap();
            var intKey = new ContextKey<int>("shared");
            var stringKey = new ContextKey<string>("shared");

            map.Set(intKey, 10);
            map.Set(stringKey, "ten");

            int intValue;
            string stringValue;
            Assert.IsTrue(map.TryGet(intKey, out intValue));
            Assert.IsTrue(map.TryGet(stringKey, out stringValue));
            Assert.AreEqual(10, intValue);
            Assert.AreEqual("ten", stringValue);
            Assert.AreEqual(2, map.Count);
        }

        [Test]
        public void ContextKey_InvalidId_Throws()
        {
            Assert.Throws<ArgumentException>(() => new ContextKey<int>(null));
            Assert.Throws<ArgumentException>(() => new ContextKey<int>(string.Empty));
            Assert.Throws<ArgumentException>(() => new ContextKey<int>("   "));
        }

        [Test]
        public void DefaultContextKey_ThrowsWhenUsed()
        {
            var map = new RuntimeContextMap();
            var key = default(ContextKey<int>);

            Assert.Throws<ArgumentException>(() => map.Set(key, 1));
            int value;
            Assert.Throws<ArgumentException>(() => map.TryGet(key, out value));
            Assert.Throws<ArgumentException>(() => map.Contains(key));
            Assert.Throws<ArgumentException>(() => map.Remove(key));
        }

        [Test]
        public void ContextKey_EqualityIncludesGenericValueType()
        {
            var left = new ContextKey<int>("shared");
            var right = new ContextKey<int>("shared");
            var differentId = new ContextKey<int>("other");
            var stringKey = new ContextKey<string>("shared");

            Assert.AreEqual(left, right);
            Assert.IsTrue(left == right);
            Assert.IsTrue(left != differentId);
            Assert.AreEqual(typeof(int), left.ValueType);
            Assert.AreNotEqual(left, (object)stringKey);
        }

        [Test]
        public void CreateSnapshot_ReturnsStableEntriesSortedByIdThenType()
        {
            var map = new RuntimeContextMap();
            map.Set(new ContextKey<string>("beta"), "value");
            map.Set(new ContextKey<int>("alpha"), 1);
            map.Set(new ContextKey<string>("alpha"), "one");

            RuntimeContextMapSnapshot snapshot = map.CreateSnapshot();
            map.Set(new ContextKey<int>("gamma"), 3);

            Assert.AreEqual(3, snapshot.Entries.Count);
            Assert.AreEqual("alpha", snapshot.Entries[0].Id);
            Assert.AreEqual("Int32", snapshot.Entries[0].ValueTypeName);
            Assert.AreEqual(typeof(int).FullName, snapshot.Entries[0].ValueTypeFullName);
            Assert.AreEqual("alpha (Int32)", snapshot.Entries[0].Summary);

            Assert.AreEqual("alpha", snapshot.Entries[1].Id);
            Assert.AreEqual("String", snapshot.Entries[1].ValueTypeName);
            Assert.AreEqual("beta", snapshot.Entries[2].Id);
            Assert.AreEqual("String", snapshot.Entries[2].ValueTypeName);
        }
    }
}
