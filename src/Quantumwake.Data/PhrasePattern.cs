using System.Text;
using System.Text.RegularExpressions;

namespace Quantumwake.Data;

/// <summary>
/// One way of saying a voice command, with its interchangeable parts written
/// once: <c>(full|max) power to [the] (shield|shields)</c>. Round brackets
/// pick one of their words; square brackets may be left out. Groups nest.
/// </summary>
/// <remarks>
/// A list of fixed sentences meant a pilot had to say the one wording that
/// was written down - "power to shields" worked and "full power to the
/// shields" did not (1.2.5). The recogniser is handed the same structure
/// rather than every sentence it allows, so a pattern with a thousand
/// wordings costs it no more than one, and stays a closed list: anything not
/// built from these words is still not heard at all.
/// </remarks>
public sealed partial class PhrasePattern
{
    /// <summary>A piece of a pattern: a word, or a group of alternatives.</summary>
    public abstract record Part;

    public sealed record Word(string Text) : Part;

    /// <summary>One of <paramref name="Choices"/>, each a run of parts; may be left out when <paramref name="Optional"/>.</summary>
    public sealed record Group(IReadOnlyList<IReadOnlyList<Part>> Choices, bool Optional) : Part;

    public string Source { get; }
    public IReadOnlyList<Part> Parts { get; }
    private readonly Regex _regex;

    private PhrasePattern(string source, IReadOnlyList<Part> parts)
    {
        Source = source;
        Parts = parts;
        _regex = new Regex("^" + RegexOf(parts) + "$", RegexOptions.CultureInvariant);
    }

    /// <summary>
    /// Reads a pattern. Throws on unbalanced brackets or an empty choice -
    /// a branch that says nothing is where the speech engine fails late, at
    /// load, with no word of which pattern it was.
    /// </summary>
    public static PhrasePattern Parse(string source)
    {
        var tokens = Tokenise(source);
        var at = 0;
        var parts = ReadRun(tokens, ref at, source);
        if (at != tokens.Count) throw new FormatException($"Unexpected '{tokens[at]}' in \"{source}\".");
        if (parts.Count == 0) throw new FormatException("An empty pattern says nothing.");
        return new PhrasePattern(source, parts);
    }

    /// <summary>True when <paramref name="heard"/> is one of the wordings, ignoring case, spacing and punctuation.</summary>
    public bool Matches(string heard) => _regex.IsMatch(Normalise(heard) + " ");

    /// <summary>Every wording, up to <paramref name="limit"/> of them - for tests and examples, not for listening.</summary>
    public IReadOnlyList<string> Expand(int limit = 100_000)
    {
        var all = new List<string>();
        foreach (var words in ExpandRun(Parts))
        {
            all.Add(string.Join(' ', words));
            if (all.Count >= limit) break;
        }
        return all;
    }

    /// <summary>
    /// A few wordings to show a pilot what is understood: the shortest and
    /// the longest, and one between. The full list can run to hundreds.
    /// </summary>
    public IReadOnlyList<string> Examples(int count = 3)
    {
        var all = Expand(5_000).Distinct().OrderBy(p => p.Length).ThenBy(p => p, StringComparer.Ordinal).ToList();
        if (all.Count <= count || count < 2) return all.Take(Math.Max(count, 0)).ToList();
        return Enumerable.Range(0, count).Select(i => all[(all.Count - 1) * i / (count - 1)]).Distinct().ToList();
    }

    /// <summary>Lower case, single spaces, letters, digits and apostrophes only - how heard text and patterns are compared.</summary>
    public static string Normalise(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text.ToLowerInvariant())
            sb.Append(char.IsLetterOrDigit(ch) || ch == '\'' ? ch : ' ');
        return MultiSpace().Replace(sb.ToString(), " ").Trim();
    }

    private static List<string> Tokenise(string source)
    {
        var tokens = new List<string>();
        var word = new StringBuilder();
        void Flush()
        {
            if (word.Length == 0) return;
            tokens.Add(Normalise(word.ToString()));
            word.Clear();
        }
        foreach (var ch in source)
        {
            if (ch is '(' or ')' or '[' or ']' or '|') { Flush(); tokens.Add(ch.ToString()); }
            else if (char.IsWhiteSpace(ch)) Flush();
            else word.Append(ch);
        }
        Flush();
        return tokens.Where(t => t.Length > 0).ToList();
    }

    private static List<Part> ReadRun(List<string> tokens, ref int at, string source)
    {
        var run = new List<Part>();
        while (at < tokens.Count)
        {
            var token = tokens[at];
            if (token is ")" or "]" or "|") break;
            at++;
            if (token is "(" or "[")
            {
                var close = token == "(" ? ")" : "]";
                var choices = new List<IReadOnlyList<Part>>();
                while (true)
                {
                    var choice = ReadRun(tokens, ref at, source);
                    if (choice.Count == 0) throw new FormatException($"An empty choice in \"{source}\".");
                    choices.Add(choice);
                    if (at >= tokens.Count) throw new FormatException($"Unclosed '{token}' in \"{source}\".");
                    var next = tokens[at++];
                    if (next == "|") continue;
                    if (next != close) throw new FormatException($"'{token}' closed by '{next}' in \"{source}\".");
                    break;
                }
                run.Add(new Group(choices, Optional: token == "["));
            }
            else
            {
                run.Add(new Word(token));
            }
        }
        return run;
    }

    // Each word carries its own trailing space, so an optional group that is
    // left out leaves no double space behind and the text is matched with one
    // added at its end.
    private static string RegexOf(IReadOnlyList<Part> run) => string.Concat(run.Select(part => part switch
    {
        Word w => Regex.Escape(w.Text).Replace("\\ ", " ") + " ",
        Group g => "(?:" + string.Join('|', g.Choices.Select(RegexOf)) + ")" + (g.Optional ? "?" : ""),
        _ => throw new InvalidOperationException(),
    }));

    private static IEnumerable<List<string>> ExpandRun(IReadOnlyList<Part> run)
    {
        if (run.Count == 0) { yield return []; yield break; }
        var head = run[0];
        var rest = run.Skip(1).ToList();
        IEnumerable<List<string>> heads = head switch
        {
            Word w => [[w.Text]],
            Group g => (g.Optional ? new[] { new List<string>() } : []).Concat(g.Choices.SelectMany(ExpandRun)),
            _ => throw new InvalidOperationException(),
        };
        foreach (var h in heads)
            foreach (var tail in ExpandRun(rest))
                yield return [.. h, .. tail];
    }

    [GeneratedRegex(" {2,}")]
    private static partial Regex MultiSpace();
}
