using System;
using System.Collections.Generic;
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
    /// <summary>Imports combo 02 segments; action timings require observed preview evidence.</summary>
    public static class SwordCombo02Importer
    {
        public const string AssetRoot = "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/SwordCombo02";
        public const string SourceRoot = "CharacterActionSource/Libraries/Common/source/SwordAnimationPack/Combo02";
        public const string KeyPrefix = "art.character.skeleton.animation.sword_combo02_";
        private const string RelativeRoot = "02_Attack/02_Combo_Attack_02";
        private const string LibraryPath = "CharacterActionSource/Libraries/Common/character_actions.json";
        private const string WeaponPath = "WeaponSource/global_weapon_library.json";

        public static string ImportClips(string humanoidRoot)
        {
            EnsureEditorAvailable();
            string folder = Path.Combine(humanoidRoot, RelativeRoot);
            string[] files = Directory.GetFiles(folder, "Combo_Attack_02_*.anim")
                .Where(p => !Path.GetFileNameWithoutExtension(p).EndsWith("_All", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p, StringComparer.Ordinal).ToArray();
            string[] expected = Enumerable.Range(1, 4).Select(i => "Combo_Attack_02_" + i.ToString("00") + ".anim").ToArray();
            if (!files.Select(Path.GetFileName).SequenceEqual(expected))
                throw new InvalidOperationException("Expected exactly the four numbered combo-02 segments.");
            JArray previous = File.Exists(SourceRoot + "/provenance.json")
                ? JArray.Parse(File.ReadAllText(SourceRoot + "/provenance.json")) : new JArray();
            for (int i = 0; i < files.Length; i++)
            {
                string destination = AssetRoot + "/sword_combo02_" + (i + 1).ToString("00") + ".anim";
                JObject prior = previous.OfType<JObject>().SingleOrDefault(p => (int?)p["segment"] == i + 1);
                if (File.Exists(destination) && (prior == null || (string)prior["sha256"] != Hash(files[i]) || (string)prior["assetPath"] != destination))
                    throw new InvalidOperationException("Existing asset lacks matching provenance; refusing to overwrite: " + destination);
                string archive = SourceRoot + "/" + Path.GetFileName(files[i]);
                if (File.Exists(archive) && Hash(archive) != Hash(files[i]))
                    throw new InvalidOperationException("Existing original archive differs: " + archive);
            }
            Directory.CreateDirectory(SourceRoot);
            EnsureAssetFolder(AssetRoot);
            string temporary = "Assets/_TempImportedResources/SwordCombo02_" + Guid.NewGuid().ToString("N");
            EnsureAssetFolder(temporary);
            JArray receipt = new JArray();
            try
            {
                for (int i = 0; i < files.Length; i++)
                {
                    string segment = (i + 1).ToString("00");
                    string destination = AssetRoot + "/sword_combo02_" + segment + ".anim";
                    string archive = SourceRoot + "/" + Path.GetFileName(files[i]);
                    if (!File.Exists(archive)) File.Copy(files[i], archive);
                    AnimationClip imported = AssetDatabase.LoadAssetAtPath<AnimationClip>(destination);
                    if (imported == null)
                    {
                        string staged = temporary + "/" + Path.GetFileName(files[i]);
                        File.Copy(files[i], staged);
                        AssetDatabase.ImportAsset(staged, ImportAssetOptions.ForceSynchronousImport);
                        AnimationClip original = AssetDatabase.LoadAssetAtPath<AnimationClip>(staged);
                        ValidateClip(original, staged);
                        AnimationClip copy = UnityEngine.Object.Instantiate(original);
                        copy.name = "sword_combo02_" + segment;
                        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(copy))
                            if (!string.IsNullOrEmpty(binding.path)) AnimationUtility.SetEditorCurve(copy, binding, null);
                        foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(copy))
                            if (!string.IsNullOrEmpty(binding.path)) AnimationUtility.SetObjectReferenceCurve(copy, binding, null);
                        var settings = AnimationUtility.GetAnimationClipSettings(copy);
                        settings.loopTime = false;
                        AnimationUtility.SetAnimationClipSettings(copy, settings);
                        AssetDatabase.CreateAsset(copy, destination);
                        AssetDatabase.SaveAssetIfDirty(copy);
                        imported = copy;
                    }
                    ValidateClip(imported, destination);
                    receipt.Add(new JObject { ["segment"] = i + 1, ["sourceRelativePath"] = RelativeRoot + "/" + Path.GetFileName(files[i]),
                        ["sha256"] = Hash(files[i]), ["assetPath"] = destination, ["resourceKey"] = KeyPrefix + segment,
                        ["durationSeconds"] = imported.length, ["sampleRate"] = imported.frameRate });
                }
                File.WriteAllText(SourceRoot + "/provenance.json", receipt.ToString(Formatting.Indented) + Environment.NewLine);
            }
            finally { AssetDatabase.DeleteAsset(temporary); }
            return receipt.ToString(Formatting.Indented);
        }

        /// <summary>Creates animation-only actions; the user will author hit windows.</summary>
        public static string BuildPreviewActions(int[] allocatedActionIds = null)
        {
            JArray provenance = JArray.Parse(File.ReadAllText(SourceRoot + "/provenance.json"));
            JArray observations = new JArray();
            foreach (JObject entry in provenance)
            {
                int segment = (int)entry["segment"];
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>((string)entry["assetPath"]);
                ValidateClip(clip, (string)entry["assetPath"]);
                observations.Add(new JObject { ["segment"] = segment, ["sourceSha256"] = (string)entry["sha256"],
                    ["durationFrames"] = Mathf.RoundToInt(clip.length * 60f), ["groundedRequired"] = false,
                    ["hitWindows"] = new JArray(), ["hitWindowStatus"] = "UserAuthoringPending" });
            }
            return BuildActions(observations.ToString(Formatting.None), previewOnly: true, allocatedActionIds: allocatedActionIds);
        }

        // Non-preview mode requires observed strike evidence.
        // This entry never writes character_application.json or ActionSet defaults.
        public static string BuildActions(string observationsJson, bool previewOnly = false, int[] allocatedActionIds = null)
        {
            EnsureEditorAvailable();
            JArray observations = JArray.Parse(observationsJson);
            if (observations.Count != 4 || !observations.OfType<JObject>().Select(o => (int)o["segment"]).OrderBy(i => i).SequenceEqual(new[] {1,2,3,4}))
                throw new InvalidOperationException("Provide observations for all four segments.");
            JArray provenance = JArray.Parse(File.ReadAllText(SourceRoot + "/provenance.json"));
            var clips = new Dictionary<int, AnimationClip>();
            foreach (JObject observation in observations)
            {
                int segment = (int)observation["segment"];
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetRoot + "/sword_combo02_" + segment.ToString("00") + ".anim");
                ValidateClip(clip, "segment " + segment);
                int duration = Mathf.RoundToInt(clip.length * 60f);
                JObject source = provenance.OfType<JObject>().Single(p => (int)p["segment"] == segment);
                if ((!previewOnly && (bool?)observation["visualConfirmed"] != true) || (string)observation["sourceSha256"] != (string)source["sha256"]
                    || (int?)observation["durationFrames"] != duration || observation["groundedRequired"]?.Type != JTokenType.Boolean)
                    throw new InvalidOperationException("Observation must identify the sampled clip, duration and grounded requirement: " + segment);
                JArray evidence = observation["evidenceFiles"] as JArray;
                if (!previewOnly && (evidence == null || evidence.Count == 0 || evidence.Values<string>().Any(p => !File.Exists(p))))
                    throw new InvalidOperationException("Observed preview evidence is required: " + segment);
                JArray windows = observation["hitWindows"] as JArray;
                if (windows == null || (!previewOnly && windows.Count == 0)) throw new InvalidOperationException("Observed hit windows are required: " + segment);
                int previousStart = -1;
                foreach (JObject window in windows)
                {
                    int start = (int)window["startFrame"], end = (int)window["endFrame"];
                    if (start <= 0 || end < start || end >= duration - 1 || start < previousStart)
                        throw new InvalidOperationException("Invalid or unordered observed hit window: " + segment);
                    previousStart = start;
                }
                clips.Add(segment, clip);
            }
            CharacterActionIdCatalogApi.Validate();
            if (allocatedActionIds != null)
            {
                if (allocatedActionIds.Length != 4 || allocatedActionIds.Distinct().Count() != 4)
                    throw new InvalidOperationException("Four distinct allocated IDs are required.");
                JObject catalog = JObject.Parse(File.ReadAllText("CharacterActionSource/action_id_catalog.json"));
                foreach (int allocated in allocatedActionIds)
                    if (!((JArray)catalog["allocations"]).OfType<JObject>().Any(a => (int)a["actionId"] == allocated
                        && (string)a["libraryId"] == "Common" && (string)a["sourcePath"] == LibraryPath && (string)a["status"] == "active"))
                        throw new InvalidOperationException("ID is not an active Common source allocation: " + allocated);
            }
            string expectedLibraryHash = Hash(LibraryPath), expectedWeaponHash = Hash(WeaponPath);
            JObject library = JObject.Parse(File.ReadAllText(LibraryPath));
            JArray actions = (JArray)library["actions"];
            JObject template = actions.OfType<JObject>().Single(a => (int)a["actionId"] == 301);
            var ids = new int[4];
            foreach (JObject observation in observations.OrderBy(o => (int)o["segment"]))
            {
                int segment = (int)observation["segment"], duration = Mathf.RoundToInt(clips[segment].length * 60f);
                string suffix = segment.ToString("00"), token = "sword.combo02." + suffix, key = KeyPrefix + suffix;
                JObject prior = actions.OfType<JObject>().SingleOrDefault(a => ((JArray)a["tags"])?.Values<string>().Contains(token) == true);
                if (prior != null) { ids[segment - 1] = (int)prior["actionId"]; continue; }
                int id = allocatedActionIds == null ? CharacterActionIdCatalogApi.Allocate("Common", LibraryPath) : allocatedActionIds[segment-1];
                if (actions.OfType<JObject>().Any(a => (int)a["actionId"] == id)) throw new InvalidOperationException("Allocated action ID already exists: " + id);
                ids[segment - 1] = id;
                JObject action = (JObject)template.DeepClone();
                JArray windows = (JArray)observation["hitWindows"];
                int firstHit = windows.Count == 0 ? 0 : windows.Min(w => (int)w["startFrame"]), lastHit = windows.Count == 0 ? duration - 1 : windows.Max(w => (int)w["endFrame"]);
                action["actionId"] = id;
                action["displayName"] = "持剑连击 02 · 第" + segment + "段";
                action["tags"] = new JArray("combat", "sword", "combo", token);
                action["durationFrames"] = duration;
                action["requirements"] = (bool)observation["groundedRequired"] ? new JArray(new JObject { ["kind"] = "Grounded" }) : new JArray();
                action["phases"] = windows.Count == 0 ? new JArray(Phase("Active",0,duration-1)) : new JArray(Phase("Startup",0,firstHit-1),Phase("Active",firstHit,lastHit),Phase("Recovery",lastHit+1,duration-1));
                if (previewOnly) ((JArray)action["tags"]).Add("authoring.hit_windows_pending");
                action["cancelRules"] = new JArray(); // Add only observed/explicit cancel rules in a subsequent authoring pass.
                action["presentationTrack"] = null; action["gameplayTrack"] = null; action["debugTrack"] = null;
                action["motionTrack"] = new JObject { ["usesRootMotion"] = true,
                    ["rootMotion"] = new JObject { ["sourceAnimationEventId"] = token + ".animation", ["sourceAnimationClipResourceKey"] = key,
                        ["space"] = "Local", ["sampleTickRate"] = 60, ["obstaclePolicy"] = "Default", ["frames"] = BakePlanar(clips[segment],duration) },
                    ["events"] = new JArray(new JObject { ["kind"] = "SetControlScale", ["frame"] = 0, ["endFrame"] = duration-1,
                        ["movementControlScale"] = 0, ["rotationControlScale"] = 0, ["stableEventId"] = token + ".control" }) };
                JArray hitEvents = new JArray();
                for (int wi = 0; wi < windows.Count; wi++)
                {
                    string hitId = token + ".hit." + (wi+1).ToString("00");
                    JObject hit = (JObject)template["combatTrack"]["events"][0].DeepClone();
                    hit["frame"] = (int)windows[wi]["startFrame"]; hit["endFrame"] = (int)windows[wi]["endFrame"];
                    hit["stableEventId"] = hitId; hit["combatActionId"] = "";
                    hit["hitEvent"]["stableEventId"] = hitId;
                    hit["hitEvent"]["hitSameTargetPolicy"] = "OncePerEvent";
                    hit["hitEvent"]["damage"]["launchPower"] = 0;
                    hitEvents.Add(hit);
                }
                action["combatTrack"] = new JObject { ["combatActionId"] = "", ["events"] = hitEvents };
                action["animationTrack"] = new JObject { ["events"] = new JArray(new JObject {
                    ["kind"] = "CrossFadeAnimation", ["frame"] = 0, ["endFrame"] = duration-1, ["playbackSpeed"] = 1,
                    ["animationClipResourceKey"] = key, ["animationActionKey"] = "", ["transitionSeconds"] = 0.12,
                    ["stableEventId"] = token + ".animation" }) };
                actions.Add(action);
            }
            JObject weapons = JObject.Parse(File.ReadAllText(WeaponPath));
            JObject animation = ((JArray)weapons["animationConfigurations"]).OfType<JObject>().Single(c => (string)c["animationConfigurationId"] == "animation.combat_girl_sword.profile.sword");
            JArray registered = (JArray)((JArray)animation["sets"][0]["groups"]).OfType<JObject>().Single(g => (string)g["groupId"] == "group.combat")["clips"];
            JObject clipTemplate = registered.OfType<JObject>().Single(c => (string)c["runtimeResourceKey"] == "art.character.skeleton.animation.sword_combo01_01");
            for (int segment = 1; segment <= 4; segment++)
            {
                string name = "sword_combo02_" + segment.ToString("00"), key = KeyPrefix + segment.ToString("00"), asset = AssetRoot + "/" + name + ".anim";
                JObject prior = registered.OfType<JObject>().SingleOrDefault(c => (string)c["runtimeResourceKey"] == key);
                if (prior != null) continue;
                JObject entry = (JObject)clipTemplate.DeepClone();
                entry["clipId"] = name; entry["displayName"] = name; entry["sourceClipName"] = name; entry["runtimeResourceKey"] = key;
                entry["loop"] = false; entry["speed"] = 1; entry["rootMotionPolicy"] = "Ignore"; entry.Remove("metadata"); entry.Remove("calibration");
                JObject selection = (JObject)entry["sourceSelection"];
                selection["resourceStableId"] = "resource.mapping.mxframework.samples|" + key + "|AnimationClip|";
                selection["unityGuid"] = AssetDatabase.AssetPathToGUID(asset); selection["unityAssetPath"] = asset;
                selection["runtimeResourceKey"] = key; selection["providerResourceKey"] = key;
                registered.Add(entry);
            }
            if (Hash(LibraryPath) != expectedLibraryHash || Hash(WeaponPath) != expectedWeaponHash)
                throw new InvalidOperationException("Authoring inputs changed during preparation; stop and reconcile.");
            File.WriteAllText(LibraryPath,library.ToString(Formatting.Indented)+Environment.NewLine);
            File.WriteAllText(WeaponPath,weapons.ToString(Formatting.Indented)+Environment.NewLine);
            File.WriteAllText(SourceRoot+"/observations.json",observations.ToString(Formatting.Indented)+Environment.NewLine);
            CharacterActionIdCatalogApi.Validate();
            return JsonConvert.SerializeObject(new { actionIds = ids, resourceKeys = Enumerable.Range(1,4).Select(i=>KeyPrefix+i.ToString("00")).ToArray() });
        }

        private static JObject Phase(string kind,int start,int end) => new JObject { ["kind"]=kind,["startFrame"]=start,["endFrame"]=end };
        private static void EnsureEditorAvailable()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Wait for an idle Edit Mode Editor before importing combo 02.");
        }
        private static void ValidateClip(AnimationClip clip,string label)
        {
            if (clip == null || !clip.humanMotion || Mathf.Abs(clip.frameRate-60f)>0.01f)
                throw new InvalidOperationException("Expected a 60 Hz Humanoid clip: "+label);
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            if (!bindings.Any(b=>b.path==""&&b.propertyName=="RootT.x") || !bindings.Any(b=>b.path==""&&b.propertyName=="RootT.z"))
                throw new InvalidOperationException("Root translation curves required for baking are absent: "+label);
        }
        private static void EnsureAssetFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            int slash = folder.LastIndexOf('/');
            string parent = folder.Substring(0,slash);
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent,folder.Substring(slash+1));
        }
        private static JArray BakePlanar(AnimationClip clip,int duration)
        {
            EditorCurveBinding[] bindings=AnimationUtility.GetCurveBindings(clip);
            AnimationCurve x=AnimationUtility.GetEditorCurve(clip,bindings.Single(b=>b.path==""&&b.propertyName=="RootT.x"));
            AnimationCurve z=AnimationUtility.GetEditorCurve(clip,bindings.Single(b=>b.path==""&&b.propertyName=="RootT.z"));
            JArray frames=new JArray();
            for(int f=0;f<duration;f++) frames.Add(new JObject { ["frame"]=f,["x"]=x.Evaluate((f+1)/60f)-x.Evaluate(f/60f),["y"]=0,["z"]=z.Evaluate((f+1)/60f)-z.Evaluate(f/60f) });
            return frames;
        }
        private static string Hash(string path)
        {
            using(var sha=SHA256.Create()) using(var stream=File.OpenRead(path))
                return "sha256:"+BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
        }
    }
}
