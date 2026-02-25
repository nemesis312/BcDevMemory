using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace DevMemory.Mcp.Server;

/// <summary>
/// MCP transport over HTTP + Server-Sent Events (spec 2024-11-05).
///
/// Protocol flow per session:
///   1. Client opens  GET /sse          → receives "endpoint" event with POST URL
///   2. Client sends  POST /message     → server processes JSON-RPC, ACKs 202
///   3. Server pushes "message" event on the SSE stream with the JSON-RPC response
///
/// Each SSE connection is a session. Sessions are tracked in-memory; they are
/// removed when the client disconnects or the server shuts down.
///
/// Activated when DEVMEMORY_TRANSPORT=http. Port is DEVMEMORY_PORT (default 8080).
/// </summary>
public sealed class SseTransport : ITransport
{
    private readonly int _port;
    private readonly ConcurrentDictionary<string, Channel<string>> _sessions = new();

    public SseTransport(int port = 8080) => _port = port;

    public async Task RunAsync(JsonRpcHandler handler, CancellationToken cancellationToken = default)
    {
        var builder = WebApplication.CreateBuilder(Array.Empty<string>());
        builder.WebHost.UseUrls($"http://0.0.0.0:{_port}");
        builder.Logging.ClearProviders();

        var app = builder.Build();

        // ── GET /sse — open SSE stream ─────────────────────────────────────────
        app.MapGet("/sse", async (HttpContext ctx) =>
        {
            var sessionId = Guid.NewGuid().ToString("N");
            var channel   = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
            {
                SingleReader = true,
            });

            _sessions[sessionId] = channel;

            ctx.Response.ContentType             = "text/event-stream";
            ctx.Response.Headers.CacheControl    = "no-cache";
            ctx.Response.Headers["X-Accel-Buffering"] = "no";

            // Tell the client where to POST messages for this session
            await ctx.Response.WriteAsync($"event: endpoint\ndata: /message?sessionId={sessionId}\n\n", cancellationToken);
            await ctx.Response.Body.FlushAsync(cancellationToken);

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, ctx.RequestAborted);

            try
            {
                await foreach (var message in channel.Reader.ReadAllAsync(linked.Token))
                {
                    await ctx.Response.WriteAsync($"event: message\ndata: {message}\n\n", linked.Token);
                    await ctx.Response.Body.FlushAsync(linked.Token);
                }
            }
            catch (OperationCanceledException) { /* client disconnected or server shutting down */ }
            finally
            {
                _sessions.TryRemove(sessionId, out _);
            }
        });

        // ── POST /message — receive JSON-RPC from client ───────────────────────
        app.MapPost("/message", async (HttpContext ctx) =>
        {
            if (!ctx.Request.Query.TryGetValue("sessionId", out var sid) ||
                sid.FirstOrDefault() is not { } sessionId)
            {
                ctx.Response.StatusCode = 400;
                return;
            }

            if (!_sessions.TryGetValue(sessionId, out var channel))
            {
                ctx.Response.StatusCode = 404;
                return;
            }

            using var reader = new StreamReader(ctx.Request.Body);
            var json = await reader.ReadToEndAsync(cancellationToken);

            string? response;
            try
            {
                response = await handler.HandleAsync(json, cancellationToken);
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"[devmemory] Unhandled: {ex.Message}");
                response = handler.SerializeInternalError(null, ex.Message);
            }

            if (response is not null)
                await channel.Writer.WriteAsync(response, cancellationToken);

            ctx.Response.StatusCode = 202;
        });

        // ── GET /health — liveness probe for container orchestrators ──────────
        app.MapGet("/health", () => Results.Ok(new
        {
            status  = "ok",
            version = McpConstants.ServerVersion,
        }));

        // Close all open SSE streams on shutdown
        cancellationToken.Register(() =>
        {
            foreach (var (_, ch) in _sessions)
                ch.Writer.TryComplete();
        });

        await Console.Error.WriteLineAsync($"[devmemory] SSE transport listening on http://0.0.0.0:{_port}");
        await app.RunAsync(cancellationToken);
    }
}
