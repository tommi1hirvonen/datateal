using Datateal.Deployment.Models;
using Datateal.Deployment.Serialization;
using Datateal.Ui.Server.Infrastructure.Deployment;
using Xunit;

namespace Datateal.Core.Tests.Deployment;

public class AdminBundleValidatorTests
{
    [Fact]
    public void Validate_Fails_WhenUnmanagedCatalogHostReferencesUnsuppliedEnvToken()
    {
        var bundle = new Bundle
        {
            Manifest = new BundleManifest { Scope = "admin" },
            Catalogs =
            [
                new CatalogModel
                {
                    Name = "sales_prod",
                    Type = "unmanaged",
                    CatalogHost = "${env.MISSING_HOST}",
                    CatalogDatabase = "sales",
                    CatalogUser = "svc_sales",
                    CatalogPassword = "already-set", // existing catalog, so password isn't required
                }
            ],
        };

        // Existing catalog so the "new catalog requires password" rule doesn't also fire.
        var ex = Assert.Throws<InvalidOperationException>(() =>
            AdminBundleValidator.Validate(
                bundle,
                existingWorkspaceNames: [],
                existingCatalogNames: ["sales_prod"],
                variables: null,
                env: null));

        Assert.Contains("MISSING_HOST", ex.Message);
    }

    [Fact]
    public void Validate_Fails_WhenUnmanagedCatalogPasswordReferencesUnsuppliedVarToken()
    {
        var bundle = new Bundle
        {
            Manifest = new BundleManifest { Scope = "admin" },
            Catalogs =
            [
                new CatalogModel
                {
                    Name = "sales_prod",
                    Type = "unmanaged",
                    CatalogHost = "sales-db.example.com",
                    CatalogDatabase = "sales",
                    CatalogUser = "svc_sales",
                    CatalogPassword = "${var.missing_password}",
                }
            ],
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            AdminBundleValidator.Validate(
                bundle,
                existingWorkspaceNames: [],
                existingCatalogNames: [],
                variables: new Dictionary<string, string>(),
                env: null));

        Assert.Contains("missing_password", ex.Message);
    }

    [Fact]
    public void Validate_Succeeds_WhenAllTokensResolve()
    {
        var bundle = new Bundle
        {
            Manifest = new BundleManifest { Scope = "admin" },
            Catalogs =
            [
                new CatalogModel
                {
                    Name = "sales_prod",
                    Type = "unmanaged",
                    CatalogHost = "${var.db_host}",
                    CatalogDatabase = "sales",
                    CatalogUser = "svc_sales",
                    CatalogPassword = "${env.SALES_DB_PASSWORD}",
                }
            ],
        };

        var variables = new Dictionary<string, string> { ["db_host"] = "sales-db.example.com" };
        var env = new Dictionary<string, string> { ["SALES_DB_PASSWORD"] = "hunter2" };

        // Should not throw
        AdminBundleValidator.Validate(
            bundle,
            existingWorkspaceNames: [],
            existingCatalogNames: [],
            variables: variables,
            env: env);
    }

    [Fact]
    public void Validate_Succeeds_WhenManagedCatalogHasNoSubstitutableCredentialFields()
    {
        var bundle = new Bundle
        {
            Manifest = new BundleManifest { Scope = "admin" },
            Catalogs =
            [
                new CatalogModel { Name = "sales_prod", Type = "managed", DataPath = "/data/sales" }
            ],
        };

        // Should not throw
        AdminBundleValidator.Validate(
            bundle,
            existingWorkspaceNames: [],
            existingCatalogNames: []);
    }
}
