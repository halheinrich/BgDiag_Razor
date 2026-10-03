using System.Reflection;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Bunit;
using BackgammonDiagram_Lib;
using BgDiag_Razor.Components;
using BgDataTypes_Lib;
using Microsoft.AspNetCore.Components;
using static BgDiag_Razor.Tests.ComponentSources;
using static BgDiag_Razor.Tests.CubeDecisions;

namespace BgDiag_Razor.Tests;

/// <summary>
/// The inert copy of the cube pill row, which a host measures for the widest
/// the live row can be before any cube decision is on screen (the umbrella's
/// SPEC-quiz-view.md §4, "One budget from the outset"). These pin what makes
/// the measurement true without a browser: the copy draws every state the
/// live row can show, from the live row's own markup and stylesheet, and it
/// is inert. Whether the widths agree under real fonts is the consumer's
/// browser check.
/// </summary>
public class BackgammonCubeActionsRulerTests : BunitContext
{
    // -----------------------------------------------------------------------
    //  Fixtures
    // -----------------------------------------------------------------------

    /// <summary>
    /// The answers in the order the row offers them: <see cref="CubeAnswer"/>'s
    /// declaration order, which the type states is the offered order.
    /// </summary>
    private static readonly CubeAnswer[] Answers = Enum.GetValues<CubeAnswer>();

    /// <summary>Both label forms: full, then short.</summary>
    public static TheoryData<bool> Forms => new(false, true);

    /// <summary>
    /// Every state the live row can show at the two decisions: each gammon
    /// fact, each form, and each value — nothing selected, then each answer.
    /// </summary>
    public static TheoryData<bool, bool, CubeAnswer?> LiveStates
    {
        get
        {
            var data = new TheoryData<bool, bool, CubeAnswer?>();
            foreach (var gammonsPossible in new[] { true, false })
            foreach (var shortLabels in new[] { false, true })
            foreach (var value in Answers.Select(a => (CubeAnswer?)a).Prepend(null))
                data.Add(gammonsPossible, shortLabels, value);
            return data;
        }
    }

    private IRenderedComponent<BackgammonCubeActionsRuler> RenderRuler(bool shortLabels) =>
        Render<BackgammonCubeActionsRuler>(p => p.Add(c => c.ShortLabels, shortLabels));

    /// <summary>The live row in one of its states, with a no-op binding.</summary>
    private IRenderedComponent<BackgammonCubeActions> RenderLive(
        bool gammonsPossible, bool shortLabels, CubeAnswer? value) =>
        Render<BackgammonCubeActions>(p => p
            .Add(c => c.Value, value)
            .Add(c => c.Decision, DecisionAt(gammonsPossible))
            .Add(c => c.ShortLabels, shortLabels)
            .Add(c => c.ValueChanged, (CubeAnswer? _) => { }));

    /// <summary>The ruler's root, the element a host measures.</summary>
    private static IElement Root(IRenderedComponent<BackgammonCubeActionsRuler> ruler) =>
        ruler.Find(".bg-cube-actions-ruler");

    /// <summary>The ruler's copies of the row, in render order.</summary>
    private static IReadOnlyList<IElement> Copies(IRenderedComponent<BackgammonCubeActionsRuler> ruler) =>
        [.. Root(ruler).Children];

    /// <summary>A row's pills, in render order.</summary>
    private static IReadOnlyList<IElement> Pills(IElement row) =>
        [.. row.QuerySelectorAll(".bg-cube-action")];

    private static bool IsSelected(IElement pill) => pill.ClassList.Contains("bg-cube-action-selected");

    /// <summary>
    /// The state a row shows: its captions in order, and which of its pills
    /// are drawn selected — what tells one state of the row from another.
    /// </summary>
    private static string StateOf(IElement row)
    {
        var pills = Pills(row);
        var selected = Enumerable.Range(0, pills.Count).Where(i => IsSelected(pills[i]));
        return $"{string.Join(" | ", pills.Select(p => p.TextContent.Trim()))} @ [{string.Join(",", selected)}]";
    }

