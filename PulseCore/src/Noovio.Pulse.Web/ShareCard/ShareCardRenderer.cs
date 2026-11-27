using System.Globalization;
using Noovio.Pulse.Application.Abstractions;
using Noovio.Pulse.Application.Analysis;
using Noovio.Pulse.Domain;
using Noovio.Pulse.Domain.Enums;
using Noovio.Pulse.Domain.Roles;
using Noovio.Pulse.Web.Components.Shared;
using Noovio.Pulse.Web.Resources;
using SkiaSharp;

namespace Noovio.Pulse.Web.ShareCard;

/// <summary>
/// Vignette d'un lien partagé (1200×630, format des aperçus Discord, X, WhatsApp…) : le personnage, son score,
/// ses chiffres clés et le verdict de chaque domaine, aux couleurs de sa classe. Dessinée sans navigateur, avec les
/// polices du site embarquées : rien à installer sur le serveur.
/// </summary>
public sealed class ShareCardRenderer(IHttpClientFactory http, PulseApp site, ILogger<ShareCardRenderer> logger)
{
    public const string HttpClientName = "share-card";
    public const int Width = 1200;
    public const int Height = 630;

    private const float Pad = 64;

    private static readonly SKTypeface Medium = LoadFont("Inter-Medium.ttf");
    private static readonly SKTypeface Bold = LoadFont("Inter-Bold.ttf");
    private static readonly SKTypeface Title = LoadFont("Cinzel-Bold.ttf");

    private static readonly SKColor Background = SKColor.Parse("#0B0E14");
    private static readonly SKColor Panel = SKColor.Parse("#111622");
    private static readonly SKColor Line = SKColor.Parse("#2A364F");
    private static readonly SKColor Text = SKColor.Parse("#E5E7EB");
    private static readonly SKColor Dim = SKColor.Parse("#9CA3AF");
    private static readonly SKColor Faint = SKColor.Parse("#6B7280");
    private static readonly SKColor Good = SKColor.Parse("#34D399");
    private static readonly SKColor Improve = SKColor.Parse("#FBBF24");
    private static readonly SKColor Problem = SKColor.Parse("#F87171");

    /// <summary>
    /// JPEG plutôt que PNG : environ trois fois plus léger. Les applications de messagerie qui construisent l'aperçu
    /// elles-mêmes (Messenger, WhatsApp) écartent les images lourdes (au-delà de 300 Ko environ).
    /// </summary>
    public const string ContentType = "image/jpeg";

    /// <summary>Adresse relative de la vignette dans une langue (« partage/abc/vignette-fr.jpg »).</summary>
    public static string Path(string token, string language) => $"partage/{token}/vignette-{language}.jpg";

    /// <summary>« 1 problème », « 3 problèmes ».</summary>
    public static string Problems(int count) => Ui.F(count > 1 ? WebTexts.Card_ProblemsMany : WebTexts.Card_ProblemsOne, count);

