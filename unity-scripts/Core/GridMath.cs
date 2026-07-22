using UnityEngine;

/// <summary>
/// Static helpers for converting between the design doc's grid
/// (40 columns x 30 rows, row 1 at the TOP) and Unity world space.
///
/// World convention used everywhere in this project:
/// - Each tile is exactly 1x1 world unit.
/// - The bottom-left corner of the map is world position (0, 0),
///   so the map spans x: 0..40 and y: 0..30.
/// - Doc tile A1 (top-left) therefore sits at world y 29..30.
/// </summary>
public static class GridMath
{
    public const int Columns = 40;
    public const int Rows = 30;

    /// <summary>
    /// Convert a doc tile (col 0-based from the left, row 0-based from the TOP)
    /// into a Tilemap cell position (which counts y upward from the bottom).
    /// </summary>
    public static Vector3Int TileToCell(Vector2Int tile) =>
        new Vector3Int(tile.x, Rows - 1 - tile.y, 0);

    /// <summary>World position of the CENTER of a tile.</summary>
    public static Vector2 TileCenterWorld(Vector2Int tile) =>
        new Vector2(tile.x + 0.5f, Rows - 1 - tile.y + 0.5f);

    /// <summary>Which doc tile contains this world position.</summary>
    public static Vector2Int WorldToTile(Vector2 worldPos)
    {
        int col = Mathf.FloorToInt(worldPos.x);
        int rowFromBottom = Mathf.FloorToInt(worldPos.y);
        return new Vector2Int(col, Rows - 1 - rowFromBottom);
    }

    public static bool InBounds(Vector2Int tile) =>
        tile.x >= 0 && tile.x < Columns && tile.y >= 0 && tile.y < Rows;

    public static Vector2 MapCenterWorld => new Vector2(Columns / 2f, Rows / 2f);
}
