using Microsoft.AspNetCore.Components;
using BackgammonDiagram_Lib;
using BgDataTypes_Lib;

namespace BgDiag_Razor.Components;

/// <summary>
/// Stateful cube-decision entry. Wraps a view-only <see cref="BackgammonDiagram"/>
/// and presents the cube decision as two independent atomic button groups — the
/// doubler's <see cref="CubeAction.NoDouble"/> / <see cref="CubeAction.Double"/>
/// choice and the taker's <see cref="CubeAction.Take"/> / <see cref="CubeAction.Pass"/>
/// choice. Once both halves are selected, the component emits the raw answer as a
/// <see cref="CubeDecisionPair"/> via <see cref="OnCubeDecisionCompleted"/>; scoring
/// the pair against the correct cube action is the (future) quiz layer's job, not
/// this component's.
///
/// <para>
/// <b>Companion to</b> <see cref="BackgammonPlayEntry"/>. Where
/// <c>BackgammonPlayEntry</c> handles play (checker-move) decisions
/// (<c>Decision.IsCube == false</c>), <c>BackgammonCubeEntry</c> handles cube
/// decisions (<c>Decision.IsCube == true</c>). Consumers route by
/// <see cref="Decision.IsCube"/>; each component throws
/// <see cref="NotImplementedException"/> when handed the other half's decision
/// type — failing loudly at the contract boundary rather than silently
/// rendering an unusable widget.
/// </para>
///
/// <para>
/// <b>Two atomic groups</b>. The cube decision is entered as two independent
/// choices, each a two-button group:
/// <list type="bullet">
///   <item>Doubler — "No Double" (<see cref="CubeAction.NoDouble"/>) /
///   "Double" (<see cref="CubeAction.Double"/>)</item>
///   <item>Taker — "Take" (<see cref="CubeAction.Take"/>) /
///   "Pass" (<see cref="CubeAction.Pass"/>)</item>
/// </list>
/// Both halves are entered before any solution is shown; the component does not
/// know or encode which combination is correct. Because the doubler group can
/// only yield a doubler-half action and the taker group a taker-half action, the
/// <see cref="CubeDecisionPair"/> constructed here always satisfies that type's
/// half-guards — construction never throws in this component.
/// </para>
///
/// <para>
/// <b>Provisional state and re-fire semantics</b>. Each click records the chosen
/// action for its group (<see cref="_doubler"/> / <see cref="_taker"/>) and marks
/// that button selected. There is no lock — re-selecting within a group is
/// allowed. Whenever <i>both</i> halves are set after a click,
/// <see cref="OnCubeDecisionCompleted"/> fires with the current
/// <see cref="CubeDecisionPair"/>, so the consumer always holds the latest
/// complete answer; changing a selection after completion re-fires with the
/// updated pair. Provisional state resets when <see cref="Request"/> advances to
/// a new cube position (see reset semantics below).
/// </para>
///
/// <para>
/// <b>Position rendering</b>. Cube decisions do not move checkers, so the
/// inner diagram receives <see cref="Request"/> unchanged. No
/// <c>MoveEntryState</c>, no Mop rebuilding, no <c>BgMoveGen</c> dependency.
/// </para>
///
/// <para>
/// <b>Inner diagram's cube hit region</b> still renders (because
/// <see cref="BackgammonDiagram"/> always wires it) but is not subscribed by
/// this component; cube-area clicks on the diagram are no-ops by design. The
/// decision is entered via the two button groups, not via the diagram's
/// hit-regions.
/// </para>
/// </summary>
public partial class BackgammonCubeEntry : ComponentBase
{
    // -----------------------------------------------------------------------
    //  Parameters
    // -----------------------------------------------------------------------

    /// <summary>
    /// The cube decision to render and accept an answer against. Required
    /// (non-null to render anything). The position and match state flow
    /// through to the inner diagram unchanged. Play decisions
    /// (<c>Decision.IsCube == false</c>) throw <see cref="NotImplementedException"/>;
    /// route those to <see cref="BackgammonPlayEntry"/>.
    /// </summary>
    [Parameter, EditorRequired]
    public DiagramRequest? Request { get; set; }

