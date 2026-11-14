using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace Noovio.Pulse.Application.Import;

/// <summary>
/// Lit la chaîne de l'addon NoovioPulse : « NP1:&lt;mode&gt;:&lt;base64&gt; ». Mode D : JSON compressé par le client
/// (deflate brut, zlib ou gzip : l'en-tête le dit) ; mode J : JSON en clair. Tolère les retours à la ligne et espaces du collage.
/// </summary>
public static class CharacterImportParser
{
    public const string Prefix = "NP1:";

    /// <summary>Taille maximale de la chaîne collée et du JSON décompressé : un export réel fait quelques dizaines de Ko.</summary>
    public const int MaxLength = 512 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
        MaxDepth = 16
    };

    public static CharacterImportResult Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Fail("Colle la chaîne copiée depuis l'addon NoovioPulse (commande /noovio en jeu).");
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
                : "Ce n'est pas une chaîne NoovioPulse : elle commence par « NP1: ». Une chaîne SimulationCraft ne convient pas.");
        }

        var mode = char.ToUpperInvariant(compact[start + Prefix.Length]);
        var payload = compact[(start + Prefix.Length + 2)..];

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(Pad(payload));
        }
        catch (FormatException)
        {
            return Fail("Chaîne tronquée ou modifiée : recopie-la entièrement depuis la fenêtre de l'addon (Ctrl+A, Ctrl+C).");
        }

        string json;
        try
        {
            json = mode switch
            {
                'J' => Encoding.UTF8.GetString(bytes),
                'D' => Inflate(bytes),
                _ => throw new InvalidDataException()
            };
        }
        catch (InvalidDataException)
        {
            return Fail("Chaîne illisible : recopie-la entièrement depuis la fenêtre de l'addon.");
        }

        CharacterImport? import;
        try
        {
            import = JsonSerializer.Deserialize<CharacterImport>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return Fail("Contenu illisible : mets l'addon NoovioPulse à jour puis refais l'export.");
        }

        return Validate(import);
    }

    private static CharacterImportResult Validate(CharacterImport? import)
    {
        if (import is null || import.V < 1)
        {
            return Fail("Contenu illisible : mets l'addon NoovioPulse à jour puis refais l'export.");
        }

        if (import.V > 1)
        {
            return Fail("Cet export vient d'une version de l'addon plus récente que le site : réessaie après la prochaine mise à jour du site.");
        }

        if (string.Equals(import.Kind, "alts", StringComparison.OrdinalIgnoreCase))
        {
            return Fail("Cette chaîne est l'avancement de tes alts : colle-la sur AltPulse (altpulse.noovio.fr), bouton « MàJ depuis l'addon ». Ici, choisis « Mon personnage » dans l'addon.");
        }

        if (import.Character is not { Name.Length: > 0, Class: > 0, Spec: > 0 })
        {
            return Fail("L'export ne contient pas de personnage : connecte-toi avec lui en jeu et refais /noovio.");
        }

        if (import.Gear is not { Count: > 0 })
        {
            return Fail("L'export ne contient aucun équipement : refais /noovio une fois en jeu, pas sur l'écran de sélection.");
        }

        return new CharacterImportResult(import, null);
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

    private static CharacterImportResult Fail(string error) => new(null, error);
}