    /// <summary>
    /// Every state the live row can show in the form, written as
    /// <see cref="StateOf"/> writes a drawn one: one spelling of each answer
    /// (every combination of what the label home lists for each), each with
    /// nothing selected and with each pill selected in turn.
    /// </summary>
    private static IEnumerable<string> EveryState(bool shortLabels)
    {
        IEnumerable<IReadOnlyList<CubeAnswerSpelling>> combinations = [[]];
        foreach (var answer in Answers)
        {
            var spellings = CubeLabels.Spellings(answer);
            combinations = [.. combinations.SelectMany(combination =>
                spellings.Select(spelling => (IReadOnlyList<CubeAnswerSpelling>)[.. combination, spelling]))];
        }

        foreach (var combination in combinations)
        {
            var captions = string.Join(" | ", combination.Select(s => shortLabels ? s.Short : s.Full));
            yield return $"{captions} @ []";
            for (var i = 0; i < Answers.Length; i++)
                yield return $"{captions} @ [{i}]";
        }
    }

    /// <summary>
    /// The one copy in <paramref name="ruler"/> that shows the state
    /// <paramref name="liveRow"/> shows.
    /// </summary>
    private static IElement CopyOf(IElement liveRow, IRenderedComponent<BackgammonCubeActionsRuler> ruler) =>
        Assert.Single(Copies(ruler), copy => StateOf(copy) == StateOf(liveRow));

    // -----------------------------------------------------------------------
    //  What it draws — every state of the row, so the fonts decide which is
    //  widest
    // -----------------------------------------------------------------------

    /// <summary>
    /// One copy for every state the live row can show in the form, and no
    /// other: every combination of the spellings the label home lists, each
    /// with nothing selected and with each pill selected in turn, so every
    /// spelling is drawn both selected and not.
    /// </summary>
    [Theory]
    [MemberData(nameof(Forms))]
    public void Ruler_DrawsEveryStateOfTheRow_EverySpellingSelectedAndNot(bool shortLabels)
    {
        var drawn = Copies(RenderRuler(shortLabels)).Select(StateOf).Order();

        Assert.Equal(EveryState(shortLabels).Order(), drawn);
    }

    /// <summary>
    /// The reason the copy exists, in words: the fourth answer reads two ways,
    /// and both are drawn, selected and not, in each form. A literal pin, so
    /// what the copy covers is stated here rather than only derived.
    /// </summary>
    [Theory]
    [InlineData(false, "Too good", "No double / Pass")]
    [InlineData(true, "TG", "NP")]
    public void Ruler_DrawsTheFourthAnswerInBothReadings_SelectedAndNot(
        bool shortLabels, string tooGood, string noDoublePass)
    {
        var fourth = Array.IndexOf(Answers, CubeAnswer.NoDoublePass);
        var fourthPills = Copies(RenderRuler(shortLabels)).Select(row => Pills(row)[fourth]).ToList();

        foreach (var caption in new[] { tooGood, noDoublePass })
        {
            Assert.Contains(fourthPills, p => p.TextContent.Trim() == caption && IsSelected(p));
            Assert.Contains(fourthPills, p => p.TextContent.Trim() == caption && !IsSelected(p));
        }
    }

    /// <summary>
    /// The full form is the default, as on the live row; the form is optional,
    /// so a host that never sets it measures the full labels.
    /// </summary>
    [Fact]
    public void Ruler_MeasuresTheFullForm_ByDefault()
    {
        var drawn = Copies(Render<BackgammonCubeActionsRuler>()).Select(StateOf).Order();

        Assert.Equal(EveryState(shortLabels: false).Order(), drawn);
        Assert.Null(typeof(BackgammonCubeActionsRuler)
            .GetProperty(nameof(BackgammonCubeActionsRuler.ShortLabels))!
            .GetCustomAttribute<EditorRequiredAttribute>());
    }

    // -----------------------------------------------------------------------
    //  No decision — the copy is drawn before any is on screen
    // -----------------------------------------------------------------------

