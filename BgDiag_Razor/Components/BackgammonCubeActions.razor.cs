using Microsoft.AspNetCore.Components;
using BackgammonDiagram_Lib;
using BgDataTypes_Lib;

namespace BgDiag_Razor.Components;

/// <summary>
/// Free-standing cube-decision answer row: one radio group offering the four
/// cube answers (<see cref="CubeAnswer"/>), each pill labelled at the decision
/// it answers (<see cref="Decision"/>) by the label home,
/// <see cref="CubeLabels"/>. One selection is one complete answer, emitted via
/// <see cref="ValueChanged"/>; what that answer costs at the decision is the
/// consumer's (quiz layer's) to ask the producer, not this component's.
///
/// <para>
/// <b>Four answers, always offered.</b> The answer model is the umbrella's
/// <c>SPEC-scoring.md</c> §3 (amended on halheinrich/backgammon#326), whose
/// type is <see cref="CubeAnswer"/>. The row offers every member, in the
/// type's declaration order, which the type states is the offered order. It
/// holds no option table of its own and withholds nothing.
/// </para>
///
/// <para>
/// <b>Labelled at the decision, spelled by the label home.</b> Each pill reads
/// <see cref="CubeLabels.Label(CubeAnswer, CubeDecision)"/> at
/// <see cref="Decision"/>, or, in the short form,
/// <see cref="CubeLabels.ShortLabel(CubeAnswer, CubeDecision)"/>. Which label
/// the fourth answer takes is the decision's reading of it
/// (<see cref="CubeDecision.ClaimOf"/>), rendered by the label home. This
/// component spells no cube wording and reads no rule, so a re-wording or a
/// re-ruling at either home reaches this row with no edit to it.
/// </para>
///
/// <para>
/// <b>The short form is the host's call.</b> The umbrella's
/// <c>SPEC-quiz-view.md</c> §4 ("The action row under quiz navigation") rules
/// when the labels abbreviate and that the full label stays each pill's
/// accessible name and tooltip. Only the host knows what else shares its row,
/// so the host decides and sets <see cref="ShortLabels"/>; this component
/// measures nothing and guesses no width. Either form keeps the full label as
/// the radio's <c>aria-label</c> and the pill's <c>title</c>, and the native
/// radio group's semantics are the same in both.
/// </para>
///
/// <para>
/// <b>Board-free by design.</b> The row renders no position and takes the
/// record, not a <c>DiagramRequest</c>: cube decisions have no click-by-click
/// board state, so the answer chrome is free-standing and the consumer places
/// it wherever its layout wants (e.g. inline in a button row beside its own
/// submit button), rendering the position separately with the view-only
/// <see cref="BackgammonDiagram"/>. Routing by the record's kind
/// (<see cref="CubeDecision"/> or <see cref="CheckerPlayDecision"/>) stays
/// consumer-side; <see cref="BackgammonPlayEntry"/> still refuses a cube
/// decision's request at its contract boundary.
/// </para>
///
/// <para>
/// <b>Value contract: strictly controlled.</b> <see cref="Value"/> is the
/// selected answer or <c>null</c>, and the row renders from it and nothing
/// else. There is no component state, because a selection is a whole answer
/// and no partial answer exists to hold. Every selection fires
/// <see cref="ValueChanged"/> with the chosen answer; the component never
/// selects on its own, so a consumer that ignores the callback sees its
/// selection snap back to <see cref="Value"/> at the next render, its own
/// answer field remaining the single source of truth. A consumer clears the
/// row for the next problem by setting <see cref="Value"/> to <c>null</c>.
/// Changing <see cref="Decision"/> or <see cref="ShortLabels"/> relabels the
/// pills at the next render and does neither.
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
/// </para>
///
/// <para>
/// Restyling the input away with <c>display: none</c>, <c>visibility: hidden</c>
/// or a zeroed size would take it out of the tab order and the accessibility
/// tree. So would the sr-only <c>clip-path</c> recipe, less visibly: that clips
/// hit-testing as well as painting, leaving the control unreachable by pointer
/// even though it still reads correctly to a screen reader.
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
    /// A value outside the four <see cref="CubeAnswer"/> members renders
    /// nothing selected. That is a caller bug surfacing, not a fallback: the
    /// row does not remap an undefined answer onto a pill.
    /// </para>
    /// </summary>
    [Parameter]
    public CubeAnswer? Value { get; set; }

    /// <summary>
    /// Fires on every selection, carrying the chosen <see cref="CubeAnswer"/>.
    /// One radio is one whole answer, so the callback never carries
    /// <c>null</c> and there is no incomplete answer for it to fire on. It
    /// re-fires whenever the selection moves, so the consumer always holds the
    /// current answer. Pairs with <see cref="Value"/> for <c>@bind-Value</c>.
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
    public EventCallback<CubeAnswer?> ValueChanged { get; set; }

    /// <summary>
    /// The cube decision the row answers. Every pill is labelled at it, through
    /// <see cref="CubeLabels"/>, and the fourth answer has no label without it:
    /// which of its two labels applies is this decision's reading
    /// (<see cref="CubeDecision.ClaimOf"/>). The row reads nothing else from it.
    /// Passing another decision relabels the pills at the next render; it
    /// selects nothing and fires no <see cref="ValueChanged"/>.
    ///
    /// <para>
    /// Marked <see cref="EditorRequiredAttribute"/>, so a Razor consumer that
    /// omits it surfaces RZ2012. A missing decision cannot be labelled, so
    /// <c>null</c> is refused at the contract boundary with an
    /// <see cref="ArgumentNullException"/> whose <c>ParamName</c> is
    /// <c>Decision</c>, rather than rendering a row with no labels.
    /// </para>
    /// </summary>
    [Parameter, EditorRequired]
    public CubeDecision Decision { get; set; } = null!;

    /// <summary>
    /// Whether each pill shows its short label
    /// (<see cref="CubeLabels.ShortLabel(CubeAnswer, CubeDecision)"/>) instead
    /// of its full one. Defaults to <c>false</c>, the full form.
    ///
    /// <para>
    /// The host's call, not this component's: the labels abbreviate only when
    /// the row cannot fit them (SPEC-quiz-view §4), and only the host knows
    /// what else shares the row. In either form the full label stays the
    /// radio's accessible name (<c>aria-label</c>) and the pill's tooltip
    /// (<c>title</c>). Switching the form relabels the pills at the next
    /// render; it selects nothing and fires no <see cref="ValueChanged"/>.
    /// </para>
    /// </summary>
    [Parameter]
    public bool ShortLabels { get; set; }

    /// <summary>
    /// Catch-all for arbitrary HTML attributes (e.g. <c>style</c>, <c>id</c>,
    /// <c>class</c>) splatted onto the root <c>div</c> (<c>bg-cube-actions</c>).
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    // -----------------------------------------------------------------------
    //  The offered answers — every CubeAnswer member, in declaration order,
    //  which the type states is the order the four are offered in
    //  (SPEC-scoring §3's column order). The order is the type's; this is no
    //  option table of the row's own, and the wording of each answer is the
    //  label home's, asked per pill by the markup.
    // -----------------------------------------------------------------------

    private static readonly CubeAnswer[] _offeredAnswers = Enum.GetValues<CubeAnswer>();

    /// <summary>
    /// The visible caption of <paramref name="answer"/> at
    /// <see cref="Decision"/>: its short label in the short form, its full
    /// label otherwise. The full label is the markup's to place as the
    /// accessible name and tooltip in both forms.
    /// </summary>
    private string CaptionOf(CubeAnswer answer) =>
        ShortLabels
            ? CubeLabels.ShortLabel(answer, Decision)
            : CubeLabels.Label(answer, Decision);

    // -----------------------------------------------------------------------
    //  Instance-unique radio group name — browsers enforce radio mutual
    //  exclusion by name document-wide, so a hardcoded name would cross-link
    //  two instances rendered on the same page.
    // -----------------------------------------------------------------------

    private readonly string _groupName = $"bg-cube-actions-{Guid.NewGuid():N}";

    // -----------------------------------------------------------------------
    //  The contract boundary
    // -----------------------------------------------------------------------

    /// <summary>
    /// Refuses a missing <see cref="Decision"/>: no answer can be labelled
    /// without the decision it answers. No state is kept here; the row renders
    /// from its parameters alone.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// <see cref="Decision"/> is <c>null</c>.
    /// </exception>
    protected override void OnParametersSet()
    {
        if (Decision is null)
        {
            throw new ArgumentNullException(
                nameof(Decision),
                "BackgammonCubeActions labels each answer at the decision it answers: " +
                "pass the CubeDecision being answered.");
        }
    }

    // -----------------------------------------------------------------------
    //  Radio selection routing
    // -----------------------------------------------------------------------

    /// <summary>
    /// Emits the selected pill's <see cref="CubeAnswer"/>. No state is
    /// recorded here — the selection renders only once the consumer writes it
    /// back into <see cref="Value"/> (strictly controlled).
    /// </summary>
    private Task HandleSelected(CubeAnswer answer) =>
        ValueChanged.InvokeAsync(answer);
}
