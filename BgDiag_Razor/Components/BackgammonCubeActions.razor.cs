using Microsoft.AspNetCore.Components;
using BgDataTypes_Lib;

namespace BgDiag_Razor.Components;

/// <summary>
/// Free-standing cube-decision answer row: one radio group offering the
/// reachable cube verdicts as whole <see cref="CubeClaimPair"/>s — "No
/// double", "Double / Take", "Double / Pass" and, when the position admits it,
/// "Too good". One selection is one complete answer, emitted via
/// <see cref="ValueChanged"/>; scoring that answer against the position's
/// derived truth is the consumer's (quiz layer's) job, not this component's.
///
/// <para>
/// <b>Four pairs, one group.</b> The answer is still the (claim, taker) pair
/// the umbrella's <c>SPEC-scoring.md</c> §3 models (halheinrich/backgammon#86):
/// the doubler's <see cref="CubeClaim"/> about the position and the taker's
/// <see cref="CubeAction.Take"/> / <see cref="CubeAction.Pass"/> response if
/// doubled, scored per half. What the 2026-09-02 amendment
/// (halheinrich/backgammon#187) changed is which pairs can be a verdict at
/// all: Too Good now requires the pass, so the reachable verdict set is
/// exactly four pairs, and the option set is those four — presented as
/// compound pills whose captions name both halves, so nothing is asserted
/// silently. The two cells the type still represents are not offered:
/// <see cref="CubeClaimPair.TooGoodTake"/> is a retired verdict, and
/// <see cref="CubeClaimPair.NoDoublePass"/> is the incoherent cell the
/// amendment made moot. The pairs are the type's own canonical instances;
/// this component composes none.
/// </para>
///
/// <para>
/// <b>Too good is offered by fact, not derived here.</b> A money position
/// under the Jacoby rule with a centred cube cannot be too good — gammons do
/// not count until the cube turns, so the no-double equity never exceeds the
/// cash — and the amendment rules the fourth pill withheld there. Whether a
/// position admits the verdict is a fact about the position, derived once at
/// the producer as <see cref="BgDecisionData.CanBeTooGood"/>; the consumer
/// passes it through <see cref="OfferTooGood"/>, and this component never
/// re-derives it from rules fields it does not see. Withholding is the only
/// contextual change the row makes; the other three pairs are offered for
/// every cube decision.
/// </para>
///
/// <para>
/// <b>Board-free by design.</b> The row renders no position and takes no
/// <c>DiagramRequest</c> — cube decisions have no click-by-click board state, so
/// the answer chrome is free-standing and the consumer places it wherever its
/// layout wants (e.g. inline in a button row beside its own submit/skip buttons),
/// rendering the position separately with the view-only
/// <see cref="BackgammonDiagram"/>. Routing by <c>Decision.IsCube</c> stays
/// consumer-side; <see cref="BackgammonPlayEntry"/> still rejects cube decisions
/// at its contract boundary.
/// </para>
///
/// <para>
/// <b>Value contract: strictly controlled.</b> <see cref="Value"/> is the
/// selected pair or <c>null</c>, and the row renders from it and nothing
/// else — there is no component state, because a selection is a whole pair
/// and no partial answer exists to hold. Every selection fires
/// <see cref="ValueChanged"/> with the chosen pair; the component never
/// selects on its own, so a consumer that ignores the callback sees its
/// selection snap back to <see cref="Value"/> at the next render, its own
/// answer field remaining the single source of truth. A consumer clears the
/// row for the next problem by setting <see cref="Value"/> to <c>null</c>.
/// </para>
///
/// <para>
/// <b>Sizing posture.</b> The pills are compact and inline-flow-friendly by
/// intent: the row takes only its content size, carries no external margins, and
/// each pill's height falls out of its own padding and line-height — roughly a
/// standard button's height, without encoding any consumer's button metrics.
/// Spacing around the row belongs to the consumer's composition context.
/// </para>
///
/// <para>
/// The row's <i>horizontal</i> metrics are a measured contract rather than free
/// styling. This row is the widest element of the consuming quiz page's action
/// row, and at its original metrics it out-widened the board and wrapped through
/// the 641–1366px band, adding a line of chrome that cost board pixels wherever
/// the board is height-bound. The compacted form — tight pill gap, tight pill
/// inline padding, and a visually hidden radio dot (see below) — is the ruled
/// resolution, and it is unconditional: no media query gates it, because a
/// producer component has no view of the consumer's layout to gate one on.
/// Re-widening any of the three reopens the wrap, so take a fresh measurement
/// first. See the umbrella's <c>SPEC-quiz-view.md</c> §2 invariance floor and
/// issue halheinrich/backgammon#99.
/// </para>
///
/// <para>
/// <b>The radio dot is hidden, not dropped.</b> The pill's own border, fill and
/// weight carry the selected state, so the native dot is redundant and stops
/// being painted: the <c>input</c> is stretched transparently over its own pill
/// rather than removed. It stays rendered, focusable, and in the accessibility
/// tree, keeping the browser's native radio-group behavior (arrow-key roving,
/// mutual exclusion by name) and the control's accessible name; the visible
/// keyboard focus ring moves from the dot to the pill, and the pill's whole area
/// becomes the input's own hit target. Consumers therefore still get a real
/// radio group — what changed is what gets painted, not the semantics — and can
/// still drive it by pointer, keyboard, or an automation harness.
///
/// <para>
/// Restyling the input away with <c>display: none</c>, <c>visibility: hidden</c>
/// or a zeroed size would take it out of the tab order and the accessibility
/// tree. So would the sr-only <c>clip-path</c> recipe, less visibly: that clips
/// hit-testing as well as painting, leaving the control unreachable by pointer
/// even though it still reads correctly to a screen reader.
/// </para>
/// </para>
///
/// <para>
/// <b>Instance-unique radio group name.</b> Browsers enforce radio mutual
/// exclusion by <c>name</c> document-wide, so the name is generated per
/// instance and two rows on one page never cross-link. It is internal —
/// consumers interact only through <see cref="Value"/> /
/// <see cref="ValueChanged"/>, and address the group by its
/// <c>aria-label</c> or the <c>bg-cube-actions</c> class.
/// </para>
/// </summary>
public partial class BackgammonCubeActions : ComponentBase
{
    // -----------------------------------------------------------------------
    //  Parameters
    // -----------------------------------------------------------------------