    /// <summary>Dessine la vignette dans la culture courante (celle de la requête).</summary>
    public async Task<byte[]> RenderAsync(SharedAnalysis shared, CancellationToken ct)
    {
        var a = shared.Analysis;
        var c = a.Character;
        using var portrait = await DownloadAsync(FullBodyUrl(c.PortraitUrl), ct) ?? await DownloadAsync(c.PortraitUrl, ct);
        using var avatar = await DownloadAsync(c.AvatarUrl, ct);

        var classColor = SKColor.TryParse(a.Spec.Color, out var parsed) ? parsed : Text;

        using var surface = SKSurface.Create(new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;

        DrawBackground(canvas, classColor, portrait);
        DrawBrand(canvas);
        var textLeft = DrawAvatar(canvas, avatar, classColor);
        DrawIdentity(canvas, shared, textLeft, classColor);
        DrawScore(canvas, a.Score);
        DrawAreas(canvas, a.Areas);
        DrawFooter(canvas, a);

        using var image = surface.Snapshot();
        using var jpeg = image.Encode(SKEncodedImageFormat.Jpeg, 85);
        return jpeg.ToArray();
    }

    private static void DrawBackground(SKCanvas canvas, SKColor classColor, SKImage? portrait)
    {
        canvas.Clear(Background);

        using (var glow = new SKPaint())
        {
            glow.Shader = SKShader.CreateRadialGradient(new SKPoint(0, 0), 950, [classColor.WithAlpha(70), classColor.WithAlpha(0)], SKShaderTileMode.Clamp);
            canvas.DrawRect(0, 0, Width, Height, glow);
        }

        // Le personnage en pied à droite, fondu vers la gauche pour laisser le texte lisible (comme l'en-tête du rapport).
        if (portrait is not null)
        {
            var scale = Math.Max((float)Height / portrait.Height, 560f / portrait.Width);
            var w = portrait.Width * scale;
            var h = portrait.Height * scale;
            var dest = SKRect.Create(Width - w + w * 0.08f, (Height - h) / 2, w, h);

            canvas.SaveLayer();
            using (var image = new SKPaint { Color = SKColors.White.WithAlpha(110) })
            {
                canvas.DrawImage(portrait, dest, new SKSamplingOptions(SKCubicResampler.Mitchell), image);
            }
            using (var fade = new SKPaint { BlendMode = SKBlendMode.DstIn })
            {
                fade.Shader = SKShader.CreateLinearGradient(new SKPoint(520, 0), new SKPoint(900, 0), [SKColors.Transparent, SKColors.Black], SKShaderTileMode.Clamp);
                canvas.DrawRect(0, 0, Width, Height, fade);
            }
            canvas.Restore();
        }

        using var bar = new SKPaint { Color = classColor, IsAntialias = true };
        canvas.DrawRect(0, 0, 8, Height, bar);
    }

    private void DrawBrand(SKCanvas canvas)
    {
        // Même emblème que le favicon : le tracé de l'application et le pouls commun, viewBox 32×32.
        canvas.Save();
        canvas.Translate(Pad, 46);
        canvas.Scale(40f / 32);
        using (var stroke = new SKPaint { Color = Text, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2, StrokeJoin = SKStrokeJoin.Round, StrokeCap = SKStrokeCap.Round })
        {
            if (SKPath.ParseSvgPathData(site.Sigil) is { } sigil)
            {
                canvas.DrawPath(sigil, stroke);
                sigil.Dispose();
            }
            using var pulse = SKPath.ParseSvgPathData("M9 16h3.5l2-4 3 8 2-4H23");
            canvas.DrawPath(pulse, stroke);
        }
        canvas.Restore();

        using var name = new SKFont(Bold, 28);
        using var by = new SKFont(Medium, 20);
        DrawText(canvas, site.DisplayName, Pad + 54, 76, name, Text);
        DrawText(canvas, "PulseMetrics", Pad + 54 + name.MeasureText(site.DisplayName) + 10, 76, by, Faint);
    }

    /// <returns>Abscisse du texte d'identité, à droite de l'avatar s'il y en a un.</returns>
    private static float DrawAvatar(SKCanvas canvas, SKImage? avatar, SKColor classColor)
    {
        if (avatar is null)
        {
            return Pad;
        }

        const float size = 136;
        var rect = SKRect.Create(Pad, 150, size, size);
        using var clip = new SKRoundRect(rect, 22);
        canvas.Save();
        canvas.ClipRoundRect(clip, antialias: true);
        canvas.DrawImage(avatar, rect, new SKSamplingOptions(SKCubicResampler.Mitchell));
        canvas.Restore();

        using var border = new SKPaint { Color = classColor, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 4 };
        canvas.DrawRoundRect(clip, border);
        return Pad + size + 30;
    }

    private static void DrawIdentity(SKCanvas canvas, SharedAnalysis shared, float left, SKColor classColor)
    {
        var a = shared.Analysis;
        var c = a.Character;
        const float right = 820;

        using var eyebrow = new SKFont(Medium, 22);
        var realm = string.IsNullOrEmpty(shared.RealmName) ? c.Region.ToUpperInvariant() : $"{shared.RealmName} ({c.Region.ToUpperInvariant()})";
        DrawText(canvas, Ellipsize($"{a.Spec.SpecName} {a.Spec.ClassName} · {realm}", eyebrow, right - left), left, 182, eyebrow, Dim);

        using var name = new SKFont(Title, 84);
        while (name.Size > 44 && name.MeasureText(c.Name) > right - left)
        {
            name.Size -= 4;
        }
        using (var glow = new SKPaint { Color = classColor.WithAlpha(90), IsAntialias = true, ImageFilter = SKImageFilter.CreateBlur(14, 14) })
        {
            canvas.DrawText(Clean(c.Name), left, 268, SKTextAlign.Left, name, glow);
        }
        DrawText(canvas, c.Name, left, 268, name, classColor);

        // Pastilles : niveau d'objet face au top, score M+, arbre héroïque.
        var x = Pad;
        const float y = 318;
        var ilvlDelta = c.Loadout.ItemLevel - a.Meta.MedianItemLevel;
        x = DrawChip(canvas, x, y, WebTexts.Report_ItemLevel, Ui.Ilvl(c.Loadout.ItemLevel), Text,
            Ui.F(WebTexts.Report_VsTop, ilvlDelta.ToString("+0.#;-0.#;0", CultureInfo.CurrentCulture)), ilvlDelta >= 0 ? Good : Problem);
        if (c.MythicPlus is { } m)
        {
            var color = SKColor.TryParse(m.ScoreColor, out var scoreColor) ? scoreColor : Text;
            x = DrawChip(canvas, x, y, WebTexts.Report_ScoreMplus, m.Score.ToString("N0", CultureInfo.CurrentCulture), color, null, default);
        }
        if (a.Talents.HeroTree is { } hero)
        {
            x = DrawChip(canvas, x, y, WebTexts.Card_Hero, hero, Text, null, default);
        }
        if (!a.PlaysSpec)
        {
            var played = c.ActiveSpecId is { } id && Enum.IsDefined((WowSpec)id) ? ((WowSpec)id).Info().SpecName : c.ActiveSpecName ?? WebTexts.Report_OtherSpec;
            DrawChip(canvas, x, y, WebTexts.Card_PlayedAs, played, Improve, null, default);
        }
    }

    /// <returns>Abscisse de la pastille suivante.</returns>
    private static float DrawChip(SKCanvas canvas, float x, float y, string label, string value, SKColor valueColor, string? extra, SKColor extraColor)
    {
        using var labelFont = new SKFont(Bold, 15);
        using var valueFont = new SKFont(Bold, 26);
        using var extraFont = new SKFont(Bold, 18);
        var labelText = label.ToUpperInvariant();

        var width = 22 + labelFont.MeasureText(labelText) + 10 + valueFont.MeasureText(Clean(value)) + 22;
        if (extra is not null)
        {
            width += 10 + extraFont.MeasureText(Clean(extra));
        }
        if (x + width > ScoreLeft - 20 && x > Pad)
        {
            return x; // Plus de place avant le score : la pastille est omise plutôt que de chevaucher l'anneau.
        }

        var rect = SKRect.Create(x, y, width, 54);
        using (var fill = new SKPaint { Color = Panel.WithAlpha(215), IsAntialias = true })
        {
            canvas.DrawRoundRect(rect, 27, 27, fill);
        }
        using (var border = new SKPaint { Color = Line, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f })
        {
            canvas.DrawRoundRect(rect, 27, 27, border);
        }

        var cx = x + 22;
        const float baselineOffset = 36;
        DrawText(canvas, labelText, cx, y + baselineOffset - 2, labelFont, Faint);
        cx += labelFont.MeasureText(labelText) + 10;
        DrawText(canvas, value, cx, y + baselineOffset, valueFont, valueColor);
        cx += valueFont.MeasureText(Clean(value)) + 10;
        if (extra is not null)
        {
            DrawText(canvas, extra, cx, y + baselineOffset - 1, extraFont, extraColor);
        }

        return x + width + 12;
    }

    private const float RingCenterX = 1010;
    private const float RingCenterY = 235;
    private const float RingRadius = 112;
    private const float ScoreLeft = RingCenterX - RingRadius - 30;

    private static void DrawScore(SKCanvas canvas, int score)
    {
        var color = score switch { >= 75 => Good, >= 50 => Improve, _ => Problem };
        var bounds = new SKRect(RingCenterX - RingRadius, RingCenterY - RingRadius, RingCenterX + RingRadius, RingCenterY + RingRadius);

        using (var disc = new SKPaint { Color = Background.WithAlpha(200), IsAntialias = true })
        {
            canvas.DrawCircle(RingCenterX, RingCenterY, RingRadius + 14, disc);
        }
        using (var track = new SKPaint { Color = Line.WithAlpha(180), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 18 })
        {
            canvas.DrawOval(bounds, track);
        }
        using (var arc = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 18, StrokeCap = SKStrokeCap.Round })
        {
            arc.ImageFilter = SKImageFilter.CreateDropShadow(0, 0, 10, 10, color.WithAlpha(120));
            canvas.DrawArc(bounds, -90, 360f * Math.Clamp(score, 0, 100) / 100, false, arc);
        }

        using var number = new SKFont(Bold, 92);
        using var outOf = new SKFont(Medium, 22);
        using var label = new SKFont(Bold, 16);
        canvas.DrawText(score.ToString(CultureInfo.InvariantCulture), RingCenterX, RingCenterY + 26, SKTextAlign.Center, number, Fill(Text));
        canvas.DrawText("/ 100", RingCenterX, RingCenterY + 60, SKTextAlign.Center, outOf, Fill(Faint));
        canvas.DrawText(WebTexts.Report_MetaCompliance.ToUpper(CultureInfo.CurrentCulture),RingCenterX, RingCenterY + RingRadius + 46, SKTextAlign.Center, label, Fill(Dim));
    }

