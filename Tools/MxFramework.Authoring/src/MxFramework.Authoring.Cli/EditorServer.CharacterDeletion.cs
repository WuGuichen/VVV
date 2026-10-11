using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    private static void HandleCharacterDeletion(HttpListenerContext context, string rootPath, bool apply, JsonSerializerOptions options)
    {
        string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
        var request = JsonSerializer.Deserialize<CharacterDeletionRequest>(body, options) ?? new();
        JsonObject result = RunCharacterDeletion(rootPath, request, apply);
        string status = result["status"]?.GetValue<string>();
        if (apply && status is "deleted" or "partial" or "already_deleted")
            InvalidateAuthoringResourceCache(rootPath, "Authoring/CharacterPackages/character-" + request.characterId);
        // The browser needs the scope and blockers, not every file's content hash.
        result.Remove("files");
        result.Remove("root");
        WriteJson(context.Response, result, options, status == "error" || (apply && status != "deleted" && status != "already_deleted") ? 409 : 200);
    }

    internal static JsonObject RunCharacterDeletion(string rootPath, CharacterDeletionRequest request, bool apply)
    {
        if (!Regex.IsMatch(request.characterId ?? "", "^[0-9]{6}$") || request.characterId == "000000")
            throw new ArgumentException("请选择有效的六位角色编号，内置模板不能彻底删除。");
        if (apply && (request.confirmCharacterId != request.characterId || !request.editorsClosed ||
                      !Regex.IsMatch(request.planHash ?? "", "^[a-f0-9]{64}$")))
            throw new ArgumentException("请先检查删除范围，输入相同角色编号，并确认已停止编辑和发布该角色。");
        string toolsRoot = string.IsNullOrWhiteSpace(s_toolsRootPath) ? rootPath : s_toolsRootPath;
        string script = Path.Combine(toolsRoot, "Tools/MxFramework.Authoring/scripts/delete_numeric_characters.py");
        var start = new ProcessStartInfo(OperatingSystem.IsWindows() ? "python" : "python3")
        {
            WorkingDirectory = rootPath, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
        };
        start.Environment["PYTHONIOENCODING"] = "utf-8";
        foreach (string arg in new[] { script, "--root", rootPath, "--ids", request.characterId }) start.ArgumentList.Add(arg);
        if (apply)
        {
            start.ArgumentList.Add("--apply");
            start.ArgumentList.Add("--plan-hash");
            start.ArgumentList.Add(request.planHash);
        }
        using var process = Process.Start(start) ?? throw new InvalidOperationException("无法启动角色删除工具。");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        // Server requests are serialized. Do not time out an in-progress deletion
        // and allow a second lifecycle mutation to race its remaining work.
        process.WaitForExit();
        string text = output.GetAwaiter().GetResult();
        string error = errors.GetAwaiter().GetResult();
        if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("角色删除工具失败：" + error);
        return JsonNode.Parse(text)?.AsObject() ?? throw new InvalidOperationException("角色删除工具未返回检查结果。");
    }

    internal sealed class CharacterDeletionRequest
    {
        public string characterId { get; set; }
        public string confirmCharacterId { get; set; }
        public string planHash { get; set; }
        public bool editorsClosed { get; set; }
    }
}
