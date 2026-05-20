using System;
using MxFramework.Audio.FMOD.Editor;
using NUnit.Framework;

namespace MxFramework.Tests.Audio
{
    public class FmodAudioLibraryExporterTests
    {
        [Test]
        public void CreateSnapshot_WhenSourceUnavailable_ReturnsStructuredDiagnostic()
        {
            FmodAudioLibrarySnapshot snapshot = FmodAudioLibraryExporter.CreateSnapshot(
                FmodAudioLibrarySourceData.Unavailable("FMOD package missing."),
                new DateTime(2026, 5, 20, 8, 0, 0, DateTimeKind.Utc));

            Assert.AreEqual(0, snapshot.events.Count);
            AssertDiagnostic(snapshot, "Error", "FMOD_UNAVAILABLE");
            Assert.That(FmodAudioLibraryExporter.ToJson(snapshot), Does.Contain("FMOD_UNAVAILABLE"));
        }

        [Test]
        public void CreateSnapshot_WithEventBankAndParameter_ExportsAuthoringFields()
        {
            FmodAudioLibrarySourceData source = CreateValidSource();

            FmodAudioLibrarySnapshot snapshot = FmodAudioLibraryExporter.CreateSnapshot(
                source,
                new DateTime(2026, 5, 20, 8, 0, 0, DateTimeKind.Utc));

            Assert.AreEqual(1, snapshot.events.Count);
            Assert.AreEqual("event:/Character/IronVanguard/SwordSlash", snapshot.events[0].path);
            Assert.AreEqual("{11111111-2222-3333-4444-555555555555}", snapshot.events[0].guid);
            Assert.AreEqual("Character", snapshot.events[0].banks[0]);
            Assert.AreEqual("Impact", snapshot.events[0].parameters[0].name);
            Assert.AreEqual(17u, snapshot.events[0].parameters[0].idData1);
            Assert.AreEqual(23u, snapshot.events[0].parameters[0].idData2);
            Assert.AreEqual("Heavy", snapshot.events[0].parameters[0].labels[1]);
            Assert.AreEqual(0, snapshot.diagnostics.Count);
        }

        [Test]
        public void CreateSnapshot_WhenBankNewerThanCache_AddsStaleDiagnostic()
        {
            FmodAudioLibrarySourceData source = CreateValidSource();
            source.CacheTimeUtc = new DateTime(2026, 5, 20, 7, 0, 0, DateTimeKind.Utc);
            source.Banks[0].LastModifiedUtc = new DateTime(2026, 5, 20, 8, 0, 0, DateTimeKind.Utc);

            FmodAudioLibrarySnapshot snapshot = FmodAudioLibraryExporter.CreateSnapshot(
                source,
                new DateTime(2026, 5, 20, 8, 30, 0, DateTimeKind.Utc));

            Assert.IsTrue(snapshot.cacheStale);
            AssertDiagnostic(snapshot, "Warning", "FMOD_CACHE_STALE");
        }

        [Test]
        public void CreateSnapshot_WhenEventHasNoBank_AddsBankMissingDiagnostic()
        {
            FmodAudioLibrarySourceData source = CreateValidSource();
            source.Events[0].Banks.Clear();

            FmodAudioLibrarySnapshot snapshot = FmodAudioLibraryExporter.CreateSnapshot(
                source,
                new DateTime(2026, 5, 20, 8, 0, 0, DateTimeKind.Utc));

            AssertDiagnostic(snapshot, "Error", "RES_LIBRARY_FMOD_BANK_MISSING");
        }

        private static FmodAudioLibrarySourceData CreateValidSource()
        {
            var source = new FmodAudioLibrarySourceData
            {
                IsAvailable = true,
                IsCacheValid = true,
                Source = "Test",
                CacheTimeUtc = new DateTime(2026, 5, 20, 8, 0, 0, DateTimeKind.Utc)
            };

            source.Banks.Add(new FmodAudioLibrarySourceBank
            {
                Name = "Character",
                Path = "/Banks/Character.bank",
                StudioPath = "bank:/Character",
                LastModifiedUtc = new DateTime(2026, 5, 20, 7, 55, 0, DateTimeKind.Utc)
            });
            source.Banks[0].FileSizes.Add(new FmodAudioLibrarySourceBankFileSize
            {
                Platform = "Desktop",
                SizeBytes = 1024
            });

            var audioEvent = new FmodAudioLibrarySourceEvent
            {
                Path = "event:/Character/IronVanguard/SwordSlash",
                Guid = "{11111111-2222-3333-4444-555555555555}",
                Kind = "Event",
                Is3D = true,
                IsLoop = false,
                MinDistance = 1f,
                MaxDistance = 25f,
                LengthMs = 850
            };
            audioEvent.Banks.Add("Character");
            audioEvent.Parameters.Add(new FmodAudioLibrarySourceParameter
            {
                Name = "Impact",
                StudioPath = "parameter:/Impact",
                IdData1 = 17,
                IdData2 = 23,
                Kind = "Labeled",
                DefaultValue = 0f,
                MinValue = 0f,
                MaxValue = 2f,
                IsGlobal = false
            });
            audioEvent.Parameters[0].Labels.Add("Light");
            audioEvent.Parameters[0].Labels.Add("Heavy");
            source.Events.Add(audioEvent);

            return source;
        }

        private static void AssertDiagnostic(FmodAudioLibrarySnapshot snapshot, string severity, string code)
        {
            for (int i = 0; i < snapshot.diagnostics.Count; i++)
            {
                FmodAudioLibraryDiagnostic diagnostic = snapshot.diagnostics[i];
                if (diagnostic.severity == severity && diagnostic.code == code)
                {
                    return;
                }
            }

            Assert.Fail("Expected diagnostic " + severity + " " + code + ".");
        }
    }
}
