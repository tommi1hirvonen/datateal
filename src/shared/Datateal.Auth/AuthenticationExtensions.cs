using Datateal.Auth.ApiKey;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Datateal.Auth;

/// <summary>
/// Extension methods for registering Datateal authentication services.
/// </summary>
public static class AuthenticationExtensions
{
    /// <summary>
    /// Configures OIDC authentication for the UI server using the provider specified in configuration.
    /// Reads "Authentication:Provider" to select the <see cref="IIdentityProviderSetup"/> implementation.
    /// The caller must register the appropriate <see cref="IIdentityProviderSetup"/> before calling this method.
    /// </summary>
    public static IServiceCollection AddDatatealWebAppAuthentication(
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
    public static IServiceCollection AddDatatealApiKeyAuthentication(
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
}
