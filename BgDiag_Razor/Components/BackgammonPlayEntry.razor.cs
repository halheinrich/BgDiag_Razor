using Microsoft.AspNetCore.Components;
using BackgammonDiagram_Lib;
using BgDataTypes_Lib;
using BgMoveGen;

namespace BgDiag_Razor.Components;

/// <summary>
/// Stateful one-click play entry for a checker-play decision. Wraps a view-only
/// <see cref="BackgammonDiagram"/> and drives a <see cref="MoveEntryState"/> from
/// its click events. Each completed <see cref="Play"/> is reported via
/// <see cref="OnPlayCompleted"/>.
///
/// <para>
/// <b>The decision and its board</b>: <see cref="Request"/> is the checker-play
/// decision's own request (<see cref="DiagramRequest.ForDecision"/>, with whatever
/// options the consumer draws it with). Entry starts from the record's board and
/// roll, and the board drawn during entry is that request's own mid-entry redraw,
/// <see cref="DiagramRequest.WithWorkingBoard"/> of the entry state's
/// <see cref="MoveEntryState.CurrentPosition"/> — never a decision record, a
/// session or display facts assembled here — so the names, score, cube and roll
/// shown are the decision's, derived by the diagram library from the record.
/// </para>
///
/// <para>
/// <b>One-click source-advance</b>: a single click on a checker's point (or the
/// bar) commits one move from that source via
/// <see cref="MoveEntryState.TryAdvanceFrom"/>, the model choosing which die to
/// consume by <see cref="DicePreference"/> (the rendered dice order, leftmost
/// first — see finding 2's display swap). A full play is entered by successive single
/// clicks. Click index conventions:
/// <list type="bullet">
///   <item>1..24 — regular board points (click a point holding an own checker)</item>
///   <item>25 — on-roll player's bar (click to enter, if a bar checker is present)</item>
/// </list>
/// Bearing off a single checker is an ordinary advance: clicking a home point
/// whose move lands on the tray commits the bear-off.
/// </para>
///
/// <para>
/// <b>One-click make-the-point</b>: clicking a <i>destination</i> point that holds
/// no own checker (an empty point or an opponent blot) lands two own checkers on it
/// via <see cref="MoveEntryState.TryMakePoint"/>, hitting a blot there
/// automatically. A point click therefore tries source-advance first and only falls
/// through to make when advance is illegal — the producer's own-occupied guard
/// makes that dispatch board-blind, so an own checker always advances (E1) and a
/// point with no own checker falls to make without the component inspecting the
/// board. A non-doubles make consumes both dice and completes the play; a doubles
/// make may leave dice for further clicks (the play stays in-progress). When the
/// point cannot be made, a single checker that can land there is moved instead. The
/// bar (a source, never a make destination) and the tray (bear-off-max) do
/// <i>not</i> chain to make.
/// </para>
///
/// <para>
/// <b>Tray click — bear-off-max shortcut</b>: clicking the tray bears off the
/// maximum number of checkers via <see cref="MoveEntryState.TryBearOffMax"/> when
/// that is unambiguous, completing the turn. It is a no-op when the max bear-off
/// is ambiguous (two ways tie), when no checker can bear off, or when the play is
/// already complete — there the user bears off via individual home-point clicks.
/// </para>
///
/// <para>
/// <b>State reset semantics</b>: a fresh <see cref="MoveEntryState"/> is constructed
/// only when the incoming decision's start — its board, a <see cref="BoardPosition"/>
/// compared by value, and its roll in rolled order — differs from the start the
/// current entry state was built from. Re-passing a request with the same start —
/// even a distinct request or record instance — preserves in-progress click state.
/// A different board or roll is treated as a new problem and resets.
/// </para>
///
/// <para>
/// <b>Cube decisions</b> are not entered here: a request that presents no checker
/// play — a cube decision's, a board's (<see cref="DiagramRequest.ForBoard"/>), or
/// an already-redrawn working board's — is refused with an
/// <see cref="ArgumentException"/> naming <see cref="Request"/>. A cube decision
/// has no click-by-click board state, so no entry wrapper exists for it: render
/// the position with the view-only <see cref="BackgammonDiagram"/> and enter the
/// answer with the free-standing <see cref="BackgammonCubeActions"/>. The refusal
/// is a run-time one because a <see cref="DiagramRequest"/> does not carry its
/// decision's kind in its type; consumers route by the record's kind
/// (<see cref="BgDecisionData.Match{TResult}"/>) before building the request.
/// </para>
///
/// <para>
/// <b>Pass positions</b> (no legal play exists) render the immovable position; clicks
/// are no-ops. The component does <i>not</i> auto-fire <see cref="OnPlayCompleted"/>
/// in this case — pass-position handling (skip-to-next-problem) is the consumer's
/// responsibility.
/// </para>
///
/// <para>
/// <b>Bounded-height contract</b>. The wrapper renders the inner diagram inside
/// an internal <c>.bg-board-slot</c> div. Give <c>.bg-play-entry</c> a definite
/// height (a real <c>height</c>, or shrinkable-flex-item sizing — never
/// <c>max-height</c> alone) and the board letterboxes to it, ratio preserved;
/// unbounded consumers get today's width-driven flow unchanged. The mechanism
/// lives in the scoped CSS (flex column, shrinkable slot) plus
/// <c>.bg-diagram</c>'s own contain-fit default.
/// </para>
/// </summary>
public partial class BackgammonPlayEntry : ComponentBase
{
    // -----------------------------------------------------------------------
    //  Parameters
    // -----------------------------------------------------------------------

