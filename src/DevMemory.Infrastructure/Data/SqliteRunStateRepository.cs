using System.Text.Json;
using Dapper;
using DevMemory.Core.Entities;
using DevMemory.Core.Interfaces;

namespace DevMemory.Infrastructure.Data;

public sealed class SqliteRunStateRepository : IRunStateRepository
{
    private readonly SqliteContext _context;

    public SqliteRunStateRepository(SqliteContext context) => _context = context;

    public async Task<RunState> SaveCheckpointAsync(
        string runId,
        string projectName,
        string goal,
        string currentPhase,
        Dictionary<string, bool> approvals,
        string? lastHandoffSummary = null,
        string[]? openRisks = null,
        string mode = "safe",
        CancellationToken cancellationToken = default)
    {
        var updatedAt    = DateTime.UtcNow;
        var approvalsJson = JsonSerializer.Serialize(approvals);
        var risksJson    = JsonSerializer.Serialize(openRisks ?? []);

        const string sql = """
            INSERT INTO run_states
                (run_id, project_name, goal, current_phase, approvals, last_handoff_summary, open_risks, mode, updated_at)
            VALUES
                (@RunId, @ProjectName, @Goal, @CurrentPhase, @Approvals, @LastHandoffSummary, @OpenRisks, @Mode, @UpdatedAt)
            ON CONFLICT(run_id) DO UPDATE SET
                project_name         = excluded.project_name,
                goal                 = excluded.goal,
                current_phase        = excluded.current_phase,
                approvals            = excluded.approvals,
                last_handoff_summary = excluded.last_handoff_summary,
                open_risks           = excluded.open_risks,
                mode                 = excluded.mode,
                updated_at           = excluded.updated_at
            """;

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(sql, new
        {
            RunId               = runId,
            ProjectName         = projectName,
            Goal                = goal,
            CurrentPhase        = currentPhase,
            Approvals           = approvalsJson,
            LastHandoffSummary  = lastHandoffSummary,
            OpenRisks           = risksJson,
            Mode                = mode,
            UpdatedAt           = updatedAt.ToString("O"),
        });

        return new RunState
        {
            RunId              = runId,
            ProjectName        = projectName,
            Goal               = goal,
            CurrentPhase       = currentPhase,
            Approvals          = approvals,
            LastHandoffSummary = lastHandoffSummary,
            OpenRisks          = openRisks ?? [],
            Mode               = mode,
            UpdatedAt          = updatedAt,
        };
    }

    public async Task<RunState?> GetRunStateAsync(
        string runId,
        CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM run_states WHERE run_id = @RunId";

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var row = await connection.QueryFirstOrDefaultAsync<RunStateRow>(sql, new { RunId = runId });
        return row?.ToEntity();
    }

    public async Task<IEnumerable<RunState>> ListActiveRunsAsync(
        string projectName,
        CancellationToken cancellationToken = default)
    {
        // "Active" = current_phase is not 'done' or 'completed'
        const string sql = """
            SELECT * FROM run_states
            WHERE project_name = @ProjectName
              AND current_phase NOT IN ('done', 'completed', 'cancelled')
            ORDER BY updated_at DESC
            LIMIT 20
            """;

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<RunStateRow>(sql, new { ProjectName = projectName });
        return rows.Select(r => r.ToEntity());
    }

    // ── internal DTOs ────────────────────────────────────────────────────────

    private sealed class RunStateRow
    {
        public string  Run_Id               { get; set; } = string.Empty;
        public string  Project_Name         { get; set; } = string.Empty;
        public string  Goal                 { get; set; } = string.Empty;
        public string  Current_Phase        { get; set; } = string.Empty;
        public string  Approvals            { get; set; } = "{}";
        public string? Last_Handoff_Summary { get; set; }
        public string  Open_Risks           { get; set; } = "[]";
        public string  Mode                 { get; set; } = "safe";
        public string  Updated_At           { get; set; } = string.Empty;

        public RunState ToEntity() => new()
        {
            RunId              = Run_Id,
            ProjectName        = Project_Name,
            Goal               = Goal,
            CurrentPhase       = Current_Phase,
            Approvals          = JsonSerializer.Deserialize<Dictionary<string, bool>>(Approvals) ?? new(),
            LastHandoffSummary = Last_Handoff_Summary,
            OpenRisks          = JsonSerializer.Deserialize<string[]>(Open_Risks) ?? [],
            Mode               = Mode,
            UpdatedAt          = DateTime.Parse(Updated_At, null,
                System.Globalization.DateTimeStyles.RoundtripKind),
        };
    }
}
