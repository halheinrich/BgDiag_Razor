using Microsoft.AspNetCore.Components;
using BgDataTypes_Lib;

namespace BgDiag_Razor.Components;

/// <summary>
/// Free-standing cube-decision answer row: four mutually-exclusive radio pills,
/// each mapping to one of the four possible <see cref="CubeDecisionPair"/> values
/// (the doubler's <see cref="CubeAction.NoDouble"/> / <see cref="CubeAction.Double"/>
/// choice crossed with the taker's <see cref="CubeAction.Take"/> /
/// <see cref="CubeAction.Pass"/> choice). Selecting a radio sets both halves
/// atomically and emits the answer via <see cref="ValueChanged"/>; scoring the pair
/// against the correct cube action is the consumer's (quiz layer's) job, not this
/// component's.
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
/// <b>Four radio options</b>. The row is a single radio group whose four options
/// are a bijection onto the four <see cref="CubeDecisionPair"/> values:
/// <list type="bullet">
///   <item>"No double" — (<see cref="CubeAction.NoDouble"/>,
///   <see cref="CubeAction.Take"/>)</item>
///   <item>"Double/Take" — (<see cref="CubeAction.Double"/>,
///   <see cref="CubeAction.Take"/>)</item>
///   <item>"Double/Pass" — (<see cref="CubeAction.Double"/>,
///   <see cref="CubeAction.Pass"/>)</item>
///   <item>"Too good" — (<see cref="CubeAction.NoDouble"/>,
///   <see cref="CubeAction.Pass"/>): don't double, but the opponent would pass.</item>
/// </list>
/// The two no-double options use their standard backgammon names ("No double",
/// "Too good") and omit the redundant taker half — a taker action is only reached
/// when the doubler doubles. Each option pairs a doubler-half action with a
/// taker-half action, so the <see cref="CubeDecisionPair"/> constructed here always
/// satisfies that type's half-guards — construction never throws in this component.
/// </para>
///
/// <para>
/// <b>Strictly controlled value contract.</b> The selected pill is whatever
/// <see cref="Value"/> says — the component holds no selection state of its own.
/// Selecting a radio invokes <see cref="ValueChanged"/> with that option's pair
/// (never <c>null</c>: radios cannot deselect); the selection only sticks once the
/// consumer writes the new value back into <see cref="Value"/>, which
/// <c>@bind-Value</c> does automatically. Because one radio completes the pair,
/// <see cref="ValueChanged"/> fires on the first selection and re-fires whenever
/// the user switches to a different option, so the consumer always holds the
/// latest complete answer — there is no half-selected state and no one-shot lock.
/// Clearing between problems is likewise the consumer's move: set
/// <see cref="Value"/> to <c>null</c> when advancing (there is no request here to
/// key an automatic reset off).
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
/// <b>Instance-unique radio group.</b> Each instance generates its own
/// <c>name</c> for its four radios, so two rows on one page never cross-link
/// their browser-native mutual exclusion. The name is internal — consumers
/// interact only through <see cref="Value"/> / <see cref="ValueChanged"/>.
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
    /// </summary>
    [Parameter]
    public CubeDecisionPair? Value { get; set; }

    /// <summary>
    /// Fires on each radio selection, carrying the chosen answer (one radio sets
    /// both halves atomically — never fires with <c>null</c>, and never while
    /// "incomplete": there is no half-selected state). Re-fires whenever the
    /// selection changes, so the consumer always holds the current complete pair.
    /// Pairs with <see cref="Value"/> for <c>@bind-Value</c>.
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
    public EventCallback<CubeDecisionPair?> ValueChanged { get; set; }

    /// <summary>
    /// Catch-all for arbitrary HTML attributes (e.g. <c>style</c>, <c>id</c>,
    /// <c>class</c>) splatted onto the root <c>div</c> (<c>bg-cube-actions</c>).
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
        ("No double",   new CubeDecisionPair(CubeAction.NoDouble, CubeAction.Take)),
        ("Double/Take", new CubeDecisionPair(CubeAction.Double,   CubeAction.Take)),
        ("Double/Pass", new CubeDecisionPair(CubeAction.Double,   CubeAction.Pass)),
        ("Too good",    new CubeDecisionPair(CubeAction.NoDouble, CubeAction.Pass)),
    ];

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
    /// Emits the selected option's <see cref="CubeDecisionPair"/>. No state is
    /// recorded here — the selection renders only once the consumer writes it
    /// back into <see cref="Value"/> (strictly controlled).
    /// </summary>
    private Task HandleSelected(CubeDecisionPair pair) =>
        ValueChanged.InvokeAsync(pair);
}
