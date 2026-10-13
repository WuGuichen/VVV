using System;
using System.IO;
using MxFramework.Platform;
using NUnit.Framework;

namespace MxFramework.Tests.Platform
{
    public sealed class PlatformEnvironmentAndLocationTests
    {
        [Test]
        public void PublicEnumValues_AreStableAndDefaultSafe()
        {
            Assert.AreEqual(0, (int)PlatformFamily.Unknown);
            Assert.AreEqual(1, (int)PlatformFamily.Windows);
            Assert.AreEqual(2, (int)PlatformFamily.MacOS);
            Assert.AreEqual(3, (int)PlatformFamily.Linux);
            Assert.AreEqual(4, (int)PlatformFamily.Android);
            Assert.AreEqual(5, (int)PlatformFamily.IOS);
            Assert.AreEqual(6, (int)PlatformFamily.Web);
            Assert.AreEqual(7, (int)PlatformFamily.Console);

            Assert.AreEqual(0, (int)PlatformLocationKind.Unknown);
            Assert.AreEqual(1, (int)PlatformLocationKind.FileSystemPath);
            Assert.AreEqual(2, (int)PlatformLocationKind.Uri);
            Assert.AreEqual(3, (int)PlatformLocationKind.ProviderOwned);

            Assert.AreEqual(0, (int)PlatformLocationId.Unknown);
            Assert.AreEqual(1, (int)PlatformLocationId.PersistentData);
            Assert.AreEqual(2, (int)PlatformLocationId.TemporaryCache);
            Assert.AreEqual(3, (int)PlatformLocationId.StreamingContent);
        }

        [Test]
        public void Environment_PreservesFactsAndNormalizesNullRuntimeName()
        {
            var environment = new PlatformEnvironment(
                PlatformFamily.Linux,
                isEditor: false,
                isHeadless: true,
                runtimeName: null);

            Assert.AreEqual(PlatformFamily.Linux, environment.Family);
            Assert.IsFalse(environment.IsEditor);
            Assert.IsTrue(environment.IsHeadless);
            Assert.AreEqual(string.Empty, environment.RuntimeName);
        }

        [Test]
        public void TryCreate_FileSystemPathPreservesAccessAndIntent()
        {
            string root = Path.Combine(Path.GetTempPath(), "mxframework-platform-location");

            PlatformLocationResult result = PlatformLocation.TryCreate(
                PlatformLocationKind.FileSystemPath,
                root,
                isReadable: true,
                isWritable: true,
                intendedForPersistentData: true);

            Assert.IsTrue(result.Success, result.Error.Message);
            Assert.AreEqual(PlatformLocationKind.FileSystemPath, result.Location.Kind);
            Assert.AreEqual(Path.GetFullPath(root), result.Location.Value);
            Assert.IsTrue(result.Location.IsReadable);
            Assert.IsTrue(result.Location.IsWritable);
            Assert.IsTrue(result.Location.IntendedForPersistentData);
            Assert.IsTrue(result.Error.IsNone);
        }

        [Test]
        public void TryCreate_UriAndProviderOwnedRemainDistinct()
        {
            PlatformLocationResult uri = PlatformLocation.TryCreate(
                PlatformLocationKind.Uri,
                "https://content.example.test/game/data.bin",
                isReadable: true,
                isWritable: false,
                intendedForPersistentData: false);
            PlatformLocationResult providerOwned = PlatformLocation.TryCreate(
                PlatformLocationKind.ProviderOwned,
                "provider://opaque-token/42",
                isReadable: true,
                isWritable: true,
                intendedForPersistentData: false);

            Assert.IsTrue(uri.Success, uri.Error.Message);
            Assert.AreEqual(PlatformLocationKind.Uri, uri.Location.Kind);
            Assert.AreEqual("https://content.example.test/game/data.bin", uri.Location.Value);
            Assert.IsTrue(uri.Location.IsReadable);
            Assert.IsFalse(uri.Location.IsWritable);

            Assert.IsTrue(providerOwned.Success, providerOwned.Error.Message);
            Assert.AreEqual(PlatformLocationKind.ProviderOwned, providerOwned.Location.Kind);
            Assert.AreEqual("provider://opaque-token/42", providerOwned.Location.Value);
            Assert.IsTrue(providerOwned.Location.IsReadable);
            Assert.IsTrue(providerOwned.Location.IsWritable);
        }

        [TestCase(PlatformLocationKind.Unknown, "C:\\data")]
        [TestCase(PlatformLocationKind.FileSystemPath, "relative/path")]
        [TestCase(PlatformLocationKind.FileSystemPath, "C:relative")]
        [TestCase(PlatformLocationKind.Uri, "relative/path")]
        [TestCase(PlatformLocationKind.ProviderOwned, "")]
        [TestCase(PlatformLocationKind.ProviderOwned, "   ")]
        public void TryCreate_InvalidOrDefaultLocationFailsStructurally(
            PlatformLocationKind kind,
            string value)
        {
            PlatformLocationResult result = PlatformLocation.TryCreate(
                kind,
                value,
                isReadable: true,
                isWritable: false,
                intendedForPersistentData: false);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(PlatformLocationKind.Unknown, result.Location.Kind);
            Assert.AreEqual(PlatformErrorCode.InvalidLocation, result.Error.Code);
        }

        [Test]
        public void SuccessfulResult_RejectsDefaultLocation()
        {
            PlatformLocationResult result = PlatformLocationResult.Ok(default);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(PlatformErrorCode.InvalidLocation, result.Error.Code);
        }
    }
}
