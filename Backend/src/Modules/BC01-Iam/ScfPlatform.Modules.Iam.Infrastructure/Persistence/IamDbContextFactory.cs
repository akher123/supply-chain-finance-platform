using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ScfPlatform.Modules.Iam.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so <c>dotnet ef migrations add</c> can build <see cref="IamDbContext"/>
/// without running the full host. The connection string here is never used at runtime — the real
/// one comes from configuration via <c>AddIamModule</c>.
/// </summary>
public sealed class IamDbContextFactory : IDesignTimeDbContextFactory<IamDbContext>
{
    public IamDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("IAM_DESIGN_TIME_CONNECTION")
            ?? "Server=localhost;Database=bdjobs_design_time;User Id=bdjobs;Password=bdjobs;TrustServerCertificate=True";

        var optionsBuilder = new DbContextOptionsBuilder<IamDbContext>().UseSqlServer(connectionString);

        return new IamDbContext(optionsBuilder.Options);
    }
}
