using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Parses design-doc tile IDs like "A1", "R29" or "AN30" into grid coordinates.
/// Columns are letters (A..Z, then AA..AN), rows are numbers 1..30 from the top.
/// </summary>
public static class TileRef
{
    public static bool TryParse(string id, out Vector2Int tile)
    {
        tile = default;
        if (string.IsNullOrWhiteSpace(id)) return false;
        id = id.Trim().ToUpperInvariant();

        int i = 0;
        int col = 0;
        while (i < id.Length && id[i] >= 'A' && id[i] <= 'Z')
        {
            col = col * 26 + (id[i] - 'A' + 1); // bijective base-26: A=1..Z=26, AA=27...
            i++;
        }
        if (i == 0 || i == id.Length) return false;
        if (!int.TryParse(id.Substring(i), out int rowNumber)) return false;

        var result = new Vector2Int(col - 1, rowNumber - 1);
        if (!GridMath.InBounds(result)) return false;

        tile = result;
        return true;
    }

    public static Vector2Int Parse(string id)
    {
        if (!TryParse(id, out var tile))
            throw new FormatException($"'{id}' is not a valid tile ID (expected e.g. \"A1\" or \"AN30\").");
        return tile;
    }
}

/// <summary>
/// A rectangle of tiles described exactly like the design doc does it,
/// e.g. from "C27" to "E28". Editable in the Inspector as plain text.
/// </summary>
[Serializable]
public class TileRegion
{
    public string name = "Region";

    [Tooltip("Top-left tile of the rectangle, e.g. \"A1\"")]
    public string from = "A1";

    [Tooltip("Bottom-right tile of the rectangle, e.g. \"B30\"")]
    public string to = "A1";

    /// <summary>Every tile inside the rectangle (inclusive of both corners).</summary>
    public IEnumerable<Vector2Int> Tiles()
    {
        if (!TileRef.TryParse(from, out var a) || !TileRef.TryParse(to, out var b))
        {
            Debug.LogWarning($"TileRegion '{name}' has an invalid tile ID ('{from}'..'{to}') and will be skipped.");
            yield break;
        }

        int minX = Mathf.Min(a.x, b.x), maxX = Mathf.Max(a.x, b.x);
        int minY = Mathf.Min(a.y, b.y), maxY = Mathf.Max(a.y, b.y);
        for (int x = minX; x <= maxX; x++)
            for (int y = minY; y <= maxY; y++)
                yield return new Vector2Int(x, y);
    }
}
