using Microsoft.AspNetCore.Components;
using BackgammonDiagram_Lib;
using BgDataTypes_Lib;

namespace BgDiag_Razor.Components;

/// <summary>
/// Stateful cube-decision entry. Wraps a view-only <see cref="BackgammonDiagram"/>
/// and presents the cube decision as four mutually-exclusive radio buttons, each
/// mapping to one of the four possible <see cref="CubeDecisionPair"/> values (the
/// doubler's <see cref="CubeAction.NoDouble"/> / <see cref="CubeAction.Double"/>
/// choice crossed with the taker's <see cref="CubeAction.Take"/> /
/// <see cref="CubeAction.Pass"/> choice). Selecting a radio sets both halves
/// atomically and emits the raw answer as a <see cref="CubeDecisionPair"/> via
/// <see cref="OnCubeDecisionCompleted"/>; scoring the pair against the correct cube
/// action is the (future) quiz layer's job, not this component's.
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
/// <b>Four radio options</b>. The cube decision is entered as a single
/// radio group whose four options are a bijection onto the four
/// <see cref="CubeDecisionPair"/> values:
/// <list type="bullet">
///   <item>"No double / Take" — (<see cref="CubeAction.NoDouble"/>,
///   <see cref="CubeAction.Take"/>)</item>
///   <item>"Double / Take" — (<see cref="CubeAction.Double"/>,
///   <see cref="CubeAction.Take"/>)</item>
///   <item>"Double / Pass" — (<see cref="CubeAction.Double"/>,
///   <see cref="CubeAction.Pass"/>)</item>
///   <item>"Too good to double" — (<see cref="CubeAction.NoDouble"/>,
///   <see cref="CubeAction.Pass"/>): don't double, but the opponent would pass,
///   so the taker half is <see cref="CubeAction.Pass"/>, not
///   <see cref="CubeAction.Take"/>.</item>
/// </list>
/// The answer is entered before any solution is shown; the component does not know
/// or encode which option is correct. Each option pairs a doubler-half action with
/// a taker-half action, so the <see cref="CubeDecisionPair"/> constructed here
/// always satisfies that type's half-guards — construction never throws in this
/// component.
/// </para>
///
/// <para>
/// <b>Provisional state and re-fire semantics</b>. Each selection records the
/// chosen <see cref="CubeDecisionPair"/> (<see cref="_selection"/>) and marks that
/// radio selected; selecting another replaces it, so exactly one is selected at a
/// time. Because one radio completes the pair,
/// <see cref="OnCubeDecisionCompleted"/> fires on the first selection with the
/// matching <see cref="CubeDecisionPair"/> and re-fires whenever the user switches
/// to a different option, so the consumer always holds the latest complete answer.
/// The "does not fire while incomplete" contract is trivially preserved — there is
/// no half-selected state. Provisional state resets when <see cref="Request"/>
/// advances to a new cube position (see reset semantics below).
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
/// decision is entered via the radio group, not via the diagram's hit-regions.
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
    //  Internal table — single source for the four radio options, each a
    //  (label, CubeDecisionPair) mapping. Localized here because it is UI text
    //  owned by this component; if a second consumer ever needs the same labels,
    //  lift to a shared helper at that point.
    // -----------------------------------------------------------------------

    private static readonly (string Label, CubeDecisionPair Pair)[] _cubeOptions =
    [
        ("No double / Take",   new CubeDecisionPair(CubeAction.NoDouble, CubeAction.Take)),
        ("Double / Take",      new CubeDecisionPair(CubeAction.Double,   CubeAction.Take)),
        ("Double / Pass",      new CubeDecisionPair(CubeAction.Double,   CubeAction.Pass)),
        ("Too good to double", new CubeDecisionPair(CubeAction.NoDouble, CubeAction.Pass)),
    ];

    // -----------------------------------------------------------------------
    //  Provisional state — the single selected pair, or null when nothing is
    //  chosen. A CubeDecisionPair is a validated value type, so equality against
    //  a table entry's pair drives the selected-state marker directly.
    // -----------------------------------------------------------------------

    private CubeDecisionPair? _selection;
    private int[]? _cachedMop;

    // -----------------------------------------------------------------------
    //  Lifecycle
    // -----------------------------------------------------------------------

    protected override void OnParametersSet()
    {
        if (Request is null)
        {
            _selection = null;
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
            _selection = null;
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
    //  Radio selection routing
    // -----------------------------------------------------------------------

    /// <summary>
    /// Records the selected option's <see cref="CubeDecisionPair"/> and emits it.
    /// Because each radio completes the pair, this fires on every selection (the
    /// first click and each subsequent switch); there is no half-selected state to
    /// gate on.
    /// </summary>
    private Task HandleCubeOptionSelected(CubeDecisionPair pair)
    {
        _selection = pair;
        return OnCubeDecisionCompleted.InvokeAsync(pair);
    }
}
