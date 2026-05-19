using System;
using System.Linq;
using MxFramework.CharacterApplication;
using MxFramework.Config;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterApplication
{
    public class CharacterApplicationResolverTests
    {
        [Test]
        public void CharacterPackageResolver_WithSwordShieldLoadout_ReturnsResolvedProfile()
        {
            IronVanguardFixture fixture = IronVanguardFixture.Create();

            CharacterPackageResolveResult result = CharacterPackageResolver.Resolve(fixture.CreatePackageRequest(fixture.SwordShieldLoadout));

            Assert.IsFalse(result.ValidationReport.HasErrors, string.Join(", ", result.ValidationReport.Issues.Select(issue => issue.StableCode)));
            Assert.AreEqual(fixture.SwordShieldState.StateId, result.ResolvedProfile.ActiveEquipmentStateId);
            Assert.AreEqual(fixture.SwordShieldActionSet.ActionSetId, result.ResolvedProfile.CombatActionSetId);
            Assert.AreEqual("anim.iron_vanguard.sword_shield", result.ResolvedProfile.AnimationProfileId);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    new CharacterAbilityId(900001),
                    new CharacterAbilityId(900002),
                    new CharacterAbilityId(900003),
                    new CharacterAbilityId(900004)
                },
                result.ResolvedProfile.EffectiveAbilityIds);
            Assert.GreaterOrEqual(result.ResolvedProfile.RequiredResources.Length, 5);
            Assert.AreEqual("mx.character.iron_vanguard", result.ResolvedProfile.DebugContext.CharacterStableId);
        }

        [Test]
        public void EquipmentStateResolver_ResolvesSingleSwordAndUnarmedDeterministically()
        {
            IronVanguardFixture fixture = IronVanguardFixture.Create();

            EquipmentStateResolveResult singleSword = EquipmentStateResolver.Resolve(
                fixture.EquipmentSchema,
                fixture.SingleSwordLoadout,
                fixture.Weapons,
                fixture.EquipmentStates);
            EquipmentStateResolveResult unarmed = EquipmentStateResolver.Resolve(
                fixture.EquipmentSchema,
                fixture.UnarmedLoadout,
                fixture.Weapons,
                fixture.EquipmentStates);

            Assert.AreEqual(EquipmentStateResolveStatus.Success, singleSword.Status);
            Assert.AreEqual(fixture.SingleSwordState.StateId, singleSword.ActiveStateId);
            Assert.AreEqual(EquipmentStateResolveStatus.Success, unarmed.Status);
            Assert.AreEqual(fixture.UnarmedState.StateId, unarmed.ActiveStateId);
        }

        [Test]
        public void EquipmentStateResolver_WhenTopPriorityTies_ReturnsStructuredFailure()
        {
            IronVanguardFixture fixture = IronVanguardFixture.Create();
            EquipmentStateConfig tieState = new EquipmentStateConfig(
                new EquipmentStateId(770004),
                "mx.equipment_state.iron_vanguard.sword_shield_tie",
                fixture.EquipmentSchema.EquipmentSchemaId,
                fixture.SwordShieldState.Priority,
                fixture.SwordShieldState.MatchRules,
                fixture.SwordShieldState.GrantedAbilityLoadoutId,
                fixture.SwordShieldState.CombatActionSetId,
                fixture.SwordShieldState.AnimationProfileId,
                Array.Empty<string>());
            EquipmentSchemaConfig schema = fixture.CreateSchemaWithStates(new[]
            {
                fixture.UnarmedState.StateId,
                fixture.SingleSwordState.StateId,
                fixture.SwordShieldState.StateId,
                tieState.StateId
            });

            EquipmentStateResolveResult result = EquipmentStateResolver.Resolve(
                schema,
                fixture.SwordShieldLoadout,
                fixture.Weapons,
                new[] { fixture.UnarmedState, fixture.SingleSwordState, fixture.SwordShieldState, tieState });

            Assert.AreEqual(EquipmentStateResolveStatus.MultipleMatchingStates, result.Status);
            AssertDiagnostic(result.Diagnostics, CharacterDiagnosticCode.EquipmentStateTie);
        }

        [Test]
        public void AbilityGrantResolver_ReportsDuplicateAbilityAndSlotConflicts()
        {
            IronVanguardFixture fixture = IronVanguardFixture.Create();
            AbilityLoadoutConfig runtimeGrant = new AbilityLoadoutConfig(
                new AbilityLoadoutId(790099),
                "mx.ability_loadout.runtime.conflict",
                new[] { new CharacterAbilityId(900002), new CharacterAbilityId(900099) },
                Array.Empty<CharacterAbilityId>(),
                new[]
                {
                    new AbilitySlotBinding("primary", new CharacterAbilityId(900099), "intent.primary"),
                    new AbilitySlotBinding("alternate", new CharacterAbilityId(900099), "intent.guard")
                });
            EquipmentStateResolveResult equipment = EquipmentStateResolver.Resolve(
                fixture.EquipmentSchema,
                fixture.SwordShieldLoadout,
                fixture.Weapons,
                fixture.EquipmentStates);

            AbilityGrantResolveResult result = AbilityGrantResolver.Resolve(new AbilityGrantResolveRequest(
                fixture.Character,
                equipment.EquippedWeapons,
                fixture.SwordShieldState,
                fixture.AbilityLoadouts,
                runtimeGrant,
                Array.Empty<CharacterAbilityId>(),
                fixture.KnownAbilityIds));

            AssertDiagnostic(result.Diagnostics, CharacterDiagnosticCode.DuplicateAbilityGrant);
            AssertDiagnostic(result.Diagnostics, CharacterDiagnosticCode.AbilitySlotConflict);
            AssertDiagnostic(result.Diagnostics, CharacterDiagnosticCode.AbilityInputIntentConflict);
        }

        [Test]
        public void BodyPartHitZoneResolver_MapsExplicitWeakPointAndReportsUnmappedHitZone()
        {
            IronVanguardFixture fixture = IronVanguardFixture.Create();

            BodyPartHitZoneResolveResult head = BodyPartHitZoneResolver.Resolve(fixture.BodyProfile, fixture.BodyParts, "hit.head");
            BodyPartHitZoneResolveResult missing = BodyPartHitZoneResolver.Resolve(fixture.BodyProfile, fixture.BodyParts, "hit.tail");

            Assert.AreEqual(BodyPartHitZoneResolveStatus.Success, head.Status);
            Assert.AreEqual("head", head.PartId);
            Assert.IsTrue(head.IsWeakPoint);
            Assert.AreEqual(1.5f, head.DamageMultiplier);
            Assert.AreEqual(BodyPartHitZoneResolveStatus.UnmappedHitZone, missing.Status);
            AssertDiagnostic(missing.Diagnostics, CharacterDiagnosticCode.UnmappedHitZone);
        }

        [Test]
        public void CombatActionBindingResolver_WhenActionDataInvalid_ReportsMissingActionAndAnimation()
        {
            var actionSet = new CombatActionSetConfig(
                new CombatActionSetId(800099),
                "mx.action_set.invalid",
                new[] { new CombatActionEntry("primary", new CharacterCombatActionId(0), string.Empty, string.Empty) });

            CombatActionBindingResolveResult result = CombatActionBindingResolver.Resolve(actionSet);

            Assert.IsTrue(result.HasErrors);
            AssertDiagnostic(result.Diagnostics, CharacterDiagnosticCode.MissingCombatAction);
            AssertDiagnostic(result.Diagnostics, CharacterDiagnosticCode.MissingAnimationAction);
        }

        [Test]
        public void ResourceDependencyResolver_WhenResourceKeyMissing_ReportsDiagnostic()
        {
            IronVanguardFixture fixture = IronVanguardFixture.Create();
            var presentation = new CharacterPresentationProfileConfig(
                fixture.Presentation.PresentationProfileId,
                fixture.Presentation.StableId,
                fixture.Presentation.DefaultAnimationProfileId,
                new[] { new CharacterResourceKeyEntry(string.Empty, "GameObject", CharacterResourceUsageKind.Model) },
                Array.Empty<string>());

            ResourceDependencyResolveResult result = ResourceDependencyResolver.Resolve(
                presentation,
                Array.Empty<EquippedWeaponSlot>(),
                fixture.SwordShieldState,
                fixture.SwordShieldActionSet);

            AssertDiagnostic(result.Diagnostics, CharacterDiagnosticCode.MissingResourceKey);
        }

        [Test]
        public void CharacterPackageResolver_WhenAbilityLoadoutMissing_ReportsStableDiagnostic()
        {
            IronVanguardFixture fixture = IronVanguardFixture.Create();
            AbilityLoadoutConfig[] missingSwordLoadout = fixture.AbilityLoadouts
                .Where(loadout => !loadout.LoadoutId.Equals(fixture.SwordAbilityLoadout.LoadoutId))
                .ToArray();

            CharacterPackageResolveResult result = CharacterPackageResolver.Resolve(fixture.CreatePackageRequest(fixture.SwordShieldLoadout, abilityLoadouts: missingSwordLoadout));

            Assert.IsTrue(result.ValidationReport.HasErrors);
            AssertDiagnostic(result.ValidationReport.Issues, CharacterDiagnosticCode.MissingAbilityLoadout);
        }

        [Test]
        public void SpawnAndSaveStateResolvers_ValidateOverridesAndActiveStateExpectations()
        {
            IronVanguardFixture fixture = IronVanguardFixture.Create();
            var spawnProfile = new SpawnProfileConfig(
                new SpawnProfileId(820001),
                "mx.spawn.iron_vanguard.default",
                fixture.Character.CharacterId,
                "team.blue",
                CharacterControllerKind.HumanInput,
                fixture.SwordShieldLoadout.LoadoutId,
                new CharacterPoseEntry("spawn.a", 1f, 0f, 2f, 90f),
                string.Empty,
                "Iron Vanguard");
            var request = new CharacterSpawnRequest(
                spawnProfile.SpawnProfileId,
                loadoutOverride: fixture.SingleSwordLoadout.LoadoutId,
                teamOverride: "team.red",
                debugNameOverride: "Override Vanguard");

            CharacterSpawnPlan plan = SpawnPlanResolver.Resolve(spawnProfile, request);
            EquipmentStateResolveResult equipment = EquipmentStateResolver.Resolve(fixture.EquipmentSchema, fixture.SingleSwordLoadout, fixture.Weapons, fixture.EquipmentStates);
            SaveStateBindingResolveResult save = SaveStateBindingResolver.Resolve(
                new CharacterSaveStateBinding(
                    fixture.Character.CharacterId,
                    fixture.Character.StableId,
                    1,
                    fixture.SingleSwordLoadout.LoadoutId,
                    fixture.SwordShieldState.StateId),
                fixture.Character,
                equipment);

            Assert.AreEqual(fixture.SingleSwordLoadout.LoadoutId, plan.LoadoutId);
            Assert.AreEqual("team.red", plan.TeamId);
            Assert.AreEqual("Override Vanguard", plan.DebugName);
            Assert.IsTrue(save.CanReconstruct);
            AssertDiagnostic(save.Diagnostics, CharacterDiagnosticCode.SaveStateActiveStateMismatch);
        }

        private static void AssertDiagnostic(CharacterDiagnostic[] diagnostics, CharacterDiagnosticCode code)
        {
            Assert.IsTrue(diagnostics.Any(diagnostic => diagnostic.Code == code), "Expected diagnostic " + CharacterDiagnosticCodes.ToStableCode(code));
        }

        private sealed class IronVanguardFixture
        {
            public CharacterConfig Character { get; private set; }
            public CharacterAttributeProfileConfig Attributes { get; private set; }
            public CharacterBodyProfileConfig BodyProfile { get; private set; }
            public CharacterBodyPartConfig[] BodyParts { get; private set; }
            public EquipmentSchemaConfig EquipmentSchema { get; private set; }
            public EquipmentLoadoutConfig UnarmedLoadout { get; private set; }
            public EquipmentLoadoutConfig SingleSwordLoadout { get; private set; }
            public EquipmentLoadoutConfig SwordShieldLoadout { get; private set; }
            public EquipmentStateConfig UnarmedState { get; private set; }
            public EquipmentStateConfig SingleSwordState { get; private set; }
            public EquipmentStateConfig SwordShieldState { get; private set; }
            public WeaponConfig Sword { get; private set; }
            public WeaponConfig Shield { get; private set; }
            public WeaponConfig[] Weapons { get; private set; }
            public AbilityLoadoutConfig BaseAbilityLoadout { get; private set; }
            public AbilityLoadoutConfig SwordAbilityLoadout { get; private set; }
            public AbilityLoadoutConfig ShieldAbilityLoadout { get; private set; }
            public AbilityLoadoutConfig SwordShieldAbilityLoadout { get; private set; }
            public AbilityLoadoutConfig[] AbilityLoadouts { get; private set; }
            public CombatActionSetConfig UnarmedActionSet { get; private set; }
            public CombatActionSetConfig SingleSwordActionSet { get; private set; }
            public CombatActionSetConfig SwordShieldActionSet { get; private set; }
            public CombatActionSetConfig[] CombatActionSets { get; private set; }
            public CharacterPresentationProfileConfig Presentation { get; private set; }
            public EquipmentStateConfig[] EquipmentStates => new[] { UnarmedState, SingleSwordState, SwordShieldState };
            public CharacterAbilityId[] KnownAbilityIds => new[]
            {
                new CharacterAbilityId(900001),
                new CharacterAbilityId(900002),
                new CharacterAbilityId(900003),
                new CharacterAbilityId(900004),
                new CharacterAbilityId(900099)
            };

            public static IronVanguardFixture Create()
            {
                var fixture = new IronVanguardFixture();
                fixture.CreateCore();
                fixture.CreateEquipment();
                fixture.CreateAbilities();
                fixture.CreateActions();
                fixture.CreatePresentation();
                fixture.Character = new CharacterConfig(
                    new CharacterConfigId(710001),
                    "mx.character.iron_vanguard",
                    new LocalizedTextKey("character.iron_vanguard.name"),
                    new LocalizedTextKey("character.iron_vanguard.desc"),
                    fixture.Attributes.AttributeProfileId,
                    fixture.BodyProfile.BodyProfileId,
                    fixture.EquipmentSchema.EquipmentSchemaId,
                    fixture.SwordShieldLoadout.LoadoutId,
                    fixture.BaseAbilityLoadout.LoadoutId,
                    fixture.Presentation.PresentationProfileId,
                    CharacterControllerKind.HumanInput,
                    "controller.human.default",
                    new[] { "sample", "vanguard" });
                return fixture;
            }

            public CharacterPackageResolveRequest CreatePackageRequest(
                EquipmentLoadoutConfig loadout,
                AbilityLoadoutConfig[] abilityLoadouts = null)
            {
                return new CharacterPackageResolveRequest(
                    Character,
                    Attributes,
                    BodyProfile,
                    BodyParts,
                    EquipmentSchema,
                    loadout,
                    EquipmentStates,
                    Weapons,
                    abilityLoadouts ?? AbilityLoadouts,
                    CombatActionSets,
                    Presentation,
                    knownAbilityIds: KnownAbilityIds);
            }

            public EquipmentSchemaConfig CreateSchemaWithStates(EquipmentStateId[] stateIds)
            {
                return new EquipmentSchemaConfig(
                    EquipmentSchema.EquipmentSchemaId,
                    EquipmentSchema.StableId,
                    EquipmentSchema.Slots,
                    EquipmentSchema.ExclusiveGroups,
                    stateIds);
            }

            private void CreateCore()
            {
                Attributes = new CharacterAttributeProfileConfig(
                    new CharacterAttributeProfileId(720001),
                    "mx.character_attr.iron_vanguard",
                    new[]
                    {
                        new CharacterAttributeEntry(new CharacterAttributeId(920001), "attr.hp", CharacterAttributeGroup.Vital, 160f, 160f, 0f, 160f),
                        new CharacterAttributeEntry(new CharacterAttributeId(920002), "attr.stamina", CharacterAttributeGroup.Resource, 100f, 100f, 0f, 100f),
                        new CharacterAttributeEntry(new CharacterAttributeId(920003), "attr.posture", CharacterAttributeGroup.Pressure, 80f, 80f, 0f, 80f)
                    });
                BodyProfile = new CharacterBodyProfileConfig(
                    new CharacterBodyProfileId(730001),
                    "mx.body.iron_vanguard.humanoid",
                    CharacterBodyKind.Humanoid,
                    "iron_vanguard_parts",
                    "motion.humanoid.medium",
                    "physics.humanoid.medium",
                    new[]
                    {
                        new CharacterSocketEntry("socket.weapon.main", "right_hand", "loc.weapon.main"),
                        new CharacterSocketEntry("socket.weapon.off", "left_hand", "loc.weapon.off")
                    },
                    new[] { new CharacterHitZoneBindingEntry("hit.head", "head", 10, true, 1.5f, 2f) });
                BodyParts = new[]
                {
                    new CharacterBodyPartConfig(new CharacterBodyPartConfigId(740001), "mx.body_part.iron_vanguard.root", "iron_vanguard_parts", "root", string.Empty, CharacterBodyPartKind.Root, "loc.root", "hit.root", "react.body", 1f, 1f, 1f, 1f, false),
                    new CharacterBodyPartConfig(new CharacterBodyPartConfigId(740002), "mx.body_part.iron_vanguard.torso", "iron_vanguard_parts", "torso", "root", CharacterBodyPartKind.Torso, "loc.torso", "hit.torso", "react.body", 1f, 1f, 1f, 1f, false),
                    new CharacterBodyPartConfig(new CharacterBodyPartConfigId(740003), "mx.body_part.iron_vanguard.head", "iron_vanguard_parts", "head", "torso", CharacterBodyPartKind.Head, "loc.head", "hit.head", "react.head", 1.25f, 0.8f, 1.2f, 1.4f, true),
                    new CharacterBodyPartConfig(new CharacterBodyPartConfigId(740004), "mx.body_part.iron_vanguard.right_hand", "iron_vanguard_parts", "right_hand", "torso", CharacterBodyPartKind.Hand, "loc.hand.r", "hit.hand.r", "react.limb", 0.75f, 0.7f, 0.8f, 0.7f, false),
                    new CharacterBodyPartConfig(new CharacterBodyPartConfigId(740005), "mx.body_part.iron_vanguard.left_hand", "iron_vanguard_parts", "left_hand", "torso", CharacterBodyPartKind.Hand, "loc.hand.l", "hit.hand.l", "react.limb", 0.75f, 0.7f, 0.8f, 0.7f, false)
                };
            }

            private void CreateEquipment()
            {
                EquipmentSchema = new EquipmentSchemaConfig(
                    new EquipmentSchemaId(750001),
                    "mx.equipment_schema.humanoid.hands",
                    new[]
                    {
                        new EquipmentSlotEntry("mainHand", EquipmentSlotKind.MainHand, "Main Hand", new[] { WeaponCategory.OneHandMelee.ToString() }),
                        new EquipmentSlotEntry("offHand", EquipmentSlotKind.OffHand, "Off Hand", new[] { WeaponCategory.Shield.ToString() })
                    },
                    new[] { new EquipmentExclusiveGroupEntry("hands", new[] { "mainHand", "offHand" }, 2) },
                    new[] { new EquipmentStateId(770001), new EquipmentStateId(770002), new EquipmentStateId(770003) });
                Sword = new WeaponConfig(
                    new WeaponConfigId(780001),
                    "mx.weapon.iron_vanguard.sword",
                    WeaponCategory.OneHandMelee,
                    new[] { "blade", "one_hand" },
                    new[] { "mainHand" },
                    new AbilityLoadoutId(790002),
                    "presentation.weapon.sword",
                    "combat.weapon.sword",
                    "resource.weapon.sword",
                    new[] { new CharacterResourceKeyEntry("art.weapon.iron_vanguard.sword.prefab", "GameObject", CharacterResourceUsageKind.WeaponModel, preloadGroupId: "character.iron_vanguard") },
                    new[] { new CharacterTraceBindingEntry("trace.iron_vanguard.sword", "mainHand", "socket.weapon.main", 1.4f, 0.12f) },
                    Array.Empty<CharacterModifierGrantEntry>());
                Shield = new WeaponConfig(
                    new WeaponConfigId(780002),
                    "mx.weapon.iron_vanguard.shield",
                    WeaponCategory.Shield,
                    new[] { "shield", "guard" },
                    new[] { "offHand" },
                    new AbilityLoadoutId(790003),
                    "presentation.weapon.shield",
                    "combat.weapon.shield",
                    "resource.weapon.shield",
                    new[] { new CharacterResourceKeyEntry("art.weapon.iron_vanguard.shield.prefab", "GameObject", CharacterResourceUsageKind.WeaponModel, preloadGroupId: "character.iron_vanguard") },
                    new[] { new CharacterTraceBindingEntry("trace.iron_vanguard.shield", "offHand", "socket.weapon.off", 0.8f, 0.2f) },
                    Array.Empty<CharacterModifierGrantEntry>());
                Weapons = new[] { Sword, Shield };
                UnarmedLoadout = new EquipmentLoadoutConfig(new EquipmentLoadoutId(760001), "mx.loadout.iron_vanguard.unarmed", EquipmentSchema.EquipmentSchemaId, Array.Empty<EquipmentLoadoutSlotEntry>());
                SingleSwordLoadout = new EquipmentLoadoutConfig(new EquipmentLoadoutId(760002), "mx.loadout.iron_vanguard.single_sword", EquipmentSchema.EquipmentSchemaId, new[] { new EquipmentLoadoutSlotEntry("mainHand", Sword.WeaponId, "weapon.instance.sword.001") });
                SwordShieldLoadout = new EquipmentLoadoutConfig(new EquipmentLoadoutId(760003), "mx.loadout.iron_vanguard.sword_shield", EquipmentSchema.EquipmentSchemaId, new[] { new EquipmentLoadoutSlotEntry("mainHand", Sword.WeaponId, "weapon.instance.sword.001"), new EquipmentLoadoutSlotEntry("offHand", Shield.WeaponId, "weapon.instance.shield.001") });
                UnarmedState = new EquipmentStateConfig(new EquipmentStateId(770001), "mx.equipment_state.iron_vanguard.unarmed", EquipmentSchema.EquipmentSchemaId, 0, new[] { new EquipmentMatchRule(Array.Empty<string>(), new[] { "mainHand", "offHand" }, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>()) }, new AbilityLoadoutId(0), new CombatActionSetId(800001), "anim.iron_vanguard.unarmed", Array.Empty<string>());
                SingleSwordState = new EquipmentStateConfig(new EquipmentStateId(770002), "mx.equipment_state.iron_vanguard.single_sword", EquipmentSchema.EquipmentSchemaId, 10, new[] { new EquipmentMatchRule(new[] { "mainHand" }, new[] { "offHand" }, new[] { "mainHand:OneHandMelee" }, new[] { "mainHand:blade" }, Array.Empty<string>()) }, new AbilityLoadoutId(0), new CombatActionSetId(800002), "anim.iron_vanguard.single_sword", Array.Empty<string>());
                SwordShieldState = new EquipmentStateConfig(new EquipmentStateId(770003), "mx.equipment_state.iron_vanguard.sword_shield", EquipmentSchema.EquipmentSchemaId, 20, new[] { new EquipmentMatchRule(new[] { "mainHand", "offHand" }, Array.Empty<string>(), new[] { "mainHand:OneHandMelee", "offHand:Shield" }, new[] { "mainHand:blade", "offHand:shield" }, Array.Empty<string>()) }, new AbilityLoadoutId(790004), new CombatActionSetId(800003), "anim.iron_vanguard.sword_shield", Array.Empty<string>());
            }

            private void CreateAbilities()
            {
                BaseAbilityLoadout = new AbilityLoadoutConfig(new AbilityLoadoutId(790001), "mx.ability_loadout.iron_vanguard.base", new[] { new CharacterAbilityId(900001) }, Array.Empty<CharacterAbilityId>(), new[] { new AbilitySlotBinding("evade", new CharacterAbilityId(900001), "intent.dodge") });
                SwordAbilityLoadout = new AbilityLoadoutConfig(new AbilityLoadoutId(790002), "mx.ability_loadout.iron_vanguard.sword", new[] { new CharacterAbilityId(900002) }, Array.Empty<CharacterAbilityId>(), new[] { new AbilitySlotBinding("primary", new CharacterAbilityId(900002), "intent.primary") });
                ShieldAbilityLoadout = new AbilityLoadoutConfig(new AbilityLoadoutId(790003), "mx.ability_loadout.iron_vanguard.shield", new[] { new CharacterAbilityId(900003) }, Array.Empty<CharacterAbilityId>(), new[] { new AbilitySlotBinding("guard", new CharacterAbilityId(900003), "intent.guard") });
                SwordShieldAbilityLoadout = new AbilityLoadoutConfig(new AbilityLoadoutId(790004), "mx.ability_loadout.iron_vanguard.sword_shield", new[] { new CharacterAbilityId(900004) }, Array.Empty<CharacterAbilityId>(), new[] { new AbilitySlotBinding("secondary", new CharacterAbilityId(900004), "intent.secondary") });
                AbilityLoadouts = new[] { BaseAbilityLoadout, SwordAbilityLoadout, ShieldAbilityLoadout, SwordShieldAbilityLoadout };
            }

            private void CreateActions()
            {
                UnarmedActionSet = new CombatActionSetConfig(new CombatActionSetId(800001), "mx.action_set.iron_vanguard.unarmed", new[] { new CombatActionEntry("primary", new CharacterCombatActionId(910001), string.Empty, "punch") });
                SingleSwordActionSet = new CombatActionSetConfig(new CombatActionSetId(800002), "mx.action_set.iron_vanguard.single_sword", new[] { new CombatActionEntry("primary", new CharacterCombatActionId(910002), "trace.iron_vanguard.sword", "slash") });
                SwordShieldActionSet = new CombatActionSetConfig(new CombatActionSetId(800003), "mx.action_set.iron_vanguard.sword_shield", new[] { new CombatActionEntry("primary", new CharacterCombatActionId(910002), "trace.iron_vanguard.sword", "slash"), new CombatActionEntry("guard", new CharacterCombatActionId(910003), "trace.iron_vanguard.shield", "shield_guard") });
                CombatActionSets = new[] { UnarmedActionSet, SingleSwordActionSet, SwordShieldActionSet };
            }

            private void CreatePresentation()
            {
                Presentation = new CharacterPresentationProfileConfig(
                    new CharacterPresentationProfileId(810001),
                    "mx.presentation.iron_vanguard",
                    "anim.iron_vanguard.default",
                    new[]
                    {
                        new CharacterResourceKeyEntry("art.character.iron_vanguard.prefab", "GameObject", CharacterResourceUsageKind.Model, preloadGroupId: "character.iron_vanguard"),
                        new CharacterResourceKeyEntry("ui.character.iron_vanguard.icon", "Sprite", CharacterResourceUsageKind.Ui)
                    },
                    new[] { "humanoid", "armored" });
            }
        }
    }
}
