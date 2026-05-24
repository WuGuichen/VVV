using System;

namespace MxFramework.Editor
{
    public readonly struct FrameworkManagerToolInfo
    {
        private readonly Action _openAction;

        public FrameworkManagerToolInfo(
            string id,
            string displayName,
            string group,
            string description,
            string status,
            string menuPath,
            int sortOrder,
            Action openAction = null)
        {
            Id = id ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Group = group ?? string.Empty;
            Description = description ?? string.Empty;
            Status = status ?? string.Empty;
            MenuPath = menuPath ?? string.Empty;
            SortOrder = sortOrder;
            _openAction = openAction;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string Group { get; }
        public string Description { get; }
        public string Status { get; }
        public string MenuPath { get; }
        public int SortOrder { get; }
        public bool HasOpenAction => _openAction != null;

        public void Open()
        {
            _openAction?.Invoke();
        }
    }
}
