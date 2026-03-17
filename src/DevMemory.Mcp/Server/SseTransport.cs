using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace DevMemory.Mcp.Server;

/// <summary>
/// MCP transport over HTTP + Server-Sent Events (spec 2024-11-05).
///
/// Protocol flow per session (Legacy SSE):
///   1. Client opens  GET /sse          → receives "endpoint" event with POST URL
///   2. Client sends  POST /message     → server processes JSON-RPC, ACKs 202
///   3. Server pushes "message" event on the SSE stream with the JSON-RPC response
///
/// Streamable HTTP (newer clients):
///   1. Client sends  POST /sse         → server processes JSON-RPC
///   2. Response streamed back via SSE or returned directly
///
/// Each SSE connection is a session. Sessions are tracked in-memory; they are
/// removed when the client disconnects or the server shuts down.
///
/// Heartbeat pings are sent every 30 seconds to keep connections alive through
/// proxies and load balancers.
///
/// Activated when DEVMEMORY_TRANSPORT=http. Port is DEVMEMORY_PORT (default 8080).
/// </summary>
public sealed class SseTransport : ITransport
{
    private readonly int _port;
    private readonly TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(30);
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
            var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
            {
                SingleReader = true,
            });

            _sessions[sessionId] = channel;

            ctx.Response.ContentType = "text/event-stream";
            ctx.Response.Headers.CacheControl = "no-cache";
            ctx.Response.Headers["X-Accel-Buffering"] = "no";

            // Tell the client where to POST messages for this session
            await ctx.Response.WriteAsync($"event: endpoint\ndata: /message?sessionId={sessionId}\n\n", cancellationToken);
            await ctx.Response.Body.FlushAsync(cancellationToken);

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, ctx.RequestAborted);

            try
            {
                await RunSseLoopWithHeartbeatAsync(ctx.Response, channel.Reader, linked.Token);
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

        // ── POST /sse — Streamable HTTP (MCP 2025+) ────────────────────────────
        // Newer MCP clients may POST directly to /sse. We handle the request and
        // return the response inline (no persistent SSE stream needed).
        app.MapPost("/sse", async (HttpContext ctx) =>
        {
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
            {
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync(response, cancellationToken);
            }
            else
            {
                ctx.Response.StatusCode = 204;
            }
        });

        // ── GET /health — liveness probe for container orchestrators ──────────
        app.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
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

    /// <summary>
    /// Reads messages from the channel and sends them as SSE events.
    /// Also sends heartbeat pings every 30 seconds to keep the connection alive
    /// through proxies and load balancers that may close idle connections.
    /// </summary>
    private async Task RunSseLoopWithHeartbeatAsync(
        HttpResponse response,
        ChannelReader<string> reader,
        CancellationToken cancellationToken)
    {
        using var heartbeatTimer = new PeriodicTimer(_heartbeatInterval);

        var readTask = reader.WaitToReadAsync(cancellationToken).AsTask();
        var heartbeatTask = heartbeatTimer.WaitForNextTickAsync(cancellationToken).AsTask();

        while (!cancellationToken.IsCancellationRequested)
        {
            var completedTask = await Task.WhenAny(readTask, heartbeatTask);

            if (completedTask == heartbeatTask)
            {
                // Send SSE comment as heartbeat (clients ignore comments)
                await response.WriteAsync(": ping\n\n", cancellationToken);
                await response.Body.FlushAsync(cancellationToken);

                // Reset heartbeat timer task
                heartbeatTask = heartbeatTimer.WaitForNextTickAsync(cancellationToken).AsTask();
            }
            else
            {
                // Channel has data or completed
                if (!await readTask)
                {
                    // Channel completed, exit loop
                    break;
                }

                // Read all available messages
                while (reader.TryRead(out var message))
                {
                    await response.WriteAsync($"event: message\ndata: {message}\n\n", cancellationToken);
                }
                await response.Body.FlushAsync(cancellationToken);

                // Reset read task
                readTask = reader.WaitToReadAsync(cancellationToken).AsTask();
            }
        }
    }
}
