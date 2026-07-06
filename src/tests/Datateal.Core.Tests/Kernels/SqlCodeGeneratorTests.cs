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
}
