using System.Globalization;
using Bunit;
using BgDiag_Razor.Components;
using BackgammonDiagram_Lib;
using BackgammonDiagram_Lib.Rendering;
using BgDataTypes_Lib;

namespace BgDiag_Razor.Tests;

public class BackgammonDiagramTests : BunitContext
{
    private static DiagramRequest DefaultRequest =>
    new DiagramRequest.Builder
    {
        Mop = [0, 2, 0, 0, 0, 0, -5, 0, -3, 0, 0, 0, 5, -5, 0, 0, 0, 3, 0, 5, 0, 0, 0, 0, -2, 0],
        OnRollName = "Player",
        OpponentName = "Opponent",
        Dice = [3, 1],
        CubeSize = 1,
        CubeOwner = CubeOwner.Centered,
    }.Build();

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

    [Fact]
    public async Task ClickDiceRect_InvokesOnDiceClicked()
    {
        bool diceFired = false;
        var cut = Render<BackgammonDiagram>(parameters => parameters
            .Add(p => p.Request, DefaultRequest)
            .Add(p => p.Options, new DiagramOptions())
            .Add(p => p.OnDiceClicked, () => { diceFired = true; }));

        // Dice rect is emitted last in the overlay (after points, bar, cube, tray).
        var rects = cut.FindAll("rect[fill='transparent'][pointer-events='all']");
        Assert.NotEmpty(rects);

        await rects[^1].ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        Assert.True(diceFired);
    }

