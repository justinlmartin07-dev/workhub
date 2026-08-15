using System.Net;
using System.Text;

namespace WorkHub.Services;

// Minimal mustache-style renderer for the print templates:
//   {{key}}            value, HTML-escaped
//   {{#key}}...{{/key}} render when truthy; repeated per item for lists
//   {{^key}}...{{/key}} render when falsy/empty
// Lists are List<Dictionary<string, object?>>; lookups walk the scope stack so
// loop items see outer values. Templates are edited server-side and fetched at
// runtime, so this is deliberately fail-soft: malformed tags render literally
// and unknown keys render empty — a bad template never crashes printing.
public static class TemplateRenderer
{
    public static string Render(string template, Dictionary<string, object?> model)
    {
        var sb = new StringBuilder(template.Length * 2);
        RenderBlock(template, [model], sb);
        return sb.ToString();
    }

    private static void RenderBlock(string tpl, List<Dictionary<string, object?>> scopes, StringBuilder sb)
    {
        var i = 0;
        while (i < tpl.Length)
        {
            var open = tpl.IndexOf("{{", i, StringComparison.Ordinal);
            if (open < 0) { sb.Append(tpl, i, tpl.Length - i); return; }
            sb.Append(tpl, i, open - i);

            var close = tpl.IndexOf("}}", open + 2, StringComparison.Ordinal);
            if (close < 0) { sb.Append(tpl, open, tpl.Length - open); return; }

            var tag = tpl.Substring(open + 2, close - open - 2).Trim();
            i = close + 2;

            if (tag.Length == 0)
                continue;

            if (tag[0] is '#' or '^')
            {
                var key = tag[1..].Trim();
                var end = FindSectionEnd(tpl, i, key);
                if (end < 0) { sb.Append(tpl, open, close + 2 - open); continue; }

                var inner = tpl[i..end];
                i = end + ("{{/" + key + "}}").Length;

                var value = Lookup(scopes, key);
                if (tag[0] == '#')
                {
                    if (value is List<Dictionary<string, object?>> list)
                    {
                        foreach (var item in list)
                        {
                            scopes.Add(item);
                            RenderBlock(inner, scopes, sb);
                            scopes.RemoveAt(scopes.Count - 1);
                        }
                    }
                    else if (IsTruthy(value))
                        RenderBlock(inner, scopes, sb);
                }
                else if (!IsTruthy(value))
                    RenderBlock(inner, scopes, sb);
            }
            else if (tag[0] == '/')
            {
                // Stray close tag — drop it.
            }
            else
            {
                var value = Lookup(scopes, tag);
                if (value != null)
                    sb.Append(WebUtility.HtmlEncode(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)));
            }
        }
    }

    // Index just past the matching {{/key}} opener, accounting for nested
    // sections over the same key; -1 when unclosed.
    private static int FindSectionEnd(string tpl, int from, string key)
    {
        var openHash = "{{#" + key + "}}";
        var openCaret = "{{^" + key + "}}";
        var closeTag = "{{/" + key + "}}";
        var depth = 1;
        var i = from;
        while (i < tpl.Length)
        {
            var nextClose = tpl.IndexOf(closeTag, i, StringComparison.Ordinal);
            if (nextClose < 0) return -1;
            var nextOpenHash = tpl.IndexOf(openHash, i, StringComparison.Ordinal);
            var nextOpenCaret = tpl.IndexOf(openCaret, i, StringComparison.Ordinal);
            var nextOpen = (nextOpenHash, nextOpenCaret) switch
            {
                (-1, -1) => -1,
                (-1, _) => nextOpenCaret,
                (_, -1) => nextOpenHash,
                _ => Math.Min(nextOpenHash, nextOpenCaret),
            };

            if (nextOpen >= 0 && nextOpen < nextClose)
            {
                depth++;
                i = nextOpen + openHash.Length;
            }
            else
            {
                depth--;
                if (depth == 0) return nextClose;
                i = nextClose + closeTag.Length;
            }
        }
        return -1;
    }

    private static object? Lookup(List<Dictionary<string, object?>> scopes, string key)
    {
        for (var s = scopes.Count - 1; s >= 0; s--)
            if (scopes[s].TryGetValue(key, out var value))
                return value;
        return null;
    }

    private static bool IsTruthy(object? value) => value switch
    {
        null => false,
        bool b => b,
        string s => !string.IsNullOrWhiteSpace(s),
        List<Dictionary<string, object?>> list => list.Count > 0,
        _ => true,
    };
}
