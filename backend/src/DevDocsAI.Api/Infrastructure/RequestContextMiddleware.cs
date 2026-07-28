using Microsoft.IdentityModel.JsonWebTokens;
using Serilog.Context;

namespace DevDocsAI.Api.Infrastructure;

/// <summary>Enriches every in-request log with a correlation id and (when authenticated) the user id.</summary>
public sealed class RequestContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var userId = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        using (LogContext.PushProperty("RequestId", context.TraceIdentifier))
        using (LogContext.PushProperty("UserId", userId ?? "anonymous"))
        {
            await next(context);
        }
    }
}
