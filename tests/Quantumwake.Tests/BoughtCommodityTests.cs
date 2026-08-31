using System.Text.Json;
using Quantumwake.Core.State;
using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// What the logs show was bought, for crossing off a shopping list.
/// </summary>
/// <remarks>
/// Two different records say "bought", and only one of them was being read.
/// Gear comes off a kiosk and lands in Purchases; cargo comes off a commodity
/// terminal and lands in Trades. A shopping line naming an ore could not be
/// crossed off however many SCU of it came home, and the line accepted the
/// attachment type all the same, so it looked supported and never fired.
/// </remarks>
public class BoughtCommodityTests : IDisposable
{
    private const string Waste = "b999ef65-35be-45bf-908a-5eac6e06ba12";

    private readonly SessionStore _store = new(":memory:");
    private readonly LogLibrary _library;

    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"qw-bought-{Guid.NewGuid():N}");

    public BoughtCommodityTests()
    {
        Directory.CreateDirectory(_directory);

        var digest = CommunityData.Digest(
            $$"""[{"UUID":"{{Waste}}","Key":"Waste","Name":"Waste","CommodityGroups":["Organic"]}]""",
            "[]");

        File.WriteAllText(Path.Combine(_directory, "digest.json"), JsonSerializer.Serialize(digest));

        _library = new LogLibrary(_store) { Community = new CommunityData(_directory) };
    }

    public void Dispose()
    {
        _store.Dispose();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        GC.SuppressFinalize(this);
    }

    private void Save(string id, DateTimeOffset at, bool isSell) =>
        _store.Save(
            new SessionSummary
            {
                Id = id,
                SourceFile = $"{id}.log",
                StartedAt = at.AddHours(-1),
                EndedAt = at.AddHours(1),
                Handle = "nekron",
                Trades = [new CommodityTrade(at, "TDD Area 18", 12_000m, 40, isSell, "Cargo", Waste)],
            },
            $"fingerprint:{id}");

    [Fact]
    public void A_commodity_bought_at_a_terminal_counts_as_bought()
    {
        var at = new DateTimeOffset(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);
        Save("buy", at, isSell: false);

        Assert.Equal(at, _library.Bought()["Waste"]);
    }

    /// <summary>Selling a thing is not buying it, however much of it moved.</summary>
    [Fact]
    public void Selling_it_does_not()
    {
        Save("sell", new DateTimeOffset(2026, 8, 20, 12, 0, 0, TimeSpan.Zero), isSell: true);

        Assert.Empty(_library.Bought());
    }

    /// <summary>
    /// A line is crossed off by the latest buy, so a second run of the same ore
    /// moves the date forward rather than leaving the first one standing.
    /// </summary>
    [Fact]
    public void The_latest_buy_is_the_one_reported()
    {
        var first = new DateTimeOffset(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);
        var later = new DateTimeOffset(2026, 8, 26, 9, 0, 0, TimeSpan.Zero);

        Save("first", first, isSell: false);
        Save("later", later, isSell: false);

        Assert.Equal(later, _library.Bought()["Waste"]);
    }
}
