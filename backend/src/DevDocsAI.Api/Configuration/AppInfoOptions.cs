using System.ComponentModel.DataAnnotations;

namespace DevDocsAI.Api.Configuration;

/// <summary>
/// Strongly-typed, validated application metadata bound from the "AppInfo"
/// configuration section. Demonstrates the validated options pattern that all
/// later configuration (JWT, database, providers) will follow.
/// </summary>
public sealed class AppInfoOptions
{
    public const string SectionName = "AppInfo";

    [Required]
    [MinLength(1)]
    public string Name { get; init; } = "DevDocs AI";

    [Required]
    [MinLength(1)]
    public string Version { get; init; } = "0.1.0";
}
