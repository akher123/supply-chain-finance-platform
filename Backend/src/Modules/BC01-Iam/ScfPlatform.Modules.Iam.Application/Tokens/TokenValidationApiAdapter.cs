using ScfPlatform.BuildingBlocks.Domain;
using ScfPlatform.Modules.Iam.Contracts;
using MediatR;

namespace ScfPlatform.Modules.Iam.Application.Tokens;

/// <summary>Adapter for this module's frozen <c>ITokenValidationApi</c> (Contracts §9.3) — used by the host's authentication middleware. See <see cref="Provisioning.IdentityProvisioningApiAdapter"/> for why this lives in Application.</summary>
public sealed class TokenValidationApiAdapter : ITokenValidationApi
{
    private readonly ISender _sender;

    public TokenValidationApiAdapter(ISender sender)
    {
        _sender = sender;
    }

    public async Task<Result<ValidatedPrincipal>> Validate(string accessToken)
    {
        var result = await _sender.Send(new ValidateTokenQuery(accessToken));

        return result.IsSuccess
            ? Result.Success(new ValidatedPrincipal(result.Value.UserId, result.Value.Role, result.Value.Permissions, result.Value.SessionId))
            : Result.Failure<ValidatedPrincipal>(result.Error);
    }
}