    private static void DrawAreas(SKCanvas canvas, IReadOnlyList<AnalysisArea> areas)
    {
        if (areas.Count == 0)
        {
            return;
        }

        // Deux rangées de quatre : sur une seule, les résumés (« 3/4 dans la fourchette ») ne tiennent pas.
        const float top = 410;
        const float height = 56;
        const float gap = 12;
        var columns = (int)Math.Ceiling(areas.Count / 2.0);
        var width = (Width - 2 * Pad - gap * (columns - 1)) / columns;

        using var labelFont = new SKFont(Bold, 13);
        using var summaryFont = new SKFont(Bold, 19);
        for (var i = 0; i < areas.Count; i++)
        {
            var area = areas[i];
            var x = Pad + i % columns * (width + gap);
            var y = top + i / columns * (height + gap);
            var verdict = area.Verdict switch { Verdict.Good => Good, Verdict.Improve => Improve, _ => Problem };
            var rect = SKRect.Create(x, y, width, height);

            canvas.Save();
            canvas.ClipRoundRect(new SKRoundRect(rect, 12), antialias: true);
            using (var fill = new SKPaint { Color = Panel.WithAlpha(230) })
            {
                canvas.DrawRect(rect, fill);
            }
            using (var stripe = new SKPaint { Color = verdict })
            {
                canvas.DrawRect(SKRect.Create(x, y, 5, height), stripe);
            }
            canvas.Restore();

            var label = Ui.AreaLabel(area.Kind).ToUpperInvariant();
            DrawText(canvas, label, x + 20, y + 22, labelFont, Dim);
            DrawText(canvas, Ellipsize(area.Summary, summaryFont, width - 36), x + 20, y + 45, summaryFont, verdict == Good ? Text : verdict);
        }
    }

