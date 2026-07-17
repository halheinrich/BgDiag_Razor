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
    /// One on-roll checker on the 1-pt with dice (1,1). The only legal play is
    /// 1/off — a single move that completes in one click. Used for single-click
    /// completion and undo coverage.
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

    /// <summary>
    /// Two on-roll checkers, on the 4-pt and 2-pt, with non-doubles dice (4,2).
    /// Both checkers are home, so two distinct maximal plays exist:
    ///   P1 = 4/off (die 4) + 2/off (die 2) — bears off both.
    ///   P2 = 4/2  (die 2) + 2/off (die 4, overshoot) — bears off one, leaves one on 2.
    /// Because the two plays reach different final boards, the die a one-click
    /// advance from the 4-pt consumes is observable in the completed play — which
    /// is exactly what the leftmost-die-preference tests assert.
    /// </summary>
    private static DiagramRequest BearOffPairRequest()
    {
        var m = new int[26];
        m[4] = 1;
        m[2] = 1;
        return new DiagramRequest.Builder
        {
            Mop = m,
            OnRollName = "Player",
            OpponentName = "Opponent",
            Dice = [4, 2],
            CubeSize = 1,
            CubeOwner = CubeOwner.Centered,
        }.Build();
    }

    /// <summary>
    /// One on-roll checker on the bar with dice (6,5) and an otherwise empty
    /// board, so entry is unobstructed. Clicking the bar enters from 25.
    /// </summary>
    private static DiagramRequest BarEntryRequest()
    {
        var m = new int[26];
        m[25] = 1;  // on-roll checker on the bar
        return new DiagramRequest.Builder
        {
            Mop = m,
            OnRollName = "Player",
            OpponentName = "Opponent",
            Dice = [6, 5],
            CubeSize = 1,
            CubeOwner = CubeOwner.Centered,
        }.Build();
    }

    // -----------------------------------------------------------------------
    //  Click helpers — translate a click target (point / bar) to the matching
    //  transparent overlay rect rendered by the inner diagram. Emission order is
    //  the Points dict (1..24), then bar, then optional tray, then dice.
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

    /// <summary>The bar rect follows the 24 point rects.</summary>
    private static int RectIndexForBar(DiagramRequest req)
    {
        var regions = DiagramRenderer.GetHitRegions(req, new DiagramOptions());
        return regions.Points.Count;
    }

    /// <summary>The on-roll tray rect follows the points and bar.</summary>
    private static int RectIndexForTray(DiagramRequest req)
    {
        var regions = DiagramRenderer.GetHitRegions(req, new DiagramOptions());
        if (regions.OnRollTray is null)
            throw new InvalidOperationException("Request has no OnRollTray region.");
        return regions.Points.Count + 1;
    }

    private static Task ClickRectAsync(
        IRenderedComponent<BackgammonPlayEntry> cut, int rectIndex)
    {
        var rects = cut.FindAll("rect[fill='transparent'][pointer-events='all']");
        return rects[rectIndex].ClickAsync(new MouseEventArgs());
    }

    /// <summary>
    /// Clicks the dice overlay rect. The diagram emits the dice rect last, and a
    /// play always has a dice region, so the final transparent rect is the dice.
    /// </summary>
    private static Task ClickDiceAsync(IRenderedComponent<BackgammonPlayEntry> cut)
    {
        var rects = cut.FindAll("rect[fill='transparent'][pointer-events='all']");
        return rects[^1].ClickAsync(new MouseEventArgs());
    }

    private static bool PlayContains(Play play, int frPt, int toPt)
    {
        for (int i = 0; i < play.Count; i++)
            if (play[i].FrPt == frPt && play[i].ToPt == toPt) return true;
        return false;
    }

    private static bool PlayHasSource(Play play, int frPt)
    {
        for (int i = 0; i < play.Count; i++)
            if (play[i].FrPt == frPt) return true;
        return false;
    }

    /// <summary>
    /// Count of moves landing on <paramref name="point"/>, regardless of the
    /// <see cref="Move.ToPt"/> hit sign (a hit is encoded as a negative ToPt). A
    /// completed make lands two checkers on the point, so this returns 2.
    /// </summary>
    private static int PlayLandingsOn(Play play, int point)
    {
        int n = 0;
        for (int i = 0; i < play.Count; i++)
            if (Math.Abs(play[i].ToPt) == point) n++;
        return n;
    }

    /// <summary>True if any move hits on <paramref name="point"/> (ToPt == -point).</summary>
    private static bool PlayHits(Play play, int point)
    {
        for (int i = 0; i < play.Count; i++)
            if (play[i].ToPt == -point) return true;
        return false;
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

    // -----------------------------------------------------------------------
    //  Bounded-height contract — board slot structure
    // -----------------------------------------------------------------------

    [Fact]
    public void BoardSlot_WrapsDiagram()
    {
        // The bounded-height contract's structural half: the diagram sits
        // inside .bg-board-slot (the shrinkable flex row), a direct child of
        // the wrapper, so a consumer-bounded height letterboxes the board.
        // The layout half lives in the scoped CSS and was validated
        // empirically; bUnit pins the markup structure it needs.
        var cut = Render<BackgammonPlayEntry>(p => p
            .Add(c => c.Request, StandardRequest()));

        var slot = cut.Find(".bg-play-entry > .bg-board-slot");
        Assert.NotNull(slot.QuerySelector(".bg-diagram"));
    }

    [Fact]
    public void BoardSlot_CarriesNoInlineSizing()
    {
        // Unbounded no-op guard: the producer's slot is class-only — no inline
        // style that would impose a bound of its own. Whether the board
        // letterboxes is decided solely by the consumer bounding the wrapper.
        var cut = Render<BackgammonPlayEntry>(p => p
            .Add(c => c.Request, StandardRequest()));

        var slot = cut.Find(".bg-board-slot");
        Assert.Null(slot.GetAttribute("style"));
    }

    [Fact]
    public void AdditionalAttributes_AreSplattedOnOuterDiv()
    {
        var cut = Render<BackgammonPlayEntry>(p => p
            .Add(c => c.Request, StandardRequest())
            .AddUnmatched("data-testid", "play-entry-1"));

        Assert.Contains("data-testid=\"play-entry-1\"", cut.Markup);
    }

    [Fact]
    public void Overlay_Unset_IsCompleteNoOp()
    {
        var cut = Render<BackgammonPlayEntry>(p => p
            .Add(c => c.Request, StandardRequest()));

        Assert.DoesNotContain("bg-diagram-overlay", cut.Markup);
    }

    [Fact]
    public void Overlay_Supplied_ForwardsToInnerDiagram()
    {
        var cut = Render<BackgammonPlayEntry>(p => p
            .Add(c => c.Request, StandardRequest())
            .Add(c => c.Overlay, builder => builder.AddMarkupContent(0, "<div class=\"my-badge\">XGID</div>")));

        var overlay = cut.Find(".bg-diagram-overlay");
        Assert.Contains("my-badge", overlay.InnerHtml);
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
    //  One-click source-advance
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SingleClick_FiresOnPlayCompleted()
    {
        // BearOffOne's only play (1/off) is a single move — one click completes it.
        var request = BearOffOneRequest();
        Play? completed = null;
        var fireCount = 0;

        var cut = Render<BackgammonPlayEntry>(p => p
            .Add(c => c.Request, request)
            .Add(c => c.OnPlayCompleted, (Play play) => { completed = play; fireCount++; }));

        await ClickRectAsync(cut, RectIndexForPoint(request, 1));

        Assert.Equal(1, fireCount);
        Assert.NotNull(completed);
        Assert.True(completed!.Value.Count >= 1);
    }

    [Fact]
    public async Task FullPlay_ViaSuccessiveSingleClicks_FiresCompletedOnce()
    {
        // Two single clicks (4-pt then 2-pt) assemble the whole two-move play.
        var request = BearOffPairRequest();
        Play? completed = null;
        var fireCount = 0;

        var cut = Render<BackgammonPlayEntry>(p => p
            .Add(c => c.Request, request)
            .Add(c => c.OnPlayCompleted, (Play play) => { completed = play; fireCount++; }));

        await ClickRectAsync(cut, RectIndexForPoint(request, 4));
        Assert.Equal(0, fireCount);  // first click commits one move, play incomplete

        await ClickRectAsync(cut, RectIndexForPoint(request, 2));
        Assert.Equal(1, fireCount);
        Assert.NotNull(completed);
        Assert.Equal(2, completed!.Value.Count);
    }

    [Fact]
    public async Task ClickSource_AdvancesByLeftmostDie()
    {
        // No swap ⇒ preference is [4, 2]. From the 4-pt the leftmost die (4) is
        // legal, so the click bears off directly: 4/off (Move 4→0), not 4/2.
        var request = BearOffPairRequest();
        Play? completed = null;

        var cut = Render<BackgammonPlayEntry>(p => p
            .Add(c => c.Request, request)
            .Add(c => c.OnPlayCompleted, (Play play) => { completed = play; }));

        await ClickRectAsync(cut, RectIndexForPoint(request, 4));
        await ClickRectAsync(cut, RectIndexForPoint(request, 2));

        Assert.NotNull(completed);
        Assert.True(PlayContains(completed!.Value, frPt: 4, toPt: 0),
            "Leftmost die (4) should have borne off directly from the 4-pt (4/off).");
    }

    [Fact]
    public async Task AfterDiceSwap_ClickSource_AdvancesByOtherDie()
    {
        // A dice click on the incomplete play swaps the display order to 2-4, so
        // the preference becomes [2, 4]. Now the same 4-pt click consumes die 2:
        // 4/2 (Move 4→2), not 4/off.
        var request = BearOffPairRequest();
        Play? completed = null;

        var cut = Render<BackgammonPlayEntry>(p => p
            .Add(c => c.Request, request)
            .Add(c => c.OnPlayCompleted, (Play play) => { completed = play; })
            .Add(c => c.OnSubmitRequested, () => { }));

        await ClickDiceAsync(cut);  // swap → preference [2, 4]
        await ClickRectAsync(cut, RectIndexForPoint(request, 4));
        await ClickRectAsync(cut, RectIndexForPoint(request, 2));

        Assert.NotNull(completed);
        Assert.True(PlayContains(completed!.Value, frPt: 4, toPt: 2),
            "After the swap, die 2 should move 4/2 from the 4-pt.");
    }

    [Fact]
    public async Task LeftmostDieIllegalFromPoint_FallsBackToOtherDie()
    {
        // Preference [4, 2]. From the 2-pt die 4 is illegal (overshoot while the
        // 4-pt is still occupied), so the click falls back to die 2 (2/off). If
        // no fallback occurred the 2-pt click would be a no-op and the play would
        // never complete — so a completion here proves the fallback.
        var request = BearOffPairRequest();
        Play? completed = null;
        var fireCount = 0;

        var cut = Render<BackgammonPlayEntry>(p => p
            .Add(c => c.Request, request)
            .Add(c => c.OnPlayCompleted, (Play play) => { completed = play; fireCount++; }));

        await ClickRectAsync(cut, RectIndexForPoint(request, 2));  // die 4 illegal → falls back to die 2
        await ClickRectAsync(cut, RectIndexForPoint(request, 4));

        Assert.Equal(1, fireCount);
        Assert.True(PlayContains(completed!.Value, frPt: 2, toPt: 0),
            "The 2-pt click should have borne off via die 2 (2/off).");
    }

    [Fact]
    public async Task BarClick_EntersFromBar()
    {
        // A checker on the bar enters via a single bar click (FrPt 25): the
        // leftmost die (6) enters bar/19, then the 19-pt click plays 19/14 to
        // finish. (The completed Play is the engine's canonical representative
        // for the final-board signature, so the exact entry point isn't readable
        // off it — leftmost-die preference is covered by the bear-off test. Here
        // we assert only that the bar click produced an entry move from 25.)
        var request = BarEntryRequest();
        Play? completed = null;
        var fireCount = 0;

        var cut = Render<BackgammonPlayEntry>(p => p
            .Add(c => c.Request, request)
            .Add(c => c.OnPlayCompleted, (Play play) => { completed = play; fireCount++; }));

        await ClickRectAsync(cut, RectIndexForBar(request));
        await ClickRectAsync(cut, RectIndexForPoint(request, 19));

        Assert.Equal(1, fireCount);
        Assert.True(PlayHasSource(completed!.Value, frPt: 25),
            "The bar click should commit an entry move from the bar (FrPt 25).");
    }

    [Fact]
    public async Task ClickEmptyUnreachablePoint_IsNoOp()
    {
        // E2: a destination click tries make-the-point when advance is illegal. The
        // 1-point in the standard start is empty, unmakeable, AND unreachable on a
        // 3-1 — the nearest own checkers (6 and 8) are both more than 4 pips away, and
        // points 2-5 hold no own checker, so no single move and no combined 3-then-1
        // path lands on 1. (The 4-point, by contrast, is reachable via the combined
        // 8/7 7/4, so it is not a no-op.) Both advance and make are illegal, so the
        // click is a true no-op. We prove state is intact by then making the 5-point
        // (8/5 6/5), which still completes.
        var request = StandardRequest(3, 1);
        var fireCount = 0;

        var cut = Render<BackgammonPlayEntry>(p => p
            .Add(c => c.Request, request)
            .Add(c => c.OnPlayCompleted, (Play _) => { fireCount++; }));

        await ClickRectAsync(cut, RectIndexForPoint(request, 1));
        Assert.Equal(0, fireCount);  // neither make nor move-one has a candidate

        await ClickRectAsync(cut, RectIndexForPoint(request, 5));  // make 5-pt, unaffected
        Assert.Equal(1, fireCount);
    }

    // -----------------------------------------------------------------------
    //  One-click make-the-point (destination click; advance-then-make)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ClickEmptyPoint_NonDoublesMake_FiresCompleted()
    {
        // Standard start, dice (3,1): clicking the empty 5-point lands two own
        // checkers on it — 8/5 (die 3) + 6/5 (die 1). Both dice are consumed, so the
        // make completes the play. Advance is illegal on the empty point, so the
        // click falls through to make.
        var request = StandardRequest(3, 1);
        Play? completed = null;
        var fireCount = 0;

        var cut = Render<BackgammonPlayEntry>(p => p
            .Add(c => c.Request, request)
            .Add(c => c.OnPlayCompleted, (Play play) => { completed = play; fireCount++; }));

        await ClickRectAsync(cut, RectIndexForPoint(request, 5));

        Assert.Equal(1, fireCount);
        Assert.NotNull(completed);
        Assert.True(PlayContains(completed!.Value, frPt: 8, toPt: 5), "8/5 should be part of the make.");
        Assert.True(PlayContains(completed!.Value, frPt: 6, toPt: 5), "6/5 should be part of the make.");
    }

    [Fact]
    public async Task ClickOpponentBlot_MakeAndHit_FiresCompleted()
    {
        // Own on 8 and 6, an opponent blot on 5, dice (3,1). Clicking the 5-point
        // makes it over the blot: 8/5* hits, 6/5 covers. Two checkers land on 5 and
        // one of the arrivals is a hit (negative ToPt). Both dice ⇒ completes.
        var m = new int[26];
        m[8] = 1;
        m[6] = 1;
        m[5] = -1;  // opponent blot
        var request = new DiagramRequest.Builder
        {
            Mop = m,
            OnRollName = "Player",
            OpponentName = "Opponent",
            Dice = [3, 1],
            CubeSize = 1,
            CubeOwner = CubeOwner.Centered,
        }.Build();
        Play? completed = null;
        var fireCount = 0;

        var cut = Render<BackgammonPlayEntry>(p => p
            .Add(c => c.Request, request)
            .Add(c => c.OnPlayCompleted, (Play play) => { completed = play; fireCount++; }));

        await ClickRectAsync(cut, RectIndexForPoint(request, 5));

        Assert.Equal(1, fireCount);
        Assert.NotNull(completed);
        Assert.Equal(2, PlayLandingsOn(completed!.Value, point: 5));  // point made (two checkers)
        Assert.True(PlayHits(completed!.Value, point: 5), "An arrival onto the blot should be a hit.");
    }

    [Fact]
    public async Task ClickOpponentBlot_ReachableOnlyViaCombinedPath_HitsAndFiresCompleted()
    {
        // One own checker on 18, an opponent blot on 10, dice (6,2). Point 10 is
        // unmakeable (a lone checker) and has no direct single-die landing (18 is 8
        // pips from 10), so the land-one fallback walks the combined path to it —
        // 18/16 16/10* (equivalently 18/12 12/10*, same final board) — hitting the
        // blot. Both dice are consumed, so the play completes and OnPlayCompleted
        // fires. This is the end-to-end of the TryMakePoint(int) follow-up: a
        // destination click that lands via a combined multi-die path.
        var m = new int[26];
        m[18] = 1;
        m[10] = -1;  // opponent blot
        var request = new DiagramRequest.Builder
        {
            Mop = m,
            OnRollName = "Player",
            OpponentName = "Opponent",
            Dice = [6, 2],
            CubeSize = 1,
            CubeOwner = CubeOwner.Centered,
        }.Build();
        Play? completed = null;
        var fireCount = 0;

        var cut = Render<BackgammonPlayEntry>(p => p
            .Add(c => c.Request, request)
            .Add(c => c.OnPlayCompleted, (Play play) => { completed = play; fireCount++; }));

        await ClickRectAsync(cut, RectIndexForPoint(request, 10));

        Assert.Equal(1, fireCount);
        Assert.NotNull(completed);
        Assert.Equal(1, PlayLandingsOn(completed!.Value, point: 10));  // one checker lands on 10
        Assert.True(PlayHits(completed!.Value, point: 10), "The arrival onto the blot should be a hit.");
    }

    [Fact]
    public async Task DoublesMake_LeavesPlayInProgress_FollowUpClickCompletes()
    {
        // Own on 12 and 14, dice (2,2). Making the 10-point is a three-sub-move make
        // (12/10 + 14/12/10) that leaves one 2 unplayed — so the make commits but the
        // play stays in-progress (OnPlayCompleted must NOT fire). A follow-up click on
        // the now-own 10-point advances 10/8 with the last die and completes.
        var m = new int[26];
        m[12] = 1;
        m[14] = 1;
        var request = new DiagramRequest.Builder
        {
            Mop = m,
            OnRollName = "Player",
            OpponentName = "Opponent",
            Dice = [2, 2],
            CubeSize = 1,
            CubeOwner = CubeOwner.Centered,
        }.Build();
        var fireCount = 0;

        var cut = Render<BackgammonPlayEntry>(p => p
            .Add(c => c.Request, request)
            .Add(c => c.OnPlayCompleted, (Play _) => { fireCount++; }));

        await ClickRectAsync(cut, RectIndexForPoint(request, 10));  // doubles make, dice left
        Assert.Equal(0, fireCount);  // play still in-progress

        await ClickRectAsync(cut, RectIndexForPoint(request, 10));  // advance 10/8, last die
        Assert.Equal(1, fireCount);  // now complete
    }

    [Fact]
    public async Task ClickUnmakeablePoint_OneCheckerLands_MovesOne()
    {
        // A single checker on 8, dice (3,1), click the empty 5-point. The point is
        // unmakeable (one checker can't put two on it), so the move-one fallback
        // lands the lone checker via die 3 (8/5) — leaving the play in-progress. A
        // follow-up click on the now-own 5-point advances 5/4 (die 1) and completes,
        // proving the move-one actually landed a checker on 5.
        var m = new int[26];
        m[8] = 1;
        var request = new DiagramRequest.Builder
        {
            Mop = m,
            OnRollName = "Player",
            OpponentName = "Opponent",
            Dice = [3, 1],
            CubeSize = 1,
            CubeOwner = CubeOwner.Centered,
        }.Build();
        var fireCount = 0;

        var cut = Render<BackgammonPlayEntry>(p => p
            .Add(c => c.Request, request)
            .Add(c => c.OnPlayCompleted, (Play _) => { fireCount++; }));

        await ClickRectAsync(cut, RectIndexForPoint(request, 5));  // move-one 8/5
        Assert.Equal(0, fireCount);  // in-progress (one die left)

        await ClickRectAsync(cut, RectIndexForPoint(request, 5));  // advance 5/4, completes
        Assert.Equal(1, fireCount);
    }

    [Fact]
    public async Task ClickOwnOccupiedSource_StillAdvances_MakeNotEngaged()
    {
        // Two own checkers on 8, dice (3,1). The 8-point holds own checkers, so the
        // click advances (E1) and never engages make (the producer rejects a make on
        // an own-occupied point). Two clicks play 8/5 (die 3) then 8/7 (die 1) — two
        // distinct single advances off the source — and the completed play carries
        // both. If the click had wrongly routed to make, the own-occupied guard would
        // make it a no-op and the play would never complete.
        var m = new int[26];
        m[8] = 2;
        var request = new DiagramRequest.Builder
        {
            Mop = m,
            OnRollName = "Player",
            OpponentName = "Opponent",
            Dice = [3, 1],
            CubeSize = 1,
            CubeOwner = CubeOwner.Centered,
        }.Build();
        Play? completed = null;
        var fireCount = 0;

        var cut = Render<BackgammonPlayEntry>(p => p
            .Add(c => c.Request, request)
            .Add(c => c.OnPlayCompleted, (Play play) => { completed = play; fireCount++; }));

        await ClickRectAsync(cut, RectIndexForPoint(request, 8));  // 8/5 (die 3)
        await ClickRectAsync(cut, RectIndexForPoint(request, 8));  // 8/7 (die 1)

        Assert.Equal(1, fireCount);
        Assert.True(PlayContains(completed!.Value, frPt: 8, toPt: 5), "First advance 8/5.");
        Assert.True(PlayContains(completed!.Value, frPt: 8, toPt: 7), "Second advance 8/7.");
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
        Assert.Equal(1, fireCount);

        // Post-completion the rendered Mop reshapes which overlay rects exist;
        // the contract under test is "no further OnPlayCompleted fires," which
        // must hold for any surviving click target.
        var rectsPost = cut.FindAll("rect[fill='transparent'][pointer-events='all']");
        Assert.NotEmpty(rectsPost);
        await rectsPost[0].ClickAsync(new MouseEventArgs());
        Assert.Equal(1, fireCount);
    }

    // -----------------------------------------------------------------------
    //  Tray click — bear-off-max shortcut
    // -----------------------------------------------------------------------

    [Fact]
    public async Task TrayClick_UniqueMaxBearOff_CompletesPlay()
    {
        // BearOffPair's deepest completion bears off BOTH checkers (4/off, 2/off)
        // and is the unique maximum, so one tray click completes the turn.
        var request = BearOffPairRequest();
        Play? completed = null;
        var fireCount = 0;

        var cut = Render<BackgammonPlayEntry>(p => p
            .Add(c => c.Request, request)
            .Add(c => c.OnPlayCompleted, (Play play) => { completed = play; fireCount++; }));

        await ClickRectAsync(cut, RectIndexForTray(request));

        Assert.Equal(1, fireCount);
        Assert.NotNull(completed);
        Assert.Equal(2, completed!.Value.Count);  // both checkers borne off
    }

    [Fact]
    public async Task TrayClick_AmbiguousMaxBearOff_IsNoOp()
    {
        // Checkers on the 6-pt and 3-pt with dice (6,2). Exactly one checker can
        // bear off, but two distinct finals tie at the maximum:
        //   6/off (die 6) + 3/1 (die 2)            → {1}
        //   6/4  (die 2) + 4/off (die 6, overshoot) → {3}
        // The tie makes max bear-off ambiguous, so the tray click is a no-op and
        // the user must bear off via individual home-point clicks.
        var m = new int[26];
        m[6] = 1;
        m[3] = 1;
        var request = new DiagramRequest.Builder
        {
            Mop = m,
            OnRollName = "Player",
            OpponentName = "Opponent",
            Dice = [6, 2],
            CubeSize = 1,
            CubeOwner = CubeOwner.Centered,
        }.Build();
        var fired = false;

        var cut = Render<BackgammonPlayEntry>(p => p
            .Add(c => c.Request, request)
            .Add(c => c.OnPlayCompleted, (Play _) => { fired = true; }));

        await ClickRectAsync(cut, RectIndexForTray(request));
        Assert.False(fired);

        // State unchanged: the play is still enterable click-by-click. Clicking
        // the 6-pt advances (6/off via die 6), proving the tray was a no-op.
        await ClickRectAsync(cut, RectIndexForPoint(request, 6));
        await ClickRectAsync(cut, RectIndexForPoint(request, 3));
        Assert.True(fired);
    }

    [Fact]
    public async Task TrayClick_DiceCannotBringOutlierHomeAndBearOff_IsNoOp()
    {
        // Checkers on the 8-pt and 6-pt with dice (2,1). The 8-pt outlier doesn't
        // categorically block bear-off — with 6-2 this same shape bears off
        // (8/6, 6/off). It's the specific dice that matter: 2-1 can bring the
        // outlier home (8/6 via the 2) but the leftover 1 can't bear anything off
        // (a 1 only bears off the 1-pt, which is empty). No completion bears off,
        // so TryBearOffMax is a no-op. Two on-board checkers keep the borne-off
        // count in range, so the tray hit-rect is still drawn and the click
        // reaches the handler.
        var m = new int[26];
        m[8] = 1;
        m[6] = 1;
        var request = new DiagramRequest.Builder
        {
            Mop = m,
            OnRollName = "Player",
            OpponentName = "Opponent",
            Dice = [2, 1],
            CubeSize = 1,
            CubeOwner = CubeOwner.Centered,
        }.Build();
        var fired = false;

        var cut = Render<BackgammonPlayEntry>(p => p
            .Add(c => c.Request, request)
            .Add(c => c.OnPlayCompleted, (Play _) => { fired = true; }));

        await ClickRectAsync(cut, RectIndexForTray(request));
        Assert.False(fired);
    }

    [Fact]
    public async Task TrayClick_AfterCompletion_IsNoOp()
    {
        // BearOffOne completes on the single 1-pt click; a subsequent tray click
        // must not fire completion again.
        var request = BearOffOneRequest();
        var fireCount = 0;

        var cut = Render<BackgammonPlayEntry>(p => p
            .Add(c => c.Request, request)
            .Add(c => c.OnPlayCompleted, (Play _) => { fireCount++; }));

        await ClickRectAsync(cut, RectIndexForPoint(request, 1));
        Assert.Equal(1, fireCount);

        await ClickRectAsync(cut, RectIndexForTray(request));
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
        Assert.Equal(1, fireCount);

        await cut.InvokeAsync(() => cut.Instance.UndoLast());

        // State is back at "play in progress with 0 moves committed."
        await ClickRectAsync(cut, RectIndexForPoint(request, 1));
        Assert.Equal(2, fireCount);
    }

    [Fact]
    public async Task UndoAll_RestoresInitialState()
    {
        // A two-move play, undone wholesale, then replayed from scratch.
        var request = BearOffPairRequest();
        var fireCount = 0;

        var cut = Render<BackgammonPlayEntry>(p => p
            .Add(c => c.Request, request)
            .Add(c => c.OnPlayCompleted, (Play _) => { fireCount++; }));

        await ClickRectAsync(cut, RectIndexForPoint(request, 4));
        await ClickRectAsync(cut, RectIndexForPoint(request, 2));
        Assert.Equal(1, fireCount);

        await cut.InvokeAsync(() => cut.Instance.UndoAll());

        await ClickRectAsync(cut, RectIndexForPoint(request, 4));
        await ClickRectAsync(cut, RectIndexForPoint(request, 2));
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
        // Begin a play on A (commit one move), then switch to a different-Mop
        // problem B. B's board only has an on-roll checker on the 1-pt if the
        // state reset — in A's board the 1-pt holds opponent checkers. So a
        // completion from clicking B's 1-pt proves the reset.
        var requestA = StandardRequest(3, 1);
        var fireCount = 0;

        var cut = Render<BackgammonPlayEntry>(p => p
            .Add(c => c.Request, requestA)
            .Add(c => c.OnPlayCompleted, (Play _) => { fireCount++; }));

        await ClickRectAsync(cut, RectIndexForPoint(requestA, 24));  // 24/21, incomplete
        Assert.Equal(0, fireCount);

        var requestB = BearOffOneRequest();  // different Mop ⇒ new problem
        cut.Render(p => p
            .Add(c => c.Request, requestB)
            .Add(c => c.OnPlayCompleted, (Play _) => { fireCount++; }));

        await ClickRectAsync(cut, RectIndexForPoint(requestB, 1));
        Assert.Equal(1, fireCount);
    }

    [Fact]
    public async Task DifferentDice_ResetsState()
    {
        // Same Mop (one checker on the 1-pt), different dice ⇒ new problem. After
        // completing A the checker is borne off; switching dice must rebuild the
        // state (checker back on the 1-pt) so B can complete again. Without a
        // reset the carried-over completed state would make B's click a no-op.
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

        await ClickRectAsync(cut, RectIndexForPoint(dice11, 1));  // 1/off, completes A
        Assert.Equal(1, fireCount);

        cut.Render(p => p
            .Add(c => c.Request, dice22)
            .Add(c => c.OnPlayCompleted, (Play _) => { fireCount++; }));

        await ClickRectAsync(cut, RectIndexForPoint(dice22, 1));  // 1/off, completes B
        Assert.Equal(2, fireCount);
    }

    [Fact]
    public async Task SameMopAndDice_DoesNotResetState()
    {
        // Two distinct DiagramRequest instances with byte-identical (Mop, Dice).
        // The reset key is value equality, not reference equality — mid-play state
        // must survive a parameter re-set.
        var requestA = StandardRequest(3, 1);
        var requestB = StandardRequest(3, 1);

        Assert.NotSame(requestA, requestB);

        var fireCount = 0;
        var cut = Render<BackgammonPlayEntry>(p => p
            .Add(c => c.Request, requestA)
            .Add(c => c.OnPlayCompleted, (Play _) => { fireCount++; }));

        // Commit the die-3 move (24/21) under requestA; play still needs die 1.
        await ClickRectAsync(cut, RectIndexForPoint(requestA, 24));
        Assert.Equal(0, fireCount);

        // Re-render with requestB. Because (Mop, Dice) match, state survives — so
        // only die 1 remains and the 24-pt now holds a single checker.
        cut.Render(p => p
            .Add(c => c.Request, requestB)
            .Add(c => c.OnPlayCompleted, (Play _) => { fireCount++; }));

        // One more 24-pt click consumes die 1 (24/23) and completes the play. If
        // state had reset, this click would instead commit die 3 and stay
        // incomplete (fireCount 0).
        await ClickRectAsync(cut, RectIndexForPoint(requestB, 24));
        Assert.Equal(1, fireCount);
    }

    // -----------------------------------------------------------------------
    //  Dice click — complete → submit signal; incomplete → display-only swap
    //  (#2 behaviour; preserved alongside one-click advance)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CompletePlay_DiceClick_FiresOnSubmitRequested()
    {
        var request = BearOffOneRequest();
        var submitCount = 0;

        var cut = Render<BackgammonPlayEntry>(p => p
            .Add(c => c.Request, request)
            .Add(c => c.OnSubmitRequested, () => { submitCount++; }));

        // One click completes the play (1/off); the dice click then signals submit.
        await ClickRectAsync(cut, RectIndexForPoint(request, 1));
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
}
