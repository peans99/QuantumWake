namespace Quantumwake.WebTests;

/// <summary>
/// The offer to renew a stale price table, and the wipe control beside it.
/// Both are startup behaviour, which is exactly the kind that goes unnoticed
/// when it breaks.
/// </summary>
public class PriceAgeTests
{
    private static string HoursAgo(double hours) =>
        DateTimeOffset.UtcNow.AddHours(-hours).ToString("O");

    private static Page WithPrices(bool enabled, double? ageHours, string feeds = "[]")
    {
        var page = new Page();

        var fetched = ageHours is null ? "null" : $"\"{HoursAgo(ageHours.Value)}\"";

        page.Serve("/api/uex", $$"""
            { "enabled": {{(enabled ? "true" : "false")}}, "prices": 1200, "fetchedAt": {{fetched}} }
            """);

        page.Serve("/api/uex/feeds", feeds);
        return page;
    }

    private static bool NoticeShown(Page page)
    {
        page.Do("await checkPriceAge();");
        return !page.Truth("__dom.node('#stale').hidden");
    }

    [Fact]
    public void Prices_fetched_this_morning_say_nothing()
    {
        Assert.False(NoticeShown(WithPrices(enabled: true, ageHours: 3)));
    }

    [Fact]
    public void Prices_a_day_old_offer_a_refresh()
    {
        var page = WithPrices(enabled: true, ageHours: 30);

        Assert.True(NoticeShown(page));
        Assert.Contains("Prices last fetched", page.NodeText("#stale-detail"));
        Assert.Contains("margins", page.NodeText("#stale-detail"));
    }

    /// <summary>Being nagged about a feature you have not enabled is noise.</summary>
    [Fact]
    public void An_integration_that_is_off_is_not_nagged_about()
    {
        Assert.False(NoticeShown(WithPrices(enabled: false, ageHours: 300)));
    }

    [Fact]
    public void Prices_never_fetched_are_not_stale_they_are_absent()
    {
        Assert.False(NoticeShown(WithPrices(enabled: true, ageHours: null)));
    }

    /// <summary>
    /// The feeds age at the same rate as the prices, and a refresh that renewed
    /// only half of it would leave the same problem behind.
    /// </summary>
    [Fact]
    public void A_stale_feed_counts_even_when_the_prices_are_fresh()
    {
        var page = WithPrices(enabled: true, ageHours: 2, feeds: $$"""
            [
              { "key": "rentals", "enabled": true, "fetchedAt": "{{HoursAgo(80)}}" },
              { "key": "fuel", "enabled": false, "fetchedAt": null }
            ]
            """);

        Assert.True(NoticeShown(page));
        Assert.Contains("1 other feed as old", page.NodeText("#stale-detail"));
    }

    [Fact]
    public void Refreshing_renews_the_prices_and_everything_as_old_as_them()
    {
        var page = WithPrices(enabled: true, ageHours: 40, feeds: $$"""
            [{ "key": "rentals", "enabled": true, "fetchedAt": "{{HoursAgo(80)}}" }]
            """);

        page.Do("await checkPriceAge();");
        page.Do("__dom.node('#stale-refresh').fire('click', { currentTarget: __dom.node('#stale-refresh') });");

        Assert.Contains("POST /api/uex/enable", page.Fetched());
        Assert.Contains("POST /api/uex/feeds/rentals/enable", page.Fetched());
        Assert.True(page.Truth("__dom.node('#stale').hidden"));
    }

    /// <summary>
    /// "Not now" means ask again when it matters, not never: by tomorrow the
    /// prices are a day older still.
    /// </summary>
    [Fact]
    public void Not_now_lasts_the_day_and_no_longer()
    {
        var page = WithPrices(enabled: true, ageHours: 40);

        page.Do("await checkPriceAge();");
        page.Do("__dom.node('#stale-dismiss').fire('click');");

        Assert.True(page.Truth("__dom.node('#stale').hidden"));
        Assert.False(NoticeShown(page));

        // Wind the dismissal back past its day and the offer returns.
        page.Do("localStorage.setItem('qw-uex-stale-dismissed', String(Date.now() - 1000));");

        Assert.True(NoticeShown(page));
    }

    [Fact]
    public void A_server_that_cannot_answer_leaves_the_page_alone()
    {
        var page = new Page();

        Assert.False(NoticeShown(page));
    }
}
