namespace DevMemory.Core.Entities;

public class MemoryStats
{
    public long TotalObservations { get; set; }
    public long TotalSessions { get; set; }
    public long ActiveSessions { get; set; }
    public long Projects { get; set; }
    public DateTime? OldestObservation { get; set; }
    public DateTime? NewestObservation { get; set; }
}
