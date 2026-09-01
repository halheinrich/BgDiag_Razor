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
    //  Fixtures — the two axes in render order, mirroring the component's
    //  _claimOptions / _takerOptions tables. The claim axis is CubeClaim's own
    //  declaration order ({No Double, Double, Too Good}, as the umbrella's
    //  SPEC-scoring.md §3 rules it for halheinrich/backgammon#86); the taker
    //  axis is Take before Pass.
    // -----------------------------------------------------------------------

    private static readonly (string Label, CubeClaim Claim)[] ClaimOptions =
    [
        ("No double", CubeClaim.NoDouble),
        ("Double",    CubeClaim.Double),
        ("Too good",  CubeClaim.TooGood),
    ];

    private static readonly (string Label, CubeAction Taker)[] TakerOptions =
    [
        ("Take", CubeAction.Take),
        ("Pass", CubeAction.Pass),
    ];

    private static int ClaimIndex(CubeClaim claim) =>
        Array.FindIndex(ClaimOptions, o => o.Claim == claim);

    private static int TakerIndex(CubeAction taker) =>
        Array.FindIndex(TakerOptions, o => o.Taker == taker);

    private static string ClaimLabel(CubeClaim claim) => ClaimOptions[ClaimIndex(claim)].Label;

    private static string TakerLabel(CubeAction taker) => TakerOptions[TakerIndex(taker)].Label;

    /// <summary>The doubler-claim group's radios, in render order.</summary>
    private static IReadOnlyList<AngleSharp.Dom.IElement> ClaimRadios(
        IRenderedComponent<BackgammonCubeActions> cut) =>
        cut.FindAll("[aria-label=\"Doubler claim\"] input[type=radio]");

    /// <summary>The taker-response group's radios, in render order.</summary>
    private static IReadOnlyList<AngleSharp.Dom.IElement> TakerRadios(
        IRenderedComponent<BackgammonCubeActions> cut) =>
        cut.FindAll("[aria-label=\"Taker response\"] input[type=radio]");

    /// <summary>Every selected pill's caption, in render order.</summary>
    private static IReadOnlyList<string> SelectedLabels(
        IRenderedComponent<BackgammonCubeActions> cut) =>
        cut.FindAll(".bg-cube-action.bg-cube-action-selected")
            .Select(e => e.TextContent.Trim())
            .ToList();

    /// <summary>A row with a no-op binding — enough to render, adopts nothing.</summary>
    private IRenderedComponent<BackgammonCubeActions> RenderRow() =>
        Render<BackgammonCubeActions>(p => p
            .Add(c => c.ValueChanged, (CubeClaimPair? _) => { }));

    // -----------------------------------------------------------------------
    //  Render shape — two orthogonal groups, not one compound list
    // -----------------------------------------------------------------------

    [Fact]
    public void Render_ContainsTwoRadioGroups_ClaimThenTaker()
    {
        var cut = RenderRow();

        var groups = cut.FindAll("[role=radiogroup]");
        Assert.Equal(2, groups.Count);
        Assert.Equal("Doubler claim", groups[0].GetAttribute("aria-label"));
        Assert.Equal("Taker response", groups[1].GetAttribute("aria-label"));

        // Three claims × two responses — the 3×2 the answer type is closed over.
        Assert.Equal(3, ClaimRadios(cut).Count);
        Assert.Equal(2, TakerRadios(cut).Count);

        foreach (var (label, _) in ClaimOptions)
            Assert.Contains(label, cut.Markup);
        foreach (var (label, _) in TakerOptions)
            Assert.Contains(label, cut.Markup);
    }

    /// <summary>
    /// Uniform availability (SPEC-scoring §3): "Too good" is offered for every
    /// cube decision whatever is selected and whatever rules are in force —
    /// nothing about a row's state withdraws a claim. Too Good genuinely occurs
    /// in money as well as matches (under Jacoby, via redoubles), so there is no
    /// context in which hiding it would be right; the pin is that the claim
    /// group is always the same three options.
    /// </summary>
    [Fact]
    public void Render_AlwaysOffersAllThreeClaims_TooGoodIncluded()
    {
        // Every state Value can be in: unanswered, plus all six cells.
        CubeClaimPair?[] everyValueState =
        [
            null,
            CubeClaimPair.NoDoubleTake,
            CubeClaimPair.NoDoublePass,
            CubeClaimPair.DoubleTake,
            CubeClaimPair.DoublePass,
            CubeClaimPair.TooGoodTake,
            CubeClaimPair.TooGoodPass,
        ];

        foreach (var value in everyValueState)
        {
            var cut = Render<BackgammonCubeActions>(p => p
                .Add(c => c.Value, value)
                .Add(c => c.ValueChanged, (CubeClaimPair? _) => { }));

            Assert.Equal(
                ClaimOptions.Select(o => o.Label),
                cut.FindAll("[aria-label=\"Doubler claim\"] .bg-cube-action")
                    .Select(e => e.TextContent.Trim()));
            Assert.Contains("Too good", cut.Markup);
        }
    }

    [Fact]
    public void Render_NullValue_NothingSelected()
    {
        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.Value, null)
            .Add(c => c.ValueChanged, (CubeClaimPair? _) => { }));

        Assert.Empty(cut.FindAll(".bg-cube-action-selected"));
        Assert.All(ClaimRadios(cut), r => Assert.False(r.HasAttribute("checked")));
        Assert.All(TakerRadios(cut), r => Assert.False(r.HasAttribute("checked")));
    }

    [Fact]
    public void AdditionalAttributes_AreSplattedOnRootDiv()
    {
        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.ValueChanged, (CubeClaimPair? _) => { })
            .AddUnmatched("data-testid", "cube-actions-1"));

        var root = cut.Find(".bg-cube-actions");
        Assert.Equal("cube-actions-1", root.GetAttribute("data-testid"));
    }

    // -----------------------------------------------------------------------
    //  Value → selection. One pill lit per group, both halves read from the
    //  pair; all six cells render, the incoherent one included.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(CubeClaim.NoDouble, CubeAction.Take)]
    [InlineData(CubeClaim.NoDouble, CubeAction.Pass)]
    [InlineData(CubeClaim.Double,   CubeAction.Take)]
    [InlineData(CubeClaim.Double,   CubeAction.Pass)]
    [InlineData(CubeClaim.TooGood,  CubeAction.Take)]
    [InlineData(CubeClaim.TooGood,  CubeAction.Pass)]
    public void Value_MarksTheMatchingPillInEachGroup(CubeClaim claim, CubeAction taker)
    {
        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.Value, new CubeClaimPair(claim, taker))
            .Add(c => c.ValueChanged, (CubeClaimPair? _) => { }));

        Assert.Equal(new[] { ClaimLabel(claim), TakerLabel(taker) }, SelectedLabels(cut));
        Assert.True(ClaimRadios(cut)[ClaimIndex(claim)].HasAttribute("checked"));
        Assert.True(TakerRadios(cut)[TakerIndex(taker)].HasAttribute("checked"));
    }

    [Fact]
    public void ClearingValue_ClearsBothHalves()
    {
        // The consumer's advance-to-next-problem path: there is no request to
        // key an automatic reset off, so the consumer clears by setting Value
        // back to null — and both halves go, not just the one it last changed.
        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.Value, CubeClaimPair.DoublePass)
            .Add(c => c.ValueChanged, (CubeClaimPair? _) => { }));
        Assert.Equal(2, cut.FindAll(".bg-cube-action-selected").Count);

        cut.Render(p => p.Add(c => c.Value, null));

        Assert.Empty(cut.FindAll(".bg-cube-action-selected"));
    }

    // -----------------------------------------------------------------------
    //  Half-at-a-time entry. Each group moves on its own; the answer exists
    //  only once both have moved, and selecting in one never completes the
    //  other for the user.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SelectingOnlyTheClaim_LeavesTheAnswerIncompleteAndSilent()
    {
        var fireCount = 0;
        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.ValueChanged, (CubeClaimPair? _) => fireCount++));

        await ClaimRadios(cut)[ClaimIndex(CubeClaim.TooGood)]
            .ChangeAsync(new ChangeEventArgs { Value = true });

        // The claim shows; the taker half is untouched, so there is no pair to
        // report and the consumer's Value stays null.
        Assert.Equal(new[] { "Too good" }, SelectedLabels(cut));
        Assert.All(TakerRadios(cut), r => Assert.False(r.HasAttribute("checked")));
        Assert.Equal(0, fireCount);
    }

    [Fact]
    public async Task SelectingOnlyTheTaker_LeavesTheAnswerIncompleteAndSilent()
    {
        var fireCount = 0;
        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.ValueChanged, (CubeClaimPair? _) => fireCount++));

        await TakerRadios(cut)[TakerIndex(CubeAction.Pass)]
            .ChangeAsync(new ChangeEventArgs { Value = true });

        Assert.Equal(new[] { "Pass" }, SelectedLabels(cut));
        Assert.All(ClaimRadios(cut), r => Assert.False(r.HasAttribute("checked")));
        Assert.Equal(0, fireCount);
    }

    /// <summary>
    /// Every one of the six cells is reachable by picking the claim and then
    /// the response, and each fires exactly once with the composed pair. The
    /// incoherent (No double, Pass) row is deliberately among them: the axes
    /// are not cross-disabled, because the answer is representable and merely
    /// never best (SPEC-scoring §3).
    /// </summary>
    [Theory]
    [InlineData(CubeClaim.NoDouble, CubeAction.Take)]
    [InlineData(CubeClaim.NoDouble, CubeAction.Pass)]
    [InlineData(CubeClaim.Double,   CubeAction.Take)]
    [InlineData(CubeClaim.Double,   CubeAction.Pass)]
    [InlineData(CubeClaim.TooGood,  CubeAction.Take)]
    [InlineData(CubeClaim.TooGood,  CubeAction.Pass)]
    public async Task ClaimThenTaker_FiresOnceWithTheComposedPair(
        CubeClaim claim, CubeAction taker)
    {
        CubeClaimPair? received = null;
        var fireCount = 0;

        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.ValueChanged,
                (CubeClaimPair? pair) => { received = pair; fireCount++; }));

        await ClaimRadios(cut)[ClaimIndex(claim)]
            .ChangeAsync(new ChangeEventArgs { Value = true });
        await TakerRadios(cut)[TakerIndex(taker)]
            .ChangeAsync(new ChangeEventArgs { Value = true });

        Assert.Equal(1, fireCount);
        Assert.Equal(new CubeClaimPair(claim, taker), received);
    }

    /// <summary>
    /// The same six cells in the other entry order — the halves are genuinely
    /// independent, so answering the response first reaches every cell too.
    /// </summary>
    [Theory]
    [InlineData(CubeClaim.NoDouble, CubeAction.Take)]
    [InlineData(CubeClaim.NoDouble, CubeAction.Pass)]
    [InlineData(CubeClaim.Double,   CubeAction.Take)]
    [InlineData(CubeClaim.Double,   CubeAction.Pass)]
    [InlineData(CubeClaim.TooGood,  CubeAction.Take)]
    [InlineData(CubeClaim.TooGood,  CubeAction.Pass)]
    public async Task TakerThenClaim_FiresOnceWithTheComposedPair(
        CubeClaim claim, CubeAction taker)
    {
        CubeClaimPair? received = null;
        var fireCount = 0;

        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.ValueChanged,
                (CubeClaimPair? pair) => { received = pair; fireCount++; }));

        await TakerRadios(cut)[TakerIndex(taker)]
            .ChangeAsync(new ChangeEventArgs { Value = true });
        await ClaimRadios(cut)[ClaimIndex(claim)]
            .ChangeAsync(new ChangeEventArgs { Value = true });

        Assert.Equal(1, fireCount);
        Assert.Equal(new CubeClaimPair(claim, taker), received);
    }

    /// <summary>
    /// The incoherent cell, called out on its own because it is the one the
    /// component could have been tempted to prevent: "not good enough to
    /// double, yet they'd pass". It is selectable, it round-trips, and the pair
    /// it emits is the one <see cref="CubeClaimPair.IsIncoherent"/> names —
    /// this row represents an answer, it never prevents one.
    /// </summary>
    [Fact]
    public async Task IncoherentCell_IsSelectableAndEmittedLikeAnyOther()
    {
        CubeClaimPair? received = null;
        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.ValueChanged, (CubeClaimPair? pair) => received = pair));

        await ClaimRadios(cut)[ClaimIndex(CubeClaim.NoDouble)]
            .ChangeAsync(new ChangeEventArgs { Value = true });
        await TakerRadios(cut)[TakerIndex(CubeAction.Pass)]
            .ChangeAsync(new ChangeEventArgs { Value = true });

        Assert.Equal(CubeClaimPair.NoDoublePass, received);
        Assert.True(received!.Value.IsIncoherent);

        cut.Render(p => p.Add(c => c.Value, received));
        Assert.Equal(new[] { "No double", "Pass" }, SelectedLabels(cut));
    }

    // -----------------------------------------------------------------------
    //  Re-firing — once complete, either half can still be changed and the
    //  recomposed pair is reported each time.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ChangingTheClaimOfACompleteAnswer_RefiresWithTheNewPair()
    {
        var received = new List<CubeClaimPair?>();
        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.Value, CubeClaimPair.NoDoubleTake)
            .Add(c => c.ValueChanged, (CubeClaimPair? pair) => received.Add(pair)));

        await ClaimRadios(cut)[ClaimIndex(CubeClaim.TooGood)]
            .ChangeAsync(new ChangeEventArgs { Value = true });

        // Only the claim moved — the taker half it was already carrying stands.
        Assert.Equal([CubeClaimPair.TooGoodTake], received);
    }

    [Fact]
    public async Task ChangingTheTakerOfACompleteAnswer_RefiresWithTheNewPair()
    {
        var received = new List<CubeClaimPair?>();
        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.Value, CubeClaimPair.DoubleTake)
            .Add(c => c.ValueChanged, (CubeClaimPair? pair) => received.Add(pair)));

        await TakerRadios(cut)[TakerIndex(CubeAction.Pass)]
            .ChangeAsync(new ChangeEventArgs { Value = true });

        Assert.Equal([CubeClaimPair.DoublePass], received);
    }

    // -----------------------------------------------------------------------
    //  Controlled round trip — the consumer wiring @bind-Value compiles to:
    //  ValueChanged writes the pair back into Value, and the selection renders
    //  from the written-back Value on the next parameter pass.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ValueWriteback_RoundTrip_CompletesThenSwitchesEitherHalf()
    {
        CubeClaimPair? current = null;

        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.Value, current)
            .Add(c => c.ValueChanged, (CubeClaimPair? pair) => current = pair));

        // Complete the answer as "Double" + "Pass"; the parent writes it back.
        await ClaimRadios(cut)[ClaimIndex(CubeClaim.Double)]
            .ChangeAsync(new ChangeEventArgs { Value = true });
        await TakerRadios(cut)[TakerIndex(CubeAction.Pass)]
            .ChangeAsync(new ChangeEventArgs { Value = true });
        Assert.Equal(CubeClaimPair.DoublePass, current);

        cut.Render(p => p.Add(c => c.Value, current));
        Assert.Equal(new[] { "Double", "Pass" }, SelectedLabels(cut));

        // Switch the claim to "Too good": the written-back value follows, and so
        // does the rendered selection — the taker half surviving the round trip.
        await ClaimRadios(cut)[ClaimIndex(CubeClaim.TooGood)]
            .ChangeAsync(new ChangeEventArgs { Value = true });
        Assert.Equal(CubeClaimPair.TooGoodPass, current);

        cut.Render(p => p.Add(c => c.Value, current));
        Assert.Equal(new[] { "Too good", "Pass" }, SelectedLabels(cut));

        // And the taker half switches the same way.
        await TakerRadios(cut)[TakerIndex(CubeAction.Take)]
            .ChangeAsync(new ChangeEventArgs { Value = true });
        Assert.Equal(CubeClaimPair.TooGoodTake, current);

        cut.Render(p => p.Add(c => c.Value, current));
        Assert.Equal(new[] { "Too good", "Take" }, SelectedLabels(cut));
    }

    /// <summary>
    /// The halves are the component's own state only because a half-answered row
    /// has no <c>CubeClaimPair</c> to be — they stay subordinate to
    /// <c>Value</c>. A consumer that binds <c>ValueChanged</c> but never writes
    /// the pair back therefore never adopts the answer: the next parameter pass
    /// re-seeds both halves from the value it is still holding, and the
    /// selection snaps back.
    /// </summary>
    [Fact]
    public async Task StrictlySubordinate_HalvesReseedFromValue_WithoutAWriteback()
    {
        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.ValueChanged, (CubeClaimPair? _) => { }));

        await ClaimRadios(cut)[ClaimIndex(CubeClaim.Double)]
            .ChangeAsync(new ChangeEventArgs { Value = true });
        await TakerRadios(cut)[TakerIndex(CubeAction.Take)]
            .ChangeAsync(new ChangeEventArgs { Value = true });
        Assert.Equal(2, cut.FindAll(".bg-cube-action-selected").Count);

        // The consumer re-renders still holding null — the answer was never
        // adopted, so it goes.
        cut.Render(p => p.Add(c => c.Value, null));

        Assert.Empty(cut.FindAll(".bg-cube-action-selected"));
        Assert.All(ClaimRadios(cut), r => Assert.False(r.HasAttribute("checked")));
        Assert.All(TakerRadios(cut), r => Assert.False(r.HasAttribute("checked")));
    }

    /// <summary>
    /// The complement of the rule above: an in-progress half must survive an
    /// unrelated re-render. A half-answered row composes to null, which is what
    /// the consumer is still holding, so the two agree and nothing is re-seeded.
    /// </summary>
    [Fact]
    public async Task HalfAnsweredRow_SurvivesAReRenderThatLeavesValueNull()
    {
        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.ValueChanged, (CubeClaimPair? _) => { }));

        await ClaimRadios(cut)[ClaimIndex(CubeClaim.TooGood)]
            .ChangeAsync(new ChangeEventArgs { Value = true });

        cut.Render(p => p.AddUnmatched("data-testid", "cube-actions-1"));

        Assert.Equal(new[] { "Too good" }, SelectedLabels(cut));
    }

    // -----------------------------------------------------------------------
    //  Radio group names — the two axes must not be mutually exclusive with
    //  each other, and two rows on one page must not cross-link either.
    // -----------------------------------------------------------------------

    [Fact]
    public void ClaimAndTakerGroups_UseDifferentRadioNames()
    {
        var cut = RenderRow();

        var claimNames = ClaimRadios(cut).Select(r => r.GetAttribute("name")).ToList();
        var takerNames = TakerRadios(cut).Select(r => r.GetAttribute("name")).ToList();

        // Within a group all radios share one name (native mutual exclusion)...
        Assert.Single(claimNames.Distinct());
        Assert.Single(takerNames.Distinct());

        // ...and across the axes the names differ, or choosing a claim would
        // deselect the response.
        Assert.NotEqual(claimNames[0], takerNames[0]);
    }

    [Fact]
    public void TwoInstances_UseDistinctRadioGroupNames()
    {
        var first = RenderRow();
        var second = RenderRow();

        Assert.NotEqual(
            ClaimRadios(first)[0].GetAttribute("name"),
            ClaimRadios(second)[0].GetAttribute("name"));
        Assert.NotEqual(
            TakerRadios(first)[0].GetAttribute("name"),
            TakerRadios(second)[0].GetAttribute("name"));
    }

    // -----------------------------------------------------------------------
    //  The retired composite surface — a grep-style pin.
    //
    //  The four-option compound list is gone, not shimmed: the component no
    //  longer names CubeDecisionPair, no longer carries the single
    //  (label, pair) table, and no longer spells the compound captions (whose
    //  "Too good" mapped to (NoDouble, Pass) — a claim its label did not name).
    //  Sources are read with comments stripped, so prose about the old shape
    //  cannot fail an assertion about the code, nor satisfy one.
    // -----------------------------------------------------------------------

    [Fact]
    public void RetiredCompositeSurface_IsGoneFromTheComponentSource()
    {
        var code = StripComments(ComponentSource("BackgammonCubeActions.razor.cs"))
                 + StripComments(ComponentSource("BackgammonCubeActions.razor"));

        Assert.DoesNotContain("CubeDecisionPair", code);
        Assert.DoesNotContain("_cubeOptions", code);
        Assert.DoesNotContain("Double/Take", code);
        Assert.DoesNotContain("Double/Pass", code);

        // ...and the claim-layer surface is what replaced it.
        Assert.Contains("CubeClaimPair", code);
        Assert.Contains("CubeClaim.TooGood", code);
    }

    [Fact]
    public void RetiredCompositeLabels_AreGoneFromTheRenderedRow()
    {
        var markup = RenderRow().Markup;

        Assert.DoesNotContain("Double/Take", markup);
        Assert.DoesNotContain("Double/Pass", markup);

        // The compound row was one group of four; this is two groups of 3 + 2.
        Assert.Equal(2, RenderRow().FindAll("[role=radiogroup]").Count);
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
        var cut = RenderRow();

        var radios = ClaimRadios(cut).Concat(TakerRadios(cut)).ToList();
        Assert.Equal(5, radios.Count);

        foreach (var radio in radios)
        {
            Assert.False(radio.HasAttribute("hidden"),
                "a `hidden` attribute would remove the radio from the tab order " +
                "and the accessibility tree — the dot is hidden by clipping, in CSS.");
            Assert.False(radio.HasAttribute("aria-hidden"),
                "aria-hidden would strip the control from the accessibility tree.");
            Assert.False(radio.HasAttribute("disabled"),
                "a disabled radio is not focusable — and no cell is ever withdrawn.");
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
    /// keyboard user arrowing through a group has no visible cursor at all.
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
    /// the live consumer, the old four compound pills totalled 561.6px — 56% of
    /// the 1001.4px action row — and out-widened the checker row through the
    /// 641–1366px band. The pill gap (0.75rem → 0.25rem), the pill's inline
    /// padding (0.9rem → 0.45rem) and the hidden dot (13px control + its 0.5rem
    /// caption gap) were the −165.6px that closed it, and the pill gap and
    /// padding are unchanged here. Splitting the row into two groups spends one
    /// 0.75rem inter-group gap (+12px) and one extra pill's chrome, and buys
    /// back more than that in captions: "No double / Double / Too good / Take /
    /// Pass" is shorter than "No double / Double/Take / Double/Pass / Too good".
    /// bUnit cannot evaluate any of that; what it can do is stop the constants
    /// being widened back without a fresh measurement.
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

        // The pills inside a group keep the compacted gap; the groups sit apart
        // at the wider one, which is the grouping's only visual signal.
        Assert.Contains("gap: 0.25rem", Rule(css, ".bg-cube-actions-group"));
        Assert.Contains("gap: 0.75rem", Rule(css, ".bg-cube-actions"));

        var pill = Rule(css, ".bg-cube-action");
        Assert.Contains("padding: 0.5rem 0.45rem", pill);
        Assert.Contains("line-height: 1.2", pill);

        Assert.DoesNotContain("@media", css);
    }

    /// <summary>
    /// With the dot gone the pill's own styling is the entire selected
    /// affordance, so all three co-varying signals — border hue, fill, and
    /// weight — have to survive together. Any one of them alone is a weaker
    /// "selected" than the state had before the dot was hidden. Both groups
    /// share the rule: a completed answer lights one pill in each.
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
