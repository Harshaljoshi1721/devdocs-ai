using Microsoft.Extensions.Options;

namespace DevDocsAI.Application.Features.Processing;

/// <summary>A chunk of text with its 1-based, inclusive source line range.</summary>
public sealed record TextChunk(int ChunkIndex, string Content, int StartLine, int EndLine);

public sealed class ChunkingOptions
{
    public const string SectionName = "Chunking";

    /// <summary>Soft upper bound on chunk size in characters.</summary>
    public int MaxChars { get; init; } = 1500;

    /// <summary>Number of lines re-included at the start of the next chunk for context continuity.</summary>
    public int OverlapLines { get; init; } = 5;
}

public interface ITextChunker
{
    /// <summary>Splits normalized text into line-aware chunks that preserve source line numbers.</summary>
    IReadOnlyList<TextChunk> Chunk(string content);
}

/// <summary>
/// Chunks text along line boundaries. Line endings are normalized 1:1 (CRLF/CR → LF)
/// so line numbers are preserved, and each chunk records the exact source line range.
/// Chunks grow until the character budget is reached; a single oversized line becomes
/// its own chunk. Consecutive chunks overlap by a configurable number of lines.
/// </summary>
public sealed class LineAwareChunker(IOptions<ChunkingOptions> options) : ITextChunker
{
    private readonly ChunkingOptions _options = options.Value;

    public IReadOnlyList<TextChunk> Chunk(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        var normalized = content.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');

        var chunks = new List<TextChunk>();
        var chunkIndex = 0;
        var i = 0;

        while (i < lines.Length)
        {
            var charCount = 0;
            var j = i;
            while (j < lines.Length && (j == i || charCount + lines[j].Length + 1 <= _options.MaxChars))
            {
                charCount += lines[j].Length + 1;
                j++;
            }

            var text = string.Join('\n', lines[i..j]);
            if (!string.IsNullOrWhiteSpace(text))
            {
                // Line numbers are 1-based and inclusive.
                chunks.Add(new TextChunk(chunkIndex++, text, i + 1, j));
            }

            // Once the remainder is fully consumed, stop — no redundant trailing
            // overlap chunks (which would duplicate content and confuse retrieval).
            if (j >= lines.Length)
            {
                break;
            }

            // Advance to the next window, applying overlap but always progressing.
            i = _options.OverlapLines > 0 ? Math.Max(j - _options.OverlapLines, i + 1) : j;
        }

        return chunks;
    }
}
