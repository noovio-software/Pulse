using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using Noovio.Pulse.Application.Import;
using Noovio.Pulse.Application.Resources;
using Noovio.Pulse.Domain;
using Noovio.Pulse.Domain.Enums;
using Noovio.Pulse.Domain.Resources;
using Noovio.Pulse.Domain.Roles;
using Noovio.Pulse.Web.Localization;
using Noovio.Pulse.Web.Resources;

namespace Noovio.Pulse.Tests;

/// <summary>Chaque texte existe en français et en anglais, avec les mêmes paramètres : un oubli se voit ici, pas en production.</summary>
public partial class LocalizationTests
{
    public static TheoryData<string> Resources => [nameof(GameTexts), nameof(AnalysisTexts), nameof(WebTexts)];

    [Theory]
    [MemberData(nameof(Resources))]
    public void English_has_every_french_text_with_the_same_placeholders(string resource)
    {
        var manager = Manager(resource);
        var french = Entries(manager, CultureInfo.InvariantCulture);
        var english = Entries(manager, new CultureInfo("en"));

        Assert.NotEmpty(french);
        Assert.Empty(french.Keys.Except(english.Keys));
        Assert.Empty(english.Keys.Except(french.Keys));
        foreach (var (key, text) in french)
        {
            Assert.True(Placeholders(text).SetEquals(Placeholders(english[key])), $"{resource}.{key} : paramètres différents entre les langues.");
            Assert.False(string.IsNullOrWhiteSpace(english[key]) && !string.IsNullOrWhiteSpace(text), $"{resource}.{key} : texte anglais vide.");
        }
    }

    [Theory]
    [MemberData(nameof(Resources))]
    public void Every_format_string_formats_in_both_languages(string resource)
    {
        var manager = Manager(resource);
        foreach (var culture in new[] { "fr", "en" })
        {
            using var _ = TestCulture.Use(culture);
            foreach (var (key, text) in Entries(manager, CultureInfo.InvariantCulture))
            {
                var format = manager.GetString(key, CultureInfo.CurrentUICulture)!;
                var arguments = Enumerable.Repeat<object>(1.5, Placeholders(format).Count == 0 ? 0 : Placeholders(format).Max() + 1).ToArray();
                var ex = Record.Exception(() => string.Format(CultureInfo.CurrentCulture, format, arguments));
                Assert.True(ex is null, $"{resource}.{key} ({culture}) : {ex?.Message}");
            }
        }
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("en")]
    public void Every_role_word_exists(string culture)
    {
        using var _ = TestCulture.Use(culture);
        foreach (var role in new[] { PulseRoles.Tank, PulseRoles.Healer, PulseRoles.Damage })
        {
            foreach (var name in role.Wording.ResourceNames)
            {
                Assert.False(string.IsNullOrEmpty(GameTexts.ResourceManager.GetString(name, CultureInfo.CurrentUICulture)), name);
            }
        }
    }

    [Fact]
    public void Spec_and_class_names_follow_the_interface_language()
    {
        var guardian = WowSpec.GuardianDruid.Info();
        Assert.Equal(("Gardien", "Druide"), (guardian.SpecName, guardian.ClassName));

        using var _ = TestCulture.Use("en");
        Assert.Equal(("Guardian", "Druid"), (guardian.SpecName, guardian.ClassName));
        Assert.Equal("healer", PulseRoles.Healer.Wording.Noun);
        Assert.All(WowSpecs.Tanks.Concat(WowSpecs.Healers).Concat(WowSpecs.Damage), s => Assert.DoesNotContain("Spec_", s.SpecName));
    }

    [Theory]
    [InlineData("fr", 0, true)]
    [InlineData("fr", 1, true)]
    [InlineData("fr", 2, false)]
    [InlineData("en", 0, false)]
    [InlineData("en", 1, true)]
    [InlineData("en", 2, false)]
    public void Plural_follows_the_language_rule(string culture, int count, bool one) =>
        Assert.Equal(one, Plural.IsOne(count, new CultureInfo(culture)));

    [Fact]
    public void Import_errors_follow_the_interface_language()
    {
        Assert.Contains("Colle la chaîne", CharacterImportParser.Parse("").Error);

        using var _ = TestCulture.Use("en");
        Assert.Contains("Paste the string", CharacterImportParser.Parse("").Error);
    }

    [Theory]
    [InlineData("/specs/blood?x=1", "/specs/blood?x=1")]
    [InlineData("/", "/")]
    [InlineData(null, "/")]
    [InlineData("", "/")]
    [InlineData("https://evil.example", "/")]
    [InlineData("//evil.example", "/")]
    [InlineData("/\\evil.example", "/")]
    public void Language_switch_only_returns_to_the_site(string? returnUrl, string expected) =>
        Assert.Equal(expected, PulseLocalization.SafeReturnUrl(returnUrl));

    /// <summary>
    /// Toutes les ressources du dépôt, y compris celles d'AltPulse et de la page Noovio que ce projet ne référence pas :
    /// lues comme fichiers, à côté de leur traduction anglaise.
    /// </summary>
    [Fact]
    public void Every_resource_file_of_the_repository_has_its_english_twin()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Pulse.sln")))
        {
            root = root.Parent;
        }

        Assert.NotNull(root);
        var neutral = Directory.EnumerateFiles(root.FullName, "*.resx", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(f => !f.EndsWith(".en.resx", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(neutral.Count >= 5, "Ressources introuvables : " + root.FullName);
        foreach (var file in neutral)
        {
            var english = Path.ChangeExtension(file, ".en.resx");
            Assert.True(File.Exists(english), $"{file} : traduction anglaise manquante.");
            var (fr, en) = (ReadResx(file), ReadResx(english));
            Assert.True(fr.Keys.ToHashSet().SetEquals(en.Keys), $"{Path.GetFileName(file)} : clés différentes entre les langues.");
            foreach (var (key, text) in fr)
            {
                Assert.True(Placeholders(text).SetEquals(Placeholders(en[key])), $"{Path.GetFileName(file)}.{key} : paramètres différents.");
            }
        }
    }

    private static Dictionary<string, string> ReadResx(string path) =>
        System.Xml.Linq.XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? "");

    private static ResourceManager Manager(string resource) => resource switch
    {
        nameof(GameTexts) => GameTexts.ResourceManager,
        nameof(AnalysisTexts) => AnalysisTexts.ResourceManager,
        _ => WebTexts.ResourceManager
    };

    private static Dictionary<string, string> Entries(ResourceManager manager, CultureInfo culture) =>
        manager.GetResourceSet(culture, createIfNotExists: true, tryParents: false)!
            .Cast<DictionaryEntry>()
            .ToDictionary(e => (string)e.Key, e => (string)e.Value!);

    private static HashSet<int> Placeholders(string text) =>
        [.. PlaceholderPattern().Matches(text).Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))];

    [GeneratedRegex(@"(?<!\{)\{(\d+)[^{}]*\}")]
    private static partial Regex PlaceholderPattern();
}
