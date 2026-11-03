using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Noovio.FrontPulse.Web.Auth;

public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    /// <summary>BattleTags autorisés sur /admin (connexion Battle.net), ex. « Pseudo#1234 ».</summary>
    public List<string> BattleTags { get; set; } = [];

    /// <summary>Clé pour les appels en ligne de commande (en-tête X-Admin-Key). Vide : accès API désactivé.</summary>
    public string? ApiKey { get; set; }
}

/// <summary>Accès d'administration : un BattleTag de la liste (navigateur) ou la clé d'admin (curl).</summary>
public sealed class AdminAccess(IOptions<AdminOptions> options)
{
    public const string ApiKeyHeader = "X-Admin-Key";

    private readonly AdminOptions _options = options.Value;

    public bool IsAdmin(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true
        && user.Identity.Name is { } battleTag
        && _options.BattleTags.Contains(battleTag, StringComparer.OrdinalIgnoreCase);

    /// <summary>Qui demande l'action : le BattleTag de l'admin, « api » pour la clé, null si refusé.</summary>
    public string? Requester(HttpContext context)
    {
        if (IsAdmin(context.User))
        {
            return context.User.Identity!.Name;
        }

        return _options.ApiKey is { Length: >= 16 } key
               && context.Request.Headers[ApiKeyHeader].ToString() is { Length: > 0 } given
               // Comparaison à temps constant : la durée ne trahit pas les caractères justes.
               && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(given), Encoding.UTF8.GetBytes(key))
            ? "api"
            : null;
    }
}
