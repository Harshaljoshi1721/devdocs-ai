using System.ComponentModel.DataAnnotations;

namespace DevDocsAI.Infrastructure.Security;

/// <summary>Validated JWT/refresh-token configuration bound from the "Jwt" section.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    [MinLength(32, ErrorMessage = "Jwt:SigningKey must be at least 32 characters.")]
    public string SigningKey { get; init; } = string.Empty;

    [Required]
    public string Issuer { get; init; } = "devdocs-ai";

    [Required]
    public string Audience { get; init; } = "devdocs-ai";

    [Range(1, 1440)]
    public int AccessTokenMinutes { get; init; } = 15;

    [Range(1, 365)]
    public int RefreshTokenDays { get; init; } = 14;
}
