namespace MxFramework.Authoring
{
    public static class AuthoringExitCodes
    {
        public const int Ready = 0;
        public const int ToolError = 1;
        public const int ValidationBlocked = 2;
        public const int SchemaIncompatible = 3;
        public const int PreviewUnavailable = 4;
        public const int Warning = 5;

        public static int From(ValidationReport report)
        {
            if (report == null) return Ready;
            if (report.RequiresUpgrade) return SchemaIncompatible;
            if (report.HasErrors) return ValidationBlocked;
            return Ready;
        }
    }
}
