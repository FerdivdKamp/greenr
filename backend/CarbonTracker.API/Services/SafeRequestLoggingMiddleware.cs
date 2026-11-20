using System.Diagnostics;
using Microsoft.AspNetCore.Routing;

namespace CarbonTracker.API.Services;

public sealed class SafeRequestLoggingMiddleware(RequestDelegate next, ILogger<SafeRequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
        }
        catch (Exception)
        {
            logger.LogError("Unhandled request error for {RequestId}", context.TraceIdentifier);
            if (context.Response.HasStarted)
                context.Abort();
            else
            {
                context.Response.Clear();
                await Results.Problem(statusCode: StatusCodes.Status500InternalServerError).ExecuteAsync(context);
            }
        }
        finally
        {
            // Never log raw paths, query strings, headers or request bodies.
            var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";
            var method = context.Request.Method;
            if (method is not ("GET" or "POST" or "PUT" or "PATCH" or "DELETE" or "HEAD" or "OPTIONS")) method = "OTHER";
            logger.LogInformation("HTTP {Method} {Route} responded {StatusCode} in {ElapsedMs} ms for {RequestId}",
                method, route, context.Response.StatusCode, Stopwatch.GetElapsedTime(started).TotalMilliseconds, context.TraceIdentifier);
        }
    }
}
