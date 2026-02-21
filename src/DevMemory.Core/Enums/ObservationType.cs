namespace DevMemory.Core.Enums;

public static class ObservationType
{
    public const string Bugfix = "bugfix";
    public const string ArchitectureDecision = "architecture-decision";
    public const string SecurityPattern = "security-pattern";
    public const string PerformanceFix = "performance-fix";
    public const string MultiTenantRule = "multi-tenant-rule";
    public const string StoredProcedurePattern = "stored-procedure-pattern";
    public const string ConfigChange = "config-change";
    public const string Pattern = "pattern";
    public const string Discovery = "discovery";
    public const string Preference = "preference";
    public const string Idea = "idea";
    public const string TechDebt = "tech-debt";
    public const string NewFeature = "new-feature";


    private static readonly HashSet<string> _all = new(StringComparer.OrdinalIgnoreCase)
    {
        Bugfix, ArchitectureDecision, SecurityPattern, PerformanceFix,
        MultiTenantRule, StoredProcedurePattern, ConfigChange, Pattern,
        Discovery, Preference, Idea, TechDebt, NewFeature
    };

    public static bool IsValid(string type) => _all.Contains(type);

    public static IReadOnlyCollection<string> All => _all;
}
