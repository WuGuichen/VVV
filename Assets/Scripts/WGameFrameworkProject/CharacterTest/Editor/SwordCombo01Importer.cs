using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using MxFramework.Editor.CharacterAction;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>Imports only the four individual SwordAnimationPack combo-01 clips into the Common library.</summary>
    public static class SwordCombo01Importer
    {
        private const string LibraryPath = "CharacterActionSource/Libraries/Common/character_actions.json";
        private const string AssetRoot = "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/SwordCombo01";
        private const string SourceRoot = "CharacterActionSource/Libraries/Common/source/SwordAnimationPack/Combo01";
        private const string KeyPrefix = "art.character.skeleton.animation.sword_combo01_";

        public static string ImportClips(string humanoidRoot)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before importing animations.");
            string[] files = Enumerable.Range(1, 4).Select(i => Path.Combine(humanoidRoot,
                "02_Attack/01_Combo_Attack_01/Combo_Attack_01_0" + i + ".anim")).ToArray();
            foreach (string file in files)
                if (!File.Exists(file)) throw new FileNotFoundException("Missing combo segment", file);
            Directory.CreateDirectory(SourceRoot);
            Directory.CreateDirectory(AssetRoot);
            string temporary = "Assets/_TempImportedResources/SwordCombo01_" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(temporary);
            var report = new JArray();
            try
            {
                for (int i = 1; i <= 4; i++)
                {
                    string file = files[i - 1];
                    string sourcePath = SourceRoot + "/" + Path.GetFileName(file);
                    File.Copy(file, sourcePath, true);
                    string stagingPath = temporary + "/" + Path.GetFileName(file);
                    File.Copy(file, stagingPath);
                    AssetDatabase.ImportAsset(stagingPath, ImportAssetOptions.ForceSynchronousImport);
                    AnimationClip original = AssetDatabase.LoadAssetAtPath<AnimationClip>(stagingPath);
                    if (original == null || !original.humanMotion || Mathf.Abs(original.frameRate - 60f) > 0.01f)
                        throw new InvalidOperationException("Expected a 60 Hz Humanoid clip: " + stagingPath);
                    AnimationClip copy = UnityEngine.Object.Instantiate(original);
                    copy.name = "sword_combo01_0" + i;
                    foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(copy))
                        if (!string.IsNullOrEmpty(binding.path)) AnimationUtility.SetEditorCurve(copy, binding, null);
                    foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(copy))
                        if (!string.IsNullOrEmpty(binding.path)) AnimationUtility.SetObjectReferenceCurve(copy, binding, null);
                    var settings = AnimationUtility.GetAnimationClipSettings(copy);
                    settings.loopTime = false;
                    AnimationUtility.SetAnimationClipSettings(copy, settings);
                    string destination = AssetRoot + "/" + copy.name + ".anim";
                    AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(destination);
                    if (existing == null) AssetDatabase.CreateAsset(copy, destination);
                    else { EditorUtility.CopySerialized(copy, existing); EditorUtility.SetDirty(existing); UnityEngine.Object.DestroyImmediate(copy); }
                    report.Add(new JObject { ["segment"] = i, ["sourceRelativePath"] = "02_Attack/01_Combo_Attack_01/" + Path.GetFileName(file),
                        ["sha256"] = Hash(file), ["assetPath"] = destination, ["resourceKey"] = KeyPrefix + "0" + i,
                        ["durationSeconds"] = original.length, ["sampleRate"] = original.frameRate });
                }
                AssetDatabase.SaveAssets();
                File.WriteAllText(SourceRoot + "/provenance.json", report.ToString(Formatting.Indented));
            }
            finally { AssetDatabase.DeleteAsset(temporary); }
            return report.ToString(Formatting.Indented);
        }

        public static string BuildActions()
        {
            JObject library = JObject.Parse(File.ReadAllText(LibraryPath));
            JArray actions = (JArray)library["actions"];
            JObject template = actions.OfType<JObject>().Single(a => (int)a["actionId"] == 301);
            var ids = new int[4];
            // Initial authoring windows; these are editable source data, not runtime timing rules.
            int[] starts = { 10, 26, 10, 11 };
            int[] ends = { 22, 37, 58, 33 };
            for (int i = 0; i < 4; i++)
            {
                string token = "sword.combo01.0" + (i + 1);
                JObject prior = actions.OfType<JObject>().SingleOrDefault(a => ((JArray)a["tags"])?.Values<string>().Contains(token) == true);
                ids[i] = prior == null ? CharacterActionIdCatalogApi.Allocate("Common", LibraryPath) : (int)prior["actionId"];
                if (prior != null) continue; // Reimport must not overwrite subsequent designer tuning.
                string key = KeyPrefix + "0" + (i + 1);
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetRoot + "/sword_combo01_0" + (i + 1) + ".anim");
                if (clip == null) throw new InvalidOperationException("Import clips first.");
                int duration = Mathf.RoundToInt(clip.length * 60);
                int recovery = ends[i] + 1;
                JObject action = (JObject)template.DeepClone();
                action["actionId"] = ids[i];
                action["displayName"] = "持剑四连击 01 · 第" + (i + 1) + "段";
                action["tags"] = new JArray("combat", "sword", "combo", token);
                action["durationFrames"] = duration;
                action["requirements"] = new JArray(new JObject { ["kind"] = "Grounded" });
                action["phases"] = new JArray(Phase("Startup", 0, starts[i]-1), Phase("Active", starts[i], ends[i]), Phase("Recovery", recovery, duration-1));
                action["cancelRules"] = new JArray(Rule(recovery, duration-1, 4, 1), Rule(recovery, duration-1, 48, 36), Rule(recovery+10, duration-1, 2, 0));
                action["presentationTrack"] = null;
                action["gameplayTrack"] = null;
                action["debugTrack"] = null;
                action["motionTrack"] = new JObject {
                    ["usesRootMotion"] = true,
                    ["rootMotion"] = new JObject { ["sourceAnimationEventId"] = token+".animation", ["sourceAnimationClipResourceKey"] = key,
                        ["space"] = "Local", ["sampleTickRate"] = 60, ["obstaclePolicy"] = "Default", ["frames"] = Bake(clip, duration) },
                    ["events"] = new JArray(new JObject { ["kind"] = "SetControlScale", ["frame"] = 0, ["endFrame"] = duration-1,
                        ["movementControlScale"] = 0, ["rotationControlScale"] = 0, ["stableEventId"] = token+".control" }) };
                JObject hit = (JObject)template["combatTrack"]["events"][0].DeepClone();
                hit["frame"] = starts[i]; hit["endFrame"] = ends[i]; hit["stableEventId"] = token+".hit";
                hit["combatActionId"] = "";
                hit["hitEvent"]["stableEventId"] = token+".hit";
                hit["hitEvent"]["hitSameTargetPolicy"] = "OncePerEvent";
                hit["hitEvent"]["damage"]["launchPower"] = 0;
                action["combatTrack"] = new JObject { ["combatActionId"] = "", ["events"] = new JArray(hit) };
                action["animationTrack"] = new JObject { ["events"] = new JArray(new JObject { ["kind"] = "CrossFadeAnimation",
                    ["frame"] = 0, ["endFrame"] = duration-1, ["playbackSpeed"] = 1, ["animationClipResourceKey"] = key,
                    ["animationActionKey"] = "", ["transitionSeconds"] = 0.12, ["stableEventId"] = token+".animation" }) };
                actions.Add(action);
            }
            File.WriteAllText(LibraryPath, library.ToString(Formatting.Indented)+Environment.NewLine);
            CharacterActionIdCatalogApi.Validate();
            JObject weapons = JObject.Parse(File.ReadAllText("WeaponSource/global_weapon_library.json"));
            JObject animation = ((JArray)weapons["animationConfigurations"]).OfType<JObject>().Single(c => (string)c["animationConfigurationId"] == "animation.combat_girl_sword.profile.sword");
            JArray clips = (JArray)((JArray)animation["sets"][0]["groups"]).OfType<JObject>().Single(g => (string)g["groupId"] == "group.combat")["clips"];
            JObject clipTemplate = (JObject)clips[0];
            for (int i = 0; i < 4; i++)
            {
                string name = "sword_combo01_0" + (i+1); string key = KeyPrefix+"0"+(i+1);
                if (clips.OfType<JObject>().Any(c => (string)c["runtimeResourceKey"] == key)) continue;
                string path = AssetRoot+"/"+name+".anim";
                JObject entry = (JObject)clipTemplate.DeepClone();
                entry["clipId"] = name; entry["displayName"] = name; entry["sourceClipName"] = name;
                entry["runtimeResourceKey"] = key; entry["loop"] = false; entry["speed"] = 1; entry["rootMotionPolicy"] = "Ignore";
                entry.Remove("metadata"); entry.Remove("calibration");
                JObject selection = (JObject)entry["sourceSelection"];
                selection["resourceStableId"] = "resource.mapping.mxframework.samples|"+key+"|AnimationClip|";
                selection["unityGuid"] = AssetDatabase.AssetPathToGUID(path); selection["unityAssetPath"] = path;
                selection["runtimeResourceKey"] = key; selection["providerResourceKey"] = key;
                clips.Add(entry);
            }
            File.WriteAllText("WeaponSource/global_weapon_library.json", weapons.ToString(Formatting.Indented)+Environment.NewLine);
            string applicationPath = "Authoring/CharacterPackages/character-000001/config/character_application.json";
            JObject application = JObject.Parse(File.ReadAllText(applicationPath));
            foreach (JObject binding in (JArray)application["combinedWeaponConfigurationBindings"])
            {
                JObject config = (JObject)binding["overrides"]?["actionConfiguration"];
                if (config == null) continue;
                JArray roles = (JArray)config["actions"];
                for (int i = 0; i < 4; i++)
                {
                    string role = i == 0 ? "primary" : "primary.combo"+(i+1);
                    JObject item = roles.OfType<JObject>().SingleOrDefault(a => (string)a["actionRole"] == role);
                    if (item == null) { item = new JObject { ["actionRole"] = role }; roles.Add(item); }
                    item["actionId"] = ids[i];
                }
                JObject sequence = ((JArray)config["commandSequences"]).OfType<JObject>().Single(s => (string)s["intentId"] == "LightAttack");
                sequence["actionRoles"] = new JArray("primary", "primary.combo2", "primary.combo3", "primary.combo4");
            }
            File.WriteAllText(applicationPath, application.ToString(Formatting.Indented)+Environment.NewLine);
            return "Common combo action IDs: " + string.Join(",", ids);
        }

        private static JObject Phase(string kind, int start, int end) => new JObject { ["kind"] = kind, ["startFrame"] = start, ["endFrame"] = end };
        private static JObject Rule(int start, int end, int request, int slot) => new JObject { ["startFrame"] = start, ["endFrame"] = end, ["requestTypeMask"] = request, ["actionSlotMask"] = slot };
        private static JArray Bake(AnimationClip clip, int duration)
        {
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            AnimationCurve x = AnimationUtility.GetEditorCurve(clip, bindings.Single(b => b.propertyName == "RootT.x" && b.path == ""));
            AnimationCurve z = AnimationUtility.GetEditorCurve(clip, bindings.Single(b => b.propertyName == "RootT.z" && b.path == ""));
            var frames = new JArray();
            for (int f = 0; f < duration; f++) frames.Add(new JObject { ["frame"] = f,
                ["x"] = x.Evaluate((f+1)/60f)-x.Evaluate(f/60f), ["y"] = 0, ["z"] = z.Evaluate((f+1)/60f)-z.Evaluate(f/60f) });
            return frames;
        }
        private static string Hash(string path)
        {
            using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path))
                return "sha256:"+BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
