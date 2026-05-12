using System.Text.Json;
using MxFramework.Authoring;
using MxFramework.Authoring.Preview;
using MxFramework.Authoring.Preview.Protocol;

namespace MxFramework.Authoring.Cli;

internal static class PreviewCommands
{
    public static int Dispatch(string[] args, JsonSerializerOptions jsonOptions)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("error: preview subcommand required.");
            return Program.ExitToolError;
        }

        string sub = args[1];
        return sub switch
        {
            "ping" => Ping(jsonOptions),
            "load" => Load(args, jsonOptions),
            "apply" => Apply(args, jsonOptions),
            "reset" => Reset(jsonOptions),
            "snapshot" => Snapshot(args, jsonOptions),
            "logs" => Logs(args, jsonOptions),
            _ => UnknownSub(sub)
        };
    }

    private static int UnknownSub(string sub)
    {
        Console.Error.WriteLine("error: unknown preview subcommand '" + sub + "'.");
        return Program.ExitToolError;
    }

    private static int Ping(JsonSerializerOptions jsonOptions)
    {
        return WithClient(async client =>
        {
            HandshakeResult result = await client.HandshakeAsync("MxAuthoringCli", "0.3.0");
            Console.WriteLine(JsonSerializer.Serialize(result, jsonOptions));
            return Program.ExitReady;
        });
    }

    private static int Load(string[] args, JsonSerializerOptions jsonOptions)
    {
        string packagePath = Program.RequireOption(args, "--package");
        PackageReadResult package = PackageReader.Read(packagePath);

        var patchElements = new List<JsonElement>();
        for (int i = 0; i < package.Patches.Count; i++)
        {
            string text = JsonSerializer.Serialize(package.Patches[i], jsonOptions);
            patchElements.Add(JsonDocument.Parse(text).RootElement.Clone());
        }

        var parameters = new LoadPatchParams
        {
            PackageId = package.Manifest?.PackageId ?? string.Empty,
            Kind = package.Manifest?.Kind.ToString() ?? "Preview",
            SchemaVersion = package.Manifest?.SchemaVersion ?? "1.0",
            Patches = patchElements,
            DiscardPrevious = true
        };

        return WithClient(async client =>
        {
            await client.HandshakeAsync("MxAuthoringCli", "0.3.0");
            LoadPatchResult result = await client.LoadPatchAsync(parameters);
            Console.WriteLine(JsonSerializer.Serialize(result, jsonOptions));
            return Program.ExitReady;
        });
    }

    private static int Apply(string[] args, JsonSerializerOptions jsonOptions)
    {
        string buffId = Program.RequireOption(args, "--buff");
        var parameters = new ApplyBuffParams
        {
            BuffId = buffId,
            CasterId = Program.GetOption(args, "--caster", "TestCaster"),
            TargetId = Program.GetOption(args, "--target", "TestTarget"),
            Stack = int.Parse(Program.GetOption(args, "--stack", "1")),
            WaitTicks = int.Parse(Program.GetOption(args, "--wait-ticks", "0"))
        };

        return WithClient(async client =>
        {
            await client.HandshakeAsync("MxAuthoringCli", "0.3.0");
            RuntimePreviewResult result = await client.ApplyBuffAsync(parameters);
            Console.WriteLine(JsonSerializer.Serialize(result, jsonOptions));
            return Program.ExitReady;
        });
    }

    private static int Reset(JsonSerializerOptions jsonOptions)
    {
        return WithClient(async client =>
        {
            await client.HandshakeAsync("MxAuthoringCli", "0.3.0");
            ResetResult result = await client.ResetAsync(new ResetParams());
            Console.WriteLine(JsonSerializer.Serialize(result, jsonOptions));
            return Program.ExitReady;
        });
    }

    private static int Snapshot(string[] args, JsonSerializerOptions jsonOptions)
    {
        string targetId = Program.GetOption(args, "--target", "TestTarget");
        return WithClient(async client =>
        {
            await client.HandshakeAsync("MxAuthoringCli", "0.3.0");
            RuntimePreviewResult result = await client.GetSnapshotAsync(new GetSnapshotParams { TargetId = targetId });
            Console.WriteLine(JsonSerializer.Serialize(result, jsonOptions));
            return Program.ExitReady;
        });
    }

    private static int Logs(string[] args, JsonSerializerOptions jsonOptions)
    {
        int after = int.Parse(Program.GetOption(args, "--after", "0"));
        int max = int.Parse(Program.GetOption(args, "--max", "200"));
        return WithClient(async client =>
        {
            await client.HandshakeAsync("MxAuthoringCli", "0.3.0");
            GetLogsResult result = await client.GetLogsAsync(new GetLogsParams { AfterSeq = after, Max = max });
            Console.WriteLine(JsonSerializer.Serialize(result, jsonOptions));
            return Program.ExitReady;
        });
    }

    private static int WithClient(Func<WebSocketPreviewClient, Task<int>> body)
    {
        PreviewConnectionDescriptor descriptor = PreviewConnectionLocator.TryRead();
        if (descriptor == null || string.IsNullOrEmpty(descriptor.Endpoint))
        {
            Console.Error.WriteLine("error: preview unavailable - no live preview server descriptor.");
            return Program.ExitPreviewUnavailable;
        }

        var endpoint = new Uri(descriptor.Endpoint);
        var client = new WebSocketPreviewClient(endpoint, descriptor.Token);
        try
        {
            return body(client).GetAwaiter().GetResult();
        }
        catch (PreviewNotHandshakedException ex)
        {
            Console.Error.WriteLine("error: preview unavailable - " + ex.Message);
            return Program.ExitPreviewUnavailable;
        }
        catch (PreviewTokenMismatchException ex)
        {
            Console.Error.WriteLine("error: preview unavailable - token mismatch: " + ex.Message);
            return Program.ExitPreviewUnavailable;
        }
        catch (PreviewConnectionException ex)
        {
            Console.Error.WriteLine("error: preview unavailable - connection failed: " + ex.Message);
            return Program.ExitPreviewUnavailable;
        }
        catch (PreviewProtocolException ex)
        {
            Console.Error.WriteLine($"error: preview rpc failed (code {ex.ErrorCode}): {ex.Message}");
            return Program.ExitToolError;
        }
        finally
        {
            try { client.Dispose(); } catch { }
        }
    }
}
