using DevDocsAI.Application.Abstractions.AI;
using DevDocsAI.Application.Abstractions.Persistence;
using DevDocsAI.Application.Abstractions.Storage;
using DevDocsAI.Domain.Entities;

namespace DevDocsAI.Application.Features.Processing;

/// <summary>
/// Processes a single uploaded document: reads its bytes, normalizes the text,
/// chunks it with line metadata, and persists the chunks — transitioning the
/// document Pending → Processing → Completed (or Failed on error). Runs on the
/// background queue, so it never blocks an upload request.
/// </summary>
public interface IDocumentProcessor
{
    Task ProcessAsync(Guid documentId, CancellationToken ct);
}

public sealed class DocumentProcessor(
    IDocumentRepository documents,
    IDocumentChunkRepository chunks,
    IFileStorage fileStorage,
    ITextChunker chunker,
    IEmbeddingService embeddings,
    IVectorStore vectorStore,
    IUnitOfWork unitOfWork) : IDocumentProcessor
{
    public async Task ProcessAsync(Guid documentId, CancellationToken ct)
    {
        var document = await documents.GetByIdAsync(documentId, ct);
        if (document is null)
        {
            return; // deleted before processing ran
        }

        try
        {
            document.MarkProcessing();
            await unitOfWork.SaveChangesAsync(ct);

            var text = await ReadTextAsync(document.StorageKey, ct);

            // Reprocessing safety: clear prior chunks and vectors first.
            await chunks.DeleteByDocumentAsync(documentId, ct);
            await vectorStore.DeleteByDocumentAsync(documentId, ct);

            var newChunks = chunker.Chunk(text)
                .Select(c => new DocumentChunk(documentId, c.ChunkIndex, c.Content, c.StartLine, c.EndLine))
                .ToList();
            await chunks.AddRangeAsync(newChunks, ct);
            await unitOfWork.SaveChangesAsync(ct); // persist chunks before their embeddings reference them

            if (newChunks.Count > 0)
            {
                var vectors = await embeddings.EmbedAsync(newChunks.Select(c => c.Content).ToList(), ct);
                var records = newChunks
                    .Zip(vectors, (chunk, vector) =>
                        new VectorRecord(chunk.Id, documentId, document.ProjectId, vector))
                    .ToList();
                await vectorStore.UpsertAsync(records, ct);
            }

            document.MarkCompleted();
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            document.MarkFailed(Truncate(ex.Message, 2000));
            await unitOfWork.SaveChangesAsync(ct);
        }
    }

    private async Task<string> ReadTextAsync(string storageKey, CancellationToken ct)
    {
        await using var stream = await fileStorage.OpenReadAsync(storageKey, ct);
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync(ct);
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