    /// <summary>
    /// The currently selected answer, or <c>null</c> when nothing is selected.
    /// The component renders strictly from this parameter: it never selects a
    /// pill on its own, so a consumer that ignores <see cref="ValueChanged"/>
    /// sees the selection snap back on the next render. Set to <c>null</c> to
    /// clear the row when advancing to a new problem.
    ///
    /// <para>
    /// Only the offered pairs can render as selected. A value outside them —
    /// <see cref="CubeClaimPair.TooGoodTake"/> or
    /// <see cref="CubeClaimPair.NoDoublePass"/>, which the type represents
    /// but no cube decision offers, or <see cref="CubeClaimPair.TooGoodPass"/>
    /// while <see cref="OfferTooGood"/> is <c>false</c> — renders nothing
    /// selected. That is a caller bug surfacing, not a fallback: the row does
    /// not remap an unoffered pair onto a pill, and a consumer holding one
    /// has handed this row an answer the position cannot receive.
    /// </para>
    /// </summary>
    [Parameter]
    public CubeClaimPair? Value { get; set; }

    /// <summary>
    /// Fires on every selection, carrying the chosen <see cref="CubeClaimPair"/>.
    /// One radio is one whole pair, so the callback never carries <c>null</c>
    /// and there is no incomplete answer for it to fire on. It re-fires
    /// whenever the selection moves, so the consumer always holds the current
    /// answer. Pairs with <see cref="Value"/> for <c>@bind-Value</c>.
    ///
    /// <para>
    /// Marked <see cref="EditorRequiredAttribute"/>: without this binding the
    /// row is inert (strictly controlled — see <see cref="Value"/>), and an
    /// out-of-date attribute name on a Razor consumer would otherwise splat
    /// silently. RZ2012 surfaces the missing binding at compile time; build
    /// with warnings-as-errors to make that a hard gate.
    /// </para>
    /// </summary>
    [Parameter, EditorRequired]
    public EventCallback<CubeClaimPair?> ValueChanged { get; set; }