    /// <summary>
    /// The checker-play decision to enter clicks against: its own request, built by
    /// <see cref="DiagramRequest.ForDecision"/> from a <see cref="CheckerPlayDecision"/>
    /// and varied with any options the consumer draws it with. Required (non-null to
    /// render anything). Entry starts from the record's board and roll; the board
    /// drawn is this request's <see cref="DiagramRequest.WithWorkingBoard"/>, so its
    /// options and the decision's presentation reach the inner diagram unchanged.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown while parameters are set when the request presents no checker-play
    /// decision (see the class summary).
    /// </exception>
    [Parameter, EditorRequired]
    public DiagramRequest? Request { get; set; }

    /// <summary>Rendering options forwarded to the inner diagram.</summary>
    [Parameter]
    public DiagramOptions Options { get; set; } = new();

    /// <summary>
    /// Fires once when a complete <see cref="Play"/> has been assembled from the
    /// click sequence. Does not fire for pass positions or for partial / illegal
    /// click sequences. The play is the entry state's
    /// <see cref="MoveEntryState.CompletedPlay"/>; how a play is compared with
    /// another is stated on <see cref="Play"/>, not here.
    /// </summary>
    [Parameter]
    public EventCallback<Play> OnPlayCompleted { get; set; }

    /// <summary>
    /// Fires when the user clicks the dice on a <i>complete</i> play, signalling
    /// "I want to submit". Parameterless by design: the consumer already holds the
    /// assembled <see cref="Play"/> from <see cref="OnPlayCompleted"/>, so this
    /// component stays submit-oblivious — it signals intent, it does not submit.
    /// <para>
    /// Marked <see cref="EditorRequiredAttribute"/> so a consumer that forgets to
    /// bind it fails at compile time (RZ2012) rather than silently dropping the
    /// submit affordance. Tradeoff: every use site must bind it.
    /// </para>
    /// </summary>
    [Parameter, EditorRequired]
    public EventCallback OnSubmitRequested { get; set; }

    /// <summary>
    /// Catch-all for arbitrary HTML attributes (e.g. <c>style</c>, <c>id</c>,
    /// <c>class</c>) splatted onto the outer wrapper <c>div</c>.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    // -----------------------------------------------------------------------
    //  Internal state
    // -----------------------------------------------------------------------

    /// <summary>The checker-play decision <see cref="Request"/> presents; null exactly when it is.</summary>
    private CheckerPlayDecision? _decision;

    /// <summary>The entry in progress; null exactly when <see cref="Request"/> is.</summary>
    private MoveEntryState? _state;

    /// <summary>
    /// The board <see cref="_state"/> was started from — half of the reset key
    /// (<see cref="IsSameStart"/>); the other half, the roll, is the entry
    /// state's own <see cref="MoveEntryState.Die1"/> and <see cref="MoveEntryState.Die2"/>.
    /// Set and cleared with <see cref="_state"/>.
    /// </summary>
    private BoardPosition? _start;

