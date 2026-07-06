using System.Text;

namespace Datateal.Core.Kernels;

/// <summary>
/// Generates Python code for executing SQL queries in the Datateal kernel environment.
/// The kernel runs Python; SQL is executed via the DuckDB Python API.
/// </summary>
public static class SqlCodeGenerator
{
    /// <summary>
    /// Wraps a SQL string as executable Python code that runs it via DuckDB and returns
    /// the result as a DataFrame. EXPLAIN queries are printed as text instead, because
    /// DuckDB's EXPLAIN output can be very wide and doesn't fit well in the DataFrame view.
    /// <para>
    /// The SQL is Base64-encoded before being embedded in the generated Python snippet so
    /// that no SQL content (quotes, backslashes, Unicode) can break the Python string literal.
    /// </para>
    /// </summary>
    /// <remarks>
    /// When the execution produces an error, pass it through <see cref="CleanSqlError"/> before
    /// displaying it to the user to strip Python internals from the error message.
    /// </remarks>
    public static string WrapSql(string sql)
    {
        var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(sql));
        return $"""
            import duckdb, base64 as _b64
            _sql = _b64.b64decode('{b64}').decode()
            _sqldf = duckdb.execute(_sql).df()
            __result = None
            if all(value in _sqldf.columns for value in ['explain_key', 'explain_value']):
                print('\\n'.join(str(v) for v in _sqldf['explain_value']))
            else:
                __result = _sqldf
            __result
            """;
    }

    /// <summary>
    /// Sanitises an <see cref="ErrorInfo"/> that originated from executing a SQL cell so that
    /// it is meaningful to SQL users rather than exposing Python internals.
    /// <list type="bullet">
    ///   <item>The Python module path is stripped from <c>Ename</c> — e.g.
    ///     <c>duckdb.duckdb.CatalogException</c> becomes <c>CatalogException</c>.</item>
    ///   <item><c>Evalue</c> is reduced to the first line of the DuckDB error message (the
    ///     human-readable summary). Any remaining lines — DuckDB's position context such as
    ///     <c>LINE N: ...</c> and the caret indicator (<c>^</c>) — are moved into
    ///     <c>Traceback</c> so they render in a preformatted block and the caret aligns
    ///     correctly beneath the relevant SQL token.</item>
    ///   <item>The Python traceback is discarded entirely. SQL users never need to see the
    ///     generated Python wrapper frames.</item>
    /// </list>
    /// </summary>
    public static ErrorInfo CleanSqlError(ErrorInfo error)
    {
        var ename = error.Ename;
        var lastDot = ename.LastIndexOf('.');
        if (lastDot >= 0)
            ename = ename[(lastDot + 1)..];

        // Split the DuckDB error message on newlines.
        // Line 0: the human-readable summary  (e.g. "Conversion Error: …")
        // Lines 1+: position context          (e.g. "LINE 3: select …" / "          ^")
        var lines = error.Evalue.Split('\n');
        var evalue = lines[0].TrimEnd('\r');
        var context = lines.Length > 1
            ? lines[1..].Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToArray()
            : [];

        return error with { Ename = ename, Evalue = evalue, Traceback = context };
    }
}
