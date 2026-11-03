using System.Security.Cryptography;
using System.Text;

namespace EcoStream.AI.Rag;

public sealed record DocumentChunk(Guid Id, string Source, string Title, string Section, string Text);

/// <summary>
/// Splits Markdown by "## " sections (keeping the document title as context), then by paragraphs
/// when a section exceeds <see cref="MaxChars"/>. Ids are deterministic so re-ingestion upserts in place.
/// </summary>
public static class MarkdownChunker
{
    public const int MaxChars = 1200;

    public static IReadOnlyList<DocumentChunk> Chunk(string source, string markdown)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentNullException.ThrowIfNull(markdown);

        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var title = lines.FirstOrDefault(l => l.StartsWith("# ", StringComparison.Ordinal))?[2..].Trim() ?? source;

        var sections = new List<(string Name, StringBuilder Body)>();
        (string Name, StringBuilder Body) current = ("Introdução", new StringBuilder());
        foreach (var line in lines)
        {
            if (line.StartsWith("# ", StringComparison.Ordinal)) continue;
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                sections.Add(current);
                current = (line[3..].Trim(), new StringBuilder());
                continue;
            }
            current.Body.AppendLine(line);
        }
        sections.Add(current);

        var chunks = new List<DocumentChunk>();
        foreach (var (name, body) in sections)
        {
            foreach (var piece in SplitBySize(body.ToString().Trim()))
            {
                var text = $"{title} — {name}\n\n{piece}";
                chunks.Add(new DocumentChunk(StableId(source, chunks.Count), source, title, name, text));
            }
        }
        return chunks;
    }

    private static IEnumerable<string> SplitBySize(string text)
    {
        if (text.Length == 0) yield break;
        if (text.Length <= MaxChars) { yield return text; yield break; }

        var buffer = new StringBuilder();
        foreach (var paragraph in text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (buffer.Length > 0 && buffer.Length + paragraph.Length > MaxChars)
            {
                yield return buffer.ToString();
                buffer.Clear();
            }
            if (buffer.Length > 0) buffer.Append("\n\n");
            buffer.Append(paragraph);
        }
        if (buffer.Length > 0) yield return buffer.ToString();
    }

    private static Guid StableId(string source, int index)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{source}#{index}"));
        return new Guid(hash.AsSpan(0, 16));
    }
}
