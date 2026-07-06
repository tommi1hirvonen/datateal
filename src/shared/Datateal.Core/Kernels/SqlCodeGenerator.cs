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
}
