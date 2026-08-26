namespace Quantumwake.WebTests;

/// <summary>
/// Arranging the Now page: which cards sit where, and what happens to a card
/// the saved arrangement has never seen.
/// </summary>
public class NowCardOrderTests
{
    private static Page Fresh() => new();

    /// <summary>The markup's own order, which is the default arrangement.</summary>
    private static string Natural(Page page) => page.Text(
        "resolveNowOrder(['location','ship','session','feed'], []).join(',')");

    [Fact]
    public void With_nothing_saved_the_markup_decides()
    {
        var page = Fresh();

        Assert.Equal("location,ship,session,feed", Natural(page));
    }

    [Fact]
    public void A_saved_arrangement_is_kept()
    {
        var page = Fresh();

        Assert.Equal("feed,location,ship,session", page.Text(
            "resolveNowOrder(['location','ship','session','feed'], ['feed','location','ship','session']).join(',')"));
    }

    /// <summary>
    /// The promise the overlay's layout store makes too: a card added in a
    /// later version must not read as one the reader arranged away.
    /// </summary>
    [Fact]
    public void A_card_the_saved_order_never_saw_appears_beside_its_neighbour()
    {
        var page = Fresh();

        // 'session' is new: it was not in the saved list, and it belongs after
        // 'ship' in the markup - not dumped at the end.
        Assert.Equal("feed,location,ship,session", page.Text(
            "resolveNowOrder(['location','ship','session','feed'], ['feed','location','ship']).join(',')"));
    }

    [Fact]
    public void A_card_that_no_longer_exists_is_dropped()
    {
        var page = Fresh();

        Assert.Equal("ship,location", page.Text(
            "resolveNowOrder(['location','ship'], ['ship','retired','location']).join(',')"));
    }

    [Fact]
    public void Moving_a_card_puts_it_before_the_one_it_was_dropped_on()
    {
        var page = Fresh();

        Assert.Equal("feed,location,ship,session", page.Text(
            "moveNowCard(['location','ship','session','feed'], 'feed', 'location').join(',')"));
    }

    [Fact]
    public void Moving_a_card_to_the_end_is_a_move_before_nothing()
    {
        var page = Fresh();

        Assert.Equal("ship,session,feed,location", page.Text(
            "moveNowCard(['location','ship','session','feed'], 'location', null).join(',')"));
    }

    /// <summary>An arrangement is worth nothing if it is gone on the next visit.</summary>
    [Fact]
    public void An_arrangement_is_remembered()
    {
        var page = Fresh();

        page.Do("""
            nowCardOrder = ['feed', 'location'];
            saveNowCardOrder();
            """);

        Assert.Contains("feed", page.Text("localStorage.getItem('qw-now-card-order')"));
    }

    /// <summary>
    /// A preference that cannot be read must not take the dashboard with it.
    /// </summary>
    [Fact]
    public void A_corrupt_arrangement_is_ignored_rather_than_obeyed()
    {
        var page = Fresh();

        var order = page.Text("""
            (() => {
              try { return resolveNowOrder(['location','ship'], JSON.parse('not json')).join(','); }
              catch { return 'threw'; }
            })()
            """);

        Assert.Equal("threw", order);
        Assert.False(page.Truth("__dom.node('#view-now').hidden === true"));
    }
}