    /// <summary>
    /// The copy takes the label form and the host's attributes, and nothing
    /// else: no decision, no value and no callback.
    /// </summary>
    [Fact]
    public void Ruler_TakesNoDecision_NoValueAndNoCallback()
    {
        var parameters = typeof(BackgammonCubeActionsRuler).GetProperties()
            .Where(p => p.GetCustomAttribute<ParameterAttribute>() is not null)
            .Select(p => p.Name)
            .Order();

        Assert.Equal(
            [nameof(BackgammonCubeActionsRuler.AdditionalAttributes), nameof(BackgammonCubeActionsRuler.ShortLabels)],
            parameters);
    }

    /// <summary>
    /// The two rows share how a pill is drawn, not how it is captioned. The
    /// copy draws every spelling the label home lists, spells none itself,
    /// and labels nothing at a decision; the live row labels each answer at
    /// its decision and never picks a caption from the list of spellings.
    /// </summary>
    [Fact]
    public void LiveRowAndCopy_ChooseTheirCaptionsApart()
    {
        var copy = Code(nameof(BackgammonCubeActionsRuler));
        var live = Code(nameof(BackgammonCubeActions));

        Assert.Contains("CubeLabels.Spellings(", copy);
        foreach (var atADecision in new[]
                 {
                     "CubeLabels.Label(", "CubeLabels.ShortLabel(", "CubeDecision", "ClaimOf", "GammonsPossible",
                 })
        {
            Assert.DoesNotContain(atADecision, copy);
        }
        foreach (var spelling in Answers.SelectMany(answer => CubeLabels.Spellings(answer)))
        {
            Assert.DoesNotContain($"\"{spelling.Full}\"", copy);
            Assert.DoesNotContain($"\"{spelling.Short}\"", copy);
        }

        Assert.DoesNotContain("Spellings", live);
        Assert.Contains("CubeLabels.Label(", live);
        Assert.Contains("CubeLabels.ShortLabel(", live);
    }

