using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// What an item class is called on screen.
/// </summary>
/// <remarks>
/// Only the last resort can be defended here. The two sources that do the real
/// work - the localisation table and the item catalogue - are both read out of
/// a 316 MB archive in the install and neither can be stood up from a fixture,
/// so the ordering between them is checked against the install itself rather
/// than in this suite. See <c>LogLibrary.ItemName</c> for what that ordering is
/// and why it exists.
/// </remarks>
public class ItemNameTests : IDisposable
{
    private readonly SessionStore _store = new(":memory:");

    public void Dispose()
    {
        _store.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// With no install read, every lookup misses - and a miss has to hand back
    /// the class rather than an empty string. A blank line in a list of things
    /// you bought says nothing at all; the class with its underscores in it at
    /// least names the thing to anyone willing to squint.
    /// </summary>
    [Fact]
    public void An_item_nothing_knows_about_comes_back_as_its_class()
    {
        var library = new LogLibrary(_store);

        Assert.Equal(
            "slaver_undersuit_01_01_01",
            library.ItemName("slaver_undersuit_01_01_01"));
    }

    [Fact]
    public void An_empty_class_stays_empty_rather_than_throwing()
    {
        var library = new LogLibrary(_store);

        Assert.Equal("", library.ItemName(""));
    }
}
