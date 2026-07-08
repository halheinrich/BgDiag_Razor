using Bunit;
using BgDiag_Razor.Components;
using BackgammonDiagram_Lib;
using BgDataTypes_Lib;
using Microsoft.AspNetCore.Components;

namespace BgDiag_Razor.Tests;

public class BackgammonCubeEntryTests : BunitContext
{
    // -----------------------------------------------------------------------
    //  Fixtures
    // -----------------------------------------------------------------------

    /// <summary>Standard backgammon starting position.</summary>
    private static int[] StandardMop()
    {
        var m = new int[26];
        m[6] = 5;  m[8] = 3;  m[13] = 5;  m[24] = 2;
        m[19] = -5; m[17] = -3; m[12] = -5; m[1] = -2;
        return m;
    }

    /// <summary>
    /// A cube-decision request: <c>IsCube = true</c>, dice forced to
    /// <c>[0, 0]</c> per the lib's invariant.
    /// </summary>
    private static DiagramRequest CubeRequest() =>
        new DiagramRequest.Builder
        {
            Mop = StandardMop(),
            IsCube = true,
            Dice = [0, 0],
            OnRollName = "Player",
            OpponentName = "Opponent",
            CubeSize = 1,
            CubeOwner = CubeOwner.Centered,
        }.Build();

    /// <summary>
    /// A play-decision request (the wrong half) — used to verify the
    /// symmetric guard.
    /// </summary>
    private static DiagramRequest PlayRequest() =>
        new DiagramRequest.Builder
        {
            Mop = StandardMop(),
            Dice = [3, 1],
            OnRollName = "Player",
            OpponentName = "Opponent",
            CubeSize = 1,
            CubeOwner = CubeOwner.Centered,
        }.Build();

    /// <summary>The four radio inputs, in render order (matches the _cubeOptions table).</summary>
    private static IReadOnlyList<AngleSharp.Dom.IElement> Radios(
        IRenderedComponent<BackgammonCubeEntry> cut) =>
        cut.FindAll(".bg-cube-actions input[type=radio]");

    // -----------------------------------------------------------------------
    //  Render
    // -----------------------------------------------------------------------

    [Fact]
    public void Render_WithCubeRequest_ContainsInnerDiagramAndFourRadios()
    {
        var cut = Render<BackgammonCubeEntry>(p => p
            .Add(c => c.Request, CubeRequest())
            .Add(c => c.OnCubeDecisionCompleted, (CubeDecisionPair _) => { }));

        Assert.Contains("<svg", cut.Markup);
        Assert.Contains("bg-cube-entry", cut.Markup);

        // One radio group holding the four mutually-exclusive options.
        Assert.Single(cut.FindAll("[role=radiogroup]"));
        Assert.Equal(4, Radios(cut).Count);

        // All four bijection labels are present.
        Assert.Contains("No double", cut.Markup);
        Assert.Contains("Double/Take", cut.Markup);
        Assert.Contains("Double/Pass", cut.Markup);
        Assert.Contains("Too good", cut.Markup);
    }

    [Fact]
    public void Render_WithNullRequest_RendersEmpty()
    {
        var cut = Render<BackgammonCubeEntry>(p => p
            .Add(c => c.Request, null)
            .Add(c => c.OnCubeDecisionCompleted, (CubeDecisionPair _) => { }));
        Assert.Equal(string.Empty, cut.Markup.Trim());
    }

    [Fact]
    public void AdditionalAttributes_AreSplattedOnOuterDiv()
    {
        var cut = Render<BackgammonCubeEntry>(p => p
            .Add(c => c.Request, CubeRequest())
            .Add(c => c.OnCubeDecisionCompleted, (CubeDecisionPair _) => { })
            .AddUnmatched("data-testid", "cube-entry-1"));

        Assert.Contains("data-testid=\"cube-entry-1\"", cut.Markup);
    }

    [Fact]
    public void Overlay_Unset_IsCompleteNoOp()
    {
        var cut = Render<BackgammonCubeEntry>(p => p
            .Add(c => c.Request, CubeRequest())
            .Add(c => c.OnCubeDecisionCompleted, (CubeDecisionPair _) => { }));

        Assert.DoesNotContain("bg-diagram-overlay", cut.Markup);
    }

