using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Bunit;
using BgDiag_Razor.Components;
using BgDataTypes_Lib;
using Microsoft.AspNetCore.Components;

namespace BgDiag_Razor.Tests;

public class BackgammonCubeActionsTests : BunitContext
{
    // -----------------------------------------------------------------------
    //  Fixtures
    // -----------------------------------------------------------------------

    /// <summary>
    /// The four options in render order — mirrors the component's _cubeOptions
    /// table (the four-option bijection onto CubeDecisionPair).
    /// </summary>
    private static readonly (string Label, CubeDecisionPair Pair)[] Options =
    [
        ("No double",   new CubeDecisionPair(CubeAction.NoDouble, CubeAction.Take)),
        ("Double/Take", new CubeDecisionPair(CubeAction.Double,   CubeAction.Take)),
        ("Double/Pass", new CubeDecisionPair(CubeAction.Double,   CubeAction.Pass)),
        ("Too good",    new CubeDecisionPair(CubeAction.NoDouble, CubeAction.Pass)),
    ];

    /// <summary>The four radio inputs, in render order (matches the options table).</summary>
    private static IReadOnlyList<AngleSharp.Dom.IElement> Radios(
        IRenderedComponent<BackgammonCubeActions> cut) =>
        cut.FindAll(".bg-cube-actions input[type=radio]");

    // -----------------------------------------------------------------------
    //  Render
    // -----------------------------------------------------------------------

