using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class SwordCombo01ContractTests
    {
        private const string ClipRoot = "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/SwordCombo01/";

        [TestCase(455, 95, 10, 22)]
        [TestCase(456, 105, 26, 37)]
        [TestCase(457, 112, 10, 58)]
        [TestCase(458, 109, 11, 33)]
        public void CommonActionHasIndependentTimelineAndRecoveryWindows(int id, int duration, int start, int end)
        {
            foreach (string path in new[] { "CharacterActionSource/Libraries/Common/character_actions.json",
                "Assets/MxFrameworkGenerated/CharacterActions/Libraries/Common/character_actions.json" })
            {
                JObject action = JObject.Parse(File.ReadAllText(path))["actions"].OfType<JObject>().Single(a => (int)a["actionId"] == id);
                Assert.AreEqual(duration, (int)action["durationFrames"], path);
                JToken hit = action["combatTrack"]["events"].Single();
                Assert.AreEqual("HitTraceWindow", (string)hit["kind"]);
                Assert.AreEqual(start, (int)hit["frame"]);
                Assert.AreEqual(end, (int)hit["endFrame"]);
                Assert.AreEqual("OncePerEvent", (string)hit["hitEvent"]["hitSameTargetPolicy"]);
                JToken recovery = action["phases"].Single(p => (string)p["kind"] == "Recovery");
                Assert.AreEqual(end + 1, (int)recovery["startFrame"]);
                JToken defense = action["cancelRules"].Single(r => (int)r["requestTypeMask"] == 48);
                Assert.AreEqual(end + 1, (int)defense["startFrame"]);
                Assert.AreEqual(36, (int)defense["actionSlotMask"]);
                JToken movement = action["cancelRules"].Single(r => (int)r["requestTypeMask"] == 2);
                Assert.AreEqual(end + 11, (int)movement["startFrame"]);
                Assert.AreEqual(duration - 1, (int)movement["endFrame"]);
                Assert.AreEqual(duration, action["motionTrack"]["rootMotion"]["frames"].Count());
                Assert.IsTrue(action["motionTrack"]["rootMotion"]["frames"].All(f => (float)f["y"] == 0));
                StringAssert.EndsWith("sword_combo01_0" + (id - 454), (string)action["animationTrack"]["events"][0]["animationClipResourceKey"]);
            }
        }

        [Test]
        public void Both000001EquipmentStatesResolveTheFourSegmentSequence()
        {
            JToken sets = JObject.Parse(File.ReadAllText("Assets/MxFrameworkGenerated/CharacterActions/ActionSets/000001/character_action_sets.json"))["actionSets"];
            Assert.AreEqual(2, sets.Count());
            foreach (JToken set in sets)
            {
                JToken sequence = set["commandSequences"].Single(s => (string)s["intentId"] == "LightAttack");
                CollectionAssert.AreEqual(new[] { 455, 456, 457, 458 }, sequence["actionIds"].Values<int>().ToArray());
            }
            string[] paths = Enumerable.Range(1, 4).Select(i => ClipRoot + "sword_combo01_0" + i + ".anim").ToArray();
            Assert.AreEqual(4, paths.Select(AssetDatabase.AssetPathToGUID).Where(g => !string.IsNullOrEmpty(g)).Distinct().Count());
            Assert.AreEqual(4, Directory.GetFiles(ClipRoot, "*.anim").Length);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void IndependentClipRetargetsThroughPlayablesWithoutMovingTransform(int segment)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipRoot + "sword_combo01_0" + segment + ".anim");
            Assert.NotNull(clip);
            Assert.IsTrue(clip.humanMotion);
            Assert.AreEqual(60, clip.frameRate);
            Assert.IsFalse(AnimationUtility.GetAnimationClipSettings(clip).loopTime);
            Assert.IsTrue(AnimationUtility.GetCurveBindings(clip).All(b => string.IsNullOrEmpty(b.path)));
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MxFrameworkGenerated/CharacterPackages/000001/resources/models/000001_lod0.fbx");
            GameObject instance = Object.Instantiate(model);
            PlayableGraph graph = PlayableGraph.Create("SwordCombo01Contract");
            try
            {
                Animator animator = instance.GetComponentInChildren<Animator>();
                Assert.IsTrue(animator.avatar.isValid && animator.avatar.isHuman);
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind();
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, clip);
                AnimationPlayableOutput.Create(graph, "clip", animator).SetSourcePlayable(playable);
                graph.Play();
                playable.SetTime(0);
                graph.Evaluate(0);
                Vector3 origin = instance.transform.position;
                Vector3 initialHand = animator.GetBoneTransform(HumanBodyBones.RightHand).position;
                float maximumDistance = 0;
                for (int frame = 1; frame < Mathf.RoundToInt(clip.length * 60); frame++)
                {
                    playable.SetTime(frame / 60.0);
                    graph.Evaluate(0);
                    Vector3 hand = animator.GetBoneTransform(HumanBodyBones.RightHand).position;
                    Assert.IsFalse(float.IsNaN(hand.x) || float.IsNaN(hand.y) || float.IsNaN(hand.z));
                    maximumDistance = Mathf.Max(maximumDistance, Vector3.Distance(initialHand, hand));
                    Assert.Less(Vector3.Distance(origin, instance.transform.position), 0.0001f);
                }
                Assert.Greater(maximumDistance, 0.05f);
            }
            finally
            {
                graph.Destroy();
                Object.DestroyImmediate(instance);
            }
        }
    }
}