    // -----------------------------------------------------------------------
    //  Inert — hidden from assistive technology, no focus, no events
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Forms))]
    public void Ruler_IsHiddenFromAssistiveTechnology_AndInert(bool shortLabels)
    {
        var root = Root(RenderRuler(shortLabels));

        Assert.Equal("true", root.GetAttribute("aria-hidden"));
        Assert.True(root.HasAttribute("inert"));
    }

    /// <summary>
    /// Inside the root there is only the pill row's markup: rows and pills,
    /// each carrying a class, a tooltip and the scoped-style attribute, and
    /// nothing else — no input or other control, no name, id or role, no
    /// tabindex, and no event handler anywhere, the root included. So nothing
    /// in the copy takes focus or a pointer, raises an event, or joins a radio
    /// group, even where a browser ignores <c>inert</c>.
    /// </summary>
    [Theory]
    [MemberData(nameof(Forms))]
    public void Ruler_HoldsNoControl_NoNameOrId_AndNoEventHandler(bool shortLabels)
    {
        var ruler = RenderRuler(shortLabels);
        var inside = Root(ruler).QuerySelectorAll("*").ToList();
        Assert.NotEmpty(inside);

        Assert.All(inside, e => Assert.Contains(e.TagName, new[] { "DIV", "LABEL" }));
        Assert.All(inside, e => Assert.All(e.Attributes, a =>
            Assert.True(a.Name is "class" or "title" || IsScopeAttribute(a.Name),
                $"a copy's <{e.LocalName}> carries `{a.Name}`; it may carry only what draws it.")));

        // Blazor writes each event handler into the markup as a blazor:on…
        // attribute, and an element reference as blazor:elementReference.
        Assert.DoesNotContain("blazor:", ruler.Markup);
    }

    /// <summary>
    /// A host's attributes reach the root, so it can find the ruler by its
    /// own name for it, but they come before the root's own two, so none of
    /// them can make the copy visible to assistive technology or interactive.
    /// </summary>
    [Fact]
    public void HostAttributes_ReachTheRoot_ButCannotUndoItsInertness()
    {
        var root = Root(Render<BackgammonCubeActionsRuler>(p => p
            .AddUnmatched("data-ruler", "cube")
            .AddUnmatched("aria-hidden", "false")
            .AddUnmatched("inert", false)));

        Assert.Equal("cube", root.GetAttribute("data-ruler"));
        Assert.Equal("true", root.GetAttribute("aria-hidden"));
        Assert.True(root.HasAttribute("inert"));
    }

    /// <summary>
    /// The root's own box, read as text (bUnit has no CSS engine): never
    /// painted, which keeps it laid out and measurable; one column of copies,
    /// each at its own width; and as wide as its widest copy wherever the host
    /// puts it, never stretched (its width) nor shrunk (its minimum). It is
    /// the stylesheet's one rule, so nothing here reaches the copies, whose
    /// geometry is the live row's stylesheet's alone.
    /// </summary>
    [Fact]
    public void RulerCss_KeepsItUnpainted_AndAsWideAsItsWidestCopy()
    {
        var css = Css(nameof(BackgammonCubeActionsRuler));
        var box = Declarations(Rule(css, ".bg-cube-actions-ruler"));

        Assert.Contains("visibility: hidden", box);
        Assert.Contains("display: inline-grid", box);
        Assert.Contains("justify-items: start", box);
        Assert.Contains("width: max-content", box);
        Assert.Contains("min-width: max-content", box);

        Assert.Single(Regex.Matches(css, @"\{"));
        Assert.DoesNotContain("::deep", css);
    }

    // -----------------------------------------------------------------------
    //  One markup source, one stylesheet — each copy is the live row's own
    //  markup, styled by the live row's own rules
    // -----------------------------------------------------------------------

    /// <summary>
    /// Each copy is the live row's markup for the same state, less the live
    /// row's wiring and nothing else: the same elements, classes, tooltips,
    /// captions and scoped-style attribute, on the root and on every pill. Take
    /// the radio group's role and name off the live root and the radios out of
    /// its pills, and what is left is the copy, character for character. That
    /// is what one markup source gives and two would not.
    /// </summary>
    [Theory]
    [MemberData(nameof(LiveStates))]
    public void EachCopy_IsTheLiveRowsMarkup_LessItsWiring(bool gammonsPossible, bool shortLabels, CubeAnswer? value)
    {
        var live = RenderLive(gammonsPossible, shortLabels, value).Find(".bg-cube-actions");
        var copy = CopyOf(live, RenderRuler(shortLabels));

        var unwired = (IElement)live.Clone(deep: true);
        unwired.RemoveAttribute("role");
        unwired.RemoveAttribute("aria-label");
        foreach (var radio in unwired.QuerySelectorAll("input").ToList())
            radio.RemoveFromParent();

        Assert.Equal(unwired.OuterHtml, copy.OuterHtml);
    }

    /// <summary>
    /// The geometry follows the stylesheet, not the class names: scoped CSS
    /// reaches an element only through the attribute its compiled selectors
    /// carry. So every selector of the compiled bundle that reaches the live
    /// row's root, or one of its pills, must reach the copy's counterpart, and
    /// the reverse: the pill rule (padding, border, line-height), the row rule
    /// (its gap) and, on a selected pill, the selected rule (its weight). A
    /// rule keyed to the live row's wiring — its role, or a selector through
    /// its radio such as the checked state — would reach the live pill alone
    /// and fail here.
    /// </summary>
    [Theory]
    [MemberData(nameof(LiveStates))]
    public void TheCompiledStylesheet_ReachesEachCopy_AsItReachesTheLiveRow(
        bool gammonsPossible, bool shortLabels, CubeAnswer? value)
    {
        var live = RenderLive(gammonsPossible, shortLabels, value).Find(".bg-cube-actions");
        var copy = CopyOf(live, RenderRuler(shortLabels));
        var selectors = CompiledStylesheet.Selectors();

        foreach (var (liveElement, copyElement) in Pills(live).Zip(Pills(copy)).Prepend((live, copy)))
        {
            var reachingLive = selectors.Where(liveElement.Matches).ToList();

            Assert.NotEmpty(reachingLive);
            if (IsSelected(liveElement))
                Assert.Contains(reachingLive, s => s.Contains("bg-cube-action-selected"));
            Assert.Equal(reachingLive, selectors.Where(copyElement.Matches).ToList());
        }
    }

    // -----------------------------------------------------------------------
    //  On one page with the live row
    // -----------------------------------------------------------------------

    /// <summary>
    /// A ruler in each form on the page beside the live row leaves the live
    /// row's radio group as it is alone: the page's one radio group, its four
    /// radios the page's only ones and the only bearers of its name, its
    /// markup what it draws alone, and a selection answering through it once.
    /// </summary>
    [Fact]
    public async Task OnOnePage_TheLiveRowsRadioGroup_IsUndisturbed()
    {
        var fired = new List<CubeAnswer?>();
        var page = Render(builder =>
        {
            builder.OpenComponent<BackgammonCubeActionsRuler>(0);
            builder.CloseComponent();
            builder.OpenComponent<BackgammonCubeActions>(1);
            builder.AddComponentParameter(2, nameof(BackgammonCubeActions.Decision), WithGammons);
            builder.AddComponentParameter(3, nameof(BackgammonCubeActions.ValueChanged),
                EventCallback.Factory.Create<CubeAnswer?>(this, fired.Add));
            builder.CloseComponent();
            builder.OpenComponent<BackgammonCubeActionsRuler>(4);
            builder.AddComponentParameter(5, nameof(BackgammonCubeActionsRuler.ShortLabels), true);
            builder.CloseComponent();
        });

        Assert.Equal(2, page.FindAll(".bg-cube-actions-ruler").Count);

        var group = Assert.Single(page.FindAll("[role=radiogroup]"));
        Assert.Null(group.Closest(".bg-cube-actions-ruler"));

        var radios = page.FindAll("input[type=radio]");
        Assert.Equal(4, radios.Count);
        Assert.All(radios, r => Assert.Same(group, r.Closest("[role=radiogroup]")));
        var name = Assert.Single(radios.Select(r => r.GetAttribute("name")).Distinct());
        Assert.Equal(4, page.FindAll($"[name=\"{name}\"]").Count);
        Assert.Empty(page.FindAll(
            ".bg-cube-actions-ruler [name], .bg-cube-actions-ruler [id], " +
            ".bg-cube-actions-ruler [for], .bg-cube-actions-ruler [form]"));

        var alone = Render<BackgammonCubeActions>(p => p
            .Add(c => c.Decision, WithGammons)
            .Add(c => c.ValueChanged, (CubeAnswer? _) => { }))
            .Find(".bg-cube-actions");
        Assert.Equal(Unnamed(alone), Unnamed(group));

        await radios[Array.IndexOf(Answers, CubeAnswer.DoublePass)]
            .ChangeAsync(new ChangeEventArgs { Value = true });
        Assert.Equal([CubeAnswer.DoublePass], fired);
    }

    // -----------------------------------------------------------------------
    //  Helpers
    // -----------------------------------------------------------------------

    /// <summary>Whether <paramref name="name"/> is a scoped-style attribute (<c>b-</c> and ten characters).</summary>
    private static bool IsScopeAttribute(string name) => Regex.IsMatch(name, "^b-[a-z0-9]{10}$");

    /// <summary>
    /// A live row's markup with what differs between two instances by design
    /// written out: its instance-unique group name, and the renderer's
    /// numbering of its event handlers.
    /// </summary>
    private static string Unnamed(IElement liveRow)
    {
        var name = liveRow.QuerySelector("input[type=radio]")!.GetAttribute("name")!;
        return Regex.Replace(liveRow.OuterHtml.Replace(name, "GROUP"), "blazor:onchange=\"[0-9]+\"", "blazor:onchange");
    }
}
