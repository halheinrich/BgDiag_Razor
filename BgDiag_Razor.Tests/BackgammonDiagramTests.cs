using Bunit;
using BgDiag_Razor.Components;
using BackgammonDiagram_Lib;

namespace BgDiag_Razor.Tests;

public class BackgammonDiagramTests : BunitContext
{
    private static DiagramRequest DefaultRequest => new()
    {
        Mop = [0, 2, 0, 0, 0, 0, -5, 0, -3, 0, 0, 0, 5, -5, 0, 0, 0, 3, 0, 5, 0, 0, 0, 0, -2, 0],
        OnRollName = "Player",
        OpponentName = "Opponent",
        Dice = [3, 1],
        CubeSize = 1,
        CubeOwner = CubeOwner.Centered,
    };

    // -----------------------------------------------------------------------
    //  Existing tests (preserved)
    // -----------------------------------------------------------------------

    [Fact]
    public void Render_WithValidRequest_ContainsSvg()
    {
        var cut = Render<BackgammonDiagram>(parameters => parameters
            .Add(p => p.Request, DefaultRequest)
            .Add(p => p.Options, new DiagramOptions()));

        Assert.Contains("<svg", cut.Markup);
    }

    [Fact]
    public void Render_WithNullRequest_RendersEmpty()
    {
        var cut = Render<BackgammonDiagram>(parameters => parameters
            .Add(p => p.Request, null));

        Assert.Equal(string.Empty, cut.Markup.Trim());
    }

    [Fact]
    public void EventCallbacks_AreExposed()
    {
        var cut = Render<BackgammonDiagram>(parameters => parameters
            .Add(p => p.Request, DefaultRequest)
            .Add(p => p.OnPointClicked, (int _) => { })
            .Add(p => p.OnBarClicked, (int _) => { })
            .Add(p => p.OnCubeClicked, () => { })
            .Add(p => p.OnTrayClicked, () => { }));

        Assert.NotNull(cut);
    }

    // -----------------------------------------------------------------------
    //  Overlay rendering tests
    // -----------------------------------------------------------------------

    [Fact]
    public void Render_WithValidRequest_ContainsOverlaySvg()
    {
        var cut = Render<BackgammonDiagram>(parameters => parameters
            .Add(p => p.Request, DefaultRequest)
            .Add(p => p.Options, new DiagramOptions()));

        // Two <svg elements: the diagram and the overlay
        var svgCount = cut.Markup.Split("<svg").Length - 1;
        Assert.True(svgCount >= 2, $"Expected at least 2 <svg> elements, found {svgCount}");
    }

    [Fact]
    public void Overlay_Contains24PointRects()
    {
        var cut = Render<BackgammonDiagram>(parameters => parameters
            .Add(p => p.Request, DefaultRequest)
            .Add(p => p.Options, new DiagramOptions()));

        // 24 point rects + 1 bar rect + cube rect = 26 transparent rects
        var rects = cut.FindAll("rect[fill='transparent']");
        Assert.True(rects.Count >= 25,
            $"Expected at least 25 transparent rects (24 points + bar), found {rects.Count}");
    }

    [Fact]
    public void Overlay_BarRectIsPresent()
    {
        var cut = Render<BackgammonDiagram>(parameters => parameters
            .Add(p => p.Request, DefaultRequest)
            .Add(p => p.Options, new DiagramOptions()));

        var transparentRects = cut.FindAll("rect[fill='transparent'][pointer-events='all']");
        Assert.NotEmpty(transparentRects);
    }

    [Fact]
    public void NullRequest_NoOverlayRendered()
    {
        var cut = Render<BackgammonDiagram>(parameters => parameters
            .Add(p => p.Request, null));

        var rects = cut.FindAll("rect[fill='transparent']");
        Assert.Empty(rects);
    }

    // -----------------------------------------------------------------------
    //  Click callback tests
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ClickPointRect_InvokesOnPointClicked_WithCorrectNumber()
    {
        int clickedPoint = -1;
        var cut = Render<BackgammonDiagram>(parameters => parameters
            .Add(p => p.Request, DefaultRequest)
            .Add(p => p.Options, new DiagramOptions())
            .Add(p => p.OnPointClicked, (int pt) => { clickedPoint = pt; }));

        var rects = cut.FindAll("rect[fill='transparent'][pointer-events='all']");
        Assert.NotEmpty(rects);

        // First rect should be a point (1–24)
        await rects[0].ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        Assert.InRange(clickedPoint, 1, 24);
    }

    [Fact]
    public async Task ClickBarRect_InvokesOnBarClicked_With25()
    {
        int barValue = -1;
        var cut = Render<BackgammonDiagram>(parameters => parameters
            .Add(p => p.Request, DefaultRequest)
            .Add(p => p.Options, new DiagramOptions())
            .Add(p => p.OnBarClicked, (int v) => { barValue = v; }));

        // Bar rect comes after 24 point rects in the overlay
        var rects = cut.FindAll("rect[fill='transparent'][pointer-events='all']");
        Assert.True(rects.Count >= 25, $"Expected at least 25 rects, found {rects.Count}");

        // Index 24 is the bar rect
        await rects[24].ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        Assert.Equal(25, barValue);
    }

    [Fact]
    public async Task ClickCubeRect_InvokesOnCubeClicked()
    {
        bool cubeFired = false;
        var cut = Render<BackgammonDiagram>(parameters => parameters
            .Add(p => p.Request, DefaultRequest)
            .Add(p => p.Options, new DiagramOptions())
            .Add(p => p.OnCubeClicked, () => { cubeFired = true; }));

        var rects = cut.FindAll("rect[fill='transparent'][pointer-events='all']");
        // Cube rect follows bar (index 25) when present
        Assert.True(rects.Count >= 26, $"Expected at least 26 rects (24 pts + bar + cube), found {rects.Count}");

        await rects[25].ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        Assert.True(cubeFired);
    }

    // -----------------------------------------------------------------------
    //  Orientation tests
    // -----------------------------------------------------------------------

    [Fact]
    public void HitRegions_PointOne_DiffersWhenHomeBoardOnRight_IsToggled()
    {
        var requestDefault = DefaultRequest with { HomeBoardOnRight = true };
        var requestFlipped = DefaultRequest with { HomeBoardOnRight = false };

        var cutDefault = Render<BackgammonDiagram>(p => p
            .Add(p => p.Request, requestDefault)
            .Add(p => p.Options, new DiagramOptions()));

        var cutFlipped = Render<BackgammonDiagram>(p => p
            .Add(p => p.Request, requestFlipped)
            .Add(p => p.Options, new DiagramOptions()));

        var rectsDefault = cutDefault.FindAll("rect[fill='transparent'][pointer-events='all']");
        var rectsFlipped = cutFlipped.FindAll("rect[fill='transparent'][pointer-events='all']");

        var xDefault = rectsDefault[0].GetAttribute("x");
        var xFlipped = rectsFlipped[0].GetAttribute("x");

        Assert.NotEqual(xDefault, xFlipped);
    }
}
