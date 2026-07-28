using DevDocsAI.Api.Infrastructure;
using DevDocsAI.Application.Common.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace DevDocsAI.IntegrationTests;

public sealed class GlobalExceptionHandlerTests
{
    private static (GlobalExceptionHandler Handler, Func<ProblemDetailsContext?> Captured) Build()
    {
        ProblemDetailsContext? captured = null;
        var pds = Substitute.For<IProblemDetailsService>();
        pds.TryWriteAsync(Arg.Do<ProblemDetailsContext>(c => captured = c)).Returns(ValueTask.FromResult(true));
        return (new GlobalExceptionHandler(pds, NullLogger<GlobalExceptionHandler>.Instance), () => captured);
    }

    [Fact]
    public async Task Unexpected_exception_is_a_generic_500_with_no_detail()
    {
        var (handler, captured) = Build();
        var ctx = new DefaultHttpContext();

        await handler.TryHandleAsync(ctx, new InvalidOperationException("secret internals"), default);

        ctx.Response.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
        captured()!.ProblemDetails.Detail.ShouldBeNull();
        captured()!.ProblemDetails.Extensions.ShouldContainKey("traceId");
    }

    [Fact]
    public async Task Not_found_maps_to_404_with_message()
    {
        var (handler, captured) = Build();
        var ctx = new DefaultHttpContext();

        await handler.TryHandleAsync(ctx, new NotFoundException("Project not found."), default);

        ctx.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        captured()!.ProblemDetails.Detail.ShouldBe("Project not found.");
    }
}