    /// <summary>The request handed to the inner diagram: <see cref="Request"/>'s working board.</summary>
    private DiagramRequest? _renderedRequest;

    /// <summary>
    /// Display-only dice order: <see cref="DiceOrder.Reversed"/> after an odd number
    /// of swaps. Purely a rendering tweak — it never touches the incoming
    /// <see cref="Request"/> or <see cref="MoveEntryState"/>, so
    /// <see cref="IsSameStart"/> stays stable and in-progress entry survives. Reset
    /// on every new problem so swap state never leaks across problems.
    /// </summary>
    private DiceOrder _diceOrder;

    // -----------------------------------------------------------------------
    //  Lifecycle
    // -----------------------------------------------------------------------

    /// <summary>
    /// Single render/reset hook. Clears all state on a null <see cref="Request"/>,
    /// refuses a request presenting no checker play at the contract boundary (see
    /// the class summary), and otherwise resets or preserves the
    /// <see cref="MoveEntryState"/> by the decision's start
    /// (<see cref="IsSameStart"/>) before redrawing the working board.
    /// </summary>
    protected override void OnParametersSet()
    {
        if (Request is null)
        {
            _decision = null;
            _state = null;
            _start = null;
            _renderedRequest = null;
            _diceOrder = DiceOrder.AsRolled;
            return;
        }

        if (Request.Decision is not CheckerPlayDecision decision)
        {
            throw new ArgumentException(
                "BackgammonPlayEntry enters a checker play: its Request is a checker-play " +
                "decision's own request (DiagramRequest.ForDecision). For a cube decision, " +
                "render the position with BackgammonDiagram and enter the answer with " +
                "BackgammonCubeActions; draw any other board with BackgammonDiagram.",
                nameof(Request));
        }

        _decision = decision;
        if (!IsSameStart(decision))
        {
            var roll = decision.Decision.Dice;
            _state = new MoveEntryState(decision.Board, roll[0], roll[1]);
            _start = decision.Board;
            _diceOrder = DiceOrder.AsRolled;  // fresh problem — swap state must not leak across problems
        }

        RedrawWorkingBoard();
    }

    /// <summary>
    /// Whether <paramref name="decision"/> starts where the current entry state
    /// did: the same board, by <see cref="BoardPosition"/>'s value equality, and
    /// the same roll in rolled order. Record and request identity play no part.
    /// </summary>
    private bool IsSameStart(CheckerPlayDecision decision) =>
        _state is not null
        && _start == decision.Board
        && _state.Die1 == decision.Decision.Dice[0]
        && _state.Die2 == decision.Decision.Dice[1];

    /// <summary>
    /// Rebuilds the inner diagram's request as <see cref="Request"/>'s own redraw of
    /// the entry state's current position, in the displayed dice order.
    /// </summary>
    private void RedrawWorkingBoard() =>
        _renderedRequest = Request!.WithWorkingBoard(_state!.CurrentPosition, _diceOrder);

    // -----------------------------------------------------------------------
    //  Click routing
    // -----------------------------------------------------------------------

    // One-click dispatch. A point click is advance-then-make (PointClick): an own
    // checker advances from that source; a point with no own checker falls through
    // to make-the-point. A bar click is advance-only — the bar is a source, never a
    // make destination — so it does not chain to make. Bearing off a single checker
    // is just a source-advance on its home point (whose move lands on the tray,
    // ToPt == 0); a tray click is the bear-off-MAX shortcut (see HandleTrayClick).
    // The model picks which die to consume by DicePreference().
    private Task HandlePointClick(int point) => PointClick(point);
    private Task HandleBarClick(int bar) => Advance(bar);
    private Task HandleTrayClick() => BearOffMax();

    /// <summary>
    /// The rendered dice order, leftmost die first — the roll as the entry state
    /// holds it (in rolled order), in <see cref="_diceOrder"/>, the order the
    /// working board draws it in. This is the only place "leftmost die" is known;
    /// the model stays die-order-agnostic and one-click advance prefers whichever
    /// die the user currently sees on the left.
    /// </summary>
    private IReadOnlyList<int> DicePreference(MoveEntryState state) =>
        _diceOrder == DiceOrder.Reversed ? [state.Die2, state.Die1] : [state.Die1, state.Die2];

