using System.ComponentModel.DataAnnotations;

namespace DevDocsAI.Infrastructure.Storage;

/// <summary>Local file-storage configuration, bound from the "Storage" section.</summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    [Required]
    public string RootPath { get; init; } = "./storage";
}
