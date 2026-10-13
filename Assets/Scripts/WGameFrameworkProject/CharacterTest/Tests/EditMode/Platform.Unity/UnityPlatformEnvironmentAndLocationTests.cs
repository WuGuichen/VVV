using System.IO;
using MxFramework.Platform;
using MxFramework.Platform.Unity;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.PlatformUnity
{
    public sealed class UnityPlatformEnvironmentAndLocationTests
    {
        [TestCase(RuntimePlatform.WindowsEditor, PlatformFamily.Windows)]
        [TestCase(RuntimePlatform.WindowsPlayer, PlatformFamily.Windows)]
        [TestCase(RuntimePlatform.WindowsServer, PlatformFamily.Windows)]
        [TestCase(RuntimePlatform.OSXEditor, PlatformFamily.MacOS)]
        [TestCase(RuntimePlatform.OSXPlayer, PlatformFamily.MacOS)]
        [TestCase(RuntimePlatform.LinuxEditor, PlatformFamily.Linux)]
        [TestCase(RuntimePlatform.LinuxPlayer, PlatformFamily.Linux)]
        [TestCase(RuntimePlatform.Android, PlatformFamily.Android)]
        [TestCase(RuntimePlatform.IPhonePlayer, PlatformFamily.IOS)]
        [TestCase(RuntimePlatform.tvOS, PlatformFamily.IOS)]
        [TestCase(RuntimePlatform.VisionOS, PlatformFamily.IOS)]
        [TestCase(RuntimePlatform.WebGLPlayer, PlatformFamily.Web)]
        [TestCase(RuntimePlatform.PS5, PlatformFamily.Console)]
        [TestCase(RuntimePlatform.XboxOne, PlatformFamily.Console)]
        [TestCase(RuntimePlatform.Switch, PlatformFamily.Console)]
        public void MapFamily_MapsKnownRuntimePlatformsExplicitly(
            RuntimePlatform runtimePlatform,
            PlatformFamily expected)
        {
            Assert.That(UnityPlatformEnvironmentProvider.MapFamily(runtimePlatform), Is.EqualTo(expected));
        }

        [Test]
        public void MapFamily_UnknownRuntimePlatformFailsClosed()
        {
            Assert.That(
                UnityPlatformEnvironmentProvider.MapFamily((RuntimePlatform)int.MaxValue),
                Is.EqualTo(PlatformFamily.Unknown));
        }

        [TestCase(RuntimePlatform.QNXArm64)]
        [TestCase(RuntimePlatform.QNXX64)]
        public void MapFamily_UnclassifiedQnxRuntimeFailsClosed(RuntimePlatform runtimePlatform)
        {
            Assert.That(
                UnityPlatformEnvironmentProvider.MapFamily(runtimePlatform),
                Is.EqualTo(PlatformFamily.Unknown));
        }

        [Test]
        public void EnvironmentProvider_PreservesIndependentEditorAndHeadlessFacts()
        {
            var provider = new UnityPlatformEnvironmentProvider(
                RuntimePlatform.WindowsEditor,
                isEditor: true,
                isHeadless: true);

            Assert.That(provider.Current.Family, Is.EqualTo(PlatformFamily.Windows));
            Assert.That(provider.Current.IsEditor, Is.True);
            Assert.That(provider.Current.IsHeadless, Is.True);
            Assert.That(provider.Current.RuntimeName, Is.EqualTo(RuntimePlatform.WindowsEditor.ToString()));
        }

        [Test]
        public void Locations_CacheWritableFilesystemRoots()
        {
            string persistent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "mx-platform-persistent"));
            string temporary = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "mx-platform-cache"));
            string streaming = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "mx-platform-streaming"));
            var locations = new UnityPlatformLocations(persistent, temporary, streaming);

            PlatformLocationResult persistentResult = locations.Resolve(PlatformLocationId.PersistentData);
            PlatformLocationResult temporaryResult = locations.Resolve(PlatformLocationId.TemporaryCache);
            PlatformLocationResult streamingResult = locations.Resolve(PlatformLocationId.StreamingContent);

            Assert.That(persistentResult.Success, Is.True);
            Assert.That(persistentResult.Location.Kind, Is.EqualTo(PlatformLocationKind.FileSystemPath));
            Assert.That(persistentResult.Location.IsReadable, Is.True);
            Assert.That(persistentResult.Location.IsWritable, Is.True);
            Assert.That(persistentResult.Location.IntendedForPersistentData, Is.True);

            Assert.That(temporaryResult.Success, Is.True);
            Assert.That(temporaryResult.Location.IsWritable, Is.True);
            Assert.That(temporaryResult.Location.IntendedForPersistentData, Is.False);

            Assert.That(streamingResult.Success, Is.True);
            Assert.That(streamingResult.Location.Kind, Is.EqualTo(PlatformLocationKind.FileSystemPath));
            Assert.That(streamingResult.Location.IsReadable, Is.True);
            Assert.That(streamingResult.Location.IsWritable, Is.False);
        }

        [TestCase("jar:file:///data/app/game.apk!/assets")]
        [TestCase("https://content.example.test/game/")]
        public void Locations_PreserveAbsoluteStreamingUriSemantics(string streamingUri)
        {
            string root = Path.GetFullPath(Path.GetTempPath());
            var locations = new UnityPlatformLocations(root, root, streamingUri);

            PlatformLocationResult result = locations.Resolve(PlatformLocationId.StreamingContent);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Location.Kind, Is.EqualTo(PlatformLocationKind.Uri));
            Assert.That(result.Location.IsReadable, Is.True);
            Assert.That(result.Location.IsWritable, Is.False);
        }

        [Test]
        public void Locations_InvalidValuesAndUnknownIdReturnStructuredFailures()
        {
            var locations = new UnityPlatformLocations("relative", "", "also-relative");

            Assert.That(locations.Resolve(PlatformLocationId.PersistentData).Error.Code,
                Is.EqualTo(PlatformErrorCode.InvalidLocation));
            Assert.That(locations.Resolve(PlatformLocationId.TemporaryCache).Error.Code,
                Is.EqualTo(PlatformErrorCode.InvalidLocation));
            Assert.That(locations.Resolve(PlatformLocationId.StreamingContent).Error.Code,
                Is.EqualTo(PlatformErrorCode.InvalidLocation));
            Assert.That(locations.Resolve(PlatformLocationId.Unknown).Error.Code,
                Is.EqualTo(PlatformErrorCode.InvalidArgument));
        }
    }
}
