using DuckHouse.Auth.ApiKey;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DuckHouse.Auth;

/// <summary>
/// Extension methods for registering DuckHouse authentication services.
/// </summary>
public static class AuthenticationExtensions
{
    /// <summary>
    /// Configures OIDC authentication for the UI server using the provider specified in configuration.
    /// Reads "Authentication:Provider" to select the <see cref="IIdentityProviderSetup"/> implementation.
    /// The caller must register the appropriate <see cref="IIdentityProviderSetup"/> before calling this method.
    /// </summary>
    public static IServiceCollection AddDuckHouseWebAppAuthentication(
        this IServiceCollection services, IConfiguration configuration)
    {
        // The IIdentityProviderSetup implementation must already be registered
        // (e.g., by AddEntraIdAuthentication). Build a temporary SP to invoke it.
        using var sp = services.BuildServiceProvider();
        var setup = sp.GetRequiredService<IIdentityProviderSetup>();
        setup.ConfigureWebAppAuthentication(services, configuration);

        return services;
    }

    /// <summary>
    /// Configures API key authentication for backend services (orchestrator, control plane).
    /// Reads "ServiceAuth:ExpectedApiKey" from configuration.
    /// </summary>
    public static IServiceCollection AddDuckHouseApiKeyAuthentication(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication(ApiKeyAuthenticationOptions.Scheme)
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationOptions.Scheme, options =>
                {
                    options.ExpectedApiKey = configuration["ServiceAuth:ExpectedApiKey"]
                        ?? throw new InvalidOperationException(
                            "ServiceAuth:ExpectedApiKey is not configured.");
                });

        services.AddAuthorization();

        return services;
    }

    /// <summary>
    /// Registers authorization policies using DuckHouse role constants.
    /// Used by the UI server to enforce role-based access on controllers and pages.
    /// </summary>
    public static IServiceCollection AddDuckHouseAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(AuthPolicy.Admin, p =>
                p.RequireRole(DuckHouseRole.Admin))
            .AddPolicy(AuthPolicy.NodePoolManage, p =>
                p.RequireRole(DuckHouseRole.Admin, DuckHouseRole.NodePoolContributor))
            .AddPolicy(AuthPolicy.NodePoolOperate, p =>
                p.RequireRole(DuckHouseRole.Admin, DuckHouseRole.NodePoolContributor, DuckHouseRole.NodePoolOperator))
            .AddPolicy(AuthPolicy.JobManage, p =>
                p.RequireRole(DuckHouseRole.Admin, DuckHouseRole.JobContributor))
            .AddPolicy(AuthPolicy.JobOperate, p =>
                p.RequireRole(DuckHouseRole.Admin, DuckHouseRole.JobContributor, DuckHouseRole.JobOperator))
            .AddPolicy(AuthPolicy.JobRead, p =>
                p.RequireRole(DuckHouseRole.Admin, DuckHouseRole.JobContributor, DuckHouseRole.JobOperator, DuckHouseRole.JobReader))
            .AddPolicy(AuthPolicy.WorkspaceManage, p =>
                p.RequireRole(DuckHouseRole.Admin, DuckHouseRole.WorkspaceContributor))
            .AddPolicy(AuthPolicy.CatalogManage, p =>
                p.RequireRole(DuckHouseRole.Admin, DuckHouseRole.CatalogContributor))
            .AddPolicy(AuthPolicy.EnvironmentManage, p =>
                p.RequireRole(DuckHouseRole.Admin, DuckHouseRole.EnvironmentManager));

        return services;
    }
}
