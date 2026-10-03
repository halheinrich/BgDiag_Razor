using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Css;
using AngleSharp.Css.Parser;
using AngleSharp.Dom;

namespace BgDiag_Razor.Tests;

/// <summary>
/// The library's compiled scoped stylesheet: the bundle a browser is served,
/// in which every selector carries the scoped-style attribute of the component
/// whose stylesheet it came from. A rule reaches an element only if that
/// element carries the attribute, which scoped CSS gives only to the markup
/// written in that component's own file — so sharing class names proves
/// nothing, and the compiled selectors are what to match. bUnit has no CSS
/// engine, but AngleSharp matches selectors and knows their specificity, so a
/// test can ask which of these rules reach a rendered element, and which
/// declared value of each property wins there.
/// </summary>
internal static class CompiledStylesheet
{
    /// <summary>The static web assets manifest the build places beside this test assembly.</summary>
    private const string ManifestFile = "BgDiag_Razor.staticwebassets.runtime.json";

    /// <summary>The bundle's name in that manifest.</summary>
    private const string BundleAsset = "BgDiag_Razor.styles.css";

    /// <summary>
    /// One selector of the bundle with its rule's declarations: a rule whose
    /// selector list has several entries gives one of these per entry.
    /// </summary>
    /// <param name="Selector">The selector, its whitespace collapsed.</param>
    /// <param name="Order">The rule's position in the bundle: of two
    /// selectors equally specific, the later rule's declarations win.</param>
    /// <param name="Declarations">The rule's declarations, property names in
    /// lower case, in the order written.</param>
    public sealed record Rule(string Selector, int Order, IReadOnlyList<KeyValuePair<string, string>> Declarations);

    /// <summary>
    /// Every selector in the compiled bundle, one per entry of each rule's
    /// selector list, in the bundle's order.
    /// </summary>
    public static IReadOnlyList<string> Selectors() => [.. Rules().Select(rule => rule.Selector)];

    /// <summary>
    /// Every rule in the compiled bundle, one per entry of each rule's
    /// selector list, in the bundle's order.
    /// </summary>
    public static IReadOnlyList<Rule> Rules()
    {
        var css = ComponentSources.StripComments(File.ReadAllText(BundlePath()));

        // A flat sheet of rules. An at-rule (a media query, say) would nest
        // rules this reading does not descend into, and !important would
        // reorder the cascade it computes, so either fails loudly rather than
        // leaving a rule misread.
        Assert.DoesNotContain("@", css);
        Assert.DoesNotContain("!important", css);
        var blocks = Regex.Matches(css, @"(?<selectors>[^{}]+)\{(?<body>[^{}]*)\}");
        Assert.Equal(css.Count(c => c == '{'), blocks.Count);

        return [.. blocks.SelectMany((block, order) =>
        {
            var declarations = DeclarationsOf(block.Groups["body"].Value);
            return SelectorList(block.Groups["selectors"].Value)
                .Select(selector => new Rule(selector, order, declarations));
        })];
    }

    /// <summary>
    /// The declared value of every property the bundle sets on
    /// <paramref name="element"/>: of the rules whose selector matches it, the
    /// most specific wins, and of equally specific ones the later. The states
    /// named in <paramref name="activeStates"/> (e.g. <c>hover</c>) are
    /// treated as holding wherever a selector asks for them, so the result is
    /// the element's declared style while it is in those states. Inline
    /// styles and inheritance are out of its reach; the caller asks each
    /// element of a tree it cares about.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Cascade(
        IElement element, IReadOnlyList<Rule> rules, IReadOnlyCollection<string> activeStates)
    {
        var parser = new CssSelectorParser();
        var declared = new Dictionary<string, string>();
        var reaching = rules
            .Where(rule => element.Matches(WithStatesHolding(rule.Selector, activeStates)))
            .Select(rule => (rule, specificity: SpecificityOf(parser, rule.Selector)))
            .ToList();
        reaching.Sort((a, b) =>
            a.specificity < b.specificity ? -1
            : a.specificity > b.specificity ? 1
            : a.rule.Order.CompareTo(b.rule.Order));

        foreach (var (rule, _) in reaching)
        foreach (var (property, value) in rule.Declarations)
            declared[property] = value;
        return declared;
    }

    /// <summary>
    /// <paramref name="selector"/> with each pseudo-class named in
    /// <paramref name="states"/> taken out, so it matches as it would while
    /// those states hold. <c>:focus</c> is told apart from
    /// <c>:focus-visible</c> and <c>:focus-within</c>.
    /// </summary>
    private static string WithStatesHolding(string selector, IReadOnlyCollection<string> states) =>
        states.Aggregate(selector, (current, state) =>
            Regex.Replace(current, $@":{Regex.Escape(state)}(?![-\w])", ""));

    /// <summary>The specificity of <paramref name="selector"/> as written, its states included.</summary>
    private static Priority SpecificityOf(CssSelectorParser parser, string selector)
    {
        var parsed = parser.ParseSelector(selector);
        Assert.True(parsed is not null, $"AngleSharp cannot parse the compiled selector `{selector}`.");
        return parsed.Specificity;
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
    /// The declarations of one rule's <paramref name="body"/>, each a
    /// property in lower case and its value with whitespace collapsed.
    /// </summary>
    private static IReadOnlyList<KeyValuePair<string, string>> DeclarationsOf(string body) =>
        [.. body.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(declaration =>
            {
                var colon = declaration.IndexOf(':');
                Assert.True(colon > 0, $"`{declaration}` is not a declaration.");
                return new KeyValuePair<string, string>(
                    declaration[..colon].Trim().ToLowerInvariant(), Collapsed(declaration[(colon + 1)..]));
            })];

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

    private static string Collapsed(string text) =>
        Regex.Replace(text.Trim(), @"\s+", " ");
}
