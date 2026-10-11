using System.Net;
using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    private static bool TryHandleCombatDesignApi(HttpListenerContext context, string projectRoot, JsonSerializerOptions options, string path)
    {
        if (!path.StartsWith("/api/combat-design/", StringComparison.Ordinal)) return false;
        try
        {
            var gateway = new CombatDesignGateway(projectRoot);
            var authoring = new CombatDesignAuthoringGateway(projectRoot);
            object result;
            if (path == "/api/combat-design/authoring" && context.Request.HttpMethod == "GET") result = authoring.Query();
            else if (path == "/api/combat-design/authoring/status" && context.Request.HttpMethod == "GET") result = authoring.Status(context.Request.QueryString["operationId"] ?? string.Empty);
            else if (path == "/api/combat-design/query" && context.Request.HttpMethod == "GET") result = gateway.Query();
            else if (path == "/api/combat-design/catalog" && context.Request.HttpMethod == "GET") result = new CombatDesignCatalogProjection(projectRoot).Query();
            // The former single-example entry now resolves to the complete component catalog.
            else if (path == "/api/combat-design/intent" && context.Request.HttpMethod == "GET") result = new CombatDesignCatalogProjection(projectRoot).Query();
            else if (path == "/api/combat-design/source" && context.Request.HttpMethod == "GET") result = new CombatDesignCatalogProjection(projectRoot).ReadSource(context.Request.QueryString["path"]);
            else if (path == "/api/combat-design/mechanisms" && context.Request.HttpMethod == "GET") result = new CombatDesignMechanismProjection(projectRoot).Query();
            else if (path == "/api/combat-design/status" && context.Request.HttpMethod == "GET")
                result = gateway.Status(context.Request.QueryString["operationId"] ?? string.Empty);
            else if ((path == "/api/combat-design/plan" || path == "/api/combat-design/submit" || path == "/api/combat-design/authoring/submit") && context.Request.HttpMethod == "POST")
            {
                string origin = context.Request.Headers["Origin"];
                if (origin != null && !string.Equals(origin, context.Request.Url!.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
                { WriteJson(context.Response, new { code = "ORIGIN_REJECTED" }, options, 403); return true; }
                if (!(context.Request.ContentType ?? "").StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
                { WriteJson(context.Response, new { code = "JSON_CONTENT_TYPE_REQUIRED" }, options, 415); return true; }
                using var reader = new StreamReader(context.Request.InputStream);
                char[] buffer = new char[1024 * 1024 + 1];
                int count = 0, read;
                while (count < buffer.Length && (read = reader.Read(buffer, count, buffer.Length - count)) > 0) count += read;
                if (count == buffer.Length) throw new ArgumentException("REQUEST_TOO_LARGE");
                string body = new string(buffer, 0, count);
                result = path == "/api/combat-design/authoring/submit" ? authoring.Submit(body)
                    : path.EndsWith("/plan", StringComparison.Ordinal) ? gateway.Plan(body) : gateway.Submit(body);
            }
            else { WriteJson(context.Response, new { code = "METHOD_OR_ROUTE_NOT_ALLOWED" }, options, 405); return true; }
            WriteJson(context.Response, result, options);
        }
        catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is IOException || error is InvalidDataException || error is JsonException)
        { WriteJson(context.Response, new { code = error.GetType().Name, message = error.Message, recovery = "query/replan; reuse the same idempotency key only for an identical command" }, options, error is InvalidOperationException ? 409 : 400); }
        return true;
    }
}
