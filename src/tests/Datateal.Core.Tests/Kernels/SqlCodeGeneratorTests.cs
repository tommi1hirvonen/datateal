using System.Text;
using Datateal.Core.Kernels;

namespace Datateal.Core.Tests.Kernels;

public class SqlCodeGeneratorTests
{
    /// <summary>
    /// Extracts the Base64 payload from the generated Python snippet and decodes it back to
    /// the original SQL string. This is the canonical verification for all WrapSql tests.
    /// </summary>
    private static string DecodeBase64FromOutput(string output)
    {
        // The generated line looks like: _sql = _b64.b64decode('BASE64==').decode()
        const string marker = "b64decode('";
        var start = output.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, "Could not find b64decode marker in output.");
        start += marker.Length;
        var end = output.IndexOf("')", start, StringComparison.Ordinal);
        Assert.True(end > start, "Could not find closing quote after b64decode payload.");
        var b64 = output[start..end];
        return Encoding.UTF8.GetString(Convert.FromBase64String(b64));
    }

    [Fact]
    public void WrapSql_NormalSql_RoundTripsCorrectly()
    {
        const string sql = "SELECT 1";
        var result = SqlCodeGenerator.WrapSql(sql);
        Assert.Equal(sql, DecodeBase64FromOutput(result));
    }

    [Fact]
    public void WrapSql_TrailingDoubleQuote_RoundTripsCorrectly()
    {
        // Regression test: SQL ending with " used to produce SyntaxError via triple-quote injection.
        const string sql = "select 1 as \"value\"";
        var result = SqlCodeGenerator.WrapSql(sql);
        Assert.Equal(sql, DecodeBase64FromOutput(result));
    }

    [Fact]
    public void WrapSql_TripleQuoteInSql_RoundTripsCorrectly()
    {
        const string sql = "SELECT '\"\"\"' AS col";
        var result = SqlCodeGenerator.WrapSql(sql);
        Assert.Equal(sql, DecodeBase64FromOutput(result));
    }

    [Fact]
    public void WrapSql_BackslashInSql_RoundTripsCorrectly()
    {
        const string sql = @"SELECT '\n' AS col";
        var result = SqlCodeGenerator.WrapSql(sql);
        Assert.Equal(sql, DecodeBase64FromOutput(result));
    }

    [Fact]
    public void WrapSql_UnicodeInSql_RoundTripsCorrectly()
    {
        const string sql = "SELECT '日本語' AS col";
        var result = SqlCodeGenerator.WrapSql(sql);
        Assert.Equal(sql, DecodeBase64FromOutput(result));
    }

    [Fact]
    public void WrapSql_AlwaysContainsPythonBoilerplate()
    {
        var result = SqlCodeGenerator.WrapSql("SELECT 1");

        Assert.Contains("import duckdb", result);
        Assert.Contains("_sqldf = duckdb.execute(_sql).df()", result);
        Assert.Contains("__result", result);
    }

    [Fact]
    public void CleanSqlError_StripsDuckDbModulePrefix()
    {
        var raw = new ErrorInfo("duckdb.duckdb.CatalogException", "Table not found", ["traceback line"]);
        var clean = SqlCodeGenerator.CleanSqlError(raw);
        Assert.Equal("CatalogException", clean.Ename);
    }

    [Fact]
    public void CleanSqlError_SingleLineEvalue_ClearsTraceback()
    {
        var raw = new ErrorInfo("duckdb.duckdb.CatalogException", "Table not found", ["py line"]);
        var clean = SqlCodeGenerator.CleanSqlError(raw);
        Assert.Equal("Table not found", clean.Evalue);
        Assert.Empty(clean.Traceback);
    }

    [Fact]
    public void CleanSqlError_MultiLineEvalue_MovesPositionContextToTraceback()
    {
        // DuckDB embeds LINE N / caret context in the evalue after a newline.
        const string evalue = "Conversion Error: Could not convert string 'asd' to DOUBLE\nLINE 3: select 1 / 'asd'\n                   ^";
        var raw = new ErrorInfo("duckdb.duckdb.ConversionException", evalue, ["py traceback"]);
        var clean = SqlCodeGenerator.CleanSqlError(raw);

        Assert.Equal("Conversion Error: Could not convert string 'asd' to DOUBLE", clean.Evalue);
        Assert.Equal(["LINE 3: select 1 / 'asd'", "                   ^"], clean.Traceback);
    }

    [Fact]
    public void CleanSqlError_NullEnameAndEvalue_DoesNotThrow()
    {
        // JSON deserialization can produce null for non-nullable string fields.
        // CleanSqlError must never throw regardless of payload content.
        var raw = new ErrorInfo(null!, null!, []);
        var clean = SqlCodeGenerator.CleanSqlError(raw);
        Assert.Equal(string.Empty, clean.Ename);
        Assert.Equal(string.Empty, clean.Evalue);
        Assert.Empty(clean.Traceback);
    }

    [Fact]
    public void CleanSqlError_NoModulePrefix_LeavesEnameUnchanged()
    {
        var raw = new ErrorInfo("Exception", "something went wrong", []);
        var clean = SqlCodeGenerator.CleanSqlError(raw);
        Assert.Equal("Exception", clean.Ename);
    }
}