    private static void DrawFooter(SKCanvas canvas, CharacterAnalysis a)
    {
        using var font = new SKFont(Medium, 20);
        DrawText(canvas, Ui.F(WebTexts.Shared_AnalyzedOn, Ui.Date(a.AnalyzedAtUtc)), Pad, 588, font, Faint);

        var problems = a.Findings.Count(f => f.Verdict == Verdict.Problem);
        var improve = a.Findings.Count(f => f.Verdict == Verdict.Improve);
        var good = a.Findings.Count(f => f.Verdict == Verdict.Good);
        var parts = new (string Text, SKColor Color)[]
        {
            (Problems(problems), Problem),
            (Ui.F(WebTexts.Card_Improve, improve), Improve),
            (Ui.F(good > 1 ? WebTexts.Card_StrengthsMany : WebTexts.Card_StrengthsOne, good), Good)
        };

        using var bold = new SKFont(Bold, 20);
        var x = Width - Pad;
        for (var i = parts.Length - 1; i >= 0; i--)
        {
            x -= bold.MeasureText(parts[i].Text);
            DrawText(canvas, parts[i].Text, x, 588, bold, parts[i].Color);
            if (i > 0)
            {
                x -= font.MeasureText("  ·  ");
                DrawText(canvas, "  ·  ", x, 588, font, Faint);
            }
        }
    }

