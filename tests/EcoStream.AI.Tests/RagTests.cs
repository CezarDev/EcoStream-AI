using EcoStream.AI.Agents;
using EcoStream.AI.Rag;
using FluentAssertions;

namespace EcoStream.AI.Tests;

public class RagTests
{
    [Fact]
    public void Protocol_docs_chunk_by_section_with_stable_ids_and_bounded_size()
    {
        var first = DocsProtocolRetriever.LoadDocs();
        var second = DocsProtocolRetriever.LoadDocs();

        first.Should().HaveCountGreaterThan(10);
        first.Select(c => c.Id).Should().OnlyHaveUniqueItems().And.Equal(second.Select(c => c.Id));
        first.Should().OnlyContain(c => c.Text.StartsWith(c.Title, StringComparison.Ordinal));
        first.Should().OnlyContain(c => c.Text.Length <= MarkdownChunker.MaxChars + 200);
    }

    [Fact]
    public void Long_sections_are_split_by_paragraph()
    {
        var paragraphs = string.Join("\n\n", Enumerable.Range(1, 20).Select(i => new string('x', 150) + i));
        var chunks = MarkdownChunker.Chunk("doc.md", $"# Doc\n\n## Grande\n{paragraphs}");

        chunks.Should().HaveCountGreaterThan(1).And.OnlyContain(c => c.Section == "Grande");
    }

    [Fact]
    public void Retrieval_query_reflects_the_hazards_present()
    {
        var query = AiRiskDecider.BuildQuery(Scenarios.StormOverFloodZone());

        query.Should().Contain("inundação").And.Contain("deslizamento").And.NotContain("vendaval");
    }
}
