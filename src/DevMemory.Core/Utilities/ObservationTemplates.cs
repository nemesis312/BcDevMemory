namespace DevMemory.Core.Utilities;

/// <summary>
/// Canonical content templates per observation type.
/// Used by CLI (--template flag), MCP tools, and documentation.
/// </summary>
public static class ObservationTemplates
{
    public static readonly IReadOnlyDictionary<string, string> Templates =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["bugfix"] =
                "What: \nWhy it happened: \nWhere: \nFix applied: \nLearned: ",

            ["architecture-decision"] =
                "Context: \nDecision: \nConsequences: \nAlternatives considered: ",

            ["security-pattern"] =
                "Pattern: \nThreat mitigated: \nMitigation: \nWhere applied: ",

            ["performance-fix"] =
                "Problem: \nRoot cause: \nFix: \nMeasured improvement: ",

            ["multi-tenant-rule"] =
                "Rule: \nRationale: \nEnforcement point: \nException handling: ",

            ["stored-procedure-pattern"] =
                "Procedure: \nPurpose: \nKey logic: \nCallers: ",

            ["config-change"] =
                "Setting: \nOld value: \nNew value: \nReason: ",

            ["pattern"] =
                "Pattern: \nContext: \nImplementation: \nWhen to use: ",

            ["discovery"] =
                "Discovery: \nContext: \nImplication: \nNext steps: ",

            ["preference"] =
                "Preference: \nReason: \nExceptions: ",

            ["idea"] =
                "Idea: \nProblem it solves: \nRough approach: \nPriority: ",

            ["tech-debt"] =
                "Issue: \nWhy it exists: \nImpact: \nProposed fix: ",

            ["new-feature"] =
                "Feature: \nMotivation: \nApproach: \nFiles changed: ",
        };

    public static string? GetTemplate(string type) =>
        Templates.TryGetValue(type, out var t) ? t : null;
}
