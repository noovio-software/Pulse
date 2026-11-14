using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AltPulse.Services;

/// <summary>
/// Lit l'export « alts » de l'addon NoovioPulse : « NP1:&lt;mode&gt;:&lt;base64&gt; ». Mode D : JSON compressé en jeu
/// (deflate brut, zlib ou gzip : l'en-tête le dit) ; mode J : JSON en clair. Même format que l'export du personnage
/// lu par FrontPulse, LifePulse et BurstPulse, qui porte « kind »: "character".
/// </summary>
public static class AddonExportParser
{
    public const string Prefix = "NP1:";

    /// <summary>Taille maximale de la chaîne collée et du JSON décompressé : un compte de 50 alts fait quelques dizaines de Ko.</summary>
    public const int MaxLength = 512 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        MaxDepth = 16
    };

    public static AddonAltsParseResult Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Fail("Colle la chaîne copiée depuis l'addon NoovioPulse (en jeu : /noovio, carte « Mes alts »).");
        }

        if (text.Length > MaxLength)
        {
            return Fail("Chaîne trop longue : ce n'est pas un export NoovioPulse.");
        }

        var compact = new string(text.Where(c => !char.IsWhiteSpace(c)).ToArray());
        var start = compact.IndexOf(Prefix, StringComparison.OrdinalIgnoreCase);
        if (start < 0 || compact.Length < start + Prefix.Length + 2 || compact[start + Prefix.Length + 1] != ':')
        {
            return Fail(compact.StartsWith("NP", StringComparison.OrdinalIgnoreCase)
                ? "Version de l'addon trop récente pour ce site, ou chaîne incomplète : recopie-la depuis le jeu."
                : "Ce n'est pas une chaîne NoovioPulse : elle commence par « NP1: ».");
        }

        var mode = char.ToUpperInvariant(compact[start + Prefix.Length]);
        var payload = compact[(start + Prefix.Length + 2)..];

        string json;
        try
        {
            var bytes = Convert.FromBase64String(Pad(payload));
            json = mode switch
            {
                'J' => Encoding.UTF8.GetString(bytes),
                'D' => Inflate(bytes),
                _ => throw new InvalidDataException()
            };
        }
        catch (Exception ex) when (ex is FormatException or InvalidDataException)
        {
            return Fail("Chaîne tronquée ou modifiée : recopie-la entièrement depuis la fenêtre de l'addon (Ctrl+C).");
        }

        AddonAltsExport? export;
        try
        {
            export = JsonSerializer.Deserialize<AddonAltsExport>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return Fail("Contenu illisible : mets l'addon NoovioPulse à jour puis refais l'export.");
        }

        if (export is null || export.V < 1)
        {
            return Fail("Contenu illisible : mets l'addon NoovioPulse à jour puis refais l'export.");
        }

        if (export.V > 1)
        {
            return Fail("Version de l'addon trop récente pour ce site : AltPulse sera mis à jour sous peu.");
        }

        if (!string.Equals(export.Kind, "alts", StringComparison.OrdinalIgnoreCase))
        {
            return Fail("Cette chaîne est l'analyse d'un personnage : colle-la sur FrontPulse, LifePulse ou BurstPulse. Pour AltPulse, choisis « Mes alts » dans l'addon.");
        }

        var characters = (export.Characters ?? [])
            .Where(c => !string.IsNullOrWhiteSpace(c.Name) && !string.IsNullOrWhiteSpace(c.Realm))
            .ToList();
        return characters.Count == 0
            ? Fail("Aucun personnage dans l'export : connecte-toi une fois sur chaque alt avec l'addon activé.")
            : new AddonAltsParseResult(characters, null);
    }

    private static string Pad(string base64) => (base64.Length % 4) switch
    {
        2 => base64 + "==",
        3 => base64 + "=",
        _ => base64
    };

    /// <summary>Deflate brut, ou zlib (0x78…) ou gzip (0x1F 0x8B) selon l'en-tête ; borné à <see cref="MaxLength"/>.</summary>
    private static string Inflate(byte[] bytes)
    {
        using var input = new MemoryStream(bytes);
        using Stream stream = bytes switch
        {
            [0x1F, 0x8B, ..] => new GZipStream(input, CompressionMode.Decompress),
            [0x78, var flags, ..] when ((0x78 << 8) | flags) % 31 == 0 => new ZLibStream(input, CompressionMode.Decompress),
            _ => new DeflateStream(input, CompressionMode.Decompress)
        };

        using var output = new MemoryStream();
        var buffer = new byte[16 * 1024];
        int read;
        while ((read = stream.Read(buffer)) > 0)
        {
            output.Write(buffer, 0, read);
            if (output.Length > MaxLength)
            {
                throw new InvalidDataException("Export trop volumineux.");
            }
        }

        return Encoding.UTF8.GetString(output.ToArray());
    }

    private static AddonAltsParseResult Fail(string error) => new([], error);
}

public sealed record AddonAltsParseResult(IReadOnlyList<AddonAltsCharacter> Characters, string? Error);

/// <summary>JSON de l'addon (snake_case), tel qu'écrit par Alts.lua.</summary>
public sealed record AddonAltsExport(int V, string? Kind, string? Addon, long ExportedAt, List<AddonAltsCharacter>? Characters);

public sealed record AddonAltsCharacter(
    string? Name,
    string? Realm,
    string? Region,
    int Class,
    string? Race,
    string? Faction,
    int Level,
    int? Spec,
    string? SpecName,
    string? Role,
    AddonAltsItemLevel? Ilvl,
    double Rating,
    string? RatingColor,
    AddonAltsKeystone? Keystone,
    long? WeekStart,
    bool HasRewards,
    List<AddonAltsVault>? Vault,
    List<AddonAltsRun>? Runs,
    long SavedAt);

public sealed record AddonAltsItemLevel(double Equipped, double Overall);

public sealed record AddonAltsKeystone(int Map, string? MapName, int Level);

public sealed record AddonAltsVault(int Type, int Index, int Threshold, int Progress, int Level);

public sealed record AddonAltsRun(int Map, string? MapName, int Level, bool Completed);
