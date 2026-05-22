using System.Linq;
using MxFramework.Animation;
using MxFramework.Resources;
using NUnit.Framework;

namespace MxFramework.Tests.Animation
{
    public sealed class MxAnimationCompiledArtifactJsonTests
    {
        [Test]
        public void LoadSetDefinitions_MapsCompiledArtifactToRuntimeContracts()
        {
            MxAnimationSetDefinition definition = MxAnimationCompiledArtifactJson
                .LoadSetDefinitions(AnimationSetDefinitionJson)
                .Single();

            Assert.AreEqual("set.base", definition.SetId);
            Assert.AreEqual(1, definition.Version);
            Assert.AreEqual(new ResourceKey("char.test.anim.idle", ResourceTypeIds.AnimationClip, string.Empty, "test_package"), definition.DefaultClip);
            Assert.AreEqual(new ResourceKey("char.test.anim.idle", ResourceTypeIds.AnimationClip, string.Empty, "test_package"), definition.FallbackClip);
            Assert.AreEqual(1, definition.Layers.Count);
            Assert.AreEqual(MxAnimationLayerBlendMode.Additive, definition.Layers[0].BlendMode);
            Assert.AreEqual(new ResourceKey("char.test.mask.upper", ResourceTypeIds.AvatarMask, string.Empty, "test_package"), definition.Layers[0].AvatarMaskKey);

            Assert.AreEqual(1, definition.Blend1DDefinitions.Count);
            Assert.AreEqual("locomotion.speed", definition.Blend1DDefinitions[0].ParameterId);
            Assert.AreEqual(2, definition.Blend1DDefinitions[0].Points.Count);
            Assert.AreEqual(1000, definition.Blend1DDefinitions[0].Points[1].Threshold);

            Assert.AreEqual(1, definition.Blend2DDefinitions.Count);
            Assert.AreEqual("locomotion.x", definition.Blend2DDefinitions[0].ParameterXId);
            Assert.AreEqual(-1000, definition.Blend2DDefinitions[0].Points[0].X);

            Assert.AreEqual(1, definition.Actions.Count);
            Assert.AreEqual("attack.light", definition.Actions[0].ActionKey);
            Assert.AreEqual(new ResourceKey("char.test.anim.attack", ResourceTypeIds.AnimationClip, string.Empty, "test_package"), definition.Actions[0].Clip);
            Assert.AreEqual(1, definition.Actions[0].PresentationEvents.Count);
            Assert.AreEqual("trace.begin", definition.Actions[0].PresentationEvents[0].EventKind);
            Assert.AreEqual(MxAnimationEventTimeDomain.CombatFrame, definition.Actions[0].PresentationEvents[0].TimeDomain);
            Assert.AreEqual(new ResourceKey("char.test.vfx.slash", ResourceTypeIds.Object, string.Empty, "test_package"), definition.Actions[0].PresentationEvents[0].PayloadKey);
            Assert.AreEqual(1, definition.LocomotionClipCalibrations.Count);
            Assert.AreEqual(new ResourceKey("char.test.anim.idle", ResourceTypeIds.AnimationClip, string.Empty, "test_package"), definition.LocomotionClipCalibrations[0].ClipKey);
            Assert.AreEqual(1.2f, definition.LocomotionClipCalibrations[0].CycleDurationSeconds, 0.0001f);
            Assert.AreEqual(1f, definition.LocomotionClipCalibrations[0].GetContactConfidence(MxAnimationLocomotionFoot.Left, 0.5f), 0.0001f);
            Assert.IsTrue(definition.TryFindLocomotionClipCalibration(
                new ResourceKey("char.test.anim.idle", ResourceTypeIds.AnimationClip, string.Empty, "test_package"),
                out MxAnimationLocomotionClipCalibration idleCalibration));
            Assert.AreEqual(0f, idleCalibration.NativeVelocityY, 0.0001f);

            CollectionAssert.Contains(definition.Warmup.RequiredKeys, definition.DefaultClip);
            CollectionAssert.Contains(definition.Warmup.RequiredKeys, definition.Actions[0].Clip);
            CollectionAssert.Contains(definition.Warmup.RequiredKeys, definition.Layers[0].AvatarMaskKey);
        }