    /// <summary>L'image « inset » de Blizzard est un petit cadrage ; « main-raw », au même emplacement, est le personnage en pied détouré.</summary>
    private static string? FullBodyUrl(string? insetUrl) =>
        insetUrl is not null && insetUrl.Contains("-inset.", StringComparison.Ordinal)
            ? insetUrl[..insetUrl.LastIndexOf("-inset.", StringComparison.Ordinal)] + "-main-raw.png"
            : null;

    private async Task<SKImage?> DownloadAsync(string? url, CancellationToken ct)
    {
        if (url is null || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        try
        {
            var bytes = await http.CreateClient(HttpClientName).GetByteArrayAsync(uri, ct);
            return SKImage.FromEncodedData(bytes);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Sans image, la vignette reste lisible : on la dessine sans.
            logger.LogInformation("Image de vignette indisponible ({Url}) : {Error}", url, ex.Message);
            return null;
        }
    }

    private static void DrawText(SKCanvas canvas, string text, float x, float y, SKFont font, SKColor color)
    {
        using var paint = Fill(color);
        canvas.DrawText(Clean(text), x, y, SKTextAlign.Left, font, paint);
    }

    private static SKPaint Fill(SKColor color) => new() { Color = color, IsAntialias = true };

    /// <summary>Les espaces insécables fines de la culture française (« 3 120 ») ne sont pas dans le sous-ensemble des polices.</summary>
    private static string Clean(string text) => text.Replace(' ', ' ').Replace(' ', ' ');

    private static string Ellipsize(string text, SKFont font, float maxWidth)
    {
        text = Clean(text);
        if (font.MeasureText(text) <= maxWidth)
        {
            return text;
        }

        while (text.Length > 1 && font.MeasureText(text + "…") > maxWidth)
        {
            text = text[..^1];
        }
        return text.TrimEnd() + "…";
    }

    private static SKTypeface LoadFont(string file)
    {
        using var stream = typeof(ShareCardRenderer).Assembly.GetManifestResourceStream($"ShareCard.{file}")
                           ?? throw new InvalidOperationException($"Police {file} absente des ressources");
        using var data = SKData.Create(stream);
        return SKTypeface.FromData(data) ?? throw new InvalidOperationException($"Police {file} illisible");
    }
}
