using Calcifer.Cathedra.Http;
using Calcifer.Public.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Calcifer.Public.Endpoints;

internal static class PublicEndpoints
{
    public static IEndpointRouteBuilder MapPublicEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/public").WithTags("Public");

        group.MapGet("/welcome", (HttpContext ctx, IPublicService svc) =>
            ApiResults.Ok(ctx, svc.GetWelcome()))
            .WithName("GetWelcome")
            .WithSummary("Welcome payload proving the Public module is alive.");

        group.MapGet("/multiply", async (HttpContext ctx, IPublicService svc,
    [FromQuery] int num1 = 1,    // default 1
    [FromQuery] int num2 = 1) => // default 1
        {
            var result = num1 * num2;
            return ApiResults.Ok(ctx, new { Number1 = num1, Number2 = num2, Result = result });
        })
        .WithName("Multiply")
        .WithSummary("Multiplies two numbers and returns the result.");

        group.MapGet("/time", (HttpContext ctx, IPublicService svc) =>
            ApiResults.Ok(ctx, new { utc = svc.GetServerTimeUtc() }))
            .WithName("GetServerTime")
            .WithSummary("Current server time in UTC.");

        // Deliberate failure to prove the global exception handler returns the same envelope.
        group.MapGet("/boom", () =>
        {
            throw new InvalidOperationException("Deliberate failure to test the global exception handler.");
        })
            .WithName("Boom")
            .WithSummary("Throws on purpose to demonstrate the uniform error envelope (HTTP 500).");

        return routes;
    }
}
