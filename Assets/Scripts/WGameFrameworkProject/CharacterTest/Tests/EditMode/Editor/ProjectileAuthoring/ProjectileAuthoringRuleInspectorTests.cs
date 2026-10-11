using System;
using System.Collections.Generic;
using System.Linq;
using MxFramework.Editor.ProjectileAuthoring;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Tests.ProjectileAuthoring
{
    public sealed class ProjectileAuthoringRuleInspectorTests
    {
        [Test]
        public void PreviewTimeline_IsOnlyVisibleForRuntimeValidation()
        {
            using Fixture fixture = Open(BuildWorkspace(
                shared: false,
                motion: "BallisticArcToTarget",
                impact: "AreaPulseOnImpact"));

            Assert.That(fixture.Window.PreviewTimelineVisibleForTests, Is.False);

            fixture.Window.SetPreviewModeForTests("Runtime Validation");
            Assert.That(fixture.Window.PreviewTimelineVisibleForTests, Is.True);

            fixture.Window.SetPreviewModeForTests("VFX Preview");
            Assert.That(fixture.Window.PreviewTimelineVisibleForTests, Is.False);
        }

        [Test]
        public void M3_E01_E02_E04_E05_E10_DefinitionSectionsAndSchemaControlsAreComplete()
        {
            using Fixture fixture = Open(BuildWorkspace(shared: false, motion: "BallisticArcToTarget", impact: "AreaPulseOnImpact"));
            Select(fixture.Window, "definition.shared");
            Assert.NotNull(fixture.Window.rootVisualElement.Q("definition-identity-section"));
            Assert.NotNull(fixture.Window.rootVisualElement.Q("definition-motion-section"));
            Assert.NotNull(fixture.Window.rootVisualElement.Q("definition-collision-section"));
            Assert.NotNull(fixture.Window.rootVisualElement.Q("definition-impact-section"));
            Assert.NotNull(fixture.Window.rootVisualElement.Q("direct-hit-section"));
            Assert.NotNull(fixture.Window.rootVisualElement.Q("area-hit-section"));
            CollectionAssert.AreEquivalent(new[] { "Linear", "ArcToTarget", "BoomerangReturn", "SwayForward", "HomingToTarget", "BezierToTarget", "BallisticArcToTarget" }, fixture.Window.rootVisualElement.Q<PopupField<string>>("motion-kind").choices);
            CollectionAssert.AreEquivalent(new[] { "Constant", "EaseInQuadratic", "EaseOutQuadratic", "EaseInOutSmoothstep" }, fixture.Window.rootVisualElement.Q<PopupField<string>>("motion-speed-profile").choices);
            Assert.NotNull(fixture.Window.rootVisualElement.Q("motion-gravity"));
            Assert.NotNull(fixture.Window.rootVisualElement.Q("motion-derived-tangent"));
            Assert.IsFalse(fixture.Window.rootVisualElement.Q<TextField>("collision-layer-mask-readonly").enabledSelf);
            Assert.IsFalse(fixture.Window.rootVisualElement.Q<TextField>("impact-area-mask-readonly").enabledSelf);
            Assert.IsFalse(fixture.Window.rootVisualElement.Q<TextField>("impact-presentation-profile-readonly").enabledSelf);
        }

        [Test]
        public void M3_E03_E07_ConditionalFieldsAndOrderedListOperationsAreExplicit()
        {
            using Fixture fixture = Open(BuildWorkspace(shared: false, motion: "BezierToTarget", impact: "DirectHit"));
            Select(fixture.Window, "definition.shared");
            Assert.NotNull(fixture.Window.rootVisualElement.Q("motion-bezier-a"));
            Assert.NotNull(fixture.Window.rootVisualElement.Q("motion-bezier-b"));
            Assert.IsNull(fixture.Window.rootVisualElement.Q("motion-gravity"));
            Assert.IsNull(fixture.Window.rootVisualElement.Q("impact-area-radius"));
            Assert.IsNull(fixture.Window.rootVisualElement.Q("impact-trigger-expire"));
            Assert.IsNull(fixture.Window.rootVisualElement.Q("impact-area-mask-readonly"));
            Assert.IsNull(fixture.Window.rootVisualElement.Q("area-hit-section"));
            Assert.NotNull(fixture.Window.rootVisualElement.Q("direct-hit-targets"));
            Assert.NotNull(fixture.Window.rootVisualElement.Q("direct-hit-damage-tags-add"));
            Assert.NotNull(fixture.Window.rootVisualElement.Q("direct-hit-buffs-add"));
            Assert.NotNull(fixture.Window.rootVisualElement.Q("impact-effect-timeline"));
        }

        [Test]
        public void M3_E08_E09_RequestUsesKnownSocketAuthorityAndThreeAimKinds()
        {
            ProjectileAuthoringWorkspaceView workspace = BuildWorkspace(shared: false, motion: "Linear", impact: "DirectHit");
            using Fixture fixture = Open(workspace);
            Select(fixture.Window, "request.one");
            Assert.NotNull(fixture.Window.rootVisualElement.Q("request-definition-section"));
            Assert.NotNull(fixture.Window.rootVisualElement.Q("request-origin-section"));
            Assert.NotNull(fixture.Window.rootVisualElement.Q("request-aim-section"));
            CollectionAssert.AreEqual(new[] { "Socket" }, fixture.Window.rootVisualElement.Q<PopupField<string>>("request-origin-kind").choices);
            CollectionAssert.AreEqual(new[] { "hand_r", "root" }, fixture.Window.rootVisualElement.Q<PopupField<string>>("request-known-socket").choices);
            CollectionAssert.AreEquivalent(new[] { "CasterForwardRange", "LockedTargetCenter", "LockedTargetOrCasterForwardRange" }, fixture.Window.rootVisualElement.Q<PopupField<string>>("request-aim-kind").choices);
            CollectionAssert.AreEquivalent(new[] { "CasterForwardRange", "LockedTargetCenter", "LockedTargetOrCasterForwardRange" }, fixture.Window.rootVisualElement.Q<PopupField<string>>("request-aim-fallback").choices);
            Assert.NotNull(fixture.Window.rootVisualElement.Q("request-local-offset"));
            Assert.NotNull(fixture.Window.rootVisualElement.Q("request-fallback-offset"));
            Assert.NotNull(fixture.Window.rootVisualElement.Q("request-target-height"));
        }

        [Test]
        public void M3_E13_E14_E15_SharedDefinitionConfirmsOnceAndSingleDefinitionCommitsDirectly()
        {
            using (Fixture shared = Open(BuildWorkspace(shared: true, motion: "Linear", impact: "DirectHit")))
            {
                Select(shared.Window, "definition.shared");
                shared.Window.CommitFieldForTests("motion.speedPerFrame", 2f);
                Assert.AreEqual(1, shared.Service.Sequences.Count, "Shared edit must produce one plan request before confirmation.");
                Assert.AreEqual(DisplayStyle.Flex, shared.Window.rootVisualElement.Q("reference-impact-panel").resolvedStyle.display);
                shared.Window.InvokeActionForTests("reference-impact-confirm");
                Assert.AreEqual(2, shared.Service.Sequences.Count, "Shared confirmation must produce the confirmed request.");
                Assert.AreEqual(1, shared.Window.SessionForTests.PendingCommands.Count, "Confirmed shared edit must create one history node.");
                Assert.AreEqual("motion.speedPerFrame", shared.Service.Sequences.Last().Last().fieldEdits.Single().fieldPath);
            }

            using (Fixture single = Open(BuildWorkspace(shared: false, motion: "Linear", impact: "DirectHit")))
            {
                Select(single.Window, "definition.shared");
                single.Window.CommitFieldForTests("motion.speedPerFrame", 3f);
                Assert.AreEqual(2, single.Service.Sequences.Count, "A single-reference edit should plan and commit without an extra user blocker.");
                Assert.AreEqual(1, single.Window.SessionForTests.PendingCommands.Count, "Auto-confirmed single edit must create one history node.");
            }
        }

        [Test]
        public void M4_E01_E03_E05_PresentationInspectorUsesSevenStableSlotsAndTypedCommands()
        {
            using Fixture fixture = Open(BuildWorkspace(shared: false, motion: "Linear", impact: "DirectHit"));
            Select(fixture.Window, "definition.shared");
            string[] slots = { "spawn", "flightBody", "trail", "directImpact", "areaImpact", "blockedImpact", "expire" };
            foreach (string slot in slots)
            {
                fixture.Window.SelectPresentationSlotForTests(slot);
                Assert.NotNull(fixture.Window.rootVisualElement.Q("presentation-slot-" + slot));
                Assert.NotNull(fixture.Window.rootVisualElement.Q("presentation-slot-" + slot + "-choose"));
                Assert.NotNull(fixture.Window.rootVisualElement.Q("presentation-slot-" + slot + "-orientation"));
                Assert.NotNull(fixture.Window.rootVisualElement.Q("presentation-slot-" + slot + "-offset"));
            }
            fixture.Window.SelectPresentationSlotForTests("flightBody");
            Assert.IsNull(fixture.Window.rootVisualElement.Q("presentation-view-name-prefix"),
                "The internal instance-name prefix must not occupy the normal editing surface.");
            Assert.IsNull(fixture.Window.rootVisualElement.Q("presentation-slot-flightBody-anchor"),
                "A slot-derived anchor must be presented as context, not as a fake editable field.");
            Assert.NotNull(fixture.Window.rootVisualElement.Q("presentation-slot-flightBody-resource-card"));
            Assert.IsTrue(fixture.Window.rootVisualElement.Q("presentation-slot-flightBody-actions")
                .ClassListContains("pa-vfx-actions"));
            Assert.IsNull(fixture.Window.rootVisualElement.Q("presentation-slot-flightBody-lifetime"),
                "Persistent slots must not expose a meaningless lifetime override.");
            fixture.Window.SelectPresentationSlotForTests("spawn");
            Assert.NotNull(fixture.Window.rootVisualElement.Q<Foldout>("presentation-slot-spawn-advanced"));
            Assert.IsTrue(fixture.Window.rootVisualElement.Q<SliderInt>("presentation-slot-spawn-lifetime").enabledSelf);
            fixture.Window.SelectPresentationSlotForTests("flightBody");
            fixture.Window.ClearPresentationSlotForTests("flightBody");
            Assert.AreEqual("ClearPresentationSlot", fixture.Service.Sequences.Last().Last().kind);
            Assert.AreEqual("flightBody", fixture.Service.Sequences.Last().Last().presentationSlotKind);
            Assert.AreEqual(1, fixture.Window.SessionForTests.PendingCommands.Count);
        }

        [Test]
        public void M4_E04_E08_SharedPresentationUsesFreshConfirmationAndProblemFocusesExactSlot()
        {
            ProjectileAuthoringWorkspaceView workspace = BuildWorkspace(shared: true, motion: "Linear", impact: "DirectHit");
            workspace.diagnostics = new[] { new ProjectileAuthoringProblemView { code = "PROJAUTH012", severity = "Error", objectPath = "definition:definition.shared", relatedStableIds = new[] { "definition.shared" }, fieldPath = "presentationBinding.slots.flightBody.orientation", message = "bad orientation" } };
            using Fixture fixture = Open(workspace);
            Select(fixture.Window, "definition.shared");
            fixture.Window.ClearPresentationSlotForTests("flightBody");
            Assert.AreEqual(1, fixture.Service.Sequences.Count);
            Assert.AreEqual(DisplayStyle.Flex, fixture.Window.rootVisualElement.Q("reference-impact-panel").resolvedStyle.display);
            fixture.Window.InvokeActionForTests("reference-impact-confirm");
            Assert.AreEqual(2, fixture.Service.Sequences.Count);
            Assert.AreEqual("workspace-token", fixture.Service.Sequences.Last().Last().workspaceToken);
            Assert.AreEqual("confirmation-workspace-token", fixture.Service.Sequences.Last().Last().confirmationToken);
            fixture.Window.LocateProblemForTests(0);
            Assert.AreEqual("definition.shared", fixture.Window.SessionForTests.SelectedStableId);
            Assert.AreEqual("presentation-slot-flightBody-orientation", fixture.Window.LastProblemFocusTargetForTests);
        }

        [Test]
        public void M4_E06_CandidateIsTransientAndFormalSelectionClearsOverrideThroughSharedBridge()
        {
            ProjectileAuthoringWorkspaceView workspace = BuildWorkspace(shared: false, motion: "Linear", impact: "DirectHit");
            using Fixture fixture = Open(workspace);
            Select(fixture.Window, "definition.shared");
            var bridge = new FakeVfxSelectionBridge();
            fixture.Window.SetPresentationVfxBridgeForTests(bridge);
            var resource = new ProjectileResourceKeyView { id = "vfx.test.flight", typeId = "GameObject", packageId = "test" };
            string workspaceJson = JsonUtility.ToJson(workspace);
            string workspaceHash = fixture.Window.SessionForTests.WorkspaceHash;
            string draftPath = fixture.Window.SessionForTests.DraftPath;
            fixture.Window.SelectPresentationResourceForTests("flightBody", resource, candidate: true);
            Assert.AreEqual(0, bridge.FormalSelectionCount, "Candidate selection must not clear or persist the local override through the formal path.");
            Assert.AreEqual(0, fixture.Service.Sequences.Count, "Candidate selection must not emit plan or typed mutation commands.");
            Assert.AreEqual(0, fixture.Window.SessionForTests.PendingCommands.Count, "Candidate selection must not create a history node.");
            Assert.AreEqual(draftPath, fixture.Window.SessionForTests.DraftPath, "Candidate selection must not create or replace a raw draft.");
            Assert.AreEqual(workspaceHash, fixture.Window.SessionForTests.WorkspaceHash, "Candidate selection must not change the package/generated workspace hash.");
            Assert.AreEqual(workspaceJson, JsonUtility.ToJson(workspace), "Candidate selection must not change source, generated projection, or serialized workspace state.");

            fixture.Window.SelectPresentationResourceForTests("flightBody", resource, candidate: false);
            Assert.AreEqual(1, bridge.FormalSelectionCount, "Formal selection must execute the shared override-clear path.");
            Assert.Greater(fixture.Service.Sequences.Count, 0, "Formal selection must continue through the typed SetPresentationSlot path.");
            Assert.AreEqual(1, fixture.Window.SessionForTests.PendingCommands.Count, "Formal selection must create exactly one history node.");
            Assert.AreEqual("Candidate stale", bridge.Resolve(Serialize(resource)).Status, "Candidate/stale state remains a transient resolver view.");
        }

        [Test]
        public void PresentationVfxSelection_FromRequestTargetsOwningDefinition()
        {
            ProjectileAuthoringWorkspaceView workspace = BuildWorkspace(shared: false, motion: "Linear", impact: "DirectHit");
            using Fixture fixture = Open(workspace);
            Select(fixture.Window, "request.one");
            Assert.IsTrue(fixture.Window.CurrentIsRequest);

            var resource = new ProjectileResourceKeyView
            {
                id = "vfx.authored.sample.fireball_impact",
                typeId = "GameObject",
                packageId = "mxframework.samples"
            };
            fixture.Window.SelectPresentationResourceForTests("areaImpact", resource, candidate: false);

            Assert.Greater(fixture.Service.Sequences.Count, 0, "Selecting a VFX from a request must emit a typed presentation mutation.");
            ProjectileAuthoringMutationRequestView command = fixture.Service.Sequences[0].Single();
            Assert.AreEqual("SetPresentationSlot", command.kind);
            Assert.AreEqual("definition.shared", command.sourceStableId);
            Assert.AreEqual("definition.shared", command.stableId);
            Assert.AreEqual("definition.shared", command.definitionStableId);
            Assert.AreEqual("areaImpact", command.presentationSlotKind);
            Assert.AreEqual(resource.id, command.presentationResource.id);
            Assert.AreEqual(resource.packageId, command.presentationResource.packageId);
            Assert.AreEqual(1, fixture.Window.SessionForTests.PendingCommands.Count, "The resolved definition edit must enter the undoable draft history.");
        }

        [Test]
        public void PreviewSelectionCleanup_SourceToGeneratedOnlyToNoSelection_ClearsAllTransientState()
        {
            ProjectileAuthoringWorkspaceView workspace = BuildWorkspace(false, "Linear", "DirectHit");
            workspace.generatedOnly = new[]
            {
                new ProjectileGeneratedOnlyListItem { stableId = "generated.only", definitionId = 999, status = "GeneratedOnly", complete = false }
            };
            using Fixture fixture = Open(workspace);
            Select(fixture.Window, "definition.shared");
            Assert.That(fixture.Window.PreviewFrameCountForTests, Is.GreaterThan(0));

            fixture.Window.SelectGeneratedOnlyPreviewForTests("generated.only");
            Assert.That(fixture.Window.PreviewFrameCountForTests, Is.Zero);
            Assert.That(fixture.Window.SceneEditRegisteredForTests, Is.False);
            Assert.That(fixture.Window.PreviewHasSelectedMarkerForTests, Is.False);
            Assert.That(fixture.Window.rootVisualElement.Q<Label>("projectile-preview-state").text, Is.EqualTo("No preview"));

            fixture.Window.ClearPreviewSelectionForTests();
            Assert.That(fixture.Window.PreviewFrameCountForTests, Is.Zero);
            Assert.That(fixture.Window.SceneEditRegisteredForTests, Is.False);
            Assert.That(fixture.Window.PreviewHasSelectedMarkerForTests, Is.False);
        }

        [Test]
        public void PreviewDiagnostics_RepeatedRefreshAndScenarioChangesRemainDeduplicated()
        {
            using Fixture fixture = Open(BuildWorkspace(false, "Linear", "DirectHit"));
            Select(fixture.Window, "definition.shared");
            fixture.Window.SetPreviewScenarioForTests(ProjectilePreviewScenario.Miss);
            fixture.Window.SetPreviewScenarioForTests(ProjectilePreviewScenario.DirectHit);
            fixture.Window.SetPreviewScenarioForTests(ProjectilePreviewScenario.Miss);

            string[] codes = fixture.Window.VisibleProblemCodesForTests.ToArray();
            Assert.That(codes.Count(value => value == "PROJAUTH022"), Is.LessThanOrEqualTo(1));
            Assert.That(codes.Count(value => value == "PROJAUTH024"), Is.LessThanOrEqualTo(1));
        }

        [Test]
        public void SceneEditMode_RefreshRestoresForValidSourceButGeneratedAndNoSelectionCloseIt()
        {
            ProjectileAuthoringWorkspaceView workspace = BuildWorkspace(false, "Linear", "DirectHit");
            workspace.generatedOnly = new[]
            {
                new ProjectileGeneratedOnlyListItem { stableId = "generated.only", definitionId = 999, status = "GeneratedOnly" }
            };
            using Fixture fixture = Open(workspace);
            Select(fixture.Window, "definition.shared");
            fixture.Window.BeginSceneEditForTests();
            Assert.That(fixture.Window.SceneEditRegisteredForTests, Is.True);

            fixture.Window.CommitSceneEditForTests(new ProjectileAuthoringFieldEditView
            {
                fieldPath = "motion.speedPerFrame",
                valueKind = "Float",
                floatValue = 1.25f
            });
            Assert.That(fixture.Window.SceneEditRegisteredForTests, Is.True,
                "A typed Scene Edit commit recompiles the valid source and must restore Scene Edit mode.");

            fixture.Window.SelectSourcePreviewForTests("request.one", isRequest: true);
            Assert.That(fixture.Window.SceneEditRegisteredForTests, Is.True,
                "Switching between editable source entries preserves the explicitly enabled mode.");

            fixture.Window.SelectGeneratedOnlyPreviewForTests("generated.only");
            Assert.That(fixture.Window.SceneEditRegisteredForTests, Is.False);
            fixture.Window.SelectSourcePreviewForTests("definition.shared");
            Assert.That(fixture.Window.SceneEditRegisteredForTests, Is.False,
                "Returning from Generated-only must not reopen a mode that was explicitly closed by invalid selection.");

            fixture.Window.BeginSceneEditForTests();
            fixture.Window.ClearPreviewSelectionForTests();
            Assert.That(fixture.Window.SceneEditRegisteredForTests, Is.False);
            fixture.Window.SelectSourcePreviewForTests("definition.shared");
            Assert.That(fixture.Window.SceneEditRegisteredForTests, Is.False);
        }

        [Test]
        public void M4_E05_VfxSemanticsAreAdvisoryAndCandidateIdentityStillBlocks()
        {
            Assert.That(ProjectileAuthoringVfxSelectionBridge.ContractDiagnostic(
                "flightBody", "Flight", "Path", "Local", "ManualStop"), Is.Empty);
            Assert.That(ProjectileAuthoringVfxSelectionBridge.ContractDiagnostic(
                "flightBody", "Impact", "HitPoint", "World", "OneShot"), Is.Not.Empty);
            Assert.That(ProjectileAuthoringVfxSelectionBridge.ContractDiagnostic(
                "directImpact", "Impact", "HitPoint", "World", "OneShot"), Is.Empty);
            Assert.That(ProjectileAuthoringVfxSelectionBridge.ContractDiagnostic(
                "directImpact", "Other", "HitPoint", "World", "OneShot"), Does.Contain("role"));
            var candidate = new MxFramework.Editor.CharacterAction.CharacterActionVfxSelectionOption
            {
                ResourceKey = new MxFramework.Resources.ResourceKey("vfx.other.flight", "GameObject", packageId: "test"),
                Role = "Flight",
                Anchor = "{\"anchor\":\"Path\",\"space\":\"Local\"}",
                Lifecycle = "{\"mode\":\"ManualStop\"}",
                IsCandidate = true
            };
            Assert.That(ProjectileAuthoringVfxSelectionBridge.PickerDiagnostic(
                "flightBody", "vfx.current.flight:GameObject@test", candidate), Does.Contain("existing stable ResourceKey"));
        }

        [Test]
        public void M4_E01_E06_LifecycleCardUsesSharedResolverThumbnailAndTransientState()
        {
            using Fixture fixture = Open(BuildWorkspace(shared: false, motion: "Linear", impact: "DirectHit"));
            var thumbnail = new Texture2D(2, 2);
            try
            {
                var bridge = new FakeVfxSelectionBridge { Thumbnail = thumbnail };
                fixture.Window.SetPresentationVfxBridgeForTests(bridge);
                Select(fixture.Window, "definition.shared");
                Assert.NotNull(fixture.Window.rootVisualElement.Q<Image>("lifecycle-card-flightBody-thumbnail"));
                Assert.That(fixture.Window.rootVisualElement.Q("lifecycle-card-flightBody").Q<Label>().text, Does.Contain("Candidate"));
            }
            finally { UnityEngine.Object.DestroyImmediate(thumbnail); }
        }

        [Test]
        public void M4_E05_E06_ProductionPlayerGateUpgradesCandidateAndReadinessToErrors()
        {
            using Fixture fixture = Open(BuildWorkspace(shared: false, motion: "Linear", impact: "DirectHit"));
            Select(fixture.Window, "definition.shared");
            var bridge = new FakeVfxSelectionBridge();
            fixture.Window.SetPresentationVfxBridgeForTests(bridge);
            Assert.NotNull(fixture.Window.rootVisualElement.Q("presentation-production-gate"));
            Assert.IsFalse(fixture.Window.ValidatePresentationProductionGateForTests(),
                "A local Candidate must block the explicit Production/Player gate.");
            bridge.Candidate = false;
            bridge.Status = "Production";
            bridge.Review = "Ready";
            bridge.Role = "Impact";
            bridge.Anchor = "HitPoint";
            bridge.Lifecycle = "OneShot";
            bridge.ActualPrefabPath = "Assets/VFX/Test.prefab";
            Assert.IsTrue(fixture.Window.ValidatePresentationProductionGateForTests(),
                "VFX semantic metadata must not block a Production + Ready formal binding.");
            fixture.Window.SelectPresentationSlotForTests("flightBody");
            Assert.That(
                fixture.Window.rootVisualElement.Q<Label>("presentation-slot-flightBody-status").text,
                Does.Contain("生产可用"));
        }

        [Test]
        public void M3_E11_E12_FieldProblemsStayInlineAndLocateThroughProblems()
        {
            ProjectileAuthoringWorkspaceView workspace = BuildWorkspace(shared: false, motion: "Linear", impact: "DirectHit");
            workspace.diagnostics = new[] { new ProjectileAuthoringProblemView { code = "PROJAUTH027", severity = "Error", message = "speed invalid", sourcePath = "config/projectiles/definition.shared.json", fieldPath = "motion.speedPerFrame" } };
            using Fixture fixture = Open(workspace);
            Select(fixture.Window, "definition.shared");
            Assert.That(fixture.Window.rootVisualElement.Q("definition-motion-section").Query<HelpBox>().ToList().Any(value => value.text.Contains("PROJAUTH027")), Is.True);
            Foldout motion = fixture.Window.rootVisualElement.Q<Foldout>("definition-motion-section");
            motion.value = false;
            fixture.Window.LocateProblemForTests(0);
            Assert.AreEqual("definition.shared", fixture.Window.SessionForTests.SelectedStableId);
            Assert.IsTrue(fixture.Window.rootVisualElement.Q<Foldout>("definition-motion-section").value, "Locate must expand the field's owning section.");
            Assert.AreEqual("motion-speed", fixture.Window.LastProblemFocusTargetForTests, "Locate must map fieldPath to the exact control before focusing it.");
        }

        [Test]
        public void M3_E12_CombinedSourceProblemsDisambiguateDefinitionAndRequestByObjectAndFieldPath()
        {
            ProjectileAuthoringWorkspaceView workspace = BuildWorkspace(shared: false, motion: "Linear", impact: "DirectHit");
            const string combinedPath = "config/projectiles/combined.json";
            workspace.requests[0].stableId = "definition.shared";
            workspace.requests[0].sourcePath = combinedPath;
            workspace.requests[0].detail.stableId = "definition.shared";
            workspace.definitions[0].requestIds = new[] { "definition.shared" };
            workspace.definitions[0].sourcePaths = new[] { combinedPath };
            workspace.sources = new[] { new ProjectileAuthoringSourceView { stableId = "definition.shared", relativePath = combinedPath, expectedSha256 = "hash" } };
            workspace.diagnostics = new[]
            {
                new ProjectileAuthoringProblemView { code = "DEF", severity = "Error", message = "definition motion", sourcePath = combinedPath, objectPath = "definition.shared", relatedStableIds = new[] { "definition.shared" }, fieldPath = "motion.speedPerFrame" },
                new ProjectileAuthoringProblemView { code = "REQ", severity = "Error", message = "request origin", sourcePath = combinedPath, objectPath = "definition.shared", relatedStableIds = new[] { "definition.shared" }, fieldPath = "origin.socketId" }
            };
            using Fixture fixture = Open(workspace);

            fixture.Window.LocateProblemForTests(0);
            Assert.IsFalse(fixture.Window.CurrentIsRequest);
            Assert.AreEqual("motion-speed", fixture.Window.LastProblemFocusTargetForTests);

            fixture.Window.LocateProblemForTests(1);
            Assert.IsTrue(fixture.Window.CurrentIsRequest);
            Assert.AreEqual("request-known-socket", fixture.Window.LastProblemFocusTargetForTests);
        }

        [Test]
        public void M3_E02_E03_E15_MotionTypePopupCommitsAtomicRequiredFieldsAndProjectsTargetInspector()
        {
            string[] kinds = { "Linear", "ArcToTarget", "BoomerangReturn", "SwayForward", "HomingToTarget", "BezierToTarget", "BallisticArcToTarget" };
            foreach (string kind in kinds)
            {
                ProjectileAuthoringWorkspaceView workspace = BuildWorkspace(shared: false, motion: "Linear", impact: "DirectHit");
                ProjectileMotionView motion = workspace.definitions[0].detail.motion;
                motion.arcHeight = 0f; motion.gravityPerFrameSquared = 0f; motion.maxLaunchAngleDegrees = 0f; motion.lateralAmplitude = 0f; motion.lateralCycles = 0;
                motion.bezierControlOffsetA = new ProjectileVector3View(); motion.bezierControlOffsetB = new ProjectileVector3View(); motion.homing = null;
                using Fixture fixture = Open(workspace);
                Select(fixture.Window, "definition.shared");
                fixture.Window.CommitPopupForTests("motion-kind", kind);

                ProjectileAuthoringMutationRequestView command = fixture.Window.SessionForTests.PendingCommands.Single();
                Assert.AreEqual(1, command.fieldEdits.Count(value => value.fieldPath == "motion.kind"), kind);
                Assert.AreEqual(kind, command.fieldEdits.Single(value => value.fieldPath == "motion.kind").stringValue);
                AssertCompoundMotionFields(command.fieldEdits, kind);
                Assert.AreEqual(1, fixture.Window.SessionForTests.PendingCommands.Count, kind + " must create one history node.");
                Assert.AreEqual(kind, fixture.Window.rootVisualElement.Q<PopupField<string>>("motion-kind").value);
                AssertMotionConditionalControl(fixture.Window, kind);
            }
        }

        [Test]
        public void M3_E03_E04_E07_E15_ImpactPopupCreatesAtomicAreaEventAndProjectsConditionalSections()
        {
            ProjectileAuthoringWorkspaceView workspace = BuildWorkspace(shared: false, motion: "Linear", impact: "DirectHit");
            workspace.definitions[0].detail.impact.areaRadius = 0f;
            workspace.definitions[0].detail.impact.areaHitEvent = null;
            using Fixture fixture = Open(workspace);
            Select(fixture.Window, "definition.shared");
            fixture.Window.CommitPopupForTests("impact-policy", "AreaPulseOnImpact");

            ProjectileAuthoringMutationRequestView area = fixture.Window.SessionForTests.PendingCommands.Single();
            string[] paths = area.fieldEdits.Select(value => value.fieldPath).ToArray();
            CollectionAssert.Contains(paths, "impact.policyKind");
            CollectionAssert.Contains(paths, "impact.areaRadius");
            CollectionAssert.Contains(paths, "impact.areaHitEvent.targetTypes");
            CollectionAssert.Contains(paths, "impact.areaHitEvent.damage.damageTypeId");
            CollectionAssert.Contains(paths, "impact.areaHitEvent.damage.tags");
            CollectionAssert.Contains(paths, "impact.areaHitEvent.onHitBuffs");
            Assert.Greater(area.fieldEdits.Single(value => value.fieldPath == "impact.areaRadius").floatValue, 0f);
            Assert.AreEqual(1, fixture.Window.SessionForTests.PendingCommands.Count);
            Assert.NotNull(fixture.Window.rootVisualElement.Q("area-hit-section"));
            Assert.NotNull(fixture.Window.rootVisualElement.Q("impact-area-radius"));

            fixture.Window.CommitPopupForTests("impact-policy", "DirectHit");
            Assert.AreEqual(2, fixture.Window.SessionForTests.PendingCommands.Count, "Each policy change must add exactly one history node.");
            Assert.AreEqual("DirectHit", fixture.Window.rootVisualElement.Q<PopupField<string>>("impact-policy").value);
            Assert.IsNull(fixture.Window.rootVisualElement.Q("area-hit-section"));
        }

        [Test]
        public void M3_E08_E09_E11_ControlCommitsAreTypedAndTransientDiagnosticsAreReplaced()
        {
            ProjectileAuthoringWorkspaceView workspace = BuildWorkspace(shared: false, motion: "Linear", impact: "DirectHit");
            workspace.requests[0].detail.origin.kind = "LegacySocket";
            workspace.diagnostics = new[] { new ProjectileAuthoringProblemView { code = "BASELINE", severity = "Warning", message = "workspace warning", sourcePath = workspace.requests[0].sourcePath } };
            using Fixture fixture = Open(workspace);
            Select(fixture.Window, "request.one");

            PopupField<string> fallback = fixture.Window.rootVisualElement.Q<PopupField<string>>("request-aim-fallback");
            Assert.NotNull(fallback);
            fixture.Window.CommitPopupForTests("request-aim-fallback", "LockedTargetCenter");
            ProjectileAuthoringFieldEditView fallbackEdit = fixture.Service.Sequences.Last().Last().fieldEdits.Single();
            Assert.AreEqual("aim.fallbackKind", fallbackEdit.fieldPath);
            Assert.AreEqual("Enum", fallbackEdit.valueKind);
            Assert.AreEqual("LockedTargetCenter", fallbackEdit.stringValue);
            CollectionAssert.Contains(fixture.Window.VisibleProblemCodesForTests, "PROJAUTH028");
            Assert.AreEqual(1, fixture.Window.VisibleProblemCodesForTests.Count(value => value == "PROJAUTH028"));

            PopupField<string> origin = fixture.Window.rootVisualElement.Q<PopupField<string>>("request-origin-kind");
            Assert.NotNull(origin);
            fixture.Window.CommitPopupForTests("request-origin-kind", "Socket");
            ProjectileAuthoringFieldEditView originEdit = fixture.Service.Sequences.Last().Last().fieldEdits.Single();
            Assert.AreEqual("origin.kind", originEdit.fieldPath);
            Assert.AreEqual("Enum", originEdit.valueKind);
            Assert.AreEqual("Socket", originEdit.stringValue);
            CollectionAssert.DoesNotContain(fixture.Window.VisibleProblemCodesForTests, "PROJAUTH028");
            CollectionAssert.Contains(fixture.Window.VisibleProblemCodesForTests, "BASELINE", "Successful field commit must clear transient field diagnostics without clearing workspace diagnostics.");
        }

        [Test]
        public void M3_E17_NonSharedFieldEditRebasesOnExternalWorkspaceWithoutReusingStaleToken()
        {
            using Fixture fixture = Open(BuildWorkspace(shared: false, motion: "Linear", impact: "DirectHit"));
            Select(fixture.Window, "definition.shared");
            fixture.Window.CommitFieldForTests("motion.speedPerFrame", 4f);
            Assert.AreEqual(1, fixture.Window.SessionForTests.PendingCommands.Count);

            fixture.Service.UseExternalWorkspace = true;
            fixture.Window.InvokeActionForTests("refresh-button");
            Assert.IsTrue(fixture.Window.SessionForTests.HasExternalChanges);
            fixture.Window.InvokeActionForTests("external-compare");
            ProjectileAuthoringMutationRequestView reviewed = fixture.Window.SessionForTests.PendingCommands.Single();
            Assert.AreEqual("workspace-token", reviewed.workspaceToken);
            Assert.AreEqual("motion.speedPerFrame", reviewed.fieldEdits.Single().fieldPath);

            int beforeReapply = fixture.Service.Sequences.Count;
            fixture.Window.InvokeActionForTests("external-reapply");
            Assert.AreEqual(beforeReapply + 2, fixture.Service.Sequences.Count, "Non-shared rebase must plan without the old token, then commit with the latest token.");
            ProjectileAuthoringMutationRequestView planned = fixture.Service.Sequences[beforeReapply].Single();
            Assert.IsEmpty(planned.workspaceToken);
            Assert.IsFalse(planned.confirmed);
            ProjectileAuthoringMutationRequestView reapplied = fixture.Service.Sequences.Last().Single();
            Assert.AreEqual("external-token", reapplied.workspaceToken);
            Assert.AreEqual("motion.speedPerFrame", reapplied.fieldEdits.Single().fieldPath);
            Assert.AreEqual(4f, reapplied.fieldEdits.Single().floatValue);
            Assert.AreEqual("external-token", fixture.Window.SessionForTests.PendingCommands.Single().workspaceToken);
            Assert.IsFalse(fixture.Window.SessionForTests.HasExternalChanges);
        }

        [Test]
        public void M3_E17_SharedFieldEditRequiresFreshExternalImpactConfirmation()
        {
            using Fixture fixture = Open(BuildWorkspace(shared: true, motion: "Linear", impact: "DirectHit"));
            Select(fixture.Window, "definition.shared");
            fixture.Window.CommitFieldForTests("motion.speedPerFrame", 6f);
            fixture.Window.InvokeActionForTests("reference-impact-confirm");
            Assert.AreEqual("workspace-token", fixture.Window.SessionForTests.PendingCommands.Single().workspaceToken);

            fixture.Service.UseExternalWorkspace = true;
            fixture.Window.InvokeActionForTests("refresh-button");
            int beforeReapply = fixture.Service.Sequences.Count;
            fixture.Window.InvokeActionForTests("external-reapply");
            Assert.AreEqual(beforeReapply + 1, fixture.Service.Sequences.Count, "Shared rebase must pause after its fresh plan.");
            ProjectileAuthoringMutationRequestView replanned = fixture.Service.Sequences.Last().Single();
            Assert.IsEmpty(replanned.workspaceToken);
            Assert.IsFalse(replanned.confirmed);
            Assert.AreEqual(DisplayStyle.Flex, fixture.Window.rootVisualElement.Q("reference-impact-panel").resolvedStyle.display);
            Assert.IsTrue(fixture.Window.SessionForTests.HasExternalChanges, "Original draft stays protected until fresh impact is confirmed.");

            fixture.Window.InvokeActionForTests("reference-impact-confirm");
            ProjectileAuthoringMutationRequestView confirmed = fixture.Service.Sequences.Last().Single();
            Assert.AreEqual("external-token", confirmed.workspaceToken);
            Assert.AreEqual("confirmation-external-token", confirmed.confirmationToken);
            Assert.IsTrue(confirmed.confirmed);
            Assert.AreEqual("motion.speedPerFrame", confirmed.fieldEdits.Single().fieldPath);
            Assert.IsFalse(fixture.Window.SessionForTests.HasExternalChanges);
        }

        [Test]
        public void M3_E18_FieldEditSequenceRestoresAfterWindowReopen()
        {
            SessionState.EraseString("MxFramework.ProjectileAuthoring.State");
            ProjectileAuthoringWindow first = ScriptableObject.CreateInstance<ProjectileAuthoringWindow>();
            var firstService = new RuleService(BuildWorkspace(shared: false, motion: "Linear", impact: "DirectHit"));
            first.SetServiceForTests(firstService);
            first.CreateGUI();
            ProjectileAuthoringWindow restored = null;
            try
            {
                Select(first, "definition.shared");
                first.CommitFieldForTests("motion.speedPerFrame", 5f);
                first.InvokeLifecycleForTests("before-reload");
                UnityEngine.Object.DestroyImmediate(first);
                first = null;

                restored = ScriptableObject.CreateInstance<ProjectileAuthoringWindow>();
                var restoredService = new RuleService(BuildWorkspace(shared: false, motion: "Linear", impact: "DirectHit"));
                restored.SetServiceForTests(restoredService);
                restored.CreateGUI();
                ProjectileAuthoringMutationRequestView command = restored.SessionForTests.PendingCommands.Single();
                Assert.AreEqual("EditDefinitionFields", command.kind);
                Assert.AreEqual("workspace-token", command.workspaceToken);
                Assert.AreEqual("motion.speedPerFrame", command.fieldEdits.Single().fieldPath);
                Assert.AreEqual(5f, command.fieldEdits.Single().floatValue);
                Assert.AreEqual("motion.speedPerFrame", restoredService.Sequences.Last().Single().fieldEdits.Single().fieldPath);
            }
            finally
            {
                if (first != null)
                {
                    first.DiscardChanges();
                    UnityEngine.Object.DestroyImmediate(first);
                }
                if (restored != null)
                {
                    restored.DiscardChanges();
                    UnityEngine.Object.DestroyImmediate(restored);
                }
                SessionState.EraseString("MxFramework.ProjectileAuthoring.State");
            }
        }

        [Test]
        public void RequestPreviewRestoresAfterDomainReload()
        {
            SessionState.EraseString("MxFramework.ProjectileAuthoring.State");
            ProjectileAuthoringWindow first = ScriptableObject.CreateInstance<ProjectileAuthoringWindow>();
            first.SetServiceForTests(new RuleService(BuildWorkspace(shared: false, motion: "Linear", impact: "DirectHit")));
            first.CreateGUI();
            ProjectileAuthoringWindow restored = null;
            try
            {
                Select(first, "request.one");
                Assert.That(first.CurrentIsRequest, Is.True);
                Assert.That(first.PreviewFrameCountForTests, Is.GreaterThan(0));

                first.InvokeLifecycleForTests("before-reload");
                Assert.That(first.PreviewDisposedForTests, Is.True);
                UnityEngine.Object.DestroyImmediate(first);
                first = null;

                restored = ScriptableObject.CreateInstance<ProjectileAuthoringWindow>();
                restored.SetServiceForTests(new RuleService(BuildWorkspace(shared: false, motion: "Linear", impact: "DirectHit")));
                restored.CreateGUI();

                Assert.That(restored.SessionForTests.SelectedStableId, Is.EqualTo("request.one"));
                Assert.That(restored.CurrentIsRequest, Is.True);
                Assert.That(restored.PreviewDisposedForTests, Is.False);
                Assert.That(restored.PreviewFrameCountForTests, Is.GreaterThan(0));
                Assert.That(restored.rootVisualElement.Q<Label>("projectile-preview-state").text, Is.Not.EqualTo("No preview"));
            }
            finally
            {
                if (first != null)
                {
                    first.DiscardChanges();
                    UnityEngine.Object.DestroyImmediate(first);
                }
                if (restored != null)
                {
                    restored.DiscardChanges();
                    UnityEngine.Object.DestroyImmediate(restored);
                }
                SessionState.EraseString("MxFramework.ProjectileAuthoring.State");
            }
        }

        private static Fixture Open(ProjectileAuthoringWorkspaceView workspace)
        {
            SessionState.EraseString("MxFramework.ProjectileAuthoring.State");
            ProjectileAuthoringWindow window = ScriptableObject.CreateInstance<ProjectileAuthoringWindow>();
            var service = new RuleService(workspace);
            window.SetServiceForTests(service);
            window.CreateGUI();
            return new Fixture(window, service);
        }

        private static void Select(ProjectileAuthoringWindow window, string stableId)
        {
            int index = window.VisibleStableIdsForTests.ToList().IndexOf(stableId);
            Assert.GreaterOrEqual(index, 0);
            window.rootVisualElement.Q<ListView>("definition-request-tree").SetSelection(index);
        }

        private static void AssertCompoundMotionFields(ProjectileAuthoringFieldEditView[] edits, string kind)
        {
            string[] paths = edits.Select(value => value.fieldPath).ToArray();
            switch (kind)
            {
                case "ArcToTarget": CollectionAssert.Contains(paths, "motion.arcHeight"); break;
                case "SwayForward": CollectionAssert.Contains(paths, "motion.lateralAmplitude"); CollectionAssert.Contains(paths, "motion.lateralCycles"); break;
                case "HomingToTarget": CollectionAssert.Contains(paths, "motion.homing.turnRatePerFrame"); break;
                case "BezierToTarget": CollectionAssert.Contains(paths, "motion.bezierControlOffsetA"); CollectionAssert.Contains(paths, "motion.bezierControlOffsetB"); break;
                case "BallisticArcToTarget": CollectionAssert.Contains(paths, "motion.gravityPerFrameSquared"); CollectionAssert.Contains(paths, "motion.maxLaunchAngleDegrees"); break;
            }
        }

        private static void AssertMotionConditionalControl(ProjectileAuthoringWindow window, string kind)
        {
            switch (kind)
            {
                case "ArcToTarget": Assert.NotNull(window.rootVisualElement.Q("motion-arc-height")); break;
                case "SwayForward": Assert.NotNull(window.rootVisualElement.Q("motion-lateral-amplitude")); break;
                case "HomingToTarget": Assert.NotNull(window.rootVisualElement.Q("motion-homing-turn-rate")); break;
                case "BezierToTarget": Assert.NotNull(window.rootVisualElement.Q("motion-bezier-a")); break;
                case "BallisticArcToTarget": Assert.NotNull(window.rootVisualElement.Q("motion-gravity")); break;
            }
        }

        private static ProjectileAuthoringWorkspaceView BuildWorkspace(bool shared, string motion, string impact)
        {
            string[] requestIds = shared ? new[] { "request.one", "request.two" } : new[] { "request.one" };
            var hit = new CombatHitEventView
            {
                targetTypes = new[] { "HostileCharacter", "WorldWall" }, environmentHitPolicy = "BlockedPresentation", stableEventId = "hit.event", maxAcceptedHits = 2, hitSameTargetPolicy = "AllowRepeat",
                damage = new CombatHitDamageView { damageTypeId = "Default", baseDamage = 3, tags = new[] { "fire", "burst" }, analysisCategory = "Impact" },
                onHitBuffs = new[] { new CombatHitBuffView { buffId = 7 }, new CombatHitBuffView { buffId = 8 } }
            };
            var detail = new ProjectileDefinitionDetailView
            {
                definitionId = 101, stableId = "definition.shared", physicalEventsEnabled = true,
                motion = new ProjectileMotionView { kind = motion, speedPerFrame = 1, lifetimeFrames = 30, gravityPerFrameSquared = .1f, maxLaunchAngleDegrees = 45, maxLaunchAngleTangent = 1, bezierControlOffsetA = new ProjectileVector3View { y = 1 }, bezierControlOffsetB = new ProjectileVector3View { y = 2 }, speedProfile = new ProjectileSpeedProfileView { kind = "Constant" }, homing = new ProjectileHomingView { turnRatePerFrame = .5f } },
                collision = new ProjectileCollisionView { radius = .2f, layerMask = 256, maxAcceptedImpacts = 2, stopOnFirstAcceptedImpact = true, hitSameTargetPolicy = "AllowRepeat" },
                impact = new ProjectileImpactView { policyKind = impact, areaRadius = 2, areaLayerMask = 258, triggerOnExpire = true, effectTimelineId = "timeline.hit", presentationProfileId = "legacy.profile", hitEvent = hit, areaHitEvent = hit }
            };
            var requests = requestIds.Select((id, index) => new ProjectileRequestVariantListItem
            {
                stableId = id, definitionStableId = "definition.shared", sourcePath = "config/projectiles/" + id + ".json",
                detail = new ProjectileRequestDetailView { stableId = id, projectileStableId = "definition.shared", origin = new ProjectileSpawnOriginView { kind = "Socket", socketId = index == 0 ? "hand_r" : "root", localOffset = new ProjectileVector3View(), fallbackRootOffset = new ProjectileVector3View { y = 1 } }, aim = new ProjectileAimView { kind = "LockedTargetCenter", fallbackKind = "CasterForwardRange", maxRange = 20, targetHeightOffset = 1 } }
            }).ToArray();
            return new ProjectileAuthoringWorkspaceView
            {
                workspaceHash = "workspace-token", knownSocketIds = new[] { "root", "hand_r" }, definitionCount = 1, requestCount = requests.Length,
                sources = requests.Select(value => new ProjectileAuthoringSourceView { stableId = value.stableId, relativePath = value.sourcePath, expectedSha256 = "hash" }).Concat(new[] { new ProjectileAuthoringSourceView { stableId = "definition.shared", relativePath = "config/projectiles/definition.shared.json", expectedSha256 = "hash" } }).ToArray(),
                definitions = new[] { new ProjectileDefinitionListItem { stableId = "definition.shared", definitionId = 101, motion = motion, impact = impact, requestIds = requestIds, sourcePaths = new[] { "config/projectiles/definition.shared.json" }, shared = shared, detail = detail } },
                requests = requests,
                bindings = new[] { new ProjectilePresentationBindingListItem { projectileDefinitionId = 101, definitionStableId = "definition.shared", sourcePath = "config/projectiles/definition.shared.json", detail = new ProjectilePresentationBindingView { projectileDefinitionId = 101, viewNamePrefix = "ProjectileVFX", slots = new ProjectilePresentationSlotsView { flightBody = new ProjectilePresentationSlotView { resource = new ProjectileResourceKeyView { id = "vfx.test.flight", typeId = "GameObject", packageId = "test" }, orientation = "ProjectileForward", localOffset = new ProjectileVector3View() } }, exitPolicy = new ProjectilePresentationExitPolicyView { acceptedImpact = "Complete", blockedImpact = "Dissipate", expiration = "Complete", externalRemoval = "Dissipate", runtimeReset = "Immediate" } } } },
                diagnostics = Array.Empty<ProjectileAuthoringProblemView>(), generatedOnly = Array.Empty<ProjectileGeneratedOnlyListItem>()
            };
        }

        private sealed class Fixture : IDisposable
        {
            public Fixture(ProjectileAuthoringWindow window, RuleService service) { Window = window; Service = service; }
            public ProjectileAuthoringWindow Window { get; }
            public RuleService Service { get; }
            public void Dispose() { Window.DiscardChanges(); UnityEngine.Object.DestroyImmediate(Window); }
        }

        private sealed class RuleService : IProjectileAuthoringService
        {
            private readonly ProjectileAuthoringWorkspaceView workspace;
            private readonly ProjectileAuthoringWorkspaceView externalWorkspace;
            public RuleService(ProjectileAuthoringWorkspaceView workspace)
            {
                this.workspace = workspace;
                externalWorkspace = JsonUtility.FromJson<ProjectileAuthoringWorkspaceView>(JsonUtility.ToJson(workspace));
                externalWorkspace.workspaceHash = "external-token";
                foreach (ProjectileAuthoringSourceView source in externalWorkspace.sources ?? Array.Empty<ProjectileAuthoringSourceView>())
                    source.expectedSha256 = "external-" + (source.expectedSha256 ?? string.Empty);
            }
            public List<ProjectileAuthoringMutationRequestView[]> Sequences { get; } = new List<ProjectileAuthoringMutationRequestView[]>();
            public bool UseExternalWorkspace { get; set; }
            public bool IsBusy => false;
            public void LoadWorkspace(string packagePath, Action<ProjectileAuthoringWorkspaceView, string> completed) => completed(UseExternalWorkspace ? externalWorkspace : workspace, null);
            public void Validate(string packagePath, Action<ProjectileAuthoringProblemView[], string> completed) => completed(workspace.diagnostics, null);
            public void Mutate(string packagePath, ProjectileAuthoringMutationRequestView request, Action<ProjectileAuthoringMutationResultView, string, string> completed) => MutateSequence(packagePath, new[] { request }, completed);
            public void MutateSequence(string packagePath, ProjectileAuthoringMutationRequestView[] commands, Action<ProjectileAuthoringMutationResultView, string, string> completed)
            {
                Sequences.Add(commands);
                ProjectileAuthoringMutationRequestView last = commands.Last();
                ProjectileAuthoringWorkspaceView current = UseExternalWorkspace ? externalWorkspace : workspace;
                ProjectileDefinitionListItem definition = current.definitions.Single(value => value.stableId == (last.definitionStableId ?? last.sourceStableId));
                bool presentation = last.kind == "SetPresentationSlot" || last.kind == "ClearPresentationSlot" || last.kind == "EditPresentationExitPolicy" || last.kind == "MigratePresentationBindingV1ToV2";
                bool shared = definition.shared && (last.kind == "EditDefinitionFields" || presentation);
                bool invalidLockedFallback = last.kind == "EditRequestFields"
                    && last.fieldEdits?.Any(value => value.fieldPath == "aim.fallbackKind" && value.stringValue != "CasterForwardRange") == true
                    && current.requests.First(value => value.stableId == last.sourceStableId).detail.aim.kind != "CasterForwardRange";
                bool staleToken = (last.kind == "EditDefinitionFields" || last.kind == "EditRequestFields" || presentation)
                    && !string.IsNullOrEmpty(last.workspaceToken)
                    && last.workspaceToken != current.workspaceHash;
                string confirmationToken = "confirmation-" + current.workspaceHash;
                bool staleSharedConfirmation = shared && !last.planOnly && (!last.confirmed || last.confirmationToken != confirmationToken);
                string compoundError = ValidateCompoundTypeChange(last);
                ProjectileAuthoringProblemView[] diagnostics = invalidLockedFallback
                    ? new[] { new ProjectileAuthoringProblemView { code = "PROJAUTH028", severity = "Error", message = "Locked target aim requires CasterForwardRange fallback.", sourcePath = current.requests.First(value => value.stableId == last.sourceStableId).sourcePath, fieldPath = "aim.fallbackKind" } }
                    : !string.IsNullOrEmpty(compoundError)
                        ? new[] { new ProjectileAuthoringProblemView { code = "PROJAUTH028", severity = "Error", message = compoundError, sourcePath = "config/projectiles/definition.shared.json", objectPath = last.sourceStableId, relatedStableIds = new[] { last.sourceStableId }, fieldPath = last.fieldEdits?.FirstOrDefault()?.fieldPath } }
                    : staleToken || staleSharedConfirmation
                        ? new[] { new ProjectileAuthoringProblemView { code = "PROJAUTH029", severity = "Error", message = "The workspace or shared confirmation token is stale.", sourcePath = "config/projectiles/definition.shared.json", fieldPath = last.fieldEdits?.FirstOrDefault()?.fieldPath } }
                        : Array.Empty<ProjectileAuthoringProblemView>();
                bool blocked = invalidLockedFallback || !string.IsNullOrEmpty(compoundError) || staleToken || staleSharedConfirmation;
                string[] fieldPaths = presentation ? new[] { last.kind == "EditPresentationExitPolicy" ? "presentationBinding.exitPolicy." + last.presentationExitKind : last.kind == "MigratePresentationBindingV1ToV2" ? "presentationBinding" : "presentationBinding.slots." + last.presentationSlotKind } : last.fieldEdits?.Select(value => value.fieldPath).ToArray();
                var impact = new ProjectileAuthoringReferenceImpactView { stableId = definition.stableId, requestStableIds = definition.requestIds, fieldPaths = fieldPaths, workspaceToken = current.workspaceHash, confirmationToken = confirmationToken, summary = shared ? "Shared definition confirmation required" : "Field edit ready", blocking = blocked || (shared && last.planOnly), blockingReasons = blocked ? new[] { staleToken || staleSharedConfirmation ? "stale-token" : "invalid-field-combination" } : shared && last.planOnly ? new[] { "shared-definition-confirmation-required" } : Array.Empty<string>() };
                ProjectileAuthoringWorkspaceView projected = blocked ? current : ProjectFieldChanges(current, last);
                completed(new ProjectileAuthoringMutationResultView { status = blocked ? "Blocked" : "Ready", diagnostics = diagnostics, impact = impact, projectedWorkspace = projected, dirtySources = last.planOnly ? Array.Empty<ProjectileAuthoringDirtySourceView>() : new[] { new ProjectileAuthoringDirtySourceView { relativePath = "config/projectiles/definition.shared.json", operation = "Upsert" } } }, last.planOnly ? string.Empty : "draft.json", null);
            }
            public void Apply(string packagePath, string draftPath, Action<ProjectileAuthoringApplyResult, string> completed) => completed(new ProjectileAuthoringApplyResult { status = "Committed" }, null);
            public void Cancel() { }
            public void Dispose() { }

            private static string ValidateCompoundTypeChange(ProjectileAuthoringMutationRequestView command)
            {
                ProjectileAuthoringFieldEditView[] edits = command.fieldEdits ?? Array.Empty<ProjectileAuthoringFieldEditView>();
                ProjectileAuthoringFieldEditView motion = edits.FirstOrDefault(value => value.fieldPath == "motion.kind");
                if (motion != null)
                {
                    switch (motion.stringValue)
                    {
                        case "ArcToTarget": if (FloatValue(edits, "motion.arcHeight") <= 0f) return "ArcToTarget requires an atomic positive arcHeight."; break;
                        case "SwayForward": if (FloatValue(edits, "motion.lateralAmplitude") <= 0f || IntValue(edits, "motion.lateralCycles") <= 0) return "SwayForward requires atomic amplitude and cycles."; break;
                        case "HomingToTarget": float turn = FloatValue(edits, "motion.homing.turnRatePerFrame"); if (turn <= 0f || turn > 1f) return "HomingToTarget requires an atomic turn rate."; break;
                        case "BezierToTarget": if (VectorZero(edits, "motion.bezierControlOffsetA") && VectorZero(edits, "motion.bezierControlOffsetB")) return "BezierToTarget requires atomic non-zero controls."; break;
                        case "BallisticArcToTarget": float angle = FloatValue(edits, "motion.maxLaunchAngleDegrees"); if (FloatValue(edits, "motion.gravityPerFrameSquared") <= 0f || angle <= 0f || angle >= 90f) return "BallisticArcToTarget requires atomic gravity and angle."; break;
                    }
                }
                ProjectileAuthoringFieldEditView impact = edits.FirstOrDefault(value => value.fieldPath == "impact.policyKind");
                if (impact?.stringValue == "AreaPulseOnImpact")
                {
                    if (FloatValue(edits, "impact.areaRadius") <= 0f
                        || (edits.FirstOrDefault(value => value.fieldPath == "impact.areaHitEvent.targetTypes")?.stringValues?.Length ?? 0) == 0
                        || string.IsNullOrWhiteSpace(edits.FirstOrDefault(value => value.fieldPath == "impact.areaHitEvent.damage.damageTypeId")?.stringValue)
                        || IntValue(edits, "impact.areaHitEvent.maxAcceptedHits") <= 0)
                        return "AreaPulseOnImpact requires one atomic, complete area hit event batch.";
                }
                return string.Empty;
            }

            private static ProjectileAuthoringWorkspaceView ProjectFieldChanges(ProjectileAuthoringWorkspaceView source, ProjectileAuthoringMutationRequestView command)
            {
                ProjectileAuthoringWorkspaceView projected = JsonUtility.FromJson<ProjectileAuthoringWorkspaceView>(JsonUtility.ToJson(source));
                if (command.kind != "EditDefinitionFields") return projected;
                ProjectileDefinitionListItem definition = projected.definitions.First(value => value.stableId == command.sourceStableId);
                ProjectileAuthoringFieldEditView[] edits = command.fieldEdits ?? Array.Empty<ProjectileAuthoringFieldEditView>();
                ProjectileAuthoringFieldEditView motionKind = edits.FirstOrDefault(value => value.fieldPath == "motion.kind");
                if (motionKind != null)
                {
                    definition.motion = motionKind.stringValue;
                    definition.detail.motion.kind = motionKind.stringValue;
                    ApplyMotionValues(definition.detail.motion, edits);
                }
                ProjectileAuthoringFieldEditView impactKind = edits.FirstOrDefault(value => value.fieldPath == "impact.policyKind");
                if (impactKind != null)
                {
                    definition.impact = impactKind.stringValue;
                    definition.detail.impact.policyKind = impactKind.stringValue;
                    if (impactKind.stringValue == "AreaPulseOnImpact") ApplyAreaValues(definition.detail.impact, edits);
                }
                return projected;
            }

            private static void ApplyMotionValues(ProjectileMotionView motion, ProjectileAuthoringFieldEditView[] edits)
            {
                ProjectileAuthoringFieldEditView value;
                if ((value = Find(edits, "motion.arcHeight")) != null) motion.arcHeight = value.floatValue;
                if ((value = Find(edits, "motion.lateralAmplitude")) != null) motion.lateralAmplitude = value.floatValue;
                if ((value = Find(edits, "motion.lateralCycles")) != null) motion.lateralCycles = value.intValue;
                if ((value = Find(edits, "motion.gravityPerFrameSquared")) != null) motion.gravityPerFrameSquared = value.floatValue;
                if ((value = Find(edits, "motion.maxLaunchAngleDegrees")) != null) motion.maxLaunchAngleDegrees = value.floatValue;
                if ((value = Find(edits, "motion.bezierControlOffsetA")) != null) motion.bezierControlOffsetA = value.vectorValue;
                if ((value = Find(edits, "motion.bezierControlOffsetB")) != null) motion.bezierControlOffsetB = value.vectorValue;
                if ((value = Find(edits, "motion.homing.turnRatePerFrame")) != null) motion.homing = new ProjectileHomingView { turnRatePerFrame = value.floatValue };
            }

            private static void ApplyAreaValues(ProjectileImpactView impact, ProjectileAuthoringFieldEditView[] edits)
            {
                impact.areaRadius = FloatValue(edits, "impact.areaRadius");
                impact.triggerOnExpire = Find(edits, "impact.triggerOnExpire")?.boolValue ?? false;
                impact.areaHitEvent ??= JsonUtility.FromJson<CombatHitEventView>(JsonUtility.ToJson(impact.hitEvent ?? new CombatHitEventView()));
                CombatHitEventView area = impact.areaHitEvent;
                area.targetTypes = Find(edits, "impact.areaHitEvent.targetTypes")?.stringValues ?? Array.Empty<string>();
                area.environmentHitPolicy = Find(edits, "impact.areaHitEvent.environmentHitPolicy")?.stringValue;
                area.stableEventId = Find(edits, "impact.areaHitEvent.stableEventId")?.stringValue;
                area.maxAcceptedHits = IntValue(edits, "impact.areaHitEvent.maxAcceptedHits");
                area.hitSameTargetPolicy = Find(edits, "impact.areaHitEvent.hitSameTargetPolicy")?.stringValue;
                area.damage ??= new CombatHitDamageView();
                area.damage.damageTypeId = Find(edits, "impact.areaHitEvent.damage.damageTypeId")?.stringValue;
                area.damage.baseDamage = IntValue(edits, "impact.areaHitEvent.damage.baseDamage");
                area.damage.impactPower = IntValue(edits, "impact.areaHitEvent.damage.impactPower");
                area.damage.launchPower = IntValue(edits, "impact.areaHitEvent.damage.launchPower");
                area.damage.tags = Find(edits, "impact.areaHitEvent.damage.tags")?.stringValues ?? Array.Empty<string>();
                area.damage.analysisCategory = Find(edits, "impact.areaHitEvent.damage.analysisCategory")?.stringValue;
                area.onHitBuffs = (Find(edits, "impact.areaHitEvent.onHitBuffs")?.intValues ?? Array.Empty<int>()).Select(value => new CombatHitBuffView { buffId = value }).ToArray();
            }

            private static ProjectileAuthoringFieldEditView Find(ProjectileAuthoringFieldEditView[] edits, string path) => edits.FirstOrDefault(value => value.fieldPath == path);
            private static float FloatValue(ProjectileAuthoringFieldEditView[] edits, string path) => Find(edits, path)?.floatValue ?? 0f;
            private static int IntValue(ProjectileAuthoringFieldEditView[] edits, string path) => Find(edits, path)?.intValue ?? 0;
            private static bool VectorZero(ProjectileAuthoringFieldEditView[] edits, string path)
            {
                ProjectileVector3View value = Find(edits, path)?.vectorValue;
                return value == null || (Math.Abs(value.x) < .0001f && Math.Abs(value.y) < .0001f && Math.Abs(value.z) < .0001f);
            }
        }

        private sealed class FakeVfxSelectionBridge : IProjectileAuthoringVfxSelectionBridge
        {
            public int FormalSelectionCount { get; private set; }
            public Texture Thumbnail { get; set; }
            public bool Candidate { get; set; } = true;
            public string Status { get; set; } = "Candidate stale";
            public string Review { get; set; } = "ReviewNeeded";
            public string Role { get; set; } = "Flight";
            public string Anchor { get; set; } = "Path";
            public string Lifecycle { get; set; } = "ManualStop";
            public string ActualPrefabPath { get; set; } = string.Empty;
            public ProjectileAuthoringVfxTransientView Resolve(string serializedResourceKey) => new ProjectileAuthoringVfxTransientView
            {
                DisplayName = serializedResourceKey,
                Status = Status,
                ActualPrefabPath = ActualPrefabPath,
                Diagnostic = "transient only",
                Role = Role,
                Anchor = Anchor,
                Space = "Local",
                Lifecycle = Lifecycle,
                Review = Review,
                Thumbnail = Thumbnail,
                IsCandidate = Candidate
            };
            public void SelectFormal(ProjectileResourceKeyView resource) => FormalSelectionCount++;
        }

        private static string Serialize(ProjectileResourceKeyView value) => value.id + ":" + value.typeId + "@" + value.packageId;
    }
}