    // -----------------------------------------------------------------------
    //  Self-sizing: root carries an aspect-ratio derived from the viewBox
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(AspectPreset.Widescreen16x9)]
    [InlineData(AspectPreset.Standard4x3)]
    [InlineData(AspectPreset.Natural)]
    public void Root_CarriesAspectRatio_FromRenderedViewBox(AspectPreset aspect)
    {
        var options = new DiagramOptions { Aspect = aspect };

        // The overlay and the aspect-ratio must come from the same source: the
        // viewBox the renderer reports. Compute the expected ratio the same way
        // the component sources it, so this pins "derived from the viewBox", not
        // "equals some literal".
        var viewBox = DiagramRenderer.GetHitRegions(DefaultRequest, options).ViewBox;
        var expected = FormattableString.Invariant(
            $"aspect-ratio: {viewBox.Width} / {viewBox.Height}");

        var cut = Render<BackgammonDiagram>(parameters => parameters
            .Add(p => p.Request, DefaultRequest)
            .Add(p => p.Options, options));

        var style = cut.Find(".bg-diagram").GetAttribute("style");
        Assert.Contains("position: relative", style);
        Assert.Contains(expected, style);
    }

    [Fact]
    public void Root_AspectRatio_TracksPreset_NotAHardcodedLiteral()
    {
        // Two presets with genuinely different overall ratios must emit two
        // different aspect-ratios — a hardcoded 16/9 literal would fail this.
        var wide = Render<BackgammonDiagram>(parameters => parameters
            .Add(p => p.Request, DefaultRequest)
            .Add(p => p.Options, new DiagramOptions { Aspect = AspectPreset.Widescreen16x9 }));
        var standard = Render<BackgammonDiagram>(parameters => parameters
            .Add(p => p.Request, DefaultRequest)
            .Add(p => p.Options, new DiagramOptions { Aspect = AspectPreset.Standard4x3 }));

        var wideStyle = wide.Find(".bg-diagram").GetAttribute("style");
        var standardStyle = standard.Find(".bg-diagram").GetAttribute("style");

        Assert.Contains("aspect-ratio:", wideStyle);
        Assert.Contains("aspect-ratio:", standardStyle);
        Assert.NotEqual(wideStyle, standardStyle);
    }

    [Fact]
    public void Root_CarriesContainFitDefault()
    {
        // The bounded-height contract: the root's inline style caps the box to
        // its containing block's definite height (max-height) and centers the
        // letterboxed board in its row (auto inline margins). Both declarations
        // are inert in an unbounded flow — a percentage max-height against an
        // indefinite containing-block height computes to none, and auto margins
        // are zero while the box fills its row — so width-driven consumers see
        // no change (validated empirically; bUnit can pin only the emitted style).
        var cut = Render<BackgammonDiagram>(parameters => parameters
            .Add(p => p.Request, DefaultRequest)
            .Add(p => p.Options, new DiagramOptions()));

        var style = cut.Find(".bg-diagram").GetAttribute("style");
        Assert.Contains("max-height: 100%", style);
        Assert.Contains("margin-inline: auto", style);
    }

    // -----------------------------------------------------------------------
    //  Overlay slot: consumer-supplied content positioned on the board
    // -----------------------------------------------------------------------

    [Fact]
    public void Overlay_Unset_IsCompleteNoOp()
    {
        var cut = Render<BackgammonDiagram>(parameters => parameters
            .Add(p => p.Request, DefaultRequest)
            .Add(p => p.Options, new DiagramOptions()));

        Assert.DoesNotContain("bg-diagram-overlay", cut.Markup);
    }

    [Fact]
    public void Overlay_Supplied_RendersInsideBgDiagram()
    {
        var cut = Render<BackgammonDiagram>(parameters => parameters
            .Add(p => p.Request, DefaultRequest)
            .Add(p => p.Options, new DiagramOptions())
            .Add(p => p.Overlay, builder => builder.AddMarkupContent(0, "<div class=\"my-badge\">XGID</div>")));

        var root = cut.Find(".bg-diagram");
        var overlay = root.QuerySelector(".bg-diagram-overlay");
        Assert.NotNull(overlay);
        Assert.Contains("my-badge", overlay!.InnerHtml);
    }

    [Fact]
    public void Overlay_Wrapper_HasPointerEventsNone_SoItNeverBlocksBoardClicks()
    {
        var cut = Render<BackgammonDiagram>(parameters => parameters
            .Add(p => p.Request, DefaultRequest)
            .Add(p => p.Options, new DiagramOptions())
            .Add(p => p.Overlay, builder => builder.AddMarkupContent(0, "<div class=\"my-badge\">XGID</div>")));

        var style = cut.Find(".bg-diagram-overlay").GetAttribute("style");
        Assert.Contains("pointer-events: none", style);
    }

    [Fact]
    public void NullRequest_OverlaySupplied_StillRendersEmpty()
    {
        // A null Request is the component's other no-op path; Overlay must
        // not force markup out when there's no board to overlay it on.
        var cut = Render<BackgammonDiagram>(parameters => parameters
            .Add(p => p.Request, null)
            .Add(p => p.Overlay, builder => builder.AddMarkupContent(0, "<div class=\"my-badge\">XGID</div>")));

        Assert.Equal(string.Empty, cut.Markup.Trim());
    }

    // -----------------------------------------------------------------------
    //  Orientation tests
    // -----------------------------------------------------------------------

    [Fact]
    public void HitRegions_PointOne_DiffersWhenHomeBoardOnRight_IsToggled()
    {
        var requestDefault = new DiagramRequest.Builder
        {
            Mop = [0, 2, 0, 0, 0, 0, -5, 0, -3, 0, 0, 0, 5, -5, 0, 0, 0, 3, 0, 5, 0, 0, 0, 0, -2, 0],
            OnRollName = "Player",
            OpponentName = "Opponent",
            Dice = [3, 1],
            CubeSize = 1,
            CubeOwner = CubeOwner.Centered,
            HomeBoardOnRight = true,
        }.Build();
        var requestFlipped = new DiagramRequest.Builder
        {
            Mop = [0, 2, 0, 0, 0, 0, -5, 0, -3, 0, 0, 0, 5, -5, 0, 0, 0, 3, 0, 5, 0, 0, 0, 0, -2, 0],
            OnRollName = "Player",
            OpponentName = "Opponent",
            Dice = [3, 1],
            CubeSize = 1,
            CubeOwner = CubeOwner.Centered,
            HomeBoardOnRight = false,
        }.Build();

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

    // -----------------------------------------------------------------------
    //  Culture-invariant overlay markup (regression: comma-decimal locales)
    // -----------------------------------------------------------------------

    [Fact]
    public void Overlay_MarkupIsCultureInvariant_UnderCommaDecimalLocale()
    {
        // Blazor formats interpolated values with the thread culture, and WASM
        // adopts the browser locale. Under a comma-decimal culture (nb-NO), a
        // bare `@r.Width` for e.g. 30.8 emits "30,8" — an invalid SVG attribute
        // a browser parses as 0, so the bar (and the viewBox) silently break.
        // The overlay must route every geometry number through the lib's
        // culture-invariant formatter instead. Confirmed as a production bug by
        // a Norway beta tester on an nb-NO browser.
        RunUnderCulture("nb-NO", () =>
        {
            // Sanity: the hostile culture really does use a comma decimal, so a
            // culture-clean overlay below is a genuine result, not a no-op.
            Assert.Equal("30,8", 30.8.ToString("0.##"));

            var options = new DiagramOptions();
            var cut = Render<BackgammonDiagram>(parameters => parameters
                .Add(p => p.Request, DefaultRequest)
                .Add(p => p.Options, options));

            // Guard against a vacuous pass: pin the assertion over the *real*
            // overlay, not an empty selection. The expected count is derived
            // from the same hit regions the overlay is built from — 24 points +
            // bar, plus cube/tray/dice as this request produces them.
            var regions = DiagramRenderer.GetHitRegions(DefaultRequest, options);
            var expectedRects = regions.Points.Count + 1                        // bar
                + (regions.Cube is null ? 0 : 1)
                + (regions.OnRollTray is null ? 0 : 1)
                + (regions.Dice is null ? 0 : 1);

            var rects = cut.FindAll("rect[fill='transparent'][pointer-events='all']");
            Assert.True(rects.Count >= 26,
                $"Expected a rich overlay (>=26 rects: 24 points + bar + cube), found {rects.Count}.");
            Assert.Equal(expectedRects, rects.Count);

            foreach (var rect in rects)
                foreach (var attr in new[] { "x", "y", "width", "height" })
                    AssertInvariantNumber(rect.GetAttribute(attr), $"rect @{attr}");

            // The overlay <svg> is the absolutely-positioned one (the diagram
            // SVG the lib injects is a MarkupString sibling with no such style).
            var overlaySvg = cut.Find("svg[style*='position: absolute']");
            var viewBox = overlaySvg.GetAttribute("viewBox");
            Assert.NotNull(viewBox);
            var parts = viewBox!.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(4, parts.Length);
            foreach (var part in parts)
                AssertInvariantNumber(part, "viewBox component");
        });
    }

    // Asserts a rendered attribute value is a culture-clean SVG number: no
    // comma decimal separator, and parseable as an invariant-culture double.
    private static void AssertInvariantNumber(string? value, string what)
    {
        Assert.NotNull(value);
        Assert.DoesNotContain(",", value);
        Assert.True(
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _),
            $"{what} '{value}' does not parse as an invariant-culture double.");
    }

    // Runs the assertion under a hostile comma-decimal culture, restoring the
    // prior cultures afterwards. CurrentCulture is what interpolation actually
    // reads on the executing thread; DefaultThreadCurrentCulture is set too so
    // any thread spawned inside the action inherits the hostile culture. Mirrors
    // the pattern established in BackgammonDiagram_Lib's SvgFormatTests.
    private static void RunUnderCulture(string cultureName, Action assertion)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        var priorCurrent = CultureInfo.CurrentCulture;
        var priorDefault = CultureInfo.DefaultThreadCurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            assertion();
        }
        finally
        {
            CultureInfo.CurrentCulture = priorCurrent;
            CultureInfo.DefaultThreadCurrentCulture = priorDefault;
        }
    }
}
