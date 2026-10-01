using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace BudgetApi.Endpoints;

public static class HealthEndpoints
{
    // Mapped on the app itself, outside the route groups that call RequireAuthorization(),
    // so a load balancer or container platform can call them without a token.
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        // Runs every registered check.
        app.MapHealthChecks("/health");

        // Readiness: can the app serve requests? Runs only the checks tagged "ready" (the database).
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready")
        });

        // Liveness: is the process running? Runs no checks, so it answers
        // Healthy as long as the app can handle a request.
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false
        });

        return app;
    }
}
