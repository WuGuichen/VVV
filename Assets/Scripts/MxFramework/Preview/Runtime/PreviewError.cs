namespace MxFramework.Preview
{
    public static class PreviewError
    {
        public const int InvalidRequest = -32600;
        public const int UnknownMethod = -32601;
        public const int InvalidParams = -32602;
        public const int InternalError = -32603;

        public const int NotHandshaked = 1001;
        public const int TokenMismatch = 1002;

        public const int PatchParseFailed = 2001;
        public const int PatchLoadRejected = 2002;
        public const int ApplyBuffFailed = 2003;
        public const int NotInPreviewMode = 2004;
    }
}
