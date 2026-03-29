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
    //  Interactivity contract (not yet wired to SVG elements)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Fired when a board point is clicked. The int is the point number (1-24),
    /// 0 for opponent bar, 25 for on-roll bar.
    /// </summary>
    [Parameter]
    public EventCallback<int> OnPointClicked { get; set; }

    /// <summary>
    /// Fired when a cube action area is clicked.
    /// </summary>
    [Parameter]
    public EventCallback OnCubeActionClicked { get; set; }

    // -----------------------------------------------------------------------
    //  Catch-all for arbitrary HTML attributes (e.g. style, id, class)
    // -----------------------------------------------------------------------

    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    // -----------------------------------------------------------------------
    //  Internal state
    // -----------------------------------------------------------------------

    private string? _svgMarkup;
    private DiagramRenderer _renderer = new();

    // -----------------------------------------------------------------------
    //  Lifecycle
    // -----------------------------------------------------------------------

    protected override void OnParametersSet()
    {
        if (Request is null)
        {
            _svgMarkup = null;
            return;
        }

        _svgMarkup = _renderer.RenderSvg(Request, Options);
    }
}
