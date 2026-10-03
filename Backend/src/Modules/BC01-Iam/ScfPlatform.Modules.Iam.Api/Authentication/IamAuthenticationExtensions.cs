using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace ScfPlatform.Modules.Iam.Api.Authentication;

/// <summary>Registers this module's <see cref="BearerTokenAuthenticationHandler"/> as the host's (only, for now) authentication scheme.</summary>
public static class IamAuthenticationExtensions
{
    public static IServiceCollection AddIamAuthentication(this IServiceCollection services)
    {
        services
            .AddAuthentication(BearerTokenAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, BearerTokenAuthenticationHandler>(BearerTokenAuthenticationHandler.SchemeName, _ => { });

        services.AddAuthorization();

        return services;
    }
}
