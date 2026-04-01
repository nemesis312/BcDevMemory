namespace DevMemory.Core.Models;

public sealed class SyncOptions
{
    public string? Project { get; set; }
    public bool AllProjects { get; set; }
    public string? SyncPath { get; set; }
}
