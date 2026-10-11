using System;
using System.Collections.Generic;
using MxFramework.Animation;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestPreparedAnimationBinding
    {
        internal CharacterTestPreparedAnimationBinding(MxAnimationActionBinding binding, int authorityBindingId)
        { Binding = binding; AuthorityBindingId = authorityBindingId; }
        internal MxAnimationActionBinding Binding { get; }
        internal int BindingId => Binding?.BindingId ?? 0;
        internal MxFramework.Resources.ResourceKey Clip => Binding?.Clip ?? default;
        internal int AuthorityBindingId { get; }
    }
    /// <summary>Immutable per-definition animation map prepared before entity simulation.</summary>
    internal sealed class CharacterTestPreparedAnimationBindings
    {
        private readonly Dictionary<(int Action, int Event), CharacterTestPreparedAnimationBinding> _events =
            new Dictionary<(int, int), CharacterTestPreparedAnimationBinding>();
        private readonly Dictionary<int, MxAnimationActionBinding> _byId = new Dictionary<int, MxAnimationActionBinding>();
        private readonly HashSet<int> _authorityBindings = new HashSet<int>();
        internal bool HasProfile { get; private set; }

        internal static CharacterTestPreparedAnimationBindings Prepare(CharacterRuntimeAnimationBinding profile, IReadOnlyList<CharacterActionConfig> actions, ISet<CharacterActionId> requiredActions = null)
        {
            var result = new CharacterTestPreparedAnimationBindings();
            if (!profile.IsValid) return result;
            if (!CharacterTestCharacterAnimationResources.TryPrepareLocomotionDefinition(profile, out CharacterRuntimeLocomotionDefinition definition))
                throw new InvalidOperationException("The character animation profile could not be prepared.");
            result.HasProfile = true;
            var byAction = new Dictionary<string, MxAnimationActionBinding>(StringComparer.Ordinal);
            var directClips = new Dictionary<string, MxAnimationActionBinding>(StringComparer.Ordinal);
            foreach (MxAnimationActionBinding binding in definition.AnimationSet.Actions)
            {
                if (binding.BindingId <= 0) throw new InvalidOperationException("Animation binding identity is missing.");
                result._byId.Add(binding.BindingId, binding);
                result._authorityBindings.Add(binding.BindingId);
                if (!string.IsNullOrEmpty(binding.ActionKey) && !byAction.ContainsKey(binding.ActionKey)) byAction.Add(binding.ActionKey, binding);
            }
            foreach (MxAnimationActionBinding binding in definition.AnimationSet.PreparedClipBindings)
            {
                result._byId.Add(binding.BindingId, binding);
                directClips.Add(binding.Clip.Id, binding);
            }
            for (int i = 0; actions != null && i < actions.Count; i++)
            {
                CharacterActionConfig action = actions[i];
                if (action == null) continue;
                foreach (AnimationTrackEvent evt in action.AnimationTrack.Events)
                {
                    string key = evt.EffectiveAnimationKey;
                    if (string.IsNullOrEmpty(key)) continue;
                    if (byAction.TryGetValue(key, out MxAnimationActionBinding binding))
                        result._events.Add((action.ActionId.Value, evt.StableEventId), new CharacterTestPreparedAnimationBinding(binding, binding.BindingId));
                    else if (directClips.TryGetValue(key, out binding))
                        result._events.Add((action.ActionId.Value, evt.StableEventId), new CharacterTestPreparedAnimationBinding(binding, 0));
                    else if (requiredActions == null || requiredActions.Contains(action.ActionId))
                        throw new InvalidOperationException("Animation event has no authored binding identity. Regenerate character content before creating the session.");
                }
            }
            return result;
        }

        internal bool TryGet(CharacterActionId actionId, int eventId, out CharacterTestPreparedAnimationBinding binding) =>
            _events.TryGetValue((actionId.Value, eventId), out binding);
        internal bool TryGetById(int bindingId, out MxAnimationActionBinding binding) => _byId.TryGetValue(bindingId, out binding);
        internal int AuthorityBindingFor(int bindingId) => _authorityBindings.Contains(bindingId) ? bindingId : 0;
    }
}