    /// <summary>Rendering options forwarded to the inner diagram.</summary>
    [Parameter]
    public DiagramOptions Options { get; set; } = new();

    /// <summary>
    /// Fires when both halves of the cube decision have been selected, carrying
    /// the user's answer as a <see cref="CubeDecisionPair"/>. Re-fires whenever a
    /// selection changes after completion, so the consumer always holds the
    /// current complete pair. Does not fire while only one half is selected.
    ///
    /// <para>
    /// Marked <see cref="EditorRequiredAttribute"/>: a consumer that omits this
    /// binding gets nothing useful from the component, and an out-of-date
    /// attribute name on a Razor consumer would otherwise splat silently
    /// (RZ2012 surfaces the missing binding at compile time). This deliberately
    /// adopts the stricter practice; the sibling
    /// <see cref="BackgammonPlayEntry.OnPlayCompleted"/> does not yet carry it.
    /// </para>
    /// </summary>
    [Parameter, EditorRequired]
    public EventCallback<CubeDecisionPair> OnCubeDecisionCompleted { get; set; }

    /// <summary>
    /// Catch-all for arbitrary HTML attributes (e.g. <c>style</c>, <c>id</c>,
    /// <c>class</c>) splatted onto the outer wrapper <c>div</c>
    /// (<c>bg-cube-entry</c>). Does not forward to the inner diagram's
    /// splat surface.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    // -----------------------------------------------------------------------
    //  Internal tables — single source for each group's (action, label)
    //  mapping. Localized here because it is UI text owned by this component;
    //  if a second consumer ever needs the same labels, lift to a shared
    //  helper at that point.
    // -----------------------------------------------------------------------

    private static readonly (CubeAction Action, string Label)[] _doublerActions =
    [
        (CubeAction.NoDouble, "No Double"),
        (CubeAction.Double,   "Double"),
    ];

    private static readonly (CubeAction Action, string Label)[] _takerActions =
    [
        (CubeAction.Take, "Take"),
        (CubeAction.Pass, "Pass"),
    ];

    // -----------------------------------------------------------------------
    //  Provisional state
    // -----------------------------------------------------------------------

    private CubeAction? _doubler;
    private CubeAction? _taker;
    private int[]? _cachedMop;

    // -----------------------------------------------------------------------
    //  Lifecycle
    // -----------------------------------------------------------------------

    protected override void OnParametersSet()
    {
        if (Request is null)
        {
            _doubler = null;
            _taker = null;
            _cachedMop = null;
            return;
        }

        if (!Request.Decision.IsCube)
        {
            throw new NotImplementedException(
                "Play (checker) decisions are not handled by BackgammonCubeEntry. " +
                "Route play decisions to BackgammonPlayEntry.");
        }

        // Cube decisions carry no dice ([0, 0] by the data-layer invariant), so
        // the starting position alone identifies the problem. Mirrors
        // BackgammonPlayEntry's (Mop, Dice) reset key, with Dice dropped.
        var mop = Request.Position.Mop;
        if (!IsSameProblem(mop))
        {
            _cachedMop = [.. mop];
            _doubler = null;
            _taker = null;
        }
    }

    private bool IsSameProblem(IReadOnlyList<int> mop)
    {
        if (_cachedMop is null) return false;
        if (_cachedMop.Length != mop.Count) return false;
        for (int i = 0; i < mop.Count; i++)
            if (_cachedMop[i] != mop[i]) return false;
        return true;
    }

    // -----------------------------------------------------------------------
    //  Button click routing
    // -----------------------------------------------------------------------

    private Task HandleDoublerSelected(CubeAction action)
    {
        _doubler = action;
        return FireIfComplete();
    }

    private Task HandleTakerSelected(CubeAction action)
    {
        _taker = action;
        return FireIfComplete();
    }

    private Task FireIfComplete() =>
        _doubler is { } doubler && _taker is { } taker
            ? OnCubeDecisionCompleted.InvokeAsync(new CubeDecisionPair(doubler, taker))
            : Task.CompletedTask;
}
