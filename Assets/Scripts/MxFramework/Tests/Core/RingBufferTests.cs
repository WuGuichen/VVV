using System;
using System.Collections.Generic;
using NUnit.Framework;
using MxFramework.Core.Collections;

namespace MxFramework.Tests.Core.Collections
{
    public class RingBufferTests
    {
        [Test]
        public void Add_CopyTo_PreservesOldestToNewestOrder()
        {
            var buffer = new RingBuffer<int>(3);
            buffer.Add(1);
            buffer.Add(2);
            buffer.Add(3);

            var output = new List<int>();
            buffer.CopyTo(output);

            Assert.AreEqual(3, buffer.Capacity);
            Assert.AreEqual(3, buffer.Count);
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, output);
        }

        [Test]
        public void Add_WhenFull_OverwritesOldest()
        {
            var buffer = new RingBuffer<int>(3);
            buffer.Add(1);
            buffer.Add(2);
            buffer.Add(3);
            buffer.Add(4);
            buffer.Add(5);

            var output = new List<int>();
            buffer.CopyTo(output);

            Assert.AreEqual(3, buffer.Count);
            CollectionAssert.AreEqual(new[] { 3, 4, 5 }, output);
        }

        [Test]
        public void Clear_ResetsCountAndKeepsCapacity()
        {
            var buffer = new RingBuffer<int>(2);
            buffer.Add(1);
            buffer.Add(2);

            buffer.Clear();

            Assert.AreEqual(0, buffer.Count);
            Assert.AreEqual(2, buffer.Capacity);

            var output = new List<int>();
            buffer.CopyTo(output);
            CollectionAssert.IsEmpty(output);
        }

        [Test]
        public void CopyTo_AppendsToExistingOutput()
        {
            var buffer = new RingBuffer<int>(2);
            buffer.Add(10);
            buffer.Add(20);

            var output = new List<int> { 1, 2 };
            buffer.CopyTo(output);

            CollectionAssert.AreEqual(new[] { 1, 2, 10, 20 }, output);
        }

        [Test]
        public void Constructor_WhenCapacityIsInvalid_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RingBuffer<int>(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RingBuffer<int>(-1));
        }

        [Test]
        public void CopyTo_WhenOutputIsNull_Throws()
        {
            var buffer = new RingBuffer<int>(1);
            Assert.Throws<ArgumentNullException>(() => buffer.CopyTo(null));
        }
    }
}
