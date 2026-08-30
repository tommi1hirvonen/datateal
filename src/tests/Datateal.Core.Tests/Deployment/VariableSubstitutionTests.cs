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
        var env = new Dictionary<string, string> { ["DB_PASSWORD"] = "secret123" };
        Assert.Equal("secret123", VariableSubstitution.Substitute("${env.DB_PASSWORD}", null, env));
    }

    [Fact]
    public void EnvToken_MissingFromDict_Throws()
    {
        Assert.Throws<DeploymentVariableException>(() =>
            VariableSubstitution.Substitute("${env.MISSING}", null, new Dictionary<string, string>()));
    }

    [Fact]
    public void EnvToken_NullDict_Throws()
    {
        Assert.Throws<DeploymentVariableException>(() =>
            VariableSubstitution.Substitute("${env.DB_PASSWORD}", null, null));
    }

    [Fact]
    public void UnresolvedVarToken_Throws()
    {
        Assert.Throws<DeploymentVariableException>(() =>
            VariableSubstitution.Substitute("${var.missing}", new Dictionary<string, string>()));
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

    [Fact]
    public void DeploymentVariableException_IsInvalidOperationException()
    {
        // DeploymentController.ExecuteChangeSetAsync only has dedicated catch clauses for
        // InvalidOperationException (400) and DeploymentConflictException/DeploymentAuthorizationException.
        // DeploymentVariableException must derive from InvalidOperationException so an unresolved
        // ${var.X}/${env.X} token surfaces as a 400 Bad Request instead of an unhandled 500.
        var ex = Assert.Throws<DeploymentVariableException>(() =>
            VariableSubstitution.Substitute("${env.MISSING}", null, new Dictionary<string, string>()));

        Assert.IsAssignableFrom<InvalidOperationException>(ex);
    }
}
