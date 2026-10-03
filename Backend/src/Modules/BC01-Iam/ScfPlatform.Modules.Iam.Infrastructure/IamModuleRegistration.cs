using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ScfPlatform.BuildingBlocks.Application;
using ScfPlatform.BuildingBlocks.Infrastructure;
using ScfPlatform.BuildingBlocks.Infrastructure.Outbox;
using ScfPlatform.Modules.Iam.Application.Abstractions;
using ScfPlatform.Modules.Iam.Application.Provisioning;
using ScfPlatform.Modules.Iam.Application.Tokens;
using ScfPlatform.Modules.Iam.Contracts;
using ScfPlatform.Modules.Iam.Infrastructure.BackgroundJobs;
using ScfPlatform.Modules.Iam.Infrastructure.Persistence;
using ScfPlatform.Modules.Iam.Infrastructure.Repositories;
using ScfPlatform.Modules.Iam.Infrastructure.Security;

namespace ScfPlatform.Modules.Iam.Infrastructure;

/// <summary>
/// The Iam module's single composition entry point (Foundations §4 "Module registration"). The
/// host's composition root calls this once, alongside every other module's Add*Module. Registers
/// this module's persistence context, repositories, port adapters, and inbound integration-event
/// subscriptions.
///
/// Schema: <see cref="ModuleSchemas.Iam"/>.
/// </summary>
public static class IamModuleRegistration
{
    public static IServiceCollection AddIamModule(this IServiceCollection services, IConfiguration configuration)
    {
        // One physical SQL Server database for the whole modular monolith — each module's schema
        // (Foundations §6.4) provides the isolation, not a separate database/connection string.
        var connectionString = configuration.GetConnectionString("SqlServer")
            ?? "Server=localhost;Database=ScfPlatform;Trusted_Connection=True;TrustServerCertificate=True";

        services.AddDbContext<IamDbContext>((sp, options) =>
            options.UseSqlServer(connectionString).AddInterceptors(sp.GetRequiredService<DomainEventDispatchInterceptor>()));

        services.TryAddSingleton(TimeProvider.System);

        // Repositories / unit of work (§11.3).
        services.AddScoped<IUserAccountRepository, UserAccountRepository>();
        services.AddScoped<IOtpChallengeRepository, OtpChallengeRepository>();
        services.AddScoped<IRevokedTokenStore, RevokedTokenStore>();
        services.AddScoped<IAdminActionLogRepository, AdminActionLogRepository>();
        services.AddScoped<IDeactivationCascadeRunRepository, DeactivationCascadeRunRepository>();
        services.AddScoped<IIamUnitOfWork, IamUnitOfWork>();

        // Inbox (Foundations §6.3), scoped to this module's DbContext.
        services.AddKeyedScoped<IInboxStore, EfInboxStore<IamDbContext>>(ModuleSchemas.Iam);

        // §9.2 port adapters — PasswordHasher/JwtSigner are real (the security core); the rest
        // are stub adapters "for the exercise" per §9.2's own text.
        services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
        services.AddSingleton<IJwtSigner, RsaJwtSigner>();
        services.AddSingleton<IBreachCheckPort, InMemoryBreachCheckPort>();
        services.AddScoped<IOtpDeliveryPort, LoggingOtpDeliveryPort>();
        services.AddSingleton<IRateLimiterPort, InMemoryRateLimiterPort>();
        services.AddSingleton<ITotpProvider, Rfc6238TotpProvider>();

        // §9.3 public API — the frozen Contracts surface every other module references.
        services.AddScoped<IIdentityProvisioningApi, IdentityProvisioningApiAdapter>();
        services.AddScoped<ITokenValidationApi, TokenValidationApiAdapter>();

        // Outbox relay (§6.2) — publishes this module's outbound integration events.
        services.AddHostedService(sp => new OutboxRelayBackgroundService<IamDbContext>(
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<IntegrationEventTypeRegistry>(),
            sp.GetRequiredService<ILogger<OutboxRelayBackgroundService<IamDbContext>>>()));

        // Scheduled jobs (§3): OTP-expiry sweep, session-expiry sweep, revoked-token/OTP cleanup,
        // and the §8.3 AccountDeactivationCascade deadline check.
        services.AddHostedService<OtpExpirySweepBackgroundService>();
        services.AddHostedService<SessionExpirySweepBackgroundService>();
        services.AddHostedService<RevokedTokenAndOtpCleanupBackgroundService>();
        services.AddHostedService<CascadeDeadlineCheckBackgroundService>();

        return services;
    }
}
