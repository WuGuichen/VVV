using System;

namespace MxFramework.Authoring
{
    /// <summary>
    /// 源契约校验失败异常。承载门禁码与精确的定位信息，
    /// 使调用方能够在不解析消息字符串的前提下产出结构化诊断。
    /// </summary>
    /// <remarks>
    /// 规范 v1.4 第 3.2 节参考实现中该类型名为 <c>DiagnosticException</c>；
    /// 此处命名为 <see cref="CharacterDiagnosticException"/>，以避免在共享命名空间
    /// <c>MxFramework.Authoring</c> 中引入过于泛化的类型名。
    /// </remarks>
    public sealed class CharacterDiagnosticException : Exception
    {
        public CharacterDiagnosticException(
            string sourceObjectPath,
            string message,
            string gateCode = "",
            string sourcePath = "",
            string field = "")
            : base(message)
        {
            SourceObjectPath = sourceObjectPath ?? string.Empty;
            GateCode = gateCode ?? string.Empty;
            SourcePath = sourcePath ?? string.Empty;
            Field = field ?? string.Empty;
        }

        /// <summary>触发失败的源对象路径，例如 <c>colliders[3].localPose.scale</c>。</summary>
        public string SourceObjectPath { get; }

        /// <summary>门禁代码；为空表示尚未登记到 <see cref="CharacterGateCodes"/>。</summary>
        public string GateCode { get; }

        /// <summary>承载该对象的源文件相对路径。</summary>
        public string SourcePath { get; }

        /// <summary>触发失败的字段名。</summary>
        public string Field { get; }

        /// <summary>
        /// 转换为既有诊断通道可消费的问题记录。门禁码未登记时回退为
        /// <see cref="CharacterAuthoringValidationGate.ExportBlocked"/> + Error。
        /// </summary>
        public CharacterAuthoringValidationIssue ToValidationIssue()
        {
            if (CharacterGateCodes.TryResolve(GateCode, out CharacterGateDefinition definition))
            {
                return CharacterGateCodes.CreateIssue(
                    GateCode,
                    SourcePath,
                    SourceObjectPath,
                    Field,
                    Message);
            }

            return new CharacterAuthoringValidationIssue
            {
                Severity = CharacterAuthoringValidationSeverity.Error,
                Gate = CharacterAuthoringValidationGate.ExportBlocked,
                Code = GateCode,
                SourcePath = SourcePath,
                SourceObjectPath = SourceObjectPath,
                Field = Field,
                Message = Message
            };
        }
    }
}
