namespace DuckHouse.Core.Kernels;

/// <summary>
/// Generates Python code for executing SQL queries in the DuckHouse kernel environment.
/// The kernel runs Python; SQL is executed via the DuckDB Python API.
/// </summary>
public static class SqlCodeGenerator
{
    /// <summary>
    /// Wraps a SQL string as executable Python code that runs it via DuckDB and returns
    /// the result as a DataFrame. EXPLAIN queries are printed as text instead, because
    /// DuckDB's EXPLAIN output can be very wide and doesn't fit well in the DataFrame view.
    /// </summary>
    public static string WrapSql(string sql) =>
        $""""
        import duckdb
        __df = duckdb.execute("""{sql}""").df()
        __result = None
        if all(value in __df.columns for value in ['explain_key', 'explain_value']):
            print('\\n'.join(str(v) for v in __df['explain_value']))
        else:
            __result = __df
        __result
        """";
}