        [Test]
        public void LoadClipRegistry_UsesCatalogHashes()
        {
            var catalog = new ResourceCatalog(
                "runtime.test",
                "test_package",
                new[]
                {
                    new ResourceCatalogEntry(
                        "char.test.anim.idle",
                        ResourceTypeIds.AnimationClip,
                        "memory",
                        "idle",
                        "default",
                        "test_package",
                        hash: "hash-idle")
                });

            MxAnimationClipRegistry registry = MxAnimationCompiledArtifactJson.LoadClipRegistry(ClipRegistryJson, catalog);

            Assert.AreEqual("runtime.test", registry.CatalogId);
            Assert.AreEqual(1, registry.Entries.Count);
            Assert.AreEqual(new ResourceKey("char.test.anim.idle", ResourceTypeIds.AnimationClip, string.Empty, "test_package"), registry.Entries[0].ClipKey);
            Assert.AreEqual("hash-idle", registry.Entries[0].CatalogEntryHash);
        }

        [Test]
        public void LoadSetDefinitions_UsesRuntimePackageOverrideForResourceKeys()
        {
            string json = AnimationSetDefinitionJson.Replace(@"""packageId"": ""test_package""", @"""packageId"": ""animation.test""");

            MxAnimationSetDefinition definition = MxAnimationCompiledArtifactJson
                .LoadSetDefinitions(json, "test_package")
                .Single();

            Assert.AreEqual(new ResourceKey("char.test.anim.idle", ResourceTypeIds.AnimationClip, string.Empty, "test_package"), definition.DefaultClip);
            Assert.AreEqual(new ResourceKey("char.test.mask.upper", ResourceTypeIds.AvatarMask, string.Empty, "test_package"), definition.Layers[0].AvatarMaskKey);
            Assert.AreEqual(new ResourceKey("char.test.vfx.slash", ResourceTypeIds.Object, string.Empty, "test_package"), definition.Actions[0].PresentationEvents[0].PayloadKey);
        }

