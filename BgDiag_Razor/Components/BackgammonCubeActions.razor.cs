using Microsoft.AspNetCore.Components;
using BgDataTypes_Lib;

namespace BgDiag_Razor.Components;

/// <summary>
/// Free-standing cube-decision answer row: two orthogonal radio groups whose
/// cross-product is the answer — the doubler's three-valued
/// <see cref="CubeClaim"/> claim ("No double" / "Double" / "Too good") and the
/// taker's <see cref="CubeAction.Take"/> / <see cref="CubeAction.Pass"/>
/// response if doubled. A complete selection is one
/// <see cref="CubeClaimPair"/>, emitted via <see cref="ValueChanged"/>;
/// scoring that answer against the position's derived truth is the consumer's
/// (quiz layer's) job, not this component's.
///
/// <para>
/// <b>Two axes, not four compound options.</b> The row was a single group of
/// four compound pills over the action-level <c>CubeDecisionPair</c>, which
/// could not express "too good to double, and they would still take" at all
/// and reused its fourth pill for a claim its label did not name. The shape
/// ruled by the umbrella's <c>SPEC-scoring.md</c> §3
/// (<c>halheinrich/backgammon#86</c>) is the 3×2 of claim × taker, entered as
/// two independent groups: the doubler half carries the <i>claim</i> about the
/// position and the taker half the response <i>if doubled</i>, answered
/// explicitly even when the claim is a no-double. The compound row's habit of
/// silently asserting the taker half is what this removes.
/// </para>
///
/// <para>
/// <b>The option set is uniform and complete.</b> All three claims and both
/// taker responses are offered for every cube decision, whatever rules are in
/// force — "Too good" is never contextually withdrawn, because it genuinely
/// occurs in money too, including under Jacoby via redoubles (SPEC-scoring §3,
/// "Uniform availability"). All six cells are therefore selectable, the
/// incoherent (<see cref="CubeClaim.NoDouble"/>,
/// <see cref="CubeAction.Pass"/>) included: the axes are deliberately not
/// cross-disabled, because choosing that cell reveals a misunderstanding a
/// review surface can name (SPEC-scoring §3, "The incoherent cell is
/// allowed"; <see cref="CubeClaimPair.IsIncoherent"/> names it). This row
/// represents an answer; it never prevents one.
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
/// <b>Value contract: controlled on the pair, own state on the halves.</b>
/// <see cref="Value"/> is the complete answer and nothing less — it is
/// <c>null</c> until both halves are chosen. A half-chosen row has no
/// <see cref="CubeClaimPair"/> to be, so the two half-selections are the
/// component's own state; there is no half-shaped parameter to hold them and
/// inventing one would put a second answer shape on the public surface. The
/// halves are otherwise strictly subordinate to <see cref="Value"/>: whenever
/// the parameter disagrees with what the halves compose to, the parameter
/// wins and the halves are re-seeded from it (both cleared when it is
/// <c>null</c>). So a consumer clears the row for the next problem exactly as
/// before — set <see cref="Value"/> to <c>null</c> — and a consumer that
/// ignores <see cref="ValueChanged"/> sees the selection snap back at its next
/// render pass, the answer field it holds remaining the single source of truth.
/// </para>
///
/// <para>
/// <b>Each half moves independently.</b> Selecting in one group never
/// auto-completes or disturbs the other: picking "Double" first leaves the
/// taker half unanswered and <see cref="Value"/> <c>null</c>; the pair exists
/// only once both groups have a selection. From then on every change to either
/// half re-emits the recomposed pair, so the consumer always holds the latest
/// complete answer. <see cref="ValueChanged"/> therefore never fires with
/// <c>null</c> and never fires for an incomplete row. Whether an incomplete
/// row may be submitted is the consumer's gate, not this component's — it has
/// no view of a submit affordance.
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
/// first. The split into two groups spends one wider inter-group gap and buys
/// back more than it spends: the five short captions total fewer characters
/// than the four compound ones they replace. See the umbrella's
/// <c>SPEC-quiz-view.md</c> §2 invariance floor and issue
/// <c>halheinrich/backgammon#99</c>.
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
/// becomes the input's own hit target. Consumers therefore still get real
/// radio groups — what changed is what gets painted, not the semantics — and can
/// still drive them by pointer, keyboard, or an automation harness.
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
/// <b>Instance-unique radio groups.</b> The two groups carry distinct
/// <c>name</c>s so a claim radio and a taker radio are never mutually exclusive
/// with each other, and both names are generated per instance, so two rows on
/// one page never cross-link their browser-native mutual exclusion either. The
/// names are internal — consumers interact only through <see cref="Value"/> /
/// <see cref="ValueChanged"/>, and address the groups by their
/// <c>aria-label</c> or the <c>bg-cube-actions-group</c> class.
/// </para>
/// </summary>
public partial class BackgammonCubeActions : ComponentBase
{
    // -----------------------------------------------------------------------
    //  Parameters
    // -----------------------------------------------------------------------

    /// <summary>
    /// The currently selected answer, or <c>null</c> when the row holds no
    /// complete answer — either nothing is selected or only one half is. The
    /// component never selects on its own and never holds a pair the consumer
    /// has not been told about: whenever this parameter disagrees with the two
    /// half-selections, the parameter wins and the halves are re-seeded from
    /// it. Set to <c>null</c> to clear the row when advancing to a new problem.
    /// </summary>
    [Parameter]
    public CubeClaimPair? Value { get; set; }

