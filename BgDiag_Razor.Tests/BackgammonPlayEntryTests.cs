using Bunit;
using BgDiag_Razor.Components;
using BackgammonDiagram_Lib;
using BackgammonDiagram_Lib.Rendering;
using BgDataTypes_Lib;
using BgMoveGen;
using Microsoft.AspNetCore.Components.Web;

namespace BgDiag_Razor.Tests;

public class BackgammonPlayEntryTests : BunitContext
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

    private static DiagramRequest StandardRequest(int die1 = 3, int die2 = 1) =>
        new DiagramRequest.Builder
        {
            Mop = StandardMop(),
            OnRollName = "Player",
            OpponentName = "Opponent",
            Dice = [die1, die2],
            CubeSize = 1,
            CubeOwner = CubeOwner.Centered,
        }.Build();

    /// <summary>
    /// One player checker on the 1-pt with dice (1,1). The only legal play is
    /// 1/off — single move, single Play. Used to drive deterministic completion
    /// sequences without hand-picking ambiguous click orderings.
    /// </summary>
    private static DiagramRequest BearOffOneRequest()
    {
        var m = new int[26];
        m[1] = 1;
        return new DiagramRequest.Builder
        {
            Mop = m,
            OnRollName = "Player",
            OpponentName = "Opponent",
            Dice = [1, 1],
            CubeSize = 1,
            CubeOwner = CubeOwner.Centered,
        }.Build();
    }

    // -----------------------------------------------------------------------
    //  Click helpers — translate a click target (point/bar/tray) to the
    //  matching transparent overlay rect rendered by the inner diagram.
    //  Order mirrors BackgammonDiagram.razor's overlay emission: Points dict
    //  in iteration order, then bar, then optional cube, then optional tray.
    //  Rects are re-found per click so post-render handler IDs stay fresh.
    // -----------------------------------------------------------------------

    private static int RectIndexForPoint(DiagramRequest req, int point)
    {
        var regions = DiagramRenderer.GetHitRegions(req, new DiagramOptions());
        int i = 0;
        foreach (var kvp in regions.Points)
        {
            if (kvp.Key == point) return i;
            i++;
        }
        throw new ArgumentException($"Point {point} not present in regions.");
    }

    private static int RectIndexForTray(DiagramRequest req)
    {
        var regions = DiagramRenderer.GetHitRegions(req, new DiagramOptions());
        if (regions.OnRollTray is null)
            throw new InvalidOperationException("Request has no OnRollTray region.");
        return regions.Points.Count + 1 + (regions.Cube is null ? 0 : 1);
    }

    private static Task ClickRectAsync(
        IRenderedComponent<BackgammonPlayEntry> cut, int rectIndex)
    {
        var rects = cut.FindAll("rect[fill='transparent'][pointer-events='all']");
        return rects[rectIndex].ClickAsync(new MouseEventArgs());
    }

    /// <summary>
    /// Clicks the dice overlay rect. The diagram emits the dice rect last (after
    /// points, bar, cube, tray), and a play always has a dice region, so the
    /// final transparent rect is the dice.
    /// </summary>
    private static Task ClickDiceAsync(IRenderedComponent<BackgammonPlayEntry> cut)
    {
        var rects = cut.FindAll("rect[fill='transparent'][pointer-events='all']");
        return rects[^1].ClickAsync(new MouseEventArgs());
    }

    // -----------------------------------------------------------------------
    //  Render
    // -----------------------------------------------------------------------

    [Fact]
    public void Render_WithRequest_ContainsInnerDiagram()
    {
        var cut = Render<BackgammonPlayEntry>(p => p
            .Add(c => c.Request, StandardRequest()));

        Assert.Contains("<svg", cut.Markup);
        Assert.Contains("bg-play-entry", cut.Markup);
    }

    [Fact]
    public void Render_WithNullRequest_RendersEmpty()
    {
        var cut = Render<BackgammonPlayEntry>(p => p.Add(c => c.Request, null));
        Assert.Equal(string.Empty, cut.Markup.Trim());
    }

    [Fact]
    public void AdditionalAttributes_AreSplattedOnOuterDiv()
    {
        var cut = Render<BackgammonPlayEntry>(p => p
            .Add(c => c.Request, StandardRequest())
            .AddUnmatched("data-testid", "play-entry-1"));

        Assert.Contains("data-testid=\"play-entry-1\"", cut.Markup);
    }

    // -----------------------------------------------------------------------
    //  Cube decision guard
    // -----------------------------------------------------------------------

    [Fact]
    public void CubeDecision_ThrowsNotImplemented()
    {
        var cubeRequest = new DiagramRequest.Builder
        {
            Mop = StandardMop(),
            IsCube = true,
            Dice = [0, 0],
            OnRollName = "Player",
            OpponentName = "Opponent",
            CubeSize = 1,
            CubeOwner = CubeOwner.Centered,
        }.Build();

        Assert.Throws<NotImplementedException>(() =>
            Render<BackgammonPlayEntry>(p => p.Add(c => c.Request, cubeRequest)));
    }

    // -----------------------------------------------------------------------
    //  Legal completion
    // -----------------------------------------------------------------------

    [Fact]
    public async Task LegalClickSequence_FiresOnPlayCompletedOnce()
    {
        var request = BearOffOneRequest();
        Play? completed = null;
        var fireCount = 0;

        var cut = Render<BackgammonPlayEntry>(p => p
            .Add(c => c.Request, request)
            .Add(c => c.OnPlayCompleted, (Play play) => { completed = play; fireCount++; }));

        await ClickRectAsync(cut, RectIndexForPoint(request, 1));
        await ClickRectAsync(cut, RectIndexForTray(request));

        Assert.Equal(1, fireCount);
        Assert.NotNull(completed);
        Assert.True(completed!.Value.Count >= 1);
    }

    [Fact]
    public async Task IllegalClick_DoesNotFireCompletion()
    {
        var request = StandardRequest(3, 1);
        var fired = false;

        var cut = Render<BackgammonPlayEntry>(p => p
            .Add(c => c.Request, request)
            .Add(c => c.OnPlayCompleted, (Play _) => { fired = true; }));

        // Point 23 is empty in the standard starting position — illegal source.
        await ClickRectAsync(cut, RectIndexForPoint(request, 23));

        Assert.False(fired);
    }

    [Fact]
    public async Task ClicksAfterCompletion_AreNoOps()
    {
        var request = BearOffOneRequest();
        var fireCount = 0;

        var cut = Render<BackgammonPlayEntry>(p => p
            .Add(c => c.Request, request)
            .Add(c => c.OnPlayCompleted, (Play _) => { fireCount++; }));

        await ClickRectAsync(cut, RectIndexForPoint(request, 1));
        await ClickRectAsync(cut, RectIndexForTray(request));
        Assert.Equal(1, fireCount);

        // Post-completion the rendered Mop (no on-roll checkers) reshapes which
        // overlay rects exist, so the original tray index may no longer be
        // valid. Re-fetch and click whatever rects survive — the contract under
        // test is "no further OnPlayCompleted fires," which must hold for any
        // click target.
        var rectsPost = cut.FindAll("rect[fill='transparent'][pointer-events='all']");
        Assert.NotEmpty(rectsPost);
        await rectsPost[0].ClickAsync(new MouseEventArgs());
        Assert.Equal(1, fireCount);
    }

    // -----------------------------------------------------------------------
    //  Undo — verified end-to-end by replaying the same play after revert
    // -----------------------------------------------------------------------

    [Fact]
    public async Task UndoLast_AllowsReplayingTheSamePlay()
    {
        var request = BearOffOneRequest();
        var fireCount = 0;

        var cut = Render<BackgammonPlayEntry>(p => p
            .Add(c => c.Request, request)
            .Add(c => c.OnPlayCompleted, (Play _) => { fireCount++; }));

        await ClickRectAsync(cut, RectIndexForPoint(request, 1));
        await ClickRectAsync(cut, RectIndexForTray(request));
        Assert.Equal(1, fireCount);

        await cut.InvokeAsync(() => cut.Instance.UndoLast());

        // State is back at "play in progress with 0 moves committed."
        await ClickRectAsync(cut, RectIndexForPoint(request, 1));
        await ClickRectAsync(cut, RectIndexForTray(request));
        Assert.Equal(2, fireCount);
    }

    [Fact]
    public async Task UndoAll_RestoresInitialState()
    {
        var request = BearOffOneRequest();
        var fireCount = 0;

        var cut = Render<BackgammonPlayEntry>(p => p
            .Add(c => c.Request, request)
            .Add(c => c.OnPlayCompleted, (Play _) => { fireCount++; }));

        await ClickRectAsync(cut, RectIndexForPoint(request, 1));
        await ClickRectAsync(cut, RectIndexForTray(request));
        Assert.Equal(1, fireCount);

        await cut.InvokeAsync(() => cut.Instance.UndoAll());

        await ClickRectAsync(cut, RectIndexForPoint(request, 1));
        await ClickRectAsync(cut, RectIndexForTray(request));
        Assert.Equal(2, fireCount);
    }

    [Fact]
    public async Task UndoLast_OnNullRequest_IsNoOp()
    {
        var cut = Render<BackgammonPlayEntry>(p => p.Add(c => c.Request, null));
        // Must not throw; nothing to revert. No StateHasChanged fires either
        // because _state is null, so no Dispatcher hop is needed.
        await cut.InvokeAsync(() => cut.Instance.UndoLast());
        await cut.InvokeAsync(() => cut.Instance.UndoAll());
    }

    // -----------------------------------------------------------------------
    //  Reset semantics — value equality on (Mop, Dice)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DifferentMop_ResetsState()
    {
        var requestA = BearOffOneRequest();
        var fireCount = 0;

        var cut = Render<BackgammonPlayEntry>(p => p
            .Add(c => c.Request, requestA)
            .Add(c => c.OnPlayCompleted, (Play _) => { fireCount++; }));

        // Click the source on requestA, leaving state with a selected source.
        await ClickRectAsync(cut, RectIndexForPoint(requestA, 1));

        // Swap to a different problem (different Mop). State must reset — the
        // earlier source-selection should not survive into the new problem.
        // Use a position whose only legal play is a single bear-off so the
        // expected click sequence on requestB is deterministically 2 clicks.
        var differentMop = new int[26];
        differentMop[3] = 1;
        var requestB = new DiagramRequest.Builder
        {
            Mop = differentMop,
            OnRollName = "Player",
            OpponentName = "Opponent",
            Dice = [3, 3],
            CubeSize = 1,
            CubeOwner = CubeOwner.Centered,
        }.Build();

        cut.Render(p => p
            .Add(c => c.Request, requestB)
            .Add(c => c.OnPlayCompleted, (Play _) => { fireCount++; }));

        // Clicking the tray immediately on requestB, *without* first selecting
        // the new source, must NOT complete a play — proving the source
        // selection from requestA did not carry over.
        await ClickRectAsync(cut, RectIndexForTray(requestB));
        Assert.Equal(0, fireCount);

        // The proper sequence on requestB still works: select source, then tray.
        await ClickRectAsync(cut, RectIndexForPoint(requestB, 3));
        await ClickRectAsync(cut, RectIndexForTray(requestB));
        Assert.Equal(1, fireCount);
    }

    [Fact]
    public async Task DifferentDice_ResetsState()
    {
        // Same Mop, different dice — a different problem.
        var m = new int[26];
        m[1] = 1;
        var dice11 = new DiagramRequest.Builder
        {
            Mop = m, Dice = [1, 1],
            OnRollName = "P", OpponentName = "O",
            CubeSize = 1, CubeOwner = CubeOwner.Centered,
        }.Build();
        var dice22 = new DiagramRequest.Builder
        {
            Mop = m, Dice = [2, 2],
            OnRollName = "P", OpponentName = "O",
            CubeSize = 1, CubeOwner = CubeOwner.Centered,
        }.Build();

        var fireCount = 0;
        var cut = Render<BackgammonPlayEntry>(p => p
            .Add(c => c.Request, dice11)
            .Add(c => c.OnPlayCompleted, (Play _) => { fireCount++; }));

        await ClickRectAsync(cut, RectIndexForPoint(dice11, 1));

        cut.Render(p => p
            .Add(c => c.Request, dice22)
            .Add(c => c.OnPlayCompleted, (Play _) => { fireCount++; }));

        // Clicking tray without re-selecting source must not complete.
        await ClickRectAsync(cut, RectIndexForTray(dice22));
        Assert.Equal(0, fireCount);
    }

    // -----------------------------------------------------------------------
    //  Dice click — complete → submit signal; incomplete → display-only swap
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CompletePlay_DiceClick_FiresOnSubmitRequested()
    {
        var request = BearOffOneRequest();
        var submitCount = 0;

        var cut = Render<BackgammonPlayEntry>(p => p
            .Add(c => c.Request, request)
            .Add(c => c.OnSubmitRequested, () => { submitCount++; }));

        // Drive the play to completion (1/off).
        await ClickRectAsync(cut, RectIndexForPoint(request, 1));
        await ClickRectAsync(cut, RectIndexForTray(request));

        // Dice click on a complete play signals submit intent.
        await ClickDiceAsync(cut);
        Assert.Equal(1, submitCount);
    }

    [Fact]
    public async Task IncompletePlay_DiceClick_SwapsDiceDisplay_DoesNotSubmit()
    {
        var request = StandardRequest(3, 1);  // non-doubles, incomplete at start
        var submitCount = 0;

        var cut = Render<BackgammonPlayEntry>(p => p
            .Add(c => c.Request, request)
            .Add(c => c.OnSubmitRequested, () => { submitCount++; }));

        // The diagram's "to play" label reflects the rendered dice order. (Full
        // markup comparison is brittle — Blazor reassigns blazor:onclick handler
        // IDs after every event — so assert on the stable rendered text instead.)
        Assert.Contains("3-1 to play", cut.Markup);

        await ClickDiceAsync(cut);

        // Display-only reorder: 3-1 → 1-3. The model is untouched, so no submit.
        Assert.Contains("1-3 to play", cut.Markup);
        Assert.DoesNotContain("3-1 to play", cut.Markup);
        Assert.Equal(0, submitCount);
    }

    [Fact]
    public async Task DoublesIncomplete_DiceClick_IsNoOp_DoesNotSubmit()
    {
        var request = StandardRequest(2, 2);  // doubles
        var submitCount = 0;

        var cut = Render<BackgammonPlayEntry>(p => p
            .Add(c => c.Request, request)
            .Add(c => c.OnSubmitRequested, () => { submitCount++; }));

        Assert.Contains("2-2 to play", cut.Markup);

        await ClickDiceAsync(cut);

        // Reversing equal dice changes nothing — dice order holds, no submit.
        Assert.Contains("2-2 to play", cut.Markup);
        Assert.Equal(0, submitCount);
    }

    [Fact]
    public async Task NewProblem_ResetsDiceSwap()
    {
        var requestA = StandardRequest(3, 1);
        var requestB = StandardRequest(5, 2);  // different dice ⇒ different problem

        var cut = Render<BackgammonPlayEntry>(p => p
            .Add(c => c.Request, requestA)
            .Add(c => c.OnSubmitRequested, () => { }));

        // Swap A's dice display: 3-1 → 1-3.
        await ClickDiceAsync(cut);
        Assert.Contains("1-3 to play", cut.Markup);

        // Switch to the new problem B. The swap must reset, so B renders in its
        // natural order (5-2), not a leaked-swap (2-5) variant.
        cut.Render(p => p
            .Add(c => c.Request, requestB)
            .Add(c => c.OnSubmitRequested, () => { }));

        Assert.Contains("5-2 to play", cut.Markup);
        Assert.DoesNotContain("2-5 to play", cut.Markup);
    }

    [Fact]
    public async Task SameMopAndDice_DoesNotResetState()
    {
        // Two distinct DiagramRequest instances with byte-identical (Mop, Dice).
        // The component's reset key is value equality, not reference equality —
        // mid-click state must survive a parameter re-set.
        var requestA = BearOffOneRequest();
        var requestB = BearOffOneRequest();

        Assert.NotSame(requestA, requestB);

        var fireCount = 0;
        var cut = Render<BackgammonPlayEntry>(p => p
            .Add(c => c.Request, requestA)
            .Add(c => c.OnPlayCompleted, (Play _) => { fireCount++; }));

        // Select source under requestA.
        await ClickRectAsync(cut, RectIndexForPoint(requestA, 1));

        // Re-render with requestB. Because (Mop, Dice) match, state survives.
        cut.Render(p => p
            .Add(c => c.Request, requestB)
            .Add(c => c.OnPlayCompleted, (Play _) => { fireCount++; }));

        // The previously-selected source is still active; clicking the tray
        // should commit and complete the play.
        await ClickRectAsync(cut, RectIndexForTray(requestB));
        Assert.Equal(1, fireCount);
    }
}
