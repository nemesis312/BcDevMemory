namespace DevMemory.Mcp.Server;

/// <summary>
/// Reads newline-delimited JSON from stdin, writes responses to stdout.
/// All diagnostic output MUST use Console.Error (stderr) — stdout is protocol-only.
/// </summary>
public sealed class StdioTransport
{
    public async Task RunAsync(JsonRpcHandler handler, CancellationToken cancellationToken = default)
    {
        // BOM-less UTF-8 — MCP clients reject messages that start with the BOM preamble.
        var utf8NoBom = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        Console.InputEncoding  = utf8NoBom;
        Console.OutputEncoding = utf8NoBom;

        using var reader = new StreamReader(Console.OpenStandardInput(),  utf8NoBom);
        await using var writer = new StreamWriter(Console.OpenStandardOutput(), utf8NoBom)
        {
            AutoFlush = false,
            NewLine   = "\n",
        };

        string? line;
        while (!cancellationToken.IsCancellationRequested &&
               (line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            string? response;
            try
            {
                response = await handler.HandleAsync(line, cancellationToken);
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"[devmemory] Unhandled: {ex.Message}");
                // Don't send error responses with id:null — MCP clients reject null ids.
                response = null;
            }

            if (response is not null)
            {
                await writer.WriteLineAsync(response);
                await writer.FlushAsync(cancellationToken);
            }
        }
    }
}
