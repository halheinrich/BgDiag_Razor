using Bunit;
using BgDiag_Razor.Components;
using BgDataTypes_Lib;
using Microsoft.AspNetCore.Components;

namespace BgDiag_Razor.Tests;

public class BackgammonCubeActionsTests : BunitContext
{
    // -----------------------------------------------------------------------
    //  Fixtures
    // -----------------------------------------------------------------------

    /// <summary>
    /// The four options in render order — mirrors the component's _cubeOptions
    /// table (the four-option bijection onto CubeDecisionPair).
    /// </summary>
    private static readonly (string Label, CubeDecisionPair Pair)[] Options =
    [
        ("No double",   new CubeDecisionPair(CubeAction.NoDouble, CubeAction.Take)),
        ("Double/Take", new CubeDecisionPair(CubeAction.Double,   CubeAction.Take)),
        ("Double/Pass", new CubeDecisionPair(CubeAction.Double,   CubeAction.Pass)),
        ("Too good",    new CubeDecisionPair(CubeAction.NoDouble, CubeAction.Pass)),
    ];

    /// <summary>The four radio inputs, in render order (matches the options table).</summary>
    private static IReadOnlyList<AngleSharp.Dom.IElement> Radios(
        IRenderedComponent<BackgammonCubeActions> cut) =>
        cut.FindAll(".bg-cube-actions input[type=radio]");

    // -----------------------------------------------------------------------
    //  Render
    // -----------------------------------------------------------------------

