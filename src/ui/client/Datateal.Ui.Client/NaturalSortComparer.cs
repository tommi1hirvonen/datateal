using System.Text.RegularExpressions;

namespace Datateal.Ui.Client;

/// <summary>
/// OS-style natural sort: numeric segments are compared by value so "2_x" sorts before "11_x".
/// </summary>
public sealed partial class NaturalSortComparer : IComparer<string?>
{
    public static readonly NaturalSortComparer OrdinalIgnoreCase = new();

    [GeneratedRegex(@"(\d+)|(\D+)")]
    private static partial Regex ChunkPattern();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        var chunksX = ChunkPattern().Matches(x);
        var chunksY = ChunkPattern().Matches(y);

        int len = Math.Min(chunksX.Count, chunksY.Count);
        for (int i = 0; i < len; i++)
        {
            var cx = chunksX[i].Value;
            var cy = chunksY[i].Value;

            int result = (long.TryParse(cx, out var nx), long.TryParse(cy, out var ny)) switch
            {
                (true, true) => nx.CompareTo(ny),
                (false, false) => string.Compare(cx, cy, StringComparison.OrdinalIgnoreCase),
                (true, false) => -1,
                (false, true) => 1,
            };

            if (result != 0) return result;
        }

        return chunksX.Count.CompareTo(chunksY.Count);
    }
}
