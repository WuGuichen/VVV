using System;
using MxFramework.CharacterAction;
using MxFramework.Combat.Hit;
using MxFramework.Core;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterAction
{
    public sealed class CharacterActionJsonImporterTests
    {
        [Test]
        public void ImportActions_Schema010IsRejectedWithoutCompatibilityImport()
        {
            CharacterActionJsonImportResult result = CharacterActionJsonImporter.ImportActions(@"{
  ""schemaVersion"": ""0.1.0"",
  ""actions"": []
}");

            Assert.IsFalse(result.Success);
            Assert.AreEqual(CharacterActionDiagnosticCodes.JsonInvalidDocument, result.Diagnostics[0].Code);
            StringAssert.Contains("0.3.0", result.Diagnostics[0].Message);
        }

        [TestCase("ResetControlScale", true)]
        [TestCase("ResetCollisionProfile", true)]
        [TestCase("StartHitTrace", false)]
        [TestCase("StopHitTrace", false)]
        public void ImportActions_RemovedPairedEventTypesReturnStableBlockingDiagnostic(
            string removedKind,
            bool motionTrack)
        {
            string trackName = motionTrack ? "motionTrack" : "combatTrack";
            string actionsJson = @"{
  ""schemaVersion"": ""0.3.0"",
  ""actions"": [{
    ""actionId"": 1,
    ""category"": ""BasicAttack"",
    ""timelineAuthority"": ""CharacterAuthored"",
    ""durationFrames"": 2,
    ""phases"": [{ ""kind"": ""Active"", ""startFrame"": 0, ""endFrame"": 1 }],
    """ + trackName + @""": { ""events"": [
      { ""frame"": 0, ""kind"": """ + removedKind + @""", ""stableEventId"": ""legacy.event"" }
    ] }
  }]
}";

            CharacterActionJsonImportResult result = CharacterActionJsonImporter.ImportActions(actionsJson);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(CharacterActionDiagnosticCodes.JsonInvalidConfig, result.Diagnostics[0].Code);
            StringAssert.Contains("removed", result.Diagnostics[0].Message);
        }

        [Test]
        public void ImportActions_SetHitTargetStatePreservesTypedMaskAndRange()
        {
            CharacterActionJsonImportResult result = CharacterActionJsonImporter.ImportActions(@"{
  ""schemaVersion"": ""0.3.0"",
  ""actions"": [{
    ""actionId"": 950015,
    ""category"": ""Dodge"",
    ""timelineAuthority"": ""CharacterAuthored"",
    ""durationFrames"": 53,
    ""phases"": [{ ""kind"": ""Active"", ""startFrame"": 0, ""endFrame"": 52 }],
    ""combatTrack"": { ""events"": [{
      ""frame"": 8,
      ""endFrame"": 27,
      ""kind"": ""SetHitTargetState"",
      ""targetStateMask"": 32,
      ""stableEventId"": ""evade.roll.unhittable""
    }] }
  }]
}");

            Assert.IsTrue(result.Success, result.Diagnostics.Length > 0 ? result.Diagnostics[0].Message : string.Empty);
            CombatTrackEvent evt = result.Actions[0].CombatTrack.Events[0];
            Assert.AreEqual(CharacterActionTrackEventKind.SetHitTargetState, evt.Kind);
            Assert.AreEqual(8, evt.Frame);
            Assert.AreEqual(27, evt.EndFrame);
            Assert.AreEqual(HitTargetStateFlags.Unhittable, evt.TargetStateFlags);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(1048576)]
        public void ImportActions_SetHitTargetStateRejectsInvalidMasks(int mask)
        {
            string json = @"{
  ""schemaVersion"": ""0.3.0"",
  ""actions"": [{
    ""actionId"": 1,
    ""category"": ""Dodge"",
    ""timelineAuthority"": ""CharacterAuthored"",
    ""durationFrames"": 2,
    ""phases"": [{ ""kind"": ""Active"", ""startFrame"": 0, ""endFrame"": 1 }],
    ""combatTrack"": { ""events"": [{
      ""frame"": 0, ""endFrame"": 1, ""kind"": ""SetHitTargetState"",
      ""targetStateMask"": " + mask + @", ""stableEventId"": ""invalid.state""
    }] }
  }]
}";

            CharacterActionJsonImportResult result = CharacterActionJsonImporter.ImportActions(json);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(CharacterActionDiagnosticCodes.JsonInvalidConfig, result.Diagnostics[0].Code);
        }

        [Test]
        public void Import_GuideShapeJson_ProducesRuntimeConfigs()
        {
            CharacterActionJsonImportResult result = CharacterActionJsonImporter.Import(ActionSetsJson, ActionsJson);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.ActionSets.Length);
            Assert.AreEqual(1, result.Actions.Length);

            CharacterActionSetConfig set = result.ActionSets[0];
            CharacterActionConfig action = result.Actions[0];

            Assert.AreEqual("iron_vanguard.default", set.StableId);
            Assert.AreEqual(CharacterTestNumericIds.IntentLightAttack, set.CommandBindings[0].IntentId);
            Assert.AreEqual(new CharacterActionId(300), set.CommandBindings[0].ActionId);
            Assert.IsTrue(set.CommandBindings[0].AllowQueue);

            Assert.AreEqual(300, action.ActionId.Value);
            Assert.AreEqual(new CharacterActionId(300), action.ActionId);
            Assert.AreEqual(CharacterActionCategory.BasicAttack, action.Category);
            Assert.AreEqual(CharacterActionTimelineAuthority.CharacterAuthored, action.TimelineAuthority);
            Assert.AreEqual(24, action.DurationFrames);
            Assert.AreEqual(CharacterActionRequirementKind.Grounded, action.Requirements[0].Kind);
            Assert.AreEqual(CharacterActionPhaseKind.Startup, action.Phases[0].Kind);
            Assert.AreEqual(1, action.CancelRules.Length);
            Assert.AreEqual(CharacterActionRequestType.Any, action.CancelRules[0].RequestTypeMask);
            Assert.AreEqual(CharacterCancelInputMask.Any, action.CancelRules[0].CancelInputMask);
            Assert.AreEqual(1, action.InterruptRules.Length);
            Assert.AreEqual(CharacterActionSourceKind.CohesionBreak, action.InterruptRules[0].SourceKind);
            Assert.IsTrue(action.CohesionSupport.Enabled);
            Assert.AreEqual(1.5f, action.CohesionSupport.SupportRate, 0.0001f);
            Assert.AreEqual(1f, action.CohesionSupport.MinSupportRatio, 0.0001f);
            Assert.AreEqual(1, action.MotionTrack.Events.Length);
            Assert.AreEqual(1, action.CombatTrack.Events.Length);
            Assert.AreEqual(1, action.GameplayTrack.Events.Length);
            Assert.AreEqual(1, action.AnimationTrack.Events.Length);
            Assert.AreEqual(1, action.PresentationTrack.Events.Length);
            Assert.AreEqual(8, action.PresentationTrack.Events[0].EndFrame);
            CharacterActionVfxEventConfig vfx = action.PresentationTrack.Events[0].VfxConfig;
            Assert.AreEqual(MxAttachmentMode.FixedWorld, vfx.Attachment.Mode);
            Assert.AreEqual("mainHand", vfx.Attachment.SocketId);
            Assert.AreEqual(MxTransformSpace.World, vfx.Attachment.OffsetSpace);
            Assert.AreEqual(0.25f, vfx.Attachment.Transform.PositionX);
            Assert.AreEqual(0.5f, vfx.Attachment.Transform.PositionY);
            Assert.AreEqual(0.75f, vfx.Attachment.Transform.PositionZ);
            Assert.AreEqual(10f, vfx.Attachment.Transform.RotationEulerX);
            Assert.AreEqual(20f, vfx.Attachment.Transform.RotationEulerY);
            Assert.AreEqual(30f, vfx.Attachment.Transform.RotationEulerZ);
            Assert.AreEqual(1.5f, vfx.Attachment.Transform.ScaleX);
            Assert.AreEqual(2f, vfx.Attachment.Transform.ScaleY);
            Assert.AreEqual(2.5f, vfx.Attachment.Transform.ScaleZ);
            Assert.AreEqual(CharacterActionVfxPlaybackMode.StopAtEventEnd, vfx.PlaybackMode);
            Assert.AreEqual(0.5f, vfx.PlaybackRate);
            Assert.AreEqual(1.25f, vfx.ScaleMultiplier);
            Assert.AreEqual(3, vfx.StartOffsetFrames);
            Assert.AreEqual(4, vfx.EndOffsetFrames);
            Assert.AreEqual(CharacterActionVfxStopPolicy.Dissipate, vfx.StopPolicy);
            Assert.AreEqual(7, vfx.Parameter0);
            Assert.AreEqual(8, vfx.Parameter1);
            Assert.AreEqual(9, vfx.Parameter2);
            Assert.AreEqual(10, vfx.Parameter3);
            Assert.AreEqual(1, action.DebugTrack.Events.Length);
        }

        [Test]
        public void Import_ReactionProfiles_SelectsActionPerActionSetProfile()
        {
            string actionSetsJson = @"{
  ""schemaVersion"": ""0.4.0"",
  ""actionSets"": [
    { ""id"": 1, ""stableId"": ""katana.set"", ""reactionProfileId"": ""reaction.katana"", ""commandBindings"": [], ""abilityBindings"": [], ""reactionBindings"": [] },
    { ""id"": 2, ""stableId"": ""hammer.set"", ""reactionProfileId"": ""reaction.hammer"", ""commandBindings"": [], ""abilityBindings"": [], ""reactionBindings"": [] }
  ]
}";
            string actionsJson = @"{
  ""schemaVersion"": ""0.3.0"",
  ""actions"": [
    { ""actionId"": 10, ""category"": ""Reaction"", ""timelineAuthority"": ""CharacterAuthored"", ""durationFrames"": 18, ""phases"": [{ ""kind"": ""Active"", ""startFrame"": 0, ""endFrame"": 17 }] },
    { ""actionId"": 11, ""category"": ""Reaction"", ""timelineAuthority"": ""CharacterAuthored"", ""durationFrames"": 24, ""phases"": [{ ""kind"": ""Active"", ""startFrame"": 0, ""endFrame"": 23 }] }
  ]
}";
            string reactionProfilesJson = @"{
  ""schemaVersion"": ""0.4.0"",
  ""reactionProfiles"": [
    {
      ""stableId"": ""reaction.katana"",
      ""defaultActionId"": 10,
      ""rules"": [
        { ""actionId"": 10, ""trigger"": ""CohesionBreak"", ""currentScatterPressureBand"": ""Broken"", ""priority"": 10 }
      ]
    },
    {
      ""stableId"": ""reaction.hammer"",
      ""defaultActionId"": 11,
      ""rules"": [
        { ""actionId"": 11, ""trigger"": ""CohesionBreak"", ""currentScatterPressureBand"": ""Broken"", ""priority"": 10 }
      ]
    }
  ]
}";

            CharacterActionJsonImportResult result = CharacterActionJsonImporter.Import(
                actionSetsJson,
                actionsJson,
                reactionProfilesJson,
                "sets",
                "actions",
                "reactionProfiles");
            var registry = new CharacterActionConfigRegistry(
                result.ActionSets,
                result.Actions,
                "sets",
                "actions",
                result.ReactionProfiles,
                "reactionProfiles");
            CharacterActionValidationReport report = CharacterActionValidationReport.Validate(registry);
            var resolver = new CharacterActionResolver();
            CharacterReactionContext reaction = CharacterReactionContextBuilder.FromCohesionBreak(
                new CohesionBreakEvent(
                    new RuntimeFrame(5),
                    new GameplayEntityId(10, 1),
                    PressureBand.Critical,
                    90,
                    100,
                    100,
                    10,
                    traceId: CharacterTestRuntimeTraceIds.Break)).Context;

            Assert.IsTrue(result.Success, FirstImportDiagnostic(result));
            Assert.IsTrue(report.Success, FirstValidationDiagnostic(report));
            Assert.AreEqual(2, result.ReactionProfiles.Length);
            Assert.IsTrue(registry.TryGetActionSet("katana.set", out CharacterActionSetConfig katana));
            Assert.IsTrue(registry.TryGetActionSet("hammer.set", out CharacterActionSetConfig hammer));

            CharacterActionResolveResult katanaResolve = resolver.ResolveReaction(
                new CharacterActionResolverContext(katana, registry.Actions, registry.ReactionProfiles),
                reaction);
            CharacterActionResolveResult hammerResolve = resolver.ResolveReaction(
                new CharacterActionResolverContext(hammer, registry.Actions, registry.ReactionProfiles),
                reaction);

            Assert.IsTrue(katanaResolve.IsSuccess, katanaResolve.RejectReason.ToString());
            Assert.IsTrue(hammerResolve.IsSuccess, hammerResolve.RejectReason.ToString());
            Assert.AreEqual(new CharacterActionId(10), katanaResolve.Plan.ActionId);
            Assert.AreEqual(new CharacterActionId(11), hammerResolve.Plan.ActionId);
        }

        [Test]
        public void Import_ControlScaleMotionEvents_ReadsExplicitAndDefaultScales()
        {
            string actionSetsJson = @"{
  ""schemaVersion"": ""0.4.0"",
  ""actionSets"": []
}";
            string actionsJson = @"{
  ""schemaVersion"": ""0.3.0"",
  ""actions"": [
    {
      ""actionId"": 1,
      ""category"": ""BasicAttack"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""durationFrames"": 4,
      ""phases"": [{ ""kind"": ""Startup"", ""startFrame"": 0, ""endFrame"": 3 }],
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""events"": [
          { ""frame"": 0, ""endFrame"": 1, ""kind"": ""SetControlScale"", ""movementControlScale"": 0.25, ""rotationControlScale"": 0.5, ""stableEventId"": ""attack.control"" },
          { ""frame"": 2, ""endFrame"": 3, ""kind"": ""SetControlScale"", ""movementControlScale"": 1.0, ""rotationControlScale"": 1.0, ""stableEventId"": ""attack.default"" }
        ]
      }
    }
  ]
}";

            CharacterActionJsonImportResult result = CharacterActionJsonImporter.Import(actionSetsJson, actionsJson);

            Assert.IsTrue(result.Success);
            MotionTrackEvent set = result.Actions[0].MotionTrack.Events[0];
            MotionTrackEvent defaultScale = result.Actions[0].MotionTrack.Events[1];
            Assert.AreEqual(CharacterActionTrackEventKind.SetControlScale, set.Kind);
            Assert.AreEqual(0.25f, set.MovementControlScale, 0.0001f);
            Assert.AreEqual(0.5f, set.RotationControlScale, 0.0001f);
            Assert.AreEqual(CharacterActionTrackEventKind.SetControlScale, defaultScale.Kind);
            Assert.AreEqual(1f, defaultScale.MovementControlScale, 0.0001f);
            Assert.AreEqual(1f, defaultScale.RotationControlScale, 0.0001f);
        }

        [Test]
        public void Import_CollisionProfileMotionEvents_ReadsAuthorityFields()
        {
            string actionSetsJson = @"{
  ""schemaVersion"": ""0.4.0"",
  ""actionSets"": []
}";
            string actionsJson = @"{
  ""schemaVersion"": ""0.3.0"",
  ""actions"": [
    {
      ""actionId"": 1,
      ""category"": ""Reaction"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""durationFrames"": 60,
      ""phases"": [{ ""kind"": ""Active"", ""startFrame"": 0, ""endFrame"": 59 }],
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""events"": [
          { ""frame"": 24, ""endFrame"": 59, ""kind"": ""SetCollisionProfile"", ""collisionProfileId"": ""corpse"", ""collisionRestorePolicy"": ""None"", ""collisionTransitionFrames"": 0, ""stableEventId"": ""death.collision_corpse"" }
        ]
      }
    }
  ]
}";

            CharacterActionJsonImportResult result = CharacterActionJsonImporter.Import(actionSetsJson, actionsJson);

            Assert.IsTrue(result.Success, FirstImportDiagnostic(result));
            MotionTrackEvent set = result.Actions[0].MotionTrack.Events[0];
            Assert.AreEqual(CharacterActionTrackEventKind.SetCollisionProfile, set.Kind);
            Assert.AreEqual("corpse", set.CollisionProfileId);
            Assert.AreEqual(CharacterActionCollisionProfileRestorePolicy.None, set.CollisionRestorePolicy);
            Assert.AreEqual(0, set.CollisionTransitionFrames);
            Assert.AreEqual(CharacterTestNumericIds.Field_death_collision_corpse, set.StableEventId);
            Assert.AreEqual(59, set.EndFrame);
        }

        [Test]
        public void Import_RootMotionObstaclePolicy_DefaultsAndReadsStaticOnly()
        {
            string actionSetsJson = @"{
  ""schemaVersion"": ""0.4.0"",
  ""actionSets"": []
}";
            string actionsJson = @"{
  ""schemaVersion"": ""0.3.0"",
  ""actions"": [
    {
      ""actionId"": 1,
      ""category"": ""Skill"",
      ""durationFrames"": 4,
      ""motionTrack"": {
        ""usesRootMotion"": true,
        ""rootMotion"": {
          ""space"": ""Local"",
          ""sampleTickRate"": 60,
          ""frames"": [{ ""frame"": 1, ""z"": 0.1 }]
        }
      }
    },
    {
      ""actionId"": 2,
      ""category"": ""Skill"",
      ""durationFrames"": 4,
      ""motionTrack"": {
        ""usesRootMotion"": true,
        ""rootMotion"": {
          ""space"": ""Local"",
          ""sampleTickRate"": 60,
          ""obstaclePolicy"": ""StaticOnly"",
          ""frames"": [{ ""frame"": 1, ""z"": 0.1 }]
        }
      }
    }
  ]
}";

            CharacterActionJsonImportResult result = CharacterActionJsonImporter.Import(actionSetsJson, actionsJson);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(
                CharacterActionRootMotionObstaclePolicy.Default,
                result.Actions[0].MotionTrack.RootMotion.ObstaclePolicy);
            Assert.AreEqual(
                CharacterActionRootMotionObstaclePolicy.StaticOnly,
                result.Actions[1].MotionTrack.RootMotion.ObstaclePolicy);
        }

        [Test]
        public void Import_SampleRootMotionY_UsesOnlyExplicitVerticalRange()
        {
            string actionSetsJson = @"{ ""schemaVersion"": ""0.6.0"", ""actionSets"": [] }";
            string actionsJson = @"{
  ""schemaVersion"": ""0.5.0"",
  ""actions"": [{
    ""actionId"": 9101,
    ""category"": ""Skill"",
    ""durationFrames"": 4,
    ""motionTrack"": {
      ""usesRootMotion"": false,
      ""rootMotion"": null,
      ""verticalRootMotion"": {
        ""frames"": [
          { ""frame"": 1, ""y"": 0.0 },
          { ""frame"": 2, ""y"": 0.15 }
        ]
      },
      ""events"": [
        { ""frame"": 1, ""endFrame"": 2, ""kind"": ""SampleRootMotionY"", ""stableEventId"": 91001, ""stableEventOrder"": 1 }
      ]
    }
  }]
}";

            CharacterActionJsonImportResult result = CharacterActionJsonImporter.Import(actionSetsJson, actionsJson);

            Assert.IsTrue(result.Success, FirstImportDiagnostic(result));
            Assert.IsFalse(result.Actions[0].MotionTrack.IsVerticalRootMotionActive(0));
            Assert.IsTrue(result.Actions[0].MotionTrack.TryGetVerticalRootMotionDelta(2, out float y));
            Assert.AreEqual(0.15f, y);
            Assert.IsFalse(result.Actions[0].MotionTrack.TryGetVerticalRootMotionDelta(3, out _));
        }

        [Test]
        public void Import_HitVolume_MissingSourceKindDefaultsToManualCapsule()
        {
            string actionSetsJson = @"{
  ""schemaVersion"": ""0.4.0"",
  ""actionSets"": []
}";
            string actionsJson = @"{
  ""schemaVersion"": ""0.3.0"",
  ""actions"": [
    {
      ""actionId"": 1,
      ""category"": ""Skill"",
      ""durationFrames"": 8,
      ""combatTrack"": {
        ""events"": [
          {
            ""frame"": 1,
            ""endFrame"": 1,
            ""kind"": ""HitTraceWindow"",
            ""stableEventId"": ""manual.hit"",
            ""hitEvent"": {
              ""damage"": { ""damageTypeId"": ""slash"", ""baseDamage"": 1 }
            },
            ""hitVolume"": {
              ""shapeKind"": ""Capsule"",
              ""localPointA"": { ""y"": 0.8, ""z"": 0.6 },
              ""localPointB"": { ""y"": 0.8, ""z"": 1.8 },
              ""radius"": 0.65
            }
          }
        ]
      }
    }
  ]
}";

            CharacterActionJsonImportResult result = CharacterActionJsonImporter.Import(actionSetsJson, actionsJson);

            Assert.IsTrue(result.Success);
            CharacterActionHitVolumeConfig hitVolume = result.Actions[0].CombatTrack.Events[0].HitVolume;
            Assert.IsTrue(hitVolume.HasVolume);
            Assert.AreEqual(CharacterActionHitVolumeSourceKind.ManualCapsule, hitVolume.SourceKind);
            Assert.AreEqual(CharacterActionHitVolumeShapeKind.Capsule, hitVolume.ShapeKind);
            Assert.AreEqual(0.8f, hitVolume.LocalPointAY, 0.0001f);
            Assert.AreEqual(0.65f, hitVolume.Radius, 0.0001f);
        }

        [Test]
        public void Import_HitVolume_CasterBodyDoesNotRequireManualCapsuleFields()
        {
            string actionSetsJson = @"{
  ""schemaVersion"": ""0.4.0"",
  ""actionSets"": []
}";
            string actionsJson = @"{
  ""schemaVersion"": ""0.3.0"",
  ""actions"": [
    {
      ""actionId"": 1,
      ""category"": ""Skill"",
      ""durationFrames"": 8,
      ""combatTrack"": {
        ""events"": [
          {
            ""frame"": 1,
            ""endFrame"": 1,
            ""kind"": ""HitTraceWindow"",
            ""stableEventId"": ""body.hit"",
            ""hitEvent"": {
              ""damage"": { ""damageTypeId"": ""slash"", ""baseDamage"": 1 }
            },
            ""hitVolume"": { ""sourceKind"": ""CasterBody"" }
          }
        ]
      }
    }
  ]
}";

            CharacterActionJsonImportResult result = CharacterActionJsonImporter.Import(actionSetsJson, actionsJson);

            Assert.IsTrue(result.Success);
            CharacterActionHitVolumeConfig hitVolume = result.Actions[0].CombatTrack.Events[0].HitVolume;
            Assert.IsTrue(hitVolume.HasVolume);
            Assert.AreEqual(CharacterActionHitVolumeSourceKind.CasterBody, hitVolume.SourceKind);
            Assert.AreEqual(CharacterActionHitVolumeShapeKind.Capsule, hitVolume.ShapeKind);
            Assert.AreEqual(0f, hitVolume.Radius, 0.0001f);
            Assert.AreEqual(0f, hitVolume.BodyOffsetX, 0.0001f);
            Assert.AreEqual(0f, hitVolume.BodyOffsetY, 0.0001f);
            Assert.AreEqual(0f, hitVolume.BodyOffsetZ, 0.0001f);
            Assert.AreEqual(1f, hitVolume.BodyRadiusScale, 0.0001f);
            Assert.AreEqual(0f, hitVolume.BodyRadiusAdd, 0.0001f);
            Assert.AreEqual(1f, hitVolume.BodyHeightScale, 0.0001f);
            Assert.AreEqual(0f, hitVolume.BodyHeightAdd, 0.0001f);
            Assert.AreEqual(0, result.Diagnostics.Length);
        }

        [Test]
        public void Import_HitVolume_CasterBodyReadsBodyModifiers()
        {
            string actionSetsJson = @"{
  ""schemaVersion"": ""0.4.0"",
  ""actionSets"": []
}";
            string actionsJson = @"{
  ""schemaVersion"": ""0.3.0"",
  ""actions"": [
    {
      ""actionId"": 1,
      ""category"": ""Skill"",
      ""durationFrames"": 8,
      ""combatTrack"": {
        ""events"": [
          {
            ""frame"": 1,
            ""endFrame"": 1,
            ""kind"": ""HitTraceWindow"",
            ""stableEventId"": ""body.hit"",
            ""hitEvent"": {
              ""damage"": { ""damageTypeId"": ""slash"", ""baseDamage"": 1 }
            },
            ""hitVolume"": {
              ""sourceKind"": ""CasterBody"",
              ""bodyOffset"": { ""x"": 0.1, ""y"": 0.2, ""z"": 0.3 },
              ""bodyRadiusScale"": 1.25,
              ""bodyRadiusAdd"": 0.15,
              ""bodyHeightScale"": 0.8,
              ""bodyHeightAdd"": -0.2
            }
          }
        ]
      }
    }
  ]
}";

            CharacterActionJsonImportResult result = CharacterActionJsonImporter.Import(actionSetsJson, actionsJson);

            Assert.IsTrue(result.Success);
            CharacterActionHitVolumeConfig hitVolume = result.Actions[0].CombatTrack.Events[0].HitVolume;
            Assert.IsTrue(hitVolume.HasVolume);
            Assert.AreEqual(CharacterActionHitVolumeSourceKind.CasterBody, hitVolume.SourceKind);
            Assert.AreEqual(0.1f, hitVolume.BodyOffsetX, 0.0001f);
            Assert.AreEqual(0.2f, hitVolume.BodyOffsetY, 0.0001f);
            Assert.AreEqual(0.3f, hitVolume.BodyOffsetZ, 0.0001f);
            Assert.AreEqual(1.25f, hitVolume.BodyRadiusScale, 0.0001f);
            Assert.AreEqual(0.15f, hitVolume.BodyRadiusAdd, 0.0001f);
            Assert.AreEqual(0.8f, hitVolume.BodyHeightScale, 0.0001f);
            Assert.AreEqual(-0.2f, hitVolume.BodyHeightAdd, 0.0001f);
            Assert.AreEqual(0, result.Diagnostics.Length);
        }

        [Test]
        public void Import_HitVolume_CasterBodyRejectsNonPositiveScale()
        {
            string actionSetsJson = @"{
  ""schemaVersion"": ""0.4.0"",
  ""actionSets"": []
}";
            string actionsJson = @"{
  ""schemaVersion"": ""0.3.0"",
  ""actions"": [
    {
      ""actionId"": 1,
      ""category"": ""Skill"",
      ""durationFrames"": 8,
      ""combatTrack"": {
        ""events"": [
          {
            ""frame"": 1,
            ""endFrame"": 1,
            ""kind"": ""HitTraceWindow"",
            ""stableEventId"": ""body.hit"",
            ""hitEvent"": {
              ""damage"": { ""damageTypeId"": ""slash"", ""baseDamage"": 1 }
            },
            ""hitVolume"": {
              ""sourceKind"": ""CasterBody"",
              ""bodyRadiusScale"": 0
            }
          }
        ]
      }
    }
  ]
}";

            CharacterActionJsonImportResult result = CharacterActionJsonImporter.Import(actionSetsJson, actionsJson);

            Assert.IsTrue(result.Success);
            Assert.IsFalse(result.Actions[0].CombatTrack.Events[0].HitVolume.HasVolume);
            Assert.AreEqual(1, result.Diagnostics.Length);
            Assert.AreEqual(CharacterActionDiagnosticCodes.JsonInvalidConfig, result.Diagnostics[0].Code);
            StringAssert.Contains("bodyRadiusScale", result.Diagnostics[0].JsonPath);
        }

        [Test]
        public void Import_HitVolume_BakedSocketCapsuleReadsSocketIdentity()
        {
            string actionSetsJson = @"{ ""schemaVersion"": ""0.4.0"", ""actionSets"": [] }";
            string actionsJson = @"{
  ""schemaVersion"": ""0.3.0"",
  ""actions"": [{
    ""actionId"": 1,
    ""category"": ""BasicAttack"",
    ""durationFrames"": 8,
    ""combatTrack"": { ""events"": [{
      ""frame"": 1,
      ""endFrame"": 3,
      ""kind"": ""HitTraceWindow"",
      ""stableEventId"": ""socket.hit"",
      ""hitEvent"": { ""damage"": { ""damageTypeId"": ""slash"", ""baseDamage"": 1 } },
      ""hitVolume"": {
        ""sourceKind"": ""BakedSocketCapsule"",
        ""startSocketId"": ""clawStart"",
        ""endSocketId"": ""clawEnd"",
        ""radius"": 0.35
      }
    }] }
  }]
}";

            CharacterActionJsonImportResult result = CharacterActionJsonImporter.Import(actionSetsJson, actionsJson);

            Assert.IsTrue(result.Success);
            CharacterActionHitVolumeConfig hitVolume = result.Actions[0].CombatTrack.Events[0].HitVolume;
            Assert.IsTrue(hitVolume.HasVolume);
            Assert.AreEqual(CharacterActionHitVolumeSourceKind.BakedSocketCapsule, hitVolume.SourceKind);
            Assert.AreEqual("clawStart", hitVolume.StartSocketId);
            Assert.AreEqual("clawEnd", hitVolume.EndSocketId);
            Assert.AreEqual(0.35f, hitVolume.Radius, 0.0001f);
            Assert.AreEqual(0, result.Diagnostics.Length);
        }

        [Test]
        public void Import_HitVolume_BakedSocketCapsuleRejectsWeaponOrManualFallbackFields()
        {
            string actionSetsJson = @"{ ""schemaVersion"": ""0.4.0"", ""actionSets"": [] }";
            string actionsJson = @"{
  ""schemaVersion"": ""0.3.0"",
  ""actions"": [{
    ""actionId"": 1,
    ""category"": ""BasicAttack"",
    ""durationFrames"": 8,
    ""combatTrack"": { ""events"": [{
      ""frame"": 1,
      ""endFrame"": 3,
      ""kind"": ""HitTraceWindow"",
      ""stableEventId"": ""socket.hit"",
      ""equipmentSlotId"": ""mainHand"",
      ""hitEvent"": { ""damage"": { ""damageTypeId"": ""slash"", ""baseDamage"": 1 } },
      ""hitVolume"": {
        ""sourceKind"": ""BakedSocketCapsule"",
        ""startSocketId"": ""clawStart"",
        ""endSocketId"": ""clawEnd"",
        ""localPointA"": { ""z"": 1 },
        ""radius"": 0.35
      }
    }] }
  }]
}";

            CharacterActionJsonImportResult result = CharacterActionJsonImporter.Import(actionSetsJson, actionsJson);

            Assert.IsTrue(result.Success);
            Assert.IsFalse(result.Actions[0].CombatTrack.Events[0].HitVolume.HasVolume);
            Assert.AreEqual(1, result.Diagnostics.Length);
            StringAssert.Contains("cannot include", result.Diagnostics[0].Message);
        }

        [Test]
        public void Import_InlineDamagePayloads_ReadsCombatAndGameplayTracks()
        {
            string actionSetsJson = @"{
  ""schemaVersion"": ""0.4.0"",
  ""actionSets"": []
}";
            string actionsJson = @"{
  ""schemaVersion"": ""0.3.0"",
  ""actions"": [
    {
      ""actionId"": 1,
      ""category"": ""BasicAttack"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""durationFrames"": 8,
      ""phases"": [{ ""kind"": ""Active"", ""startFrame"": 0, ""endFrame"": 7 }],
      ""combatTrack"": {
        ""events"": [
          { ""frame"": 2, ""endFrame"": 2, ""kind"": ""HitTraceWindow"", ""equipmentSlotId"": ""mainHand"", ""stableEventId"": ""attack.hit"", ""hitEvent"": { ""damage"": { ""damageTypeId"": ""slash"", ""formulaId"": ""ct.soft_defense_v1"", ""baseDamage"": 6, ""sourceAttributeScales"": [{ ""attributeId"": 2, ""scale"": 0.65 }], ""impactPower"": 12, ""tags"": [""melee"", ""weapon"", ""scatter""], ""analysisCategory"": ""BasicAttack"" } } }
        ]
      },
      ""gameplayTrack"": {
        ""events"": [
          { ""frame"": 4, ""kind"": ""ApplyGameplayEffect"", ""targetPolicyId"": ""target.locked.area_2m"", ""stableEventId"": ""attack.wave"", ""hitEvent"": { ""damage"": { ""damageTypeId"": ""force"", ""formulaId"": ""Flat"", ""baseDamage"": 6, ""impactPower"": 12, ""tags"": [""area"", ""shockwave"", ""scatter""], ""analysisCategory"": ""AreaPulse"" } } }
        ]
      }
    }
  ]
}";

            CharacterActionJsonImportResult result = CharacterActionJsonImporter.Import(actionSetsJson, actionsJson);

            Assert.IsTrue(result.Success);
            CombatTrackEvent combat = result.Actions[0].CombatTrack.Events[0];
            GameplayTrackEvent gameplay = result.Actions[0].GameplayTrack.Events[0];
            Assert.AreEqual("mainHand", combat.EquipmentSlotId);
            Assert.AreEqual(CharacterTestNumericIds.DamageSlash, combat.DamagePayload.DamageTypeId);
            Assert.AreEqual("ct.soft_defense_v1", combat.DamagePayload.FormulaId);
            Assert.AreEqual(6, combat.DamagePayload.BaseDamage);
            Assert.AreEqual(0.65f, combat.DamagePayload.SourceAttributeScales[0].Scale, 0.0001f);
            Assert.AreEqual(12, combat.DamagePayload.ImpactPower);
            Assert.AreEqual("BasicAttack", combat.DamagePayload.AnalysisCategory);
            Assert.AreEqual(CharacterTestNumericIds.DamageForce, gameplay.DamagePayload.DamageTypeId);
            Assert.AreEqual("Flat", gameplay.DamagePayload.FormulaId);
            Assert.AreEqual(6, gameplay.DamagePayload.BaseDamage);
            Assert.AreEqual(12, gameplay.DamagePayload.ImpactPower);
            Assert.AreEqual("target.locked.area_2m", gameplay.TargetPolicyId);
        }

        [Test]
        public void Import_LegacyTopLevelScatterDamage_Fails()
        {
            string actionSetsJson = @"{
  ""schemaVersion"": ""0.4.0"",
  ""actionSets"": []
}";
            string actionsJson = @"{
  ""schemaVersion"": ""0.3.0"",
  ""actions"": [
    {
      ""actionId"": 1,
      ""category"": ""BasicAttack"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""durationFrames"": 8,
      ""phases"": [{ ""kind"": ""Active"", ""startFrame"": 0, ""endFrame"": 7 }],
      ""combatTrack"": {
        ""events"": [
          { ""frame"": 2, ""endFrame"": 2, ""kind"": ""HitTraceWindow"", ""damageTypeId"": ""slash"", ""formulaId"": ""Flat"", ""baseDamage"": 6, ""scatterDamage"": 99, ""stableEventId"": ""attack.hit"" }
        ]
      },
      ""gameplayTrack"": {
        ""events"": [
          { ""frame"": 4, ""kind"": ""ApplyGameplayEffect"", ""damageTypeId"": ""force"", ""formulaId"": ""Flat"", ""baseDamage"": 6, ""scatterDamage"": 77, ""targetPolicyId"": ""target.locked.area_2m"", ""stableEventId"": ""attack.wave"" }
        ]
      }
    }
  ]
}";

            CharacterActionJsonImportResult result = CharacterActionJsonImporter.Import(actionSetsJson, actionsJson);

            Assert.IsFalse(result.Success);
            StringAssert.Contains("hitEvent.damage", FirstImportDiagnostic(result));
        }

        [Test]
        public void Import_HitEventDamageWithLegacyTopLevelDamage_Fails()
        {
            string actionSetsJson = @"{
  ""schemaVersion"": ""0.4.0"",
  ""actionSets"": []
}";
            string actionsJson = @"{
  ""schemaVersion"": ""0.3.0"",
  ""actions"": [
    {
      ""actionId"": 1,
      ""category"": ""BasicAttack"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""durationFrames"": 8,
      ""phases"": [{ ""kind"": ""Active"", ""startFrame"": 0, ""endFrame"": 7 }],
      ""combatTrack"": {
        ""events"": [
          {
            ""frame"": 2,
            ""endFrame"": 2,
            ""kind"": ""HitTraceWindow"",
            ""equipmentSlotId"": ""mainHand"",
            ""damageTypeId"": ""legacy"",
            ""baseDamage"": 1,
            ""impactPower"": 2,
            ""launchPower"": 3,
            ""stableEventId"": ""attack.hit"",
            ""hitEvent"": {
              ""stableEventId"": ""attack.hit"",
              ""damage"": {
                ""damageTypeId"": ""slash"",
                ""baseDamage"": 6,
                ""impactPower"": 34,
                ""launchPower"": 100
              }
            }
          }
        ]
      }
    }
  ]
}";

            CharacterActionJsonImportResult result = CharacterActionJsonImporter.Import(actionSetsJson, actionsJson);

            Assert.IsFalse(result.Success);
            StringAssert.Contains("hitEvent.damage", FirstImportDiagnostic(result));
        }

        [Test]
        public void Import_LegacyDamageProfileReferences_Fails()
        {
            string actionSetsJson = @"{
  ""schemaVersion"": ""0.4.0"",
  ""actionSets"": []
}";
            string actionsJson = @"{
  ""schemaVersion"": ""0.3.0"",
  ""actions"": [
    {
      ""actionId"": 1,
      ""category"": ""BasicAttack"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""durationFrames"": 8,
      ""phases"": [{ ""kind"": ""Active"", ""startFrame"": 0, ""endFrame"": 7 }],
      ""combatTrack"": {
        ""events"": [
          { ""frame"": 2, ""endFrame"": 2, ""kind"": ""HitTraceWindow"", ""equipmentSlotId"": ""mainHand"", ""damageProfileId"": ""damage.light_slash"", ""stableEventId"": ""attack.hit"", ""hitEvent"": { ""damage"": { ""damageTypeId"": ""slash"", ""baseDamage"": 1 } } }
        ]
      },
      ""gameplayTrack"": {
        ""events"": [
          { ""frame"": 4, ""kind"": ""ApplyGameplayEffect"", ""damageProfileId"": ""damage.shockwave"", ""targetPolicyId"": ""target.locked.area_2m"", ""stableEventId"": ""attack.wave"", ""hitEvent"": { ""damage"": { ""damageTypeId"": ""force"", ""baseDamage"": 1 } } }
        ]
      }
    }
  ]
}";

            CharacterActionJsonImportResult result = CharacterActionJsonImporter.Import(actionSetsJson, actionsJson);

            Assert.IsFalse(result.Success);
            StringAssert.Contains("damageProfileId", FirstImportDiagnostic(result));
        }

        [Test]
        public void Import_HitTraceWindowHitEvent_NormalizesCommonCombatHitConfig()
        {
            string actionSetsJson = @"{
  ""schemaVersion"": ""0.4.0"",
  ""actionSets"": []
}";
            string actionsJson = @"{
  ""schemaVersion"": ""0.3.0"",
  ""actions"": [
    {
      ""actionId"": 1,
      ""category"": ""BasicAttack"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""durationFrames"": 8,
      ""phases"": [{ ""kind"": ""Active"", ""startFrame"": 0, ""endFrame"": 7 }],
      ""combatTrack"": {
        ""events"": [
          {
            ""frame"": 2,
            ""endFrame"": 2,
            ""kind"": ""HitTraceWindow"",
            ""equipmentSlotId"": ""mainHand"",
            ""hitEvent"": {
              ""targetTypes"": [""HostileCharacter"", ""WorldWall""],
              ""environmentHitPolicy"": ""BlockedPresentation"",
              ""stableEventId"": ""attack.hit"",
              ""maxAcceptedHits"": 2,
              ""hitSameTargetPolicy"": ""OncePerEvent"",
              ""damage"": {
                ""damageTypeId"": ""slash"",
                ""formulaId"": ""ct.soft_defense_v1"",
                ""baseDamage"": 9,
                ""sourceAttributeScales"": [
                  { ""attributeId"": 2, ""scale"": 0.65 }
                ],
                ""impactPower"": 5,
                ""tags"": [""melee"", ""weapon""],
                ""analysisCategory"": ""BasicAttack""
              },
              ""onHitBuffs"": [
                { ""buffId"": 601009 }
              ]
            }
          }
        ]
      }
    }
  ]
}";

            CharacterActionJsonImportResult result = CharacterActionJsonImporter.Import(actionSetsJson, actionsJson);

            Assert.IsTrue(result.Success);
            CombatTrackEvent combat = result.Actions[0].CombatTrack.Events[0];
            Assert.AreEqual(CharacterTestNumericIds.Assert_attack_hit, combat.StableEventId);
            Assert.AreEqual(
                CombatHitTargetTypes.HostileCharacter | CombatHitTargetTypes.WorldWall,
                combat.HitEvent.TargetTypes);
            Assert.AreEqual(CombatEnvironmentHitPolicy.BlockedPresentation, combat.HitEvent.EnvironmentHitPolicy);
            Assert.AreEqual(2, combat.HitEvent.MaxAcceptedHits);
            Assert.AreEqual(CombatHitSameTargetPolicy.OncePerEvent, combat.HitEvent.HitSameTargetPolicy);
            Assert.AreEqual(CharacterTestNumericIds.DamageSlash, combat.HitEvent.Damage.DamageTypeId);
            Assert.AreEqual(9, combat.HitEvent.Damage.BaseDamage);
            Assert.AreEqual(5, combat.HitEvent.Damage.ImpactPower);
            Assert.AreEqual(CharacterTestNumericIds.DamageSlash, combat.DamagePayload.DamageTypeId);
            Assert.AreEqual("ct.soft_defense_v1", combat.DamagePayload.FormulaId);
            Assert.AreEqual(9, combat.DamagePayload.BaseDamage);
            Assert.AreEqual(0.65f, combat.DamagePayload.SourceAttributeScales[0].Scale, 0.0001f);
            Assert.AreEqual(5, combat.DamagePayload.ImpactPower);
            Assert.AreEqual(1, combat.HitEvent.OnHitBuffCount);
            Assert.AreEqual(601009, combat.HitEvent.OnHitBuffs[0].BuffId);
        }

        [Test]
        public void Import_GameplayHitEventOnHitBuffs_ArePreserved()
        {
            string actionSetsJson = @"{
  ""schemaVersion"": ""0.4.0"",
  ""actionSets"": []
}";
            string actionsJson = @"{
  ""schemaVersion"": ""0.3.0"",
  ""actions"": [
    {
      ""actionId"": 1,
      ""category"": ""Skill"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""durationFrames"": 8,
      ""phases"": [{ ""kind"": ""Active"", ""startFrame"": 0, ""endFrame"": 7 }],
      ""gameplayTrack"": {
        ""events"": [
          {
            ""frame"": 2,
            ""kind"": ""ApplyGameplayEffect"",
            ""targetPolicyId"": ""target.locked.area_2m"",
            ""stableEventId"": ""shockwave.area"",
            ""hitEvent"": {
              ""targetTypes"": [""HostileCharacter""],
              ""damage"": {
                ""damageTypeId"": ""force"",
                ""baseDamage"": 5
              },
              ""onHitBuffs"": [
                { ""buffId"": 601009 }
              ]
            }
          }
        ]
      }
    }
  ]
}";

            CharacterActionJsonImportResult result = CharacterActionJsonImporter.Import(actionSetsJson, actionsJson);

            Assert.IsTrue(result.Success);
            GameplayTrackEvent gameplay = result.Actions[0].GameplayTrack.Events[0];
            Assert.AreEqual(1, gameplay.HitEvent.OnHitBuffCount);
            Assert.AreEqual(601009, gameplay.HitEvent.OnHitBuffs[0].BuffId);
        }

        [Test]
        public void Import_DuplicateOnHitBuffIds_ReturnsDiagnostic()
        {
            string actionsJson = @"{
  ""schemaVersion"": ""0.3.0"",
  ""actions"": [
    {
      ""actionId"": 1,
      ""category"": ""BasicAttack"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""durationFrames"": 8,
      ""phases"": [{ ""kind"": ""Active"", ""startFrame"": 0, ""endFrame"": 7 }],
      ""combatTrack"": {
        ""events"": [
          {
            ""frame"": 2,
            ""endFrame"": 2,
            ""kind"": ""HitTraceWindow"",
            ""hitEvent"": {
              ""damage"": { ""damageTypeId"": ""slash"", ""baseDamage"": 1 },
              ""onHitBuffs"": [
                { ""buffId"": 601009 },
                { ""buffId"": 601009 }
              ]
            }
          }
        ]
      }
    }
  ]
}";

            CharacterActionJsonImportResult result = CharacterActionJsonImporter.Import("{}", actionsJson);

            Assert.IsFalse(result.Success);
            AssertHasDiagnostic(
                result,
                CharacterActionDiagnosticCodes.JsonInvalidConfig,
                "character_actions.json",
                "$.actions[0].combatTrack.events[0].hitEvent.onHitBuffs[1].buffId");
        }

        [Test]
        public void Import_ControlScaleMotionEvent_TreatsOmittedAxisAsUnchanged()
        {
            string actionSetsJson = @"{
  ""schemaVersion"": ""0.4.0"",
  ""actionSets"": []
}";
            string actionsJson = @"{
  ""schemaVersion"": ""0.3.0"",
  ""actions"": [
    {
      ""actionId"": 1,
      ""category"": ""BasicAttack"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""durationFrames"": 4,
      ""phases"": [{ ""kind"": ""Startup"", ""startFrame"": 0, ""endFrame"": 3 }],
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""events"": [
          { ""frame"": 0, ""endFrame"": 3, ""kind"": ""SetControlScale"", ""movementControlScale"": 0.0, ""stableEventId"": ""attack.move_lock"" },
          { ""frame"": 1, ""endFrame"": 3, ""kind"": ""SetControlScale"", ""rotationControlScale"": 0.5, ""stableEventId"": ""attack.turn_half"" }
        ]
      }
    }
  ]
}";

            CharacterActionJsonImportResult result = CharacterActionJsonImporter.Import(actionSetsJson, actionsJson);

            Assert.IsTrue(result.Success);
            MotionTrackEvent movementOnly = result.Actions[0].MotionTrack.Events[0];
            MotionTrackEvent rotationOnly = result.Actions[0].MotionTrack.Events[1];
            Assert.IsTrue(movementOnly.HasMovementControlScale);
            Assert.IsFalse(movementOnly.HasRotationControlScale);
            Assert.AreEqual(0f, movementOnly.MovementControlScale, 0.0001f);
            Assert.IsFalse(rotationOnly.HasMovementControlScale);
            Assert.IsTrue(rotationOnly.HasRotationControlScale);
            Assert.AreEqual(0.5f, rotationOnly.RotationControlScale, 0.0001f);
        }

        [Test]
        public void Import_ApplyImpulseMotionEvent_ReadsVectorSpace()
        {
            string actionSetsJson = @"{
  ""schemaVersion"": ""0.4.0"",
  ""actionSets"": []
}";
            string actionsJson = @"{
  ""schemaVersion"": ""0.3.0"",
  ""actions"": [
    {
      ""actionId"": 1,
      ""category"": ""Jump"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""durationFrames"": 2,
      ""phases"": [{ ""kind"": ""Startup"", ""startFrame"": 0, ""endFrame"": 1 }],
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""events"": [
          { ""frame"": 0, ""kind"": ""ApplyImpulse"", ""movementMode"": ""Airborne"", ""space"": ""Local"", ""y"": 0.0, ""z"": 3.0, ""stableEventId"": ""jump.forward"" }
        ]
      }
    }
  ]
}";

            CharacterActionJsonImportResult result = CharacterActionJsonImporter.Import(actionSetsJson, actionsJson);

            Assert.IsTrue(result.Success);
            MotionTrackEvent impulse = result.Actions[0].MotionTrack.Events[0];
            Assert.AreEqual(CharacterActionTrackEventKind.ApplyImpulse, impulse.Kind);
            Assert.AreEqual(CharacterActionMotionVectorSpace.Local, impulse.Space);
            Assert.AreEqual(3f, impulse.Z, 0.0001f);
        }

        [Test]
        public void Import_AnimationClipResourceKey_TakesPriorityOverLegacyActionKey()
        {
            string actionSetsJson = @"{
  ""schemaVersion"": ""0.4.0"",
  ""actionSets"": []
}";
            string actionsJson = @"{
  ""schemaVersion"": ""0.3.0"",
  ""actions"": [
    {
      ""actionId"": 1,
      ""category"": ""Jump"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""durationFrames"": 12,
      ""phases"": [{ ""kind"": ""Startup"", ""startFrame"": 0, ""endFrame"": 11 }],
            ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": ""legacy.action"",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.standing_jump"",
            ""animationStartOffsetFrames"": 6,
            ""animationEndOffsetFrames"": 4
          }
        ]
      }
    }
  ]
}";

            CharacterActionJsonImportResult result = CharacterActionJsonImporter.Import(actionSetsJson, actionsJson);

            Assert.IsTrue(result.Success);
            AnimationTrackEvent evt = result.Actions[0].AnimationTrack.Events[0];
            Assert.AreEqual("legacy.action", evt.AnimationActionKey);
            Assert.AreEqual("art.character.skeleton.animation.standing_jump", evt.AnimationClipResourceKey);
            Assert.AreEqual("art.character.skeleton.animation.standing_jump", evt.EffectiveAnimationKey);
            Assert.AreEqual(6, evt.AnimationStartOffsetFrames);
            Assert.AreEqual(4, evt.AnimationEndOffsetFrames);
        }

        [Test]
        public void Import_DuplicateActionIdsAndActionSetIds_ReturnsStableDiagnostics()
        {
            string actionSetsJson = @"{
  ""schemaVersion"": ""0.4.0"",
  ""actionSets"": [
    { ""id"": 1, ""stableId"": ""dup.set"" },
    { ""id"": 1, ""stableId"": ""dup.set"" }
  ]
}";
            string actionsJson = @"{
  ""schemaVersion"": ""0.3.0"",
  ""actions"": [
    { ""actionId"": 1, ""category"": ""Jump"", ""timelineAuthority"": ""CharacterAuthored"", ""durationFrames"": 1, ""phases"": [{ ""kind"": ""Startup"", ""startFrame"": 0, ""endFrame"": 0 }] },
    { ""actionId"": 1, ""category"": ""Jump"", ""timelineAuthority"": ""CharacterAuthored"", ""durationFrames"": 1, ""phases"": [{ ""kind"": ""Startup"", ""startFrame"": 0, ""endFrame"": 0 }] }
  ]
}";

            CharacterActionJsonImportResult result = CharacterActionJsonImporter.Import(actionSetsJson, actionsJson);

            Assert.IsFalse(result.Success);
            AssertHasDiagnostic(result, CharacterActionDiagnosticCodes.JsonDuplicateId, "character_action_sets.json", "$.actionSets[1].id");
            AssertHasDiagnostic(result, CharacterActionDiagnosticCodes.JsonDuplicateStableId, "character_action_sets.json", "$.actionSets[1].stableId");
            AssertHasDiagnostic(result, CharacterActionDiagnosticCodes.JsonDuplicateId, "character_actions.json", "$.actions[1].actionId");
        }

        [Test]
        public void Import_MissingCommandBindingAction_ReturnsMissingActionConfig()
        {
            string actionSetsJson = @"{
  ""schemaVersion"": ""0.4.0"",
  ""actionSets"": [
    {
      ""id"": 1,
      ""stableId"": ""test.set"",
      ""commandBindings"": [
        { ""intentId"": ""Jump"", ""actionId"": 999999 }
      ]
    }
  ]
}";

            CharacterActionJsonImportResult result = CharacterActionJsonImporter.Import(actionSetsJson, EmptyActionsJson);

            Assert.IsFalse(result.Success);
            AssertHasDiagnostic(
                result,
                CharacterActionDiagnosticCodes.MissingActionConfig,
                "character_action_sets.json",
                "$.actionSets[0].commandBindings[0].actionId");
        }

        [Test]
        public void Import_MalformedJsonOrMissingRootArrays_ReturnsDocumentDiagnostics()
        {
            CharacterActionJsonImportResult malformed = CharacterActionJsonImporter.Import("{", EmptyActionsJson);
            CharacterActionJsonImportResult missingArrays = CharacterActionJsonImporter.Import(
                "{ \"schemaVersion\": \"0.4.0\" }",
                "{ \"schemaVersion\": \"0.3.0\" }");

            Assert.IsFalse(malformed.Success);
            AssertHasDiagnostic(malformed, CharacterActionDiagnosticCodes.JsonInvalidDocument, "character_action_sets.json", "$");
            Assert.IsFalse(missingArrays.Success);
            AssertHasDiagnostic(missingArrays, CharacterActionDiagnosticCodes.JsonMissingRootArray, "character_action_sets.json", "$.actionSets");
            AssertHasDiagnostic(missingArrays, CharacterActionDiagnosticCodes.JsonMissingRootArray, "character_actions.json", "$.actions");
        }

        [Test]
        public void Import_InvalidEnumAndFrames_ReturnsStableDiagnostics()
        {
            string actionsJson = @"{
  ""schemaVersion"": ""0.3.0"",
  ""actions"": [
    {
      ""actionId"": 2,
      ""category"": ""NotACategory"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""durationFrames"": 5,
      ""phases"": [
        { ""kind"": ""Startup"", ""startFrame"": 4, ""endFrame"": 2 }
      ],
      ""motionTrack"": {
        ""events"": [
          { ""frame"": 0, ""kind"": ""PlayAnimation"" }
        ]
      }
    },
    {
      ""actionId"": 3,
      ""category"": ""Jump"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""durationFrames"": 5,
      ""phases"": [
        { ""kind"": ""Startup"", ""startFrame"": 4, ""endFrame"": 2 }
      ]
    }
  ]
}";

            CharacterActionJsonImportResult result = CharacterActionJsonImporter.Import("{}", actionsJson);

            Assert.IsFalse(result.Success);
            AssertHasDiagnostic(result, CharacterActionDiagnosticCodes.JsonInvalidEnum, "character_actions.json", "$.actions[0].category");
            AssertHasDiagnostic(result, CharacterActionDiagnosticCodes.JsonInvalidFrame, "character_actions.json", "$.actions[1].phases[0]");
        }

        [Test]
        public void LandingProfiles_ImportOptionalSlotsAndResolveThresholdFallbacks()
        {
            const string actionSetsJson = @"{
  ""schemaVersion"": ""0.4.0"",
  ""actionSets"": [
    { ""id"": 1, ""stableId"": ""set.default"", ""landingProfileId"": ""landing.default"" }
  ]
}";
            const string landingProfilesJson = @"{
  ""schemaVersion"": ""0.4.0"",
  ""landingProfiles"": [
    {
      ""stableId"": ""landing.default"",
      ""lightLandingActionId"": 300,
      ""heavyLandingActionId"": 300,
      ""lightLandingMinDownwardSpeedMillimetersPerSecond"": 1000,
      ""heavyLandingMinDownwardSpeedMillimetersPerSecond"": 8000
    }
  ]
}";

            CharacterActionJsonImportResult result = CharacterActionJsonImporter.Import(
                actionSetsJson,
                ActionsJson,
                CreateEmptyReactionProfilesJson(),
                landingProfilesJson,
                CharacterActionJsonImporter.DefaultActionSetsSourcePath,
                CharacterActionJsonImporter.DefaultActionsSourcePath,
                CharacterActionJsonImporter.DefaultReactionProfilesSourcePath,
                CharacterActionJsonImporter.DefaultLandingProfilesSourcePath);

            Assert.IsTrue(result.Success, FirstImportDiagnostic(result));
            Assert.AreEqual("landing.default", result.ActionSets[0].LandingProfileId);
            Assert.AreEqual(1, result.LandingProfiles.Length);
            CharacterLandingProfile profile = result.LandingProfiles[0];
            Assert.IsFalse(profile.MeetsLightLandingThreshold(999));
            Assert.IsTrue(profile.MeetsLightLandingThreshold(1000));
            Assert.IsFalse(profile.IsHeavyLanding(7999));
            Assert.IsTrue(profile.IsHeavyLanding(8000));
            Assert.IsTrue(profile.TryResolveAction(false, out CharacterActionId lightAction, out bool lightResolvedHeavy, out bool lightFallback));
            Assert.AreEqual(new CharacterActionId(300), lightAction);
            Assert.IsFalse(lightResolvedHeavy);
            Assert.IsFalse(lightFallback);
            Assert.IsTrue(profile.TryResolveAction(true, out CharacterActionId heavyAction, out bool heavyResolvedHeavy, out bool heavyFallback));
            Assert.AreEqual(new CharacterActionId(300), heavyAction);
            Assert.IsTrue(heavyResolvedHeavy);
            Assert.IsFalse(heavyFallback);

            var lightOnly = new CharacterLandingProfile("landing.light_only", new CharacterActionId(300), CharacterActionId.None, 8000);
            Assert.IsTrue(lightOnly.TryResolveAction(true, out CharacterActionId fallbackAction, out bool fallbackHeavy, out bool usedFallback));
            Assert.AreEqual(new CharacterActionId(300), fallbackAction);
            Assert.IsFalse(fallbackHeavy);
            Assert.IsTrue(usedFallback);

            var empty = new CharacterLandingProfile("landing.empty");
            Assert.IsFalse(empty.TryResolveAction(false, out _, out _, out _));
            var emptyRegistry = new CharacterActionConfigRegistry(
                result.ActionSets,
                result.Actions,
                landingProfiles: new[]
                {
                    new CharacterLandingProfile("landing.default")
                });
            CharacterActionValidationReport emptyReport =
                CharacterActionValidationReport.Validate(emptyRegistry);
            Assert.IsTrue(emptyReport.Success, FirstValidationDiagnostic(emptyReport));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterLandingProfile(
                "landing.zero_threshold",
                lightLandingMinDownwardSpeedMillimetersPerSecond: 0));
        }

        [Test]
        public void LandingProfiles_MissingConfiguredActionIsValidationError()
        {
            var actionSet = new CharacterActionSetConfig(
                1,
                "set.default",
                string.Empty,
                string.Empty,
                string.Empty,
                Array.Empty<CharacterActionBinding>(),
                Array.Empty<CharacterAbilityActionBinding>(),
                Array.Empty<CharacterReactionBinding>(),
                0,
                string.Empty,
                MxFramework.CharacterAction.CharacterActionId.None,
                landingProfileId: "landing.default");
            var landingProfile = new CharacterLandingProfile(
                "landing.default",
                lightLandingActionId: CharacterTestActionIds.Action_missing_action);
            var registry = new CharacterActionConfigRegistry(
                new[] { actionSet },
                Array.Empty<CharacterActionConfig>(),
                landingProfiles: new[] { landingProfile });

            CharacterActionValidationReport report = CharacterActionValidationReport.Validate(registry);

            Assert.IsTrue(report.HasErrors);
            Assert.IsTrue(Array.Exists(
                report.Diagnostics,
                diagnostic => diagnostic.Code == CharacterActionDiagnosticCodes.MissingActionConfig
                    && diagnostic.StablePath.Contains("lightLandingActionId")));
        }

        private static string CreateEmptyReactionProfilesJson()
        {
            return @"{ ""schemaVersion"": ""0.4.0"", ""reactionProfiles"": [] }";
        }

        private static void AssertHasDiagnostic(
            CharacterActionJsonImportResult result,
            string code,
            string sourcePath,
            string jsonPath)
        {
            for (int i = 0; i < result.Diagnostics.Length; i++)
            {
                CharacterActionJsonDiagnostic diagnostic = result.Diagnostics[i];
                if (diagnostic.Code == code
                    && diagnostic.SourcePath == sourcePath
                    && diagnostic.JsonPath == jsonPath)
                {
                    return;
                }
            }

            Assert.Fail("Expected diagnostic " + code + " at " + sourcePath + ":" + jsonPath + ".");
        }

        private static string FirstImportDiagnostic(CharacterActionJsonImportResult result)
        {
            return result != null && result.Diagnostics.Length > 0
                ? result.Diagnostics[0].ToString()
                : string.Empty;
        }

        private static string FirstValidationDiagnostic(CharacterActionValidationReport report)
        {
            return report != null && report.Diagnostics.Length > 0
                ? report.Diagnostics[0].ToString()
                : string.Empty;
        }

        private const string EmptyActionsJson = @"{
  ""schemaVersion"": ""0.3.0"",
  ""actions"": []
}";

        private const string ActionSetsJson = @"{
  ""schemaVersion"": ""0.4.0"",
  ""actionSets"": [
    {
      ""id"": 1,
      ""stableId"": ""iron_vanguard.default"",
      ""displayName"": ""Iron Vanguard Default"",
      ""characterStableId"": ""iron_vanguard"",
      ""equipmentStateStableId"": ""iron_vanguard.default_equipment"",
      ""movementProfileId"": ""movement.default"",
      ""reactionProfileId"": ""reaction.default"",
      ""defaultActionId"": 0,
      ""commandBindings"": [
        {
          ""intentId"": ""LightAttack"",
          ""actionId"": 300,
          ""priority"": 10,
          ""allowQueue"": true,
          ""queueWindowFrames"": 6
        }
      ],
      ""abilityBindings"": [
        {
          ""abilityId"": 9001,
          ""actionId"": 300,
          ""requiredTags"": [""weapon.sword""],
          ""forbiddenTags"": []
        }
      ],
      ""reactionBindings"": []
    }
  ]
}";

        private const string ActionsJson = @"{
  ""schemaVersion"": ""0.3.0"",
  ""actions"": [
    {
      ""actionId"": 300,
      ""displayName"": ""Light Attack"",
      ""category"": ""BasicAttack"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [""attack"", ""melee""],
      ""priority"": 10,
      ""durationFrames"": 24,
      ""requirements"": [
        { ""kind"": ""Grounded"", ""stableId"": """", ""value"": 0 }
      ],
      ""phases"": [
        { ""kind"": ""Startup"", ""startFrame"": 0, ""endFrame"": 5 },
        { ""kind"": ""Active"", ""startFrame"": 6, ""endFrame"": 10 },
        { ""kind"": ""Recovery"", ""startFrame"": 11, ""endFrame"": 23 }
      ],
      ""cancelRules"": [
        { ""startFrame"": 16, ""endFrame"": 23, ""targetActionId"": 0, ""sourceKind"": ""Command"", ""allow"": true }
      ],
      ""interruptRules"": [
        { ""sourceKind"": ""CohesionBreak"", ""minimumPriority"": 100, ""targetActionId"": 0, ""allow"": true }
      ],
      ""cohesionSupport"": {
        ""enabled"": true,
        ""supportRate"": 1.5,
        ""minSupportRatio"": 1.0
      },
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 23,
            ""kind"": ""LockMovement"",
            ""movementMode"": ""ControlLocked"",
            ""stableEventId"": ""light_attack.motion_lock""
          }
        ]
      },
      ""combatTrack"": {
        ""combatActionId"": ""combat.light_attack"",
        ""events"": [
          {
            ""frame"": 6,
            ""kind"": ""StartCombatAction"",
            ""combatActionId"": ""combat.light_attack"",
            ""stableEventId"": ""light_attack.combat""
          }
        ]
      },
      ""gameplayTrack"": {
        ""events"": [
          {
            ""frame"": 6,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": ""scatter.light"",
            ""stableEventId"": ""light_attack.gameplay""
          }
        ]
      },
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": ""anim.light_attack"",
            ""transitionSeconds"": 0.08,
            ""stableEventId"": ""light_attack.animation""
          }
        ]
      },
      ""presentationTrack"": {
        ""events"": [
          {
            ""frame"": 6,
            ""endFrame"": 8,
            ""kind"": ""SpawnVisualCue"",
            ""resourceKey"": ""vfx.light_attack"",
            ""vfxAttachment"": {
              ""mode"": ""FixedWorld"",
              ""socketId"": ""mainHand"",
              ""offsetSpace"": ""World"",
              ""transform"": {
                ""positionX"": 0.25,
                ""positionY"": 0.5,
                ""positionZ"": 0.75,
                ""rotationEulerX"": 10,
                ""rotationEulerY"": 20,
                ""rotationEulerZ"": 30,
                ""scaleX"": 1.5,
                ""scaleY"": 2,
                ""scaleZ"": 2.5
              }
            },
            ""vfxPlaybackMode"": ""StopAtEventEnd"",
            ""vfxPlaybackRate"": 0.5,
            ""vfxScaleMultiplier"": 1.25,
            ""vfxStartOffsetFrames"": 3,
            ""vfxEndOffsetFrames"": 4,
            ""vfxNaturalEndPolicy"": ""Dissipate"",
            ""vfxParameter0"": 7,
            ""vfxParameter1"": 8,
            ""vfxParameter2"": 9,
            ""vfxParameter3"": 10,
            ""stableEventId"": ""light_attack.vfx""
          }
        ]
      },
      ""debugTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""kind"": ""EmitDebugMarker"",
            ""markerId"": ""light_attack.start"",
            ""stableEventId"": ""light_attack.debug""
          }
        ]
      }
    }
  ]
}";
    }
}
