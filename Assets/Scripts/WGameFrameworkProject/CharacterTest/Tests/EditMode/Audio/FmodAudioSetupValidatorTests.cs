using System.IO;
using FMODUnity;
using MxFramework.Audio.FMOD.Editor;
using NUnit.Framework;

namespace MxFramework.Tests.Audio
{
    public class FmodAudioSetupValidatorTests
    {
        private string _tempRoot;

        [SetUp]
        public void SetUp()
        {
            _tempRoot = Path.Combine("Temp", "MxFrameworkFmodValidatorTests");
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, true);
            }

            Directory.CreateDirectory(_tempRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, true);
            }
        }

        [Test]
        public void ValidateBankDirectory_WhenDirectoryMissing_ReturnsBankRootMissing()
        {
            FmodAudioSetupReport report = FmodAudioSetupValidator.ValidateBankDirectory(Path.Combine(_tempRoot, "missing"));

            Assert.IsTrue(report.HasErrors);
            AssertIssue(report, "FmodBankRootMissing");
        }

        [Test]
        public void ValidateBankDirectory_WhenMasterStringsMissing_ReturnsError()
        {
            File.WriteAllBytes(Path.Combine(_tempRoot, "SFX.bank"), new byte[] { 1 });

            FmodAudioSetupReport report = FmodAudioSetupValidator.ValidateBankDirectory(_tempRoot);

            Assert.IsTrue(report.HasErrors);
            AssertIssue(report, "FmodMasterBankMissing");
            AssertIssue(report, "FmodMasterStringsBankMissing");
        }

        [Test]
        public void ValidateBankDirectory_WithMasterStringsAndContentBank_ReturnsNoErrors()
        {
            File.WriteAllBytes(Path.Combine(_tempRoot, "Master.bank"), new byte[] { 1 });
            File.WriteAllBytes(Path.Combine(_tempRoot, "Master.strings.bank"), new byte[] { 1 });
            File.WriteAllBytes(Path.Combine(_tempRoot, "SFX.bank"), new byte[] { 1 });

            FmodAudioSetupReport report = FmodAudioSetupValidator.ValidateBankDirectory(
                _tempRoot,
                new[] { "Master" },
                new[] { "SFX" },
                null,
                BankLoadType.All);

            Assert.IsFalse(report.HasErrors, FmodAudioSetupValidator.CreateReportText(report));
        }

        [Test]
        public void ValidateBankDirectory_WhenSpecifiedBanksAreEmpty_ReturnsError()
        {
            File.WriteAllBytes(Path.Combine(_tempRoot, "Master.bank"), new byte[] { 1 });
            File.WriteAllBytes(Path.Combine(_tempRoot, "Master.strings.bank"), new byte[] { 1 });

            FmodAudioSetupReport report = FmodAudioSetupValidator.ValidateBankDirectory(
                _tempRoot,
                new[] { "Master" },
                null,
                new string[0],
                BankLoadType.Specified);

            Assert.IsTrue(report.HasErrors);
            AssertIssue(report, "FmodSpecifiedBanksEmpty");
        }

        private static void AssertIssue(FmodAudioSetupReport report, string code)
        {
            for (int i = 0; i < report.Issues.Count; i++)
            {
                if (report.Issues[i].Code == code)
                {
                    return;
                }
            }

            Assert.Fail("Expected FMOD setup issue: " + code + "\n" + FmodAudioSetupValidator.CreateReportText(report));
        }
    }
}
