using System;
using MxFramework.Core.Pooling;
using NUnit.Framework;

namespace MxFramework.Tests.Core.Pooling
{
    public class ObjectPoolTests
    {
        [Test]
        public void Get_CreatesObjectAndUpdatesCounts()
        {
            var pool = new ObjectPool<PooledItem>(() => new PooledItem());

            PooledItem item = pool.Get();

            Assert.IsNotNull(item);
            Assert.AreEqual(1, pool.CountActive);
            Assert.AreEqual(0, pool.CountInactive);
            Assert.AreEqual(1, pool.CountAll);
        }

        [Test]
        public void Release_MovesObjectToInactive()
        {
            var pool = new ObjectPool<PooledItem>(() => new PooledItem());
            PooledItem item = pool.Get();

            pool.Release(item);

            Assert.AreEqual(0, pool.CountActive);
            Assert.AreEqual(1, pool.CountInactive);
            Assert.AreEqual(1, pool.CountAll);
            Assert.AreSame(item, pool.Get());
        }

        [Test]
        public void GetAndRelease_InvokeCallbacksInOrder()
        {
            string trace = string.Empty;
            ObjectPool<PooledItem> pool = null;
            pool = new ObjectPool<PooledItem>(
                () => new PooledItem(),
                item => trace += "get",
                item =>
                {
                    Assert.AreEqual(0, pool.CountInactive);
                    trace += "-release";
                });

            PooledItem item = pool.Get();
            pool.Release(item);

            Assert.AreEqual("get-release", trace);
        }

        [Test]
        public void Release_WhenInactiveAtMaxSize_DoesNotCacheExtraItems()
        {
            var pool = new ObjectPool<PooledItem>(() => new PooledItem(), maxSize: 1);
            PooledItem first = pool.Get();
            PooledItem second = pool.Get();

            pool.Release(first);
            pool.Release(second);

            Assert.AreEqual(0, pool.CountActive);
            Assert.AreEqual(1, pool.CountInactive);
            Assert.AreEqual(1, pool.CountAll);
        }

        [Test]
        public void Clear_RemovesInactiveOnly()
        {
            var pool = new ObjectPool<PooledItem>(() => new PooledItem());
            PooledItem first = pool.Get();
            PooledItem second = pool.Get();
            pool.Release(first);

            pool.Clear();

            Assert.AreEqual(1, pool.CountActive);
            Assert.AreEqual(0, pool.CountInactive);
            Assert.AreEqual(1, pool.CountAll);
            pool.Release(second);
        }

        [Test]
        public void Prewarm_AddsInactiveWithoutActiveItems()
        {
            var pool = new ObjectPool<PooledItem>(() => new PooledItem());

            pool.Prewarm(3);

            Assert.AreEqual(0, pool.CountActive);
            Assert.AreEqual(3, pool.CountInactive);
            Assert.AreEqual(3, pool.CountAll);
        }

        [Test]
        public void Constructor_ValidatesArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new ObjectPool<PooledItem>(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ObjectPool<PooledItem>(() => new PooledItem(), defaultCapacity: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ObjectPool<PooledItem>(() => new PooledItem(), maxSize: -1));
        }

        [Test]
        public void Get_WhenCreateReturnsNull_Throws()
        {
            var pool = new ObjectPool<PooledItem>(() => null);

            Assert.Throws<InvalidOperationException>(() => pool.Get());
        }

        [Test]
        public void Release_WhenNull_Throws()
        {
            var pool = new ObjectPool<PooledItem>(() => new PooledItem());

            Assert.Throws<ArgumentNullException>(() => pool.Release(null));
        }

        [Test]
        public void Release_WhenDuplicate_ThrowsAndDoesNotCorruptCounts()
        {
            var pool = new ObjectPool<PooledItem>(() => new PooledItem());
            PooledItem item = pool.Get();
            pool.Release(item);

            Assert.Throws<InvalidOperationException>(() => pool.Release(item));
            Assert.AreEqual(0, pool.CountActive);
            Assert.AreEqual(1, pool.CountInactive);
            Assert.AreEqual(1, pool.CountAll);
        }

        private sealed class PooledItem
        {
        }
    }
}