    [Fact]
    public void Render_ContainsOneRadioGroupWithFourOptions()
    {
        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.ValueChanged, (CubeDecisionPair? _) => { }));

        // One radio group holding the four mutually-exclusive options.
        Assert.Single(cut.FindAll("[role=radiogroup]"));
        Assert.Equal(4, Radios(cut).Count);

        // All four bijection labels are present.
        foreach (var (label, _) in Options)
            Assert.Contains(label, cut.Markup);
    }

    [Fact]
    public void Render_NullValue_NothingSelected()
    {
        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.Value, null)
            .Add(c => c.ValueChanged, (CubeDecisionPair? _) => { }));

        Assert.Empty(cut.FindAll(".bg-cube-action-selected"));
        Assert.All(Radios(cut), r => Assert.False(r.HasAttribute("checked")));
    }

    [Fact]
    public void AdditionalAttributes_AreSplattedOnRootDiv()
    {
        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.ValueChanged, (CubeDecisionPair? _) => { })
            .AddUnmatched("data-testid", "cube-actions-1"));

        var root = cut.Find(".bg-cube-actions");
        Assert.Equal("cube-actions-1", root.GetAttribute("data-testid"));
    }

    // -----------------------------------------------------------------------
    //  Controlled value — the selected pill is whatever Value says
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Value_MarksExactlyTheMatchingOptionSelected(int optionIndex)
    {
        var (label, pair) = Options[optionIndex];

        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.Value, pair)
            .Add(c => c.ValueChanged, (CubeDecisionPair? _) => { }));

        var selected = cut.FindAll(".bg-cube-action.bg-cube-action-selected");
        Assert.Single(selected);
        Assert.Equal(label, selected[0].TextContent.Trim());
        Assert.True(Radios(cut)[optionIndex].HasAttribute("checked"));
    }

    [Fact]
    public void ClearingValue_ClearsTheSelection()
    {
        // The consumer's advance-to-next-problem path: there is no request to
        // key an automatic reset off, so the consumer clears by setting Value
        // back to null.
        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.Value, Options[1].Pair)
            .Add(c => c.ValueChanged, (CubeDecisionPair? _) => { }));
        Assert.Single(cut.FindAll(".bg-cube-action-selected"));

        cut.Render(p => p.Add(c => c.Value, null));

        Assert.Empty(cut.FindAll(".bg-cube-action-selected"));
    }

    [Fact]
    public async Task StrictlyControlled_SelectionDoesNotStickWithoutValueWriteback()
    {
        // A consumer that ignores ValueChanged never updates Value, so the
        // component (which holds no selection state of its own) keeps rendering
        // nothing selected.
        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.ValueChanged, (CubeDecisionPair? _) => { }));

        await Radios(cut)[2].ChangeAsync(new ChangeEventArgs { Value = true });

        Assert.Empty(cut.FindAll(".bg-cube-action-selected"));
        Assert.All(Radios(cut), r => Assert.False(r.HasAttribute("checked")));
    }

    // -----------------------------------------------------------------------
    //  ValueChanged — fires once per selection with the matching pair.
    //  Parameterized over all four options; the index tracks the render order.
    //  Note "Too good" (index 3) maps to (NoDouble, Pass) — don't double, but
    //  the opponent would pass.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(0, CubeAction.NoDouble, CubeAction.Take)]
    [InlineData(1, CubeAction.Double,   CubeAction.Take)]
    [InlineData(2, CubeAction.Double,   CubeAction.Pass)]
    [InlineData(3, CubeAction.NoDouble, CubeAction.Pass)]
    public async Task SelectingRadio_FiresOnceWithMatchingPair(
        int radioIndex, CubeAction expectedDoubler, CubeAction expectedTaker)
    {
        CubeDecisionPair? received = null;
        var fireCount = 0;

        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.ValueChanged,
                (CubeDecisionPair? pair) => { received = pair; fireCount++; }));

        await Radios(cut)[radioIndex].ChangeAsync(new ChangeEventArgs { Value = true });

        Assert.Equal(1, fireCount);
        Assert.Equal(new CubeDecisionPair(expectedDoubler, expectedTaker), received);
    }

    // -----------------------------------------------------------------------
    //  Switching — selecting a different radio re-fires with the new pair.
    //  (One radio completes the pair, so there is no half-selected state; the
    //  "does not fire while incomplete" contract is trivially preserved, and
    //  the callback never carries null — radios cannot deselect.)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SwitchingRadio_FiresAgainWithNewPair()
    {
        var received = new List<CubeDecisionPair?>();

        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.ValueChanged,
                (CubeDecisionPair? pair) => received.Add(pair)));

        // Select "Double/Take" (index 1), then switch to "Too good" (index 3).
        await Radios(cut)[1].ChangeAsync(new ChangeEventArgs { Value = true });
        await Radios(cut)[3].ChangeAsync(new ChangeEventArgs { Value = true });

        Assert.Equal(
            new CubeDecisionPair?[]
            {
                new CubeDecisionPair(CubeAction.Double, CubeAction.Take),
                new CubeDecisionPair(CubeAction.NoDouble, CubeAction.Pass),
            },
            received);
    }

    // -----------------------------------------------------------------------
    //  Controlled round trip — the consumer wiring @bind-Value compiles to:
    //  ValueChanged writes the pair back into Value, and the selection renders
    //  from the written-back Value.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ValueWriteback_RoundTrip_SelectsAndSwitches()
    {
        CubeDecisionPair? current = null;

        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.Value, current)
            .Add(c => c.ValueChanged, (CubeDecisionPair? pair) => current = pair));

        // Select "Double/Pass" (index 2); the parent writes the value back.
        await Radios(cut)[2].ChangeAsync(new ChangeEventArgs { Value = true });
        Assert.Equal(Options[2].Pair, current);
        cut.Render(p => p.Add(c => c.Value, current));
        Assert.Equal("Double/Pass",
            cut.Find(".bg-cube-action-selected").TextContent.Trim());

        // Switch to "No double" (index 0); the selection follows the value.
        await Radios(cut)[0].ChangeAsync(new ChangeEventArgs { Value = true });
        Assert.Equal(Options[0].Pair, current);
        cut.Render(p => p.Add(c => c.Value, current));
        var selected = cut.FindAll(".bg-cube-action.bg-cube-action-selected");
        Assert.Single(selected);
        Assert.Equal("No double", selected[0].TextContent.Trim());
    }

    // -----------------------------------------------------------------------
    //  Instance-unique radio group — two rows on one page must not cross-link
    //  their browser-native mutual exclusion.
    // -----------------------------------------------------------------------

    [Fact]
    public void TwoInstances_UseDistinctRadioGroupNames()
    {
        var first = Render<BackgammonCubeActions>(p => p
            .Add(c => c.ValueChanged, (CubeDecisionPair? _) => { }));
        var second = Render<BackgammonCubeActions>(p => p
            .Add(c => c.ValueChanged, (CubeDecisionPair? _) => { }));

        // Within an instance all four radios share one group name...
        var firstNames = Radios(first).Select(r => r.GetAttribute("name")).ToList();
        var secondNames = Radios(second).Select(r => r.GetAttribute("name")).ToList();
        Assert.Single(firstNames.Distinct());
        Assert.Single(secondNames.Distinct());

        // ...and the two instances' group names differ.
        Assert.NotEqual(firstNames[0], secondNames[0]);
    }

    // -----------------------------------------------------------------------
    //  Compact metrics and the hidden-but-focusable radio.
    //
    //  These pin the ruled resolution of halheinrich/backgammon#99 (umbrella
    //  SPEC-quiz-view.md §2's invariance floor): the row's horizontal metrics
    //  are a measured contract, not free styling, and the native radio dot is
    //  hidden rather than dropped. bUnit has no CSS engine and builds no
    //  accessibility tree, so the fact is pinned from both ends — the markup
    //  half here, and the styling half by reading the scoped stylesheet as
    //  text (the technique BgQuiz's MainLayout band tests established).
    // -----------------------------------------------------------------------

    /// <summary>
    /// The dot comes out of the <i>visual</i> box only: each option must still
    /// render a real <c>input type=radio</c> that assistive technology sees and
    /// the keyboard reaches. Dropping the input, or hiding it with the markup
    /// switches asserted against here, would take the row's native radio-group
    /// behavior (arrow-key roving, mutual exclusion by name) and its accessible
    /// name with it — the cheap way to "remove the dot", and the wrong one.
    /// </summary>
    [Fact]
    public void Render_RadioInputs_StayRealFocusableControls()
    {
        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.ValueChanged, (CubeDecisionPair? _) => { }));

        var radios = Radios(cut);
        Assert.Equal(4, radios.Count);

        foreach (var radio in radios)
        {
            Assert.False(radio.HasAttribute("hidden"),
                "a `hidden` attribute would remove the radio from the tab order " +
                "and the accessibility tree — the dot is hidden by clipping, in CSS.");
            Assert.False(radio.HasAttribute("aria-hidden"),
                "aria-hidden would strip the control from the accessibility tree.");
            Assert.False(radio.HasAttribute("disabled"),
                "a disabled radio is not focusable.");
            Assert.False(radio.HasAttribute("tabindex"),
                "the radios rely on the browser's native roving tab order; an " +
                "explicit tabindex (least of all -1) would override it.");
            Assert.DoesNotContain("display", radio.GetAttribute("style") ?? "",
                StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// The hiding technique, pinned: the native control is stretched over its
    /// own pill and made transparent, so it keeps both halves of being a real
    /// control — it stays in the accessibility tree and focusable, and it stays
    /// the element at its own coordinates.
    ///
    /// <para>
    /// The blunt ways out are defects, not equivalent implementations:
    /// <c>display: none</c>, <c>visibility: hidden</c> and a zeroed size each
    /// take the radio out of the tab order and out of assistive technology's
    /// reach. So, less obviously, is the sr-only <c>clip-path</c> recipe — it
    /// clips hit-testing along with painting, so the input stops being the
    /// element at its own centre (<c>elementFromPoint</c> there returns the
    /// label) and anything driving the real control by pointer cannot reach it.
    /// Measured against the live consumer: the clip form makes Playwright's
    /// <c>CheckAsync</c> — which the BgQuiz e2e suite uses to answer cube
    /// problems — fail its actionability check. Hence the explicit assertion
    /// against <c>clip</c> below; it is the plausible-looking regression.
    /// (Comments are stripped before matching, so the stylesheet's own prose
    /// about not using these declarations cannot satisfy an assertion.)
    /// </para>
    /// </summary>
    [Fact]
    public void CubeActionsCss_HidesTheRadio_WithoutCostingItFocusOrItsHitArea()
    {
        var radio = Rule(CubeActionsCss(), ".bg-cube-action input[type=\"radio\"]");

        // Transparent, stretched over the pill, and out of flow (so it adds
        // nothing to the row's width).
        Assert.Contains("opacity: 0", radio);
        Assert.Contains("position: absolute", radio);
        Assert.Contains("inset: 0", radio);
        Assert.Contains("width: 100%", radio);
        Assert.Contains("height: 100%", radio);

        Assert.DoesNotContain("display: none", radio);
        Assert.DoesNotContain("visibility: hidden", radio);
        Assert.DoesNotContain("width: 0", radio);
        Assert.DoesNotContain("height: 0", radio);
        Assert.DoesNotContain("clip", radio);
    }

    /// <summary>
    /// The focus ring the browser drew around the native dot is clipped away
    /// with it, so the pill has to draw one instead — without this rule a
    /// keyboard user arrowing through the group has no visible cursor at all.
    /// It rides <c>outline</c> deliberately: outlines draw outside the border
    /// box, so the ring cannot widen the row and reopen the wrap this whole
    /// change exists to close.
    /// </summary>
    [Fact]
    public void CubeActionsCss_KeepsAVisibleKeyboardFocusRingOnThePill()
    {
        var focus = Rule(
            CubeActionsCss(),
            ".bg-cube-action:has(input[type=\"radio\"]:focus-visible)");

        Assert.Contains("outline:", focus);
        Assert.DoesNotContain("outline: none", focus);
    }

    /// <summary>
    /// The three compaction constants, pinned with their arithmetic. Measured
    /// against the live consumer, the four pills totalled 561.6px — 56% of the
    /// 1001.4px action row — and out-widened the checker row through the
    /// 641–1366px band. The row gap (0.75rem → 0.25rem, ×3 gaps = −24px), the
    /// pill's inline padding (0.9rem → 0.45rem, ×2 sides ×4 pills = −57.6px)
    /// and the hidden dot (13px control + its 0.5rem caption gap, ×4 = −84px)
    /// sum to −165.6px. bUnit cannot evaluate any of that; what it can do is
    /// stop the constants being widened back without a fresh measurement.
    ///
    /// <para>
    /// The <i>vertical</i> metrics are pinned in the same breath because they
    /// are the tap-target floor: 0.5rem of block padding plus a 1.2 line-height
    /// on a 16px caption plus 1px borders is the pill's measured 37px height,
    /// which real-tablet touch data stands behind. Compaction was horizontal
    /// only — shaving block padding would trade a layout win for a touch
    /// regression. The absence of a media query is pinned too: the compact form
    /// is the form at every width, because a producer component has no view of
    /// the consumer's layout to gate one on.
    /// </para>
    /// </summary>
    [Fact]
    public void CubeActionsCss_KeepsItsMeasuredCompactMetrics()
    {
        var css = CubeActionsCss();

        Assert.Contains("gap: 0.25rem", Rule(css, ".bg-cube-actions"));

        var pill = Rule(css, ".bg-cube-action");
        Assert.Contains("padding: 0.5rem 0.45rem", pill);
        Assert.Contains("line-height: 1.2", pill);

        Assert.DoesNotContain("@media", css);
    }

    /// <summary>
    /// With the dot gone the pill's own styling is the entire selected
    /// affordance, so all three co-varying signals — border hue, fill, and
    /// weight — have to survive together. Any one of them alone is a weaker
    /// "selected" than the state had before the dot was hidden.
    /// </summary>
    [Fact]
    public void CubeActionsCss_SelectedPill_KeepsAllThreeSignals()
    {
        var selected = Rule(
            CubeActionsCss(),
            ".bg-cube-action.bg-cube-action-selected");

        Assert.Contains("border-color: #2f6fed", selected);
        Assert.Contains("background: #e8f0fe", selected);
        Assert.Contains("font-weight: 600", selected);
    }

    // -----------------------------------------------------------------------
    //  Stylesheet-as-text helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// The component's scoped stylesheet with comments stripped, so prose that
    /// names a declaration cannot be mistaken for the declaration itself.
    /// </summary>
    private static string CubeActionsCss() =>
        Regex.Replace(
            File.ReadAllText(CubeActionsCssPath()), @"/\*.*?\*/", "", RegexOptions.Singleline);

    /// <summary>
    /// The declaration block for <paramref name="selector"/>, tolerating a
    /// trailing selector list (the selected-state rule is doubled so it outranks
    /// <c>:hover</c>). Fails the test outright when the rule has gone missing —
    /// an absent rule must never read as a vacuously passing assertion.
    /// </summary>
    private static string Rule(string css, string selector)
    {
        var match = Regex.Match(
            css, Regex.Escape(selector) + @"\s*(,[^{]*)?\{(?<body>[^}]*)\}");

        Assert.True(match.Success,
            $"the `{selector}` rule is missing from BackgammonCubeActions.razor.css.");
        return match.Groups["body"].Value;
    }

    /// <summary>
    /// Absolute path to the component's <c>.razor.css</c>, resolved from this
    /// test file's own compile-time location — scoped CSS is compiled into a
    /// bundle at build time and never copied to the test output, so the source
    /// file is the only thing there is to read.
    /// </summary>
    private static string CubeActionsCssPath([CallerFilePath] string thisFile = "")
    {
        var testDir = Path.GetDirectoryName(thisFile)!;
        return Path.GetFullPath(Path.Combine(
            testDir, "..", "BgDiag_Razor", "Components", "BackgammonCubeActions.razor.css"));
    }
}
