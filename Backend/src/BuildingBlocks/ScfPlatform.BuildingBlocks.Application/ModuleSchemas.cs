namespace ScfPlatform.BuildingBlocks.Application;

/// <summary>
/// Canonical identity string for each of the 12 bounded-context modules: their Postgres
/// schema name (Handover_Packages/00-Shared-Foundations.md §6.4: "Each module owns its own
/// schema/namespace ... No foreign key crosses a module boundary") and, doubling up, their
/// keyed-DI service key (see <see cref="IInboxStore"/> — each module's <c>EfInboxStore</c>
/// is registered keyed by this string so the shared, non-generic <see cref="IInboxStore"/>
/// interface resolves to the correct module's store even when many modules share one host
/// container). Declared once, centrally, in the shared kernel (not Infrastructure) so both
/// Infrastructure module-registration code and Application-layer handler constructors can
/// reference the same constant instead of each worker inventing/duplicating its own —
/// avoids collisions/drift across parallel B1x/B2x units.
/// </summary>
public static class ModuleSchemas
{
    public const string Iam = "iam";
    public const string EmployerProfile = "employer_profile";
    public const string JobSeekerProfile = "jobseeker_profile";
    public const string JobPostings = "job_postings";
    public const string JobApplication = "job_application";
    public const string SearchDiscovery = "search_discovery";
    public const string Recommendation = "recommendation";
    public const string ExternalJobSync = "external_job_sync";
    public const string Notification = "notification";
    public const string Reporting = "reporting";
    public const string AdminConfiguration = "admin_configuration";
    public const string ContentManagement = "content_management";
}