    /// <summary>
    /// Fires whenever a selection completes or changes the answer, carrying the
    /// recomposed <see cref="CubeClaimPair"/>. It never carries <c>null</c> and
    /// never fires for a half-answered row: selecting the first half leaves the
    /// answer incomplete (and <see cref="Value"/> already <c>null</c>), so
    /// there is nothing to report; once both halves are set, every subsequent
    /// change to either half re-fires with the updated pair. Pairs with
    /// <see cref="Value"/> for <c>@bind-Value</c>.
    ///
    /// <para>
    /// Marked <see cref="EditorRequiredAttribute"/>: without this binding the
    /// row's selections are never adopted (the halves re-seed from a
    /// <see cref="Value"/> that never changes), and an out-of-date attribute
    /// name on a Razor consumer would otherwise splat silently. RZ2012 surfaces
    /// the missing binding at compile time; build with warnings-as-errors to
    /// make that a hard gate.
    /// </para>
    /// </summary>
    [Parameter, EditorRequired]
    public EventCallback<CubeClaimPair?> ValueChanged { get; set; }

    /// <summary>
    /// Catch-all for arbitrary HTML attributes (e.g. <c>style</c>, <c>id</c>,
    /// <c>class</c>) splatted onto the root <c>div</c> (<c>bg-cube-actions</c>).
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    // -----------------------------------------------------------------------
    //  Internal tables — one per axis, each the single source for that group's
    //  radios in render order. The claim table's order is CubeClaim's own
    //  declaration order, which is the axis as SPEC-scoring §3 rules it
    //  ({No Double, Double, Too Good}); the taker table's is Take before Pass.
    //
    //  The captions are this component's own UI text. BgDataTypes_Lib spells no
    //  display wording for either CubeClaim or CubeAction, so nothing here is a
    //  second spelling of a producer's label; consolidating cube wording at the
    //  producer is the standing charter question this arc's later legs settle
    //  (see BgQuiz's CubeActionDisplay), and it is a producer decision, not one
    //  to pre-empt from a consumer. If it lands, these tables lose their strings
    //  and keep their order.
    // -----------------------------------------------------------------------

    private static readonly (string Label, CubeClaim Claim)[] _claimOptions =
    [
        ("No double", CubeClaim.NoDouble),
        ("Double",    CubeClaim.Double),
        ("Too good",  CubeClaim.TooGood),
    ];

    private static readonly (string Label, CubeAction Taker)[] _takerOptions =
    [
        ("Take", CubeAction.Take),
        ("Pass", CubeAction.Pass),
    ];

    // -----------------------------------------------------------------------
    //  Instance-unique radio group names — browsers enforce radio mutual
    //  exclusion by name document-wide, so the two axes need different names
    //  (or choosing a claim would deselect the taker) and both need an instance
    //  suffix (or two rows on one page would cross-link).
    // -----------------------------------------------------------------------

    private readonly string _claimGroupName = $"bg-cube-claim-{Guid.NewGuid():N}";
    private readonly string _takerGroupName = $"bg-cube-taker-{Guid.NewGuid():N}";

    // -----------------------------------------------------------------------
    //  Half-selection state
    //
    //  The two halves chosen so far, each null until its group is answered.
    //  This is the component's own state only because a half-answered row has
    //  no CubeClaimPair to be — see the value contract in the type doc. It is
    //  re-seeded from Value on every parameter pass that disagrees with it, so
    //  it can never quietly diverge from the consumer's answer field.
    // -----------------------------------------------------------------------

    private CubeClaim? _claim;
    private CubeAction? _taker;

    /// <summary>
    /// What the two half-selections currently compose to: the complete
    /// <see cref="CubeClaimPair"/> once both are set, otherwise <c>null</c>.
    /// Each half comes from its own axis, so the pair built here always
    /// satisfies <see cref="CubeClaimPair"/>'s half-guards — construction never
    /// throws in this component.
    /// </summary>
    private CubeClaimPair? Composed =>
        _claim is { } claim && _taker is { } taker
            ? new CubeClaimPair(claim, taker)
            : null;

    /// <summary>
    /// Re-seeds the halves from <see cref="Value"/> whenever the two disagree —
    /// the whole of the subordination rule. Agreement is the steady state (the
    /// consumer wrote back the pair just emitted), so this is a no-op on the
    /// ordinary path; disagreement means either the consumer moved the value (a
    /// clear between problems, or an externally set answer) or it declined the
    /// one just emitted, and both resolve the same way: the parameter wins. A
    /// half-answered row composes to <c>null</c> and so sits undisturbed under
    /// a <c>null</c> <see cref="Value"/>, which is what lets the first half
    /// survive until the second is chosen.
    /// </summary>
    protected override void OnParametersSet()
    {
        if (Value != Composed)
        {
            _claim = Value?.Claim;
            _taker = Value?.Taker;
        }
    }

    // -----------------------------------------------------------------------
    //  Radio selection routing — one handler per axis, both recomposing.
    // -----------------------------------------------------------------------

    /// <summary>Records the doubler-half claim and reports the recomposed answer.</summary>
    private Task HandleClaimSelected(CubeClaim claim)
    {
        _claim = claim;
        return EmitIfAnswerChanged();
    }

    /// <summary>Records the taker-half response and reports the recomposed answer.</summary>
    private Task HandleTakerSelected(CubeAction taker)
    {
        _taker = taker;
        return EmitIfAnswerChanged();
    }

    /// <summary>
    /// Reports the recomposed answer, and only when it is news. A first-half
    /// selection composes to <c>null</c> while <see cref="Value"/> is already
    /// <c>null</c>, so nothing fires for an incomplete row; re-selecting the
    /// half already chosen likewise changes nothing. No selection is recorded
    /// as an answer here — it becomes one only once the consumer writes the
    /// pair back into <see cref="Value"/>.
    /// </summary>
    private Task EmitIfAnswerChanged()
    {
        var composed = Composed;
        return composed == Value ? Task.CompletedTask : ValueChanged.InvokeAsync(composed);
    }
}
