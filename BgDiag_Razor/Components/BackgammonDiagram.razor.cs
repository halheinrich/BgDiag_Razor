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
    private DiagramRenderer _renderer = new();

    // -----------------------------------------------------------------------
    //  Lifecycle
    // -----------------------------------------------------------------------

    protected override void OnParametersSet()
    {
        if (Request is null)
        {
            _svgMarkup = null;
            _hitRegions = null;
            return;
        }

        _svgMarkup = _renderer.RenderSvg(Request, Options);
        _hitRegions = _renderer.GetHitRegions(Request, Options);
    }
}