    [Fact]
    public void Render_ContainsOneRadioGroupWithFourOptions()
    {
        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.ValueChanged, (CubeDecisionPair? _) => { }));

        // One radio group holding the four mutually-exclusive options.
        Assert.Single(cut.FindAll("[role=radiogroup]"));
        Assert.Equal(4, Radios(cut).Count);

        // All four bijection labels are present.
        foreach (var (label, _) in Options)
            Assert.Contains(label, cut.Markup);
    }

    [Fact]
    public void Render_NullValue_NothingSelected()
    {
        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.Value, null)
            .Add(c => c.ValueChanged, (CubeDecisionPair? _) => { }));

        Assert.Empty(cut.FindAll(".bg-cube-action-selected"));
        Assert.All(Radios(cut), r => Assert.False(r.HasAttribute("checked")));
    }

    [Fact]
    public void AdditionalAttributes_AreSplattedOnRootDiv()
    {
        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.ValueChanged, (CubeDecisionPair? _) => { })
            .AddUnmatched("data-testid", "cube-actions-1"));

        var root = cut.Find(".bg-cube-actions");
        Assert.Equal("cube-actions-1", root.GetAttribute("data-testid"));
    }

    // -----------------------------------------------------------------------
    //  Controlled value — the selected pill is whatever Value says
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Value_MarksExactlyTheMatchingOptionSelected(int optionIndex)
    {
        var (label, pair) = Options[optionIndex];

        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.Value, pair)
            .Add(c => c.ValueChanged, (CubeDecisionPair? _) => { }));

        var selected = cut.FindAll(".bg-cube-action.bg-cube-action-selected");
        Assert.Single(selected);
        Assert.Equal(label, selected[0].TextContent.Trim());
        Assert.True(Radios(cut)[optionIndex].HasAttribute("checked"));
    }

    [Fact]
    public void ClearingValue_ClearsTheSelection()
    {
        // The consumer's advance-to-next-problem path: there is no request to
        // key an automatic reset off, so the consumer clears by setting Value
        // back to null.
        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.Value, Options[1].Pair)
            .Add(c => c.ValueChanged, (CubeDecisionPair? _) => { }));
        Assert.Single(cut.FindAll(".bg-cube-action-selected"));

        cut.Render(p => p.Add(c => c.Value, null));

        Assert.Empty(cut.FindAll(".bg-cube-action-selected"));
    }

    [Fact]
    public async Task StrictlyControlled_SelectionDoesNotStickWithoutValueWriteback()
    {
        // A consumer that ignores ValueChanged never updates Value, so the
        // component (which holds no selection state of its own) keeps rendering
        // nothing selected.
        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.ValueChanged, (CubeDecisionPair? _) => { }));

        await Radios(cut)[2].ChangeAsync(new ChangeEventArgs { Value = true });

        Assert.Empty(cut.FindAll(".bg-cube-action-selected"));
        Assert.All(Radios(cut), r => Assert.False(r.HasAttribute("checked")));
    }

    // -----------------------------------------------------------------------
    //  ValueChanged — fires once per selection with the matching pair.
    //  Parameterized over all four options; the index tracks the render order.
    //  Note "Too good" (index 3) maps to (NoDouble, Pass) — don't double, but
    //  the opponent would pass.
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

        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.ValueChanged,
                (CubeDecisionPair? pair) => { received = pair; fireCount++; }));

        await Radios(cut)[radioIndex].ChangeAsync(new ChangeEventArgs { Value = true });

        Assert.Equal(1, fireCount);
        Assert.Equal(new CubeDecisionPair(expectedDoubler, expectedTaker), received);
    }

    // -----------------------------------------------------------------------
    //  Switching — selecting a different radio re-fires with the new pair.
    //  (One radio completes the pair, so there is no half-selected state; the
    //  "does not fire while incomplete" contract is trivially preserved, and
    //  the callback never carries null — radios cannot deselect.)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SwitchingRadio_FiresAgainWithNewPair()
    {
        var received = new List<CubeDecisionPair?>();

        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.ValueChanged,
                (CubeDecisionPair? pair) => received.Add(pair)));

        // Select "Double/Take" (index 1), then switch to "Too good" (index 3).
        await Radios(cut)[1].ChangeAsync(new ChangeEventArgs { Value = true });
        await Radios(cut)[3].ChangeAsync(new ChangeEventArgs { Value = true });

        Assert.Equal(
            new CubeDecisionPair?[]
            {
                new CubeDecisionPair(CubeAction.Double, CubeAction.Take),
                new CubeDecisionPair(CubeAction.NoDouble, CubeAction.Pass),
            },
            received);
    }

    // -----------------------------------------------------------------------
    //  Controlled round trip — the consumer wiring @bind-Value compiles to:
    //  ValueChanged writes the pair back into Value, and the selection renders
    //  from the written-back Value.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ValueWriteback_RoundTrip_SelectsAndSwitches()
    {
        CubeDecisionPair? current = null;

        var cut = Render<BackgammonCubeActions>(p => p
            .Add(c => c.Value, current)
            .Add(c => c.ValueChanged, (CubeDecisionPair? pair) => current = pair));

        // Select "Double/Pass" (index 2); the parent writes the value back.
        await Radios(cut)[2].ChangeAsync(new ChangeEventArgs { Value = true });
        Assert.Equal(Options[2].Pair, current);
        cut.Render(p => p.Add(c => c.Value, current));
        Assert.Equal("Double/Pass",
            cut.Find(".bg-cube-action-selected").TextContent.Trim());

        // Switch to "No double" (index 0); the selection follows the value.
        await Radios(cut)[0].ChangeAsync(new ChangeEventArgs { Value = true });
        Assert.Equal(Options[0].Pair, current);
        cut.Render(p => p.Add(c => c.Value, current));
        var selected = cut.FindAll(".bg-cube-action.bg-cube-action-selected");
        Assert.Single(selected);
        Assert.Equal("No double", selected[0].TextContent.Trim());
    }

    // -----------------------------------------------------------------------
    //  Instance-unique radio group — two rows on one page must not cross-link
    //  their browser-native mutual exclusion.
    // -----------------------------------------------------------------------

    [Fact]
    public void TwoInstances_UseDistinctRadioGroupNames()
    {
        var first = Render<BackgammonCubeActions>(p => p
            .Add(c => c.ValueChanged, (CubeDecisionPair? _) => { }));
        var second = Render<BackgammonCubeActions>(p => p
            .Add(c => c.ValueChanged, (CubeDecisionPair? _) => { }));

        // Within an instance all four radios share one group name...
        var firstNames = Radios(first).Select(r => r.GetAttribute("name")).ToList();
        var secondNames = Radios(second).Select(r => r.GetAttribute("name")).ToList();
        Assert.Single(firstNames.Distinct());
        Assert.Single(secondNames.Distinct());

        // ...and the two instances' group names differ.
        Assert.NotEqual(firstNames[0], secondNames[0]);
    }
}
