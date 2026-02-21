using System.Text.Json;
using DevMemory.Core.Interfaces;
using DevMemory.Core.Utilities;
using DevMemory.Mcp.Models;

namespace DevMemory.Mcp.Tools;

/// <summary>
/// mem_save_prompt — store a user prompt for future context retrieval.
/// Useful for saving complex prompts that took effort to craft.
/// </summary>
public sealed class MemSavePromptTool : IMcpTool
{
    private readonly IMemoryRepository  _memory;
    private readonly ISessionRepository _sessions;

    public MemSavePromptTool(IMemoryRepository memory, ISessionRepository sessions)
    {
        _memory   = memory;
        _sessions = sessions;
    }

    public string Name        => "mem_save_prompt";
    public string Description => "Save a user prompt for future reference. Useful for complex prompts worth reusing.";
    public object InputSchema => new
    {
        type       = "object",
        properties = new
        {
            content    = new { type = "string", description = "The prompt content to save" },
            project    = new { type = "string", description = "Project to associate with (optional)" },
            session_id = new { type = "string", description = "Specific session ID (optional)" },
        },
        required = new[] { "content" },
    };

    public async Task<McpResponse> ExecuteAsync(
        object? requestId, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        if (!arguments.TryGetProperty("content", out var cEl) || cEl.GetString() is not { Length: > 0 } content)
            return McpResponse.InvalidParams(requestId, "'content' is required");

        content = PrivacyHelper.StripPrivateTags(content);

        var project = arguments.TryGetProperty("project", out var pEl) ? pEl.GetString() : null;

        Guid sessionId;

        if (arguments.TryGetProperty("session_id", out var idEl) &&
            Guid.TryParse(idEl.GetString(), out var explicitId))
        {
            sessionId = explicitId;
        }
        else
        {
            var session = await _sessions.GetActiveSessionAsync(project, cancellationToken)
                       ?? await _sessions.CreateSessionAsync(project, cancellationToken: cancellationToken);
            sessionId = session.Id;
        }

        await _memory.SavePromptAsync(sessionId, content, cancellationToken);

        return McpResponse.ToolSuccess(requestId, "✓ Prompt saved.");
    }
}