    /// <summary>
    /// Point click — advance-then-make. An own checker on <paramref name="point"/>
    /// advances from it (E1); if that is illegal the point holds no own checker, so
    /// the click falls through to make-the-point on it (E2). The advance-first
    /// ordering plus the producer's own-occupied guard keep the dispatch board-blind
    /// — the component never inspects <see cref="MoveEntryState.CurrentPosition"/>.
    /// Illegal from both is a no-op.
    /// </summary>
    private async Task PointClick(int point)
    {
        if (_state is null) return;

        var outcome = _state.TryAdvanceFrom(point, DicePreference(_state));
        if (outcome == ClickOutcome.Illegal)
            outcome = _state.TryMakePoint(point);

        await ApplyOutcome(outcome);
    }

    /// <summary>Source-advance only (used by the bar, which is never a make destination).</summary>
    private async Task Advance(int point)
    {
        if (_state is null) return;
        await ApplyOutcome(_state.TryAdvanceFrom(point, DicePreference(_state)));
    }

    /// <summary>
    /// Tray click — the bear-off-MAX shortcut. Bears off the maximum number of
    /// checkers when that is unambiguous, completing the turn. A no-op (no state
    /// change, no completion) when the max bear-off is ambiguous (two ways tie),
    /// when no checker can bear off, or when the play is already complete — in
    /// those cases the user bears off via individual home-point clicks instead.
    /// <see cref="MoveEntryState.TryBearOffMax"/> always completes the turn when it
    /// commits, so a <see cref="ClickOutcome.MoveCommitted"/> never arises here.
    /// </summary>
    private async Task BearOffMax()
    {
        if (_state is null) return;
        await ApplyOutcome(_state.TryBearOffMax());
    }

    /// <summary>
    /// Shared tail for every one-click action: a no-op on
    /// <see cref="ClickOutcome.Illegal"/>; otherwise redraws the working board and,
    /// only on <see cref="ClickOutcome.PlayCompleted"/>, fires <see cref="OnPlayCompleted"/>.
    /// A <see cref="ClickOutcome.MoveCommitted"/> (including a doubles make that
    /// leaves dice) just redraws and waits for the next click.
    /// </summary>
    private async Task ApplyOutcome(ClickOutcome outcome)
    {
        if (outcome == ClickOutcome.Illegal) return;

        RedrawWorkingBoard();

        if (outcome == ClickOutcome.PlayCompleted && _state!.CompletedPlay is { } play)
        {
            await OnPlayCompleted.InvokeAsync(play);
        }
    }

    /// <summary>
    /// Dice click. On a complete play it signals submit intent via
    /// <see cref="OnSubmitRequested"/> (this component never submits itself). On an
    /// incomplete play it toggles the display-only dice order and redraws —
    /// except for doubles, where reversing equal dice changes nothing, so it's a
    /// no-op (no pointless render).
    /// </summary>
    private async Task HandleDiceClick()
    {
        if (_state is null) return;

        if (_state.IsComplete)
        {
            await OnSubmitRequested.InvokeAsync();
            return;
        }

        // Incomplete: swap is a visual no-op for doubles.
        if (_decision!.Dice.IsDouble) return;

        _diceOrder = _diceOrder == DiceOrder.AsRolled ? DiceOrder.Reversed : DiceOrder.AsRolled;
        RedrawWorkingBoard();
        StateHasChanged();
    }

    // -----------------------------------------------------------------------
    //  Imperative control
    // -----------------------------------------------------------------------

    /// <summary>
    /// Roll back the most recent change. If a source is selected with no move
    /// pending, clears the selection. Otherwise undoes the last committed move.
    /// No-op if neither holds.
    /// </summary>
    public void UndoLast()
    {
        if (_state is null) return;
        _state.UndoLast();
        RedrawWorkingBoard();
        StateHasChanged();
    }

    /// <summary>
    /// Restore the initial position. Clears any source selection and any committed
    /// moves. Allowed even after the play has completed; the consumer can choose
    /// to expose this as a "redo from start" affordance.
    /// </summary>
    public void UndoAll()
    {
        if (_state is null) return;
        _state.UndoAll();
        RedrawWorkingBoard();
        StateHasChanged();
    }
}
