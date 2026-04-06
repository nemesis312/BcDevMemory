namespace DevMemory.Core.Entities;

public class Artifact
{
    public string Id          { get; set; } = string.Empty;
    public string RunId       { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string Type        { get; set; } = string.Empty;
    public string Content     { get; set; } = string.Empty;
    public int    Version     { get; set; } = 1;
    public Dictionary<string, string>? Metadata { get; set; }
    public DateTime CreatedAt { get; set; }
}
