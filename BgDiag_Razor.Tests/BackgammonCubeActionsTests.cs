using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Bunit;
using BackgammonDiagram_Lib;
using BgDiag_Razor.Components;
using BgDataTypes_Lib;
using BgDataTypes_Lib.TestSupport;
using Microsoft.AspNetCore.Components;

namespace BgDiag_Razor.Tests;

public class BackgammonCubeActionsTests : BunitContext
{
    // -----------------------------------------------------------------------
    //  Fixtures — the four answers in the order the row offers them, which is
    //  CubeAnswer's declaration order (the type states it is the offered
    //  order; the umbrella's SPEC-scoring.md §3, amended on
    //  halheinrich/backgammon#326), and what a user reads off each pill.
    //
    //  The captions are literals deliberately, and stay literals. The
    //  component spells none of them: each pill renders CubeLabels.Label or
    //  CubeLabels.ShortLabel from BackgammonDiagram_Lib, at the decision, and
    //  that home's own suite (CubeLabelsTests) proves the wording. These are a
    //  consumer's pins: they say what a user reads off this row, so a
    //  re-wording at the label home has to arrive here as a deliberate edit
    //  instead of passing through unseen. That the row asks the label home
    //  rather than spelling its own is pinned separately, by
    //  Render_EachPill_IsLabelledByTheLabelHome_AtItsDecision and by the
    //  source pin Component_SpellsNoCubeWording_AndReadsNoRule; do not
    //  "de-duplicate" these tables against the label home.
    // -----------------------------------------------------------------------

    private static readonly CubeAnswer[] Answers =
    [
        CubeAnswer.NoDouble,
        CubeAnswer.DoubleTake,
        CubeAnswer.DoublePass,
        CubeAnswer.NoDoublePass,
    ];

    /// <summary>The full labels where gammons are possible: the fourth answer reads Too good.</summary>
    private static readonly string[] FullWhereGammonsPossible =
        ["No double", "Double / Take", "Double / Pass", "Too good"];

    /// <summary>The full labels where gammons are not possible: the fourth answer reads No double / Pass.</summary>
    private static readonly string[] FullWhereGammonsNotPossible =
        ["No double", "Double / Take", "Double / Pass", "No double / Pass"];

    /// <summary>The short labels where gammons are possible.</summary>
    private static readonly string[] ShortWhereGammonsPossible =
        ["ND", "D/T", "D/P", "TG"];

    /// <summary>The short labels where gammons are not possible.</summary>
    private static readonly string[] ShortWhereGammonsNotPossible =
        ["ND", "D/T", "D/P", "NP"];

    /// <summary>
    /// The short form's accessible names where gammons are possible: the
    /// visible short label first, then the full label in parentheses
    /// (SPEC-quiz-view §4, amended 2026-10-02).
    /// </summary>
    private static readonly string[] ShortFormNamesWhereGammonsPossible =
        ["ND (No double)", "D/T (Double / Take)", "D/P (Double / Pass)", "TG (Too good)"];

    /// <summary>The short form's accessible names where gammons are not possible.</summary>
    private static readonly string[] ShortFormNamesWhereGammonsNotPossible =
        ["ND (No double)", "D/T (Double / Take)", "D/P (Double / Pass)", "NP (No double / Pass)"];

    private static string[] ShortFormNamesAt(bool gammonsPossible) =>
        gammonsPossible ? ShortFormNamesWhereGammonsPossible : ShortFormNamesWhereGammonsNotPossible;

    private static string[] FullLabelsAt(bool gammonsPossible) =>
        gammonsPossible ? FullWhereGammonsPossible : FullWhereGammonsNotPossible;

    private static string[] ShortLabelsAt(bool gammonsPossible) =>
        gammonsPossible ? ShortWhereGammonsPossible : ShortWhereGammonsNotPossible;

    private static int IndexOf(CubeAnswer answer) => Array.IndexOf(Answers, answer);

    /// <summary>
    /// A cube decision where gammons are or are not possible. The two differ
    /// only in the gammon fact: both are a money session with the cube
    /// centred, and only the Jacoby rule, which closes gammons at a centred
    /// cube, differs. The fact is the producer's
    /// (<see cref="CubeDecision.GammonsPossible"/>); the fixture refuses to
    /// hand a test a decision that disagrees with what it asked for.
    /// </summary>
    private static CubeDecision DecisionWhereGammons(bool possible)
    {
        var decision = TestRecords.Cube(
            position: TestRecords.Position(session: TestRecords.MoneySession(isJacoby: !possible)));
        if (decision.GammonsPossible != possible)
            throw new InvalidOperationException(
                $"The fixture asked for gammons possible = {possible}; the record says {decision.GammonsPossible}.");
        return decision;
    }

