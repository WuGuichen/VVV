using System;
using System.Runtime.CompilerServices;
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

        [TestCase(false)]
        [TestCase(true)]
        public void Get_WhenOnGetThrows_DiscardsFailedObjectAndPreservesException(bool prewarm)
        {
            var expected = new ApplicationException("onGet failed");
            DisposablePooledItem failedItem = null;
            bool failOnGet = true;
            bool rejectRelease = false;
            int releaseCalls = 0;
            ObjectPool<DisposablePooledItem> pool = null;
            pool = new ObjectPool<DisposablePooledItem>(
                () => new DisposablePooledItem(),
                item =>
                {
                    Assert.AreEqual(1, pool.CountActive);
                    Assert.AreEqual(0, pool.CountInactive);
                    if (failOnGet)
                    {
                        failedItem = item;
                        ThrowOnGet(expected);
                    }
                },
                item =>
                {
                    releaseCalls++;
                    if (rejectRelease)
                        throw new InvalidOperationException("Unexpected release of failed Get.");
                });
            if (prewarm)
                pool.Prewarm(1);
            int releasesBeforeFailure = releaseCalls;
            rejectRelease = true;

            ApplicationException actual = Assert.Throws<ApplicationException>(() => pool.Get());

            Assert.AreSame(expected, actual);
            StringAssert.Contains(nameof(ThrowOnGet), actual.StackTrace);
            Assert.AreEqual(0, pool.CountActive);
            Assert.AreEqual(0, pool.CountInactive);
            Assert.AreEqual(0, pool.CountAll);
            Assert.AreEqual(releasesBeforeFailure, releaseCalls);
            Assert.AreEqual(0, failedItem.DisposeCalls);
            Assert.Throws<InvalidOperationException>(() => pool.Release(failedItem));
            pool.Clear();
            Assert.AreEqual(0, pool.CountAll);

            failOnGet = false;
            rejectRelease = false;
            DisposablePooledItem recovered = pool.Get();
            Assert.AreNotSame(failedItem, recovered);
            pool.Release(recovered);
            Assert.AreEqual(releasesBeforeFailure + 1, releaseCalls);
            Assert.AreEqual(0, pool.CountActive);
            Assert.AreEqual(1, pool.CountInactive);
            Assert.AreSame(recovered, pool.Get());
            pool.Release(recovered);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Get_WhenOnGetRepeatedlyThrows_DoesNotAccumulateLeases(bool prewarm)
        {
            int releaseCalls = 0;
            var pool = new ObjectPool<PooledItem>(
                () => new PooledItem(),
                item => throw new ApplicationException("onGet failed"),
                item => releaseCalls++);
            if (prewarm)
                pool.Prewarm(3);

            for (int i = 0; i < 3; i++)
            {
                Assert.Throws<ApplicationException>(() => pool.Get());
                Assert.AreEqual(0, pool.CountActive);
                Assert.AreEqual(prewarm ? 2 - i : 0, pool.CountInactive);
                Assert.AreEqual(pool.CountInactive, pool.CountAll);
            }

            Assert.AreEqual(prewarm ? 3 : 0, releaseCalls);
            pool.Clear();
            Assert.AreEqual(0, pool.CountAll);
        }

        [Test]
        public void Get_WhenOnGetThrows_PreservesExistingActiveLease()
        {
            bool failOnGet = false;
            PooledItem failedItem = null;
            ObjectPool<PooledItem> pool = null;
            pool = new ObjectPool<PooledItem>(
                () => new PooledItem(),
                item =>
                {
                    if (failOnGet)
                    {
                        Assert.AreEqual(2, pool.CountActive);
                        failedItem = item;
                        throw new ApplicationException("onGet failed");
                    }
                });
            PooledItem active = pool.Get();
            failOnGet = true;

            Assert.Throws<ApplicationException>(() => pool.Get());

            Assert.AreEqual(1, pool.CountActive);
            Assert.AreEqual(0, pool.CountInactive);
            Assert.AreEqual(1, pool.CountAll);
            Assert.Throws<InvalidOperationException>(() => pool.Release(failedItem));
            pool.Clear();
            Assert.AreEqual(1, pool.CountActive);
            pool.Release(active);
            Assert.AreEqual(0, pool.CountActive);
            Assert.AreEqual(1, pool.CountInactive);
        }

        [Test]
        public void Get_WhenFactoryReturnsAlreadyActiveItem_PreservesExistingLease()
        {
            var shared = new PooledItem();
            int getCalls = 0;
            var pool = new ObjectPool<PooledItem>(() => shared, item => getCalls++);
            PooledItem active = pool.Get();

            Assert.Throws<InvalidOperationException>(() => pool.Get());

            Assert.AreEqual(1, getCalls);
            Assert.AreEqual(1, pool.CountActive);
            Assert.AreEqual(0, pool.CountInactive);
            pool.Release(active);
            Assert.AreEqual(0, pool.CountActive);
            Assert.AreEqual(1, pool.CountInactive);
        }

        [Test]
        public void Get_WhenFactoryThrows_PreservesExistingLease()
        {
            bool failFactory = false;
            var expected = new ApplicationException("factory failed");
            int getCalls = 0;
            var pool = new ObjectPool<PooledItem>(
                () =>
                {
                    if (failFactory)
                        throw expected;
                    return new PooledItem();
                },
                item => getCalls++);
            PooledItem active = pool.Get();
            failFactory = true;

            Assert.AreSame(expected, Assert.Throws<ApplicationException>(() => pool.Get()));

            Assert.AreEqual(1, getCalls);
            Assert.AreEqual(1, pool.CountActive);
            Assert.AreEqual(0, pool.CountInactive);
            Assert.AreEqual(1, pool.CountAll);
            pool.Release(active);
            Assert.AreEqual(0, pool.CountActive);
            Assert.AreEqual(1, pool.CountInactive);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Get_WhenOnGetReacquiresSameItemThenThrows_PreservesNestedLease(bool prewarm)
        {
            var expected = new ApplicationException("outer onGet failed");
            int getCalls = 0;
            int releaseCalls = 0;
            PooledItem outerItem = null;
            PooledItem delivered = null;
            ObjectPool<PooledItem> pool = null;
            pool = new ObjectPool<PooledItem>(
                () => new PooledItem(),
                item =>
                {
                    getCalls++;
                    if (getCalls != 1)
                        return;
                    outerItem = item;
                    pool.Release(item);
                    delivered = pool.Get();
                    ThrowOnGet(expected);
                },
                item => releaseCalls++);
            if (prewarm)
                pool.Prewarm(1);
            int initialReleaseCalls = releaseCalls;

            Assert.AreSame(expected, Assert.Throws<ApplicationException>(() => pool.Get()));

            Assert.AreSame(outerItem, delivered);
            Assert.AreEqual(2, getCalls);
            Assert.AreEqual(initialReleaseCalls + 1, releaseCalls);
            Assert.AreEqual(1, pool.CountActive);
            Assert.AreEqual(0, pool.CountInactive);
            Assert.AreEqual(1, pool.CountAll);
            pool.Clear();
            Assert.AreEqual(1, pool.CountActive);
            pool.Release(delivered);
            Assert.AreEqual(initialReleaseCalls + 2, releaseCalls);
            Assert.AreEqual(0, pool.CountActive);
            Assert.AreEqual(1, pool.CountInactive);
            Assert.AreSame(delivered, pool.Get());
            pool.Release(delivered);
        }

        [Test]
        public void Get_WhenOnGetDeliversDifferentItemThenThrows_PreservesNestedLease()
        {
            var expected = new ApplicationException("outer onGet failed");
            PooledItem failedItem = null;
            PooledItem delivered = null;
            bool isOuterCallback = true;
            ObjectPool<PooledItem> pool = null;
            pool = new ObjectPool<PooledItem>(
                () => new PooledItem(),
                item =>
                {
                    if (!isOuterCallback)
                        return;
                    isOuterCallback = false;
                    failedItem = item;
                    delivered = pool.Get();
                    ThrowOnGet(expected);
                });

            Assert.AreSame(expected, Assert.Throws<ApplicationException>(() => pool.Get()));

            Assert.AreNotSame(failedItem, delivered);
            Assert.AreEqual(1, pool.CountActive);
            Assert.AreEqual(0, pool.CountInactive);
            Assert.Throws<InvalidOperationException>(() => pool.Release(failedItem));
            pool.Release(delivered);
            Assert.AreEqual(0, pool.CountActive);
            Assert.AreEqual(1, pool.CountInactive);
        }

        [Test]
        public void Get_WhenOnGetReleasesItemThenThrows_PreservesExplicitRelease()
        {
            var expected = new ApplicationException("onGet failed after Release");
            bool failOnGet = true;
            int releaseCalls = 0;
            PooledItem released = null;
            ObjectPool<PooledItem> pool = null;
            pool = new ObjectPool<PooledItem>(
                () => new PooledItem(),
                item =>
                {
                    if (!failOnGet)
                        return;
                    released = item;
                    pool.Release(item);
                    ThrowOnGet(expected);
                },
                item => releaseCalls++);

            Assert.AreSame(expected, Assert.Throws<ApplicationException>(() => pool.Get()));

            Assert.AreEqual(1, releaseCalls);
            Assert.AreEqual(0, pool.CountActive);
            Assert.AreEqual(1, pool.CountInactive);
            Assert.AreEqual(1, pool.CountAll);
            failOnGet = false;
            Assert.AreSame(released, pool.Get());
            pool.Release(released);
        }

        [Test]
        public void Get_WhenNestedOnGetFailsAfterReacquiringItem_RemovesOnlyFailedLeases()
        {
            var outerException = new ApplicationException("outer onGet failed");
            var innerException = new ApplicationException("inner onGet failed");
            int getCalls = 0;
            int releaseCalls = 0;
            PooledItem outerItem = null;
            ObjectPool<PooledItem> pool = null;
            pool = new ObjectPool<PooledItem>(
                () => new PooledItem(),
                item =>
                {
                    getCalls++;
                    if (getCalls == 2)
                    {
                        Assert.AreSame(outerItem, item);
                        ThrowOnGet(innerException);
                    }
                    outerItem = item;
                    pool.Release(item);
                    Assert.AreSame(innerException, Assert.Throws<ApplicationException>(() => pool.Get()));
                    Assert.AreEqual(0, pool.CountAll);
                    ThrowOnGet(outerException);
                },
                item => releaseCalls++);

            Assert.AreSame(outerException, Assert.Throws<ApplicationException>(() => pool.Get()));

            Assert.AreEqual(2, getCalls);
            Assert.AreEqual(1, releaseCalls);
            Assert.AreEqual(0, pool.CountActive);
            Assert.AreEqual(0, pool.CountInactive);
            Assert.AreEqual(0, pool.CountAll);
            pool.Clear();
            Assert.AreEqual(0, pool.CountAll);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowOnGet(Exception exception)
        {
            throw exception;
        }

        private sealed class DisposablePooledItem : IDisposable
        {
            public int DisposeCalls { get; private set; }

            public void Dispose()
            {
                DisposeCalls++;
            }
        }

        private sealed class PooledItem
        {
        }
    }
}
