using System.Text.Json;
using System.Text.Json.Nodes;

namespace WorkHub.Services;

public static class JsonComparison
{
    // Structural equality of two object graphs, ignoring every property named
    // "Url" at any depth. Presigned photo URLs are regenerated on each API
    // response, so without this a refresh would always look "changed" and force
    // a full UI rebind even when nothing the user sees has moved.
    public static bool EqualIgnoringUrls<T>(T a, T b)
    {
        var nodeA = JsonSerializer.SerializeToNode(a);
        var nodeB = JsonSerializer.SerializeToNode(b);
        StripUrls(nodeA);
        StripUrls(nodeB);
        return (nodeA?.ToJsonString() ?? "") == (nodeB?.ToJsonString() ?? "");
    }

    private static void StripUrls(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(p => p.Key).ToList())
                {
                    if (key.Equals("Url", StringComparison.OrdinalIgnoreCase))
                        obj.Remove(key);
                    else
                        StripUrls(obj[key]);
                }
                break;
            case JsonArray arr:
                foreach (var item in arr)
                    StripUrls(item);
                break;
        }
    }
}
