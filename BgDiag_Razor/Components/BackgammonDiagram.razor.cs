using Microsoft.AspNetCore.Components;
using BackgammonDiagram_Lib;
using BackgammonDiagram_Lib.Rendering;

namespace BgDiag_Razor.Components;

/// <summary>
/// View-only backgammon board primitive: given a <see cref="DiagramRequest"/> it
/// renders the position and surfaces click events, holding no
/// position-manipulation state of its own. It is the thin Blazor binding over
/// <c>BackgammonDiagram_Lib</c> — all SVG generation and hit-region geometry live
/// in the core lib (which stays Blazor-free); this component injects the lib's SVG
/// and lays a transparent, pixel-aligned click overlay on top.
///
/// <para>
/// <b>Render pipeline.</b> <see cref="OnParametersSet"/> is the single render hook:
/// a null <see cref="Request"/> clears the cached outputs and renders nothing;
/// otherwise it caches <c>DiagramRenderer.RenderSvg</c> and
/// <c>DiagramRenderer.GetHitRegions</c> and builds the root's inline style
/// (see <see cref="_rootStyle"/>). The lib SVG is injected as a
/// <see cref="MarkupString"/> inside a <c>pointer-events: none</c> child so clicks
/// fall through to the overlay.
/// </para>
///
/// <para>
/// <b>Transparent click overlay.</b> A second <c>&lt;svg&gt;</c>, sized from the
/// same <c>BoardHitRegions.ViewBox</c> as the diagram, carries one transparent
/// <c>&lt;rect&gt;</c> per hit region: each board point (invoking
/// <see cref="OnPointClicked"/> with 1–24), the bar (<see cref="OnBarClicked"/>
/// with 25), the on-roll tray (<see cref="OnTrayClicked"/>), and the dice region
/// when present (<see cref="OnDiceClicked"/>). The view forwards clicks; it does
/// not interpret them.
/// </para>
///
/// <para>
/// <b>Point rects are addressable.</b> Each point's rect carries
/// <c>data-point="N"</c> (N = 1–24, the same index the callback reports); no
/// other region's rect carries the attribute, so <c>rect[data-point]</c> is
/// exactly the point set and <c>rect[data-point="7"]</c> is point 7. This
/// exists for out-of-process drivers — a browser automation harness cannot
/// bind an <c>EventCallback</c>, and before this attribute its only handle was
/// the overlay's render order (rect index = point − 1), a positional contract
/// that was documented at the consumer but never stated by the producer
/// (<c>halheinrich/backgammon#13</c>). Render order is unchanged, so the
/// positional reading still holds; it is simply no longer the only one. It is
/// deliberately a plain <c>data-</c> attribute rather than an <c>id</c>: it
/// repeats across two diagrams on one page without minting duplicate ids.
/// </para>
///
/// <para>
/// <b>Stateful and cube-decision consumers.</b> A consumer that needs
/// click-by-click move entry composes this via <see cref="BackgammonPlayEntry"/>;
/// a cube decision is answered with the free-standing
/// <see cref="BackgammonCubeActions"/> row beside a bare diagram.
/// </para>
///
/// <para>
/// <b>Self-sizing.</b> The <c>.bg-diagram</c> root carries an
/// <c>aspect-ratio</c> derived from the rendered viewBox plus a contain-fit
/// default, so a consumer supplies one definite dimension for width-driven flow
/// or a definite height to letterbox — see the sizing pitfalls in the submodule
/// <c>INSTRUCTIONS.md</c>.
/// </para>
/// </summary>
public partial class BackgammonDiagram : ComponentBase
{
    // -----------------------------------------------------------------------
    //  Required parameters
    // -----------------------------------------------------------------------

    /// <summary>
    /// The position and match state to render. A null request is the no-op
    /// path: the component clears its cached outputs and renders nothing.
    /// </summary>
    [Parameter]
    public DiagramRequest? Request { get; set; }

    /// <summary>
    /// Rendering options (orientation, aspect preset, etc.), forwarded to the
    /// lib's renderer. Defaults to <c>new()</c>.
    /// </summary>
    [Parameter]
    public DiagramOptions Options { get; set; } = new();

