using System.Collections;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MxFramework.Audio;
using MxFramework.Audio.Config;
using MxFramework.Audio.Physical;
using MxFramework.Audio.Physical.Config;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MxFramework.Tests.PlayMode.CharacterTest
{
    public sealed class CharacterTestPhysicalAudioPlayModeTests
    {
        [UnityTest]
        public IEnumerator PlayerMirror_LinksFootstepImpulseResponse()
        {
            yield return null;

            string audioJson = File.ReadAllText(Path.Combine(
                Application.streamingAssetsPath,
                "MxFramework",
                "Audio",
                "audio_runtime_catalog.json"));
            string physicalJson = File.ReadAllText(Path.Combine(
                Application.streamingAssetsPath,
                "MxFramework",
                "Audio",
                "audio_physical_runtime_catalog.json"));
            Assert.That(
                AudioRuntimeCatalogJson.TryLoad(
                    audioJson,
                    out AudioDefinitionCatalog audioCatalog,
                    out string audioError),
                Is.True,
                audioError);
            Assert.That(
                PhysicalAudioRuntimeCatalogJson.TryLoad(
                    physicalJson,
                    out PhysicalAudioDefinitionCatalog physicalCatalog,
                    out string physicalError),
                Is.True,
                physicalError);
            Assert.That(physicalCatalog.AudioCatalogHash, Is.EqualTo(ComputeSha256(audioJson)));
            Assert.That(
                CharacterTestAudioCueCatalog.TryFromRuntimeCatalog(
                    audioCatalog,
                    physicalCatalog,
                    out CharacterTestAudioCueCatalog linkedCatalog,
                    out string linkError),
                Is.True,
                linkError);
            Assert.That(linkedCatalog.PhysicalAudioDefinitions, Is.SameAs(physicalCatalog));

            Assert.That(
                physicalCatalog.TryGetFamilyCandidate(
                    PhysicalAudioInteractionKind.Footstep,
                    PhysicalAudioInteractionMode.Step,
                    0,
                    out PhysicalAudioFamilyDefinition footstepFamily),
                Is.True);
            Assert.That(
                physicalCatalog.TryGetResponseProfile(
                    footstepFamily.ResponseProfileId,
                    out PhysicalAudioResponseProfileDefinition responseProfile),
                Is.True);
            Assert.That(responseProfile.GetCurve(0).Input, Is.EqualTo(PhysicalAudioResponseInput.Impulse));

            Assert.That(
                physicalCatalog.TryGetBasis(
                    footstepFamily.FallbackBasisId,
                    out PhysicalAudioBasisDefinition fallbackBasis),
                Is.True);
            var resolver = new PhysicalAudioResolver(physicalCatalog);
            var parameters = new AudioParameterValue[8];
            PhysicalAudioInteractionFact quiet = CreateFact(fallbackBasis, impulse: 0.1f);
            PhysicalAudioInteractionFact loud = CreateFact(fallbackBasis, impulse: 0.9f);
            Assert.That(
                resolver.TryResolve(quiet, parameters, out PhysicalAudioResolvedPlayback quietResult),
                Is.True);
            float quietVolume = FindParameter(parameters, quietResult.ParameterCount, 500251);
            Assert.That(
                resolver.TryResolve(loud, parameters, out PhysicalAudioResolvedPlayback loudResult),
                Is.True);
            float loudVolume = FindParameter(parameters, loudResult.ParameterCount, 500251);
            Assert.That(loudVolume, Is.GreaterThan(quietVolume));
        }

        private static PhysicalAudioInteractionFact CreateFact(
            in PhysicalAudioBasisDefinition basis,
            float impulse)
        {
            return new PhysicalAudioInteractionFact(
                PhysicalAudioInteractionKind.Footstep,
                PhysicalAudioInteractionMode.Step,
                PhysicalAudioInteractionPhase.Started,
                CreateBody(basis.Impactor),
                CreateBody(basis.Receiver),
                relativeSpeed: 2f,
                normalSpeed: 1f,
                tangentialSpeed: 0f,
                pressure: 0f,
                impulse,
                angleDegrees: 0f,
                intensity: impulse,
                PhysicalAudioDirection.Zero,
                AudioTransform.Origin,
                PhysicalAudioContactDynamicsSource.ContactPointEstimated);
        }

        private static AcousticBodyProperties CreateBody(
            in PhysicalAudioBodyAnchorDefinition anchor)
        {
            return new AcousticBodyProperties(
                anchor.MaterialId,
                anchor.Mass.Anchor,
                anchor.ContactArea.Anchor,
                anchor.Surface,
                anchor.Construction);
        }

        private static float FindParameter(
            AudioParameterValue[] parameters,
            int count,
            int parameterId)
        {
            for (int i = 0; i < count; i++)
            {
                if (parameters[i].ParameterId == parameterId)
                    return parameters[i].Value;
            }
            Assert.Fail("Missing response parameter " + parameterId + ".");
            return 0f;
        }

        private static string ComputeSha256(string value)
        {
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
            var builder = new StringBuilder(7 + (hash.Length * 2));
            builder.Append("sha256:");
            for (int i = 0; i < hash.Length; i++)
                builder.Append(hash[i].ToString("x2"));
            return builder.ToString();
        }
    }
}
