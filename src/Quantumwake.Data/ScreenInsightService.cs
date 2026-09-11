namespace Quantumwake.Data;

/// <summary>What one scan of a screenshot found.</summary>
/// <param name="Trouble">
/// Why there is no answer, in the pilot's words, or null when there is one.
/// Never a bare empty result: a panel that goes blank is indistinguishable
/// from one that is broken.
/// </param>
/// <param name="Shot">The file that was read, by name only.</param>
/// <param name="TookMs">How long the engine took, because it is worth knowing.</param>
public sealed record ScreenScan(
    string? Shot,
    DateTimeOffset? ShotAt,
    string? Name,
    IReadOnlyDictionary<string, string> Fields,
    IReadOnlyList<ScreenScanMatch> Matches,
    IReadOnlyList<ScreenScanNamed> Named,
    bool Certain,
    string? Trouble,
    long TookMs);

/// <summary>One thing the tooltip might be, and what vouches for it.</summary>
public sealed record ScreenScanMatch(
    string Name,
    string ClassName,
    string? Manufacturer,
    string? Type,
    long MicroScu,
    string Tier,
    IReadOnlyList<string> Agrees,
    IReadOnlyList<string> Disagrees);

/// <summary>One line of a frame that named something.</summary>
public sealed record ScreenScanNamed(string Text, IReadOnlyList<string> Candidates, bool Exact);

/// <summary>What reading the clipboard found.</summary>
public sealed record ClipboardReading(
    bool Found,
    double? X,
    double? Y,
    double? Z,
    double? GigametresFromCentre,
    string? Trouble);