    [Fact]
    public void Overlay_Supplied_ForwardsToInnerDiagram()
    {
        var cut = Render<BackgammonCubeEntry>(p => p
            .Add(c => c.Request, CubeRequest())
            .Add(c => c.OnCubeDecisionCompleted, (CubeDecisionPair _) => { })
            .Add(c => c.Overlay, builder => builder.AddMarkupContent(0, "<div class=\"my-badge\">XGID</div>")));

        var overlay = cut.Find(".bg-diagram-overlay");
        Assert.Contains("my-badge", overlay.InnerHtml);
    }

    // -----------------------------------------------------------------------
    //  Symmetric guard — play decisions are rejected at the contract boundary
    // -----------------------------------------------------------------------

    [Fact]
    public void PlayDecision_ThrowsNotImplemented()
    {
        Assert.Throws<NotImplementedException>(() =>
            Render<BackgammonCubeEntry>(p => p
                .Add(c => c.Request, PlayRequest())
                .Add(c => c.OnCubeDecisionCompleted, (CubeDecisionPair _) => { })));
    }

    // -----------------------------------------------------------------------
    //  Completion — selecting one radio fires once with the matching
    //  CubeDecisionPair. Parameterized over all four options; the index tracks
    //  the _cubeOptions render order. Note "Too good" (index 3) maps to
    //  (NoDouble, Pass) — don't double, but the opponent would pass.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(0, CubeAction.NoDouble, CubeAction.Take)]
    [InlineData(1, CubeAction.Double,   CubeAction.Take)]
    [InlineData(2, CubeAction.Double,   CubeAction.Pass)]
    [InlineData(3, CubeAction.NoDouble, CubeAction.Pass)]
    public async Task SelectingRadio_FiresOnceWithMatchingPair(
        int radioIndex, CubeAction expectedDoubler, CubeAction expectedTaker)
    {
        CubeDecisionPair? received = null;
        var fireCount = 0;

        var cut = Render<BackgammonCubeEntry>(p => p
            .Add(c => c.Request, CubeRequest())
            .Add(c => c.OnCubeDecisionCompleted,
                (CubeDecisionPair pair) => { received = pair; fireCount++; }));

        await Radios(cut)[radioIndex].ChangeAsync(new ChangeEventArgs { Value = true });

        Assert.Equal(1, fireCount);
        Assert.Equal(new CubeDecisionPair(expectedDoubler, expectedTaker), received);
    }

    // -----------------------------------------------------------------------
    //  Switching — selecting a different radio re-fires with the new pair.
    //  (One radio completes the pair, so there is no half-selected state; the
    //  "does not fire while incomplete" contract is trivially preserved.)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SwitchingRadio_FiresAgainWithNewPair()
    {
        var received = new List<CubeDecisionPair>();

        var cut = Render<BackgammonCubeEntry>(p => p
            .Add(c => c.Request, CubeRequest())
            .Add(c => c.OnCubeDecisionCompleted,
                (CubeDecisionPair pair) => received.Add(pair)));

        // Select "Double/Take" (index 1), then switch to "Too good" (index 3).
        await Radios(cut)[1].ChangeAsync(new ChangeEventArgs { Value = true });
        await Radios(cut)[3].ChangeAsync(new ChangeEventArgs { Value = true });

        Assert.Equal(
            new[]
            {
                new CubeDecisionPair(CubeAction.Double, CubeAction.Take),
                new CubeDecisionPair(CubeAction.NoDouble, CubeAction.Pass),
            },
            received);
    }

    // -----------------------------------------------------------------------
    //  Selected-state class tracks the single selected radio
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SelectedRadio_GetsSelectedStateClass()
    {
        var cut = Render<BackgammonCubeEntry>(p => p
            .Add(c => c.Request, CubeRequest())
            .Add(c => c.OnCubeDecisionCompleted, (CubeDecisionPair _) => { }));

        // Select "Double/Take" (index 1).
        await Radios(cut)[1].ChangeAsync(new ChangeEventArgs { Value = true });

        var selected = cut.FindAll(".bg-cube-action.bg-cube-action-selected");
        Assert.Single(selected);
        Assert.Equal("Double/Take", selected[0].TextContent.Trim());
    }
}
