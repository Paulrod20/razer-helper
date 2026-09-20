using RazerHelper.Helpers;

namespace RazerHelper.Tests.Helpers;

public class WindowPlacementTests
{
    private static readonly Rectangle Screen = new(0, 0, 1920, 1040);
    private static readonly Size Settings = new(372, 498);

    [Fact]
    public void GoesToTheLeftOfAPopupAtTheRightEdge_LinedUpAtTheBottom()
    {
        var popup = new Rectangle(1330, 430, 584, 596);

        var at = WindowPlacement.Beside(popup, Settings, Screen);

        Assert.Equal(popup.Left - Settings.Width - 8, at.X);
        Assert.Equal(popup.Bottom - Settings.Height, at.Y);
    }

    [Fact]
    public void GoesToTheRightWhenThereIsNoRoomOnTheLeft()
    {
        var popup = new Rectangle(100, 300, 584, 596);

        var at = WindowPlacement.Beside(popup, Settings, Screen);

        Assert.Equal(popup.Right + 8, at.X);
    }

    [Fact]
    public void NeverOverlapsThePopupWhenThereIsRoomOnEitherSide()
    {
        foreach (var x in new[] { 0, 400, 900, 1336 })
        {
            var popup = new Rectangle(x, 300, 584, 596);
            var at = WindowPlacement.Beside(popup, Settings, Screen);

            Assert.False(new Rectangle(at, Settings).IntersectsWith(popup), $"popup at x={x}");
        }
    }

    [Fact]
    public void StaysInsideTheScreenVertically()
    {
        var tallOnTop = new Rectangle(1330, 0, 584, 200);

        var at = WindowPlacement.Beside(tallOnTop, Settings, Screen);

        Assert.True(at.Y >= Screen.Top);
        Assert.True(at.Y + Settings.Height <= Screen.Bottom);
    }

    [Fact]
    public void ALargerWindowThanTheScreenStillStartsAtTheTopLeft()
    {
        var at = WindowPlacement.Beside(new Rectangle(0, 0, 100, 100), new Size(3000, 2000), Screen);

        Assert.Equal(Screen.Location, at);
    }
}