        [Test]
        public void LoadSetDefinitions_SkipsBlendOnlyActionBindings()
        {
            string json = AnimationSetDefinitionJson.Replace(
                @"""actionBindings"": [
        {
          ""bindingId"": ""lightAttack"",
          ""actionId"": ""attack.light"",
          ""clipId"": ""attack"",
          ""timelineId"": ""attack.timeline""
        }
      ]",
                @"""actionBindings"": [
        {
          ""bindingId"": ""locomotion"",
          ""actionId"": ""locomotion.move"",
          ""clipId"": """",
          ""blendId"": ""move2d"",
          ""timelineId"": ""attack.timeline""
        },
        {
          ""bindingId"": ""lightAttack"",
          ""actionId"": ""attack.light"",
          ""clipId"": ""attack"",
          ""timelineId"": ""attack.timeline""
        }
      ]");

            MxAnimationSetDefinition definition = MxAnimationCompiledArtifactJson
                .LoadSetDefinitions(json)
                .Single();

            Assert.AreEqual(1, definition.Actions.Count);
            Assert.AreEqual("lightAttack", definition.Actions[0].BindingId);
            CollectionAssert.Contains(
                definition.Warmup.RequiredKeys,
                new ResourceKey("char.test.anim.attack", ResourceTypeIds.AnimationClip, string.Empty, "test_package"));
        }

        [Test]
        public void LoadClipRegistry_UsesRuntimeCatalogPackageOverAnimationPackage()
        {
            string json = ClipRegistryJson.Replace(@"""packageId"": ""test_package""", @"""packageId"": ""animation.test""");
            var catalog = new ResourceCatalog(
                "runtime.test",
                "test_package",
                new[]
                {
                    new ResourceCatalogEntry(
                        "char.test.anim.idle",
                        ResourceTypeIds.AnimationClip,
                        "memory",
                        "idle",
                        string.Empty,
                        "test_package",
                        hash: "hash-idle")
                });

            MxAnimationClipRegistry registry = MxAnimationCompiledArtifactJson.LoadClipRegistry(json, catalog);

            Assert.AreEqual(new ResourceKey("char.test.anim.idle", ResourceTypeIds.AnimationClip, string.Empty, "test_package"), registry.Entries[0].ClipKey);
            Assert.AreEqual("hash-idle", registry.Entries[0].CatalogEntryHash);
        }

        [Test]
        public void LoadSetDefinitions_RejectsAuthoringJson()
        {
            Assert.Throws<MxAnimationCompiledArtifactJsonException>(
                () => MxAnimationCompiledArtifactJson.LoadSetDefinitions(@"{ ""format"": ""mx.animationAuthoring.v1"" }"));
        }

        private const string AnimationSetDefinitionJson = @"{
  ""format"": ""mx.animationSetDefinition.v1"",
  ""schemaVersion"": ""1.0"",
  ""packageId"": ""test_package"",
  ""sets"": [
    {
      ""setId"": ""set.base"",
      ""version"": ""1.0"",
      ""defaultClipId"": ""idle"",
      ""fallbackClipId"": ""idle"",
      ""layers"": [
        {
          ""layerId"": ""upper"",
          ""purpose"": ""combat"",
          ""weight"": 0.75,
          ""additive"": true,
          ""avatarMaskSelection"": {
            ""runtimeResourceKey"": ""char.test.mask.upper"",
            ""expectedKind"": ""avatarMask""
          }
        }
      ],
      ""groups"": [
        {
          ""groupId"": ""locomotion"",
          ""clips"": [
            {
              ""clipId"": ""idle"",
              ""runtimeResourceKey"": ""char.test.anim.idle"",
              ""loop"": true,
              ""speed"": 1,
              ""calibration"": {
                ""nativeVelocityX"": 0,
                ""nativeVelocityY"": 0,
                ""playbackSpeed"": 1,
                ""cycleDurationSeconds"": 1.2,
                ""leftFootContactWindows"": [
                  { ""startNormalized"": 0, ""endNormalized"": 1, ""confidence"": 1 }
                ],
                ""rightFootContactWindows"": [
                  { ""startNormalized"": 0, ""endNormalized"": 1, ""confidence"": 1 }
                ]
              }
            },
            {
              ""clipId"": ""attack"",
              ""runtimeResourceKey"": ""char.test.anim.attack"",
              ""loop"": false,
              ""speed"": 1.2
            }
          ],
          ""blend1D"": [
            {
              ""blendId"": ""speed"",
              ""parameter"": ""locomotion.speed"",
              ""points"": [
                { ""clipId"": ""idle"", ""value"": 0 },
                { ""clipId"": ""attack"", ""value"": 1 }
              ]
            }
          ],
          ""blend2D"": [
            {
              ""blendId"": ""move2d"",
              ""xParameter"": ""locomotion.x"",
              ""yParameter"": ""locomotion.y"",
              ""points"": [
                { ""clipId"": ""idle"", ""x"": -1, ""y"": 0 },
                { ""clipId"": ""attack"", ""x"": 1, ""y"": 0 }
              ]
            }
          ],
          ""timelines"": [
            {
              ""timelineId"": ""attack.timeline"",
              ""clipId"": ""attack"",
              ""timeDomain"": ""CombatFrame"",
              ""events"": [
                {
                  ""eventId"": ""trace.start"",
                  ""time"": 4,
                  ""eventKind"": ""trace.begin"",
                  ""resourceSelection"": {
                    ""runtimeResourceKey"": ""char.test.vfx.slash""
                  },
                  ""metadata"": {
                    ""socket"": ""mainHand"",
                    ""tag"": ""slash""
                  }
                }
              ]
            }
          ]
        }
      ],
      ""actionBindings"": [
        {
          ""bindingId"": ""lightAttack"",
          ""actionId"": ""attack.light"",
          ""clipId"": ""attack"",
          ""timelineId"": ""attack.timeline""
        }
      ]
    }
  ]
}";

        private const string ClipRegistryJson = @"{
  ""format"": ""mx.animationClipRegistry.v1"",
  ""schemaVersion"": ""1.0"",
  ""packageId"": ""test_package"",
  ""clips"": [
    {
      ""setId"": ""set.base"",
      ""groupId"": ""locomotion"",
      ""clipId"": ""idle"",
      ""runtimeResourceKey"": ""char.test.anim.idle""
    }
  ]
}";
    }
}