/// <summary>
/// The screenshot feature, joined up: a file, an engine, the catalogue, and
/// what the logs believed.
/// </summary>
/// <remarks>
/// <para>
/// Everything here refuses rather than guesses. There is no engine on some
/// machines, no screenshots folder until the first screenshot, and no name on
/// some tooltips - and each of those is a sentence the panel can show rather
/// than an empty box.
/// </para>
/// <para>
/// The reader is optional because the server can run without one. See
/// <see cref="IScreenReader"/>.
/// </para>
/// </remarks>
public sealed class ScreenInsightService(
    LogLibrary library,
    ScreenReadingStore readings,
    IScreenReader? reader = null,
    IClipboardReader? clipboard = null)
{
    public bool CanReadScreenshots => reader is not null;

    public bool CanReadClipboard => clipboard is not null;

    /// <summary>Why a screenshot cannot be read right now, or null when one can.</summary>
    public string? Excuse(string? installRoot)
    {
        if (reader is null)
            return "this copy cannot read screenshots - the overlay does that";

        if (installRoot is not { Length: > 0 })
            return "no Star Citizen install found to take screenshots from";

        return null;
    }

    /// <summary>Reads the newest screenshot and says what it holds.</summary>
    public async Task<ScreenSighting> ScanNewestAsync(string? installRoot, CancellationToken token = default)
    {
        if (Excuse(installRoot) is { } excuse)
            return Nothing("", DateTimeOffset.UtcNow, excuse);

        if (Screenshots.Newest(installRoot!) is not { } shot)
            return Nothing("", DateTimeOffset.UtcNow, "no screenshots yet - press Print Screen in the game and try again");

        return await ReadShotAsync(shot, token);
    }

    /// <summary>
    /// Reads one screenshot, works out which screen it is, checks what it
    /// says against the logs, and remembers the result.
    /// </summary>
    public async Task<ScreenSighting> ReadShotAsync(string path, CancellationToken token = default)
    {
        var info = new FileInfo(path);
        var shotAt = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero);

        if (reader is null)
            return Nothing(info.Name, shotAt, "this copy cannot read screenshots - the overlay does that");

        var watch = System.Diagnostics.Stopwatch.StartNew();
        IReadOnlyList<ScreenTextLine> lines;

        try
        {
            lines = await reader.ReadAsync(path, token);
        }
        catch (Exception e)
        {
            // A half-written file is the likely one: the game is still saving
            // the shot the pilot took a moment ago.
            return Nothing(info.Name, shotAt, $"could not read that screenshot ({e.GetType().Name})");
        }

        watch.Stop();

        var sighting = Understand(info.Name, shotAt, lines, watch.ElapsedMilliseconds);
        readings.Add(sighting);
        return sighting;
    }

    /// <summary>Everything after the engine, with nothing written: what a frame's lines mean.</summary>
    public ScreenSighting Understand(
        string shot, DateTimeOffset shotAt, IReadOnlyList<ScreenTextLine> lines, long tookMs)
    {
        var items = library.Items();
        var ships = ShipNames(items);

        var frame = ScreenFrames.Read(lines, items, ships, library.Handle(), CommodityNames());
        var beliefs = new LibraryBeliefs(library);
        var checks = ScreenChecks.Check(frame, shotAt, beliefs, readings.LastWallet());

        // The fittings check knows which parts are stock; say so on each part
        // as well as in the verdict, because the fleet page shows them one by one.
        var loadout = frame.Loadout;

        if (loadout?.Ship is { } ship)
        {
            var stock = beliefs.StockParts(ship);

            if (stock.Count > 0)
            {
                loadout = loadout with
                {
                    Fittings = [.. loadout.Fittings.Select(f => f.Name is null
                        ? f
                        : f with { Stock = stock.Contains(f.Name, StringComparer.OrdinalIgnoreCase) })],
                };
            }
        }

        var item = frame.Kind == ScreenKind.Tooltip ? ItemScan(shot, shotAt, frame, lines, items, tookMs) : null;

        return new ScreenSighting(
            shot, shotAt, frame.Kind,
            Summarise(frame, loadout, item),
            checks, item, loadout, frame.Map, frame.Wallet, frame.Lines, tookMs,
            frame.Contracts, frame.Fleet, frame.Reputation, frame.Kiosk);
    }

    /// <summary>Reads the clipboard for a <c>/showlocation</c> reading.</summary>
    public async Task<ClipboardReading> ReadClipboardAsync(bool mergeWithLatest = false, CancellationToken token = default)
    {
        if (clipboard is null)
            return new ClipboardReading(false, null, null, null, null,
                "this copy cannot read the clipboard - the overlay does that");

        var text = await clipboard.ReadTextAsync(token);

        if (text is not { Length: > 0 })
            return new ClipboardReading(false, null, null, null, null, "nothing copied");

        if (ShipPosition.Parse(text) is not { } position)
        {
            return new ClipboardReading(false, null, null, null, null,
                "what you copied is not a location - type /showlocation in the game first");
        }

        // Kept, with where the logs put the pilot at the time. The reading
        // names no place and no system, so without that it is three numbers
        // nobody can place a week later.
        var now = DateTimeOffset.UtcNow;
        var believed = new LibraryBeliefs(library).Placed(now);

        readings.AddClipboard(new ClipboardSighting(
            now, position.X, position.Y, position.Z, position.GigametresFromCentre,
            believed?.Name, believed?.System,
            BelievedBy: believed?.Signal, BelievedAt: believed?.SignalAt), mergeWithLatest);

        return new ClipboardReading(
            true, position.X, position.Y, position.Z, position.GigametresFromCentre, null);
    }

    /// <summary>
    /// The names a ship can be read as: the dataset's, and the install's own
    /// vehicle entries, which spell the same ship twelve ways by class and
    /// one way by name.
    /// </summary>
    private IReadOnlyList<string> ShipNames(IReadOnlyList<ItemReference> items)
    {
        var names = library.Community.Ships.Values
            .Select(ship => ship.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name));

        // The install types a ship as NOITEM_Vehicle / Vehicle_Spaceship.
        var vehicles = items
            .Where(item => item.Type?.Contains("Vehicle", StringComparison.OrdinalIgnoreCase) == true)
            .Select(item => item.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!);

        return [.. names.Concat(vehicles).Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>What the game calls its commodities, install first.</summary>
    private IReadOnlyList<string> CommodityNames() =>
        [.. library.GameCommodities.All.Values
            .Concat(library.Community.All.Values.Select(c => c.Name))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)];

    private static string Summarise(ScreenFrame frame, LoadoutReading? loadout, ScreenScan? item) => frame.Kind switch
    {
        ScreenKind.Loadout when loadout is not null =>
            $"{loadout.Ship ?? loadout.ShipRead ?? "a ship"}, {loadout.Fittings.Count(f => f.Name is not null)} parts named",
        ScreenKind.Map when frame.Map is not null =>
            frame.Map.PlaceRead is { Length: > 0 } place
                ? frame.Map.SystemRead is { Length: > 0 } system ? $"{system} > {place}" : place
                : "the map, with no footer read",
        ScreenKind.Tooltip when item is not null =>
            item.Certain && item.Matches.Count == 1 ? item.Matches[0].Name
            : item.Name ?? (item.Named.Count > 0 ? $"{item.Named.Count} things named" : "a tooltip that named nothing"),
        ScreenKind.Contracts when frame.Contracts is not null =>
            frame.Contracts.Accepted is { } n ? $"{n} contracts accepted" : $"{frame.Contracts.Cards.Count} contracts read",
        ScreenKind.Fleet when frame.Fleet is not null =>
            $"{frame.Fleet.Ships.Count} ships at the Fleet Manager",
        ScreenKind.Reputation when frame.Reputation is not null =>
            frame.Reputation.Organisation is { Length: > 0 } org
                ? $"reputation with {org}" + (frame.Reputation.Standing is { Length: > 0 } s ? $": {s}" : "")
                : "the Rep app",
        ScreenKind.Kiosk when frame.Kiosk is not null =>
            $"a kiosk {(frame.Kiosk.Buying == false ? "selling" : "buying")}, {frame.Kiosk.Rows.Count} commodities listed",
        ScreenKind.MobiGlas => "a mobiGlas screen this app cannot read yet",
        _ => "nothing this app knows how to read",
    };

    private static ScreenScan ItemScan(
        string shot, DateTimeOffset shotAt, ScreenFrame frame,
        IReadOnlyList<ScreenTextLine> lines, IReadOnlyList<ItemReference> items, long tookMs)
    {
        var result = frame.Tooltip!;

        var named = ScreenInsight.Sweep(lines, items)
            .Select(line => new ScreenScanNamed(
                line.Text,
                [.. line.Candidates.Select(c => c.Item.Name ?? c.Item.ClassName)],
                line.Named))
            .ToList();

        return new ScreenScan(
            shot, shotAt,
            result.Reading.Name,
            result.Reading.Fields,
            [.. result.Candidates.Select(Describe)],
            named,
            result.Certain,

            // A frame with no tooltip is not a failure when it named things.
            result.Reading.Name is null && named.Count > 0 ? null : result.Trouble,
            tookMs);
    }

    private static ScreenSighting Nothing(string shot, DateTimeOffset shotAt, string trouble) =>
        new(shot, shotAt, ScreenKind.Unknown, trouble, [], null, null, null, null, [], 0);

    private static ScreenScanMatch Describe(ScreenCandidate candidate) =>
        new(candidate.Item.Name ?? candidate.Item.ClassName,
            candidate.Item.ClassName,
            candidate.Item.Manufacturer,
            candidate.Item.Type,
            candidate.Item.MicroScu,
            candidate.Tier.ToString(),
            candidate.Agrees,
            candidate.Disagrees);
}
