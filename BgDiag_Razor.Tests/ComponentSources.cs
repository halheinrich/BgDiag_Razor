using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace BgDiag_Razor.Tests;

/// <summary>
/// The components' source files as text, for the pins a render cannot make:
/// what a component's code must not contain, and what its scoped stylesheet
/// declares (bUnit has no CSS engine). Every reading has its comments
/// stripped, so prose about a thing can neither fail an assertion about the
/// code nor satisfy one. The technique is BgQuiz's (<c>MainLayoutTests</c>'
/// narrow-desktop band).
/// </summary>
internal static class ComponentSources
{
    /// <summary>
    /// The code-behind and markup of <paramref name="component"/> (its type
    /// name), with comments stripped.
    /// </summary>
    public static string Code(string component) =>
        StripComments(Read(component + ".razor.cs"))
        + StripComments(Read(component + ".razor"));

    /// <summary>
    /// The scoped stylesheet of <paramref name="component"/> (its type name),
    /// with comments stripped.
    /// </summary>
    public static string Css(string component) =>
        StripComments(Read(component + ".razor.css"));

    /// <summary>
    /// <paramref name="source"/> with block comments and <c>//</c> line
    /// comments (XML doc comments included) removed.
    /// </summary>
    public static string StripComments(string source) =>
        Regex.Replace(
            Regex.Replace(source, @"/\*.*?\*/", "", RegexOptions.Singleline),
            @"//.*?$", "", RegexOptions.Multiline);

    /// <summary>
    /// The declaration block for <paramref name="selector"/>, tolerating a
    /// trailing selector list (the selected-state rule is doubled so it outranks
    /// <c>:hover</c>). Fails the test outright when the rule has gone missing —
    /// an absent rule must never read as a vacuously passing assertion.
    /// </summary>
    public static string Rule(string css, string selector)
    {
        var match = Regex.Match(
            css, Regex.Escape(selector) + @"\s*(,[^{]*)?\{(?<body>[^}]*)\}");

        Assert.True(match.Success, $"the `{selector}` rule is missing from the stylesheet.");
        return match.Groups["body"].Value;
    }

    /// <summary>
    /// The declarations of a rule's <paramref name="body"/>, each as written
    /// (<c>width: max-content</c>), so a test can ask for a declaration whole
    /// rather than for text one declaration might hold inside another.
    /// </summary>
    public static IReadOnlyList<string> Declarations(string body) =>
        body.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// The text of one of the components' source files, resolved from this
    /// file's own compile-time location. Scoped CSS is compiled into a bundle
    /// at build time and Razor sources are compiled away entirely, so the
    /// source tree is the only place their text is to be read.
    /// </summary>
    private static string Read(string fileName, [CallerFilePath] string thisFile = "")
    {
        var testDir = Path.GetDirectoryName(thisFile)!;
        return File.ReadAllText(Path.GetFullPath(Path.Combine(
            testDir, "..", "BgDiag_Razor", "Components", fileName)));
    }
}
