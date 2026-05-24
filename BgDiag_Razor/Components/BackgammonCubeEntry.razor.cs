using Microsoft.AspNetCore.Components;
using BackgammonDiagram_Lib;
using BgDataTypes_Lib;

namespace BgDiag_Razor.Components;

/// <summary>
/// Stateful cube-decision entry. Wraps a view-only <see cref="BackgammonDiagram"/>
/// and presents the four <see cref="CubeVerdict"/> options as buttons. Selecting
/// a verdict invokes <see cref="OnCubeVerdictCompleted"/> with the chosen enum
/// value.
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
/// <b>Verdict surface</b>. The four <see cref="CubeVerdict"/> members
/// (<see cref="CubeVerdict.NoDouble"/>, <see cref="CubeVerdict.DoubleTake"/>,
/// <see cref="CubeVerdict.DoublePass"/>, <see cref="CubeVerdict.TooGood"/>)
/// each render as a button. The component is intentionally stateless beyond
/// the cached <see cref="Request"/> — there is no provisional "selected but
/// not yet committed" state, no internal "completed" lock, and no imperative
/// undo. Every button click fires <see cref="OnCubeVerdictCompleted"/>; the
/// consumer decides what to do on subsequent clicks (most quiz consumers will
/// navigate away on the first callback, making re-selection moot).
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
/// verdict is entered via the buttons, not via the diagram's hit-regions —
/// a single cube region cannot disambiguate four outcomes without a secondary
/// modal.
/// </para>
/// </summary>
public partial class BackgammonCubeEntry : ComponentBase
{
    // -----------------------------------------------------------------------
    //  Parameters
    // -----------------------------------------------------------------------

    /// <summary>
    /// The cube decision to render and accept a verdict against. Required
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
    /// Fires when the user selects a verdict. Carries the chosen
    /// <see cref="CubeVerdict"/>. Fires on every button click — there is no
    /// internal one-shot lock; if the consumer wants to ignore subsequent
    /// selections it can do so on its side (e.g. by advancing to the next
    /// problem on the first callback).
    /// </summary>
    [Parameter]
    public EventCallback<CubeVerdict> OnCubeVerdictCompleted { get; set; }

    /// <summary>
    /// Catch-all for arbitrary HTML attributes (e.g. <c>style</c>, <c>id</c>,
    /// <c>class</c>) splatted onto the outer wrapper <c>div</c>
    /// (<c>bg-cube-entry</c>). Does not forward to the inner diagram's
    /// splat surface.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    // -----------------------------------------------------------------------
    //  Internal table — single source for the (verdict, button label)
    //  mapping. Localized here because it is UI text owned by this
    //  component; if a second consumer ever needs the same labels, lift
    //  to a shared helper at that point.
    // -----------------------------------------------------------------------

    private static readonly (CubeVerdict Verdict, string Label)[] _verdicts =
    [
        (CubeVerdict.NoDouble,   "No double"),
        (CubeVerdict.DoubleTake, "Double / Take"),
        (CubeVerdict.DoublePass, "Double / Pass"),
        (CubeVerdict.TooGood,    "Too good"),
    ];

    // -----------------------------------------------------------------------
    //  Lifecycle
    // -----------------------------------------------------------------------

    protected override void OnParametersSet()
    {
        if (Request is null) return;

        if (!Request.Decision.IsCube)
        {
            throw new NotImplementedException(
                "Play (checker) decisions are not handled by BackgammonCubeEntry. " +
                "Route play decisions to BackgammonPlayEntry.");
        }
    }

    // -----------------------------------------------------------------------
    //  Verdict-button click routing
    // -----------------------------------------------------------------------

    private Task HandleVerdictSelected(CubeVerdict verdict) =>
        OnCubeVerdictCompleted.InvokeAsync(verdict);
}
