using System;
using System.Collections.Generic;
using MxFramework.Story.Runtime;

namespace MxFramework.Story.Editor
{
    public sealed class StoryEditorDebugTarget
    {
        public StoryEditorDebugTarget(
            string name,
            StoryRuntimeModule module,
            Func<IReadOnlyList<StoryRuntimeEvent>> recentEventsProvider = null)
        {
            Name = string.IsNullOrWhiteSpace(name) ? StoryRuntimeDebugSource.DefaultSourceName : name;
            Module = module;
            RecentEventsProvider = recentEventsProvider;
        }

        public string Name { get; }
        public StoryRuntimeModule Module { get; }
        public Func<IReadOnlyList<StoryRuntimeEvent>> RecentEventsProvider { get; }
        public bool IsAvailable => Module != null;

        public StoryRuntimeDebugSource CreateSource()
        {
            return new StoryRuntimeDebugSource(Module, Name, RecentEventsProvider);
        }
    }

    public static class StoryEditorDebugRegistry
    {
        private static readonly List<StoryEditorDebugTarget> TargetsInternal = new List<StoryEditorDebugTarget>();

        public static IReadOnlyList<StoryEditorDebugTarget> Targets => TargetsInternal;

        public static StoryEditorDebugTarget Register(
            string name,
            StoryRuntimeModule module,
            Func<IReadOnlyList<StoryRuntimeEvent>> recentEventsProvider = null)
        {
            var target = new StoryEditorDebugTarget(name, module, recentEventsProvider);
            for (int i = 0; i < TargetsInternal.Count; i++)
            {
                if (string.Equals(TargetsInternal[i].Name, target.Name, StringComparison.Ordinal))
                {
                    TargetsInternal[i] = target;
                    return target;
                }
            }

            TargetsInternal.Add(target);
            return target;
        }

        public static bool Unregister(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            for (int i = 0; i < TargetsInternal.Count; i++)
            {
                if (string.Equals(TargetsInternal[i].Name, name, StringComparison.Ordinal))
                {
                    TargetsInternal.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        public static void Clear()
        {
            TargetsInternal.Clear();
        }
    }
}
