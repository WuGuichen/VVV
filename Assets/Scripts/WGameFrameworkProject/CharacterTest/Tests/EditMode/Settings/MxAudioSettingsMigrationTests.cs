using MxFramework.Settings;
using NUnit.Framework;

namespace MxFramework.Tests.Settings
{
    public sealed class MxAudioSettingsMigrationTests
    {
        [Test]
        public void Migrate_PreservesKnownLegacyMaster_AddsDefaults_AndRetiresUnknown()
        {
            var source = new MxAudioSettings(new[]
            {
                new MxAudioBusSetting(500001, 0.4f, true),
                new MxAudioBusSetting(999999, 0.2f, false)
            });
            MxAudioSettings migrated = MxAudioSettingsMigration.Migrate(source, new[]
            {
                new MxAudioBusSetting(500001, 1f, false),
                new MxAudioBusSetting(500002, 0.8f, false)
            });

            Assert.AreEqual(2, migrated.BusSettings.Count);
            Assert.AreEqual(0.4f, migrated.BusSettings[0].Volume);
            Assert.IsTrue(migrated.BusSettings[0].Muted);
            Assert.AreEqual(500002, migrated.BusSettings[1].BusId);
            Assert.AreEqual(0.8f, migrated.BusSettings[1].Volume);
        }
    }
}
