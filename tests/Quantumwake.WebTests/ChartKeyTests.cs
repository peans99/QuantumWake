namespace Quantumwake.WebTests;

/// <summary>A chart's key has to name the line it is drawn beside.</summary>
public class ChartKeyTests
{
    [Fact]
    public void The_key_colours_the_line_that_was_actually_drawn()
    {
        var page = new Page();

        // The shape a player who has bought cargo but sold none produces: the
        // first series has nothing to draw, so the second one takes the first
        // colour - and the key has to agree rather than count the skipped line.
        page.Do("""
            const series = [
              { label: 'Earned selling cargo', points: [] },
              { label: 'Spent buying it', points: [{ t: 1000, v: 10 }, { t: 2000, v: 20 }] },
            ];
            timeChart(__dom.node('#commodity-price-chart'), series, (v) => String(v));
            chartKey('#commodity-price-key', series);
            """);

        var drawn = page.Text(
            "__dom.node('#commodity-price-chart').children"
            + ".filter(c => c.tagName === 'path').map(c => c.getAttribute('stroke')).join('|')");

        var keyed = page.Text(
            "__dom.node('#commodity-price-key').children.map(e => e.children[0].style.background).join('|')");

        Assert.NotEmpty(drawn);
        Assert.Equal(drawn, keyed);
        Assert.Equal("Spent buying it", page.NodeText("#commodity-price-key"));
    }

    [Fact]
    public void Totals_only_span_the_days_every_counter_reported_in()
    {
        var page = new Page();

        // One counter with three days of history, one that joined on the last
        // of them. Summing the union would step demand from 10 to 50 on the day
        // the second counter appears, which says nothing about the market.
        page.Do("""
            __daily = dailyMarket({ series: [
              { terminal: 'Old counter', points: [
                { at: '2026-08-20T00:00:00Z', sell: 100, buy: 0, demand: 10, stock: 0 },
                { at: '2026-08-22T00:00:00Z', sell: 100, buy: 0, demand: 10, stock: 0 }] },
              { terminal: 'New counter', points: [
                { at: '2026-08-22T00:00:00Z', sell: 120, buy: 0, demand: 40, stock: 0 }] },
            ] });
            """);

        Assert.Equal(3, page.Count("__daily.length"));

        // One counter reporting 10, then two reporting 10 and 40: the level a
        // counter reports rises from 10 to 25, and does not jump to 50 merely
        // because a second counter started answering.
        Assert.Equal(1, page.Count("__daily[0].counters"));
        Assert.Equal(10, page.Number("__daily[0].demand"));
        Assert.Equal(2, page.Count("__daily[2].counters"));
        Assert.Equal(25, page.Number("__daily[2].demand"));

        // And the price line is a max over whoever reported, so it is not
        // distorted by a counter that had not joined yet.
        Assert.Equal(100, page.Number("__daily[0].bestSell"));
        Assert.Equal(120, page.Number("__daily[2].bestSell"));
    }
}
