using System;
using System.IO;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestBuffAuthoringRuntimeTests
    {
        [Test]
        public void GameplayBuffDefinitionJsonImporter_ImportsGuardAuraBuff()
        {
            GameplayBuffDefinitionRegistry registry = GameplayBuffDefinitionJsonImporter.Import(GuardAuraBuffDefinitionsJson);

            Assert.AreEqual(6, registry.Count);
            Assert.IsTrue(registry.TryGet(CharacterTestGuardAuraIds.BuffId, out GameplayBuffDefinition definition));
            Assert.AreEqual("buff.character_test.guard_aura.defense", definition.StableId);
            Assert.AreEqual(CharacterTestGuardAuraIds.GuardAuraBuffDurationFrames, definition.DefaultDurationFrames);
            Assert.AreEqual(1, definition.MaxStackCount);
            Assert.AreEqual(1, definition.ApplyStackCount);
            Assert.AreEqual(GameplayBuffStackPolicy.RefreshDuration, definition.StackPolicy);
            GameplayBuffEffectDefinition[] effects = definition.GetEffects();
            Assert.AreEqual(1, effects.Length);
            Assert.AreEqual(GameplayBuffEffectKind.AttributeModifier, effects[0].Kind);
            Assert.AreEqual(CharacterTestCharacterAttributeIds.Defense, effects[0].AttributeId);
            Assert.AreEqual(CharacterTestGuardAuraIds.DefenseBonus, effects[0].AddValue);
            Assert.IsTrue(registry.TryGet(CharacterTestGuardAuraIds.OnHitDefenseBreakBuffId, out GameplayBuffDefinition onHitDefinition));
            Assert.AreEqual("buff.character_test.on_hit.defense_break", onHitDefinition.StableId);
            Assert.AreEqual(CharacterTestGuardAuraIds.OnHitDefenseBreakDurationFrames, onHitDefinition.DefaultDurationFrames);
            GameplayBuffEffectDefinition[] onHitEffects = onHitDefinition.GetEffects();
            Assert.AreEqual(1, onHitEffects.Length);
            Assert.AreEqual(CharacterTestCharacterAttributeIds.Defense, onHitEffects[0].AttributeId);
            Assert.AreEqual(CharacterTestGuardAuraIds.OnHitDefensePenalty, onHitEffects[0].AddValue);
            Assert.IsTrue(registry.TryGet(CharacterTestAreaConfinementIds.RootedBuffId, out GameplayBuffDefinition rootedDefinition));
            Assert.AreEqual("buff.character_test.area_confinement.rooted", rootedDefinition.StableId);
            Assert.AreEqual(CharacterTestAreaConfinementIds.RootedBuffDurationFrames, rootedDefinition.DefaultDurationFrames);
            GameplayBuffEffectDefinition[] rootedEffects = rootedDefinition.GetEffects();
            Assert.AreEqual(1, rootedEffects.Length);
            Assert.AreEqual(GameplayBuffEffectKind.Status, rootedEffects[0].Kind);
            Assert.AreEqual(
                new GameplayStatusId(CharacterTestAreaConfinementIds.AreaConfinedStatusId),
                rootedEffects[0].StatusId);
            Assert.IsTrue(registry.TryGet(CharacterTestStasisIds.StasisBuffId, out GameplayBuffDefinition stasisDefinition));
            Assert.AreEqual(CharacterTestStasisIds.StasisBuffStableId, stasisDefinition.StableId);
            Assert.AreEqual(CharacterTestStasisIds.StasisBuffDurationFrames, stasisDefinition.DefaultDurationFrames);
            Assert.AreEqual(1, stasisDefinition.MaxStackCount);
            Assert.AreEqual(GameplayBuffStackPolicy.RefreshDuration, stasisDefinition.StackPolicy);
            GameplayBuffEffectDefinition[] stasisEffects = stasisDefinition.GetEffects();
            Assert.AreEqual(1, stasisEffects.Length);
            Assert.AreEqual(GameplayBuffEffectKind.Status, stasisEffects[0].Kind);
            Assert.AreEqual(new GameplayStatusId(CharacterTestStasisIds.StasisStatusId), stasisEffects[0].StatusId);
            Assert.IsTrue(registry.TryGet(CharacterTestStunIds.StunBuffId, out GameplayBuffDefinition stunDefinition));
            Assert.AreEqual(CharacterTestStunIds.StunBuffStableId, stunDefinition.StableId);
            Assert.AreEqual(CharacterTestStunIds.StunBuffDurationFrames, stunDefinition.DefaultDurationFrames);
            Assert.AreEqual(1, stunDefinition.MaxStackCount);
            Assert.AreEqual(GameplayBuffStackPolicy.RefreshDuration, stunDefinition.StackPolicy);
            GameplayBuffEffectDefinition[] stunEffects = stunDefinition.GetEffects();
            Assert.AreEqual(1, stunEffects.Length);
            Assert.AreEqual(GameplayBuffEffectKind.Status, stunEffects[0].Kind);
            Assert.AreEqual(new GameplayStatusId(CharacterTestStunIds.StunnedStatusId), stunEffects[0].StatusId);
            Assert.IsTrue(registry.TryGet(CharacterTestPoisonIds.PoisonedBuffId, out GameplayBuffDefinition poisonDefinition));
            Assert.AreEqual(CharacterTestPoisonIds.PoisonedBuffStableId, poisonDefinition.StableId);
            Assert.AreEqual(CharacterTestPoisonIds.PoisonedBuffDurationFrames, poisonDefinition.DefaultDurationFrames);
            Assert.AreEqual(1, poisonDefinition.MaxStackCount);
            Assert.AreEqual(GameplayBuffStackPolicy.RefreshDuration, poisonDefinition.StackPolicy);
            GameplayBuffEffectDefinition[] poisonEffects = poisonDefinition.GetEffects();
            Assert.AreEqual(1, poisonEffects.Length);
            Assert.AreEqual(GameplayBuffEffectKind.Status, poisonEffects[0].Kind);
            Assert.AreEqual(new GameplayStatusId(CharacterTestPoisonIds.PoisonedStatusId), poisonEffects[0].StatusId);
        }

        [Test]
        public void CombatGameplayAuraBuffDefinitionJsonImporter_ImportsGuardAuraDefinition()
        {
            CombatGameplayAuraBuffDefinitionRegistry registry =
                CombatGameplayAuraBuffDefinitionJsonImporter.Import(GuardAuraDefinitionsJson);

            Assert.AreEqual(1, registry.Count);
            Assert.IsTrue(registry.TryGet(CharacterTestGuardAuraIds.AuraId, out CombatGameplayAuraBuffDefinition definition));
            Assert.AreEqual(CharacterTestGuardAuraIds.BuffId, definition.BuffId);
            Assert.AreEqual(Fix64.FromInt(CharacterTestGuardAuraIds.RadiusMeters), definition.Radius);
            Assert.AreEqual(new CombatPhysicsLayerMask(256), definition.LayerMask);
            Assert.AreEqual(GameplayTargetRelationFilter.SameTeam, definition.RelationFilter);
            Assert.IsTrue(definition.IncludeCaster);
        }

        [Test]
        public void GameplayBuffDefinitionJsonImporter_RejectsUnsupportedSchema()
        {
            Assert.Throws<InvalidOperationException>(() =>
                GameplayBuffDefinitionJsonImporter.Import(@"{ ""schemaVersion"": ""2"", ""definitions"": [] }"));
        }

        [Test]
        public void CombatGameplayAuraBuffDefinitionJsonImporter_RejectsNonPositiveLayerMask()
        {
            Assert.Throws<InvalidOperationException>(() =>
                CombatGameplayAuraBuffDefinitionJsonImporter.Import(InvalidAuraLayerMaskJson));
        }

        [Test]
        public void CharacterTestGuardAuraRuntime_LoadsGeneratedBuffDefinitionsBeforeFallback()
        {
            string directory = CreateTempDirectory();
            string generatedPath = Path.Combine(directory, "buff_definitions.json");
            try
            {
                File.WriteAllText(generatedPath, AlternateBuffDefinitionsJson);

                GameplayBuffDefinitionRegistry generated = CharacterTestGuardAuraRuntime.CreateBuffDefinitions(generatedPath);
                GameplayBuffDefinitionRegistry fallback = CharacterTestGuardAuraRuntime.CreateBuffDefinitions(Path.Combine(directory, "missing.json"));

                Assert.AreEqual(1, generated.Count);
                Assert.IsTrue(generated.TryGet(700001, out GameplayBuffDefinition generatedDefinition));
                Assert.AreEqual("buff.test.generated", generatedDefinition.StableId);
                Assert.IsFalse(generated.TryGet(CharacterTestGuardAuraIds.BuffId, out _));
                Assert.IsTrue(fallback.TryGet(CharacterTestGuardAuraIds.BuffId, out GameplayBuffDefinition fallbackDefinition));
                Assert.AreEqual("buff.character_test.guard_aura.defense", fallbackDefinition.StableId);
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        }

        [Test]
        public void CharacterTestGuardAuraRuntime_LoadsGeneratedAuraDefinitionsBeforeFallback()
        {
            string directory = CreateTempDirectory();
            string generatedPath = Path.Combine(directory, "aura_buff_definitions.json");
            try
            {
                File.WriteAllText(generatedPath, AlternateAuraDefinitionsJson);

                CombatGameplayAuraBuffDefinitionRegistry generated = CharacterTestGuardAuraRuntime.CreateAuraDefinitions(generatedPath);
                CombatGameplayAuraBuffDefinitionRegistry fallback = CharacterTestGuardAuraRuntime.CreateAuraDefinitions(Path.Combine(directory, "missing.json"));

                Assert.AreEqual(1, generated.Count);
                Assert.IsTrue(generated.TryGet(700101, out CombatGameplayAuraBuffDefinition generatedDefinition));
                Assert.AreEqual(700001, generatedDefinition.BuffId);
                Assert.IsFalse(generated.TryGet(CharacterTestGuardAuraIds.AuraId, out _));
                Assert.IsTrue(fallback.TryGet(CharacterTestGuardAuraIds.AuraId, out CombatGameplayAuraBuffDefinition fallbackDefinition));
                Assert.AreEqual(CharacterTestGuardAuraIds.BuffId, fallbackDefinition.BuffId);
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        }

        [Test]
        public void GuardAuraGeneratedJson_MatchesEmbeddedDefinitions()
        {
            GameplayBuffDefinitionRegistry generatedBuffs =
                GameplayBuffDefinitionJsonImporter.Import(GuardAuraBuffDefinitionsJson);
            CombatGameplayAuraBuffDefinitionRegistry generatedAuras =
                CombatGameplayAuraBuffDefinitionJsonImporter.Import(GuardAuraDefinitionsJson);

            Assert.AreEqual(
                CharacterTestGuardAuraRuntime.CreateEmbeddedBuffDefinitions().ComputeStableHash(),
                generatedBuffs.ComputeStableHash());
            Assert.AreEqual(
                CharacterTestGuardAuraRuntime.CreateEmbeddedAuraDefinitions().ComputeStableHash(),
                generatedAuras.ComputeStableHash());
        }

        [Test]
        public void GuardAuraGeneratedPresentationJson_MatchesEmbeddedStasisPresentation()
        {
            GameplayBuffPresentationDefinitionRegistry generated =
                GameplayBuffPresentationDefinitionJsonImporter.ImportFile(
                    CharacterTestGuardAuraIds.DefaultGeneratedBuffPresentationDefinitionsPath);
            GameplayBuffPresentationDefinitionRegistry embedded =
                CharacterTestGuardAuraRuntime.CreateEmbeddedBuffPresentationDefinitions();

            Assert.AreEqual(embedded.Count, generated.Count);
            Assert.IsTrue(generated.TryGet(
                CharacterTestStasisIds.StasisBuffId,
                out GameplayBuffPresentationDefinition stasis));
            Assert.AreEqual("Stasis", stasis.DisplayName);
            Assert.AreEqual(CharacterTestStasisIds.StasisBuffNameKey, stasis.NameKey);
            Assert.AreEqual("STOP", stasis.ShortName);
            Assert.AreEqual(
                "Self-driven action, movement, and facing pause; external motion remains active.",
                stasis.Description);
            Assert.AreEqual(GameplayBuffPresentationPolarity.Debuff, stasis.Polarity);
            Assert.IsTrue(stasis.ShowOnPlayerHud);
            Assert.IsTrue(stasis.ShowOnTargetHud);
            Assert.IsTrue(stasis.ShowOnHeadPad);
            Assert.IsTrue(generated.TryGet(
                CharacterTestStunIds.StunBuffId,
                out GameplayBuffPresentationDefinition stunned));
            Assert.AreEqual("Stunned", stunned.DisplayName);
            Assert.AreEqual(CharacterTestStunIds.StunBuffNameKey, stunned.NameKey);
            Assert.AreEqual("STUN", stunned.ShortName);
            Assert.AreEqual(GameplayBuffPresentationPolarity.Debuff, stunned.Polarity);
            Assert.IsTrue(stunned.ShowOnPlayerHud);
            Assert.IsTrue(stunned.ShowOnTargetHud);
            Assert.IsTrue(stunned.ShowOnHeadPad);
            Assert.IsTrue(generated.TryGet(
                CharacterTestPoisonIds.PoisonedBuffId,
                out GameplayBuffPresentationDefinition poisoned));
            Assert.AreEqual("Poisoned", poisoned.DisplayName);
            Assert.AreEqual(CharacterTestPoisonIds.PoisonedBuffNameKey, poisoned.NameKey);
            Assert.AreEqual("POIS", poisoned.ShortName);
            Assert.AreEqual(GameplayBuffPresentationPolarity.Debuff, poisoned.Polarity);
            Assert.IsTrue(poisoned.ShowOnPlayerHud);
            Assert.IsTrue(poisoned.ShowOnTargetHud);
            Assert.IsTrue(poisoned.ShowOnHeadPad);
            CollectionAssert.AreEqual(embedded.ToArray(), generated.ToArray());
        }

        private static string CreateTempDirectory()
        {
            string directory = Path.Combine(Path.GetTempPath(), "mx-charactertest-buff-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private const string GuardAuraBuffDefinitionsJson = @"{
  ""schemaVersion"": ""1"",
  ""packageId"": ""character_test.guard_aura"",
  ""definitions"": [
    {
      ""buffId"": 601008,
      ""stableId"": ""buff.character_test.guard_aura.defense"",
      ""displayName"": ""CharacterTest Guard Aura Defense"",
      ""defaultDurationFrames"": 2,
      ""maxStackCount"": 1,
      ""applyStackCount"": 1,
      ""stackPolicy"": ""RefreshDuration"",
      ""isPermanent"": false,
      ""effects"": [
        {
          ""effectId"": 1,
          ""kind"": ""AttributeModifier"",
          ""attributeId"": 3,
          ""addValue"": 5,
          ""statusId"": 0
        }
      ]
    },
    {
      ""buffId"": 601009,
      ""stableId"": ""buff.character_test.on_hit.defense_break"",
      ""displayName"": ""CharacterTest On-Hit Defense Break"",
      ""defaultDurationFrames"": 300,
      ""maxStackCount"": 1,
      ""applyStackCount"": 1,
      ""stackPolicy"": ""RefreshDuration"",
      ""isPermanent"": false,
      ""effects"": [
        {
          ""effectId"": 1,
          ""kind"": ""AttributeModifier"",
          ""attributeId"": 3,
          ""addValue"": -3,
          ""statusId"": 0
        }
      ]
    },
    {
      ""buffId"": 601010,
      ""stableId"": ""buff.character_test.area_confinement.rooted"",
      ""displayName"": ""CharacterTest Area Confinement Rooted"",
      ""defaultDurationFrames"": 2,
      ""maxStackCount"": 1,
      ""applyStackCount"": 1,
      ""stackPolicy"": ""RefreshDuration"",
      ""isPermanent"": false,
      ""effects"": [
        {
          ""effectId"": 1,
          ""kind"": ""Status"",
          ""attributeId"": 0,
          ""addValue"": 0,
          ""statusId"": 901010
        }
      ]
    },
    {
      ""buffId"": 601011,
      ""stableId"": ""buff.character_test.stasis.action_stop"",
      ""displayName"": ""Stasis"",
      ""defaultDurationFrames"": 90,
      ""maxStackCount"": 1,
      ""applyStackCount"": 1,
      ""stackPolicy"": ""RefreshDuration"",
      ""isPermanent"": false,
      ""effects"": [
        {
          ""effectId"": 1,
          ""kind"": ""Status"",
          ""attributeId"": 0,
          ""addValue"": 0,
          ""statusId"": 901011
        }
      ]
    },
    {
      ""buffId"": 601013,
      ""stableId"": ""buff.character_test.stun.control"",
      ""displayName"": ""Stunned"",
      ""defaultDurationFrames"": 90,
      ""maxStackCount"": 1,
      ""applyStackCount"": 1,
      ""stackPolicy"": ""RefreshDuration"",
      ""isPermanent"": false,
      ""effects"": [
        {
          ""effectId"": 1,
          ""kind"": ""Status"",
          ""attributeId"": 0,
          ""addValue"": 0,
          ""statusId"": 901013
        }
      ]
    },
    {
      ""buffId"": 601012,
      ""stableId"": ""buff.character_test.poisoned.dot"",
      ""displayName"": ""Poisoned"",
      ""defaultDurationFrames"": 360,
      ""maxStackCount"": 1,
      ""applyStackCount"": 1,
      ""stackPolicy"": ""RefreshDuration"",
      ""isPermanent"": false,
      ""effects"": [
        {
          ""effectId"": 1,
          ""kind"": ""Status"",
          ""attributeId"": 0,
          ""addValue"": 0,
          ""statusId"": 901012
        }
      ]
    }
  ]
}";

        private const string GuardAuraDefinitionsJson = @"{
  ""schemaVersion"": ""1"",
  ""packageId"": ""character_test.guard_aura"",
  ""definitions"": [
    {
      ""auraId"": 501008,
      ""buffId"": 601008,
      ""radiusMeters"": 4.0,
      ""layerMask"": 256,
      ""relationFilter"": ""SameTeam"",
      ""includeCaster"": true
    }
  ]
}";

        private const string InvalidAuraLayerMaskJson = @"{
  ""schemaVersion"": ""1"",
  ""definitions"": [
    {
      ""auraId"": 501008,
      ""buffId"": 601008,
      ""radiusMeters"": 4.0,
      ""layerMask"": -1,
      ""relationFilter"": ""SameTeam"",
      ""includeCaster"": true
    }
  ]
}";

        private const string AlternateBuffDefinitionsJson = @"{
  ""schemaVersion"": ""1"",
  ""packageId"": ""test.generated"",
  ""definitions"": [
    {
      ""buffId"": 700001,
      ""stableId"": ""buff.test.generated"",
      ""defaultDurationFrames"": 5,
      ""maxStackCount"": 1,
      ""applyStackCount"": 1,
      ""stackPolicy"": ""RefreshDuration"",
      ""effects"": []
    }
  ]
}";

        private const string AlternateAuraDefinitionsJson = @"{
  ""schemaVersion"": ""1"",
  ""packageId"": ""test.generated"",
  ""definitions"": [
    {
      ""auraId"": 700101,
      ""buffId"": 700001,
      ""radiusMeters"": 1.0,
      ""layerMask"": 1,
      ""relationFilter"": ""Any"",
      ""includeCaster"": false
    }
  ]
}";
    }
}
