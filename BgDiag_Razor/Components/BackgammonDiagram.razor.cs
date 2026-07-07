using Microsoft.AspNetCore.Components;
using BackgammonDiagram_Lib;
using BackgammonDiagram_Lib.Rendering;

namespace BgDiag_Razor.Components;

public partial class BackgammonDiagram : ComponentBase
{
    // -----------------------------------------------------------------------
    //  Required parameters
    // -----------------------------------------------------------------------

    [Parameter]
    public DiagramRequest? Request { get; set; }

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

    /// <summary>Fired when the cube area is clicked.</summary>
    [Parameter]
    public EventCallback OnCubeClicked { get; set; }

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
    /// </summary>
    private string? _rootStyle;

    // -----------------------------------------------------------------------
    //  Lifecycle
    // -----------------------------------------------------------------------

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
            $"position: relative; aspect-ratio: {viewBox.Width} / {viewBox.Height};");
    }
}
