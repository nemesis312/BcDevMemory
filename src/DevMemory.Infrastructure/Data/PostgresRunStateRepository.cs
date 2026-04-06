using System.Text.Json;
using Dapper;
using DevMemory.Core.Entities;
using DevMemory.Core.Interfaces;
using Npgsql;
using NpgsqlTypes;

namespace DevMemory.Infrastructure.Data;

public sealed class PostgresRunStateRepository : IRunStateRepository
{
    private readonly DapperContext _context;

    public PostgresRunStateRepository(DapperContext context) => _context = context;

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
        const string sql =
            "SELECT sp_upsert_run_state(@p_run_id, @p_project_name, @p_goal, @p_current_phase, " +
            "@p_approvals, @p_last_handoff_summary, @p_open_risks, @p_mode)";

        var approvalsJson = JsonSerializer.Serialize(approvals);
        var risks         = openRisks ?? [];

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var parameters = new UpsertRunStateParams(
            runId, projectName, goal, currentPhase,
            approvalsJson, lastHandoffSummary, risks, mode);

        await connection.ExecuteAsync(sql, parameters);

        return new RunState
        {
            RunId               = runId,
            ProjectName         = projectName,
            Goal                = goal,
            CurrentPhase        = currentPhase,
            Approvals           = approvals,
            LastHandoffSummary  = lastHandoffSummary,
            OpenRisks           = risks,
            Mode                = mode,
            UpdatedAt           = DateTime.UtcNow,
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
        const string sql = """
            SELECT * FROM run_states
            WHERE project_name = @ProjectName
              AND current_phase NOT IN ('done', 'completed', 'cancelled')
            ORDER BY updated_at DESC
            """;

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<RunStateRow>(sql, new { ProjectName = projectName });
        return rows.Select(r => r.ToEntity());
    }

    // ── IDynamicParameters for JSONB + TEXT[] ────────────────────────────

    private sealed class UpsertRunStateParams : SqlMapper.IDynamicParameters
    {
        private readonly string   _runId;
        private readonly string   _projectName;
        private readonly string   _goal;
        private readonly string   _currentPhase;
        private readonly string   _approvalsJson;
        private readonly string?  _lastHandoffSummary;
        private readonly string[] _openRisks;
        private readonly string   _mode;

        public UpsertRunStateParams(
            string runId, string projectName, string goal, string currentPhase,
            string approvalsJson, string? lastHandoffSummary, string[] openRisks, string mode)
        {
            _runId               = runId;
            _projectName         = projectName;
            _goal                = goal;
            _currentPhase        = currentPhase;
            _approvalsJson       = approvalsJson;
            _lastHandoffSummary  = lastHandoffSummary;
            _openRisks           = openRisks;
            _mode                = mode;
        }

        public void AddParameters(System.Data.IDbCommand command, SqlMapper.Identity identity)
        {
            var cmd = (NpgsqlCommand)command;
            cmd.Parameters.AddWithValue("p_run_id",               _runId);
            cmd.Parameters.AddWithValue("p_project_name",         _projectName);
            cmd.Parameters.AddWithValue("p_goal",                 _goal);
            cmd.Parameters.AddWithValue("p_current_phase",        _currentPhase);
            cmd.Parameters.Add(new NpgsqlParameter("p_approvals", NpgsqlDbType.Jsonb)
            {
                Value = _approvalsJson
            });
            cmd.Parameters.AddWithValue("p_last_handoff_summary", (object?)_lastHandoffSummary ?? DBNull.Value);
            cmd.Parameters.Add(new NpgsqlParameter("p_open_risks", NpgsqlDbType.Array | NpgsqlDbType.Text)
            {
                Value = _openRisks
            });
            cmd.Parameters.AddWithValue("p_mode", _mode);
        }
    }

    // ── internal DTO ─────────────────────────────────────────────────────

    private sealed class RunStateRow
    {
        public string   Run_Id               { get; set; } = string.Empty;
        public string   Project_Name         { get; set; } = string.Empty;
        public string   Goal                 { get; set; } = string.Empty;
        public string   Current_Phase        { get; set; } = string.Empty;
        public string   Approvals            { get; set; } = "{}";
        public string?  Last_Handoff_Summary { get; set; }
        public string[] Open_Risks           { get; set; } = Array.Empty<string>();
        public string   Mode                 { get; set; } = string.Empty;
        public DateTime Updated_At           { get; set; }

        public RunState ToEntity() => new()
        {
            RunId               = Run_Id,
            ProjectName         = Project_Name,
            Goal                = Goal,
            CurrentPhase        = Current_Phase,
            Approvals           = JsonSerializer.Deserialize<Dictionary<string, bool>>(Approvals) ?? new(),
            LastHandoffSummary  = Last_Handoff_Summary,
            OpenRisks           = Open_Risks,
            Mode                = Mode,
            UpdatedAt           = Updated_At,
        };
    }
}
