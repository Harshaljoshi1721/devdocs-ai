using DevDocsAI.Application.Features.Processing;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace DevDocsAI.UnitTests.Features;

public sealed class LineAwareChunkerTests
{
    private static LineAwareChunker Chunker(int maxChars = 1500, int overlap = 0) =>
        new(Options.Create(new ChunkingOptions { MaxChars = maxChars, OverlapLines = overlap }));

    [Theory]
    [InlineData("")]
    [InlineData("   \n  \n")]
    public void Empty_content_yields_no_chunks(string content)
    {
        Chunker().Chunk(content).ShouldBeEmpty();
    }

    [Fact]
    public void Small_content_is_a_single_chunk_covering_all_lines()
    {
        var chunks = Chunker().Chunk("line1\nline2\nline3");

        chunks.Count.ShouldBe(1);
        chunks[0].ChunkIndex.ShouldBe(0);
        chunks[0].StartLine.ShouldBe(1);
        chunks[0].EndLine.ShouldBe(3);
        chunks[0].Content.ShouldBe("line1\nline2\nline3");
    }

    [Fact]
    public void Crlf_and_cr_are_normalized_without_losing_line_numbers()
    {
        var chunks = Chunker().Chunk("a\r\nb\rc");

        chunks.Count.ShouldBe(1);
        chunks[0].Content.ShouldBe("a\nb\nc");
        chunks[0].StartLine.ShouldBe(1);
        chunks[0].EndLine.ShouldBe(3);
    }

    [Fact]
    public void Content_splits_into_multiple_chunks_with_contiguous_line_ranges()
    {
        // Each "abcd" line is 5 chars including the newline; MaxChars=10 fits two per chunk.
        var content = "abcd\nabcd\nabcd\nabcd";
        var chunks = Chunker(maxChars: 10, overlap: 0).Chunk(content);

        chunks.Count.ShouldBe(2);
        chunks[0].StartLine.ShouldBe(1);
        chunks[0].EndLine.ShouldBe(2);
        chunks[1].StartLine.ShouldBe(3);
        chunks[1].EndLine.ShouldBe(4);
        chunks.Select(c => c.ChunkIndex).ShouldBe([0, 1]);
    }

    [Fact]
    public void A_line_larger_than_the_budget_becomes_its_own_chunk()
    {
        var chunks = Chunker(maxChars: 3, overlap: 0).Chunk("abcdefghij");

        chunks.Count.ShouldBe(1);
        chunks[0].StartLine.ShouldBe(1);
        chunks[0].EndLine.ShouldBe(1);
        chunks[0].Content.ShouldBe("abcdefghij");
    }

    [Fact]
    public void A_file_that_fits_in_one_chunk_yields_exactly_one_even_with_overlap()
    {
        // Regression: overlap must not emit redundant trailing chunks once the
        // whole file is already covered.
        var chunks = Chunker(maxChars: 1500, overlap: 5).Chunk("line1\nline2\nline3\nline4\nline5\nline6\nline7");

        chunks.Count.ShouldBe(1);
        chunks[0].StartLine.ShouldBe(1);
        chunks[0].EndLine.ShouldBe(7);
    }

    [Fact]
    public void Overlap_reincludes_previous_lines()
    {
        var chunks = Chunker(maxChars: 10, overlap: 1).Chunk("abcd\nabcd\nabcd\nabcd");

        // Chunk 2 should start on chunk 1's last line (overlap of 1).
        chunks[0].EndLine.ShouldBe(2);
        chunks[1].StartLine.ShouldBe(2);
    }
}
