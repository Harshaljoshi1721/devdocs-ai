namespace DevDocsAI.Domain.Enums;

/// <summary>Broad category a supported file falls into, derived from its extension.</summary>
public enum FileType
{
    Code = 0,
    Documentation = 1,
    Configuration = 2,
    Other = 3,
}
