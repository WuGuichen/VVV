using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MxFramework.Animation;
using MxFramework.CharacterAction;
using MxFramework.Core.Math;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Collects content from the actual action and bake providers, never caller-supplied content hashes.</summary>
    /// <remarks>LowFreqAlloc. Measurement/load boundary only; never call from Tick or an action guard.</remarks>
    internal static class CharacterActionContentHashUtility
    {
        internal const string FingerprintVersion = "character-action-tactical-input-v1";

        internal static string ComputeReferenceDummyHash(CharacterActionReferenceDummy dummy)
        {
            if (dummy == null) throw new ArgumentNullException(nameof(dummy));
            return Hash(new JObject { ["schema"] = "reference-capsule-v1", ["geometry"] = ToToken(dummy) });
        }

        internal static bool TryCompute(CharacterActionConfig action, CharacterTestCharacterEntity source,
            CharacterTestCharacterPhysicsRuntime physics, ICharacterTestBakedWeaponTraceSource bakedSource,
            ICharacterTestWeaponTraceBindingResolver bindings, out string hash, out string issue, CharacterActionProjectileReferenceCatalog combat = null, bool requireProjectileCatalog = false)
        {
            hash = string.Empty;
            issue = string.Empty;
            if (action == null) { issue = "ActionUnavailable"; return false; }
            var dependencies = new JArray();
            var content = new JObject { ["schema"] = FingerprintVersion, ["action"] = ToToken(action), ["dependencies"] = dependencies };
            // A nominal measurement is bound to a frozen source shape, not its live world transform or entity id.
            if (source != null && physics != null)
            {
                FixVector3 extents = physics.GetCharacterHalfExtents(source);
                content["sourceBody"] = new JArray(extents.X.RawValue, extents.Y.RawValue, extents.Z.RawValue,
                    physics.GetCharacterCenterOffsetY(source).RawValue);
                content["sourceAuthorityRig"] = ToToken(source.AuthorityPose?.Definition);
            }
            for (int eventIndex = 0; eventIndex < action.CombatTrack.Events.Length; eventIndex++)
            {
                CombatTrackEvent evt = action.CombatTrack.Events[eventIndex];
                if (evt.Kind != CharacterActionTrackEventKind.HitTraceWindow) continue;
                // Match the existing evaluator's finite preview limit and fail before an unbounded traversal.
                if ((long)evt.EndFrame - evt.Frame > 240) { issue = "TraceWindowUnsupported"; return false; }
                if (evt.HitVolume.HasVolume && evt.HitVolume.SourceKind == CharacterActionHitVolumeSourceKind.CasterBody
                    && (source == null || physics == null)) { issue = "SourceBodyUnavailable"; return false; }
                CharacterTestWeaponTraceBinding binding = default;
                if (!evt.HitVolume.HasVolume)
                {
                    if (bakedSource == null || bindings == null || !bindings.TryResolve(source, evt.EquipmentSlotId, evt.TraceProfileId, out binding))
                    { issue = "WeaponBindingUnavailable"; return false; }
                    dependencies.Add(new JObject { ["eventIndex"] = eventIndex, ["binding"] = ToToken(new {
                        binding.TraceProfileId, binding.EquipmentSlotId, binding.WeaponId, binding.Length, binding.Radius,
                        binding.ProximalExtension, binding.ProximalRadius, binding.TraceBinding }) });
                }
                for (long local = evt.Frame; local <= evt.EndFrame; local++)
                {
                    int frame = (int)local;
                    if (evt.HitVolume.HasVolume && evt.HitVolume.SourceKind != CharacterActionHitVolumeSourceKind.BakedSocketCapsule) continue;
                    string clip = ResolveAnimationClipKey(action, frame);
                    if (string.IsNullOrWhiteSpace(clip) || bakedSource == null) { issue = "AnimationBakeUnavailable"; return false; }
                    var dependency = new JObject { ["eventIndex"] = eventIndex, ["frame"] = frame, ["clip"] = clip };
                    if (!evt.HitVolume.HasVolume)
                    {
                        if (!bakedSource.TryGetFrameForEntity(source, clip, binding.TraceProfileId, frame, out MxAnimationBakedWeaponTraceFrame baked) || baked == null)
                        { issue = "WeaponBakeFrameUnavailable:" + frame.ToString(CultureInfo.InvariantCulture); return false; }
                        dependency["weaponFrame"] = ToToken(baked);
                        // This is a nominal content fingerprint. Transient secondary-motion offsets
                        // remain part of live hit geometry, not authored measurement dependencies.
                    }
                    else
                    {
                        if (!TryGetSocket(bakedSource, source, clip, evt.HitVolume.StartSocketId, frame, out MxAnimationBakedSocketFrame start)
                            || !TryGetSocket(bakedSource, source, clip, evt.HitVolume.EndSocketId, frame, out MxAnimationBakedSocketFrame end))
                        { issue = "SocketBakeFrameUnavailable:" + frame.ToString(CultureInfo.InvariantCulture); return false; }
                        dependency["startSocket"] = ToToken(start);
                        dependency["endSocket"] = ToToken(end);
                    }
                    dependencies.Add(dependency);
                }
            }
            if (CharacterActionProjectileReference.IsCandidate(action) && (combat != null || requireProjectileCatalog))
            {
                if (combat == null) { issue = "ProjectileMeasurementProviderMissing"; return false; }
                if (!combat.TryGetReferenceProjectile(action, out var evt, out var profile, out var definition, out issue)) return false;
                string clip = ResolveAnimationClipKey(action, evt.Frame);
                bool baked = bakedSource != null && !string.IsNullOrEmpty(clip)
                    && TryGetSocket(bakedSource, source, clip, profile.Origin.SocketId, evt.Frame, out _);
                var projectile = new JObject { ["executor"] = CharacterActionProjectileReference.ExecutorFingerprint,
                    ["profile"] = ToToken(profile), ["definition"] = ToToken(definition), ["clip"] = clip,
                    ["usesSocketBake"] = baked };
                if (baked && TryGetSocket(bakedSource, source, clip, profile.Origin.SocketId, evt.Frame, out var socket))
                    projectile["socket"] = ToToken(socket);
                dependencies.Add(projectile);
            }
            hash = Hash(content);
            return true;
        }

        private static bool TryGetSocket(ICharacterTestBakedWeaponTraceSource bakedSource, CharacterTestCharacterEntity source,
            string clip, int socket, int frame, out MxAnimationBakedSocketFrame value)
        {
            value = null;
            if (bakedSource is ICharacterTestPackageBakedSocketSource packages)
                return source != null && packages.TryGetSocketFrame(source.Identity.GeneratedPackageRoot, clip, socket, frame, out value) && value != null;
            return bakedSource.TryGetSocketFrame(clip, socket, frame, out value) && value != null;
        }

        private static string ResolveAnimationClipKey(CharacterActionConfig action, int localFrame)
        {
            string key = string.Empty;
            foreach (AnimationTrackEvent evt in action.AnimationTrack.Events)
                if (evt.Frame <= localFrame && localFrame <= evt.EndFrame) key = evt.EffectiveAnimationKey;
            return key;
        }

        private static JToken ToToken(object value)
        {
            if (value == null) return JValue.CreateNull();
            // Do not inherit JsonConvert.DefaultSettings or ambient culture from an editor/consumer.
            return JToken.FromObject(value, JsonSerializer.Create(new JsonSerializerSettings {
                Culture = CultureInfo.InvariantCulture, TypeNameHandling = TypeNameHandling.None,
                ReferenceLoopHandling = ReferenceLoopHandling.Error }));
        }

        private static string Hash(JToken value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(Canonicalize(value).ToString(Formatting.None));
            using (SHA256 algorithm = SHA256.Create())
            {
                byte[] digest = algorithm.ComputeHash(bytes);
                var text = new StringBuilder(digest.Length * 2);
                foreach (byte part in digest) text.Append(part.ToString("x2", CultureInfo.InvariantCulture));
                return text.ToString();
            }
        }

        private static JToken Canonicalize(JToken token)
        {
            if (token is JObject obj)
            {
                var properties = new List<JProperty>(obj.Properties());
                properties.Sort((left, right) => StringComparer.Ordinal.Compare(left.Name, right.Name));
                var result = new JObject();
                foreach (JProperty property in properties) result.Add(property.Name, Canonicalize(property.Value));
                return result;
            }
            if (token is JArray array)
            {
                var result = new JArray();
                foreach (JToken item in array) result.Add(Canonicalize(item));
                return result;
            }
            return token.DeepClone();
        }
    }
}
