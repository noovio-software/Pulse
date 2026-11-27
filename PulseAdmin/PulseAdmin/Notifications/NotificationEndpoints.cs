using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PulseAdmin.Auth;
using PulseAdmin.Stats;

namespace PulseAdmin.Notifications;

public sealed class ApiOptions
{
    public const string SectionName = "Api";

    /// <summary>
    /// Jeton des appels de service (le bot Discord), envoyé en <c>Authorization: Bearer</c>.
    /// Vide : l'API de résumé est fermée.
    /// </summary>
    public string Key { get; set; } = "";
}

public sealed record PushSubscriptionRequest(string Endpoint, string P256dh, string Auth);
public sealed record PushEndpointRequest(string Endpoint);

public static class NotificationEndpoints
{
    /// <summary>Au-delà d'un mois, le tableau de bord est plus parlant qu'un message.</summary>
    public const int MaxHours = 24 * 31;

    public static IServiceCollection AddNotifications(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<NotificationOptions>(configuration.GetSection(NotificationOptions.SectionName));
        services.Configure<ApiOptions>(configuration.GetSection(ApiOptions.SectionName));
        services.AddAdminDatabase(configuration);
        services.AddScoped<PushSender>();
        services.AddHostedService<DigestWorker>();
        return services;
    }

    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        // Appelés en JSON par app.js depuis une page de l'admin, avec le cookie de session. Pas de formulaire :
        // l'antiforgery ne s'applique pas, et le cookie (SameSite=Lax) ne part pas sur un POST venu d'un autre site.
        var push = app.MapGroup("/api/push").RequireAuthorization(AdminAuthentication.Policy).DisableAntiforgery();

        push.MapPost("/subscribe", async (PushSubscriptionRequest request, HttpContext context, AdminDbContext db, TimeProvider time, CancellationToken ct) =>
        {
            if (!Uri.TryCreate(request.Endpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme != Uri.UriSchemeHttps
                || request.Endpoint.Length > 1024 || string.IsNullOrWhiteSpace(request.P256dh) || string.IsNullOrWhiteSpace(request.Auth))
            {
                return Results.BadRequest();
            }

            var device = await db.PushDevices.SingleOrDefaultAsync(d => d.Endpoint == request.Endpoint, ct);
            if (device is null)
            {
                device = new PushDevice { Endpoint = request.Endpoint, CreatedAtUtc = time.GetUtcNow() };
                db.PushDevices.Add(device);
            }

            // Le navigateur peut changer les clés en gardant l'adresse : on garde toujours les dernières.
            device.P256dh = request.P256dh;
            device.Auth = request.Auth;
            device.Owner = context.User.Identity?.Name ?? "local";
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        push.MapPost("/unsubscribe", async (PushEndpointRequest request, AdminDbContext db, CancellationToken ct) =>
        {
            await db.PushDevices.Where(d => d.Endpoint == request.Endpoint).ExecuteDeleteAsync(ct);
            return Results.NoContent();
        });

        push.MapPost("/test", async (PushEndpointRequest request, AdminDbContext db, PushSender sender, UsageQueries queries, CancellationToken ct) =>
        {
            var device = await db.PushDevices.SingleOrDefaultAsync(d => d.Endpoint == request.Endpoint, ct);
            if (device is null)
            {
                return Results.NotFound();
            }

            var (today, _) = await queries.GetTodayAsync(ct);
            var message = DigestText.Build(today, "aujourd'hui") with { Tag = "pulseadmin-test" };
            return await sender.SendAsync(device, message, ct) ? Results.NoContent() : Results.StatusCode(StatusCodes.Status502BadGateway);
        });

        // Pour le bot Discord : le résumé d'une période, sur ?hours=X (les X dernières heures) ou ?from=…&to=… (ISO 8601).
        app.MapGet("/api/usage/summary", async (HttpContext context, IOptions<ApiOptions> api, UsageQueries queries, TimeProvider time, CancellationToken ct) =>
        {
            if (!Authorized(context, api.Value.Key))
            {
                return Results.Unauthorized();
            }

            var now = time.GetUtcNow();
            var query = context.Request.Query;
            DateTimeOffset from, to;
            if (query.ContainsKey("hours"))
            {
                if (!int.TryParse(query["hours"], NumberStyles.None, CultureInfo.InvariantCulture, out var hours) || hours is < 1 or > MaxHours)
                {
                    return Results.BadRequest($"hours : de 1 à {MaxHours}.");
                }
                (from, to) = (now.AddHours(-hours), now);
            }
            else if (DateTimeOffset.TryParse(query["from"], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out from)
                && DateTimeOffset.TryParse(query["to"], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out to)
                && from < to && to - from <= TimeSpan.FromHours(MaxHours))
            {
                to = to > now ? now : to;
            }
            else
            {
                return Results.BadRequest("hours, ou from et to (ISO 8601).");
            }

            return Results.Ok(await queries.GetDigestAsync(from.ToUniversalTime(), to.ToUniversalTime(), ct));
        }).AllowAnonymous().DisableAntiforgery();

        return app;
    }

    private static bool Authorized(HttpContext context, string key)
    {
        if (key.Length == 0 || context.Request.Headers.Authorization.ToString() is not { } header
            || !header.StartsWith("Bearer ", StringComparison.Ordinal))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(header["Bearer ".Length..].Trim()), Encoding.UTF8.GetBytes(key));
    }
}