    // -----------------------------------------------------------------------
    //  Interactivity — wired to transparent click overlay
    // -----------------------------------------------------------------------

    /// <summary>Fired when a board point is clicked. Returns 1–24.</summary>
    [Parameter]
    public EventCallback<int> OnPointClicked { get; set; }

    /// <summary>Fired when the bar is clicked. Returns 25.</summary>
    [Parameter]
    public EventCallback<int> OnBarClicked { get; set; }

    /// <summary>Fired when the on-roll player's bearing-off tray is clicked.</summary>
    [Parameter]
    public EventCallback OnTrayClicked { get; set; }

    /// <summary>Fired when the dice area is clicked. The view forwards the click;
    /// it does not interpret it (submit vs. swap is the consumer's choice).</summary>
    [Parameter]
    public EventCallback OnDiceClicked { get; set; }

    // -----------------------------------------------------------------------
    //  Catch-all for arbitrary HTML attributes (e.g. style, id, class)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Catch-all for arbitrary HTML attributes (e.g. <c>style</c>, <c>id</c>,
    /// <c>class</c>) splatted onto the outer <c>.bg-diagram</c> wrapper
    /// <c>div</c>. Note that passing <c>style</c> here overrides the component's
    /// inline root style and drops the self-sizing ratio and contain-fit default
    /// (see the self-sizing pitfall) — prefer sizing via the container.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    // -----------------------------------------------------------------------
    //  Internal state
    // -----------------------------------------------------------------------

    private string? _svgMarkup;
    private BoardHitRegions? _hitRegions;

    /// <summary>
    /// Inline style for the <c>.bg-diagram</c> root: <c>position: relative</c>
    /// (the overlay's positioning context) plus an <c>aspect-ratio</c> derived
    /// from the rendered viewBox, so the component is intrinsically sizable —
    /// a consumer supplies a width <em>or</em> a height and the box preserves
    /// the board's ratio without re-encoding it. The ratio is render-time
    /// dynamic (it tracks <see cref="DiagramOptions.Aspect"/>), so it lives
    /// here, inline, sourced from the same <see cref="BoardHitRegions.ViewBox"/>
    /// the overlay uses — one source, no literal.
    ///
    /// <para>
    /// The style also carries the <b>contain-fit default</b>:
    /// <c>max-height: 100%</c> plus <c>margin-inline: auto</c>. When a consumer
    /// gives the containing block a <em>definite</em> height, the board caps to
    /// it and the width re-derives through the aspect-ratio (CSS transfers the
    /// max constraint across the ratio), letterboxing the board centered in its
    /// row. In an unbounded flow both declarations are inert — a percentage
    /// max-height against an indefinite containing-block height computes to
    /// <c>none</c>, and the auto margins are zero while the box fills its row —
    /// so width-driven consumers are unaffected.
    /// </para>
    /// </summary>
    private string? _rootStyle;

    // -----------------------------------------------------------------------
    //  Lifecycle
    // -----------------------------------------------------------------------

    /// <summary>
    /// Single render hook. On a null <see cref="Request"/> it clears the cached
    /// SVG, hit regions, and root style so the component renders nothing;
    /// otherwise it renders the SVG and hit regions via the lib's
    /// <c>DiagramRenderer</c> and builds the root's inline style (aspect-ratio
    /// from the viewBox plus the contain-fit default).
    /// </summary>
    protected override void OnParametersSet()
    {
        if (Request is null)
        {
            _svgMarkup = null;
            _hitRegions = null;
            _rootStyle = null;
            return;
        }

        _svgMarkup = DiagramRenderer.RenderSvg(Request, Options);
        _hitRegions = DiagramRenderer.GetHitRegions(Request, Options);

        var viewBox = _hitRegions.ViewBox;
        _rootStyle = FormattableString.Invariant(
            $"position: relative; aspect-ratio: {viewBox.Width} / {viewBox.Height}; max-height: 100%; margin-inline: auto;");
    }
}
