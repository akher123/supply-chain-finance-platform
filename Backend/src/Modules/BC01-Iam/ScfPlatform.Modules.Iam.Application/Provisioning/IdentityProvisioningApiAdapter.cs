using MediatR;
using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Contracts;

namespace ScfPlatform.Modules.Iam.Application.Provisioning;

/// <summary>
/// The Infrastructure-registered implementation of this module's frozen <c>IIdentityProvisioningApi</c>
/// (Contracts §9.3) — a thin adapter translating the synchronous OHS call into a mediator dispatch,
/// so BC-2/BC-3 (which reference only this module's Contracts, not its Application types) get a
/// plain in-process C# call. Lives in Application (not Infrastructure) because it only needs the
/// mediator, already available here, and Contracts + Application only.
/// </summary>
public sealed class IdentityProvisioningApiAdapter : IIdentityProvisioningApi
{
    private readonly ISender _sender;

    public IdentityProvisioningApiAdapter(ISender sender)
    {
        _sender = sender;
    }

    public async Task<Result<ProvisionedIdentity>> ProvisionCredential(string email, string mobile, string password, string role)
    {
        var result = await _sender.Send(new ProvisionCredentialCommand(email, mobile, password, role));

        return result.IsSuccess
            ? Result.Success(new ProvisionedIdentity(result.Value.UserId))
            : Result.Failure<ProvisionedIdentity>(result.Error);
    }
}
