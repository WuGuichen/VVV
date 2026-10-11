using System;
using System.Collections.Generic;
using System.Reflection;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestRuntimeAiLabPresentationDiagnosticProbeTests
    {
        [Test]
        public void RuntimeAiLabPresentationDiagnosticProbe_IdenticalSamplePasses()
        {
            CharacterTestRuntimeAiLabPresentationDiagnosticSample sample = CreateSample();

            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabPresentationDiagnosticProbe.Compare(sample, sample);

            Assert.That(report.Passed, Is.True, report.SummaryText);
        }

        [Test]
        public void RuntimeAiLabPresentationDiagnosticProbe_LocatesViewAnimationVfxAndOverlay()
        {
            CharacterTestRuntimeAiLabPresentationDiagnosticSample expected = CreateSample(
                character: CreateCharacter(hasUnityView: true, actionKey: "idle"),
                vfx: new CharacterTestRuntimeAiLabFrameSyncVfxRuntimeSample(
                    runtimeAvailable: true,
                    moduleId: "runtime-vfx",
                    definitionHash: 17L,
                    nextInstanceId: 3L,
                    activeCount: 0,
                    pendingSpawnCount: 0,
                    pendingStopCount: 0,
                    pendingEventCount: 0,
                    activeInstances: Array.Empty<CharacterTestRuntimeAiLabFrameSyncVfxInstanceSample>()),
                overlay: new CharacterTestRuntimeAiLabFrameSyncOverlaySample(
                    available: true,
                    frame: 42,
                    agentEntityId: 1,
                    targetEntityId: 2,
                    sourceCenter: Vector3.zero,
                    targetCenter: Vector3.one,
                    moveEnd: Vector3.right,
                    attackRange: 2f));
            CharacterTestRuntimeAiLabPresentationDiagnosticSample actual = CreateSample(
                character: CreateCharacter(hasUnityView: false, actionKey: "attack"));

            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabPresentationDiagnosticProbe.Compare(expected, actual);

            Assert.That(report.Passed, Is.False);
            Assert.That(Contains(report.MismatchRows, "view", "available"), Is.True, report.SummaryText);
            Assert.That(Contains(report.MismatchRows, "animationInput", "actionKey"), Is.True, report.SummaryText);
            Assert.That(Contains(report.MismatchRows, "vfx", "runtimeAvailable"), Is.True, report.SummaryText);
            Assert.That(Contains(report.MismatchRows, "overlay", "available"), Is.True, report.SummaryText);
        }

        [Test]
        public void RuntimeAiLabPresentationDiagnosticProbe_MissingCharacterUsesViewLayer()
        {
            CharacterTestRuntimeAiLabPresentationDiagnosticSample expected = CreateSample(
                character: CreateCharacter(hasUnityView: true, actionKey: "idle"));
            CharacterTestRuntimeAiLabPresentationDiagnosticSample actual = CreateSample();

            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabPresentationDiagnosticProbe.Compare(expected, actual);

            Assert.That(Contains(report.MismatchRows, "view", "missing"), Is.True, report.SummaryText);
            for (int i = 0; i < report.MismatchRows.Count; i++)
                Assert.That(report.MismatchRows[i].Layer, Is.EqualTo("view"));
        }

        [Test]
        public void RuntimeAiLabPresentationDiagnosticSample_DoesNotCarryGameplayPlannerOrProjectileState()
        {
            PropertyInfo[] properties = typeof(CharacterTestRuntimeAiLabPresentationDiagnosticSample)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public);
            var names = new string[properties.Length];
            for (int i = 0; i < properties.Length; i++)
                names[i] = properties[i].Name;

            CollectionAssert.AreEquivalent(
                new[] { "Characters", "VfxRuntime", "Overlay" },
                names);
        }

        private static CharacterTestRuntimeAiLabPresentationDiagnosticSample CreateSample(
            CharacterTestRuntimeAiLabPresentationCharacterSample? character = null,
            CharacterTestRuntimeAiLabFrameSyncVfxRuntimeSample vfx = default,
            CharacterTestRuntimeAiLabFrameSyncOverlaySample overlay = default)
        {
            CharacterTestRuntimeAiLabPresentationCharacterSample[] characters = character.HasValue
                ? new[] { character.Value }
                : Array.Empty<CharacterTestRuntimeAiLabPresentationCharacterSample>();
            return new CharacterTestRuntimeAiLabPresentationDiagnosticSample(characters, vfx, overlay);
        }

        private static CharacterTestRuntimeAiLabPresentationCharacterSample CreateCharacter(
            bool hasUnityView,
            string actionKey)
        {
            return new CharacterTestRuntimeAiLabPresentationCharacterSample(
                entityId: 1,
                hasUnityView: hasUnityView,
                unityViewXMillimeters: hasUnityView ? 1000 : 0,
                unityViewYMillimeters: 0,
                unityViewZMillimeters: 0,
                unityViewYawMilliDegrees: 0,
                presentationState: null,
                animationBlendXMilli: 0,
                animationBlendYMilli: 0,
                animationSpeed01Milli: 0,
                animationActionKey: actionKey,
                animationGrounded: true,
                animationAirborne: false);
        }

        private static bool Contains(
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncMismatch> rows,
            string layer,
            string field)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (string.Equals(rows[i].Layer, layer, StringComparison.Ordinal)
                    && string.Equals(rows[i].Field, field, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
