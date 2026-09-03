using System.Reflection;
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
    //  Fixtures — the four offered pairs in render order, mirroring the
    //  component's _options table: the reachable verdicts of the umbrella's
    //  SPEC-scoring.md §3 as amended 2026-09-02 (halheinrich/backgammon#187),
    //  walking the claim axis in CubeClaim's declaration order and the taker
    //  axis Take-before-Pass within it. The first three are offered for every
    //  cube decision; the fourth only when the position admits Too Good.
    // -----------------------------------------------------------------------

    private static readonly (string Label, CubeClaimPair Pair)[] Options =
    [
        ("No double / Take", CubeClaimPair.NoDoubleTake),
        ("Double / Take",    CubeClaimPair.DoubleTake),
        ("Double / Pass",    CubeClaimPair.DoublePass),
        ("Too good / Pass",  CubeClaimPair.TooGoodPass),
    ];

    private static readonly IReadOnlyList<string> AllLabels =
        Options.Select(o => o.Label).ToList();

    private static readonly IReadOnlyList<string> LabelsWithoutTooGood =
        AllLabels.Take(3).ToList();

    private static int IndexOf(CubeClaimPair pair) =>
        Array.FindIndex(Options, o => o.Pair == pair);

    private static string LabelOf(CubeClaimPair pair) => Options[IndexOf(pair)].Label;

    /// <summary>The group's radios, in render order.</summary>
    private static IReadOnlyList<AngleSharp.Dom.IElement> Radios(
        IRenderedComponent<BackgammonCubeActions> cut) =>
        cut.FindAll("input[type=radio]");

    /// <summary>Every pill's caption, in render order.</summary>
    private static IReadOnlyList<string> Labels(
        IRenderedComponent<BackgammonCubeActions> cut) =>
        cut.FindAll(".bg-cube-action").Select(e => e.TextContent.Trim()).ToList();

    /// <summary>Every selected pill's caption, in render order.</summary>
    private static IReadOnlyList<string> SelectedLabels(
        IRenderedComponent<BackgammonCubeActions> cut) =>
        cut.FindAll(".bg-cube-action.bg-cube-action-selected")
            .Select(e => e.TextContent.Trim())
            .ToList();

    /// <summary>
    /// A row offering all four pairs with a no-op binding — enough to render,
    /// adopts nothing.
    /// </summary>
    private IRenderedComponent<BackgammonCubeActions> RenderRow(
        CubeClaimPair? value = null, bool offerTooGood = true) =>
        Render<BackgammonCubeActions>(p => p
            .Add(c => c.Value, value)
            .Add(c => c.OfferTooGood, offerTooGood)
            .Add(c => c.ValueChanged, (CubeClaimPair? _) => { }));

    /// <summary>The theory data for the four offered pairs.</summary>
    public static TheoryData<CubeClaimPair> OfferedPairs =>
        new(Options.Select(o => o.Pair));

    /// <summary>The theory data for the three pairs offered without Too Good.</summary>
    public static TheoryData<CubeClaimPair> PairsOfferedWithoutTooGood =>
        new(Options.Take(3).Select(o => o.Pair));

    // -----------------------------------------------------------------------
    //  Render shape — one radio group of whole pairs, in the ruled order
    // -----------------------------------------------------------------------

    [Fact]
    public void Render_IsOneRadioGroup_OfTheFourPairsInRuledOrder()
    {
        var cut = RenderRow();

        var group = Assert.Single(cut.FindAll("[role=radiogroup]"));
        Assert.Equal("Cube decision", group.GetAttribute("aria-label"));
        Assert.True(group.ClassList.Contains("bg-cube-actions"),
            "the root div is itself the radio group — there is no nested group element.");

        Assert.Equal(4, Radios(cut).Count);
        Assert.Equal(AllLabels, Labels(cut));
    }

    /// <summary>
    /// Too Good is offered by fact (SPEC-scoring §3, 2026-09-02 amendment,
    /// halheinrich/backgammon#187): the consumer passes the producer's
    /// <c>BgDecisionData.CanBeTooGood</c>, and when it is <c>false</c> — a
    /// money position under Jacoby with the cube centred — the fourth pill is
    /// not rendered at all. The other three are the same three, in the same
    /// order, so nothing shifts under the user.
    /// </summary>
    [Fact]
    public void Render_OfferTooGoodFalse_OmitsTheFourthPill()
    {
        var cut = RenderRow(offerTooGood: false);

        Assert.Single(cut.FindAll("[role=radiogroup]"));
        Assert.Equal(3, Radios(cut).Count);
        Assert.Equal(LabelsWithoutTooGood, Labels(cut));
        Assert.DoesNotContain("Too good", cut.Markup);
    }

    /// <summary>
    /// The complement: with the fact <c>true</c> the pill is there whatever
    /// the row's own state, so the fact is the only thing that withholds it.
    /// </summary>
    [Fact]
    public void Render_OfferTooGoodTrue_OffersTooGoodInEveryValueState()
    {
        CubeClaimPair?[] everyValueState = [null, .. Options.Select(o => (CubeClaimPair?)o.Pair)];

        foreach (var value in everyValueState)
        {
            var cut = RenderRow(value);
            Assert.Equal(AllLabels, Labels(cut));
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Render_NullValue_NothingSelected(bool offerTooGood)
    {
        var cut = RenderRow(value: null, offerTooGood: offerTooGood);

        Assert.Empty(SelectedLabels(cut));
        Assert.All(Radios(cut), r => Assert.False(r.HasAttribute("checked")));
    }

    [Fact]
    public void AdditionalAttributes_AreSplattedOnRootDiv()
    {
        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.OfferTooGood, true)
            .Add(c => c.ValueChanged, (CubeClaimPair? _) => { })
            .AddUnmatched("data-testid", "cube-actions-1"));

        var root = cut.Find(".bg-cube-actions");
        Assert.Equal("cube-actions-1", root.GetAttribute("data-testid"));
    }

    // -----------------------------------------------------------------------
    //  Value → selection. Exactly one pill lit, the one whose pair it is.
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(OfferedPairs))]
    public void Value_MarksExactlyTheMatchingPill(CubeClaimPair pair)
    {
        var cut = RenderRow(pair);

        Assert.Equal([LabelOf(pair)], SelectedLabels(cut));

        var radios = Radios(cut);
        for (var i = 0; i < radios.Count; i++)
            Assert.Equal(i == IndexOf(pair), radios[i].HasAttribute("checked"));
    }

    /// <summary>
    /// The two cells <c>CubeClaimPair</c> still represents but no cube
    /// decision offers — the retired (Too Good, Take) verdict and the
    /// incoherent (No Double, Pass) — render nothing selected. That is a
    /// caller bug surfacing, and it is pinned as one: the row must not remap
    /// an unoffered pair onto some pill as a fallback.
    /// </summary>
    [Fact]
    public void Value_OutsideTheOfferedPairs_RendersNothingSelected()
    {
        foreach (var unoffered in new[] { CubeClaimPair.TooGoodTake, CubeClaimPair.NoDoublePass })
        {
            var cut = RenderRow(unoffered);

            Assert.Equal(AllLabels, Labels(cut));
            Assert.Empty(SelectedLabels(cut));
            Assert.All(Radios(cut), r => Assert.False(r.HasAttribute("checked")));
        }
    }

    /// <summary>
    /// The same rule at the offerability gate: a Too Good answer handed to a
    /// row whose position cannot be too good has no pill to be, so nothing is
    /// selected — the three offered pills stay unlit rather than one of them
    /// standing in.
    /// </summary>
    [Fact]
    public void Value_TooGoodPass_WithOfferTooGoodFalse_RendersNothingSelected()
    {
        var cut = RenderRow(CubeClaimPair.TooGoodPass, offerTooGood: false);

        Assert.Equal(LabelsWithoutTooGood, Labels(cut));
        Assert.Empty(SelectedLabels(cut));
        Assert.All(Radios(cut), r => Assert.False(r.HasAttribute("checked")));
    }

    [Fact]
    public void ClearingValue_ClearsTheSelection()
    {
        // The consumer's advance-to-next-problem path: there is no request to
        // key an automatic reset off, so the consumer clears by setting Value
        // back to null.
        var cut = RenderRow(CubeClaimPair.DoublePass);
        Assert.Single(SelectedLabels(cut));

        cut.Render(p => p.Add(c => c.Value, null));

        Assert.Empty(SelectedLabels(cut));
        Assert.All(Radios(cut), r => Assert.False(r.HasAttribute("checked")));
    }

    // -----------------------------------------------------------------------
    //  Selection → ValueChanged. One radio is one whole pair: every selection
    //  fires once with its pair, never null, and there is no half-answered
    //  state for anything to be silent about.
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(OfferedPairs))]
    public async Task SelectingAPill_FiresOnceWithItsPair(CubeClaimPair pair)
    {
        CubeClaimPair? received = null;
        var fireCount = 0;

        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.OfferTooGood, true)
            .Add(c => c.ValueChanged,
                (CubeClaimPair? received_) => { received = received_; fireCount++; }));

        await Radios(cut)[IndexOf(pair)].ChangeAsync(new ChangeEventArgs { Value = true });

        Assert.Equal(1, fireCount);
        Assert.NotNull(received);
        Assert.Equal(pair, received);
    }

    /// <summary>
    /// With Too Good withheld the three remaining radios still map to their
    /// own pairs — withholding the fourth pill shifts no index onto a
    /// neighbour's pair.
    /// </summary>
    [Theory]
    [MemberData(nameof(PairsOfferedWithoutTooGood))]
    public async Task SelectingAPill_WithTooGoodWithheld_FiresWithItsOwnPair(CubeClaimPair pair)
    {
        CubeClaimPair? received = null;

        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.OfferTooGood, false)
            .Add(c => c.ValueChanged, (CubeClaimPair? received_) => received = received_));

        await Radios(cut)[IndexOf(pair)].ChangeAsync(new ChangeEventArgs { Value = true });

        Assert.Equal(pair, received);
    }

    [Fact]
    public async Task ChangingTheSelection_RefiresWithTheNewPair()
    {
        var received = new List<CubeClaimPair?>();
        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.Value, CubeClaimPair.NoDoubleTake)
            .Add(c => c.OfferTooGood, true)
            .Add(c => c.ValueChanged, (CubeClaimPair? pair) => received.Add(pair)));

        await Radios(cut)[IndexOf(CubeClaimPair.TooGoodPass)]
            .ChangeAsync(new ChangeEventArgs { Value = true });

        // No one-shot lock: a row already holding an answer reports the new one.
        Assert.Equal([CubeClaimPair.TooGoodPass], received);
    }

    // -----------------------------------------------------------------------
    //  Controlled round trip — the consumer wiring @bind-Value compiles to:
    //  ValueChanged writes the pair back into Value, and the selection renders
    //  from the written-back Value on the next parameter pass.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ValueWriteback_RoundTrip_SelectsThenSwitches()
    {
        CubeClaimPair? current = null;

        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.Value, current)
            .Add(c => c.OfferTooGood, true)
            .Add(c => c.ValueChanged, (CubeClaimPair? pair) => current = pair));

        await Radios(cut)[IndexOf(CubeClaimPair.DoublePass)]
            .ChangeAsync(new ChangeEventArgs { Value = true });
        Assert.Equal(CubeClaimPair.DoublePass, current);

        cut.Render(p => p.Add(c => c.Value, current));
        Assert.Equal(["Double / Pass"], SelectedLabels(cut));

        await Radios(cut)[IndexOf(CubeClaimPair.TooGoodPass)]
            .ChangeAsync(new ChangeEventArgs { Value = true });
        Assert.Equal(CubeClaimPair.TooGoodPass, current);

        cut.Render(p => p.Add(c => c.Value, current));
        Assert.Equal(["Too good / Pass"], SelectedLabels(cut));
    }

    /// <summary>
    /// Strictly controlled: the row holds no selection of its own. A consumer
    /// that binds <c>ValueChanged</c> but never writes the pair back never
    /// adopts the answer — the next render still reads the <c>Value</c> it is
    /// holding, and the selection is whatever that says.
    /// </summary>
    [Fact]
    public async Task StrictlyControlled_SelectionFollowsValue_WithoutAWriteback()
    {
        var cut = RenderRow();

        await Radios(cut)[IndexOf(CubeClaimPair.DoubleTake)]
            .ChangeAsync(new ChangeEventArgs { Value = true });

        cut.Render(p => p.Add(c => c.Value, null));

        Assert.Empty(SelectedLabels(cut));
        Assert.All(Radios(cut), r => Assert.False(r.HasAttribute("checked")));
    }

    // -----------------------------------------------------------------------
    //  Required parameters — the promoted mechanism for silent splats. A
    //  consumer that omits either fails RZ2012 under warnings-as-errors; the
    //  attribute's presence is what that gate stands on.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(nameof(BackgammonCubeActions.ValueChanged))]
    [InlineData(nameof(BackgammonCubeActions.OfferTooGood))]
    public void Parameter_IsEditorRequired(string parameterName)
    {
        var property = typeof(BackgammonCubeActions).GetProperty(parameterName)!;

        Assert.NotNull(property.GetCustomAttribute<ParameterAttribute>());
        Assert.NotNull(property.GetCustomAttribute<EditorRequiredAttribute>());
    }

    // -----------------------------------------------------------------------
    //  Radio group name — one name for the whole row (native mutual
    //  exclusion), and two rows on one page must not cross-link.
    // -----------------------------------------------------------------------

    [Fact]
    public void AllRadios_ShareOneGroupName()
    {
        var names = Radios(RenderRow()).Select(r => r.GetAttribute("name")).ToList();

        Assert.Equal(4, names.Count);
        Assert.Single(names.Distinct());
    }

    [Fact]
    public void TwoInstances_UseDistinctRadioGroupNames()
    {
        var first = RenderRow();
        var second = RenderRow();

        Assert.NotEqual(
            Radios(first)[0].GetAttribute("name"),
            Radios(second)[0].GetAttribute("name"));
    }

    // -----------------------------------------------------------------------
    //  The retired two-axis surface — a grep-style pin.
    //
    //  The two orthogonal groups are gone, not shimmed: the component no
    //  longer renders a nested group element or the per-axis accessible
    //  names, no longer carries the per-axis tables or the half-selection
    //  state that a two-group row needed, and never names the action-level
    //  CubeDecisionPair that predates the claim layer. Sources are read with
    //  comments stripped, so prose about the old shapes cannot fail an
    //  assertion about the code, nor satisfy one.
    // -----------------------------------------------------------------------

    [Fact]
    public void RetiredTwoAxisSurface_IsGoneFromTheComponentSource()
    {
        var code = StripComments(ComponentSource("BackgammonCubeActions.razor.cs"))
                 + StripComments(ComponentSource("BackgammonCubeActions.razor"));

        Assert.DoesNotContain("bg-cube-actions-group", code);
        Assert.DoesNotContain("Doubler claim", code);
        Assert.DoesNotContain("Taker response", code);
        Assert.DoesNotContain("_claimOptions", code);
        Assert.DoesNotContain("_takerOptions", code);
        Assert.DoesNotContain("OnParametersSet", code);
        Assert.DoesNotContain("CubeDecisionPair", code);

        // ...and the pair-valued, fact-gated surface is what stands.
        Assert.Contains("CubeClaimPair", code);
        Assert.Contains("OfferTooGood", code);
    }

    [Fact]
    public void RetiredTwoAxisSurface_IsGoneFromTheRenderedRow()
    {
        var cut = RenderRow();

        Assert.Empty(cut.FindAll(".bg-cube-actions-group"));
        Assert.Empty(cut.FindAll("[aria-label=\"Doubler claim\"]"));
        Assert.Empty(cut.FindAll("[aria-label=\"Taker response\"]"));

        // The two-axis row was two groups of 3 + 2; this is one group of four.
        Assert.Single(cut.FindAll("[role=radiogroup]"));
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
    [Theory]
    [InlineData(true, 4)]
    [InlineData(false, 3)]
    public void Render_RadioInputs_StayRealFocusableControls(bool offerTooGood, int expectedCount)
    {
        var radios = Radios(RenderRow(offerTooGood: offerTooGood));
        Assert.Equal(expectedCount, radios.Count);

        foreach (var radio in radios)
        {
            Assert.False(radio.HasAttribute("hidden"),
                "a `hidden` attribute would remove the radio from the tab order " +
                "and the accessibility tree — the dot is hidden by clipping, in CSS.");
            Assert.False(radio.HasAttribute("aria-hidden"),
                "aria-hidden would strip the control from the accessibility tree.");
            Assert.False(radio.HasAttribute("disabled"),
                "a disabled radio is not focusable — an unoffered pair is not " +
                "rendered, never rendered disabled.");
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
    /// The compaction constants, pinned with their arithmetic. Measured against
    /// the live consumer, the original four compound pills totalled 561.6px —
    /// 56% of the 1001.4px action row — and out-widened the checker row
    /// through the 641–1366px band. The pill gap (0.75rem → 0.25rem), the
    /// pill's inline padding (0.9rem → 0.45rem) and the hidden dot (13px
    /// control + its 0.5rem caption gap) were the −165.6px that closed it,
    /// and all three stand here. The four-pair row at these constants
    /// measures 136.2 + 113.9 + 116.0 + 131.1 = 497.2px of pills plus three
    /// 4px gaps: 509.2px unselected, 516.8px with the widest pill selected
    /// (weight 600 widens its caption), and 374.1px for the three pairs with
    /// Too Good withheld — under the consumer's 16px Helvetica/Arial stack.
    /// That is wider than the two-group row it replaces (364.8px) and than
    /// the compacted compound row before that (396.0px): the compound
    /// captions name both halves, which costs the width the five short
    /// captions had saved. Whether 509px still clears the consumer's row is
    /// the consumer's measurement to take; these numbers are its input. With
    /// one group there is one gap, the compacted one: the wider inter-group
    /// gap went with the second group. bUnit cannot evaluate any of that;
    /// what it can do is stop the constants being widened back without a
    /// fresh measurement.
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

        // One group, one gap — the compacted one; no nested group rule is left
        // to carry a wider one.
        Assert.Contains("gap: 0.25rem", Rule(css, ".bg-cube-actions"));
        Assert.DoesNotContain(".bg-cube-actions-group", css);

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
    //  Source-as-text helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// The component's scoped stylesheet with comments stripped, so prose that
    /// names a declaration cannot be mistaken for the declaration itself.
    /// </summary>
    private static string CubeActionsCss() =>
        StripComments(ComponentSource("BackgammonCubeActions.razor.css"));

    /// <summary>
    /// <paramref name="source"/> with block comments and <c>//</c> line
    /// comments (XML doc comments included) removed.
    /// </summary>
    private static string StripComments(string source) =>
        Regex.Replace(
            Regex.Replace(source, @"/\*.*?\*/", "", RegexOptions.Singleline),
            @"//.*?$", "", RegexOptions.Multiline);

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
    /// The text of one of the component's source files, resolved from this test
    /// file's own compile-time location. Scoped CSS is compiled into a bundle at
    /// build time and Razor sources are compiled away entirely, so the source
    /// tree is the only thing there is to read.
    /// </summary>
    private static string ComponentSource(
        string fileName, [CallerFilePath] string thisFile = "")
    {
        var testDir = Path.GetDirectoryName(thisFile)!;
        return File.ReadAllText(Path.GetFullPath(Path.Combine(
            testDir, "..", "BgDiag_Razor", "Components", fileName)));
    }
}
