using System.Text.Json;

namespace Datateal.Core.Kernels;

public record DataFrameColumn(string Name, string Type);

public record DataFrameOutput(
    IReadOnlyList<DataFrameColumn> Columns,
    IReadOnlyList<IReadOnlyList<object?>> Rows,
    int TotalRows,
    int DisplayedRows)
{
    public const string MimeType = "application/vnd.datateal.dataframe+json";

    /// <summary>
    /// Maximum rows the runtime formatter emits. When <see cref="TotalRows"/>
    /// equals this value the actual DataFrame may have been larger.
    /// </summary>
    public const int RowCap = 10_000;

    /// <summary>
    /// Tries to deserialise a <see cref="DataFrameOutput"/> from the raw value
    /// stored in <see cref="Output.Data"/> under <see cref="MimeType"/>.
    /// Returns <c>null</c> if <paramref name="raw"/> is not a recognisable payload.
    /// </summary>
    public static DataFrameOutput? TryParse(object? raw)
    {
        if (raw is not JsonElement je) return null;

        try
        {
            var columns = je.GetProperty("columns").EnumerateArray()
                .Select(c => new DataFrameColumn(
                    c.GetProperty("name").GetString() ?? "",
                    c.GetProperty("type").GetString() ?? "string"))
                .ToList();

            var rows = je.GetProperty("rows").EnumerateArray()
                .Select(row => (IReadOnlyList<object?>)row.EnumerateArray()
                    .Select(cell => cell.ValueKind switch
                    {
                        JsonValueKind.Null => (object?)null,
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Number => cell.TryGetInt64(out var l) ? l : cell.GetDouble(),
                        _ => cell.GetString(),
                    })
                    .ToList())
                .ToList();

            int totalRows = je.GetProperty("total_rows").GetInt32();
            int displayedRows = je.GetProperty("displayed_rows").GetInt32();

            return new DataFrameOutput(columns, rows, totalRows, displayedRows);
        }
        catch
        {
            return null;
        }
    }
}
