using Noovio.FrontPulse.Application.Abstractions;

namespace Noovio.FrontPulse.Web.Auth;

/// <summary>
/// API d'administration : déclenchement d'une ingestion hors calendrier et suivi des dernières exécutions.
/// <code>curl -X POST https://frontpulse.noovio.fr/api/admin/ingestion -H "X-Admin-Key: …"</code>
/// </summary>
public static class AdminEndpoints
{
    public static IServiceCollection AddFrontPulseAdmin(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AdminOptions>(configuration.GetSection(AdminOptions.SectionName));
        services.AddScoped<AdminAccess>();
        return services;
    }

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin").DisableAntiforgery();

        admin.MapPost("/ingestion", async (HttpContext context, AdminAccess access, IIngestionRequestStore requests, CancellationToken ct) =>
        {
            if (access.Requester(context) is not { } requester)
            {
                return Results.Unauthorized();
            }

            var request = await requests.RequestAsync(requester, ct);
            return Results.Accepted("/api/admin/ingestion", new
            {
                request.Id,
                Status = request.Status.ToString(),
                request.RequestedAtUtc,
                Message = "Demande enregistrée : le worker la lance sous 30 s (environ 30 min d'ingestion)."
            });
        });

        admin.MapGet("/ingestion", async (HttpContext context, AdminAccess access, IIngestionRequestStore requests, CancellationToken ct) =>
            access.Requester(context) is null
                ? Results.Unauthorized()
                : Results.Ok((await requests.GetRecentAsync(10, ct)).Select(r => new
                {
                    r.Id,
                    Status = r.Status.ToString(),
                    r.RequestedBy,
                    r.RequestedAtUtc,
                    r.StartedAtUtc,
                    r.CompletedAtUtc,
                    r.Message
                })));

        return app;
    }
}
