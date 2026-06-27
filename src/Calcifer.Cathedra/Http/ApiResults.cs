using Calcifer.Cathedra.Domain;
using Microsoft.AspNetCore.Http;

namespace Calcifer.Cathedra.Http;

public static class ApiResults
{
    /// <summary>Wrap a value as a successful envelope (200).</summary>
    public static IResult Ok<T>(HttpContext ctx, T data, string? message = null)
    {
        var resp = ApiResponse<T>.Ok(data, message);
        resp.CorrelationId = ctx.GetCorrelationId();
        return Microsoft.AspNetCore.Http.Results.Json(resp, statusCode: StatusCodes.Status200OK);
    }

    /// <summary>Convert a Result&lt;T&gt; into an enveloped response with a mapped status code.</summary>
    public static IResult From<T>(HttpContext ctx, Result<T> result,
        int successStatus = StatusCodes.Status200OK)
    {
        if (result.IsSuccess)
        {
            var ok = ApiResponse<T>.Ok(result.Value!);
            ok.CorrelationId = ctx.GetCorrelationId();
            return Microsoft.AspNetCore.Http.Results.Json(ok, statusCode: successStatus);
        }

        var fail = ApiResponse<T>.Fail(result.Error!.Code, result.Error.Message);
        fail.CorrelationId = ctx.GetCorrelationId();
        return Microsoft.AspNetCore.Http.Results.Json(fail, statusCode: MapStatus(result.Error.Code));
    }

    /// <summary>Default code→status convention. Modules can rely on these suffixes/keywords.</summary>
    public static int MapStatus(string code) => code switch
    {
        _ when code.EndsWith("NOT_FOUND")                                   => StatusCodes.Status404NotFound,
        _ when code.EndsWith("TAKEN") || code.EndsWith("CONFLICT")          => StatusCodes.Status409Conflict,
        _ when code.Contains("UNAUTH") || code.Contains("CREDENTIALS")
               || code.Contains("REFRESH")                                  => StatusCodes.Status401Unauthorized,
        _ when code.Contains("FORBIDDEN")                                   => StatusCodes.Status403Forbidden,
        _                                                                   => StatusCodes.Status400BadRequest,
    };
}