    /// <summary>
    /// Whether the position admits the Too Good verdict, and so whether the
    /// "Too good" pill is offered. <c>false</c> renders the other three
    /// pairs only. The consumer passes the producer's own fact —
    /// <see cref="BgDecisionData.CanBeTooGood"/>, which is <c>false</c> exactly
    /// for a money position under the Jacoby rule with a centred cube
    /// (SPEC-scoring §3, 2026-09-02 amendment, halheinrich/backgammon#187) —
    /// and this component never derives it: it has no view of the position's
    /// rules and would only be restating a rule that has one home.
    ///
    /// <para>
    /// Marked <see cref="EditorRequiredAttribute"/> because a forgotten
    /// binding would be a silent splat with a wrong default either way: a
    /// <c>bool</c> defaults to <c>false</c>, which would withhold Too Good
    /// from every position. RZ2012 surfaces the omission at compile time;
    /// build with warnings-as-errors to make that a hard gate.
    /// </para>
    /// </summary>
    [Parameter, EditorRequired]
    public bool OfferTooGood { get; set; }

    /// <summary>
    /// Catch-all for arbitrary HTML attributes (e.g. <c>style</c>, <c>id</c>,
    /// <c>class</c>) splatted onto the root <c>div</c> (<c>bg-cube-actions</c>).
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    // -----------------------------------------------------------------------
    //  Internal table — the single source for the radio options, in render
    //  order: the four reachable verdicts of SPEC-scoring §3 as amended
    //  2026-09-02 (halheinrich/backgammon#187), each mapped to the pair's own
    //  canonical instance. The order walks the claim axis in CubeClaim's
    //  declaration order and the taker axis Take-before-Pass within it, which
    //  is also the verdict table's order in the spec.
    //
    //  The captions are this component's own UI text, spelled by the amended
    //  pair-label ruling on halheinrich/backgammon#185: a pair reads as its
    //  claim alone when that claim has exactly one reachable pair, else as
    //  claim and response joined by " / " in sentence case — so the implied
    //  half is omitted, because No double implies Take and Too good implies
    //  Pass under SPEC-scoring §3's 2026-09-02 amendment, while Double needs
    //  its response spelled. BgDataTypes_Lib spells no display wording for
    //  CubeClaim or CubeAction, so nothing here is a second spelling of a
    //  producer's label; a future label home re-sources these strings, and
    //  this table then loses them and keeps its order.
    // -----------------------------------------------------------------------

    private static readonly (string Label, CubeClaimPair Pair)[] _options =
    [
        ("No double",     CubeClaimPair.NoDoubleTake),
        ("Double / Take", CubeClaimPair.DoubleTake),
        ("Double / Pass", CubeClaimPair.DoublePass),
        ("Too good",      CubeClaimPair.TooGoodPass),
    ];

    /// <summary>
    /// The options this render offers, in table order: the whole table, or
    /// the table without its Too Good pair when <see cref="OfferTooGood"/> is
    /// <c>false</c>. The gate is on the claim, which is the axis the
    /// offerability fact is about — a second Too Good pair would be withheld
    /// with it, not by a positional slice.
    /// </summary>
    private IEnumerable<(string Label, CubeClaimPair Pair)> OfferedOptions =>
        OfferTooGood
            ? _options
            : _options.Where(o => o.Pair.Claim != CubeClaim.TooGood);

    // -----------------------------------------------------------------------
    //  Instance-unique radio group name — browsers enforce radio mutual
    //  exclusion by name document-wide, so a hardcoded name would cross-link
    //  two instances rendered on the same page.
    // -----------------------------------------------------------------------

    private readonly string _groupName = $"bg-cube-actions-{Guid.NewGuid():N}";

    // -----------------------------------------------------------------------
    //  Radio selection routing
    // -----------------------------------------------------------------------

    /// <summary>
    /// Emits the selected option's <see cref="CubeClaimPair"/>. No state is
    /// recorded here — the selection renders only once the consumer writes it
    /// back into <see cref="Value"/> (strictly controlled).
    /// </summary>
    private Task HandleSelected(CubeClaimPair pair) =>
        ValueChanged.InvokeAsync(pair);
}
