namespace DevDocsAI.Domain.Enums;

/// <summary>Lifecycle of a document as it is ingested and indexed.</summary>
public enum ProcessingStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3,
}
