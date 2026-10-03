using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using ScfPlatform.BuildingBlocks.Application;
using ScfPlatform.BuildingBlocks.Infrastructure;
using ScfPlatform.BuildingBlocks.Infrastructure.Outbox;
using ScfPlatform.BuildingBlocks.Infrastructure.Realtime;
// Api namespaces — brings each module's Map<Module>Endpoints extension method into scope.
using ScfPlatform.Modules.Iam.Api;
using ScfPlatform.Modules.Iam.Api.Authentication;
// Infrastructure namespaces — brings each module's Add<Module>Module extension method into scope.
using ScfPlatform.Modules.Iam.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ---- Composition root (Foundations §4 "Module registration"; BACKEND_BUILD_INSTRUCTIONS.md §4 Phase A) ----
//
// Every module's Application assembly is scanned once, centrally, for MediatR handlers and
// FluentValidation validators (§6.1) — never per-module. Every module's Contracts assembly
// is scanned once for integration-event types so the outbox relay (§6.2) can resolve a
// stored event's CLR type by name at publish time. Namespaces are fully qualified below
// (rather than `using`d) because every module defines a same-named
// ApplicationAssemblyMarker/ContractsAssemblyMarker anchor type — qualifying avoids a
// CS0104 ambiguity between the 12 identically-named markers.
var moduleApplicationAssemblies = new[]
{
    typeof(ScfPlatform.Modules.Iam.Application.ApplicationAssemblyMarker).Assembly,

};

var moduleContractsAssemblies = new[]
{
    typeof(ScfPlatform.Modules.Iam.Contracts.ContractsAssemblyMarker).Assembly,

};

builder.Services.AddCogniJobsMediator(moduleApplicationAssemblies);
builder.Services.AddCogniJobsValidation(moduleApplicationAssemblies);

builder.Services.AddSingleton(new IntegrationEventTypeRegistry().RegisterAssemblies(moduleContractsAssemblies));

// Shared EF Core save-changes interceptor that dispatches domain events after commit (§6.1).
// Each module's own Add<Module>Module registers it against that module's DbContext.
builder.Services.AddScoped<DomainEventDispatchInterceptor>();

// The platform's single, shared real-time channel (§6.6) — hosted once, here, never per-module.
builder.Services.AddCogniJobsRealtime();

// The host's authentication middleware (BC-01-IAM-and-UAM.md §9.3): no scheme existed before
// this module's own build unit filled it in. Wraps IAM's frozen TokenValidationApi — every
// module's endpoints authenticate through this one scheme, never a module-specific one.
builder.Services.AddIamAuthentication();

// Enums serialize/deserialize as their string names (not ordinals) on every module's JSON
// request/response bodies — the conventional, frontend-friendly default; host-wide, not per-module.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

// Each module's own composition entry point: DbContext (in its own schema — see
// ModuleSchemas), repositories, port adapters, outbox relay hosted service, and inbound
// integration-event subscriptions. B0 scope: these are scaffolding stubs — real
// registrations land in each module's own later build unit.
// Frontend runs on a different origin/port than this API host (Next.js dev server at
// localhost:3000 vs. this host's localhost:5262, and equivalently distinct origins in any
// real deployment) — every browser-driven request is cross-origin. `withCredentials: true`
// on the frontend's axios client (it carries BC-1's httpOnly session/refresh cookie) means
// the CORS policy must name explicit origins and set AllowCredentials — a wildcard origin is
// rejected by the CORS spec whenever credentials are allowed.
var corsAllowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:3000"];

builder.Services.AddCors(options => options.AddPolicy("Frontend", policy => policy
    .WithOrigins(corsAllowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

// OpenAPI document (built-in generator) + a "Bearer" security scheme so Scalar's UI below can
// carry a token on every request — every module's endpoints authenticate through that one
// scheme (see AddIamAuthentication above), so this is declared once, host-wide, not per-module.
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info ??= new OpenApiInfo();
        document.Info.Title = "CogniJobs API";
        document.Info.Description = "The CogniJobs modular monolith — one HTTP surface across all 12 bounded-context modules.";

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Paste the access token returned by BC-1's login/refresh endpoints (no \"Bearer \" prefix needed).",
        };

        return Task.CompletedTask;
    });

    // Marks every operation as accepting the Bearer scheme (harmless for the handful of
    // genuinely anonymous endpoints — Scalar just offers an optional token for those too).
    options.AddOperationTransformer((operation, context, _) =>
    {
        operation.Security ??= new List<OpenApiSecurityRequirement>();
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = [],
        });

        return Task.CompletedTask;
    });
});

builder.Services.AddIamModule(builder.Configuration);


var app = builder.Build();

// No deploy tooling runs `dotnet ef database update` anywhere in this repo — each module's own
// schema is migrated here, at startup, once per DbContext. Idempotent (EF Core's migrations
// history table skips already-applied migrations), so this is safe to leave on for every
// environment: a fresh database (e.g. a newly provisioned RDS instance) gets its schema on first
// boot, and an already-migrated database is a no-op.
using (var migrationScope = app.Services.CreateScope())
{
    var migrationServices = migrationScope.ServiceProvider;
    await migrationServices.GetRequiredService<ScfPlatform.Modules.Iam.Infrastructure.Persistence.IamDbContext>().Database.MigrateAsync();

}

// Must run before auth so CORS preflight (OPTIONS) requests are answered without needing a
// bearer token/session cookie — otherwise every preflight 405s and no real request ever leaves
// the browser.
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "CogniJobs API");
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

// Dev-only: the raw OpenAPI document + Scalar's interactive UI for exercising every module's
// §12 HTTP contract by hand. Never exposed outside Development — this is a testing aid, not a
// shipped surface.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options
        .WithTitle("CogniJobs API")
        .WithTheme(ScalarTheme.Purple)
        .AddPreferredSecuritySchemes("Bearer"));
}

// The shared SignalR hub (§6.6) — one hub, one route, for every module's real-time push.
app.MapCogniJobsRealtimeHub();

// Each module's own §12 HTTP contract, mapped once here.
app.MapIamEndpoints();


app.Run();

// Exposed so WebApplicationFactory<Program>-based integration tests can boot the whole host.
public partial class Program;
