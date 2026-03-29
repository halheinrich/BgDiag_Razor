using Bunit;
using BgDiag_Razor.Components;
using BackgammonDiagram_Lib;

namespace BgDiag_Razor.Tests;

public class BackgammonDiagramTests : BunitContext
{
    [Fact]
    public void Render_WithValidRequest_ContainsSvg()
    {
        // Arrange — opening position
        var request = new DiagramRequest
        {
            Mop = [0, 2, 0, 0, 0, 0, -5, 0, -3, 0, 0, 0, 5, -5, 0, 0, 0, 3, 0, 5, 0, 0, 0, 0, -2, 0],
            OnRollName = "Player",
            OpponentName = "Opponent",
            Dice = [3, 1],
            CubeSize = 1,
            CubeOwner = CubeOwner.Centered,
        };

        // Act
        var cut = Render<BackgammonDiagram>(parameters => parameters
            .Add(p => p.Request, request)
            .Add(p => p.Options, new DiagramOptions()));

        // Assert
        Assert.Contains("<svg", cut.Markup);
    }

    [Fact]
    public void Render_WithNullRequest_RendersEmpty()
    {
        // Act
        var cut = Render<BackgammonDiagram>(parameters => parameters
            .Add(p => p.Request, null));

        // Assert
        Assert.Equal(string.Empty, cut.Markup.Trim());
    }

    [Fact]
    public void EventCallbacks_AreExposed()
    {
        // Act — verify callbacks bind without error (contract test)
        var cut = Render<BackgammonDiagram>(parameters => parameters
            .Add(p => p.Request, new DiagramRequest())
            .Add(p => p.OnPointClicked, (int _) => { })
            .Add(p => p.OnCubeActionClicked, () => { }));

        // Assert
        Assert.NotNull(cut);
    }
}