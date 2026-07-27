using DevDocsAI.Application.Abstractions.Security;
using Microsoft.IdentityModel.JsonWebTokens;

namespace DevDocsAI.Api.Security;

/// <summary>Resolves the authenticated user id from the request's JWT claims.</summary>
public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public bool IsAuthenticated =>
        accessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;

    public Guid? UserId
    {
        get
        {
            var value = accessor.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }
}
