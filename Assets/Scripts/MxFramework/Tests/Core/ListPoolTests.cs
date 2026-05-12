using System;
using System.Collections.Generic;
using MxFramework.Core.Pooling;
using NUnit.Framework;

namespace MxFramework.Tests.Core.Pooling
{
    public class ListPoolTests
    {
        [Test]
        public void Get_ReturnsUsableList()
        {
            using (PooledList<int> pooled = ListPool<int>.Get())
            {
                pooled.List.Add(1);

                CollectionAssert.AreEqual(new[] { 1 }, pooled.List);
            }
        }

        [Test]
        public void Dispose_ClearsListBeforeReuse()
        {
            List<int> firstList;
            using (PooledList<int> pooled = ListPool<int>.Get(out firstList))
            {
                firstList.Add(1);
                firstList.Add(2);
            }

            using (PooledList<int> pooled = ListPool<int>.Get(out List<int> reused))
            {
                Assert.AreSame(firstList, reused);
                Assert.AreEqual(0, reused.Count);
            }
        }

        [Test]
        public void Dispose_IsIdempotent()
        {
            PooledList<int> pooled = ListPool<int>.Get();

            pooled.Dispose();
            pooled.Dispose();

            Assert.Throws<ObjectDisposedException>(() => _ = pooled.List);
        }
    }
}
