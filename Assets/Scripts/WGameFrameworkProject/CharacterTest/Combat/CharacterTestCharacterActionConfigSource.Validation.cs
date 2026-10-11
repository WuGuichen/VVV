using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.CharacterAction;
using Newtonsoft.Json.Linq;

namespace MxFramework.Game.CharacterTest
{
    public static partial class CharacterTestCharacterActionConfigSource
    {
        private sealed class CharacterTestAnimationActionKeyProvider : ICharacterActionAnimationActionKeyProvider
        {
            private static readonly HashSet<string> KnownKeys = new HashSet<string>(StringComparer.Ordinal)
            {
                JumpAnimationActionKey,
                JumpAnimationClipResourceKey,
                LightLandingAnimationActionKey,
                HeavyLandingAnimationActionKey,
                LandingAnimationClipResourceKey,
                LightAttackAnimationActionKey,
                LightAttackAnimationClipResourceKey,
                CohesionBreakReactionAnimationActionKey,
                CohesionBreakReactionAnimationClipResourceKey,
                HitHighReactionAnimationActionKey,
                HitHighReactionAnimationClipResourceKey,
                HitMidReactionAnimationActionKey,
                HitMidReactionAnimationClipResourceKey,
                HitLowReactionAnimationActionKey,
                HitLowReactionAnimationClipResourceKey,
                HitHighStrongReactionAnimationActionKey,
                HitHighStrongReactionAnimationClipResourceKey,
                HitMidStrongReactionAnimationActionKey,
                HitMidStrongReactionAnimationClipResourceKey,
                HitLowStrongReactionAnimationActionKey,
                HitLowStrongReactionAnimationClipResourceKey,
                DeathReactionAnimationActionKey,
                DeathReactionAnimationClipResourceKey,
                "art.character.skeleton.animation.katana_atk2",
                "art.character.skeleton.animation.katana_atk3",
                "art.character.combat_girl_sword.animation.s_attack1",
                "art.character.combat_girl_sword.animation.s_attack2",
                "art.character.combat_girl_sword.animation.s_attack3",
                "art.character.combat_girl_sword.animation.s_evade",
                "art.character.combat_girl_sword.animation.s_die",
                "art.character.combat_girl_sword.animation.s_hit_l",
                "art.character.combat_girl_sword.animation.s_hit_r",
                "art.character.combat_girl_sword.animation.s_stun"
            };

            private readonly HashSet<string> _registeredKeys;

            public CharacterTestAnimationActionKeyProvider()
            {
                _registeredKeys = new HashSet<string>(KnownKeys, StringComparer.Ordinal);
                AddGeneratedAnimationClipRegistryKeys(
                    "Assets/MxFrameworkGenerated/CharacterPackages");
                AddGeneratedAnimationClipRegistryKeys(
                    "Assets/StreamingAssets/MxFramework/CharacterPackages");
            }

            public bool ContainsAnimationActionKey(
                string actionSetStableId,
                CharacterActionId actionId,
                string animationActionKey)
            {
                return _registeredKeys.Contains(animationActionKey ?? string.Empty);
            }

            private void AddGeneratedAnimationClipRegistryKeys(string root)
            {
                if (!Directory.Exists(root))
                    return;

                string[] paths = Directory.GetFiles(
                    root,
                    "animation_clip_registry.json",
                    SearchOption.AllDirectories);
                Array.Sort(paths, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < paths.Length; i++)
                {
                    try
                    {
                        JObject document = JObject.Parse(File.ReadAllText(paths[i]));
                        if (!(document["clips"] is JArray clips))
                            continue;

                        for (int clipIndex = 0; clipIndex < clips.Count; clipIndex++)
                        {
                            string runtimeResourceKey =
                                (string)(clips[clipIndex] as JObject)?["runtimeResourceKey"];
                            if (!string.IsNullOrWhiteSpace(runtimeResourceKey))
                                _registeredKeys.Add(runtimeResourceKey);
                        }
                    }
                    catch (Exception ex) when (
                        ex is IOException
                        || ex is UnauthorizedAccessException
                        || ex is Newtonsoft.Json.JsonException)
                    {
                        // Invalid or inaccessible generated registries remain fail-closed: their keys are not registered.
                    }
                }
            }
        }
    }
}
