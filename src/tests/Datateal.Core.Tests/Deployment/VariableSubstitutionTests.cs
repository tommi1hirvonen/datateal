using Datateal.Deployment.Serialization;

namespace Datateal.Core.Tests.Deployment;

public class VariableSubstitutionTests
{
    private static readonly IReadOnlyDictionary<string, string> Vars = new Dictionary<string, string>
    {
        ["db_host"] = "postgres.example.com",
        ["port"] = "5432",
    };

    [Fact]
    public void NoTokens_ReturnsSameString()
    {
        Assert.Equal("plain value", VariableSubstitution.Substitute("plain value", Vars));
    }

    [Fact]
    public void VarToken_Resolved()
    {
        Assert.Equal("postgres.example.com", VariableSubstitution.Substitute("${var.db_host}", Vars));
    }

    [Fact]
    public void MultipleTokens_AllResolved()
    {
        var result = VariableSubstitution.Substitute("host=${var.db_host} port=${var.port}", Vars);
        Assert.Equal("host=postgres.example.com port=5432", result);
    }

    [Fact]
    public void EnvToken_Resolved()
    {
        System.Environment.SetEnvironmentVariable("DATATEAL_TEST_VAR", "test_value");
        try
        {
            var result = VariableSubstitution.Substitute("${env.DATATEAL_TEST_VAR}", null);
            Assert.Equal("test_value", result);
        }
        finally
        {
            System.Environment.SetEnvironmentVariable("DATATEAL_TEST_VAR", null);
        }
    }

    [Fact]
    public void UnresolvedVarToken_Throws()
    {
        Assert.Throws<DeploymentVariableException>(() =>
            VariableSubstitution.Substitute("${var.missing}", new Dictionary<string, string>()));
    }

    [Fact]
    public void UnresolvedEnvToken_Throws()
    {
        // Use an env var name that is extremely unlikely to exist
        var uniqueName = $"DATATEAL_NONEXISTENT_{Guid.NewGuid():N}";
        Assert.Throws<DeploymentVariableException>(() =>
            VariableSubstitution.Substitute($"${{env.{uniqueName}}}", null));
    }

    [Fact]
    public void NullVariables_VarTokenThrows()
    {
        Assert.Throws<DeploymentVariableException>(() =>
            VariableSubstitution.Substitute("${var.something}", null));
    }

    [Fact]
    public void SubstituteDict_AppliesSubstitutionToValues()
    {
        var dict = new Dictionary<string, string>
        {
            ["HOST"] = "${var.db_host}",
            ["PLAIN"] = "no-change",
        };

        var result = VariableSubstitution.SubstituteDict(dict, Vars);

        Assert.NotNull(result);
        Assert.Equal("postgres.example.com", result!["HOST"]);
        Assert.Equal("no-change", result["PLAIN"]);
    }

    [Fact]
    public void SubstituteDict_NullInput_ReturnsNull()
    {
        Assert.Null(VariableSubstitution.SubstituteDict(null, Vars));
    }
}
