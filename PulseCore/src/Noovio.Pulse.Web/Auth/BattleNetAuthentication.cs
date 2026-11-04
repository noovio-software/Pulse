using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Noovio.Pulse.Application.Accounts;
using Noovio.Pulse.Domain.Roles;
using Noovio.Pulse.Infrastructure.Blizzard;

namespace Noovio.Pulse.Web.Auth;

/// <summary>
/// Connexion uniquement par Battle.net (OAuth « authorization code », scope wow.profile) puis cookie de session.
/// La première connexion crée le compte ; chaque connexion resynchronise ses personnages.
/// </summary>
public static class BattleNetAuthentication
{
    public const string Scheme = "BattleNet";

    /// <summary>Adresse de retour à déclarer dans le client Battle.net (develop.battle.net), pour chaque hôte.</summary>
    public const string CallbackPath = "/signin-battlenet";

    public static IServiceCollection AddBattleNetAuthentication(this IServiceCollection services, IConfiguration configuration, PulseApp app)
    {
        var blizzard = configuration.GetSection(BlizzardOptions.SectionName).Get<BlizzardOptions>() ?? new BlizzardOptions();

        services.AddAuthentication(o =>
            {
                o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                // Une page protégée renvoie directement vers Battle.net : il n'y a pas d'autre moyen de se connecter.
                o.DefaultChallengeScheme = Scheme;
            })
            .AddCookie(o =>
            {
                o.Cookie.Name = $"{app.Key}.auth";
                o.ExpireTimeSpan = TimeSpan.FromDays(30);
                o.SlidingExpiration = true;
            })
            .AddOAuth(Scheme, "Battle.net", o =>
            {
                o.ClientId = blizzard.ClientId ?? "";
                o.ClientSecret = blizzard.ClientSecret ?? "";
                o.AuthorizationEndpoint = "https://oauth.battle.net/authorize";
                o.TokenEndpoint = "https://oauth.battle.net/token";
                o.UserInformationEndpoint = "https://oauth.battle.net/userinfo";
                o.CallbackPath = CallbackPath;
                o.Scope.Add("openid");
                o.Scope.Add("wow.profile");

                // Le jeton utilisateur (24 h) reste dans le cookie chiffré : il sert au bouton « Synchroniser ».
                o.SaveTokens = true;

                o.ClaimActions.MapCustomJson(ClaimTypes.NameIdentifier, user => user.GetProperty("id").GetRawText());
                o.ClaimActions.MapJsonKey(ClaimTypes.Name, "battletag");
                o.Events.OnCreatingTicket = OnCreatingTicketAsync;
            });

        services.AddAuthorization();
        services.AddCascadingAuthenticationState();
        return services;
    }

    /// <summary>Identifiant Battle.net de l'utilisateur connecté (claim NameIdentifier).</summary>
    public static long? BattleNetId(this ClaimsPrincipal user) =>
        long.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;

    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var account = app.MapGroup("/compte");

        account.MapGet("/connexion", (string? returnUrl) =>
            Results.Challenge(new AuthenticationProperties { RedirectUri = LocalUrl(returnUrl) ?? "/compte" }, [Scheme]));

        account.MapGet("/deconnexion", async (HttpContext context) =>
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect("/");
        });

        account.MapGet("/synchroniser", async (HttpContext context, AccountSyncService sync, TimeProvider time, CancellationToken ct) =>
        {
            var reconnect = Results.Challenge(new AuthenticationProperties { RedirectUri = "/compte" }, [Scheme]);
            var token = await context.GetTokenAsync("access_token");
            var expiresAt = await context.GetTokenAsync("expires_at");
            if (token is null || context.User.BattleNetId() is not { } id
                || (DateTimeOffset.TryParse(expiresAt, CultureInfo.InvariantCulture, out var at) && at <= time.GetUtcNow()))
            {
                // Jeton expiré : se reconnecter resynchronise (Battle.net ne redemande pas l'accord déjà donné).
                return reconnect;
            }

            try
            {
                await sync.SyncAsync(id, token, ct);
            }
            catch (UnauthorizedAccessException)
            {
                return reconnect;
            }

            return Results.Redirect("/compte");
        }).RequireAuthorization();

        return app;
    }

    private static async Task OnCreatingTicketAsync(OAuthCreatingTicketContext context)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
        using var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
        response.EnsureSuccessStatusCode();

        using var user = JsonDocument.Parse(await response.Content.ReadAsStringAsync(context.HttpContext.RequestAborted));
        context.RunClaimActions(user.RootElement);

        if (context.Principal?.BattleNetId() is not { } id || context.AccessToken is null)
        {
            throw new InvalidOperationException("Réponse Battle.net sans identifiant de compte.");
        }

        var services = context.HttpContext.RequestServices;
        try
        {
            await services.GetRequiredService<AccountSyncService>()
                .SignInAsync(id, context.Principal.Identity?.Name ?? "", context.AccessToken, context.HttpContext.RequestAborted);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UnauthorizedAccessException)
        {
            // La connexion aboutit quand même : le compte existe, la liste se resynchronisera au prochain essai.
            services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(BattleNetAuthentication))
                .LogWarning(ex, "Synchronisation des personnages en échec à la connexion de {BattleNetId}", id);
        }
    }

    /// <summary>Retour uniquement vers une page du site (pas de redirection ouverte vers un autre domaine).</summary>
    private static string? LocalUrl(string? url) =>
        url is { Length: > 0 } && url[0] == '/' && !url.StartsWith("//", StringComparison.Ordinal) && !url.StartsWith("/\\", StringComparison.Ordinal)
            ? url
            : null;
}
