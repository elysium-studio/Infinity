using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Infinity.Tests;

public sealed class LocalizationResourceTests
{
    private static readonly string[] ExpectedLanguages =
    [
        "de",
        "en",
        "es",
        "fr",
        "it",
        "ja",
        "ko",
        "nl",
        "pl",
        "pt-BR",
        "qps-ploc",
        "zh-Hans",
    ];

    [Fact]
    public void LocalizedResourcesMatchEnglishContracts()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "LocalizationResources");
        string[] languages = [.. Directory.GetDirectories(root)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Order()];

        Assert.Equal(ExpectedLanguages, languages);

        IReadOnlyList<ResourceEntry> english = ReadResources(Path.Combine(root, "en", "Resources.resw"));

        foreach (string language in languages.Where(language => language != "en"))
        {
            IReadOnlyList<ResourceEntry> localized = ReadResources(Path.Combine(root, language, "Resources.resw"));
            Assert.Equal(english.Select(entry => entry.Name), localized.Select(entry => entry.Name));

            for (int index = 0; index < english.Count; index++)
            {
                Assert.Equal(GetPlaceholders(english[index].Value), GetPlaceholders(localized[index].Value));

                if (english[index].Value.Contains("Infinity", StringComparison.Ordinal))
                {
                    Assert.Contains("Infinity", localized[index].Value, StringComparison.Ordinal);
                }
            }
        }
    }

    private static IReadOnlyList<ResourceEntry> ReadResources(string path) =>
        XDocument.Load(path)
            .Root!
            .Elements("data")
            .Select(element => new ResourceEntry(element.Attribute("name")!.Value,
                element.Element("value")!.Value))
            .ToArray();

    private static string[] GetPlaceholders(string value) =>
        [.. Regex.Matches(value, "\\{\\d+\\}").Select(match => match.Value)];

    private sealed record ResourceEntry(string Name, string Value);
}