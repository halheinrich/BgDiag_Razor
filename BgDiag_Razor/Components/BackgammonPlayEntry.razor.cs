using Microsoft.AspNetCore.Components;
using BackgammonDiagram_Lib;
using BgDataTypes_Lib;
using BgMoveGen;

namespace BgDiag_Razor.Components;

/// <summary>
/// Stateful click-by-click play entry. Wraps a view-only <see cref="BackgammonDiagram"/>
/// and drives a <see cref="MoveEntryState"/> from its click events. Each completed
/// <see cref="Play"/> is reported via <see cref="OnPlayCompleted"/>.
///
/// <para>
/// <b>Click index conventions</b> (matching <see cref="MoveEntryState"/>'s contract and
/// the inner diagram's event surface):
/// <list type="bullet">
///   <item>1..24 — regular board points</item>
///   <item>25 — on-roll player's bar (legal source if a bar checker is present)</item>
///   <item>0 — bear-off tray (legal destination only)</item>
/// </list>
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
/// Route cube decisions to <see cref="BackgammonCubeEntry"/>, the sibling component
/// that owns the cube path.
/// </para>
///
/// <para>
/// <b>Pass positions</b> (no legal play exists) render the immovable position; clicks
/// are no-ops. The component does <i>not</i> auto-fire <see cref="OnPlayCompleted"/>
/// in this case — pass-position handling (skip-to-next-problem) is the consumer's
/// responsibility.
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
                "Route cube decisions to BackgammonCubeEntry.");
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

    private Task HandlePointClick(int point) => TryClick(point);
    private Task HandleBarClick(int bar) => TryClick(bar);
    private Task HandleTrayClick() => TryClick(0);

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

    private async Task TryClick(int clickIndex)
    {
        if (_state is null) return;

        var outcome = _state.TryAddClick(clickIndex);
        if (outcome == ClickOutcome.Illegal) return;

        RebuildRenderedRequest();

        if (outcome == ClickOutcome.PlayCompleted && _state.CompletedPlay is { } play)
        {
            await OnPlayCompleted.InvokeAsync(play);
        }
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
