using System.Text.RegularExpressions;

namespace DevMemory.Core.Utilities;

/// <summary>
/// Strips <private>...</private> blocks before any content reaches the database.
/// Apply at: repository layer, MCP tools, and API endpoints.
/// </summary>
public static partial class PrivacyHelper
{
    [GeneratedRegex(@"<private>.*?</private>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex PrivateTagPattern();

    public static string StripPrivateTags(string content) =>
        PrivateTagPattern().Replace(content, "[REDACTED]");
}
