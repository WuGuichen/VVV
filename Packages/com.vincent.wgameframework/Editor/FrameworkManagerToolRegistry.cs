using System;
using System.Collections.Generic;

namespace MxFramework.Editor
{
    public static class FrameworkManagerToolRegistry
    {
        private static readonly List<FrameworkManagerToolInfo> Tools = new List<FrameworkManagerToolInfo>();

        public static void Register(FrameworkManagerToolInfo tool)
        {
            if (string.IsNullOrWhiteSpace(tool.Id))
                throw new ArgumentException("Framework Manager tool id cannot be empty.", nameof(tool));

            for (int i = 0; i < Tools.Count; i++)
            {
                if (string.Equals(Tools[i].Id, tool.Id, StringComparison.Ordinal))
                {
                    Tools[i] = tool;
                    return;
                }
            }

            Tools.Add(tool);
        }

        public static IReadOnlyList<FrameworkManagerToolInfo> GetTools()
        {
            var tools = new List<FrameworkManagerToolInfo>(Tools);
            tools.Sort(CompareTools);
            return tools;
        }

        public static void Clear()
        {
            Tools.Clear();
        }

        private static int CompareTools(FrameworkManagerToolInfo left, FrameworkManagerToolInfo right)
        {
            int group = string.Compare(left.Group, right.Group, StringComparison.Ordinal);
            if (group != 0)
                return group;

            int order = left.SortOrder.CompareTo(right.SortOrder);
            if (order != 0)
                return order;

            return string.Compare(left.DisplayName, right.DisplayName, StringComparison.Ordinal);
        }
    }
}
