namespace MxFramework.Diagnostics
{
    public readonly struct FrameworkDebugSection
    {
        public FrameworkDebugSection(string title, string body)
        {
            Title = title ?? string.Empty;
            Body = body ?? string.Empty;
        }

        public string Title { get; }
        public string Body { get; }
    }
}
