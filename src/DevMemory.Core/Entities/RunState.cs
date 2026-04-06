namespace DevMemory.Core.Entities;

public class RunState
{
    public string  RunId               { get; set; } = string.Empty;
    public string  ProjectName         { get; set; } = string.Empty;
    public string  Goal                { get; set; } = string.Empty;
    public string  CurrentPhase        { get; set; } = string.Empty;
    public Dictionary<string, bool> Approvals { get; set; } = new();
    public string? LastHandoffSummary  { get; set; }
    public string[] OpenRisks          { get; set; } = Array.Empty<string>();
    public string  Mode                { get; set; } = string.Empty;
    public DateTime UpdatedAt          { get; set; }
}