    private static readonly CubeDecision WithGammons = DecisionWhereGammons(possible: true);
    private static readonly CubeDecision WithoutGammons = DecisionWhereGammons(possible: false);

    private static CubeDecision DecisionAt(bool gammonsPossible) =>
        gammonsPossible ? WithGammons : WithoutGammons;

    /// <summary>The group's radios, in render order.</summary>
    private static IReadOnlyList<AngleSharp.Dom.IElement> Radios(
        IRenderedComponent<BackgammonCubeActions> cut) =>
        cut.FindAll("input[type=radio]");

    /// <summary>The group's pills (the <c>label</c> elements), in render order.</summary>
    private static IReadOnlyList<AngleSharp.Dom.IElement> Pills(
        IRenderedComponent<BackgammonCubeActions> cut) =>
        cut.FindAll(".bg-cube-action");

    /// <summary>Every pill's visible caption, in render order.</summary>
    private static IReadOnlyList<string> Captions(
        IRenderedComponent<BackgammonCubeActions> cut) =>
        Pills(cut).Select(e => e.TextContent.Trim()).ToList();

    /// <summary>Every radio's accessible name (its <c>aria-label</c>), in render order.</summary>
    private static IReadOnlyList<string?> AccessibleNames(
        IRenderedComponent<BackgammonCubeActions> cut) =>
        Radios(cut).Select(r => r.GetAttribute("aria-label")).ToList();

    /// <summary>Every pill's tooltip (its <c>title</c>), in render order.</summary>
    private static IReadOnlyList<string?> Tooltips(
        IRenderedComponent<BackgammonCubeActions> cut) =>
        Pills(cut).Select(p => p.GetAttribute("title")).ToList();

    /// <summary>Every selected pill's caption, in render order.</summary>
    private static IReadOnlyList<string> SelectedCaptions(
        IRenderedComponent<BackgammonCubeActions> cut) =>
        cut.FindAll(".bg-cube-action.bg-cube-action-selected")
            .Select(e => e.TextContent.Trim())
            .ToList();

    private static void AssertNothingSelected(IRenderedComponent<BackgammonCubeActions> cut)
    {
        Assert.Empty(SelectedCaptions(cut));
        Assert.All(Radios(cut), r => Assert.False(r.HasAttribute("checked")));
    }

    /// <summary>
    /// A row at <paramref name="decision"/> (by default one where gammons are
    /// possible) with a no-op binding — enough to render, adopts nothing. The
    /// short form is set only when <paramref name="shortLabels"/> is given, so
    /// the default form stays the component's own.
    /// </summary>
    private IRenderedComponent<BackgammonCubeActions> RenderRow(
        CubeAnswer? value = null, CubeDecision? decision = null, bool? shortLabels = null) =>
        Render<BackgammonCubeActions>(p =>
        {
            p.Add(c => c.Value, value)
             .Add(c => c.Decision, decision ?? WithGammons)
             .Add(c => c.ValueChanged, (CubeAnswer? _) => { });
            if (shortLabels is { } form)
                p.Add(c => c.ShortLabels, form);
        });

    /// <summary>Both gammon facts.</summary>
    public static TheoryData<bool> GammonFacts => new(true, false);

    /// <summary>Both gammon facts in both forms.</summary>
    public static TheoryData<bool, bool> GammonFactsAndForms => new()
    {
        { true, false }, { true, true }, { false, false }, { false, true },
    };

    /// <summary>Every answer at both gammon facts.</summary>
    public static TheoryData<CubeAnswer, bool> AnswersAtBothGammonFacts
    {
        get
        {
            var data = new TheoryData<CubeAnswer, bool>();
            foreach (var answer in Answers)
            {
                data.Add(answer, true);
                data.Add(answer, false);
            }
            return data;
        }
    }

