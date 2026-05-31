using Bunit;
using BgDiag_Razor.Components;
using BackgammonDiagram_Lib;
using BgDataTypes_Lib;
using Microsoft.AspNetCore.Components.Web;

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

    // -----------------------------------------------------------------------
    //  Render
    // -----------------------------------------------------------------------

    [Fact]
    public void Render_WithCubeRequest_ContainsInnerDiagramAndBothGroups()
    {
        var cut = Render<BackgammonCubeEntry>(p => p
            .Add(c => c.Request, CubeRequest())
            .Add(c => c.OnCubeDecisionCompleted, (CubeDecisionPair _) => { }));

        Assert.Contains("<svg", cut.Markup);
        Assert.Contains("bg-cube-entry", cut.Markup);

        // Doubler group: two buttons (No Double / Double).
        var doublerButtons = cut.FindAll(".bg-cube-doubler button");
        Assert.Equal(2, doublerButtons.Count);

        // Taker group: two buttons (Take / Pass).
        var takerButtons = cut.FindAll(".bg-cube-taker button");
        Assert.Equal(2, takerButtons.Count);
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
    //  Completion — selecting one doubler + one taker fires once with the
    //  matching CubeDecisionPair. Parameterized over the full 2×2 grid.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(0, 0, CubeAction.NoDouble, CubeAction.Take)]
    [InlineData(0, 1, CubeAction.NoDouble, CubeAction.Pass)]
    [InlineData(1, 0, CubeAction.Double,   CubeAction.Take)]
    [InlineData(1, 1, CubeAction.Double,   CubeAction.Pass)]
    public async Task SelectingBothHalves_FiresOnceWithMatchingPair(
        int doublerIndex, int takerIndex,
        CubeAction expectedDoubler, CubeAction expectedTaker)
    {
        CubeDecisionPair? received = null;
        var fireCount = 0;

        var cut = Render<BackgammonCubeEntry>(p => p
            .Add(c => c.Request, CubeRequest())
            .Add(c => c.OnCubeDecisionCompleted,
                (CubeDecisionPair pair) => { received = pair; fireCount++; }));

        await cut.FindAll(".bg-cube-doubler button")[doublerIndex].ClickAsync(new MouseEventArgs());
        await cut.FindAll(".bg-cube-taker button")[takerIndex].ClickAsync(new MouseEventArgs());

        Assert.Equal(1, fireCount);
        Assert.Equal(new CubeDecisionPair(expectedDoubler, expectedTaker), received);
    }

    // -----------------------------------------------------------------------
    //  Gating — only one half selected does not fire
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SelectingOnlyDoubler_DoesNotFire()
    {
        var fireCount = 0;

        var cut = Render<BackgammonCubeEntry>(p => p
            .Add(c => c.Request, CubeRequest())
            .Add(c => c.OnCubeDecisionCompleted, (CubeDecisionPair _) => fireCount++));

        await cut.FindAll(".bg-cube-doubler button")[1].ClickAsync(new MouseEventArgs());

        Assert.Equal(0, fireCount);
    }

    [Fact]
    public async Task SelectingOnlyTaker_DoesNotFire()
    {
        var fireCount = 0;

        var cut = Render<BackgammonCubeEntry>(p => p
            .Add(c => c.Request, CubeRequest())
            .Add(c => c.OnCubeDecisionCompleted, (CubeDecisionPair _) => fireCount++));

        await cut.FindAll(".bg-cube-taker button")[0].ClickAsync(new MouseEventArgs());

        Assert.Equal(0, fireCount);
    }

    // -----------------------------------------------------------------------
    //  Re-selection — changing a half after completion re-fires with the
    //  latest pair (no internal one-shot lock)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ReSelectingAfterCompletion_FiresAgainWithLatestPair()
    {
        var received = new List<CubeDecisionPair>();

        var cut = Render<BackgammonCubeEntry>(p => p
            .Add(c => c.Request, CubeRequest())
            .Add(c => c.OnCubeDecisionCompleted,
                (CubeDecisionPair pair) => received.Add(pair)));

        // Complete the pair: Double + Take.
        await cut.FindAll(".bg-cube-doubler button")[1].ClickAsync(new MouseEventArgs());
        await cut.FindAll(".bg-cube-taker button")[0].ClickAsync(new MouseEventArgs());

        // Change the doubler half to No Double — both halves still set, so it re-fires.
        await cut.FindAll(".bg-cube-doubler button")[0].ClickAsync(new MouseEventArgs());

        Assert.Equal(
            new[]
            {
                new CubeDecisionPair(CubeAction.Double, CubeAction.Take),
                new CubeDecisionPair(CubeAction.NoDouble, CubeAction.Take),
            },
            received);
    }

    // -----------------------------------------------------------------------
    //  Selected-state class reflects the chosen action within each group
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SelectedButton_GetsSelectedStateClass()
    {
        var cut = Render<BackgammonCubeEntry>(p => p
            .Add(c => c.Request, CubeRequest())
            .Add(c => c.OnCubeDecisionCompleted, (CubeDecisionPair _) => { }));

        await cut.FindAll(".bg-cube-doubler button")[1].ClickAsync(new MouseEventArgs());

        var selected = cut.FindAll(".bg-cube-doubler button.bg-cube-action-selected");
        Assert.Single(selected);
        Assert.Equal("Double", selected[0].TextContent.Trim());
    }
}
