using Microsoft.AspNetCore.Authentication;

namespace Datateal.Ui.Server.Auth;

/// <summary>
/// Registration and pipeline helpers for Datateal API-token authentication.
/// </summary>
public static class ApiTokenAuthenticationExtensions
{
    /// <summary>
    /// Registers the API-token authentication scheme and its validator. Does not change the
    /// interactive default scheme; token authentication is layered in via
    /// <see cref="UseDatatealApiTokenAuthentication"/>.
    /// </summary>
    public static IServiceCollection AddDatatealApiTokenAuthentication(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddScoped<IApiTokenAuthenticator, ApiTokenAuthenticator>();

        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, ApiTokenAuthenticationHandler>(
                ApiTokenAuthenticationDefaults.Scheme, _ => { });

        return services;
    }

    /// <summary>
    /// Adds middleware that authenticates requests carrying a Datateal API token. Must be placed
    /// after <c>UseAuthentication()</c> and before <c>UseAuthorization()</c>. When a token is
    /// present it is validated and — on success — becomes the request principal, overriding any
    /// interactive identity (e.g. the dev dummy user). An invalid/expired/revoked token yields a
    /// 401 instead of falling through to the interactive challenge.
    /// </summary>
    public static IApplicationBuilder UseDatatealApiTokenAuthentication(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (ApiTokenAuthenticationHandler.ExtractToken(context.Request) is not null)
            {
                var result = await context.AuthenticateAsync(ApiTokenAuthenticationDefaults.Scheme);
                if (result.Succeeded)
                {
                    context.User = result.Principal;
                }
                else
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }
            }

            await next(context);
        });
}
