using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;

namespace PulseAdmin.Auth;

public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    /// <summary>BattleTags autorisés (« Pseudo#1234 ») : tout autre compte Battle.net est refusé.</summary>
    public List<string> BattleTags { get; set; } = [];

    /// <summary>En développement seulement : ouvre le site sans connexion (pas de client Battle.net en local).</summary>
    public bool AllowAnonymousInDevelopment { get; set; }
}

/// <summary>
/// Connexion Battle.net, comme les trois sites, réservée aux BattleTags de <see cref="AdminOptions.BattleTags"/>.
/// Pas de compte ni de base pour les utilisateurs : seul le cookie porte l'identité.
/// </summary>
public static class AdminAuthentication
{
    public const string Scheme = "BattleNet";
    public const string Policy = "Admin";

    public static IServiceCollection AddAdminAuthentication(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var admin = configuration.GetSection(AdminOptions.SectionName).Get<AdminOptions>() ?? new AdminOptions();
        var anonymous = admin.AllowAnonymousInDevelopment && environment.IsDevelopment();

        services.AddAuthentication(o =>
            {
                o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                o.DefaultChallengeScheme = Scheme;
            })
            .AddCookie(o =>
            {
                o.Cookie.Name = "pulseadmin.auth";
                o.ExpireTimeSpan = TimeSpan.FromDays(7);
                o.SlidingExpiration = true;
                o.AccessDeniedPath = "/acces-refuse";
            })
            .AddOAuth(Scheme, "Battle.net", o =>
            {
                o.ClientId = configuration["Blizzard:ClientId"] ?? "";
                o.ClientSecret = configuration["Blizzard:ClientSecret"] ?? "";
                o.AuthorizationEndpoint = "https://oauth.battle.net/authorize";
                o.TokenEndpoint = "https://oauth.battle.net/token";
                o.UserInformationEndpoint = "https://oauth.battle.net/userinfo";
                o.CallbackPath = "/signin-battlenet";
                o.Scope.Add("openid");
                o.ClaimActions.MapCustomJson(ClaimTypes.NameIdentifier, user => user.GetProperty("id").GetRawText());
                o.ClaimActions.MapJsonKey(ClaimTypes.Name, "battletag");
                o.Events.OnCreatingTicket = async context =>
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
                    using var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
                    response.EnsureSuccessStatusCode();
                    using var user = JsonDocument.Parse(await response.Content.ReadAsStringAsync(context.HttpContext.RequestAborted));
                    context.RunClaimActions(user.RootElement);
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Policy, p => p.RequireAssertion(context =>
                anonymous
                || (context.User.Identity?.IsAuthenticated == true
                    && context.User.Identity.Name is { } tag
                    && admin.BattleTags.Contains(tag, StringComparer.OrdinalIgnoreCase))));

        services.AddCascadingAuthenticationState();
        return services;
    }

    public static IEndpointRouteBuilder MapAdminAccountEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/connexion", () => Results.Challenge(new AuthenticationProperties { RedirectUri = "/" }, [Scheme])).AllowAnonymous();
        app.MapGet("/deconnexion", async (HttpContext context) =>
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect("/acces-refuse");
        }).AllowAnonymous();
        return app;
    }
}
