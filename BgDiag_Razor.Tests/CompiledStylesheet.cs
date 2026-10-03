using System.Text.Json;
using System.Text.RegularExpressions;

namespace BgDiag_Razor.Tests;

/// <summary>
/// The library's compiled scoped stylesheet: the bundle a browser is served,
/// in which every selector carries the scoped-style attribute of the component
/// whose stylesheet it came from. A rule reaches an element only if that
/// element carries the attribute, which scoped CSS gives only to the markup
/// written in that component's own file — so sharing class names proves
/// nothing, and the compiled selectors are what to match. bUnit has no CSS
/// engine, but AngleSharp matches selectors, so a test can ask which of these
/// rules reach a rendered element.
/// </summary>
internal static class CompiledStylesheet
{
    /// <summary>The static web assets manifest the build places beside this test assembly.</summary>
    private const string ManifestFile = "BgDiag_Razor.staticwebassets.runtime.json";

    /// <summary>The bundle's name in that manifest.</summary>
    private const string BundleAsset = "BgDiag_Razor.styles.css";

    /// <summary>
    /// Every selector in the compiled bundle, one per entry of each rule's
    /// selector list, in the bundle's order.
    /// </summary>
    public static IReadOnlyList<string> Selectors()
    {
        var css = ComponentSources.StripComments(File.ReadAllText(BundlePath()));

        // A flat sheet of rules. An at-rule (a media query, say) would nest
        // rules this reading does not descend into, so it fails loudly rather
        // than leaving rules unread.
        Assert.DoesNotContain("@", css);
        var rules = Regex.Matches(css, @"(?<selectors>[^{}]+)\{[^{}]*\}");
        Assert.Equal(css.Count(c => c == '{'), rules.Count);

        return [.. rules.SelectMany(rule => SelectorList(rule.Groups["selectors"].Value))];
    }

    /// <summary>
    /// The bundle's path, as the manifest names it: a content root (the
    /// build's own output for this configuration) and the bundle's path
    /// beneath it.
    /// </summary>
    private static string BundlePath()
    {
        using var manifest = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, ManifestFile)));
        var root = manifest.RootElement;
        var asset = root.GetProperty("Root").GetProperty("Children")
            .GetProperty(BundleAsset).GetProperty("Asset");
        var contentRoot = root.GetProperty("ContentRoots")
            [asset.GetProperty("ContentRootIndex").GetInt32()].GetString()!;
        return Path.Combine(contentRoot, asset.GetProperty("SubPath").GetString()!);
    }

    /// <summary>
    /// The selectors of one rule's <paramref name="list"/>, split at its
    /// top-level commas (a comma inside parentheses or brackets belongs to
    /// one selector), each with its whitespace collapsed.
    /// </summary>
    private static IEnumerable<string> SelectorList(string list)
    {
        var depth = 0;
        var start = 0;
        for (var i = 0; i < list.Length; i++)
        {
            switch (list[i])
            {
                case '(' or '[':
                    depth++;
                    break;
                case ')' or ']':
                    depth--;
                    break;
                case ',' when depth == 0:
                    yield return Collapsed(list[start..i]);
                    start = i + 1;
                    break;
            }
        }
        yield return Collapsed(list[start..]);
    }

    private static string Collapsed(string selector) =>
        Regex.Replace(selector.Trim(), @"\s+", " ");
}
