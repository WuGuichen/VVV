using System.Collections;
using System.IO;
using MxFramework.Demo;
using MxFramework.Audio;
using MxFramework.Audio.FMOD;
using MxFramework.Audio.Config;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MxFramework.Tests.PlayMode.Audio
{
    public sealed class FmodAudioValidationPlayModeTests
    {
        [UnityTest]
        [Timeout(15000)]
        public IEnumerator RealBanks_AutomatedSmoke_PassesAndWritesReport()
        {
            var runnerObject = new GameObject("Audio Validation Test Runner");
            runnerObject.SetActive(false);
            FmodAudioDemoRunner runner = runnerObject.AddComponent<FmodAudioDemoRunner>();
            runner.ConfigureValidationDefaults();

            runnerObject.SetActive(true);

            float deadline = Time.realtimeSinceStartup + 10f;
            while (runner.SmokeStatus != FmodAudioSmokeStatus.Passed
                   && runner.SmokeStatus != FmodAudioSmokeStatus.Failed
                   && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.AreEqual(FmodAudioSmokeStatus.Passed, runner.SmokeStatus, runner.LastSmokeReport);
            Assert.IsTrue(File.Exists(runner.LastSmokeReportPath), "Audio smoke report was not written.");
            StringAssert.Contains("recentErrorCount: 0", runner.LastSmokeReport);

            Object.Destroy(runnerObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RealBanks_VoiceBudget_RejectsAndStealsByPriority()
        {
            var catalog = new AudioDefinitionCatalog(
                new[] { new AudioBusDefinition(500001, "Master", "bus:/") },
                new[]
                {
                    new AudioEventDefinition(500101, "two-dimensional", "event:/MxFramework/Demo/OneShot", "{532d60e2-a86b-468f-a2f8-5d69efcf7207}", AudioEventKind.Event, 500001, false, false, 0f),
                    new AudioEventDefinition(500103, "three-dimensional", "event:/MxFramework/Demo/OneShot3D", "{5b50605b-36ec-4999-96c8-ec6cc5fcaa28}", AudioEventKind.Event, 500001, true, false, 30f)
                },
                null);
            using (var service = new AudioService(catalog, new FmodAudioBackend(new FmodAudioBackendOptions
            {
                BankManifest = LoadBankManifest(),
                PreloadBusIds = new[] { 500001 },
                GlobalVoiceLimit = 1,
                BusVoiceLimit = 8,
                EventVoiceLimit = 8,
                EmitterVoiceLimit = 8
            })))
            {
                Assert.IsTrue(service.PlayOneShot(AudioPlayRequest.Create2D(500101, priority: 1)).Success);
                AudioPlayResult rejected = service.PlayOneShot(AudioPlayRequest.Create3D(500103, AudioTransform.Origin, emitterId: 7, priority: 0));
                AudioPlayResult stolen = service.PlayOneShot(AudioPlayRequest.Create3D(500103, AudioTransform.Origin, emitterId: 7, priority: 10));

                Assert.AreEqual(AudioErrorCode.RequestRejected, rejected.ErrorCode);
                Assert.IsTrue(stolen.Success, stolen.Message);
                AudioDebugSnapshot snapshot = service.CaptureSnapshot();
                Assert.AreEqual(1, snapshot.TotalVoiceRejections);
                Assert.AreEqual(1, snapshot.TotalVoiceSteals);
                Assert.AreEqual(1, snapshot.ActiveEventCount);
            }
            yield return null;
        }

        private static AudioBankManifest LoadBankManifest()
        {
            string path = Path.Combine(Application.dataPath, "MxFrameworkGenerated", "Audio", "audio_bank_manifest.json");
            Assert.IsTrue(AudioBankManifestJson.TryLoadFile(path, out AudioBankManifest manifest, out string error), error);
            return manifest;
        }
    }
}
