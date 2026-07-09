using Microsoft.AspNetCore.Components;
using BackgammonDiagram_Lib;
using BgDataTypes_Lib;
using BgMoveGen;

namespace BgDiag_Razor.Components;

/// <summary>
/// Stateful one-click play entry. Wraps a view-only <see cref="BackgammonDiagram"/>
/// and drives a <see cref="MoveEntryState"/> from its click events. Each completed
/// <see cref="Play"/> is reported via <see cref="OnPlayCompleted"/>.
///
/// <para>
/// <b>One-click source-advance</b>: a single click on a checker's point (or the
/// bar) commits one move from that source via
/// <see cref="MoveEntryState.TryAdvanceFrom"/>, the model choosing which die to
/// consume by <see cref="DicePreference"/> (the rendered dice order, leftmost
/// first — see #2's display swap). A full play is entered by successive single
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
/// only when the incoming <see cref="Request"/>'s starting position
/// (<c>Position.Mop</c>) or dice (<c>Decision.Dice</c>) differ value-wise from the
/// previously cached pair. Re-passing a request with the same Mop and Dice — even a
/// distinct object reference — preserves in-progress click state. Different Mop or
/// Dice is treated as a new problem and resets.
/// </para>
///
/// <para>
/// <b>Cube decisions</b> (signalled by <c>Decision.IsCube == true</c>; in the data
/// layer this also coincides with <c>Dice == [0, 0]</c>) are not supported by this
/// component; constructing one throws <see cref="NotImplementedException"/>.
/// Cube decisions have no click-by-click board state, so no entry wrapper exists
/// for them: render the position with the view-only <see cref="BackgammonDiagram"/>
/// and enter the answer with the free-standing <see cref="BackgammonCubeActions"/>.
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
    /// The decision to enter clicks against. Required (non-null to render anything).
    /// The starting position and dice are read from <c>Position.Mop</c> and
    /// <c>Decision.Dice</c>; other fields (names, cube state, orientation) flow
    /// through to the inner diagram unchanged.
    /// </summary>
    [Parameter, EditorRequired]
    public DiagramRequest? Request { get; set; }

    /// <summary>Rendering options forwarded to the inner diagram.</summary>
    [Parameter]
    public DiagramOptions Options { get; set; } = new();

    /// <summary>
    /// Content forwarded, unchanged, to the inner <see cref="BackgammonDiagram"/>'s
    /// own <see cref="BackgammonDiagram.Overlay"/> — see that parameter for the
    /// positioning contract. This component adds no wrapper of its own around it;
    /// the overlay lands directly on the inner diagram's board box.
    /// </summary>
    [Parameter]
    public RenderFragment? Overlay { get; set; }

    /// <summary>
    /// Fires once when a complete <see cref="Play"/> has been assembled from the
    /// click sequence. Does not fire for pass positions or for partial / illegal
    /// click sequences.
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

    private MoveEntryState? _state;
    private DiagramRequest? _renderedRequest;
    private int[]? _cachedMop;
    private int[]? _cachedDice;

    /// <summary>
    /// Display-only flag: when set, the rendered request shows the dice in
    /// reversed order. Purely a rendering tweak — it never touches the incoming
    /// <see cref="Request"/> or <see cref="MoveEntryState"/>, so
    /// <see cref="IsSameProblem"/> stays stable and in-progress entry survives.
    /// Reset on every new problem so swap state never leaks across problems.
    /// </summary>
    private bool _diceSwapped;

    // -----------------------------------------------------------------------
    //  Lifecycle
    // -----------------------------------------------------------------------

    protected override void OnParametersSet()
    {
        if (Request is null)
        {
            _state = null;
            _renderedRequest = null;
            _cachedMop = null;
            _cachedDice = null;
            _diceSwapped = false;
            return;
        }

        if (Request.Decision.IsCube)
        {
            throw new NotImplementedException(
                "Cube decisions are not handled by BackgammonPlayEntry. " +
                "Render the position with BackgammonDiagram and enter the answer " +
                "with BackgammonCubeActions.");
        }

        var mop = Request.Position.Mop;
        var dice = Request.Decision.Dice;

        if (!IsSameProblem(mop, dice))
        {
            _cachedMop = [.. mop];
            _cachedDice = [.. dice];
            _state = new MoveEntryState(BoardState.FromMop(mop), dice[0], dice[1]);
            _diceSwapped = false;  // fresh problem — swap state must not leak across problems
        }

        RebuildRenderedRequest();
    }

    private bool IsSameProblem(IReadOnlyList<int> mop, IReadOnlyList<int> dice)
    {
        if (_cachedMop is null || _cachedDice is null) return false;
        if (_cachedMop.Length != mop.Count || _cachedDice.Length != dice.Count) return false;
        for (int i = 0; i < mop.Count; i++)
            if (_cachedMop[i] != mop[i]) return false;
        for (int i = 0; i < dice.Count; i++)
            if (_cachedDice[i] != dice[i]) return false;
        return true;
    }

    private void RebuildRenderedRequest()
    {
        if (Request is null || _state is null)
        {
            _renderedRequest = null;
            return;
        }
        var b = DiagramRequest.Builder.From(Request);
        b.Mop = [.. _state.Current.ToMop()];
        if (_diceSwapped)
            b.Dice = [b.Dice[1], b.Dice[0]];  // display-only reorder; Request/state untouched
        _renderedRequest = b.Build();
    }

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
    /// The rendered dice order, leftmost die first — reflecting the #2 display
    /// swap. This is the only place "leftmost die" is known; the model stays
    /// die-order-agnostic and one-click advance prefers whichever die the user
    /// currently sees on the left.
    /// </summary>
    private IReadOnlyList<int> DicePreference()
    {
        var dice = Request!.Decision.Dice;
        return _diceSwapped ? [dice[1], dice[0]] : [dice[0], dice[1]];
    }

    /// <summary>
    /// Point click — advance-then-make. An own checker on <paramref name="point"/>
    /// advances from it (E1); if that is illegal the point holds no own checker, so
    /// the click falls through to make-the-point on it (E2). The advance-first
    /// ordering plus the producer's own-occupied guard keep the dispatch board-blind
    /// — the component never inspects <c>_state.Current</c>. Illegal from both is a
    /// no-op.
    /// </summary>
    private async Task PointClick(int point)
    {
        if (_state is null) return;

        var outcome = _state.TryAdvanceFrom(point, DicePreference());
        if (outcome == ClickOutcome.Illegal)
            outcome = _state.TryMakePoint(point);

        await ApplyOutcome(outcome);
    }

    /// <summary>Source-advance only (used by the bar, which is never a make destination).</summary>
    private async Task Advance(int point)
    {
        if (_state is null) return;
        await ApplyOutcome(_state.TryAdvanceFrom(point, DicePreference()));
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
    /// <see cref="ClickOutcome.Illegal"/>; otherwise re-renders the board and, only
    /// on <see cref="ClickOutcome.PlayCompleted"/>, fires <see cref="OnPlayCompleted"/>.
    /// A <see cref="ClickOutcome.MoveCommitted"/> (including a doubles make that
    /// leaves dice) just re-renders and waits for the next click.
    /// </summary>
    private async Task ApplyOutcome(ClickOutcome outcome)
    {
        if (outcome == ClickOutcome.Illegal) return;

        RebuildRenderedRequest();

        if (outcome == ClickOutcome.PlayCompleted && _state!.CompletedPlay is { } play)
        {
            await OnPlayCompleted.InvokeAsync(play);
        }
    }

    /// <summary>
    /// Dice click. On a complete play it signals submit intent via
    /// <see cref="OnSubmitRequested"/> (this component never submits itself). On an
    /// incomplete play it toggles the display-only dice swap and re-renders —
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
        if (_cachedDice is { Length: 2 } d && d[0] == d[1]) return;

        _diceSwapped = !_diceSwapped;
        RebuildRenderedRequest();
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
        RebuildRenderedRequest();
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
        RebuildRenderedRequest();
        StateHasChanged();
    }
}
