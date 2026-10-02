namespace ScfPlatform.BuildingBlocks.Infrastructure.Realtime;

/// <summary>The shared hub's group-naming convention (Foundations §6.6) — group-scoped by userId or role.</summary>
public static class HubGroups
{
    public static string ForUser(Guid userId) => $"user:{userId}";

    public static string ForRole(string role) => $"role:{role}";
}
