using System;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Runtime
{
    public sealed class RuntimeDefinitionIdCatalogTests
    {
        private const int Domain = 91001;
        private const int FirstId = 1, SecondId = 2;

        [Test]
        public void CatalogKeepsAuthoredIdentitiesAcrossLoadingOrder()
        {
            var a = new RuntimeDefinitionIdEntry(new RuntimeDefinitionId(Domain, FirstId));
            var b = new RuntimeDefinitionIdEntry(new RuntimeDefinitionId(Domain, SecondId), retired: true);
            var first = new RuntimeDefinitionIdCatalog(1, default, new[] { a, b });
            var second = new RuntimeDefinitionIdCatalog(1, default, new[] { b, a });
            Assert.AreEqual(first.GetEntry(0).Id, second.GetEntry(0).Id);
            Assert.AreEqual(first.GetEntry(1).Id, second.GetEntry(1).Id);
            Assert.IsTrue(first.IsActive(a.Id)); Assert.IsTrue(first.IsRetired(b.Id));
            Assert.Throws<ArgumentException>(() => first.ValidateReference(b.Id));
            Assert.Throws<ArgumentException>(() => first.ValidateReference(new RuntimeDefinitionId(Domain, 3)));
            Assert.Throws<ArgumentException>(() => first.ValidateReference(RuntimeDefinitionId.None));
            Assert.DoesNotThrow(() => first.ValidateReference(RuntimeDefinitionId.None, optional: true));
        }

        [Test]
        public void CatalogRejectsDuplicateIdentitiesAndOwnsItsInput()
        {
            var entry = new RuntimeDefinitionIdEntry(new RuntimeDefinitionId(Domain, FirstId));
            Assert.Throws<ArgumentException>(() => new RuntimeDefinitionIdCatalog(1, default, new[] { entry, entry }));
            var source = new[] { entry };
            var catalog = new RuntimeDefinitionIdCatalog(1, default, source);
            source[0] = new RuntimeDefinitionIdEntry(new RuntimeDefinitionId(Domain, SecondId));
            Assert.AreEqual(entry.Id, catalog.GetEntry(0).Id);
            Assert.IsFalse(catalog.IsActive(source[0].Id));
        }

        [Test]
        public void EqualNumbersInDifferentDomainsRemainDifferentIdentities()
        {
            Assert.AreNotEqual(new RuntimeDefinitionId(Domain, FirstId), new RuntimeDefinitionId(Domain + 1, FirstId));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RuntimeDefinitionId(0, FirstId));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RuntimeDefinitionId(Domain, 0));
        }
    }
}
