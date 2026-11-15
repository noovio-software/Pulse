using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Noovio.Pulse.Application.Resources;

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
            return Fail(AnalysisTexts.Import_Empty);
        }

        if (text.Length > MaxLength)
        {
            return Fail(AnalysisTexts.Import_TooLong);
        }

        var compact = new string(text.Where(c => !char.IsWhiteSpace(c)).ToArray());
        var start = compact.IndexOf(Prefix, StringComparison.OrdinalIgnoreCase);
        if (start < 0 || compact.Length < start + Prefix.Length + 2 || compact[start + Prefix.Length + 1] != ':')
        {
            return Fail(compact.StartsWith("NP", StringComparison.OrdinalIgnoreCase)
                ? AnalysisTexts.Import_UnknownVersion
                : AnalysisTexts.Import_NotNoovio);
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
            return Fail(AnalysisTexts.Import_Truncated);
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
            return Fail(AnalysisTexts.Import_Unreadable);
        }

        CharacterImport? import;
        try
        {
            import = JsonSerializer.Deserialize<CharacterImport>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return Fail(AnalysisTexts.Import_BadContent);
        }

        return Validate(import);
    }

    private static CharacterImportResult Validate(CharacterImport? import)
    {
        if (import is null || import.V < 1)
        {
            return Fail(AnalysisTexts.Import_BadContent);
        }

        if (import.V > 1)
        {
            return Fail(AnalysisTexts.Import_NewerAddon);
        }

        if (string.Equals(import.Kind, "alts", StringComparison.OrdinalIgnoreCase))
        {
            return Fail(AnalysisTexts.Import_AltsString);
        }

        if (import.Character is not { Name.Length: > 0, Class: > 0, Spec: > 0 })
        {
            return Fail(AnalysisTexts.Import_NoCharacter);
        }

        if (import.Gear is not { Count: > 0 })
        {
            return Fail(AnalysisTexts.Import_NoGear);
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
