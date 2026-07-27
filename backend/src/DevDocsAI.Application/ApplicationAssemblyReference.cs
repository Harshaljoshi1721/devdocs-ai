namespace DevDocsAI.Application;

/// <summary>
/// Marker type for the Application assembly. Holds use cases, DTOs, validation,
/// and the port interfaces (LLM, embeddings, vector store, storage, GitHub,
/// background jobs) that Infrastructure implements. Used for assembly scanning
/// (validators, handlers). Non-static so it can serve as a generic marker.
/// </summary>
public sealed class ApplicationAssemblyReference
{
    private ApplicationAssemblyReference() { }
}
