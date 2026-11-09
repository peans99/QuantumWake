namespace Quantumwake.WebTests;

public class NavigationMenuBoundsTests
{
    [Fact]
    public void The_sticky_flight_line_follows_the_actual_wrapped_header_height()
    {
        var page = new Page();
        page.Do("__dom.node('.topbar').getBoundingClientRect = () => ({height: 117.4}); syncNavigationHeader();");
        Assert.Equal("118px", page.Text("document.body.style['--console-header']"));
    }

    [Theory]
    [InlineData(12, 165, 320, 12)]
    [InlineData(193, 165, 320, 143)]
    [InlineData(282, 165, 390, 213)]
    [InlineData(778, 180, 1600, 778)]
    public void Wrapped_navigation_menus_remain_reachable_from_either_screen_edge(double anchor, double width, double viewport, double expectedLeft)
    {
        var page = new Page();
        var left = page.Text($"String({anchor} + navigationMenuOffset({anchor}, {width}, {viewport}))");
        Assert.Equal(expectedLeft.ToString(System.Globalization.CultureInfo.InvariantCulture), left);
    }
}
