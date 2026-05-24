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
    public void Render_WithCubeRequest_ContainsInnerDiagramAndButtons()
    {
        var cut = Render<BackgammonCubeEntry>(p => p
            .Add(c => c.Request, CubeRequest()));

        Assert.Contains("<svg", cut.Markup);
        Assert.Contains("bg-cube-entry", cut.Markup);

        // Four verdict buttons, one per CubeVerdict member.
        var buttons = cut.FindAll("button.bg-cube-verdict");
        Assert.Equal(4, buttons.Count);
    }

    [Fact]
    public void Render_WithNullRequest_RendersEmpty()
    {
        var cut = Render<BackgammonCubeEntry>(p => p.Add(c => c.Request, null));
        Assert.Equal(string.Empty, cut.Markup.Trim());
    }

    [Fact]
    public void AdditionalAttributes_AreSplattedOnOuterDiv()
    {
        var cut = Render<BackgammonCubeEntry>(p => p
            .Add(c => c.Request, CubeRequest())
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
            Render<BackgammonCubeEntry>(p => p.Add(c => c.Request, PlayRequest())));
    }

    // -----------------------------------------------------------------------
    //  Verdict-button click fires OnCubeVerdictCompleted with matching enum
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(0, CubeVerdict.NoDouble)]
    [InlineData(1, CubeVerdict.DoubleTake)]
    [InlineData(2, CubeVerdict.DoublePass)]
    [InlineData(3, CubeVerdict.TooGood)]
    public async Task VerdictButtonClick_FiresCallbackWithMatchingEnum(
        int buttonIndex, CubeVerdict expected)
    {
        CubeVerdict? received = null;
        var fireCount = 0;

        var cut = Render<BackgammonCubeEntry>(p => p
            .Add(c => c.Request, CubeRequest())
            .Add(c => c.OnCubeVerdictCompleted,
                (CubeVerdict v) => { received = v; fireCount++; }));

        var buttons = cut.FindAll("button.bg-cube-verdict");
        await buttons[buttonIndex].ClickAsync(new MouseEventArgs());

        Assert.Equal(1, fireCount);
        Assert.Equal(expected, received);
    }

    // -----------------------------------------------------------------------
    //  Re-selection contract: each click fires; no internal one-shot lock
    // -----------------------------------------------------------------------

    [Fact]
    public async Task MultipleClicks_FireMultipleTimes()
    {
        var verdicts = new List<CubeVerdict>();

        var cut = Render<BackgammonCubeEntry>(p => p
            .Add(c => c.Request, CubeRequest())
            .Add(c => c.OnCubeVerdictCompleted,
                (CubeVerdict v) => verdicts.Add(v)));

        // Click the first three verdict buttons in order. Re-fetch the
        // button list each time to avoid stale references after re-render.
        for (int i = 0; i < 3; i++)
        {
            var buttons = cut.FindAll("button.bg-cube-verdict");
            await buttons[i].ClickAsync(new MouseEventArgs());
        }

        Assert.Equal(
            new[] { CubeVerdict.NoDouble, CubeVerdict.DoubleTake, CubeVerdict.DoublePass },
            verdicts);
    }
}