    // -----------------------------------------------------------------------
    //  The answers — one radio group of the four, in order, at every decision
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(GammonFacts))]
    public void Render_IsOneRadioGroup_OfTheFourAnswersInOrder(bool gammonsPossible)
    {
        var cut = RenderRow(decision: DecisionAt(gammonsPossible));

        var group = Assert.Single(cut.FindAll("[role=radiogroup]"));
        Assert.Equal("Cube decision", group.GetAttribute("aria-label"));
        Assert.True(group.ClassList.Contains("bg-cube-actions"),
            "the root div is itself the radio group — there is no nested group element.");

        Assert.Equal(4, Radios(cut).Count);
        Assert.Equal(FullLabelsAt(gammonsPossible), Captions(cut));
    }

    /// <summary>
    /// The fourth answer, "don't double, they'd pass", is always offered and
    /// labelled at its decision (SPEC-scoring §3, second 2026-10-01
    /// amendment): Too good where gammons are possible, No double / Pass where
    /// they are not. The two decisions differ only in the gammon fact, so the
    /// fourth pill's label is the only thing that changes between them.
    /// </summary>
    [Fact]
    public void Render_FourthAnswer_ReadsTooGood_WhereGammonsArePossible_AndNoDoublePass_WhereNot()
    {
        var withGammons = Captions(RenderRow(decision: WithGammons));
        var withoutGammons = Captions(RenderRow(decision: WithoutGammons));

        Assert.Equal("Too good", withGammons[IndexOf(CubeAnswer.NoDoublePass)]);
        Assert.Equal("No double / Pass", withoutGammons[IndexOf(CubeAnswer.NoDoublePass)]);
        Assert.Equal(withGammons.Take(3), withoutGammons.Take(3));
    }

    /// <summary>
    /// Every caption, accessible name and tooltip is the label home's, read at
    /// the row's decision: the caption is <see cref="CubeLabels.Label(CubeAnswer, CubeDecision)"/>
    /// in the full form and <see cref="CubeLabels.ShortLabel(CubeAnswer, CubeDecision)"/>
    /// in the short form; the tooltip is the full label in both; and the
    /// accessible name is the full label in the full form and the two joined,
    /// short first, in the short form. This pins the wiring — which member the
    /// row asks, for which answer, at which decision — not the wording, which
    /// the literal tables above pin as what a user reads and hears.
    /// </summary>
    [Theory]
    [MemberData(nameof(GammonFactsAndForms))]
    public void Render_EachPill_IsLabelledByTheLabelHome_AtItsDecision(bool gammonsPossible, bool shortLabels)
    {
        var decision = DecisionAt(gammonsPossible);
        var cut = RenderRow(decision: decision, shortLabels: shortLabels);

        var full = Answers.Select(a => CubeLabels.Label(a, decision)).ToList();
        var shortForm = Answers.Select(a => CubeLabels.ShortLabel(a, decision)).ToList();

        Assert.Equal(shortLabels ? shortForm : full, Captions(cut));
        Assert.Equal(
            shortLabels ? shortForm.Zip(full, (s, f) => $"{s} ({f})").ToList() : full,
            AccessibleNames(cut));
        Assert.Equal(full, Tooltips(cut));
    }

    [Fact]
    public void AdditionalAttributes_AreSplattedOnRootDiv()
    {
        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.Decision, WithGammons)
            .Add(c => c.ValueChanged, (CubeAnswer? _) => { })
            .AddUnmatched("data-testid", "cube-actions-1"));

        var root = cut.Find(".bg-cube-actions");
        Assert.Equal("cube-actions-1", root.GetAttribute("data-testid"));
    }

    // -----------------------------------------------------------------------
    //  The short form — the host's call (SPEC-quiz-view §4, "The action row
    //  under quiz navigation", amended 2026-10-02): short captions, each
    //  radio's accessible name the short label then the full one in
    //  parentheses (so it contains the text shown), each pill's tooltip the
    //  full label, full by default.
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(GammonFacts))]
    public void ShortForm_ShowsTheShortLabels_NamedShortThenFull_WithTheFullLabelAsTooltip(bool gammonsPossible)
    {
        var cut = RenderRow(decision: DecisionAt(gammonsPossible), shortLabels: true);

        Assert.Equal(ShortLabelsAt(gammonsPossible), Captions(cut));
        Assert.Equal(ShortFormNamesAt(gammonsPossible), AccessibleNames(cut));
        Assert.Equal(FullLabelsAt(gammonsPossible), Tooltips(cut));

        // Still the one native radio group: four real radios under one name.
        Assert.Single(cut.FindAll("[role=radiogroup]"));
        Assert.Equal(4, Radios(cut).Count);
        Assert.Single(Radios(cut).Select(r => r.GetAttribute("name")).Distinct());
    }

    [Theory]
    [MemberData(nameof(GammonFacts))]
    public void FullForm_AccessibleNameAndTooltip_AreTheFullLabel(bool gammonsPossible)
    {
        var cut = RenderRow(decision: DecisionAt(gammonsPossible), shortLabels: false);

        Assert.Equal(FullLabelsAt(gammonsPossible), Captions(cut));
        Assert.Equal(FullLabelsAt(gammonsPossible), AccessibleNames(cut));
        Assert.Equal(FullLabelsAt(gammonsPossible), Tooltips(cut));
    }

    /// <summary>
    /// The full form is the default: a host that never sets the short form
    /// gets the full labels. The parameter is optional by design — not
    /// <see cref="EditorRequiredAttribute"/> — because its default is the
    /// ruled one (the labels abbreviate only when the row cannot fit them).
    /// </summary>
    [Fact]
    public void FullForm_IsTheDefault()
    {
        var cut = RenderRow();

        Assert.Equal(FullWhereGammonsPossible, Captions(cut));

        var property = typeof(BackgammonCubeActions).GetProperty(nameof(BackgammonCubeActions.ShortLabels))!;
        Assert.NotNull(property.GetCustomAttribute<ParameterAttribute>());
        Assert.Null(property.GetCustomAttribute<EditorRequiredAttribute>());
        Assert.False(cut.Instance.ShortLabels);
    }

    // -----------------------------------------------------------------------
    //  Parameter changes — the host relabels the row by changing the decision
    //  or the form. Every caption, accessible name and tooltip follows at the
    //  next render; the change leaves Value alone and fires no ValueChanged.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChangingTheDecision_RelabelsTheFourthPill_SelectingNothingAndFiringNothing(bool shortLabels)
    {
        var fired = 0;
        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.Value, CubeAnswer.NoDoublePass)
            .Add(c => c.Decision, WithGammons)
            .Add(c => c.ShortLabels, shortLabels)
            .Add(c => c.ValueChanged, (CubeAnswer? _) => fired++));

        var fourth = IndexOf(CubeAnswer.NoDoublePass);
        void AssertFourthReads(string caption, string name, string tooltip)
        {
            Assert.Equal(caption, Captions(cut)[fourth]);
            Assert.Equal(name, AccessibleNames(cut)[fourth]);
            Assert.Equal(tooltip, Tooltips(cut)[fourth]);
            Assert.Equal([caption], SelectedCaptions(cut));
        }

        if (shortLabels) AssertFourthReads("TG", "TG (Too good)", "Too good");
        else AssertFourthReads("Too good", "Too good", "Too good");

        // Gammons possible → not possible.
        cut.Render(p => p.Add(c => c.Decision, WithoutGammons));

        if (shortLabels) AssertFourthReads("NP", "NP (No double / Pass)", "No double / Pass");
        else AssertFourthReads("No double / Pass", "No double / Pass", "No double / Pass");

        // ...and back.
        cut.Render(p => p.Add(c => c.Decision, WithGammons));

        if (shortLabels) AssertFourthReads("TG", "TG (Too good)", "Too good");
        else AssertFourthReads("Too good", "Too good", "Too good");

        Assert.Equal(CubeAnswer.NoDoublePass, cut.Instance.Value);
        Assert.Equal(0, fired);
    }

    [Fact]
    public void ChangingTheForm_RelabelsThePills_SelectingNothingAndFiringNothing()
    {
        var fired = 0;
        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.Value, CubeAnswer.NoDoublePass)
            .Add(c => c.Decision, WithoutGammons)
            .Add(c => c.ValueChanged, (CubeAnswer? _) => fired++));

        Assert.Equal(FullWhereGammonsNotPossible, Captions(cut));

        // Full → short.
        cut.Render(p => p.Add(c => c.ShortLabels, true));

        Assert.Equal(ShortWhereGammonsNotPossible, Captions(cut));
        Assert.Equal(ShortFormNamesWhereGammonsNotPossible, AccessibleNames(cut));
        Assert.Equal(FullWhereGammonsNotPossible, Tooltips(cut));
        Assert.Equal(["NP"], SelectedCaptions(cut));

        // ...and back.
        cut.Render(p => p.Add(c => c.ShortLabels, false));

        Assert.Equal(FullWhereGammonsNotPossible, Captions(cut));
        Assert.Equal(FullWhereGammonsNotPossible, AccessibleNames(cut));
        Assert.Equal(FullWhereGammonsNotPossible, Tooltips(cut));
        Assert.Equal(["No double / Pass"], SelectedCaptions(cut));

        Assert.Equal(CubeAnswer.NoDoublePass, cut.Instance.Value);
        Assert.Equal(0, fired);
    }

    // -----------------------------------------------------------------------
    //  Value → selection. Exactly one pill lit, the one whose answer it is.
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(GammonFacts))]
    public void Render_NullValue_NothingSelected(bool gammonsPossible)
    {
        AssertNothingSelected(RenderRow(value: null, decision: DecisionAt(gammonsPossible)));
    }

    [Theory]
    [MemberData(nameof(AnswersAtBothGammonFacts))]
    public void Value_MarksExactlyTheMatchingPill(CubeAnswer answer, bool gammonsPossible)
    {
        var cut = RenderRow(answer, DecisionAt(gammonsPossible));

        Assert.Equal([FullLabelsAt(gammonsPossible)[IndexOf(answer)]], SelectedCaptions(cut));

        var radios = Radios(cut);
        for (var i = 0; i < radios.Count; i++)
            Assert.Equal(i == IndexOf(answer), radios[i].HasAttribute("checked"));
    }

    /// <summary>
    /// A value outside the four <see cref="CubeAnswer"/> members renders
    /// nothing selected. That is a caller bug surfacing, and it is pinned as
    /// one: the row must not remap an undefined answer onto some pill as a
    /// fallback.
    /// </summary>
    [Fact]
    public void Value_OutsideTheFour_RendersNothingSelected()
    {
        foreach (var undefined in new[] { (CubeAnswer)4, (CubeAnswer)(-1) })
        {
            var cut = RenderRow(undefined);

            Assert.Equal(FullWhereGammonsPossible, Captions(cut));
            AssertNothingSelected(cut);
        }
    }

    [Fact]
    public void ClearingValue_ClearsTheSelection()
    {
        // The consumer's advance-to-next-problem path: there is no request to
        // key an automatic reset off, so the consumer clears by setting Value
        // back to null.
        var cut = RenderRow(CubeAnswer.DoublePass);
        Assert.Single(SelectedCaptions(cut));

        cut.Render(p => p.Add(c => c.Value, null));

        AssertNothingSelected(cut);
    }

    // -----------------------------------------------------------------------
    //  Selection → ValueChanged. One radio is one whole answer: every
    //  selection fires once with its answer, never null.
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(AnswersAtBothGammonFacts))]
    public async Task SelectingAPill_FiresOnceWithItsAnswer(CubeAnswer answer, bool gammonsPossible)
    {
        CubeAnswer? received = null;
        var fireCount = 0;

        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.Decision, DecisionAt(gammonsPossible))
            .Add(c => c.ValueChanged,
                (CubeAnswer? received_) => { received = received_; fireCount++; }));

        await Radios(cut)[IndexOf(answer)].ChangeAsync(new ChangeEventArgs { Value = true });

        Assert.Equal(1, fireCount);
        Assert.NotNull(received);
        Assert.Equal(answer, received);
    }

    [Fact]
    public async Task SelectingAPill_InTheShortForm_FiresWithTheSameAnswer()
    {
        CubeAnswer? received = null;
        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.Decision, WithoutGammons)
            .Add(c => c.ShortLabels, true)
            .Add(c => c.ValueChanged, (CubeAnswer? answer) => received = answer));

        await Radios(cut)[IndexOf(CubeAnswer.NoDoublePass)]
            .ChangeAsync(new ChangeEventArgs { Value = true });

        // The form changes what a pill reads, not which answer it is.
        Assert.Equal(CubeAnswer.NoDoublePass, received);
    }

    [Fact]
    public async Task ChangingTheSelection_RefiresWithTheNewAnswer()
    {
        var received = new List<CubeAnswer?>();
        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.Value, CubeAnswer.NoDouble)
            .Add(c => c.Decision, WithGammons)
            .Add(c => c.ValueChanged, (CubeAnswer? answer) => received.Add(answer)));

        await Radios(cut)[IndexOf(CubeAnswer.NoDoublePass)]
            .ChangeAsync(new ChangeEventArgs { Value = true });

        // No one-shot lock: a row already holding an answer reports the new one.
        Assert.Equal([CubeAnswer.NoDoublePass], received);
    }

    // -----------------------------------------------------------------------
    //  Controlled round trip — the consumer wiring @bind-Value compiles to:
    //  ValueChanged writes the answer back into Value, and the selection
    //  renders from the written-back Value on the next parameter pass.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ValueWriteback_RoundTrip_SelectsThenSwitches()
    {
        CubeAnswer? current = null;

        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.Value, current)
            .Add(c => c.Decision, WithGammons)
            .Add(c => c.ValueChanged, (CubeAnswer? answer) => current = answer));

        await Radios(cut)[IndexOf(CubeAnswer.DoublePass)]
            .ChangeAsync(new ChangeEventArgs { Value = true });
        Assert.Equal(CubeAnswer.DoublePass, current);

        cut.Render(p => p.Add(c => c.Value, current));
        Assert.Equal(["Double / Pass"], SelectedCaptions(cut));

        await Radios(cut)[IndexOf(CubeAnswer.NoDoublePass)]
            .ChangeAsync(new ChangeEventArgs { Value = true });
        Assert.Equal(CubeAnswer.NoDoublePass, current);

        cut.Render(p => p.Add(c => c.Value, current));
        Assert.Equal(["Too good"], SelectedCaptions(cut));
    }

    /// <summary>
    /// Strictly controlled: the row holds no selection of its own. A consumer
    /// that binds <c>ValueChanged</c> but never writes the answer back never
    /// adopts it — the next render still reads the <c>Value</c> it is holding,
    /// and the selection is whatever that says.
    /// </summary>
    [Fact]
    public async Task StrictlyControlled_SelectionFollowsValue_WithoutAWriteback()
    {
        var cut = RenderRow();

        await Radios(cut)[IndexOf(CubeAnswer.DoubleTake)]
            .ChangeAsync(new ChangeEventArgs { Value = true });

        cut.Render(p => p.Add(c => c.Value, null));

        AssertNothingSelected(cut);
    }

    // -----------------------------------------------------------------------
    //  Required parameters — the promoted mechanism for silent splats. A
    //  consumer that omits either fails RZ2012 under warnings-as-errors; the
    //  attribute's presence is what that gate stands on.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(nameof(BackgammonCubeActions.ValueChanged))]
    [InlineData(nameof(BackgammonCubeActions.Decision))]
    public void Parameter_IsEditorRequired(string parameterName)
    {
        var property = typeof(BackgammonCubeActions).GetProperty(parameterName)!;

        Assert.NotNull(property.GetCustomAttribute<ParameterAttribute>());
        Assert.NotNull(property.GetCustomAttribute<EditorRequiredAttribute>());
    }

    /// <summary>
    /// RZ2012 reaches only a Razor consumer at compile time; a missing
    /// decision at run time is refused at the contract boundary rather than
    /// rendered as a row that cannot label its answers.
    /// </summary>
    [Fact]
    public void MissingDecision_IsRefused_NamingDecision()
    {
        var refusal = Assert.Throws<ArgumentNullException>(() =>
            Render<BackgammonCubeActions>(p => p
                .Add(c => c.ValueChanged, (CubeAnswer? _) => { })));

        Assert.Equal(nameof(BackgammonCubeActions.Decision), refusal.ParamName);
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
    //  Source pins — what the component must not contain. Sources are read
    //  with comments stripped, so prose about a thing can neither fail an
    //  assertion about the code nor satisfy one.
    // -----------------------------------------------------------------------

    /// <summary>
    /// The component spells no cube wording and reads no gammon rule: every
    /// label comes from <see cref="CubeLabels"/>, and which label the fourth
    /// answer takes is the decision's reading, which the label home renders.
    /// A caption literal or a rule read here would be a second spelling or a
    /// second derivation beside the one home.
    /// </summary>
    [Fact]
    public void Component_SpellsNoCubeWording_AndReadsNoRule()
    {
        var code = ComponentCode();

        foreach (var wording in FullWhereGammonsPossible.Concat(FullWhereGammonsNotPossible)
                     .Concat(ShortWhereGammonsPossible).Concat(ShortWhereGammonsNotPossible)
                     .Distinct())
        {
            Assert.DoesNotContain($"\"{wording}\"", code);
        }

        foreach (var rule in new[] { "GammonsPossible", "ClaimOf", "CubeClaim", "IsJacoby", "CubeOwner", "MoneySession", "MatchSession" })
            Assert.DoesNotContain(rule, code);

        Assert.Contains("CubeLabels.Label(", code);
        Assert.Contains("CubeLabels.ShortLabel(", code);
    }

    /// <summary>
    /// The retired surfaces are gone, not shimmed. The claim × response pair
    /// and the Too good offerability fact retired with SPEC-scoring §3's
    /// amendments on halheinrich/backgammon#326 (the answer is one of four, and
    /// all four are always offered), so the component no longer names the
    /// pair, the fact or its producer member, nor keeps an option table of its
    /// own. The two-axis row before them stays gone too: no nested group
    /// element, no per-axis accessible names or tables, and never the
    /// action-level <c>CubeDecisionPair</c>.
    /// </summary>
    [Fact]
    public void RetiredSurfaces_AreGoneFromTheComponentSource()
    {
        var code = ComponentCode();

        Assert.DoesNotContain("CubeClaimPair", code);
        Assert.DoesNotContain("OfferTooGood", code);
        Assert.DoesNotContain("CanBeTooGood", code);
        Assert.DoesNotContain("_options", code);

        Assert.DoesNotContain("bg-cube-actions-group", code);
        Assert.DoesNotContain("Doubler claim", code);
        Assert.DoesNotContain("Taker response", code);
        Assert.DoesNotContain("_claimOptions", code);
        Assert.DoesNotContain("_takerOptions", code);
        Assert.DoesNotContain("CubeDecisionPair", code);

        // ...and the answer-valued, decision-labelled surface is what stands.
        Assert.Contains("CubeAnswer", code);
        Assert.Contains("CubeDecision Decision", code);
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
    /// the keyboard reaches, in both forms. Dropping the input, or hiding it
    /// with the markup switches asserted against here, would take the row's
    /// native radio-group behavior (arrow-key roving, mutual exclusion by
    /// name) and its accessible name with it — the cheap way to "remove the
    /// dot", and the wrong one.
    /// </summary>
    [Theory]
    [MemberData(nameof(GammonFactsAndForms))]
    public void Render_RadioInputs_StayRealFocusableControls(bool gammonsPossible, bool shortLabels)
    {
        var radios = Radios(RenderRow(decision: DecisionAt(gammonsPossible), shortLabels: shortLabels));
        Assert.Equal(4, radios.Count);

        foreach (var radio in radios)
        {
            Assert.False(radio.HasAttribute("hidden"),
                "a `hidden` attribute would remove the radio from the tab order " +
                "and the accessibility tree — the dot is hidden in CSS.");
            Assert.False(radio.HasAttribute("aria-hidden"),
                "aria-hidden would strip the control from the accessibility tree.");
            Assert.False(radio.HasAttribute("disabled"),
                "a disabled radio is not focusable — every answer is offered, " +
                "never rendered disabled.");
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
    /// and all three stand here. The four full labels with the fourth reading
    /// Too good measure, at these constants,
    /// 89.3 + 113.9 + 116.0 + 82.2 = 401.4px of pills plus three 4px gaps:
    /// 413.5px unselected, 418.8px with the widest pill (Double / Pass)
    /// selected and 419.3px at most over any selection (weight 600 widens the
    /// selected caption) — under the consumer's 16px Helvetica/Arial stack.
    /// The longer fourth label, No double / Pass, and the short form are not
    /// measured here: whether the row clears the consumer's row, and where
    /// the host switches to the short form, are the consumer's measurements to
    /// take (SPEC-quiz-view §4), and these numbers are its input. With one
    /// group there is one gap, the compacted one: the wider inter-group gap
    /// went with the second group. bUnit cannot evaluate any of that; what
    /// it can do is stop the constants being widened back without a fresh
    /// measurement.
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
    /// The component's code-behind and markup with comments stripped.
    /// </summary>
    private static string ComponentCode() =>
        StripComments(ComponentSource("BackgammonCubeActions.razor.cs"))
        + StripComments(ComponentSource("BackgammonCubeActions.razor"));

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
