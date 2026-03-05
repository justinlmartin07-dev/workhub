using System.Text.RegularExpressions;

namespace WorkHub.Api.Services;

public static partial class AddressNormalizer
{
    private static readonly Dictionary<string, string> Abbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["street"] = "st",
        ["avenue"] = "ave",
        ["boulevard"] = "blvd",
        ["drive"] = "dr",
        ["lane"] = "ln",
        ["road"] = "rd",
        ["court"] = "ct",
        ["place"] = "pl",
        ["circle"] = "cir",
        ["apartment"] = "apt",
        ["suite"] = "ste",
        ["north"] = "n",
        ["south"] = "s",
        ["east"] = "e",
        ["west"] = "w",
        ["northeast"] = "ne",
        ["northwest"] = "nw",
        ["southeast"] = "se",
        ["southwest"] = "sw",
    };

    public static string? Normalize(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return null;

        var normalized = address.ToLowerInvariant();
        normalized = PunctuationRegex().Replace(normalized, " ");
        normalized = WhitespaceRegex().Replace(normalized, " ").Trim();

        var words = normalized.Split(' ');
        for (var i = 0; i < words.Length; i++)
        {
            if (Abbreviations.TryGetValue(words[i], out var abbr))
                words[i] = abbr;
        }

        return string.Join(' ', words);
    }

    [GeneratedRegex(@"[^\w\s]")]
    private static partial Regex PunctuationRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
