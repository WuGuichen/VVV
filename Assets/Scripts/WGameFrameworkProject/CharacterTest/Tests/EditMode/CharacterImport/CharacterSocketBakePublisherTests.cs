using System;
using System.IO;
using MxFramework.Animation;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Editor.CharacterImport;
using NUnit.Framework;
using UnityEditor;

namespace MxFramework.Tests.CharacterImport
{
    public sealed class CharacterSocketBakePublisherTests
    {
        private string _root;
        private CharacterImportedPackage _package;
        private string Index => _root + "/" + CharacterSocketBakeCatalogBinary.DefaultIndexRelativePath;
        private string Binary => _root + "/" + CharacterSocketBakeCatalogBinary.DefaultBinaryRelativePath;

        [SetUp]
        public void SetUp()
        {
            string id = "__socket_publish_" + Guid.NewGuid().ToString("N");
            _root = "Assets/" + id;
            AssetDatabase.CreateFolder("Assets", id);
            AssetDatabase.CreateFolder(_root, "generated");
            _package = new CharacterImportedPackage(Path.GetFullPath(_root), id, null, null, null, null, null);
        }

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(_root);

        [Test]
        public void PublishImportsBothAssetsAndKeepsUnchangedBytesMtimeAndMeta()
        {
            var catalog = Catalog(10);
            CharacterImportedPackagePrefabBuilder.WriteSocketBakeCatalogForTests(_package, catalog);
            Assert.IsNotEmpty(AssetDatabase.AssetPathToGUID(Index));
            Assert.IsNotEmpty(AssetDatabase.AssetPathToGUID(Binary));
            byte[] indexMeta = File.ReadAllBytes(Index + ".meta");
            byte[] binaryMeta = File.ReadAllBytes(Binary + ".meta");
            DateTime indexTime = File.GetLastWriteTimeUtc(Index);
            DateTime binaryTime = File.GetLastWriteTimeUtc(Binary);
            CharacterImportedPackagePrefabBuilder.WriteSocketBakeCatalogForTests(_package, catalog,
                _ => Assert.Fail("An unchanged pair must not enter publication."));
            Assert.AreEqual(indexTime, File.GetLastWriteTimeUtc(Index));
            Assert.AreEqual(binaryTime, File.GetLastWriteTimeUtc(Binary));
            CollectionAssert.AreEqual(indexMeta, File.ReadAllBytes(Index + ".meta"));
            CollectionAssert.AreEqual(binaryMeta, File.ReadAllBytes(Binary + ".meta"));
        }

        [TestCase("BeforeBinary")]
        [TestCase("AfterBinary")]
        [TestCase("AfterIndex")]
        public void PublishFailureRestoresExistingPairAndPreservesMeta(string stage)
        {
            CharacterImportedPackagePrefabBuilder.WriteSocketBakeCatalogForTests(_package, Catalog(10));
            byte[] index = File.ReadAllBytes(Index);
            byte[] binary = File.ReadAllBytes(Binary);
            byte[] indexMeta = File.ReadAllBytes(Index + ".meta");
            byte[] binaryMeta = File.ReadAllBytes(Binary + ".meta");
            Assert.Throws<IOException>(() => CharacterImportedPackagePrefabBuilder.WriteSocketBakeCatalogForTests(
                _package, Catalog(20), current => { if (current == stage) throw new IOException("injected " + stage); }));
            CollectionAssert.AreEqual(index, File.ReadAllBytes(Index));
            CollectionAssert.AreEqual(binary, File.ReadAllBytes(Binary));
            CollectionAssert.AreEqual(indexMeta, File.ReadAllBytes(Index + ".meta"));
            CollectionAssert.AreEqual(binaryMeta, File.ReadAllBytes(Binary + ".meta"));
            Assert.IsTrue(CharacterSocketBakeCatalogLoader.TryLoadFromDirectory(_root, _package.PackageId,
                out _, out _, out string[] issues), string.Join(" | ", issues));
            CharacterImportedPackagePrefabBuilder.WriteSocketBakeCatalogForTests(_package, Catalog(20));
            CollectionAssert.AreEqual(binaryMeta, File.ReadAllBytes(Binary + ".meta"));
        }

        [Test]
        public void NewPairFailureLeavesNoPartialPayload()
        {
            Assert.Throws<IOException>(() => CharacterImportedPackagePrefabBuilder.WriteSocketBakeCatalogForTests(
                _package, Catalog(10), stage => { if (stage == "AfterBinary") throw new IOException("injected"); }));
            Assert.IsFalse(File.Exists(Index));
            Assert.IsFalse(File.Exists(Binary));
            Assert.IsFalse(File.Exists(Index + ".meta"));
            Assert.IsFalse(File.Exists(Binary + ".meta"));
        }

        [Test]
        public void SamePackageReentryIsRejectedBeforeOverwritingRecoveryFiles()
        {
            CharacterImportedPackagePrefabBuilder.WriteSocketBakeCatalogForTests(_package, Catalog(10));
            bool checkedReentry = false;
            CharacterImportedPackagePrefabBuilder.WriteSocketBakeCatalogForTests(_package, Catalog(20), stage =>
            {
                if (stage != "AfterBinary") return;
                checkedReentry = true;
                Assert.Throws<IOException>(() =>
                    CharacterImportedPackagePrefabBuilder.WriteSocketBakeCatalogForTests(_package, Catalog(30)));
            });
            Assert.IsTrue(checkedReentry);
            Assert.IsTrue(CharacterSocketBakeCatalogLoader.TryLoadFromDirectory(_root, _package.PackageId,
                out CharacterSocketBakeCatalog catalog, out _, out string[] issues), string.Join(" | ", issues));
            Assert.IsTrue(catalog.TryGetFrame("clip.test", 1, 0, out MxAnimationBakedSocketFrame frame));
            Assert.AreEqual(20, frame.Position.X);
        }

        private static CharacterSocketBakeCatalog Catalog(long position)
        {
            var catalog = new CharacterSocketBakeCatalog();
            catalog.RegisterTrack("clip.test", 1, 1000, new[]
            {
                new MxAnimationBakedSocketFrame(0, 1, "root/socket",
                    new MxAnimationBakedVector3(position, 0, 0), MxAnimationBakedVector3.Zero)
            });
            return catalog;
        }
    }
}
