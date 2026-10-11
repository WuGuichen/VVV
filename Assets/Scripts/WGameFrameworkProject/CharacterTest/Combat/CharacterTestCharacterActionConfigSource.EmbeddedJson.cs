namespace MxFramework.Game.CharacterTest
{
    public static partial class CharacterTestCharacterActionConfigSource
    {
        private const string EmbeddedActionSetsSourcePath = "CharacterTest/embedded/character_action_sets.json";
        private const string EmbeddedActionsSourcePath = "CharacterTest/embedded/character_actions.json";
        private const string EmbeddedReactionProfilesSourcePath = "CharacterTest/embedded/character_reaction_profiles.json";
        private const string EmbeddedLandingProfilesSourcePath = "CharacterTest/embedded/character_landing_profiles.json";

        private const string ActionSetsJson = @"
{
  ""schemaVersion"": ""0.6.0"",
  ""actionSets"": [
    {
      ""id"": 2,
      ""stableId"": ""codex_flow_test.empty_empty"",
      ""displayName"": ""空手 / 空手"",
      ""characterStableId"": ""char.codex_flow_test"",
      ""equipmentStateStableId"": ""equip_state.codex_flow_test.empty_empty"",
      ""movementProfileId"": 1,
      ""reactionProfileId"": ""reaction.codex_flow_test.empty_empty"",
      ""landingProfileId"": ""landing.codex_flow_test.default"",
      ""defaultActionId"": 0,
      ""defaultActionSlotMask"": 415,
      ""commandBindings"": [
        {
          ""intentId"": 60,
          ""actionId"": 930000,
          ""priority"": 10,
          ""allowQueue"": false,
          ""queueWindowFrames"": 0,
          ""intentLabel"": ""intent.dodge""
        }
      ],
      ""commandSequences"": [
        {
          ""intentId"": 59,
          ""actionIds"": [
            301,
            324,
            325
          ],
          ""wrapAfterLast"": true,
          ""priority"": 10,
          ""allowQueue"": false,
          ""queueWindowFrames"": 0,
          ""intentLabel"": ""LightAttack""
        }
      ],
      ""abilityBindings"": [
        {
          ""abilityId"": 900001,
          ""actionId"": 930000,
          ""requiredTags"": [],
          ""forbiddenTags"": []
        }
      ],
      ""reactionBindings"": [
        {
          ""reactionProfileId"": ""reaction.codex_flow_test.empty_empty"",
          ""defaultActionId"": 332,
          ""requiredTags"": [],
          ""forbiddenTags"": []
        }
      ],
      ""movementProfileLabel"": ""movement.default""
    },
    {
      ""id"": 3,
      ""stableId"": ""codex_flow_test.sword_empty"",
      ""displayName"": ""剑 / 空手"",
      ""characterStableId"": ""char.codex_flow_test"",
      ""equipmentStateStableId"": ""equip_state.codex_flow_test.sword_empty"",
      ""movementProfileId"": 1,
      ""reactionProfileId"": ""reaction.codex_flow_test.sword_empty"",
      ""landingProfileId"": ""landing.codex_flow_test.default"",
      ""defaultActionId"": 0,
      ""defaultActionSlotMask"": 415,
      ""commandBindings"": [
        {
          ""intentId"": 60,
          ""actionId"": 930100,
          ""priority"": 10,
          ""allowQueue"": false,
          ""queueWindowFrames"": 0,
          ""intentLabel"": ""intent.dodge""
        },
        {
          ""intentId"": 86,
          ""actionId"": 930101,
          ""priority"": 10,
          ""allowQueue"": false,
          ""queueWindowFrames"": 0,
          ""intentLabel"": ""intent.secondary""
        }
      ],
      ""commandSequences"": [
        {
          ""intentId"": 59,
          ""actionIds"": [
            301,
            324,
            325
          ],
          ""wrapAfterLast"": true,
          ""priority"": 10,
          ""allowQueue"": false,
          ""queueWindowFrames"": 0,
          ""intentLabel"": ""LightAttack""
        }
      ],
      ""abilityBindings"": [
        {
          ""abilityId"": 900001,
          ""actionId"": 930100,
          ""requiredTags"": [],
          ""forbiddenTags"": []
        },
        {
          ""abilityId"": 900102,
          ""actionId"": 930101,
          ""requiredTags"": [],
          ""forbiddenTags"": []
        }
      ],
      ""reactionBindings"": [
        {
          ""reactionProfileId"": ""reaction.codex_flow_test.sword_empty"",
          ""defaultActionId"": 332,
          ""requiredTags"": [],
          ""forbiddenTags"": []
        }
      ],
      ""movementProfileLabel"": ""movement.default""
    }
  ]
}";

        private const string ReactionProfilesJson = @"
{
  ""schemaVersion"": ""0.6.0"",
  ""reactionProfiles"": [
    {
      ""stableId"": ""reaction.codex_flow_test.empty_empty"",
      ""displayName"": ""Default reaction profile / 空手 / 空手"",
      ""defaultActionId"": 332,
      ""rules"": [
        {
          ""actionId"": 930001,
          ""trigger"": ""Death"",
          ""currentScatterPressureBand"": """",
          ""priority"": 200,
          ""minImpactForce"": null,
          ""maxImpactForce"": null,
          ""reactionGroupId"": 0,
          ""requiresImpactForce"": false,
          ""reactionGroupLabel"": """"
        },
        {
          ""actionId"": 362,
          ""trigger"": ""Lifecycle"",
          ""currentScatterPressureBand"": """",
          ""priority"": 150,
          ""minImpactForce"": null,
          ""maxImpactForce"": null,
          ""reactionGroupId"": 0,
          ""requiresImpactForce"": false,
          ""reactionGroupLabel"": """"
        },
        {
          ""actionId"": 330,
          ""trigger"": ""CohesionBreak"",
          ""currentScatterPressureBand"": ""Broken"",
          ""priority"": 120,
          ""minImpactForce"": null,
          ""maxImpactForce"": null,
          ""reactionGroupId"": 0,
          ""requiresImpactForce"": false,
          ""reactionGroupLabel"": """"
        },
        {
          ""actionId"": 363,
          ""trigger"": ""Hit"",
          ""currentScatterPressureBand"": ""Broken"",
          ""priority"": 115,
          ""minImpactForce"": null,
          ""maxImpactForce"": null,
          ""reactionGroupId"": 4,
          ""requiresImpactForce"": false,
          ""reactionGroupLabel"": ""reaction.special.launch""
        },
        {
          ""actionId"": 334,
          ""trigger"": ""Hit"",
          ""currentScatterPressureBand"": """",
          ""priority"": 110,
          ""minImpactForce"": 60,
          ""maxImpactForce"": null,
          ""reactionGroupId"": 1,
          ""requiresImpactForce"": true,
          ""reactionGroupLabel"": ""reaction.height.high""
        },
        {
          ""actionId"": 335,
          ""trigger"": ""Hit"",
          ""currentScatterPressureBand"": """",
          ""priority"": 109,
          ""minImpactForce"": 60,
          ""maxImpactForce"": null,
          ""reactionGroupId"": 2,
          ""requiresImpactForce"": true,
          ""reactionGroupLabel"": ""reaction.height.mid""
        },
        {
          ""actionId"": 336,
          ""trigger"": ""Hit"",
          ""currentScatterPressureBand"": """",
          ""priority"": 108,
          ""minImpactForce"": 60,
          ""maxImpactForce"": null,
          ""reactionGroupId"": 3,
          ""requiresImpactForce"": true,
          ""reactionGroupLabel"": ""reaction.height.low""
        },
        {
          ""actionId"": 331,
          ""trigger"": ""Hit"",
          ""currentScatterPressureBand"": """",
          ""priority"": 90,
          ""minImpactForce"": null,
          ""maxImpactForce"": 59,
          ""reactionGroupId"": 1,
          ""requiresImpactForce"": true,
          ""reactionGroupLabel"": ""reaction.height.high""
        },
        {
          ""actionId"": 332,
          ""trigger"": ""Hit"",
          ""currentScatterPressureBand"": """",
          ""priority"": 89,
          ""minImpactForce"": null,
          ""maxImpactForce"": 59,
          ""reactionGroupId"": 2,
          ""requiresImpactForce"": true,
          ""reactionGroupLabel"": ""reaction.height.mid""
        },
        {
          ""actionId"": 333,
          ""trigger"": ""Hit"",
          ""currentScatterPressureBand"": """",
          ""priority"": 88,
          ""minImpactForce"": null,
          ""maxImpactForce"": 59,
          ""reactionGroupId"": 3,
          ""requiresImpactForce"": true,
          ""reactionGroupLabel"": ""reaction.height.low""
        },
        {
          ""actionId"": 332,
          ""trigger"": ""Hit"",
          ""currentScatterPressureBand"": """",
          ""priority"": 60,
          ""minImpactForce"": null,
          ""maxImpactForce"": null,
          ""reactionGroupId"": 0,
          ""requiresImpactForce"": false,
          ""reactionGroupLabel"": """"
        }
      ]
    },
    {
      ""stableId"": ""reaction.codex_flow_test.sword_empty"",
      ""displayName"": ""Default reaction profile / 剑 / 空手"",
      ""defaultActionId"": 332,
      ""rules"": [
        {
          ""actionId"": 930102,
          ""trigger"": ""Death"",
          ""currentScatterPressureBand"": """",
          ""priority"": 200,
          ""minImpactForce"": null,
          ""maxImpactForce"": null,
          ""reactionGroupId"": 0,
          ""requiresImpactForce"": false,
          ""reactionGroupLabel"": """"
        },
        {
          ""actionId"": 362,
          ""trigger"": ""Lifecycle"",
          ""currentScatterPressureBand"": """",
          ""priority"": 150,
          ""minImpactForce"": null,
          ""maxImpactForce"": null,
          ""reactionGroupId"": 0,
          ""requiresImpactForce"": false,
          ""reactionGroupLabel"": """"
        },
        {
          ""actionId"": 330,
          ""trigger"": ""CohesionBreak"",
          ""currentScatterPressureBand"": ""Broken"",
          ""priority"": 120,
          ""minImpactForce"": null,
          ""maxImpactForce"": null,
          ""reactionGroupId"": 0,
          ""requiresImpactForce"": false,
          ""reactionGroupLabel"": """"
        },
        {
          ""actionId"": 363,
          ""trigger"": ""Hit"",
          ""currentScatterPressureBand"": ""Broken"",
          ""priority"": 115,
          ""minImpactForce"": null,
          ""maxImpactForce"": null,
          ""reactionGroupId"": 4,
          ""requiresImpactForce"": false,
          ""reactionGroupLabel"": ""reaction.special.launch""
        },
        {
          ""actionId"": 334,
          ""trigger"": ""Hit"",
          ""currentScatterPressureBand"": """",
          ""priority"": 110,
          ""minImpactForce"": 60,
          ""maxImpactForce"": null,
          ""reactionGroupId"": 1,
          ""requiresImpactForce"": true,
          ""reactionGroupLabel"": ""reaction.height.high""
        },
        {
          ""actionId"": 335,
          ""trigger"": ""Hit"",
          ""currentScatterPressureBand"": """",
          ""priority"": 109,
          ""minImpactForce"": 60,
          ""maxImpactForce"": null,
          ""reactionGroupId"": 2,
          ""requiresImpactForce"": true,
          ""reactionGroupLabel"": ""reaction.height.mid""
        },
        {
          ""actionId"": 336,
          ""trigger"": ""Hit"",
          ""currentScatterPressureBand"": """",
          ""priority"": 108,
          ""minImpactForce"": 60,
          ""maxImpactForce"": null,
          ""reactionGroupId"": 3,
          ""requiresImpactForce"": true,
          ""reactionGroupLabel"": ""reaction.height.low""
        },
        {
          ""actionId"": 331,
          ""trigger"": ""Hit"",
          ""currentScatterPressureBand"": """",
          ""priority"": 90,
          ""minImpactForce"": null,
          ""maxImpactForce"": 59,
          ""reactionGroupId"": 1,
          ""requiresImpactForce"": true,
          ""reactionGroupLabel"": ""reaction.height.high""
        },
        {
          ""actionId"": 332,
          ""trigger"": ""Hit"",
          ""currentScatterPressureBand"": """",
          ""priority"": 89,
          ""minImpactForce"": null,
          ""maxImpactForce"": 59,
          ""reactionGroupId"": 2,
          ""requiresImpactForce"": true,
          ""reactionGroupLabel"": ""reaction.height.mid""
        },
        {
          ""actionId"": 333,
          ""trigger"": ""Hit"",
          ""currentScatterPressureBand"": """",
          ""priority"": 88,
          ""minImpactForce"": null,
          ""maxImpactForce"": 59,
          ""reactionGroupId"": 3,
          ""requiresImpactForce"": true,
          ""reactionGroupLabel"": ""reaction.height.low""
        },
        {
          ""actionId"": 332,
          ""trigger"": ""Hit"",
          ""currentScatterPressureBand"": """",
          ""priority"": 60,
          ""minImpactForce"": null,
          ""maxImpactForce"": null,
          ""reactionGroupId"": 0,
          ""requiresImpactForce"": false,
          ""reactionGroupLabel"": """"
        }
      ]
    }
  ]
}";

        private const string LandingProfilesJson = @"
{
  ""schemaVersion"": ""0.6.0"",
  ""landingProfiles"": [
    {
      ""stableId"": ""landing.codex_flow_test.default"",
      ""lightLandingActionId"": 398,
      ""heavyLandingActionId"": 399,
      ""lightLandingMinDownwardSpeedMillimetersPerSecond"": 1000,
      ""heavyLandingMinDownwardSpeedMillimetersPerSecond"": 8000
    }
  ]
}";

        private const string ActionsJson = @"
{
  ""schemaVersion"": ""0.5.0"",
  ""animationSources"": [
    {
      ""resourceKeyPrefix"": ""art.character.skeleton.animation."",
      ""rigType"": ""Humanoid"",
      ""skeletonProfileId"": ""skeleton.codex_flow_test.humanoid"",
      ""avatarProfileId"": ""avatar.codex_flow_test.humanoid"",
      ""allowRetargeting"": true
    }
  ],
  ""actions"": [
    {
      ""actionId"": 300,
      ""displayName"": ""Basic Jump"",
      ""category"": ""Jump"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""movement"",
        ""jump""
      ],
      ""priority"": 10,
      ""durationFrames"": 108,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Startup"",
          ""startFrame"": 0,
          ""endFrame"": 2
        },
        {
          ""kind"": ""Active"",
          ""startFrame"": 3,
          ""endFrame"": 16
        },
        {
          ""kind"": ""Recovery"",
          ""startFrame"": 17,
          ""endFrame"": 107
        }
      ],
      ""cancelRules"": [
        {
          ""startFrame"": 17,
          ""endFrame"": 107,
          ""requestTypeMask"": 0,
          ""actionSlotMask"": 384
        }
      ],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""kind"": ""ApplyImpulse"",
            ""movementMode"": ""Airborne"",
            ""x"": 0,
            ""y"": 12,
            ""z"": 0,
            ""stableEventId"": 152,
            ""stableEventLabel"": ""basic_jump.impulse"",
            ""stableEventOrder"": 19
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": null,
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 107,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": ""anim.basic_jump"",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.standing_jump"",
            ""animationStartOffsetFrames"": 34,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.08,
            ""stableEventId"": 153,
            ""stableEventLabel"": ""basic_jump.animation"",
            ""stableEventOrder"": 18
          }
        ]
      },
      ""presentationTrack"": null,
      ""debugTrack"": null
    },
    {
      ""actionId"": 398,
      ""displayName"": ""Light Landing"",
      ""category"": ""Movement"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""movement"",
        ""landing"",
        ""light""
      ],
      ""priority"": 10,
      ""durationFrames"": 55,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Landing"",
          ""startFrame"": 0,
          ""endFrame"": 54
        }
      ],
      ""cancelRules"": [
        {
          ""startFrame"": 9,
          ""endFrame"": 54,
          ""requestTypeMask"": 0,
          ""actionSlotMask"": 0
        }
      ],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 53,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 154,
            ""stableEventLabel"": ""landing_light.control_lock"",
            ""stableEventOrder"": 314
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": null,
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 54,
            ""playbackSpeed"": 1.5,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": ""anim.landing.light"",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.standing_jump_running_landing"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.08,
            ""stableEventId"": 155,
            ""stableEventLabel"": ""landing_light.animation"",
            ""stableEventOrder"": 313
          }
        ]
      },
      ""presentationTrack"": null,
      ""debugTrack"": null
    },
    {
      ""actionId"": 399,
      ""displayName"": ""Heavy Landing"",
      ""category"": ""Movement"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""movement"",
        ""landing"",
        ""heavy""
      ],
      ""priority"": 10,
      ""durationFrames"": 103,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Landing"",
          ""startFrame"": 0,
          ""endFrame"": 102
        }
      ],
      ""cancelRules"": [
        {
          ""startFrame"": 36,
          ""endFrame"": 102,
          ""requestTypeMask"": 0,
          ""actionSlotMask"": 0
        }
      ],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 101,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 156,
            ""stableEventLabel"": ""landing_heavy.control_lock"",
            ""stableEventOrder"": 312
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": null,
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 102,
            ""playbackSpeed"": 0.8,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": ""anim.landing.heavy"",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.standing_jump_running_landing"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.08,
            ""stableEventId"": 157,
            ""stableEventLabel"": ""landing_heavy.animation"",
            ""stableEventOrder"": 311
          }
        ]
      },
      ""presentationTrack"": null,
      ""debugTrack"": null
    },
    {
      ""actionId"": 301,
      ""displayName"": ""Light Attack"",
      ""category"": ""BasicAttack"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""combat"",
        ""light_attack""
      ],
      ""priority"": 20,
      ""durationFrames"": 76,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Startup"",
          ""startFrame"": 0,
          ""endFrame"": 17
        },
        {
          ""kind"": ""Active"",
          ""startFrame"": 18,
          ""endFrame"": 34
        },
        {
          ""kind"": ""Recovery"",
          ""startFrame"": 35,
          ""endFrame"": 75
        }
      ],
      ""cancelRules"": [
        {
          ""startFrame"": 35,
          ""endFrame"": 75,
          ""requestTypeMask"": 0,
          ""actionSlotMask"": 1
        }
      ],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": true,
        ""rootMotion"": {
          ""sourceAnimationEventId"": 158,
          ""sourceAnimationClipResourceKey"": ""art.character.skeleton.animation.katana_atk1"",
          ""space"": ""Local"",
          ""sampleTickRate"": 60,
          ""obstaclePolicy"": ""Default"",
          ""frames"": [
            {
              ""frame"": 0,
              ""x"": 0,
              ""y"": 0,
              ""z"": 0
            },
            {
              ""frame"": 1,
              ""x"": -0.00245633582,
              ""y"": 0.0,
              ""z"": 0.000965271145
            },
            {
              ""frame"": 2,
              ""x"": -0.00292111374,
              ""y"": 0.0,
              ""z"": 0.000428251922
            },
            {
              ""frame"": 3,
              ""x"": -0.00334803248,
              ""y"": 0.0,
              ""z"": 1.835078e-05
            },
            {
              ""frame"": 4,
              ""x"": -0.00373000652,
              ""y"": 0.0,
              ""z"": -0.000226762146
            },
            {
              ""frame"": 5,
              ""x"": -0.004086576,
              ""y"": 0.0,
              ""z"": -0.0004164651
            },
            {
              ""frame"": 6,
              ""x"": -0.00430831825,
              ""y"": 0.0,
              ""z"": -0.000547777861
            },
            {
              ""frame"": 7,
              ""x"": -0.004333769,
              ""y"": 0.0,
              ""z"": -0.0005391538
            },
            {
              ""frame"": 8,
              ""x"": -0.00416303426,
              ""y"": 0.0,
              ""z"": -0.0003937222
            },
            {
              ""frame"": 9,
              ""x"": -0.00379938819,
              ""y"": 0.0,
              ""z"": 0.000157631934
            },
            {
              ""frame"": 10,
              ""x"": -0.0032177642,
              ""y"": 0.0,
              ""z"": 0.000987038
            },
            {
              ""frame"": 11,
              ""x"": -0.00257419422,
              ""y"": 0.0,
              ""z"": 0.0017827861
            },
            {
              ""frame"": 12,
              ""x"": -0.0018679481,
              ""y"": 0.0,
              ""z"": 0.002555821
            },
            {
              ""frame"": 13,
              ""x"": -0.0010134615,
              ""y"": 0.0,
              ""z"": 0.0033801645
            },
            {
              ""frame"": 14,
              ""x"": -0.000150337815,
              ""y"": 0.0,
              ""z"": 0.00427543
            },
            {
              ""frame"": 15,
              ""x"": -0.000181432813,
              ""y"": 0.0,
              ""z"": 0.005215574
            },
            {
              ""frame"": 16,
              ""x"": -0.00071137026,
              ""y"": 0.0,
              ""z"": 0.006207112
            },
            {
              ""frame"": 17,
              ""x"": -0.000902239233,
              ""y"": 0.0,
              ""z"": 0.00736113265
            },
            {
              ""frame"": 18,
              ""x"": -0.0007280037,
              ""y"": 0.0,
              ""z"": 0.00870787352
            },
            {
              ""frame"": 19,
              ""x"": -0.0001662448,
              ""y"": 0.0,
              ""z"": 0.0102570429
            },
            {
              ""frame"": 20,
              ""x"": 0.000331714749,
              ""y"": 0.0,
              ""z"": 0.0117423236
            },
            {
              ""frame"": 21,
              ""x"": -0.0003025271,
              ""y"": 0.0,
              ""z"": 0.0135285854
            },
            {
              ""frame"": 22,
              ""x"": -0.0005989447,
              ""y"": 0.0,
              ""z"": 0.0194678
            },
            {
              ""frame"": 23,
              ""x"": 0.00256869569,
              ""y"": 0.0,
              ""z"": 0.0309990346
            },
            {
              ""frame"": 24,
              ""x"": 0.009620123,
              ""y"": 0.0,
              ""z"": 0.0431426
            },
            {
              ""frame"": 25,
              ""x"": 0.0155633055,
              ""y"": 0.0,
              ""z"": 0.0443900973
            },
            {
              ""frame"": 26,
              ""x"": 0.01508091,
              ""y"": 0.0,
              ""z"": 0.03456369
            },
            {
              ""frame"": 27,
              ""x"": 0.0111999512,
              ""y"": 0.0,
              ""z"": 0.0226155519
            },
            {
              ""frame"": 28,
              ""x"": 0.008279249,
              ""y"": 0.0,
              ""z"": 0.0124347806
            },
            {
              ""frame"": 29,
              ""x"": 0.005878091,
              ""y"": 0.0,
              ""z"": 0.00513696671
            },
            {
              ""frame"": 30,
              ""x"": 0.00463793427,
              ""y"": 0.0,
              ""z"": -0.0007109642
            },
            {
              ""frame"": 31,
              ""x"": 0.00351680443,
              ""y"": 0.0,
              ""z"": -0.00569674373
            },
            {
              ""frame"": 32,
              ""x"": 0.00190509856,
              ""y"": 0.0,
              ""z"": -0.009835869
            },
            {
              ""frame"": 33,
              ""x"": 0.0006736927,
              ""y"": 0.0,
              ""z"": -0.01232478
            },
            {
              ""frame"": 34,
              ""x"": -0.00025786832,
              ""y"": 0.0,
              ""z"": -0.0129666924
            },
            {
              ""frame"": 35,
              ""x"": -0.0008182116,
              ""y"": 0.0,
              ""z"": -0.0123230219
            },
            {
              ""frame"": 36,
              ""x"": -0.0009388998,
              ""y"": 0.0,
              ""z"": -0.0117533207
            },
            {
              ""frame"": 37,
              ""x"": -0.000899426639,
              ""y"": 0.0,
              ""z"": -0.0115836412
            },
            {
              ""frame"": 38,
              ""x"": -0.0006653704,
              ""y"": 0.0,
              ""z"": -0.0114160329
            },
            {
              ""frame"": 39,
              ""x"": -0.000396218151,
              ""y"": 0.0,
              ""z"": -0.0112867057
            },
            {
              ""frame"": 40,
              ""x"": -0.00011728704,
              ""y"": 0.0,
              ""z"": -0.01118657
            },
            {
              ""frame"": 41,
              ""x"": 0.00018081069,
              ""y"": 0.0,
              ""z"": -0.0110125244
            },
            {
              ""frame"": 42,
              ""x"": 0.0004980825,
              ""y"": 0.0,
              ""z"": -0.0107138157
            },
            {
              ""frame"": 43,
              ""x"": 0.0008617714,
              ""y"": 0.0,
              ""z"": -0.0102881193
            },
            {
              ""frame"": 44,
              ""x"": 0.000949405134,
              ""y"": 0.0,
              ""z"": -0.00990814
            },
            {
              ""frame"": 45,
              ""x"": 0.000966552645,
              ""y"": 0.0,
              ""z"": -0.009494171
            },
            {
              ""frame"": 46,
              ""x"": 0.001227621,
              ""y"": 0.0,
              ""z"": -0.008907482
            },
            {
              ""frame"": 47,
              ""x"": 0.00145432353,
              ""y"": 0.0,
              ""z"": -0.008281097
            },
            {
              ""frame"": 48,
              ""x"": 0.00153854117,
              ""y"": 0.0,
              ""z"": -0.007847831
            },
            {
              ""frame"": 49,
              ""x"": 0.00142627582,
              ""y"": 0.0,
              ""z"": -0.00800208747
            },
            {
              ""frame"": 50,
              ""x"": 0.00109624863,
              ""y"": 0.0,
              ""z"": -0.008273423
            },
            {
              ""frame"": 51,
              ""x"": 0.0008387454,
              ""y"": 0.0,
              ""z"": -0.008375347
            },
            {
              ""frame"": 52,
              ""x"": 0.000414971262,
              ""y"": 0.0,
              ""z"": -0.008315623
            },
            {
              ""frame"": 53,
              ""x"": -0.000454902649,
              ""y"": 0.0,
              ""z"": -0.008497193
            },
            {
              ""frame"": 54,
              ""x"": -0.0009011142,
              ""y"": 0.0,
              ""z"": -0.00831958652
            },
            {
              ""frame"": 55,
              ""x"": -0.00112262368,
              ""y"": 0.0,
              ""z"": -0.007975146
            },
            {
              ""frame"": 56,
              ""x"": -0.00133978575,
              ""y"": 0.0,
              ""z"": -0.007598892
            },
            {
              ""frame"": 57,
              ""x"": -0.0015386343,
              ""y"": 0.0,
              ""z"": -0.0071728304
            },
            {
              ""frame"": 58,
              ""x"": -0.00171916187,
              ""y"": 0.0,
              ""z"": -0.00669689476
            },
            {
              ""frame"": 59,
              ""x"": -0.00188138708,
              ""y"": 0.0,
              ""z"": -0.006171137
            },
            {
              ""frame"": 60,
              ""x"": -0.00202528015,
              ""y"": 0.0,
              ""z"": -0.00559547544
            },
            {
              ""frame"": 61,
              ""x"": -0.00215086341,
              ""y"": 0.0,
              ""z"": -0.00496998057
            },
            {
              ""frame"": 62,
              ""x"": -0.00225813314,
              ""y"": 0.0,
              ""z"": -0.00429463
            },
            {
              ""frame"": 63,
              ""x"": -0.00234708562,
              ""y"": 0.0,
              ""z"": -0.0035694316
            },
            {
              ""frame"": 64,
              ""x"": -0.00241774321,
              ""y"": 0.0,
              ""z"": -0.00279439241
            },
            {
              ""frame"": 65,
              ""x"": -0.002498906,
              ""y"": 0.0,
              ""z"": -0.00196944177
            },
            {
              ""frame"": 66,
              ""x"": -0.00258182734,
              ""y"": 0.0,
              ""z"": -0.00111114979
            },
            {
              ""frame"": 67,
              ""x"": -0.00259661674,
              ""y"": 0.0,
              ""z"": -0.0004616361
            },
            {
              ""frame"": 68,
              ""x"": -0.00254226848,
              ""y"": 0.0,
              ""z"": 0.000372337177
            },
            {
              ""frame"": 69,
              ""x"": -0.00240152515,
              ""y"": 0.0,
              ""z"": 0.00105963275
            },
            {
              ""frame"": 70,
              ""x"": -0.002182153,
              ""y"": 0.0,
              ""z"": 0.001479812
            },
            {
              ""frame"": 71,
              ""x"": -0.00191808585,
              ""y"": 0.0,
              ""z"": 0.00163348764
            },
            {
              ""frame"": 72,
              ""x"": -0.00160954613,
              ""y"": 0.0,
              ""z"": 0.001608707
            },
            {
              ""frame"": 73,
              ""x"": -0.0012565013,
              ""y"": 0.0,
              ""z"": 0.00147663057
            },
            {
              ""frame"": 74,
              ""x"": -0.0008589821,
              ""y"": 0.0,
              ""z"": 0.00115957484
            },
            {
              ""frame"": 75,
              ""x"": -0.000451652333,
              ""y"": 0.0,
              ""z"": 0.00071458146
            }
          ],
          ""sourceAnimationEventLabel"": ""light_attack.animation""
        },
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 16,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Airborne"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 1,
            ""stableEventId"": 159,
            ""stableEventLabel"": ""light_attack.control_scale"",
            ""stableEventOrder"": 319
          },
          {
            ""frame"": 17,
            ""endFrame"": 74,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Airborne"",
            ""x"": 0,
            ""y"": 8,
            ""z"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 160,
            ""stableEventLabel"": ""light_attack.impulse"",
            ""stableEventOrder"": 321
          }
        ]
      },
      ""combatTrack"": {
        ""combatActionId"": 5,
        ""events"": [
          {
            ""frame"": 23,
            ""endFrame"": 29,
            ""kind"": ""HitTraceWindow"",
            ""combatActionId"": 0,
            ""traceProfileId"": 0,
            ""equipmentSlotId"": 1,
            ""stableEventId"": 161,
            ""hitEvent"": {
              ""targetTypes"": [
                ""HostileCharacter""
              ],
              ""environmentHitPolicy"": ""None"",
              ""stableEventId"": 161,
              ""hitSameTargetPolicy"": ""AllowRepeat"",
              ""damage"": {
                ""damageTypeId"": 1,
                ""formulaId"": 4,
                ""baseDamage"": 6,
                ""sourceAttributeScales"": [
                  {
                    ""attributeId"": 2,
                    ""scale"": 2.65
                  }
                ],
                ""impactPower"": 34,
                ""launchPower"": 100,
                ""tags"": [
                  14,
                  34,
                  30,
                  36
                ],
                ""analysisCategory"": 28,
                ""damageTypeLabel"": ""slash"",
                ""formulaLabel"": ""ct.soft_defense_v1"",
                ""tagLabels"": [
                  ""light"",
                  ""melee"",
                  ""poise"",
                  ""weapon""
                ],
                ""analysisCategoryLabel"": ""BasicAttack""
              },
              ""stableEventLabel"": ""light_attack.hit_window""
            },
            ""combatActionLabel"": """",
            ""traceProfileLabel"": """",
            ""equipmentSlotLabel"": ""mainHand"",
            ""stableEventLabel"": ""light_attack.hit_window"",
            ""stableEventOrder"": 320
          }
        ],
        ""combatActionLabel"": ""light_attack""
      },
      ""gameplayTrack"": null,
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 75,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": """",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.katana_atk1"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.12,
            ""stableEventId"": 158,
            ""stableEventLabel"": ""light_attack.animation"",
            ""stableEventOrder"": 318
          }
        ]
      },
      ""presentationTrack"": {
        ""events"": [
          {
            ""frame"": 26,
            ""endFrame"": 0,
            ""kind"": ""PlayAudioCue"",
            ""cueId"": 500304,
            ""stableEventId"": 139,
            ""cueLabel"": ""sfx.air_displacement.light_attack"",
            ""stableEventLabel"": ""sfx.light_attack.air_peak"",
            ""stableEventOrder"": 342
          }
        ]
      },
      ""debugTrack"": null
    },
    {
      ""actionId"": 324,
      ""displayName"": ""Light Attack 2"",
      ""category"": ""BasicAttack"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""combat"",
        ""light_attack""
      ],
      ""priority"": 20,
      ""durationFrames"": 76,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Startup"",
          ""startFrame"": 0,
          ""endFrame"": 17
        },
        {
          ""kind"": ""Active"",
          ""startFrame"": 18,
          ""endFrame"": 34
        },
        {
          ""kind"": ""Recovery"",
          ""startFrame"": 35,
          ""endFrame"": 75
        }
      ],
      ""cancelRules"": [
        {
          ""startFrame"": 48,
          ""endFrame"": 75,
          ""requestTypeMask"": 0,
          ""actionSlotMask"": 1
        }
      ],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": true,
        ""rootMotion"": {
          ""sourceAnimationEventId"": 162,
          ""sourceAnimationClipResourceKey"": ""art.character.skeleton.animation.katana_atk2"",
          ""space"": ""Local"",
          ""sampleTickRate"": 60,
          ""obstaclePolicy"": ""Default"",
          ""frames"": [
            {
              ""frame"": 0,
              ""x"": 0,
              ""y"": 0,
              ""z"": 0
            },
            {
              ""frame"": 1,
              ""x"": 0.00325695425,
              ""y"": 0.0,
              ""z"": 0.00241891667
            },
            {
              ""frame"": 2,
              ""x"": 0.00449635554,
              ""y"": 0.0,
              ""z"": 0.002885092
            },
            {
              ""frame"": 3,
              ""x"": 0.0054591,
              ""y"": 0.0,
              ""z"": 0.00342443213
            },
            {
              ""frame"": 4,
              ""x"": 0.00598860532,
              ""y"": 0.0,
              ""z"": 0.003593076
            },
            {
              ""frame"": 5,
              ""x"": 0.006084863,
              ""y"": 0.0,
              ""z"": 0.0033999458
            },
            {
              ""frame"": 6,
              ""x"": 0.00587780029,
              ""y"": 0.0,
              ""z"": 0.00299436226
            },
            {
              ""frame"": 7,
              ""x"": 0.00558336824,
              ""y"": 0.0,
              ""z"": 0.00230924413
            },
            {
              ""frame"": 8,
              ""x"": 0.004557289,
              ""y"": 0.0,
              ""z"": 0.000973444432
            },
            {
              ""frame"": 9,
              ""x"": 0.0027625598,
              ""y"": 0.0,
              ""z"": -0.000157963485
            },
            {
              ""frame"": 10,
              ""x"": 0.00145823509,
              ""y"": 0.0,
              ""z"": -0.00113356486
            },
            {
              ""frame"": 11,
              ""x"": 0.000198554248,
              ""y"": 0.0,
              ""z"": -0.0015764758
            },
            {
              ""frame"": 12,
              ""x"": -0.002694901,
              ""y"": 0.0,
              ""z"": -0.0005182661
            },
            {
              ""frame"": 13,
              ""x"": -0.00535860658,
              ""y"": 0.0,
              ""z"": 0.0007590279
            },
            {
              ""frame"": 14,
              ""x"": -0.00710297,
              ""y"": 0.0,
              ""z"": 0.00171746686
            },
            {
              ""frame"": 15,
              ""x"": -0.008761872,
              ""y"": 0.0,
              ""z"": 0.00312206149
            },
            {
              ""frame"": 16,
              ""x"": -0.0103089958,
              ""y"": 0.0,
              ""z"": 0.00515649468
            },
            {
              ""frame"": 17,
              ""x"": -0.0113651846,
              ""y"": 0.0,
              ""z"": 0.00782074
            },
            {
              ""frame"": 18,
              ""x"": -0.0119514521,
              ""y"": 0.0,
              ""z"": 0.0109273568
            },
            {
              ""frame"": 19,
              ""x"": -0.009968208,
              ""y"": 0.0,
              ""z"": 0.01266104
            },
            {
              ""frame"": 20,
              ""x"": -0.00647585839,
              ""y"": 0.0,
              ""z"": 0.0154356733
            },
            {
              ""frame"": 21,
              ""x"": -0.00110538863,
              ""y"": 0.0,
              ""z"": 0.02014397
            },
            {
              ""frame"": 22,
              ""x"": 0.007901411,
              ""y"": 0.0,
              ""z"": 0.0280247778
            },
            {
              ""frame"": 23,
              ""x"": 0.01445278,
              ""y"": 0.0,
              ""z"": 0.0343079567
            },
            {
              ""frame"": 24,
              ""x"": 0.0140274586,
              ""y"": 0.0,
              ""z"": 0.1290693
            },
            {
              ""frame"": 25,
              ""x"": -0.0190798286,
              ""y"": 0.0,
              ""z"": 0.222578973
            },
            {
              ""frame"": 26,
              ""x"": -0.06319622,
              ""y"": 0.0,
              ""z"": 0.1942848
            },
            {
              ""frame"": 27,
              ""x"": -0.0545351431,
              ""y"": 0.0,
              ""z"": 0.128909111
            },
            {
              ""frame"": 28,
              ""x"": -0.00245556235,
              ""y"": 0.0,
              ""z"": 0.07708317
            },
            {
              ""frame"": 29,
              ""x"": 0.0165110081,
              ""y"": 0.0,
              ""z"": 0.0314583778
            },
            {
              ""frame"": 30,
              ""x"": 0.01809562,
              ""y"": 0.0,
              ""z"": 0.00624305
            },
            {
              ""frame"": 31,
              ""x"": 0.0151844174,
              ""y"": 0.0,
              ""z"": -0.00272375345
            },
            {
              ""frame"": 32,
              ""x"": 0.0125446692,
              ""y"": 0.0,
              ""z"": -0.00206518173
            },
            {
              ""frame"": 33,
              ""x"": 0.010949757,
              ""y"": 0.0,
              ""z"": -0.003868103
            },
            {
              ""frame"": 34,
              ""x"": 0.0076693,
              ""y"": 0.0,
              ""z"": -0.00558519363
            },
            {
              ""frame"": 35,
              ""x"": 0.0045703724,
              ""y"": 0.0,
              ""z"": -0.007199645
            },
            {
              ""frame"": 36,
              ""x"": 0.002815012,
              ""y"": 0.0,
              ""z"": -0.008470714
            },
            {
              ""frame"": 37,
              ""x"": 0.00190406665,
              ""y"": 0.0,
              ""z"": -0.00948995352
            },
            {
              ""frame"": 38,
              ""x"": 0.000912860036,
              ""y"": 0.0,
              ""z"": -0.010565877
            },
            {
              ""frame"": 39,
              ""x"": -0.000188518316,
              ""y"": 0.0,
              ""z"": -0.0115915537
            },
            {
              ""frame"": 40,
              ""x"": -0.00140006468,
              ""y"": 0.0,
              ""z"": -0.0124238729
            },
            {
              ""frame"": 41,
              ""x"": -0.002643302,
              ""y"": 0.0,
              ""z"": -0.013032496
            },
            {
              ""frame"": 42,
              ""x"": -0.00369065627,
              ""y"": 0.0,
              ""z"": -0.0134176016
            },
            {
              ""frame"": 43,
              ""x"": -0.0045068115,
              ""y"": 0.0,
              ""z"": -0.0135789514
            },
            {
              ""frame"": 44,
              ""x"": -0.00509179,
              ""y"": 0.0,
              ""z"": -0.0136144161
            },
            {
              ""frame"": 45,
              ""x"": -0.005554162,
              ""y"": 0.0,
              ""z"": -0.0134323239
            },
            {
              ""frame"": 46,
              ""x"": -0.005897198,
              ""y"": 0.0,
              ""z"": -0.0128896236
            },
            {
              ""frame"": 47,
              ""x"": -0.00601387769,
              ""y"": 0.0,
              ""z"": -0.0121005774
            },
            {
              ""frame"": 48,
              ""x"": -0.00594116,
              ""y"": 0.0,
              ""z"": -0.0125933886
            },
            {
              ""frame"": 49,
              ""x"": -0.00523887575,
              ""y"": 0.0,
              ""z"": -0.0144674182
            },
            {
              ""frame"": 50,
              ""x"": -0.00622602552,
              ""y"": 0.0,
              ""z"": -0.0163311362
            },
            {
              ""frame"": 51,
              ""x"": -0.005220309,
              ""y"": 0.0,
              ""z"": -0.0182088017
            },
            {
              ""frame"": 52,
              ""x"": -0.00369589776,
              ""y"": 0.0,
              ""z"": -0.0200821757
            },
            {
              ""frame"": 53,
              ""x"": -0.00207108259,
              ""y"": 0.0,
              ""z"": -0.0212064981
            },
            {
              ""frame"": 54,
              ""x"": 0.000443220139,
              ""y"": 0.0,
              ""z"": -0.0215812922
            },
            {
              ""frame"": 55,
              ""x"": 0.007162638,
              ""y"": 0.0,
              ""z"": -0.0235484838
            },
            {
              ""frame"": 56,
              ""x"": 0.0063335225,
              ""y"": 0.0,
              ""z"": -0.0267272
            },
            {
              ""frame"": 57,
              ""x"": 0.009681299,
              ""y"": 0.0,
              ""z"": -0.0279380083
            },
            {
              ""frame"": 58,
              ""x"": 0.0123721808,
              ""y"": 0.0,
              ""z"": -0.03150356
            },
            {
              ""frame"": 59,
              ""x"": 0.0137583986,
              ""y"": 0.0,
              ""z"": -0.02603072
            },
            {
              ""frame"": 60,
              ""x"": 0.0129203275,
              ""y"": 0.0,
              ""z"": -0.0166744
            },
            {
              ""frame"": 61,
              ""x"": 0.0104931146,
              ""y"": 0.0,
              ""z"": -0.00767099857
            },
            {
              ""frame"": 62,
              ""x"": 0.007674806,
              ""y"": 0.0,
              ""z"": -0.004681468
            },
            {
              ""frame"": 63,
              ""x"": 0.00523337349,
              ""y"": 0.0,
              ""z"": -0.006257236
            },
            {
              ""frame"": 64,
              ""x"": 0.00339253247,
              ""y"": 0.0,
              ""z"": -0.007630706
            },
            {
              ""frame"": 65,
              ""x"": 0.00208641868,
              ""y"": 0.0,
              ""z"": -0.009640932
            },
            {
              ""frame"": 66,
              ""x"": 0.00203177379,
              ""y"": 0.0,
              ""z"": -0.0102901161
            },
            {
              ""frame"": 67,
              ""x"": 0.00239006779,
              ""y"": 0.0,
              ""z"": -0.008998483
            },
            {
              ""frame"": 68,
              ""x"": 0.002529496,
              ""y"": 0.0,
              ""z"": -0.008114815
            },
            {
              ""frame"": 69,
              ""x"": 0.002357141,
              ""y"": 0.0,
              ""z"": -0.00761130452
            },
            {
              ""frame"": 70,
              ""x"": 0.00199793186,
              ""y"": 0.0,
              ""z"": -0.007151842
            },
            {
              ""frame"": 71,
              ""x"": 0.0014935527,
              ""y"": 0.0,
              ""z"": -0.006780535
            },
            {
              ""frame"": 72,
              ""x"": 0.000815778971,
              ""y"": 0.0,
              ""z"": -0.00649252534
            },
            {
              ""frame"": 73,
              ""x"": 0.000306307338,
              ""y"": 0.0,
              ""z"": -0.00600868464
            },
            {
              ""frame"": 74,
              ""x"": 6.01559877e-05,
              ""y"": 0.0,
              ""z"": -0.00533100963
            },
            {
              ""frame"": 75,
              ""x"": -2.31759623e-05,
              ""y"": 0.0,
              ""z"": -0.00467568636
            }
          ],
          ""sourceAnimationEventLabel"": ""light_attack_2.animation""
        },
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 16,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Airborne"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 1,
            ""stableEventId"": 163,
            ""stableEventLabel"": ""light_attack_2.control_scale"",
            ""stableEventOrder"": 323
          },
          {
            ""frame"": 17,
            ""endFrame"": 74,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Airborne"",
            ""x"": 0,
            ""y"": 8,
            ""z"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 164,
            ""stableEventLabel"": ""light_attack_2.impulse"",
            ""stableEventOrder"": 325
          }
        ]
      },
      ""combatTrack"": {
        ""combatActionId"": 6,
        ""events"": [
          {
            ""frame"": 24,
            ""endFrame"": 30,
            ""kind"": ""HitTraceWindow"",
            ""combatActionId"": 0,
            ""traceProfileId"": 0,
            ""equipmentSlotId"": 1,
            ""stableEventId"": 165,
            ""hitEvent"": {
              ""targetTypes"": [
                ""HostileCharacter""
              ],
              ""environmentHitPolicy"": ""None"",
              ""stableEventId"": 165,
              ""hitSameTargetPolicy"": ""AllowRepeat"",
              ""damage"": {
                ""damageTypeId"": 1,
                ""formulaId"": 4,
                ""baseDamage"": 6,
                ""sourceAttributeScales"": [
                  {
                    ""attributeId"": 2,
                    ""scale"": 0.65
                  }
                ],
                ""impactPower"": 24,
                ""tags"": [
                  14,
                  34,
                  30,
                  36
                ],
                ""analysisCategory"": 28,
                ""damageTypeLabel"": ""slash"",
                ""formulaLabel"": ""ct.soft_defense_v1"",
                ""tagLabels"": [
                  ""light"",
                  ""melee"",
                  ""poise"",
                  ""weapon""
                ],
                ""analysisCategoryLabel"": ""BasicAttack""
              },
              ""onHitBuffs"": [
                {
                  ""buffId"": 601009
                }
              ],
              ""stableEventLabel"": ""light_attack_2.hit_window""
            },
            ""combatActionLabel"": """",
            ""traceProfileLabel"": """",
            ""equipmentSlotLabel"": ""mainHand"",
            ""stableEventLabel"": ""light_attack_2.hit_window"",
            ""stableEventOrder"": 324
          }
        ],
        ""combatActionLabel"": ""light_attack_2""
      },
      ""gameplayTrack"": null,
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 75,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": """",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.katana_atk2"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.12,
            ""stableEventId"": 162,
            ""stableEventLabel"": ""light_attack_2.animation"",
            ""stableEventOrder"": 322
          }
        ]
      },
      ""presentationTrack"": {
        ""events"": [
          {
            ""frame"": 27,
            ""endFrame"": 0,
            ""kind"": ""PlayAudioCue"",
            ""cueId"": 500304,
            ""stableEventId"": 140,
            ""cueLabel"": ""sfx.air_displacement.light_attack"",
            ""stableEventLabel"": ""sfx.light_attack_2.air_peak"",
            ""stableEventOrder"": 343
          }
        ]
      },
      ""debugTrack"": null
    },
    {
      ""actionId"": 325,
      ""displayName"": ""Light Attack 3"",
      ""category"": ""BasicAttack"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""combat"",
        ""light_attack""
      ],
      ""priority"": 20,
      ""durationFrames"": 76,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Startup"",
          ""startFrame"": 0,
          ""endFrame"": 17
        },
        {
          ""kind"": ""Active"",
          ""startFrame"": 18,
          ""endFrame"": 34
        },
        {
          ""kind"": ""Recovery"",
          ""startFrame"": 35,
          ""endFrame"": 75
        }
      ],
      ""cancelRules"": [
        {
          ""startFrame"": 67,
          ""endFrame"": 75,
          ""requestTypeMask"": 0,
          ""actionSlotMask"": 4
        }
      ],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": true,
        ""rootMotion"": {
          ""sourceAnimationEventId"": 166,
          ""sourceAnimationClipResourceKey"": ""art.character.skeleton.animation.katana_atk3"",
          ""space"": ""Local"",
          ""sampleTickRate"": 60,
          ""obstaclePolicy"": ""Default"",
          ""frames"": [
            {
              ""frame"": 0,
              ""x"": 0,
              ""y"": 0,
              ""z"": 0
            },
            {
              ""frame"": 1,
              ""x"": 0.000631437637,
              ""y"": 0.0,
              ""z"": 0.0004195571
            },
            {
              ""frame"": 2,
              ""x"": 0.00111477729,
              ""y"": 0.0,
              ""z"": 0.000491961837
            },
            {
              ""frame"": 3,
              ""x"": 0.00152458157,
              ""y"": 0.0,
              ""z"": 0.0005684756
            },
            {
              ""frame"": 4,
              ""x"": 0.00186085235,
              ""y"": 0.0,
              ""z"": 0.0006490983
            },
            {
              ""frame"": 5,
              ""x"": 0.002123584,
              ""y"": 0.0,
              ""z"": 0.00073383376
            },
            {
              ""frame"": 6,
              ""x"": 0.002312785,
              ""y"": 0.0,
              ""z"": 0.0008226745
            },
            {
              ""frame"": 7,
              ""x"": 0.00242844783,
              ""y"": 0.0,
              ""z"": 0.000915631652
            },
            {
              ""frame"": 8,
              ""x"": 0.00247057527,
              ""y"": 0.0,
              ""z"": 0.00101269037
            },
            {
              ""frame"": 9,
              ""x"": 0.00243916735,
              ""y"": 0.0,
              ""z"": 0.00111386552
            },
            {
              ""frame"": 10,
              ""x"": 0.002334226,
              ""y"": 0.0,
              ""z"": 0.00121914968
            },
            {
              ""frame"": 11,
              ""x"": 0.00215574354,
              ""y"": 0.0,
              ""z"": 0.0013285391
            },
            {
              ""frame"": 12,
              ""x"": 0.00190373138,
              ""y"": 0.0,
              ""z"": 0.00144204125
            },
            {
              ""frame"": 13,
              ""x"": 0.00157818571,
              ""y"": 0.0,
              ""z"": 0.00155965611
            },
            {
              ""frame"": 14,
              ""x"": 0.00117909536,
              ""y"": 0.0,
              ""z"": 0.00168137625
            },
            {
              ""frame"": 15,
              ""x"": 0.0007064752,
              ""y"": 0.0,
              ""z"": 0.002196379
            },
            {
              ""frame"": 16,
              ""x"": 0.000861216336,
              ""y"": 0.0,
              ""z"": 0.00318666548
            },
            {
              ""frame"": 17,
              ""x"": 0.001589112,
              ""y"": 0.0,
              ""z"": 0.004304059
            },
            {
              ""frame"": 18,
              ""x"": 0.00216218829,
              ""y"": 0.0,
              ""z"": 0.005548574
            },
            {
              ""frame"": 19,
              ""x"": 0.00258042663,
              ""y"": 0.0,
              ""z"": 0.00692018867
            },
            {
              ""frame"": 20,
              ""x"": 0.002843853,
              ""y"": 0.0,
              ""z"": 0.00841894
            },
            {
              ""frame"": 21,
              ""x"": 0.0029524453,
              ""y"": 0.0,
              ""z"": 0.0100447685
            },
            {
              ""frame"": 22,
              ""x"": 0.00290621445,
              ""y"": 0.0,
              ""z"": 0.011797756
            },
            {
              ""frame"": 23,
              ""x"": 0.002705153,
              ""y"": 0.0,
              ""z"": 0.0136778057
            },
            {
              ""frame"": 24,
              ""x"": 0.00234927237,
              ""y"": 0.0,
              ""z"": 0.01568503
            },
            {
              ""frame"": 25,
              ""x"": 0.00183855742,
              ""y"": 0.0,
              ""z"": 0.0178192854
            },
            {
              ""frame"": 26,
              ""x"": 0.00178004429,
              ""y"": 0.0,
              ""z"": 0.02008076
            },
            {
              ""frame"": 27,
              ""x"": 0.0003477931,
              ""y"": 0.0,
              ""z"": 0.0203878582
            },
            {
              ""frame"": 28,
              ""x"": -0.003978189,
              ""y"": 0.0,
              ""z"": 0.0212479681
            },
            {
              ""frame"": 29,
              ""x"": -0.0111978874,
              ""y"": 0.0,
              ""z"": 0.0259958953
            },
            {
              ""frame"": 30,
              ""x"": -0.0213113483,
              ""y"": 0.0,
              ""z"": 0.0370345563
            },
            {
              ""frame"": 31,
              ""x"": -0.03414087,
              ""y"": 0.0,
              ""z"": 0.0328888
            },
            {
              ""frame"": 32,
              ""x"": -0.0301637817,
              ""y"": 0.0,
              ""z"": 0.0107229948
            },
            {
              ""frame"": 33,
              ""x"": -0.01170931,
              ""y"": 0.0,
              ""z"": -0.00297042727
            },
            {
              ""frame"": 34,
              ""x"": -0.00169691071,
              ""y"": 0.0,
              ""z"": -0.0052497685
            },
            {
              ""frame"": 35,
              ""x"": 0.000327400863,
              ""y"": 0.0,
              ""z"": -0.00238323212
            },
            {
              ""frame"": 36,
              ""x"": -0.00110512972,
              ""y"": 0.0,
              ""z"": -0.002072066
            },
            {
              ""frame"": 37,
              ""x"": -0.000685174,
              ""y"": 0.0,
              ""z"": -0.00189867616
            },
            {
              ""frame"": 38,
              ""x"": -0.000289481133,
              ""y"": 0.0,
              ""z"": -0.00186318159
            },
            {
              ""frame"": 39,
              ""x"": 8.197874e-05,
              ""y"": 0.0,
              ""z"": -0.00196552277
            },
            {
              ""frame"": 40,
              ""x"": 0.0004291907,
              ""y"": 0.0,
              ""z"": -0.00220566988
            },
            {
              ""frame"": 41,
              ""x"": 0.0007521622,
              ""y"": 0.0,
              ""z"": -0.00258371234
            },
            {
              ""frame"": 42,
              ""x"": 0.00105089322,
              ""y"": 0.0,
              ""z"": -0.003099531
            },
            {
              ""frame"": 43,
              ""x"": 0.00132538,
              ""y"": 0.0,
              ""z"": -0.003753215
            },
            {
              ""frame"": 44,
              ""x"": 0.00157563388,
              ""y"": 0.0,
              ""z"": -0.00454479456
            },
            {
              ""frame"": 45,
              ""x"": 0.00180162862,
              ""y"": 0.0,
              ""z"": -0.00547415
            },
            {
              ""frame"": 46,
              ""x"": 0.00200339779,
              ""y"": 0.0,
              ""z"": -0.00654134154
            },
            {
              ""frame"": 47,
              ""x"": 0.002180919,
              ""y"": 0.0,
              ""z"": -0.00761881471
            },
            {
              ""frame"": 48,
              ""x"": 0.00233419612,
              ""y"": 0.0,
              ""z"": -0.008550197
            },
            {
              ""frame"": 49,
              ""x"": 0.00246322528,
              ""y"": 0.0,
              ""z"": -0.009385049
            },
            {
              ""frame"": 50,
              ""x"": 0.00256802887,
              ""y"": 0.0,
              ""z"": -0.0101233721
            },
            {
              ""frame"": 51,
              ""x"": 0.00264858827,
              ""y"": 0.0,
              ""z"": -0.0107651651
            },
            {
              ""frame"": 52,
              ""x"": 0.00270488486,
              ""y"": 0.0,
              ""z"": -0.0113103688
            },
            {
              ""frame"": 53,
              ""x"": 0.00273696147,
              ""y"": 0.0,
              ""z"": -0.0117590427
            },
            {
              ""frame"": 54,
              ""x"": 0.002744794,
              ""y"": 0.0,
              ""z"": -0.012111187
            },
            {
              ""frame"": 55,
              ""x"": 0.00272837467,
              ""y"": 0.0,
              ""z"": -0.0123668313
            },
            {
              ""frame"": 56,
              ""x"": 0.00268771872,
              ""y"": 0.0,
              ""z"": -0.0125258267
            },
            {
              ""frame"": 57,
              ""x"": 0.00262281671,
              ""y"": 0.0,
              ""z"": -0.012588352
            },
            {
              ""frame"": 58,
              ""x"": 0.002533678,
              ""y"": 0.0,
              ""z"": -0.0125543028
            },
            {
              ""frame"": 59,
              ""x"": 0.00242031,
              ""y"": 0.0,
              ""z"": -0.0124237686
            },
            {
              ""frame"": 60,
              ""x"": 0.0022826707,
              ""y"": 0.0,
              ""z"": -0.0121966
            },
            {
              ""frame"": 61,
              ""x"": 0.00212320965,
              ""y"": 0.0,
              ""z"": -0.0118729323
            },
            {
              ""frame"": 62,
              ""x"": 0.001962542,
              ""y"": 0.0,
              ""z"": -0.0114527345
            },
            {
              ""frame"": 63,
              ""x"": 0.001808587,
              ""y"": 0.0,
              ""z"": -0.0109359622
            },
            {
              ""frame"": 64,
              ""x"": 0.00166135514,
              ""y"": 0.0,
              ""z"": -0.01032275
            },
            {
              ""frame"": 65,
              ""x"": 0.001520813,
              ""y"": 0.0,
              ""z"": -0.009612814
            },
            {
              ""frame"": 66,
              ""x"": 0.00138699636,
              ""y"": 0.0,
              ""z"": -0.008806437
            },
            {
              ""frame"": 67,
              ""x"": 0.00125989132,
              ""y"": 0.0,
              ""z"": -0.007903501
            },
            {
              ""frame"": 68,
              ""x"": 0.00113949925,
              ""y"": 0.0,
              ""z"": -0.00690405071
            },
            {
              ""frame"": 69,
              ""x"": 0.00102581922,
              ""y"": 0.0,
              ""z"": -0.005807951
            },
            {
              ""frame"": 70,
              ""x"": 0.0009188531,
              ""y"": 0.0,
              ""z"": -0.00426001847
            },
            {
              ""frame"": 71,
              ""x"": 0.000818599947,
              ""y"": 0.0,
              ""z"": -0.00250623561
            },
            {
              ""frame"": 72,
              ""x"": 0.0007250644,
              ""y"": 0.0,
              ""z"": -0.00102519244
            },
            {
              ""frame"": 73,
              ""x"": 0.0006382279,
              ""y"": 0.0,
              ""z"": 0.000183125958
            },
            {
              ""frame"": 74,
              ""x"": 0.0005581137,
              ""y"": 0.0,
              ""z"": 0.0011186935
            },
            {
              ""frame"": 75,
              ""x"": 0.000484710559,
              ""y"": 0.0,
              ""z"": 0.00178152323
            }
          ],
          ""sourceAnimationEventLabel"": ""light_attack_3.animation""
        },
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 16,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Airborne"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 1,
            ""stableEventId"": 167,
            ""stableEventLabel"": ""light_attack_3.control_scale"",
            ""stableEventOrder"": 327
          },
          {
            ""frame"": 17,
            ""endFrame"": 74,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Airborne"",
            ""x"": 0,
            ""y"": 8,
            ""z"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 168,
            ""stableEventLabel"": ""light_attack_3.impulse"",
            ""stableEventOrder"": 329
          }
        ]
      },
      ""combatTrack"": {
        ""combatActionId"": 7,
        ""events"": [
          {
            ""frame"": 29,
            ""endFrame"": 35,
            ""kind"": ""HitTraceWindow"",
            ""combatActionId"": 0,
            ""traceProfileId"": 0,
            ""equipmentSlotId"": 1,
            ""stableEventId"": 169,
            ""hitEvent"": {
              ""targetTypes"": [
                ""HostileCharacter""
              ],
              ""environmentHitPolicy"": ""None"",
              ""stableEventId"": 169,
              ""hitSameTargetPolicy"": ""AllowRepeat"",
              ""damage"": {
                ""damageTypeId"": 1,
                ""formulaId"": 4,
                ""baseDamage"": 6,
                ""sourceAttributeScales"": [
                  {
                    ""attributeId"": 2,
                    ""scale"": 0.65
                  }
                ],
                ""impactPower"": 34,
                ""tags"": [
                  34,
                  36,
                  14,
                  30
                ],
                ""analysisCategory"": 28,
                ""damageTypeLabel"": ""slash"",
                ""formulaLabel"": ""ct.soft_defense_v1"",
                ""tagLabels"": [
                  ""melee"",
                  ""weapon"",
                  ""light"",
                  ""poise""
                ],
                ""analysisCategoryLabel"": ""BasicAttack""
              },
              ""onHitBuffs"": [
                {
                  ""buffId"": 601009
                }
              ],
              ""stableEventLabel"": ""light_attack_3.hit_window""
            },
            ""combatActionLabel"": """",
            ""traceProfileLabel"": """",
            ""equipmentSlotLabel"": ""mainHand"",
            ""stableEventLabel"": ""light_attack_3.hit_window"",
            ""stableEventOrder"": 328
          }
        ],
        ""combatActionLabel"": ""light_attack_3""
      },
      ""gameplayTrack"": null,
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 75,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": """",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.katana_atk3"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.12,
            ""stableEventId"": 166,
            ""stableEventLabel"": ""light_attack_3.animation"",
            ""stableEventOrder"": 326
          }
        ]
      },
      ""presentationTrack"": {
        ""events"": [
          {
            ""frame"": 32,
            ""endFrame"": 0,
            ""kind"": ""PlayAudioCue"",
            ""cueId"": 500304,
            ""stableEventId"": 141,
            ""cueLabel"": ""sfx.air_displacement.light_attack"",
            ""stableEventLabel"": ""sfx.light_attack_3.air_peak"",
            ""stableEventOrder"": 344
          }
        ]
      },
      ""debugTrack"": null
    },
    {
      ""actionId"": 302,
      ""displayName"": ""Fireball Impact"",
      ""category"": ""Skill"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""combat"",
        ""magic"",
        ""fireball""
      ],
      ""priority"": 30,
      ""durationFrames"": 90,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Startup"",
          ""startFrame"": 0,
          ""endFrame"": 5
        },
        {
          ""kind"": ""Active"",
          ""startFrame"": 6,
          ""endFrame"": 65
        },
        {
          ""kind"": ""Recovery"",
          ""startFrame"": 66,
          ""endFrame"": 89
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 11,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 170,
            ""stableEventLabel"": ""fireball_impact.control_lock"",
            ""stableEventOrder"": 255
          },
          {
            ""frame"": 12,
            ""endFrame"": 89,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 171,
            ""stableEventLabel"": ""fireball_impact.control_release"",
            ""stableEventOrder"": 256
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": {
        ""events"": [
          {
            ""frame"": 6,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": 17,
            ""abilityStableId"": 0,
            ""stableEventId"": 18,
            ""requestLabel"": ""character_test.fireball.projectile"",
            ""abilityStableLabel"": """",
            ""stableEventLabel"": ""character_test.fireball.projectile.impact"",
            ""stableEventOrder"": 64
          }
        ]
      },
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 23,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": """",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.katana_atk1"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.12,
            ""stableEventId"": 172,
            ""stableEventLabel"": ""fireball_impact.animation"",
            ""stableEventOrder"": 254
          }
        ]
      },
      ""presentationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 29,
            ""kind"": ""SpawnVisualCue"",
            ""resourceKey"": ""vfx.magic.fireball.charge:GameObject@mxframework.samples"",
            ""vfxAttachment"": {
              ""mode"": ""FollowSubject"",
              ""socketId"": """",
              ""offsetSpace"": ""Local"",
              ""transform"": {
                ""positionX"": 0,
                ""positionY"": 0,
                ""positionZ"": 0,
                ""rotationEulerX"": 0,
                ""rotationEulerY"": 0,
                ""rotationEulerZ"": 0,
                ""scaleX"": 1,
                ""scaleY"": 1,
                ""scaleZ"": 1
              }
            },
            ""vfxPlaybackMode"": ""PlayToCompletion"",
            ""vfxPlaybackRate"": 1,
            ""vfxScaleMultiplier"": 1,
            ""vfxStartOffsetFrames"": 0,
            ""vfxEndOffsetFrames"": 0,
            ""vfxNaturalEndPolicy"": ""Complete"",
            ""vfxParameter0"": 0,
            ""vfxParameter1"": 0,
            ""vfxParameter2"": 0,
            ""vfxParameter3"": 0,
            ""stableEventId"": 86,
            ""stableEventLabel"": ""character_test.fireball.windup_vfx"",
            ""vfxDefinitionId"": 12004,
            ""numericSocketId"": 0,
            ""stableEventOrder"": 65
          }
        ]
      },
      ""debugTrack"": null
    },
    {
      ""actionId"": 303,
      ""displayName"": ""Fireball Burst"",
      ""category"": ""Skill"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""combat"",
        ""magic"",
        ""fireball"",
        ""burst""
      ],
      ""priority"": 30,
      ""durationFrames"": 90,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Startup"",
          ""startFrame"": 0,
          ""endFrame"": 5
        },
        {
          ""kind"": ""Active"",
          ""startFrame"": 6,
          ""endFrame"": 8
        },
        {
          ""kind"": ""Recovery"",
          ""startFrame"": 9,
          ""endFrame"": 89
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 11,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 173,
            ""stableEventLabel"": ""fireball_burst.control_lock"",
            ""stableEventOrder"": 240
          },
          {
            ""frame"": 12,
            ""endFrame"": 89,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 174,
            ""stableEventLabel"": ""fireball_burst.control_release"",
            ""stableEventOrder"": 241
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": {
        ""events"": [
          {
            ""frame"": 25,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": 19,
            ""abilityStableId"": 0,
            ""stableEventId"": 20,
            ""requestLabel"": ""character_test.fireball_burst.projectile"",
            ""abilityStableLabel"": """",
            ""stableEventLabel"": ""character_test.fireball_burst.projectile.impact"",
            ""stableEventOrder"": 70
          }
        ]
      },
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 89,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": """",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.katana_atk1"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.12,
            ""stableEventId"": 175,
            ""stableEventLabel"": ""fireball_burst.animation"",
            ""stableEventOrder"": 239
          }
        ]
      },
      ""presentationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 25,
            ""kind"": ""SpawnVisualCue"",
            ""resourceKey"": ""vfx.authored.3fc1a5a8-a28b-4d01-a9ba-21083b97bc38:GameObject@mxframework.samples"",
            ""vfxAttachment"": {
              ""mode"": ""FollowSocket"",
              ""socketId"": ""mainHand"",
              ""offsetSpace"": ""Local"",
              ""transform"": {
                ""positionX"": 0,
                ""positionY"": 0,
                ""positionZ"": 0,
                ""rotationEulerX"": 0,
                ""rotationEulerY"": 0,
                ""rotationEulerZ"": 0,
                ""scaleX"": 1,
                ""scaleY"": 1,
                ""scaleZ"": 1
              }
            },
            ""vfxPlaybackMode"": ""PlayToCompletion"",
            ""vfxPlaybackRate"": 11.5384617,
            ""vfxScaleMultiplier"": 0.4,
            ""vfxStartOffsetFrames"": 0,
            ""vfxEndOffsetFrames"": 0,
            ""vfxNaturalEndPolicy"": ""Complete"",
            ""vfxParameter0"": 0,
            ""vfxParameter1"": 0,
            ""vfxParameter2"": 0,
            ""vfxParameter3"": 0,
            ""stableEventId"": 87,
            ""stableEventLabel"": ""character_test.fireball_burst.windup_vfx"",
            ""vfxDefinitionId"": 20001,
            ""numericSocketId"": 12,
            ""stableEventOrder"": 71
          }
        ]
      },
      ""debugTrack"": null
    },
    {
      ""actionId"": 304,
      ""displayName"": ""Flame Lance"",
      ""category"": ""Skill"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""combat"",
        ""magic"",
        ""flame_lance"",
        ""pierce""
      ],
      ""priority"": 30,
      ""durationFrames"": 90,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Startup"",
          ""startFrame"": 0,
          ""endFrame"": 5
        },
        {
          ""kind"": ""Active"",
          ""startFrame"": 6,
          ""endFrame"": 8
        },
        {
          ""kind"": ""Recovery"",
          ""startFrame"": 9,
          ""endFrame"": 89
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 11,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 176,
            ""stableEventLabel"": ""flame_lance.control_lock"",
            ""stableEventOrder"": 270
          },
          {
            ""frame"": 12,
            ""endFrame"": 89,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 177,
            ""stableEventLabel"": ""flame_lance.control_release"",
            ""stableEventOrder"": 271
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": {
        ""events"": [
          {
            ""frame"": 6,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": 21,
            ""abilityStableId"": 0,
            ""stableEventId"": 22,
            ""requestLabel"": ""character_test.flame_lance.projectile"",
            ""abilityStableLabel"": """",
            ""stableEventLabel"": ""character_test.flame_lance.projectile.impact"",
            ""stableEventOrder"": 93
          }
        ]
      },
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 89,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": """",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.katana_atk1"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.12,
            ""stableEventId"": 178,
            ""stableEventLabel"": ""flame_lance.animation"",
            ""stableEventOrder"": 269
          }
        ]
      },
      ""presentationTrack"": null,
      ""debugTrack"": null
    },
    {
      ""actionId"": 305,
      ""displayName"": ""Fireball Lob"",
      ""category"": ""Skill"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""combat"",
        ""magic"",
        ""fireball"",
        ""lob"",
        ""area""
      ],
      ""priority"": 30,
      ""durationFrames"": 90,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Startup"",
          ""startFrame"": 0,
          ""endFrame"": 5
        },
        {
          ""kind"": ""Active"",
          ""startFrame"": 6,
          ""endFrame"": 8
        },
        {
          ""kind"": ""Recovery"",
          ""startFrame"": 9,
          ""endFrame"": 89
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 11,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 179,
            ""stableEventLabel"": ""fireball_lob.control_lock"",
            ""stableEventOrder"": 258
          },
          {
            ""frame"": 12,
            ""endFrame"": 89,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 180,
            ""stableEventLabel"": ""fireball_lob.control_release"",
            ""stableEventOrder"": 259
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": {
        ""events"": [
          {
            ""frame"": 6,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": 23,
            ""abilityStableId"": 0,
            ""stableEventId"": 24,
            ""requestLabel"": ""character_test.fireball_lob.projectile"",
            ""abilityStableLabel"": """",
            ""stableEventLabel"": ""character_test.fireball_lob.projectile.impact"",
            ""stableEventOrder"": 81
          }
        ]
      },
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 89,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": """",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.katana_atk1"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.12,
            ""stableEventId"": 181,
            ""stableEventLabel"": ""fireball_lob.animation"",
            ""stableEventOrder"": 257
          }
        ]
      },
      ""presentationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 29,
            ""kind"": ""SpawnVisualCue"",
            ""resourceKey"": ""vfx.magic.fireball_burst.charge:GameObject@mxframework.samples"",
            ""vfxAttachment"": {
              ""mode"": ""FollowSubject"",
              ""socketId"": """",
              ""offsetSpace"": ""Local"",
              ""transform"": {
                ""positionX"": 0,
                ""positionY"": 0,
                ""positionZ"": 0,
                ""rotationEulerX"": 0,
                ""rotationEulerY"": 0,
                ""rotationEulerZ"": 0,
                ""scaleX"": 1,
                ""scaleY"": 1,
                ""scaleZ"": 1
              }
            },
            ""vfxPlaybackMode"": ""PlayToCompletion"",
            ""vfxPlaybackRate"": 1,
            ""vfxScaleMultiplier"": 1,
            ""vfxStartOffsetFrames"": 0,
            ""vfxEndOffsetFrames"": 0,
            ""vfxNaturalEndPolicy"": ""Complete"",
            ""vfxParameter0"": 0,
            ""vfxParameter1"": 0,
            ""vfxParameter2"": 0,
            ""vfxParameter3"": 0,
            ""stableEventId"": 182,
            ""stableEventLabel"": ""character_test.fireball_lob.windup_vfx"",
            ""vfxDefinitionId"": 12005,
            ""numericSocketId"": 0,
            ""stableEventOrder"": 82
          }
        ]
      },
      ""debugTrack"": null
    },
    {
      ""actionId"": 306,
      ""displayName"": ""Fireball Surge"",
      ""category"": ""Skill"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""combat"",
        ""magic"",
        ""fireball"",
        ""surge""
      ],
      ""priority"": 30,
      ""durationFrames"": 90,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Startup"",
          ""startFrame"": 0,
          ""endFrame"": 5
        },
        {
          ""kind"": ""Active"",
          ""startFrame"": 6,
          ""endFrame"": 8
        },
        {
          ""kind"": ""Recovery"",
          ""startFrame"": 9,
          ""endFrame"": 89
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 11,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 183,
            ""stableEventLabel"": ""fireball_surge.control_lock"",
            ""stableEventOrder"": 264
          },
          {
            ""frame"": 12,
            ""endFrame"": 89,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 184,
            ""stableEventLabel"": ""fireball_surge.control_release"",
            ""stableEventOrder"": 265
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": {
        ""events"": [
          {
            ""frame"": 6,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": 25,
            ""abilityStableId"": 0,
            ""stableEventId"": 26,
            ""requestLabel"": ""character_test.fireball_surge.projectile"",
            ""abilityStableLabel"": """",
            ""stableEventLabel"": ""character_test.fireball_surge.projectile.impact"",
            ""stableEventOrder"": 87
          }
        ]
      },
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 89,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": """",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.katana_atk1"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.12,
            ""stableEventId"": 185,
            ""stableEventLabel"": ""fireball_surge.animation"",
            ""stableEventOrder"": 263
          }
        ]
      },
      ""presentationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 29,
            ""kind"": ""SpawnVisualCue"",
            ""resourceKey"": ""vfx.magic.fireball.charge:GameObject@mxframework.samples"",
            ""vfxAttachment"": {
              ""mode"": ""FollowSubject"",
              ""socketId"": """",
              ""offsetSpace"": ""Local"",
              ""transform"": {
                ""positionX"": 0,
                ""positionY"": 0,
                ""positionZ"": 0,
                ""rotationEulerX"": 0,
                ""rotationEulerY"": 0,
                ""rotationEulerZ"": 0,
                ""scaleX"": 1,
                ""scaleY"": 1,
                ""scaleZ"": 1
              }
            },
            ""vfxPlaybackMode"": ""PlayToCompletion"",
            ""vfxPlaybackRate"": 1,
            ""vfxScaleMultiplier"": 1,
            ""vfxStartOffsetFrames"": 0,
            ""vfxEndOffsetFrames"": 0,
            ""vfxNaturalEndPolicy"": ""Complete"",
            ""vfxParameter0"": 0,
            ""vfxParameter1"": 0,
            ""vfxParameter2"": 0,
            ""vfxParameter3"": 0,
            ""stableEventId"": 186,
            ""stableEventLabel"": ""character_test.fireball_surge.windup_vfx"",
            ""vfxDefinitionId"": 12004,
            ""numericSocketId"": 0,
            ""stableEventOrder"": 88
          }
        ]
      },
      ""debugTrack"": null
    },
    {
      ""actionId"": 307,
      ""displayName"": ""Fireball Return"",
      ""category"": ""Skill"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""combat"",
        ""magic"",
        ""fireball"",
        ""return""
      ],
      ""priority"": 30,
      ""durationFrames"": 90,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Startup"",
          ""startFrame"": 0,
          ""endFrame"": 5
        },
        {
          ""kind"": ""Active"",
          ""startFrame"": 6,
          ""endFrame"": 8
        },
        {
          ""kind"": ""Recovery"",
          ""startFrame"": 9,
          ""endFrame"": 89
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 11,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 187,
            ""stableEventLabel"": ""fireball_return.control_lock"",
            ""stableEventOrder"": 261
          },
          {
            ""frame"": 12,
            ""endFrame"": 89,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 188,
            ""stableEventLabel"": ""fireball_return.control_release"",
            ""stableEventOrder"": 262
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": {
        ""events"": [
          {
            ""frame"": 6,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": 27,
            ""abilityStableId"": 0,
            ""stableEventId"": 28,
            ""requestLabel"": ""character_test.fireball_return.projectile"",
            ""abilityStableLabel"": """",
            ""stableEventLabel"": ""character_test.fireball_return.projectile.impact"",
            ""stableEventOrder"": 84
          }
        ]
      },
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 89,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": """",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.katana_atk1"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.12,
            ""stableEventId"": 189,
            ""stableEventLabel"": ""fireball_return.animation"",
            ""stableEventOrder"": 260
          }
        ]
      },
      ""presentationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 29,
            ""kind"": ""SpawnVisualCue"",
            ""resourceKey"": ""vfx.magic.fireball.charge:GameObject@mxframework.samples"",
            ""vfxAttachment"": {
              ""mode"": ""FollowSubject"",
              ""socketId"": """",
              ""offsetSpace"": ""Local"",
              ""transform"": {
                ""positionX"": 0,
                ""positionY"": 0,
                ""positionZ"": 0,
                ""rotationEulerX"": 0,
                ""rotationEulerY"": 0,
                ""rotationEulerZ"": 0,
                ""scaleX"": 1,
                ""scaleY"": 1,
                ""scaleZ"": 1
              }
            },
            ""vfxPlaybackMode"": ""PlayToCompletion"",
            ""vfxPlaybackRate"": 1,
            ""vfxScaleMultiplier"": 1,
            ""vfxStartOffsetFrames"": 0,
            ""vfxEndOffsetFrames"": 0,
            ""vfxNaturalEndPolicy"": ""Complete"",
            ""vfxParameter0"": 0,
            ""vfxParameter1"": 0,
            ""vfxParameter2"": 0,
            ""vfxParameter3"": 0,
            ""stableEventId"": 190,
            ""stableEventLabel"": ""character_test.fireball_return.windup_vfx"",
            ""vfxDefinitionId"": 12004,
            ""numericSocketId"": 0,
            ""stableEventOrder"": 85
          }
        ]
      },
      ""debugTrack"": null
    },
    {
      ""actionId"": 308,
      ""displayName"": ""Fireball Sway"",
      ""category"": ""Skill"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""combat"",
        ""magic"",
        ""fireball"",
        ""sway""
      ],
      ""priority"": 30,
      ""durationFrames"": 90,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Startup"",
          ""startFrame"": 0,
          ""endFrame"": 5
        },
        {
          ""kind"": ""Active"",
          ""startFrame"": 6,
          ""endFrame"": 8
        },
        {
          ""kind"": ""Recovery"",
          ""startFrame"": 9,
          ""endFrame"": 89
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 11,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 191,
            ""stableEventLabel"": ""fireball_sway.control_lock"",
            ""stableEventOrder"": 267
          },
          {
            ""frame"": 12,
            ""endFrame"": 89,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 192,
            ""stableEventLabel"": ""fireball_sway.control_release"",
            ""stableEventOrder"": 268
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": {
        ""events"": [
          {
            ""frame"": 6,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": 29,
            ""abilityStableId"": 0,
            ""stableEventId"": 30,
            ""requestLabel"": ""character_test.fireball_sway.projectile"",
            ""abilityStableLabel"": """",
            ""stableEventLabel"": ""character_test.fireball_sway.projectile.impact"",
            ""stableEventOrder"": 90
          }
        ]
      },
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 89,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": """",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.katana_atk1"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.12,
            ""stableEventId"": 193,
            ""stableEventLabel"": ""fireball_sway.animation"",
            ""stableEventOrder"": 266
          }
        ]
      },
      ""presentationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 29,
            ""kind"": ""SpawnVisualCue"",
            ""resourceKey"": ""vfx.magic.fireball.charge:GameObject@mxframework.samples"",
            ""vfxAttachment"": {
              ""mode"": ""FollowSubject"",
              ""socketId"": """",
              ""offsetSpace"": ""Local"",
              ""transform"": {
                ""positionX"": 0,
                ""positionY"": 0,
                ""positionZ"": 0,
                ""rotationEulerX"": 0,
                ""rotationEulerY"": 0,
                ""rotationEulerZ"": 0,
                ""scaleX"": 1,
                ""scaleY"": 1,
                ""scaleZ"": 1
              }
            },
            ""vfxPlaybackMode"": ""PlayToCompletion"",
            ""vfxPlaybackRate"": 1,
            ""vfxScaleMultiplier"": 1,
            ""vfxStartOffsetFrames"": 0,
            ""vfxEndOffsetFrames"": 0,
            ""vfxNaturalEndPolicy"": ""Complete"",
            ""vfxParameter0"": 0,
            ""vfxParameter1"": 0,
            ""vfxParameter2"": 0,
            ""vfxParameter3"": 0,
            ""stableEventId"": 194,
            ""stableEventLabel"": ""character_test.fireball_sway.windup_vfx"",
            ""vfxDefinitionId"": 12004,
            ""numericSocketId"": 0,
            ""stableEventOrder"": 91
          }
        ]
      },
      ""debugTrack"": null
    },
    {
      ""actionId"": 309,
      ""displayName"": ""Guard Aura"",
      ""category"": ""Skill"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""combat"",
        ""magic"",
        ""aura"",
        ""buff""
      ],
      ""priority"": 30,
      ""durationFrames"": 90,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Startup"",
          ""startFrame"": 0,
          ""endFrame"": 5
        },
        {
          ""kind"": ""Active"",
          ""startFrame"": 6,
          ""endFrame"": 8
        },
        {
          ""kind"": ""Recovery"",
          ""startFrame"": 9,
          ""endFrame"": 89
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 11,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 195,
            ""stableEventLabel"": ""guard_aura.control_lock"",
            ""stableEventOrder"": 276
          },
          {
            ""frame"": 12,
            ""endFrame"": 89,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 196,
            ""stableEventLabel"": ""guard_aura.control_release"",
            ""stableEventOrder"": 277
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": {
        ""events"": [
          {
            ""frame"": 6,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": 77,
            ""abilityStableId"": 0,
            ""stableEventId"": 88,
            ""requestLabel"": ""character_test.guard_aura.component_ability"",
            ""abilityStableLabel"": """",
            ""stableEventLabel"": ""character_test.guard_aura.cast"",
            ""stableEventOrder"": 95
          }
        ]
      },
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 89,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": """",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.katana_atk1"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.12,
            ""stableEventId"": 197,
            ""stableEventLabel"": ""guard_aura.animation"",
            ""stableEventOrder"": 275
          }
        ]
      },
      ""presentationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 89,
            ""kind"": ""SpawnVisualCue"",
            ""resourceKey"": ""vfx.magic.guard_aura:GameObject@mxframework.samples"",
            ""vfxAttachment"": {
              ""mode"": ""FollowSubject"",
              ""socketId"": """",
              ""offsetSpace"": ""Local"",
              ""transform"": {
                ""positionX"": 0,
                ""positionY"": 0,
                ""positionZ"": 0,
                ""rotationEulerX"": 0,
                ""rotationEulerY"": 0,
                ""rotationEulerZ"": 0,
                ""scaleX"": 1,
                ""scaleY"": 1,
                ""scaleZ"": 1
              }
            },
            ""vfxPlaybackMode"": ""PlayToCompletion"",
            ""vfxPlaybackRate"": 1,
            ""vfxScaleMultiplier"": 1,
            ""vfxStartOffsetFrames"": 0,
            ""vfxEndOffsetFrames"": 0,
            ""vfxNaturalEndPolicy"": ""Complete"",
            ""vfxParameter0"": 0,
            ""vfxParameter1"": 0,
            ""vfxParameter2"": 0,
            ""vfxParameter3"": 0,
            ""stableEventId"": 89,
            ""stableEventLabel"": ""character_test.guard_aura.aura_vfx"",
            ""vfxDefinitionId"": 12006,
            ""numericSocketId"": 0,
            ""stableEventOrder"": 94
          }
        ]
      },
      ""debugTrack"": null
    },
    {
      ""actionId"": 320,
      ""displayName"": ""Physical Shield"",
      ""category"": ""Skill"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""combat"",
        ""defense"",
        ""shield""
      ],
      ""priority"": 30,
      ""durationFrames"": 90,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Startup"",
          ""startFrame"": 0,
          ""endFrame"": 5
        },
        {
          ""kind"": ""Active"",
          ""startFrame"": 6,
          ""endFrame"": 8
        },
        {
          ""kind"": ""Recovery"",
          ""startFrame"": 9,
          ""endFrame"": 89
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 11,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 198,
            ""stableEventLabel"": ""physical_shield.control_lock"",
            ""stableEventOrder"": 334
          },
          {
            ""frame"": 12,
            ""endFrame"": 89,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 199,
            ""stableEventLabel"": ""physical_shield.control_release"",
            ""stableEventOrder"": 335
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": {
        ""events"": [
          {
            ""frame"": 6,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": 79,
            ""abilityStableId"": 0,
            ""stableEventId"": 90,
            ""requestLabel"": ""character_test.physical_shield.create"",
            ""abilityStableLabel"": """",
            ""stableEventLabel"": ""character_test.physical_shield.cast"",
            ""stableEventOrder"": 115
          }
        ]
      },
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 89,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": """",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.katana_atk1"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.12,
            ""stableEventId"": 200,
            ""stableEventLabel"": ""physical_shield.animation"",
            ""stableEventOrder"": 333
          }
        ]
      },
      ""presentationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 89,
            ""kind"": ""SpawnVisualCue"",
            ""resourceKey"": ""vfx.magic.physical_shield:GameObject@mxframework.samples"",
            ""vfxAttachment"": {
              ""mode"": ""FollowSubject"",
              ""socketId"": """",
              ""offsetSpace"": ""Local"",
              ""transform"": {
                ""positionX"": 0,
                ""positionY"": 0,
                ""positionZ"": 0,
                ""rotationEulerX"": 0,
                ""rotationEulerY"": 0,
                ""rotationEulerZ"": 0,
                ""scaleX"": 1,
                ""scaleY"": 1,
                ""scaleZ"": 1
              }
            },
            ""vfxPlaybackMode"": ""PlayToCompletion"",
            ""vfxPlaybackRate"": 1,
            ""vfxScaleMultiplier"": 1,
            ""vfxStartOffsetFrames"": 0,
            ""vfxEndOffsetFrames"": 0,
            ""vfxNaturalEndPolicy"": ""Complete"",
            ""vfxParameter0"": 0,
            ""vfxParameter1"": 0,
            ""vfxParameter2"": 0,
            ""vfxParameter3"": 0,
            ""stableEventId"": 91,
            ""stableEventLabel"": ""character_test.physical_shield.vfx"",
            ""vfxDefinitionId"": 12031,
            ""numericSocketId"": 0,
            ""stableEventOrder"": 117
          }
        ]
      },
      ""debugTrack"": null
    },
    {
      ""actionId"": 310,
      ""displayName"": ""Fireball Homing"",
      ""category"": ""Skill"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""combat"",
        ""magic"",
        ""fireball"",
        ""homing""
      ],
      ""priority"": 30,
      ""durationFrames"": 90,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Startup"",
          ""startFrame"": 0,
          ""endFrame"": 5
        },
        {
          ""kind"": ""Active"",
          ""startFrame"": 6,
          ""endFrame"": 8
        },
        {
          ""kind"": ""Recovery"",
          ""startFrame"": 9,
          ""endFrame"": 89
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 11,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 201,
            ""stableEventLabel"": ""fireball_homing.control_lock"",
            ""stableEventOrder"": 252
          },
          {
            ""frame"": 12,
            ""endFrame"": 89,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 202,
            ""stableEventLabel"": ""fireball_homing.control_release"",
            ""stableEventOrder"": 253
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": {
        ""events"": [
          {
            ""frame"": 6,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": 31,
            ""abilityStableId"": 0,
            ""stableEventId"": 32,
            ""requestLabel"": ""character_test.fireball_homing.projectile"",
            ""abilityStableLabel"": """",
            ""stableEventLabel"": ""character_test.fireball_homing.projectile.impact"",
            ""stableEventOrder"": 78
          }
        ]
      },
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 89,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": """",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.katana_atk1"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.12,
            ""stableEventId"": 203,
            ""stableEventLabel"": ""fireball_homing.animation"",
            ""stableEventOrder"": 251
          }
        ]
      },
      ""presentationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 29,
            ""kind"": ""SpawnVisualCue"",
            ""resourceKey"": ""vfx.magic.fireball.charge:GameObject@mxframework.samples"",
            ""vfxAttachment"": {
              ""mode"": ""FollowSubject"",
              ""socketId"": """",
              ""offsetSpace"": ""Local"",
              ""transform"": {
                ""positionX"": 0,
                ""positionY"": 0,
                ""positionZ"": 0,
                ""rotationEulerX"": 0,
                ""rotationEulerY"": 0,
                ""rotationEulerZ"": 0,
                ""scaleX"": 1,
                ""scaleY"": 1,
                ""scaleZ"": 1
              }
            },
            ""vfxPlaybackMode"": ""PlayToCompletion"",
            ""vfxPlaybackRate"": 1,
            ""vfxScaleMultiplier"": 1,
            ""vfxStartOffsetFrames"": 0,
            ""vfxEndOffsetFrames"": 0,
            ""vfxNaturalEndPolicy"": ""Complete"",
            ""vfxParameter0"": 0,
            ""vfxParameter1"": 0,
            ""vfxParameter2"": 0,
            ""vfxParameter3"": 0,
            ""stableEventId"": 204,
            ""stableEventLabel"": ""character_test.fireball_homing.windup_vfx"",
            ""vfxDefinitionId"": 12004,
            ""numericSocketId"": 0,
            ""stableEventOrder"": 79
          }
        ]
      },
      ""debugTrack"": null
    },
    {
      ""actionId"": 311,
      ""displayName"": ""Fireball Bezier"",
      ""category"": ""Skill"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""combat"",
        ""magic"",
        ""fireball"",
        ""bezier""
      ],
      ""priority"": 30,
      ""durationFrames"": 90,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Startup"",
          ""startFrame"": 0,
          ""endFrame"": 5
        },
        {
          ""kind"": ""Active"",
          ""startFrame"": 6,
          ""endFrame"": 8
        },
        {
          ""kind"": ""Recovery"",
          ""startFrame"": 9,
          ""endFrame"": 89
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 11,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 205,
            ""stableEventLabel"": ""fireball_bezier.control_lock"",
            ""stableEventOrder"": 237
          },
          {
            ""frame"": 12,
            ""endFrame"": 89,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 206,
            ""stableEventLabel"": ""fireball_bezier.control_release"",
            ""stableEventOrder"": 238
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": {
        ""events"": [
          {
            ""frame"": 6,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": 33,
            ""abilityStableId"": 0,
            ""stableEventId"": 34,
            ""requestLabel"": ""character_test.fireball_bezier.projectile"",
            ""abilityStableLabel"": """",
            ""stableEventLabel"": ""character_test.fireball_bezier.projectile.impact"",
            ""stableEventOrder"": 67
          }
        ]
      },
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 89,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": """",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.katana_atk1"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.12,
            ""stableEventId"": 207,
            ""stableEventLabel"": ""fireball_bezier.animation"",
            ""stableEventOrder"": 236
          }
        ]
      },
      ""presentationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 29,
            ""kind"": ""SpawnVisualCue"",
            ""resourceKey"": ""vfx.magic.fireball.charge:GameObject@mxframework.samples"",
            ""vfxAttachment"": {
              ""mode"": ""FollowSubject"",
              ""socketId"": """",
              ""offsetSpace"": ""Local"",
              ""transform"": {
                ""positionX"": 0,
                ""positionY"": 0,
                ""positionZ"": 0,
                ""rotationEulerX"": 0,
                ""rotationEulerY"": 0,
                ""rotationEulerZ"": 0,
                ""scaleX"": 1,
                ""scaleY"": 1,
                ""scaleZ"": 1
              }
            },
            ""vfxPlaybackMode"": ""PlayToCompletion"",
            ""vfxPlaybackRate"": 1,
            ""vfxScaleMultiplier"": 1,
            ""vfxStartOffsetFrames"": 0,
            ""vfxEndOffsetFrames"": 0,
            ""vfxNaturalEndPolicy"": ""Complete"",
            ""vfxParameter0"": 0,
            ""vfxParameter1"": 0,
            ""vfxParameter2"": 0,
            ""vfxParameter3"": 0,
            ""stableEventId"": 208,
            ""stableEventLabel"": ""character_test.fireball_bezier.windup_vfx"",
            ""vfxDefinitionId"": 12004,
            ""numericSocketId"": 0,
            ""stableEventOrder"": 68
          }
        ]
      },
      ""debugTrack"": null
    },
    {
      ""actionId"": 312,
      ""displayName"": ""Fireball Explosion"",
      ""category"": ""Skill"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""combat"",
        ""magic"",
        ""fireball"",
        ""explosion""
      ],
      ""priority"": 30,
      ""durationFrames"": 90,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Startup"",
          ""startFrame"": 0,
          ""endFrame"": 5
        },
        {
          ""kind"": ""Active"",
          ""startFrame"": 6,
          ""endFrame"": 8
        },
        {
          ""kind"": ""Recovery"",
          ""startFrame"": 9,
          ""endFrame"": 89
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 11,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 209,
            ""stableEventLabel"": ""fireball_explosion.control_lock"",
            ""stableEventOrder"": 246
          },
          {
            ""frame"": 12,
            ""endFrame"": 89,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 210,
            ""stableEventLabel"": ""fireball_explosion.control_release"",
            ""stableEventOrder"": 247
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": {
        ""events"": [
          {
            ""frame"": 6,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": 35,
            ""abilityStableId"": 0,
            ""stableEventId"": 36,
            ""requestLabel"": ""character_test.fireball_explosion.projectile"",
            ""abilityStableLabel"": """",
            ""stableEventLabel"": ""character_test.fireball_explosion.projectile.impact"",
            ""stableEventOrder"": 74
          }
        ]
      },
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 89,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": """",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.katana_atk1"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.12,
            ""stableEventId"": 211,
            ""stableEventLabel"": ""fireball_explosion.animation"",
            ""stableEventOrder"": 245
          }
        ]
      },
      ""presentationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 29,
            ""kind"": ""SpawnVisualCue"",
            ""resourceKey"": ""vfx.magic.fireball_burst.charge:GameObject@mxframework.samples"",
            ""vfxAttachment"": {
              ""mode"": ""FollowSubject"",
              ""socketId"": """",
              ""offsetSpace"": ""Local"",
              ""transform"": {
                ""positionX"": 0,
                ""positionY"": 0,
                ""positionZ"": 0,
                ""rotationEulerX"": 0,
                ""rotationEulerY"": 0,
                ""rotationEulerZ"": 0,
                ""scaleX"": 1,
                ""scaleY"": 1,
                ""scaleZ"": 1
              }
            },
            ""vfxPlaybackMode"": ""PlayToCompletion"",
            ""vfxPlaybackRate"": 1,
            ""vfxScaleMultiplier"": 1,
            ""vfxStartOffsetFrames"": 0,
            ""vfxEndOffsetFrames"": 0,
            ""vfxNaturalEndPolicy"": ""Complete"",
            ""vfxParameter0"": 0,
            ""vfxParameter1"": 0,
            ""vfxParameter2"": 0,
            ""vfxParameter3"": 0,
            ""stableEventId"": 92,
            ""stableEventLabel"": ""character_test.fireball_explosion.windup_vfx"",
            ""vfxDefinitionId"": 12005,
            ""numericSocketId"": 0,
            ""stableEventOrder"": 76
          }
        ]
      },
      ""debugTrack"": null
    },
    {
      ""actionId"": 313,
      ""displayName"": ""Tornado"",
      ""category"": ""Skill"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""combat"",
        ""magic"",
        ""tornado"",
        ""wind""
      ],
      ""priority"": 30,
      ""durationFrames"": 90,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Startup"",
          ""startFrame"": 0,
          ""endFrame"": 5
        },
        {
          ""kind"": ""Active"",
          ""startFrame"": 6,
          ""endFrame"": 8
        },
        {
          ""kind"": ""Recovery"",
          ""startFrame"": 9,
          ""endFrame"": 89
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 11,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 212,
            ""stableEventLabel"": ""tornado.control_lock"",
            ""stableEventOrder"": 367
          },
          {
            ""frame"": 12,
            ""endFrame"": 89,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 213,
            ""stableEventLabel"": ""tornado.control_release"",
            ""stableEventOrder"": 368
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": {
        ""events"": [
          {
            ""frame"": 6,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": 37,
            ""abilityStableId"": 0,
            ""stableEventId"": 38,
            ""requestLabel"": ""character_test.tornado.projectile"",
            ""abilityStableLabel"": """",
            ""stableEventLabel"": ""character_test.tornado.projectile.impact"",
            ""stableEventOrder"": 143
          }
        ]
      },
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 89,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": """",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.katana_atk1"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.12,
            ""stableEventId"": 214,
            ""stableEventLabel"": ""tornado.animation"",
            ""stableEventOrder"": 366
          }
        ]
      },
      ""presentationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 29,
            ""kind"": ""SpawnVisualCue"",
            ""resourceKey"": ""vfx.magic.tornado.charge:GameObject@mxframework.samples"",
            ""vfxAttachment"": {
              ""mode"": ""FollowSubject"",
              ""socketId"": """",
              ""offsetSpace"": ""Local"",
              ""transform"": {
                ""positionX"": 0,
                ""positionY"": 0,
                ""positionZ"": 0,
                ""rotationEulerX"": 0,
                ""rotationEulerY"": 0,
                ""rotationEulerZ"": 0,
                ""scaleX"": 1,
                ""scaleY"": 1,
                ""scaleZ"": 1
              }
            },
            ""vfxPlaybackMode"": ""PlayToCompletion"",
            ""vfxPlaybackRate"": 1,
            ""vfxScaleMultiplier"": 1,
            ""vfxStartOffsetFrames"": 0,
            ""vfxEndOffsetFrames"": 0,
            ""vfxNaturalEndPolicy"": ""Complete"",
            ""vfxParameter0"": 0,
            ""vfxParameter1"": 0,
            ""vfxParameter2"": 0,
            ""vfxParameter3"": 0,
            ""stableEventId"": 93,
            ""stableEventLabel"": ""character_test.tornado.windup_vfx"",
            ""vfxDefinitionId"": 12008,
            ""numericSocketId"": 0,
            ""stableEventOrder"": 145
          }
        ]
      },
      ""debugTrack"": null
    },
    {
      ""actionId"": 315,
      ""displayName"": ""Beam Lance"",
      ""category"": ""Skill"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""combat"",
        ""magic"",
        ""beam_lance"",
        ""pierce""
      ],
      ""priority"": 30,
      ""durationFrames"": 90,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Startup"",
          ""startFrame"": 0,
          ""endFrame"": 29
        },
        {
          ""kind"": ""Active"",
          ""startFrame"": 30,
          ""endFrame"": 30
        },
        {
          ""kind"": ""Recovery"",
          ""startFrame"": 31,
          ""endFrame"": 89
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 59,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 215,
            ""stableEventLabel"": ""beam_lance.control_lock"",
            ""stableEventOrder"": 21
          },
          {
            ""frame"": 60,
            ""endFrame"": 89,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 216,
            ""stableEventLabel"": ""beam_lance.control_release"",
            ""stableEventOrder"": 22
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": {
        ""events"": [
          {
            ""frame"": 30,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": 40,
            ""abilityStableId"": 0,
            ""stableEventId"": 41,
            ""requestLabel"": ""character_test.beam_lance.projectile"",
            ""abilityStableLabel"": """",
            ""stableEventLabel"": ""character_test.beam_lance.projectile.impact"",
            ""stableEventOrder"": 49
          }
        ]
      },
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 89,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": """",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.katana_atk1"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.08,
            ""stableEventId"": 217,
            ""stableEventLabel"": ""beam_lance.animation"",
            ""stableEventOrder"": 20
          }
        ]
      },
      ""presentationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 29,
            ""kind"": ""SpawnVisualCue"",
            ""resourceKey"": ""vfx.magic.beam_lance.charge:GameObject@mxframework.samples"",
            ""vfxAttachment"": {
              ""mode"": ""FollowSubject"",
              ""socketId"": """",
              ""offsetSpace"": ""Local"",
              ""transform"": {
                ""positionX"": 0,
                ""positionY"": 0,
                ""positionZ"": 0,
                ""rotationEulerX"": 0,
                ""rotationEulerY"": 0,
                ""rotationEulerZ"": 0,
                ""scaleX"": 1,
                ""scaleY"": 1,
                ""scaleZ"": 1
              }
            },
            ""vfxPlaybackMode"": ""PlayToCompletion"",
            ""vfxPlaybackRate"": 1,
            ""vfxScaleMultiplier"": 1,
            ""vfxStartOffsetFrames"": 0,
            ""vfxEndOffsetFrames"": 0,
            ""vfxNaturalEndPolicy"": ""Complete"",
            ""vfxParameter0"": 0,
            ""vfxParameter1"": 0,
            ""vfxParameter2"": 0,
            ""vfxParameter3"": 0,
            ""stableEventId"": 100,
            ""stableEventLabel"": ""character_test.beam_lance.charge_vfx"",
            ""vfxDefinitionId"": 12018,
            ""numericSocketId"": 0,
            ""stableEventOrder"": 47
          },
          {
            ""frame"": 30,
            ""endFrame"": 89,
            ""kind"": ""SpawnVisualCue"",
            ""resourceKey"": ""vfx.magic.beam_lance.beam:GameObject@mxframework.samples"",
            ""vfxAttachment"": {
              ""mode"": ""FollowSubject"",
              ""socketId"": """",
              ""offsetSpace"": ""Local"",
              ""transform"": {
                ""positionX"": 0,
                ""positionY"": 0,
                ""positionZ"": 0,
                ""rotationEulerX"": 0,
                ""rotationEulerY"": 0,
                ""rotationEulerZ"": 0,
                ""scaleX"": 1,
                ""scaleY"": 1,
                ""scaleZ"": 1
              }
            },
            ""vfxPlaybackMode"": ""PlayToCompletion"",
            ""vfxPlaybackRate"": 1,
            ""vfxScaleMultiplier"": 1,
            ""vfxStartOffsetFrames"": 0,
            ""vfxEndOffsetFrames"": 0,
            ""vfxNaturalEndPolicy"": ""Complete"",
            ""vfxParameter0"": 0,
            ""vfxParameter1"": 0,
            ""vfxParameter2"": 0,
            ""vfxParameter3"": 0,
            ""stableEventId"": 101,
            ""stableEventLabel"": ""character_test.beam_lance.beam_vfx"",
            ""vfxDefinitionId"": 12016,
            ""numericSocketId"": 0,
            ""stableEventOrder"": 46
          }
        ]
      },
      ""debugTrack"": null
    },
    {
      ""actionId"": 316,
      ""displayName"": ""Channel Beam"",
      ""category"": ""Skill"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""combat"",
        ""magic"",
        ""channel_beam"",
        ""laser"",
        ""channel""
      ],
      ""priority"": 30,
      ""durationFrames"": 120,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Startup"",
          ""startFrame"": 0,
          ""endFrame"": 29
        },
        {
          ""kind"": ""Active"",
          ""startFrame"": 30,
          ""endFrame"": 89
        },
        {
          ""kind"": ""Recovery"",
          ""startFrame"": 90,
          ""endFrame"": 119
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 118,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 218,
            ""stableEventLabel"": ""channel_beam.control_lock"",
            ""stableEventOrder"": 27
          },
          {
            ""frame"": 119,
            ""endFrame"": 119,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 219,
            ""stableEventLabel"": ""channel_beam.control_release"",
            ""stableEventOrder"": 28
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": {
        ""events"": [
          {
            ""frame"": 30,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": 42,
            ""abilityStableId"": 0,
            ""stableEventId"": 43,
            ""requestLabel"": ""character_test.channel_beam.tick_projectile"",
            ""abilityStableLabel"": """",
            ""stableEventLabel"": ""character_test.channel_beam.tick_projectile.impact"",
            ""stableEventOrder"": 58
          },
          {
            ""frame"": 36,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": 42,
            ""abilityStableId"": 0,
            ""stableEventId"": 43,
            ""requestLabel"": ""character_test.channel_beam.tick_projectile"",
            ""abilityStableLabel"": """",
            ""stableEventLabel"": ""character_test.channel_beam.tick_projectile.impact"",
            ""stableEventOrder"": 58
          },
          {
            ""frame"": 42,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": 42,
            ""abilityStableId"": 0,
            ""stableEventId"": 43,
            ""requestLabel"": ""character_test.channel_beam.tick_projectile"",
            ""abilityStableLabel"": """",
            ""stableEventLabel"": ""character_test.channel_beam.tick_projectile.impact"",
            ""stableEventOrder"": 58
          },
          {
            ""frame"": 48,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": 42,
            ""abilityStableId"": 0,
            ""stableEventId"": 43,
            ""requestLabel"": ""character_test.channel_beam.tick_projectile"",
            ""abilityStableLabel"": """",
            ""stableEventLabel"": ""character_test.channel_beam.tick_projectile.impact"",
            ""stableEventOrder"": 58
          },
          {
            ""frame"": 54,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": 42,
            ""abilityStableId"": 0,
            ""stableEventId"": 43,
            ""requestLabel"": ""character_test.channel_beam.tick_projectile"",
            ""abilityStableLabel"": """",
            ""stableEventLabel"": ""character_test.channel_beam.tick_projectile.impact"",
            ""stableEventOrder"": 58
          },
          {
            ""frame"": 60,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": 42,
            ""abilityStableId"": 0,
            ""stableEventId"": 43,
            ""requestLabel"": ""character_test.channel_beam.tick_projectile"",
            ""abilityStableLabel"": """",
            ""stableEventLabel"": ""character_test.channel_beam.tick_projectile.impact"",
            ""stableEventOrder"": 58
          },
          {
            ""frame"": 66,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": 42,
            ""abilityStableId"": 0,
            ""stableEventId"": 43,
            ""requestLabel"": ""character_test.channel_beam.tick_projectile"",
            ""abilityStableLabel"": """",
            ""stableEventLabel"": ""character_test.channel_beam.tick_projectile.impact"",
            ""stableEventOrder"": 58
          },
          {
            ""frame"": 72,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": 42,
            ""abilityStableId"": 0,
            ""stableEventId"": 43,
            ""requestLabel"": ""character_test.channel_beam.tick_projectile"",
            ""abilityStableLabel"": """",
            ""stableEventLabel"": ""character_test.channel_beam.tick_projectile.impact"",
            ""stableEventOrder"": 58
          },
          {
            ""frame"": 78,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": 42,
            ""abilityStableId"": 0,
            ""stableEventId"": 43,
            ""requestLabel"": ""character_test.channel_beam.tick_projectile"",
            ""abilityStableLabel"": """",
            ""stableEventLabel"": ""character_test.channel_beam.tick_projectile.impact"",
            ""stableEventOrder"": 58
          },
          {
            ""frame"": 84,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": 42,
            ""abilityStableId"": 0,
            ""stableEventId"": 43,
            ""requestLabel"": ""character_test.channel_beam.tick_projectile"",
            ""abilityStableLabel"": """",
            ""stableEventLabel"": ""character_test.channel_beam.tick_projectile.impact"",
            ""stableEventOrder"": 58
          }
        ]
      },
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 119,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": """",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.katana_atk1"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.08,
            ""stableEventId"": 220,
            ""stableEventLabel"": ""channel_beam.animation"",
            ""stableEventOrder"": 26
          }
        ]
      },
      ""presentationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 29,
            ""kind"": ""SpawnVisualCue"",
            ""resourceKey"": ""vfx.magic.channel_beam.charge:GameObject@mxframework.samples"",
            ""vfxAttachment"": {
              ""mode"": ""FollowSubject"",
              ""socketId"": """",
              ""offsetSpace"": ""Local"",
              ""transform"": {
                ""positionX"": 0,
                ""positionY"": 0,
                ""positionZ"": 0,
                ""rotationEulerX"": 0,
                ""rotationEulerY"": 0,
                ""rotationEulerZ"": 0,
                ""scaleX"": 1,
                ""scaleY"": 1,
                ""scaleZ"": 1
              }
            },
            ""vfxPlaybackMode"": ""PlayToCompletion"",
            ""vfxPlaybackRate"": 1,
            ""vfxScaleMultiplier"": 1,
            ""vfxStartOffsetFrames"": 0,
            ""vfxEndOffsetFrames"": 0,
            ""vfxNaturalEndPolicy"": ""Complete"",
            ""vfxParameter0"": 0,
            ""vfxParameter1"": 0,
            ""vfxParameter2"": 0,
            ""vfxParameter3"": 0,
            ""stableEventId"": 102,
            ""stableEventLabel"": ""character_test.channel_beam.charge_vfx"",
            ""vfxDefinitionId"": 12019,
            ""numericSocketId"": 0,
            ""stableEventOrder"": 56
          },
          {
            ""frame"": 30,
            ""endFrame"": 119,
            ""kind"": ""SpawnVisualCue"",
            ""resourceKey"": ""vfx.magic.channel_beam.beam:GameObject@mxframework.samples"",
            ""vfxAttachment"": {
              ""mode"": ""FollowSubject"",
              ""socketId"": """",
              ""offsetSpace"": ""Local"",
              ""transform"": {
                ""positionX"": 0,
                ""positionY"": 0,
                ""positionZ"": 0,
                ""rotationEulerX"": 0,
                ""rotationEulerY"": 0,
                ""rotationEulerZ"": 0,
                ""scaleX"": 1,
                ""scaleY"": 1,
                ""scaleZ"": 1
              }
            },
            ""vfxPlaybackMode"": ""PlayToCompletion"",
            ""vfxPlaybackRate"": 1,
            ""vfxScaleMultiplier"": 1,
            ""vfxStartOffsetFrames"": 0,
            ""vfxEndOffsetFrames"": 0,
            ""vfxNaturalEndPolicy"": ""Complete"",
            ""vfxParameter0"": 0,
            ""vfxParameter1"": 0,
            ""vfxParameter2"": 0,
            ""vfxParameter3"": 0,
            ""stableEventId"": 103,
            ""stableEventLabel"": ""character_test.channel_beam.beam_vfx"",
            ""vfxDefinitionId"": 12020,
            ""numericSocketId"": 0,
            ""stableEventOrder"": 55
          }
        ]
      },
      ""debugTrack"": null
    },
    {
      ""actionId"": 317,
      ""displayName"": ""Laser Lance"",
      ""category"": ""Skill"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""combat"",
        ""magic"",
        ""laser_lance"",
        ""single_target""
      ],
      ""priority"": 30,
      ""durationFrames"": 90,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Startup"",
          ""startFrame"": 0,
          ""endFrame"": 29
        },
        {
          ""kind"": ""Active"",
          ""startFrame"": 30,
          ""endFrame"": 30
        },
        {
          ""kind"": ""Recovery"",
          ""startFrame"": 31,
          ""endFrame"": 89
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 59,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 221,
            ""stableEventLabel"": ""laser_lance.control_lock"",
            ""stableEventOrder"": 316
          },
          {
            ""frame"": 60,
            ""endFrame"": 89,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 222,
            ""stableEventLabel"": ""laser_lance.control_release"",
            ""stableEventOrder"": 317
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": {
        ""events"": [
          {
            ""frame"": 30,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": 44,
            ""abilityStableId"": 0,
            ""stableEventId"": 45,
            ""requestLabel"": ""character_test.laser_lance.projectile"",
            ""abilityStableLabel"": """",
            ""stableEventLabel"": ""character_test.laser_lance.projectile.impact"",
            ""stableEventOrder"": 103
          }
        ]
      },
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 89,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": """",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.katana_atk1"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.08,
            ""stableEventId"": 223,
            ""stableEventLabel"": ""laser_lance.animation"",
            ""stableEventOrder"": 315
          }
        ]
      },
      ""presentationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 29,
            ""kind"": ""SpawnVisualCue"",
            ""resourceKey"": ""vfx.magic.laser_lance.charge:GameObject@mxframework.samples"",
            ""vfxAttachment"": {
              ""mode"": ""FollowSubject"",
              ""socketId"": """",
              ""offsetSpace"": ""Local"",
              ""transform"": {
                ""positionX"": 0,
                ""positionY"": 0,
                ""positionZ"": 0,
                ""rotationEulerX"": 0,
                ""rotationEulerY"": 0,
                ""rotationEulerZ"": 0,
                ""scaleX"": 1,
                ""scaleY"": 1,
                ""scaleZ"": 1
              }
            },
            ""vfxPlaybackMode"": ""PlayToCompletion"",
            ""vfxPlaybackRate"": 1,
            ""vfxScaleMultiplier"": 1,
            ""vfxStartOffsetFrames"": 0,
            ""vfxEndOffsetFrames"": 0,
            ""vfxNaturalEndPolicy"": ""Complete"",
            ""vfxParameter0"": 0,
            ""vfxParameter1"": 0,
            ""vfxParameter2"": 0,
            ""vfxParameter3"": 0,
            ""stableEventId"": 104,
            ""stableEventLabel"": ""character_test.laser_lance.charge_vfx"",
            ""vfxDefinitionId"": 12022,
            ""numericSocketId"": 0,
            ""stableEventOrder"": 100
          }
        ]
      },
      ""debugTrack"": null
    },
    {
      ""actionId"": 318,
      ""displayName"": ""Chain Lightning"",
      ""category"": ""Skill"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""combat"",
        ""magic"",
        ""lightning"",
        ""chain""
      ],
      ""priority"": 30,
      ""durationFrames"": 90,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Startup"",
          ""startFrame"": 0,
          ""endFrame"": 29
        },
        {
          ""kind"": ""Active"",
          ""startFrame"": 30,
          ""endFrame"": 30
        },
        {
          ""kind"": ""Recovery"",
          ""startFrame"": 31,
          ""endFrame"": 89
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 59,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 224,
            ""stableEventLabel"": ""chain_lightning.control_lock"",
            ""stableEventOrder"": 24
          },
          {
            ""frame"": 60,
            ""endFrame"": 89,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 225,
            ""stableEventLabel"": ""chain_lightning.control_release"",
            ""stableEventOrder"": 25
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": {
        ""events"": [
          {
            ""frame"": 30,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": 105,
            ""abilityStableId"": 0,
            ""stableEventId"": 106,
            ""requestLabel"": ""character_test.chain_lightning.cast"",
            ""abilityStableLabel"": """",
            ""stableEventLabel"": ""character_test.chain_lightning.hit"",
            ""stableEventOrder"": 53
          }
        ]
      },
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 89,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": """",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.katana_atk1"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.08,
            ""stableEventId"": 226,
            ""stableEventLabel"": ""chain_lightning.animation"",
            ""stableEventOrder"": 23
          }
        ]
      },
      ""presentationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 29,
            ""kind"": ""SpawnVisualCue"",
            ""resourceKey"": ""vfx.magic.chain_lightning.charge:GameObject@mxframework.samples"",
            ""vfxAttachment"": {
              ""mode"": ""FollowSubject"",
              ""socketId"": """",
              ""offsetSpace"": ""Local"",
              ""transform"": {
                ""positionX"": 0,
                ""positionY"": 0,
                ""positionZ"": 0,
                ""rotationEulerX"": 0,
                ""rotationEulerY"": 0,
                ""rotationEulerZ"": 0,
                ""scaleX"": 1,
                ""scaleY"": 1,
                ""scaleZ"": 1
              }
            },
            ""vfxPlaybackMode"": ""PlayToCompletion"",
            ""vfxPlaybackRate"": 1,
            ""vfxScaleMultiplier"": 1,
            ""vfxStartOffsetFrames"": 0,
            ""vfxEndOffsetFrames"": 0,
            ""vfxNaturalEndPolicy"": ""Complete"",
            ""vfxParameter0"": 0,
            ""vfxParameter1"": 0,
            ""vfxParameter2"": 0,
            ""vfxParameter3"": 0,
            ""stableEventId"": 107,
            ""stableEventLabel"": ""character_test.chain_lightning.charge_vfx"",
            ""vfxDefinitionId"": 12025,
            ""numericSocketId"": 0,
            ""stableEventOrder"": 52
          }
        ]
      },
      ""debugTrack"": null
    },
    {
      ""actionId"": 319,
      ""displayName"": ""Poison Bottle"",
      ""category"": ""Skill"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""combat"",
        ""magic"",
        ""poison"",
        ""bottle"",
        ""pool""
      ],
      ""priority"": 30,
      ""durationFrames"": 90,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Startup"",
          ""startFrame"": 0,
          ""endFrame"": 15
        },
        {
          ""kind"": ""Active"",
          ""startFrame"": 16,
          ""endFrame"": 34
        },
        {
          ""kind"": ""Recovery"",
          ""startFrame"": 35,
          ""endFrame"": 89
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 59,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 227,
            ""stableEventLabel"": ""poison_bottle.control_lock"",
            ""stableEventOrder"": 337
          },
          {
            ""frame"": 60,
            ""endFrame"": 89,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 228,
            ""stableEventLabel"": ""poison_bottle.control_release"",
            ""stableEventOrder"": 338
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": {
        ""events"": [
          {
            ""frame"": 30,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": 46,
            ""abilityStableId"": 0,
            ""stableEventId"": 47,
            ""requestLabel"": ""character_test.poison_bottle.projectile"",
            ""abilityStableLabel"": """",
            ""stableEventLabel"": ""character_test.poison_bottle.projectile.shatter"",
            ""stableEventOrder"": 121
          }
        ]
      },
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 89,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": """",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.katana_atk1"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.08,
            ""stableEventId"": 229,
            ""stableEventLabel"": ""poison_bottle.animation"",
            ""stableEventOrder"": 336
          }
        ]
      },
      ""presentationTrack"": null,
      ""debugTrack"": null
    },
    {
      ""actionId"": 321,
      ""displayName"": ""Stun Bolt"",
      ""category"": ""Skill"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""combat"",
        ""magic"",
        ""control"",
        ""stun"",
        ""stun_bolt""
      ],
      ""priority"": 30,
      ""durationFrames"": 90,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Startup"",
          ""startFrame"": 0,
          ""endFrame"": 29
        },
        {
          ""kind"": ""Active"",
          ""startFrame"": 30,
          ""endFrame"": 30
        },
        {
          ""kind"": ""Recovery"",
          ""startFrame"": 31,
          ""endFrame"": 89
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 59,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 230,
            ""stableEventLabel"": ""stun_bolt.control_lock"",
            ""stableEventOrder"": 346
          },
          {
            ""frame"": 60,
            ""endFrame"": 89,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 231,
            ""stableEventLabel"": ""stun_bolt.control_release"",
            ""stableEventOrder"": 347
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": {
        ""events"": [
          {
            ""frame"": 30,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": 48,
            ""abilityStableId"": 0,
            ""stableEventId"": 49,
            ""requestLabel"": ""character_test.stun_bolt.projectile"",
            ""abilityStableLabel"": """",
            ""stableEventLabel"": ""character_test.stun_bolt.projectile.impact"",
            ""stableEventOrder"": 137
          }
        ]
      },
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 89,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": """",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.katana_atk1"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.08,
            ""stableEventId"": 232,
            ""stableEventLabel"": ""stun_bolt.animation"",
            ""stableEventOrder"": 345
          }
        ]
      },
      ""presentationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 29,
            ""kind"": ""SpawnVisualCue"",
            ""resourceKey"": ""vfx.magic.stun_bolt.charge:GameObject@mxframework.samples"",
            ""vfxAttachment"": {
              ""mode"": ""FollowSubject"",
              ""socketId"": """",
              ""offsetSpace"": ""Local"",
              ""transform"": {
                ""positionX"": 0,
                ""positionY"": 0,
                ""positionZ"": 0,
                ""rotationEulerX"": 0,
                ""rotationEulerY"": 0,
                ""rotationEulerZ"": 0,
                ""scaleX"": 1,
                ""scaleY"": 1,
                ""scaleZ"": 1
              }
            },
            ""vfxPlaybackMode"": ""PlayToCompletion"",
            ""vfxPlaybackRate"": 1,
            ""vfxScaleMultiplier"": 1,
            ""vfxStartOffsetFrames"": 0,
            ""vfxEndOffsetFrames"": 0,
            ""vfxNaturalEndPolicy"": ""Complete"",
            ""vfxParameter0"": 0,
            ""vfxParameter1"": 0,
            ""vfxParameter2"": 0,
            ""vfxParameter3"": 0,
            ""stableEventId"": 110,
            ""stableEventLabel"": ""character_test.stun_bolt.charge_vfx"",
            ""vfxDefinitionId"": 12034,
            ""numericSocketId"": 0,
            ""stableEventOrder"": 135
          }
        ]
      },
      ""debugTrack"": null
    },
    {
      ""actionId"": 314,
      ""displayName"": ""Area Confinement"",
      ""category"": ""Skill"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""combat"",
        ""magic"",
        ""area"",
        ""root""
      ],
      ""priority"": 30,
      ""durationFrames"": 90,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Startup"",
          ""startFrame"": 0,
          ""endFrame"": 5
        },
        {
          ""kind"": ""Active"",
          ""startFrame"": 6,
          ""endFrame"": 65
        },
        {
          ""kind"": ""Recovery"",
          ""startFrame"": 66,
          ""endFrame"": 89
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 11,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 95,
            ""stableEventLabel"": ""area_confinement.control_lock"",
            ""stableEventOrder"": 16
          },
          {
            ""frame"": 12,
            ""endFrame"": 65,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 1,
            ""stableEventId"": 96,
            ""stableEventLabel"": ""area_confinement.channel_movement_lock"",
            ""stableEventOrder"": 15
          },
          {
            ""frame"": 66,
            ""endFrame"": 89,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 97,
            ""stableEventLabel"": ""area_confinement.control_release"",
            ""stableEventOrder"": 17
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": {
        ""events"": [
          {
            ""frame"": 6,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": 78,
            ""abilityStableId"": 0,
            ""stableEventId"": 94,
            ""requestLabel"": ""character_test.area_confinement.zone"",
            ""abilityStableLabel"": """",
            ""stableEventLabel"": ""character_test.area_confinement.cast"",
            ""stableEventOrder"": 38
          }
        ]
      },
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 89,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": """",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.katana_atk1"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.12,
            ""stableEventId"": 233,
            ""stableEventLabel"": ""area_confinement.animation"",
            ""stableEventOrder"": 14
          }
        ]
      },
      ""presentationTrack"": null,
      ""debugTrack"": null
    },
    {
      ""actionId"": 322,
      ""displayName"": ""Meteor Barrage"",
      ""category"": ""Skill"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""combat"",
        ""magic"",
        ""area"",
        ""meteor"",
        ""meteor_barrage""
      ],
      ""priority"": 30,
      ""durationFrames"": 90,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Startup"",
          ""startFrame"": 0,
          ""endFrame"": 23
        },
        {
          ""kind"": ""Active"",
          ""startFrame"": 24,
          ""endFrame"": 60
        },
        {
          ""kind"": ""Recovery"",
          ""startFrame"": 61,
          ""endFrame"": 89
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 59,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 234,
            ""stableEventLabel"": ""meteor_barrage.control_lock"",
            ""stableEventOrder"": 331
          },
          {
            ""frame"": 60,
            ""endFrame"": 89,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 235,
            ""stableEventLabel"": ""meteor_barrage.control_release"",
            ""stableEventOrder"": 332
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": 111,
            ""abilityStableId"": 0,
            ""stableEventId"": 76,
            ""requestLabel"": ""character_test.meteor_barrage.telegraph"",
            ""abilityStableLabel"": """",
            ""stableEventLabel"": ""character_test.meteor_barrage.telegraph_vfx"",
            ""stableEventOrder"": 114
          },
          {
            ""frame"": 24,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": 50,
            ""abilityStableId"": 0,
            ""stableEventId"": 16,
            ""requestLabel"": ""character_test.meteor_barrage.projectile.0"",
            ""abilityStableLabel"": """",
            ""stableEventLabel"": ""character_test.meteor_barrage.projectile.impact"",
            ""stableEventOrder"": 112
          },
          {
            ""frame"": 30,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": 51,
            ""abilityStableId"": 0,
            ""stableEventId"": 16,
            ""requestLabel"": ""character_test.meteor_barrage.projectile.1"",
            ""abilityStableLabel"": """",
            ""stableEventLabel"": ""character_test.meteor_barrage.projectile.impact"",
            ""stableEventOrder"": 112
          },
          {
            ""frame"": 36,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": 52,
            ""abilityStableId"": 0,
            ""stableEventId"": 16,
            ""requestLabel"": ""character_test.meteor_barrage.projectile.2"",
            ""abilityStableLabel"": """",
            ""stableEventLabel"": ""character_test.meteor_barrage.projectile.impact"",
            ""stableEventOrder"": 112
          },
          {
            ""frame"": 42,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": 53,
            ""abilityStableId"": 0,
            ""stableEventId"": 16,
            ""requestLabel"": ""character_test.meteor_barrage.projectile.3"",
            ""abilityStableLabel"": """",
            ""stableEventLabel"": ""character_test.meteor_barrage.projectile.impact"",
            ""stableEventOrder"": 112
          },
          {
            ""frame"": 48,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": 54,
            ""abilityStableId"": 0,
            ""stableEventId"": 16,
            ""requestLabel"": ""character_test.meteor_barrage.projectile.4"",
            ""abilityStableLabel"": """",
            ""stableEventLabel"": ""character_test.meteor_barrage.projectile.impact"",
            ""stableEventOrder"": 112
          },
          {
            ""frame"": 54,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": 55,
            ""abilityStableId"": 0,
            ""stableEventId"": 16,
            ""requestLabel"": ""character_test.meteor_barrage.projectile.5"",
            ""abilityStableLabel"": """",
            ""stableEventLabel"": ""character_test.meteor_barrage.projectile.impact"",
            ""stableEventOrder"": 112
          }
        ]
      },
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 89,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": """",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.katana_atk1"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.08,
            ""stableEventId"": 236,
            ""stableEventLabel"": ""meteor_barrage.animation"",
            ""stableEventOrder"": 330
          }
        ]
      },
      ""presentationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 29,
            ""kind"": ""SpawnVisualCue"",
            ""resourceKey"": ""vfx.magic.meteor_barrage.charge:GameObject@mxframework.samples"",
            ""vfxAttachment"": {
              ""mode"": ""FollowSubject"",
              ""socketId"": """",
              ""offsetSpace"": ""Local"",
              ""transform"": {
                ""positionX"": 0,
                ""positionY"": 0,
                ""positionZ"": 0,
                ""rotationEulerX"": 0,
                ""rotationEulerY"": 0,
                ""rotationEulerZ"": 0,
                ""scaleX"": 1,
                ""scaleY"": 1,
                ""scaleZ"": 1
              }
            },
            ""vfxPlaybackMode"": ""PlayToCompletion"",
            ""vfxPlaybackRate"": 1,
            ""vfxScaleMultiplier"": 1,
            ""vfxStartOffsetFrames"": 0,
            ""vfxEndOffsetFrames"": 0,
            ""vfxNaturalEndPolicy"": ""Complete"",
            ""vfxParameter0"": 0,
            ""vfxParameter1"": 0,
            ""vfxParameter2"": 0,
            ""vfxParameter3"": 0,
            ""stableEventId"": 112,
            ""stableEventLabel"": ""character_test.meteor_barrage.charge_vfx"",
            ""vfxDefinitionId"": 12036,
            ""numericSocketId"": 0,
            ""stableEventOrder"": 105
          }
        ]
      },
      ""debugTrack"": null
    },
    {
      ""actionId"": 323,
      ""displayName"": ""Sword Wave"",
      ""category"": ""Skill"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""combat"",
        ""magic"",
        ""wind"",
        ""sword_wave"",
        ""slash"",
        ""pierce""
      ],
      ""priority"": 30,
      ""durationFrames"": 155,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Startup"",
          ""startFrame"": 0,
          ""endFrame"": 12
        },
        {
          ""kind"": ""Active"",
          ""startFrame"": 13,
          ""endFrame"": 133
        },
        {
          ""kind"": ""Recovery"",
          ""startFrame"": 134,
          ""endFrame"": 154
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 23,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 237,
            ""stableEventLabel"": ""sword_wave.control_lock"",
            ""stableEventOrder"": 364
          },
          {
            ""frame"": 24,
            ""endFrame"": 154,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 238,
            ""stableEventLabel"": ""sword_wave.control_release"",
            ""stableEventOrder"": 365
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": {
        ""events"": [
          {
            ""frame"": 13,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": 56,
            ""abilityStableId"": 0,
            ""stableEventId"": 57,
            ""requestLabel"": ""character_test.sword_wave.projectile"",
            ""abilityStableLabel"": """",
            ""stableEventLabel"": ""character_test.sword_wave.projectile.impact"",
            ""stableEventOrder"": 140
          },
          {
            ""frame"": 73,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": 56,
            ""abilityStableId"": 0,
            ""stableEventId"": 57,
            ""requestLabel"": ""character_test.sword_wave.projectile"",
            ""abilityStableLabel"": """",
            ""stableEventLabel"": ""character_test.sword_wave.projectile.impact"",
            ""stableEventOrder"": 140
          },
          {
            ""frame"": 133,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": 56,
            ""abilityStableId"": 0,
            ""stableEventId"": 57,
            ""requestLabel"": ""character_test.sword_wave.projectile"",
            ""abilityStableLabel"": """",
            ""stableEventLabel"": ""character_test.sword_wave.projectile.impact"",
            ""stableEventOrder"": 140
          }
        ]
      },
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 34,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": """",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.katana_atk1"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.08,
            ""stableEventId"": 239,
            ""stableEventLabel"": ""sword_wave.animation"",
            ""stableEventOrder"": 363
          }
        ]
      },
      ""presentationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 29,
            ""kind"": ""SpawnVisualCue"",
            ""resourceKey"": ""vfx.magic.sword_wave.charge:GameObject@mxframework.samples"",
            ""vfxAttachment"": {
              ""mode"": ""FollowSubject"",
              ""socketId"": """",
              ""offsetSpace"": ""Local"",
              ""transform"": {
                ""positionX"": 0,
                ""positionY"": 0,
                ""positionZ"": 0,
                ""rotationEulerX"": 0,
                ""rotationEulerY"": 0,
                ""rotationEulerZ"": 0,
                ""scaleX"": 1,
                ""scaleY"": 1,
                ""scaleZ"": 1
              }
            },
            ""vfxPlaybackMode"": ""PlayToCompletion"",
            ""vfxPlaybackRate"": 1,
            ""vfxScaleMultiplier"": 1,
            ""vfxStartOffsetFrames"": 0,
            ""vfxEndOffsetFrames"": 0,
            ""vfxNaturalEndPolicy"": ""Complete"",
            ""vfxParameter0"": 0,
            ""vfxParameter1"": 0,
            ""vfxParameter2"": 0,
            ""vfxParameter3"": 0,
            ""stableEventId"": 113,
            ""stableEventLabel"": ""character_test.sword_wave.charge_vfx"",
            ""vfxDefinitionId"": 12039,
            ""numericSocketId"": 0,
            ""stableEventOrder"": 138
          }
        ]
      },
      ""debugTrack"": null
    },
    {
      ""actionId"": 327,
      ""displayName"": ""Dash Strike"",
      ""category"": ""Skill"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""combat"",
        ""melee"",
        ""mobility"",
        ""dash_strike"",
        ""slash""
      ],
      ""priority"": 30,
      ""durationFrames"": 90,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Startup"",
          ""startFrame"": 0,
          ""endFrame"": 5
        },
        {
          ""kind"": ""Active"",
          ""startFrame"": 6,
          ""endFrame"": 19
        },
        {
          ""kind"": ""Recovery"",
          ""startFrame"": 20,
          ""endFrame"": 89
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": true,
        ""rootMotion"": {
          ""sourceAnimationEventId"": 0,
          ""sourceAnimationClipResourceKey"": """",
          ""space"": ""Local"",
          ""sampleTickRate"": 60,
          ""obstaclePolicy"": ""StaticOnly"",
          ""frames"": [
            {
              ""frame"": 6,
              ""x"": 0,
              ""y"": 0,
              ""z"": 0.3
            },
            {
              ""frame"": 7,
              ""x"": 0,
              ""y"": 0,
              ""z"": 0.3
            },
            {
              ""frame"": 8,
              ""x"": 0,
              ""y"": 0,
              ""z"": 0.3
            },
            {
              ""frame"": 9,
              ""x"": 0,
              ""y"": 0,
              ""z"": 0.3
            },
            {
              ""frame"": 10,
              ""x"": 0,
              ""y"": 0,
              ""z"": 0.3
            },
            {
              ""frame"": 11,
              ""x"": 0,
              ""y"": 0,
              ""z"": 0.3
            },
            {
              ""frame"": 12,
              ""x"": 0,
              ""y"": 0,
              ""z"": 0.3
            },
            {
              ""frame"": 13,
              ""x"": 0,
              ""y"": 0,
              ""z"": 0.3
            },
            {
              ""frame"": 14,
              ""x"": 0,
              ""y"": 0,
              ""z"": 0.3
            },
            {
              ""frame"": 15,
              ""x"": 0,
              ""y"": 0,
              ""z"": 0.3
            },
            {
              ""frame"": 16,
              ""x"": 0,
              ""y"": 0,
              ""z"": 0.3
            },
            {
              ""frame"": 17,
              ""x"": 0,
              ""y"": 0,
              ""z"": 0.3
            },
            {
              ""frame"": 18,
              ""x"": 0,
              ""y"": 0,
              ""z"": 0.3
            },
            {
              ""frame"": 19,
              ""x"": 0,
              ""y"": 0,
              ""z"": 0.3
            }
          ],
          ""sourceAnimationEventLabel"": """"
        },
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 19,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 115,
            ""stableEventLabel"": ""dash_strike.control_lock"",
            ""stableEventOrder"": 231
          },
          {
            ""frame"": 20,
            ""endFrame"": 89,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 116,
            ""stableEventLabel"": ""dash_strike.control_release"",
            ""stableEventOrder"": 232
          }
        ]
      },
      ""combatTrack"": {
        ""combatActionId"": 8,
        ""events"": [
          {
            ""frame"": 6,
            ""endFrame"": 20,
            ""kind"": ""HitTraceWindow"",
            ""combatActionId"": 0,
            ""traceProfileId"": 0,
            ""stableEventId"": 114,
            ""hitEvent"": {
              ""targetTypes"": [
                ""HostileCharacter""
              ],
              ""environmentHitPolicy"": ""None"",
              ""stableEventId"": 114,
              ""hitSameTargetPolicy"": ""OncePerEvent"",
              ""damage"": {
                ""damageTypeId"": 1,
                ""formulaId"": 4,
                ""baseDamage"": 8,
                ""sourceAttributeScales"": [
                  {
                    ""attributeId"": 2,
                    ""scale"": 0.65
                  }
                ],
                ""impactPower"": 20,
                ""tags"": [
                  53,
                  34,
                  36
                ],
                ""analysisCategory"": 29,
                ""damageTypeLabel"": ""slash"",
                ""formulaLabel"": ""ct.soft_defense_v1"",
                ""tagLabels"": [
                  ""dash"",
                  ""melee"",
                  ""weapon""
                ],
                ""analysisCategoryLabel"": ""DashStrike""
              },
              ""stableEventLabel"": ""dash_strike.hit_window""
            },
            ""hitVolume"": {
              ""sourceKind"": ""CasterBody"",
              ""bodyOffset"": {
                ""z"": 0.4
              }
            },
            ""combatActionLabel"": """",
            ""traceProfileLabel"": """",
            ""stableEventLabel"": ""dash_strike.hit_window"",
            ""stableEventOrder"": 233
          }
        ],
        ""combatActionLabel"": ""dash_strike""
      },
      ""gameplayTrack"": null,
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 34,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": """",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.katana_atk1"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.08,
            ""stableEventId"": 117,
            ""stableEventLabel"": ""dash_strike.animation"",
            ""stableEventOrder"": 230
          }
        ]
      },
      ""presentationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 29,
            ""kind"": ""SpawnVisualCue"",
            ""resourceKey"": ""vfx.magic.sword_wave.charge:GameObject@mxframework.samples"",
            ""vfxAttachment"": {
              ""mode"": ""FollowSubject"",
              ""socketId"": """",
              ""offsetSpace"": ""Local"",
              ""transform"": {
                ""positionX"": 0,
                ""positionY"": 0,
                ""positionZ"": 0,
                ""rotationEulerX"": 0,
                ""rotationEulerY"": 0,
                ""rotationEulerZ"": 0,
                ""scaleX"": 1,
                ""scaleY"": 1,
                ""scaleZ"": 1
              }
            },
            ""vfxPlaybackMode"": ""PlayToCompletion"",
            ""vfxPlaybackRate"": 1,
            ""vfxScaleMultiplier"": 1,
            ""vfxStartOffsetFrames"": 0,
            ""vfxEndOffsetFrames"": 0,
            ""vfxNaturalEndPolicy"": ""Complete"",
            ""vfxParameter0"": 0,
            ""vfxParameter1"": 0,
            ""vfxParameter2"": 0,
            ""vfxParameter3"": 0,
            ""stableEventId"": 118,
            ""stableEventLabel"": ""character_test.dash_strike.charge_vfx"",
            ""vfxDefinitionId"": 12039,
            ""numericSocketId"": 0,
            ""stableEventOrder"": 59
          }
        ]
      },
      ""debugTrack"": null
    },
    {
      ""actionId"": 326,
      ""displayName"": ""Hook Chain"",
      ""category"": ""Skill"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""combat"",
        ""magic"",
        ""control"",
        ""hook_chain"",
        ""projectile"",
        ""pull""
      ],
      ""priority"": 30,
      ""durationFrames"": 90,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Startup"",
          ""startFrame"": 0,
          ""endFrame"": 11
        },
        {
          ""kind"": ""Active"",
          ""startFrame"": 12,
          ""endFrame"": 17
        },
        {
          ""kind"": ""Recovery"",
          ""startFrame"": 18,
          ""endFrame"": 89
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 17,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 240,
            ""stableEventLabel"": ""hook_chain.control_lock"",
            ""stableEventOrder"": 279
          },
          {
            ""frame"": 18,
            ""endFrame"": 89,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 241,
            ""stableEventLabel"": ""hook_chain.control_release"",
            ""stableEventOrder"": 280
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": {
        ""events"": [
          {
            ""frame"": 12,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": 58,
            ""abilityStableId"": 0,
            ""stableEventId"": 59,
            ""requestLabel"": ""character_test.hook_chain.projectile"",
            ""abilityStableLabel"": """",
            ""stableEventLabel"": ""character_test.hook_chain.projectile.impact"",
            ""stableEventOrder"": 99
          }
        ]
      },
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 34,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": """",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.katana_atk1"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.08,
            ""stableEventId"": 242,
            ""stableEventLabel"": ""hook_chain.animation"",
            ""stableEventOrder"": 278
          }
        ]
      },
      ""presentationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 29,
            ""kind"": ""SpawnVisualCue"",
            ""resourceKey"": ""vfx.magic.stun_bolt.charge:GameObject@mxframework.samples"",
            ""vfxAttachment"": {
              ""mode"": ""FollowSubject"",
              ""socketId"": """",
              ""offsetSpace"": ""Local"",
              ""transform"": {
                ""positionX"": 0,
                ""positionY"": 0,
                ""positionZ"": 0,
                ""rotationEulerX"": 0,
                ""rotationEulerY"": 0,
                ""rotationEulerZ"": 0,
                ""scaleX"": 1,
                ""scaleY"": 1,
                ""scaleZ"": 1
              }
            },
            ""vfxPlaybackMode"": ""PlayToCompletion"",
            ""vfxPlaybackRate"": 1,
            ""vfxScaleMultiplier"": 1,
            ""vfxStartOffsetFrames"": 0,
            ""vfxEndOffsetFrames"": 0,
            ""vfxNaturalEndPolicy"": ""Complete"",
            ""vfxParameter0"": 0,
            ""vfxParameter1"": 0,
            ""vfxParameter2"": 0,
            ""vfxParameter3"": 0,
            ""stableEventId"": 119,
            ""stableEventLabel"": ""character_test.hook_chain.charge_vfx"",
            ""vfxDefinitionId"": 12034,
            ""numericSocketId"": 0,
            ""stableEventOrder"": 97
          }
        ]
      },
      ""debugTrack"": null
    },
    {
      ""actionId"": 328,
      ""displayName"": ""Arc Mine"",
      ""category"": ""Skill"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""combat"",
        ""magic"",
        ""trap"",
        ""deployable"",
        ""arc_mine"",
        ""area""
      ],
      ""priority"": 30,
      ""durationFrames"": 90,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Startup"",
          ""startFrame"": 0,
          ""endFrame"": 17
        },
        {
          ""kind"": ""Recovery"",
          ""startFrame"": 18,
          ""endFrame"": 89
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 17,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 121,
            ""stableEventLabel"": ""arc_mine.control_lock"",
            ""stableEventOrder"": 12
          },
          {
            ""frame"": 18,
            ""endFrame"": 89,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 122,
            ""stableEventLabel"": ""arc_mine.control_release"",
            ""stableEventOrder"": 13
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": {
        ""events"": [
          {
            ""frame"": 12,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": 60,
            ""abilityStableId"": 910025,
            ""stableEventId"": 60,
            ""requestLabel"": ""character_test.arc_mine.deploy"",
            ""abilityStableLabel"": ""ability.character_test.arc_mine"",
            ""stableEventLabel"": ""character_test.arc_mine.deploy"",
            ""stableEventOrder"": 29
          }
        ]
      },
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 34,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": """",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.katana_atk1"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.08,
            ""stableEventId"": 120,
            ""stableEventLabel"": ""arc_mine.animation"",
            ""stableEventOrder"": 11
          }
        ]
      },
      ""presentationTrack"": null,
      ""debugTrack"": null
    },
    {
      ""actionId"": 329,
      ""displayName"": ""Rune Turret"",
      ""category"": ""Skill"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""combat"",
        ""magic"",
        ""turret"",
        ""deployable"",
        ""rune_turret"",
        ""projectile""
      ],
      ""priority"": 30,
      ""durationFrames"": 90,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Startup"",
          ""startFrame"": 0,
          ""endFrame"": 17
        },
        {
          ""kind"": ""Recovery"",
          ""startFrame"": 18,
          ""endFrame"": 89
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 17,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 128,
            ""stableEventLabel"": ""rune_turret.control_lock"",
            ""stableEventOrder"": 340
          },
          {
            ""frame"": 18,
            ""endFrame"": 89,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 129,
            ""stableEventLabel"": ""rune_turret.control_release"",
            ""stableEventOrder"": 341
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": {
        ""events"": [
          {
            ""frame"": 12,
            ""kind"": ""SendGameplayRequest"",
            ""requestId"": 64,
            ""abilityStableId"": 910026,
            ""stableEventId"": 64,
            ""requestLabel"": ""character_test.rune_turret.deploy"",
            ""abilityStableLabel"": ""ability.character_test.rune_turret"",
            ""stableEventLabel"": ""character_test.rune_turret.deploy"",
            ""stableEventOrder"": 123
          }
        ]
      },
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 34,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": """",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.katana_atk1"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.08,
            ""stableEventId"": 127,
            ""stableEventLabel"": ""rune_turret.animation"",
            ""stableEventOrder"": 339
          }
        ]
      },
      ""presentationTrack"": null,
      ""debugTrack"": null
    },
    {
      ""actionId"": 330,
      ""displayName"": ""Katana Cohesion Break React"",
      ""category"": ""Reaction"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""reaction"",
        ""cohesion_break"",
        ""katana"",
        ""hit_stun""
      ],
      ""priority"": 100,
      ""durationFrames"": 18,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Active"",
          ""startFrame"": 0,
          ""endFrame"": 17
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 16,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 243,
            ""stableEventLabel"": ""katana_cohesion_break_react.control_lock"",
            ""stableEventOrder"": 282
          },
          {
            ""frame"": 17,
            ""endFrame"": 17,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 244,
            ""stableEventLabel"": ""katana_cohesion_break_react.control_release"",
            ""stableEventOrder"": 283
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": null,
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 17,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": ""anim.reaction.cohesion_break"",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.damage_stun01_start"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.04,
            ""stableEventId"": 245,
            ""stableEventLabel"": ""katana_cohesion_break_react.animation"",
            ""stableEventOrder"": 281
          }
        ]
      },
      ""presentationTrack"": null,
      ""debugTrack"": null
    },
    {
      ""actionId"": 331,
      ""displayName"": ""Katana Hit High React"",
      ""category"": ""Reaction"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""reaction"",
        ""hit"",
        ""height_high"",
        ""katana"",
        ""hit_stun""
      ],
      ""priority"": 90,
      ""durationFrames"": 12,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Active"",
          ""startFrame"": 0,
          ""endFrame"": 11
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 10,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 246,
            ""stableEventLabel"": ""katana_hit_high_react.control_lock"",
            ""stableEventOrder"": 288
          },
          {
            ""frame"": 11,
            ""endFrame"": 11,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 247,
            ""stableEventLabel"": ""katana_hit_high_react.control_release"",
            ""stableEventOrder"": 289
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": null,
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 11,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": ""anim.reaction.hit_high"",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.damage_high_f"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.04,
            ""stableEventId"": 248,
            ""stableEventLabel"": ""katana_hit_high_react.animation"",
            ""stableEventOrder"": 287
          }
        ]
      },
      ""presentationTrack"": null,
      ""debugTrack"": null
    },
    {
      ""actionId"": 332,
      ""displayName"": ""Katana Hit Mid React"",
      ""category"": ""Reaction"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""reaction"",
        ""hit"",
        ""height_mid"",
        ""katana"",
        ""hit_stun""
      ],
      ""priority"": 89,
      ""durationFrames"": 12,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Active"",
          ""startFrame"": 0,
          ""endFrame"": 11
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 10,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 249,
            ""stableEventLabel"": ""katana_hit_mid_react.control_lock"",
            ""stableEventOrder"": 300
          },
          {
            ""frame"": 11,
            ""endFrame"": 11,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 250,
            ""stableEventLabel"": ""katana_hit_mid_react.control_release"",
            ""stableEventOrder"": 301
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": null,
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 11,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": ""anim.reaction.hit_mid"",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.damage_mid_f"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.04,
            ""stableEventId"": 251,
            ""stableEventLabel"": ""katana_hit_mid_react.animation"",
            ""stableEventOrder"": 299
          }
        ]
      },
      ""presentationTrack"": null,
      ""debugTrack"": null
    },
    {
      ""actionId"": 333,
      ""displayName"": ""Katana Hit Low React"",
      ""category"": ""Reaction"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""reaction"",
        ""hit"",
        ""height_low"",
        ""katana"",
        ""hit_stun""
      ],
      ""priority"": 88,
      ""durationFrames"": 12,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Active"",
          ""startFrame"": 0,
          ""endFrame"": 11
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 10,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 252,
            ""stableEventLabel"": ""katana_hit_low_react.control_lock"",
            ""stableEventOrder"": 294
          },
          {
            ""frame"": 11,
            ""endFrame"": 11,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 253,
            ""stableEventLabel"": ""katana_hit_low_react.control_release"",
            ""stableEventOrder"": 295
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": null,
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 11,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": ""anim.reaction.hit_low"",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.damage_low_l1"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.04,
            ""stableEventId"": 254,
            ""stableEventLabel"": ""katana_hit_low_react.animation"",
            ""stableEventOrder"": 293
          }
        ]
      },
      ""presentationTrack"": null,
      ""debugTrack"": null
    },
    {
      ""actionId"": 334,
      ""displayName"": ""Katana Hit High Strong React"",
      ""category"": ""Reaction"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""reaction"",
        ""hit"",
        ""height_high"",
        ""strong"",
        ""katana"",
        ""hit_stun""
      ],
      ""priority"": 110,
      ""durationFrames"": 18,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Active"",
          ""startFrame"": 0,
          ""endFrame"": 17
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 16,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 255,
            ""stableEventLabel"": ""katana_hit_high_strong_react.control_lock"",
            ""stableEventOrder"": 291
          },
          {
            ""frame"": 17,
            ""endFrame"": 17,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 256,
            ""stableEventLabel"": ""katana_hit_high_strong_react.control_release"",
            ""stableEventOrder"": 292
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": null,
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 17,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": ""anim.reaction.hit_high_strong"",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.damage_strong_l_head"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.04,
            ""stableEventId"": 257,
            ""stableEventLabel"": ""katana_hit_high_strong_react.animation"",
            ""stableEventOrder"": 290
          }
        ]
      },
      ""presentationTrack"": null,
      ""debugTrack"": null
    },
    {
      ""actionId"": 335,
      ""displayName"": ""Katana Hit Mid Strong React"",
      ""category"": ""Reaction"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""reaction"",
        ""hit"",
        ""height_mid"",
        ""strong"",
        ""katana"",
        ""hit_stun""
      ],
      ""priority"": 109,
      ""durationFrames"": 18,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Active"",
          ""startFrame"": 0,
          ""endFrame"": 17
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 16,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 258,
            ""stableEventLabel"": ""katana_hit_mid_strong_react.control_lock"",
            ""stableEventOrder"": 303
          },
          {
            ""frame"": 17,
            ""endFrame"": 17,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 259,
            ""stableEventLabel"": ""katana_hit_mid_strong_react.control_release"",
            ""stableEventOrder"": 304
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": null,
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 17,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": ""anim.reaction.hit_mid_strong"",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.damage_strong_l_chest"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.04,
            ""stableEventId"": 260,
            ""stableEventLabel"": ""katana_hit_mid_strong_react.animation"",
            ""stableEventOrder"": 302
          }
        ]
      },
      ""presentationTrack"": null,
      ""debugTrack"": null
    },
    {
      ""actionId"": 336,
      ""displayName"": ""Katana Hit Low Strong React"",
      ""category"": ""Reaction"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""reaction"",
        ""hit"",
        ""height_low"",
        ""strong"",
        ""katana"",
        ""hit_stun""
      ],
      ""priority"": 108,
      ""durationFrames"": 18,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Active"",
          ""startFrame"": 0,
          ""endFrame"": 17
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 16,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 261,
            ""stableEventLabel"": ""katana_hit_low_strong_react.control_lock"",
            ""stableEventOrder"": 297
          },
          {
            ""frame"": 17,
            ""endFrame"": 17,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 262,
            ""stableEventLabel"": ""katana_hit_low_strong_react.control_release"",
            ""stableEventOrder"": 298
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": null,
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 17,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": ""anim.reaction.hit_low_strong"",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.damage_strong_l_foot"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.04,
            ""stableEventId"": 263,
            ""stableEventLabel"": ""katana_hit_low_strong_react.animation"",
            ""stableEventOrder"": 296
          }
        ]
      },
      ""presentationTrack"": null,
      ""debugTrack"": null
    },
    {
      ""actionId"": 361,
      ""displayName"": ""Katana Death React"",
      ""category"": ""Reaction"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""reaction"",
        ""death"",
        ""katana"",
        ""control_locked""
      ],
      ""priority"": 200,
      ""durationFrames"": 60,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Active"",
          ""startFrame"": 0,
          ""endFrame"": 59
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 59,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 264,
            ""stableEventLabel"": ""katana_death_react.control_lock"",
            ""stableEventOrder"": 286
          },
          {
            ""frame"": 24,
            ""endFrame"": 59,
            ""kind"": ""SetCollisionProfile"",
            ""movementMode"": ""Airborne"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""collisionProfileId"": 5,
            ""collisionRestorePolicy"": ""None"",
            ""stableEventId"": 265,
            ""collisionProfileLabel"": ""corpse"",
            ""stableEventLabel"": ""katana_death_react.collision_corpse"",
            ""stableEventOrder"": 285
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": null,
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 59,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": ""anim.reaction.death"",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.damage_ko_mid_2"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.04,
            ""stableEventId"": 266,
            ""stableEventLabel"": ""katana_death_react.animation"",
            ""stableEventOrder"": 284
          }
        ]
      },
      ""presentationTrack"": null,
      ""debugTrack"": null
    },
    {
      ""actionId"": 362,
      ""displayName"": ""Katana Revive React"",
      ""category"": ""Reaction"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""reaction"",
        ""revive"",
        ""katana"",
        ""lifecycle""
      ],
      ""priority"": 150,
      ""durationFrames"": 30,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Active"",
          ""startFrame"": 0,
          ""endFrame"": 29
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 28,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 267,
            ""stableEventLabel"": ""katana_revive_react.control_lock"",
            ""stableEventOrder"": 309
          },
          {
            ""frame"": 0,
            ""endFrame"": 28,
            ""kind"": ""SetCollisionProfile"",
            ""movementMode"": ""Airborne"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""collisionProfileId"": 6,
            ""stableEventId"": 268,
            ""collisionProfileLabel"": ""revive_low"",
            ""stableEventLabel"": ""katana_revive_react.collision_revive_low"",
            ""stableEventOrder"": 308
          },
          {
            ""frame"": 29,
            ""endFrame"": 29,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 269,
            ""stableEventLabel"": ""katana_revive_react.control_release"",
            ""stableEventOrder"": 310
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": null,
      ""animationTrack"": null,
      ""presentationTrack"": null,
      ""debugTrack"": null
    },
    {
      ""actionId"": 363,
      ""displayName"": ""Katana Launch React"",
      ""category"": ""Reaction"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""reaction"",
        ""hit"",
        ""launch"",
        ""katana"",
        ""special_hit""
      ],
      ""priority"": 115,
      ""durationFrames"": 24,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Active"",
          ""startFrame"": 0,
          ""endFrame"": 23
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 22,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 270,
            ""stableEventLabel"": ""katana_launch_react.control_lock"",
            ""stableEventOrder"": 306
          },
          {
            ""frame"": 23,
            ""endFrame"": 23,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 271,
            ""stableEventLabel"": ""katana_launch_react.control_release"",
            ""stableEventOrder"": 307
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": null,
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 17,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": ""anim.reaction.launch"",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.damage_strong_l_chest"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.04,
            ""stableEventId"": 272,
            ""stableEventLabel"": ""katana_launch_react.animation"",
            ""stableEventOrder"": 305
          }
        ]
      },
      ""presentationTrack"": null,
      ""debugTrack"": null
    },
    {
      ""actionId"": 337,
      ""displayName"": ""Cohesion Break React"",
      ""category"": ""Reaction"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""reaction"",
        ""cohesion_break"",
        ""codex_flow_test"",
        ""hit_stun"",
        ""cohesion_break""
      ],
      ""priority"": 120,
      ""durationFrames"": 18,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Active"",
          ""startFrame"": 0,
          ""endFrame"": 17
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 16,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 273,
            ""stableEventLabel"": ""codex_flow_test_cohesion_break_react.control_lock"",
            ""stableEventOrder"": 148
          },
          {
            ""frame"": 17,
            ""endFrame"": 17,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 274,
            ""stableEventLabel"": ""codex_flow_test_cohesion_break_react.control_release"",
            ""stableEventOrder"": 149
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": null,
      ""animationTrack"": {
        ""events"": []
      },
      ""presentationTrack"": null,
      ""debugTrack"": null
    },
    {
      ""actionId"": 338,
      ""displayName"": ""High Hit React"",
      ""category"": ""Reaction"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""reaction"",
        ""hit_high"",
        ""codex_flow_test"",
        ""hit_stun""
      ],
      ""priority"": 90,
      ""durationFrames"": 12,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Active"",
          ""startFrame"": 0,
          ""endFrame"": 11
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 10,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 275,
            ""stableEventLabel"": ""codex_flow_test_hit_high_react.control_lock"",
            ""stableEventOrder"": 153
          },
          {
            ""frame"": 11,
            ""endFrame"": 11,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 276,
            ""stableEventLabel"": ""codex_flow_test_hit_high_react.control_release"",
            ""stableEventOrder"": 154
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": null,
      ""animationTrack"": {
        ""events"": []
      },
      ""presentationTrack"": null,
      ""debugTrack"": null
    },
    {
      ""actionId"": 339,
      ""displayName"": ""Mid Hit React"",
      ""category"": ""Reaction"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""reaction"",
        ""hit_mid"",
        ""codex_flow_test"",
        ""hit_stun""
      ],
      ""priority"": 89,
      ""durationFrames"": 12,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Active"",
          ""startFrame"": 0,
          ""endFrame"": 11
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 10,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 277,
            ""stableEventLabel"": ""codex_flow_test_hit_mid_react.control_lock"",
            ""stableEventOrder"": 161
          },
          {
            ""frame"": 11,
            ""endFrame"": 11,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 278,
            ""stableEventLabel"": ""codex_flow_test_hit_mid_react.control_release"",
            ""stableEventOrder"": 162
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": null,
      ""animationTrack"": {
        ""events"": []
      },
      ""presentationTrack"": null,
      ""debugTrack"": null
    },
    {
      ""actionId"": 340,
      ""displayName"": ""Low Hit React"",
      ""category"": ""Reaction"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""reaction"",
        ""hit_low"",
        ""codex_flow_test"",
        ""hit_stun""
      ],
      ""priority"": 88,
      ""durationFrames"": 12,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Active"",
          ""startFrame"": 0,
          ""endFrame"": 11
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 10,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 279,
            ""stableEventLabel"": ""codex_flow_test_hit_low_react.control_lock"",
            ""stableEventOrder"": 157
          },
          {
            ""frame"": 11,
            ""endFrame"": 11,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 280,
            ""stableEventLabel"": ""codex_flow_test_hit_low_react.control_release"",
            ""stableEventOrder"": 158
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": null,
      ""animationTrack"": {
        ""events"": []
      },
      ""presentationTrack"": null,
      ""debugTrack"": null
    },
    {
      ""actionId"": 341,
      ""displayName"": ""High Strong Hit React"",
      ""category"": ""Reaction"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""reaction"",
        ""hit_high_strong"",
        ""codex_flow_test"",
        ""hit_stun""
      ],
      ""priority"": 110,
      ""durationFrames"": 18,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Active"",
          ""startFrame"": 0,
          ""endFrame"": 17
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 16,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 281,
            ""stableEventLabel"": ""codex_flow_test_hit_high_strong_react.control_lock"",
            ""stableEventOrder"": 155
          },
          {
            ""frame"": 17,
            ""endFrame"": 17,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 282,
            ""stableEventLabel"": ""codex_flow_test_hit_high_strong_react.control_release"",
            ""stableEventOrder"": 156
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": null,
      ""animationTrack"": {
        ""events"": []
      },
      ""presentationTrack"": null,
      ""debugTrack"": null
    },
    {
      ""actionId"": 342,
      ""displayName"": ""Mid Strong Hit React"",
      ""category"": ""Reaction"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""reaction"",
        ""hit_mid_strong"",
        ""codex_flow_test"",
        ""hit_stun""
      ],
      ""priority"": 109,
      ""durationFrames"": 18,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Active"",
          ""startFrame"": 0,
          ""endFrame"": 17
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 16,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 283,
            ""stableEventLabel"": ""codex_flow_test_hit_mid_strong_react.control_lock"",
            ""stableEventOrder"": 163
          },
          {
            ""frame"": 17,
            ""endFrame"": 17,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 284,
            ""stableEventLabel"": ""codex_flow_test_hit_mid_strong_react.control_release"",
            ""stableEventOrder"": 164
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": null,
      ""animationTrack"": {
        ""events"": []
      },
      ""presentationTrack"": null,
      ""debugTrack"": null
    },
    {
      ""actionId"": 343,
      ""displayName"": ""Low Strong Hit React"",
      ""category"": ""Reaction"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""reaction"",
        ""hit_low_strong"",
        ""codex_flow_test"",
        ""hit_stun""
      ],
      ""priority"": 108,
      ""durationFrames"": 18,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Active"",
          ""startFrame"": 0,
          ""endFrame"": 17
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 16,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 285,
            ""stableEventLabel"": ""codex_flow_test_hit_low_strong_react.control_lock"",
            ""stableEventOrder"": 159
          },
          {
            ""frame"": 17,
            ""endFrame"": 17,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 286,
            ""stableEventLabel"": ""codex_flow_test_hit_low_strong_react.control_release"",
            ""stableEventOrder"": 160
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": null,
      ""animationTrack"": {
        ""events"": []
      },
      ""presentationTrack"": null,
      ""debugTrack"": null
    },
    {
      ""actionId"": 344,
      ""displayName"": ""Death React"",
      ""category"": ""Reaction"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""reaction"",
        ""death"",
        ""codex_flow_test"",
        ""hit_stun""
      ],
      ""priority"": 200,
      ""durationFrames"": 60,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Active"",
          ""startFrame"": 0,
          ""endFrame"": 59
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 58,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 287,
            ""stableEventLabel"": ""codex_flow_test_death_react.control_lock"",
            ""stableEventOrder"": 151
          },
          {
            ""frame"": 59,
            ""endFrame"": 59,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""Run"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 1,
            ""rotationControlScale"": 1,
            ""stableEventId"": 288,
            ""stableEventLabel"": ""codex_flow_test_death_react.control_release"",
            ""stableEventOrder"": 152
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": null,
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 59,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": ""anim.reaction.death"",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.damage_ko_mid_2"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.04,
            ""stableEventId"": 289,
            ""stableEventLabel"": ""codex_flow_test_death_react.animation"",
            ""stableEventOrder"": 150
          }
        ]
      },
      ""presentationTrack"": null,
      ""debugTrack"": null
    },
    {
      ""actionId"": 400,
      ""displayName"": ""破防反应"",
      ""category"": ""Reaction"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""reaction"",
        ""cohesionbreak"",
        ""hit_stun""
      ],
      ""priority"": 120,
      ""durationFrames"": 18,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Active"",
          ""startFrame"": 0,
          ""endFrame"": 17
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 17,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 290,
            ""stableEventLabel"": ""combat_girl_sword_cohesion_break_react.control_lock"",
            ""stableEventOrder"": 220
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": null,
      ""animationTrack"": {
        ""events"": []
      },
      ""presentationTrack"": null,
      ""debugTrack"": null
    },
    {
      ""actionId"": 401,
      ""displayName"": ""普通 · 上段"",
      ""category"": ""Reaction"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""reaction"",
        ""hit_normal_high"",
        ""hit_stun""
      ],
      ""priority"": 90,
      ""durationFrames"": 12,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Active"",
          ""startFrame"": 0,
          ""endFrame"": 11
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 11,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 291,
            ""stableEventLabel"": ""combat_girl_sword_hit_normal_high_react.control_lock"",
            ""stableEventOrder"": 224
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": null,
      ""animationTrack"": {
        ""events"": []
      },
      ""presentationTrack"": null,
      ""debugTrack"": null
    },
    {
      ""actionId"": 402,
      ""displayName"": ""普通 · 中段"",
      ""category"": ""Reaction"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""reaction"",
        ""hit_normal_mid"",
        ""hit_stun""
      ],
      ""priority"": 89,
      ""durationFrames"": 12,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Active"",
          ""startFrame"": 0,
          ""endFrame"": 11
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 11,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 292,
            ""stableEventLabel"": ""combat_girl_sword_hit_normal_mid_react.control_lock"",
            ""stableEventOrder"": 226
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": null,
      ""animationTrack"": {
        ""events"": []
      },
      ""presentationTrack"": null,
      ""debugTrack"": null
    },
    {
      ""actionId"": 403,
      ""displayName"": ""普通 · 下段"",
      ""category"": ""Reaction"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""reaction"",
        ""hit_normal_low"",
        ""hit_stun""
      ],
      ""priority"": 88,
      ""durationFrames"": 12,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Active"",
          ""startFrame"": 0,
          ""endFrame"": 11
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 11,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 293,
            ""stableEventLabel"": ""combat_girl_sword_hit_normal_low_react.control_lock"",
            ""stableEventOrder"": 225
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": null,
      ""animationTrack"": {
        ""events"": []
      },
      ""presentationTrack"": null,
      ""debugTrack"": null
    },
    {
      ""actionId"": 404,
      ""displayName"": ""强冲击 · 上段"",
      ""category"": ""Reaction"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""reaction"",
        ""hit_strong_high"",
        ""hit_stun""
      ],
      ""priority"": 110,
      ""durationFrames"": 18,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Active"",
          ""startFrame"": 0,
          ""endFrame"": 17
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 17,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 294,
            ""stableEventLabel"": ""combat_girl_sword_hit_strong_high_react.control_lock"",
            ""stableEventOrder"": 227
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": null,
      ""animationTrack"": {
        ""events"": []
      },
      ""presentationTrack"": null,
      ""debugTrack"": null
    },
    {
      ""actionId"": 405,
      ""displayName"": ""强冲击 · 中段"",
      ""category"": ""Reaction"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""reaction"",
        ""hit_strong_mid"",
        ""hit_stun""
      ],
      ""priority"": 109,
      ""durationFrames"": 18,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Active"",
          ""startFrame"": 0,
          ""endFrame"": 17
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 17,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 295,
            ""stableEventLabel"": ""combat_girl_sword_hit_strong_mid_react.control_lock"",
            ""stableEventOrder"": 229
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": null,
      ""animationTrack"": {
        ""events"": []
      },
      ""presentationTrack"": null,
      ""debugTrack"": null
    },
    {
      ""actionId"": 406,
      ""displayName"": ""强冲击 · 下段"",
      ""category"": ""Reaction"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""reaction"",
        ""hit_strong_low"",
        ""hit_stun""
      ],
      ""priority"": 108,
      ""durationFrames"": 18,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Active"",
          ""startFrame"": 0,
          ""endFrame"": 17
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 17,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 296,
            ""stableEventLabel"": ""combat_girl_sword_hit_strong_low_react.control_lock"",
            ""stableEventOrder"": 228
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": null,
      ""animationTrack"": {
        ""events"": []
      },
      ""presentationTrack"": null,
      ""debugTrack"": null
    },
    {
      ""actionId"": 407,
      ""displayName"": ""死亡"",
      ""category"": ""Reaction"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""reaction"",
        ""death"",
        ""hit_stun""
      ],
      ""priority"": 200,
      ""durationFrames"": 60,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Active"",
          ""startFrame"": 0,
          ""endFrame"": 59
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": {
        ""usesRootMotion"": false,
        ""rootMotion"": null,
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 59,
            ""kind"": ""SetControlScale"",
            ""movementMode"": ""ControlLocked"",
            ""x"": 0,
            ""y"": 0,
            ""z"": 0,
            ""movementControlScale"": 0,
            ""rotationControlScale"": 0,
            ""stableEventId"": 297,
            ""stableEventLabel"": ""combat_girl_sword_death_react.control_lock"",
            ""stableEventOrder"": 223
          },
          {
            ""frame"": 24,
            ""endFrame"": 59,
            ""kind"": ""SetCollisionProfile"",
            ""collisionProfileId"": 5,
            ""collisionRestorePolicy"": ""None"",
            ""collisionTransitionFrames"": 0,
            ""stableEventId"": 298,
            ""collisionProfileLabel"": ""corpse"",
            ""stableEventLabel"": ""combat_girl_sword_death_react.collision_corpse"",
            ""stableEventOrder"": 222
          }
        ]
      },
      ""combatTrack"": null,
      ""gameplayTrack"": null,
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 59,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": ""anim.reaction.death"",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.damage_ko_mid_2"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.04,
            ""stableEventId"": 299,
            ""stableEventLabel"": ""combat_girl_sword_death_react.animation"",
            ""stableEventOrder"": 221
          }
        ]
      },
      ""presentationTrack"": null,
      ""debugTrack"": null
    },
    {
      ""actionId"": 930000,
      ""displayName"": ""ability.evade"",
      ""category"": ""Dodge"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""ability.evade""
      ],
      ""priority"": 0,
      ""durationFrames"": 1,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Active"",
          ""startFrame"": 0,
          ""endFrame"": 0
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": null,
      ""combatTrack"": null,
      ""gameplayTrack"": null,
      ""animationTrack"": null,
      ""presentationTrack"": null,
      ""debugTrack"": null,
      ""authoringTimeline"": {
        ""timelineId"": ""codex_flow_test.empty_empty.evade.timeline"",
        ""durationFrames"": 1,
        ""animationClipResourceKey"": """"
      }
    },
    {
      ""actionId"": 930001,
      ""displayName"": ""reaction.death"",
      ""category"": ""Reaction"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""reaction.death""
      ],
      ""priority"": 0,
      ""durationFrames"": 60,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Active"",
          ""startFrame"": 0,
          ""endFrame"": 59
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": null,
      ""combatTrack"": null,
      ""gameplayTrack"": null,
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 59,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": ""reaction.death"",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.damage_ko_mid_2"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.08,
            ""stableEventId"": 300,
            ""stableEventLabel"": ""codex_flow_test.empty_empty.reaction.death.timeline"",
            ""stableEventOrder"": 146
          }
        ]
      },
      ""presentationTrack"": null,
      ""debugTrack"": null,
      ""authoringTimeline"": {
        ""timelineId"": ""codex_flow_test.empty_empty.reaction.death.timeline"",
        ""durationFrames"": 60,
        ""animationClipResourceKey"": ""art.character.skeleton.animation.damage_ko_mid_2""
      }
    },
    {
      ""actionId"": 930100,
      ""displayName"": ""ability.evade"",
      ""category"": ""Dodge"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""ability.evade""
      ],
      ""priority"": 0,
      ""durationFrames"": 1,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Active"",
          ""startFrame"": 0,
          ""endFrame"": 0
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": null,
      ""combatTrack"": null,
      ""gameplayTrack"": null,
      ""animationTrack"": null,
      ""presentationTrack"": null,
      ""debugTrack"": null,
      ""authoringTimeline"": {
        ""timelineId"": ""codex_flow_test.sword_empty.evade.timeline"",
        ""durationFrames"": 1,
        ""animationClipResourceKey"": """"
      }
    },
    {
      ""actionId"": 930101,
      ""displayName"": ""ability.secondary"",
      ""category"": ""Skill"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""ability.secondary""
      ],
      ""priority"": 0,
      ""durationFrames"": 1,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Active"",
          ""startFrame"": 0,
          ""endFrame"": 0
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": null,
      ""combatTrack"": null,
      ""gameplayTrack"": null,
      ""animationTrack"": null,
      ""presentationTrack"": null,
      ""debugTrack"": null,
      ""authoringTimeline"": {
        ""timelineId"": ""codex_flow_test.sword_empty.secondary.timeline"",
        ""durationFrames"": 1,
        ""animationClipResourceKey"": """"
      }
    },
    {
      ""actionId"": 930102,
      ""displayName"": ""reaction.death"",
      ""category"": ""Reaction"",
      ""timelineAuthority"": ""CharacterAuthored"",
      ""tags"": [
        ""reaction.death""
      ],
      ""priority"": 0,
      ""durationFrames"": 60,
      ""requirements"": [],
      ""phases"": [
        {
          ""kind"": ""Active"",
          ""startFrame"": 0,
          ""endFrame"": 59
        }
      ],
      ""cancelRules"": [],
      ""interruptRules"": [],
      ""cohesionSupport"": null,
      ""motionTrack"": null,
      ""combatTrack"": null,
      ""gameplayTrack"": null,
      ""animationTrack"": {
        ""events"": [
          {
            ""frame"": 0,
            ""endFrame"": 59,
            ""playbackSpeed"": 1,
            ""kind"": ""CrossFadeAnimation"",
            ""animationActionKey"": ""reaction.death"",
            ""animationClipResourceKey"": ""art.character.skeleton.animation.damage_ko_mid_2"",
            ""animationStartOffsetFrames"": 0,
            ""animationEndOffsetFrames"": 0,
            ""transitionSeconds"": 0.08,
            ""stableEventId"": 301,
            ""stableEventLabel"": ""codex_flow_test.sword_empty.reaction.death.timeline"",
            ""stableEventOrder"": 147
          }
        ]
      },
      ""presentationTrack"": null,
      ""debugTrack"": null,
      ""authoringTimeline"": {
        ""timelineId"": ""codex_flow_test.sword_empty.reaction.death.timeline"",
        ""durationFrames"": 60,
        ""animationClipResourceKey"": ""art.character.skeleton.animation.damage_ko_mid_2""
      }
    }
  ]
}";

    }
}
