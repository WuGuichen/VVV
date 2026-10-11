using System.Net;
using System.Text.Json;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    private static bool TryHandleTacticalCardQueryApi(HttpListenerContext context, string root, JsonSerializerOptions json, string path)
    {
        if (!new[] { "/api/tactical-card/query", "/api/tactical-card/catalog", "/api/tactical-card/preview", "/api/tactical-card/source" }.Contains(path)) return false;
        if (context.Request.HttpMethod != "GET") { WriteJson(context.Response, new { status = "read-only", message = "GET only" }, json, 405); return true; }
        try
        {
            var q = context.Request.QueryString;
            if (path == "/api/tactical-card/source") { WriteJson(context.Response, TacticalCardDirectoryCommands.Source(root, q["key"]), json); return true; }
            if (path == "/api/tactical-card/catalog") { WriteJson(context.Response, TacticalCardDirectoryCommands.Catalog(root, q["locale"] ?? "zh-CN"), json); return true; }
            if (path == "/api/tactical-card/preview") { WriteJson(context.Response, TacticalCardDirectoryCommands.Preview(root, q["key"], q["locale"] ?? "zh-CN", q["role"], q["bindings"], q["sourceFingerprint"]), json); return true; }
            var options = new MxFramework.Authoring.TacticalCardQueryOptions { Name = q["name"], Contains = q["contains"], Id = q["id"],
                InstanceId = q["instanceId"], Locale = q["locale"] ?? "zh-CN", Layer = q["layer"] ?? "generated" };
            WriteJson(context.Response, TacticalCardQueryCommands.Query(root, options), json);
        }
        catch (Exception error) { WriteJson(context.Response, new { status = "error", message = (error.InnerException ?? error).Message }, json, 400); }
        return true;
    }
}
