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
/// <param name="Nearest">
/// The closest saved points to this copy, nearest first - the first thing a
/// coordinate is good for. Empty when nothing is pinned.
/// </param>
public sealed record ClipboardReading(
    bool Found,
    double? X,
    double? Y,
    double? Z,
    double? GigametresFromCentre,
    string? Trouble,
    IReadOnlyList<NearPoint>? Nearest = null);

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
    IClipboardReader? clipboard = null,
    Func<IScreenReader?>? panelReader = null)
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

        try
        {
            return await ReadShotAsync(shot, token);
        }
        catch (ScreenEngineUnavailableException e)
        {
            // No shot named, so the page shows why and nothing is kept.
            return Nothing("", DateTimeOffset.UtcNow, e.Message);
        }
    }

    /// <summary>
    /// The screenshots in the game's folder that have never been read, newest
    /// first. Empty when there is no folder, or no reader to count for.
    /// </summary>
    public IReadOnlyList<ScreenFile> Unread(string? installRoot)
    {
        if (Excuse(installRoot) is not null) return [];

        var folder = Screenshots.FolderFor(installRoot!);
        if (!Directory.Exists(folder)) return [];

        try
        {
            return ScreenFolder.Unread(
                new DirectoryInfo(folder).EnumerateFiles()
                    .Where(f => ScreenFolder.IsScreenshot(f.Name))
                    .Select(f => new ScreenFile(f.FullName, f.Length, new DateTimeOffset(f.LastWriteTimeUtc, TimeSpan.Zero))),
                DateTimeOffset.UtcNow,
                path => readings.Has(Path.GetFileName(path)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>
    /// Rep app screenshots read before the bars were measured, newest first,
    /// whose files are still in the game's folder.
    /// </summary>
    /// <remarks>
    /// The rank and its progress come from pixels, and a reading taken before
    /// 0.18.22 kept only the text - so every install that photographed the
    /// Rep app before then holds readings with no rank in them, and the most
    /// history shows the least. Reading the file again is the only way back,
    /// and costs a fifth of a second a frame.
    /// </remarks>
    public IReadOnlyList<string> Unmeasured(string? installRoot)
    {
        if (Excuse(installRoot) is not null) return [];

        var folder = Screenshots.FolderFor(installRoot!);

        return [.. readings.ReputationHistory()
            .Where(r => !r.Dismissed && r.Reading.Ranks?.Any(c => c.Fill is not null) != true)
            .Select(r => Path.Combine(folder, r.Shot))
            .Where(File.Exists)];
    }

    /// <summary>
    /// Screenshots filed as a mobiGlas screen this app cannot read that carry
    /// one of the Rep app's tabs - the frames a stricter reader turned away -
    /// whose files are still in the game's folder.
    /// </summary>
    /// <remarks>
    /// Before 0.18.26 the Rep app needed both CAREER and DOSSIER to read, and
    /// a frame where the engine lost one was filed as unreadable for good: the
    /// log is not read twice. These are worth one more look with the reader
    /// as it is now. Kept apart from <see cref="Unmeasured"/>, which the page
    /// counts as Rep readings waiting - these may turn out not to be.
    /// </remarks>
    public IReadOnlyList<string> Misfiled(string? installRoot)
    {
        if (Excuse(installRoot) is not null) return [];

        var folder = Screenshots.FolderFor(installRoot!);

        return [.. readings.All()
            .Where(s => !s.Dismissed && s.Kind == ScreenKind.MobiGlas)
            .Where(s => s.Lines.Any(line => line.Trim() is "CAREER" or "DOSSIER"))
            .Select(s => Path.Combine(folder, s.Shot))
            .Where(File.Exists)];
    }

    /// <summary>
    /// Rep readings whose bars say nothing was earned on the rank held - no
    /// fill on any card, or the held rank at exactly nothing - whose files
    /// are still in the game's folder.
    /// </summary>
    /// <remarks>
    /// Before 0.18.28 a fill washed nearly cyan was not counted as fill at
    /// all, and the 24 October 22:46 frame was kept as Red Wind Member 0%
    /// when its bar showed 78%. A real 0% needs the pilot to photograph the
    /// moment of a promotion, so these are worth one more look; a reading
    /// that is right costs a fifth of a second to confirm.
    /// </remarks>
    public IReadOnlyList<string> Doubtful(string? installRoot)
    {
        if (Excuse(installRoot) is not null) return [];

        var folder = Screenshots.FolderFor(installRoot!);

        return [.. readings.ReputationHistory()
            .Where(r => !r.Dismissed && r.Reading.Ranks?.Any(c => c.Fill is not null) == true)
            .Where(r => r.Reading.Progress is 0 || r.Reading.Ranks!.All(c => c.Fill is null or 0))
            .Select(r => Path.Combine(folder, r.Shot))
            .Where(File.Exists)];
    }

    /// <summary>
    /// Contracts app frames read before 0.19.4 on which the engine split an
    /// objective's diamond from its text, whose files are still in the
    /// game's folder.
    /// </summary>
    /// <remarks>
    /// Those are the frames the older reader joined into one objective or
    /// lost objectives from - see <see cref="ScreenFrames.ContractsReader"/>.
    /// The stored lines carry no positions, so they cannot be read again
    /// without the image; a frame that was read whole is left alone, and one
    /// read again is marked with the new reader and not offered twice.
    /// </remarks>
    public IReadOnlyList<string> Unjoined(string? installRoot)
    {
        if (Excuse(installRoot) is not null) return [];

        var folder = Screenshots.FolderFor(installRoot!);

        return [.. readings.ContractFrames()
            .Where(s => !s.Dismissed && s.Contracts is { Reader: < ScreenFrames.ContractsReader })
            .Where(s => s.Lines.Any(line => line.Trim() is "o" or "O" or "0"
                || line.TrimStart().StartsWith("Deliver ", StringComparison.Ordinal)
                || line.TrimStart().StartsWith("Collect ", StringComparison.Ordinal)))
            .Select(s => Path.Combine(folder, s.Shot))
            .Where(File.Exists)];
    }

    /// <summary>
    /// Reads the newest unread screenshots, at most <paramref name="take"/> of
    /// them, and says how many are still waiting. The archive on demand: the
    /// watch reads nothing that was there before it began, and the loadout a
    /// pilot photographed the evening before they installed the app is still
    /// the only photograph of that ship.
    /// </summary>
    public async Task<(IReadOnlyList<ScreenSighting> Read, int Remaining)> ReadOlderAsync(
        string? installRoot, int take, CancellationToken token = default)
    {
        var waiting = Unread(installRoot);
        var read = new List<ScreenSighting>();

        foreach (var file in waiting.Take(Math.Max(0, take)))
        {
            token.ThrowIfCancellationRequested();

            ScreenSighting sighting;

            try
            {
                sighting = await ReadShotAsync(file.Path, token);
            }
            catch (Exception e) when (e is not OperationCanceledException and not ScreenEngineUnavailableException)
            {
                sighting = Nothing(Path.GetFileName(file.Path), file.LastWrite,
                    $"could not read that screenshot ({e.GetType().Name})");
            }

            // A file the engine refused is not half-written here as it is
            // under the watch - it is days old - so the refusal is kept.
            // Otherwise the same file heads the list at every press, and a
            // bounded read never gets past it.
            if (!readings.Has(sighting.Shot)) readings.Add(sighting);
            read.Add(sighting);
        }

        return (read, Math.Max(0, waiting.Count - read.Count));
    }

    /// <summary>
    /// Reads one screenshot, works out which screen it is, checks what it
    /// says against the logs, and remembers the result.
    /// </summary>
    public async Task<ScreenSighting> ReadShotAsync(string path, CancellationToken token = default)
    {
        var info = new FileInfo(path);
        var (sighting, read, _) = await ReadFrameAsync(path, info.Name, new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero), token);

        // A frame the engine could not open is not kept: the watcher gives a
        // file the game is still writing another look before it settles that.
        if (read) readings.Add(sighting);
        return sighting;
    }

    /// <summary>
    /// Reads a frame copied off the screen just now, and remembers nothing:
    /// the live loop decides what is worth keeping, because most frames are
    /// the same frame again.
    /// </summary>
    /// <param name="path">Where the frame was written.</param>
    /// <param name="shot">The name the reading goes by if it is kept - there is no file a pilot saved.</param>
    /// <param name="shotAt">When it was copied.</param>
    public async Task<ScreenSighting> ReadLiveAsync(string path, string shot, DateTimeOffset shotAt, CancellationToken token = default) =>
        (await ReadFrameAsync(path, shot, shotAt, token)).Sighting;

    /// <summary>
    /// <see cref="ReadLiveAsync"/> with the engine's lines kept, for the
    /// readers that need to know where a thing sat rather than what it said -
    /// the target panel is found by its words and then read again up close.
    /// </summary>
    public async Task<(ScreenSighting Sighting, IReadOnlyList<ScreenTextLine> Lines)> ReadLiveFrameAsync(
        string path, string shot, DateTimeOffset shotAt, CancellationToken token = default)
    {
        var (sighting, _, lines) = await ReadFrameAsync(path, shot, shotAt, token);
        return (sighting, lines);
    }

    /// <summary>The hulls by cross-section, rebuilt when the dataset under it changes.</summary>
    public ShipSignatureBook SignatureBook()
    {
        var community = library.Community;
        if (!ReferenceEquals(community, _bookFrom))
        {
            _book = ShipSignatureBook.From(community);
            _bookFrom = community;
        }

        return _book;
    }

    private ShipSignatureBook _book = ShipSignatureBook.Empty;
    private CommunityData? _bookFrom;

    /// <summary>
    /// The mining radar's signature readout above the compass, read up close by
    /// the panel reader - Windows' engine read nothing in that box on 80
    /// frames; see <see cref="PingReadout"/>. Null when the box is empty.
    /// </summary>
    public async Task<double?> ReadPingAsync(string path, int width, int height, CancellationToken token = default)
    {
        var look = panelReader?.Invoke() ?? reader;
        if (look is null) return null;
        try
        {
            return PingReadout.Value(await look.ReadAsync(path, PingReadout.Patch(width, height), new ScreenTreatment(Scale: 2, Shear: 0), token));
        }
        catch (Exception e) when (e is not ScreenEngineUnavailableException && !token.IsCancellationRequested)
        {
            return null;
        }
    }

    /// <summary>
    /// Whether the "INBOUND MISSILE" warning is in the band it moves in, read up
    /// close by the panel reader: about 77 ms a frame on the 2026-11-06
    /// recording, which caught it on all 4 frames it showed and on none of the
    /// other 133. See <see cref="HudText"/>.
    /// </summary>
    public async Task<bool> ReadMissileAsync(string path, int width, int height, CancellationToken token = default)
    {
        var look = panelReader?.Invoke() ?? reader;
        if (look is null) return false;
        try
        {
            var lines = await look.ReadAsync(path, HudText.MissileBand(width, height), new ScreenTreatment(Scale: 1, Shear: 0), token);
            return HudText.Missile(lines, width, height);
        }
        catch (Exception e) when (e is not ScreenEngineUnavailableException && !token.IsCancellationRequested)
        {
            return false;
        }
    }

    /// <summary>What each rock reads on the radar, from the install; empty until it has been read.</summary>
    public IReadOnlyList<Quantumwake.Core.GameData.RadarRow> RadarRows()
    {
        var rocks = library.GameCommodities.Mining.Signatures;
        if (!ReferenceEquals(rocks, _radarFrom))
        {
            _radar = rocks is null ? [] : Quantumwake.Core.GameData.GameRockSignatures.Radar(rocks);
            _radarFrom = rocks;
        }
        return _radar;
    }

    private IReadOnlyList<Quantumwake.Core.GameData.RadarRow> _radar = [];
    private object? _radarFrom;

    /// <summary>The places with mineables and what spawns at each, from the install's deposit tables; empty until read.</summary>
    public IReadOnlyList<PingPlace> PingPlaces()
    {
        var spawns = library.GameCommodities.Spawns;
        if (!ReferenceEquals(spawns, _placesFrom))
        {
            _places = Quantumwake.Data.PingPlaces.From(spawns);
            _placesFrom = spawns;
        }
        return _places;
    }

    private IReadOnlyList<PingPlace> _places = [];
    private object? _placesFrom;

    /// <summary>
    /// The fracture HUD's charge against its window, from pixels in
    /// <paramref name="area"/> - the search area on a first look, the bar's
    /// neighbourhood after; see <see cref="ChargeWindow"/>. About 22 ms a
    /// frame on the 2560 stream, with no text read at all.
    /// </summary>
    /// <summary>The locked ship's hologram judged for damage; see <see cref="HologramDamage"/>.</summary>
    public async Task<HologramReading?> ReadHologramAsync(string path, ScreenPatch panel, int height, CancellationToken token = default) =>
        reader is null || await reader.PixelsAsync(path, HologramDamage.Area(panel, height), token) is not { } pixels
            ? null : HologramDamage.Read(pixels, height);

    public async Task<ChargeReading?> ReadChargeAsync(string path, ScreenPatch area, int height, CancellationToken token = default) =>
        reader is null || await reader.PixelsAsync(path, area, token) is not { } pixels ? null : ChargeWindow.Read(pixels, height);

    /// <summary>
    /// The target panel, read up close; see <see cref="TargetLook"/>. With the
    /// host's panel reader when it offers one - ONNX, when its models are on
    /// disk - because the Windows engine read the boxed HUD worst of all.
    /// </summary>
    public Task<(TargetReading? Panel, TargetStance Stance)> ReadTargetAsync(
        string path, IReadOnlyList<ScreenTextLine>? whole, ScreenPatch? known, int width, int height,
        CancellationToken token = default) =>
        reader is null
            ? Task.FromResult<(TargetReading?, TargetStance)>((null, TargetStance.Unread))
            : TargetLook.ReadAsync(panelReader?.Invoke() ?? reader, path, whole, known, width, height, token);

    /// <summary>A frame read through every second look; <c>Read</c> is false when the engine never got to it.</summary>
    private async Task<(ScreenSighting Sighting, bool Read, IReadOnlyList<ScreenTextLine> Lines)> ReadFrameAsync(string path, string shot, DateTimeOffset shotAt, CancellationToken token)
    {
        if (reader is null)
            return (Nothing(shot, shotAt, "this copy cannot read screenshots - the overlay does that"), false, []);

        var watch = System.Diagnostics.Stopwatch.StartNew();
        IReadOnlyList<ScreenTextLine> lines;

        try
        {
            lines = await reader.ReadAsync(path, token);
        }
        catch (Exception e) when (e is not ScreenEngineUnavailableException)
        {
            // A half-written file is the likely one: the game is still saving
            // the shot the pilot took a moment ago.
            return (Nothing(shot, shotAt, $"could not read that screenshot ({e.GetType().Name})"), false, []);
        }

        // The whole read's lines are in the engine's straightened copy of a
        // tilted frame; every close read below is cut from the frame where the
        // text really is and handed back in that copy. See ScreenGeometry.
        var tilt = (reader as ITiltedReader)?.TiltOf(path);
        var straight = ScreenGeometry.Straight(reader, tilt);

        // The balance is the one line the engine drops from a whole frame
        // that it will read from a sheared crop; see WalletSecondLook. Only
        // ever runs when the bar is on the frame and the figure is not.
        lines = await WalletSecondLook.TakeAsync(lines,
            (patch, treatment, t) => straight.ReadAsync(path, patch, treatment, t), token);

        // The scan panel loses most of its figures in the whole read; see
        // MiningSecondLook. Only runs when the whole read already made the
        // frame a scan - or, with the host's panel reader at hand, when it
        // found the title at all: on 200 frames of a 2560 MOLE stream Windows'
        // engine made a scan of 3, and ONNX read the panel on 135.
        var closer = panelReader?.Invoke();
        var looks = ScreenGeometry.Straight(closer ?? reader, tilt);
        var panelLooks = ScreenGeometry.Straight(closer ?? reader, tilt, linesOnFrame: true);
        var scan = await MiningSecondLook.SettleAsync(lines,
            (patch, treatment, t) => panelLooks.ReadAsync(path, patch, treatment, t), CommodityNames(),
            fromTitle: closer is not null, token: token);

        // The Freight Manager's commodity under each crate tile, spelled
        // better up close; see FreightSecondLook. Only on its confirmation.
        lines = await FreightSecondLook.TakeAsync(lines,
            (patch, treatment, t) => looks.ReadAsync(path, patch, treatment, t), token);

        // The rank held is drawn, not written; see RepBars. Only decodes
        // pixels when the frame has rank cards on it.
        var bars = await RepBars.MeasureAsync(lines, (patch, t) => reader.PixelsAsync(path, patch, t), token);

        watch.Stop();

        return (Understand(shot, shotAt, lines, watch.ElapsedMilliseconds, scan, bars), true, lines);
    }

    /// <summary>Everything after the engine, with nothing written: what a frame's lines mean.</summary>
    public ScreenSighting Understand(
        string shot, DateTimeOffset shotAt, IReadOnlyList<ScreenTextLine> lines, long tookMs,
        MiningScanReading? settledScan = null, IReadOnlyList<RepRankCard>? repBars = null)
    {
        var items = library.Items();
        var ships = ShipNames(items);

        var frame = ScreenFrames.Read(lines, items, ships, CommodityNames(), settledScan);

        if (frame.Reputation is { } rep && repBars is { Count: > 0 })
            frame = frame with { Reputation = rep.WithBars(repBars) };
        var beliefs = new LibraryBeliefs(library);
        var checks = ScreenChecks.Check(frame, shotAt, beliefs, readings.LastWallet(before: shotAt));

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
            frame.Contracts, frame.Fleet, frame.Reputation, frame.Kiosk, Mining: frame.Mining,
            Refinery: frame.Refinery, Freight: frame.Freight);
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

        // Three at most: the panel is a glance, and the Points page has the rest.
        var nearest = PointDistances
            .From(position.X, position.Y, position.Z, believed?.System, readings.Pinned())
            .Take(3)
            .Select(NearPoint.Of)
            .ToList();

        return new ClipboardReading(
            true, position.X, position.Y, position.Z, position.GigametresFromCentre, null, nearest);
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
                ? $"reputation with {org}" + (frame.Reputation.Rank is { Length: > 0 } rank
                    ? $": {rank} {Math.Round((frame.Reputation.Progress ?? 0) * 100)}%"
                    : frame.Reputation.Standing is { Length: > 0 } s ? $": {s}" : "")
                : "the Rep app",
        ScreenKind.Kiosk when frame.Kiosk is not null =>
            $"a kiosk {(frame.Kiosk.Buying == false ? "selling" : "buying")}, {frame.Kiosk.Rows.Count} commodities listed",
        // A gem cluster's mass is 0.12 and its yield thousandths of a SCU, so
        // neither is rounded to a whole number - "0 kg, 0 SCU" was the first
        // summary of an aphorite scan.
        ScreenKind.Mining when frame.Mining is not null =>
            $"a rock scanned: {frame.Mining.Primary ?? frame.Mining.PrimaryRead ?? "mineral unread"}"
            + (frame.Mining.MassKg is { } kg ? $", {kg:#,0.##} kg" : ", mass unread")
            + (frame.Mining.ResistancePercent is { } r ? $", {r:0}% resistance" : "")
            + (frame.Mining.Scu is { } scu ? scu is > 0 and < 1 ? $", {scu * 1000:0.##}m SCU" : $", {scu:0.##} SCU" : ""),
        ScreenKind.Refinery when frame.Refinery is { } refinery =>
            RefinerySummary(refinery),
        ScreenKind.Freight when frame.Freight is { } freight =>
            FreightSummary(freight),
        ScreenKind.MobiGlas => "a mobiGlas screen this app cannot read yet",
        // 17 of this install's screenshots read no text at all, every one the
        // same black picture the game saved before the scene drew (1.2.24).
        ScreenKind.Unknown when !frame.Lines.Any(l => l.Count(char.IsLetter) >= 3) =>
            "no text on it at all - a black or loading screen reads like this",
        _ => "nothing this app knows how to read",
    };

    /// <summary>
    /// "the freight elevator: 2 contracts to hand over, crates of 8, 2 and 1
    /// SCU" or "the freight elevator, 94/1120 SCU on the elevator, cargo for
    /// 1 contract". Sizes, never a number of crates: the count does not read
    /// (see FreightCrates).
    /// </summary>
    private static string FreightSummary(FreightReading freight)
    {
        var contracts = freight.Orders.Count == 1 ? "1 contract" : $"{freight.Orders.Count} contracts";

        if (freight.Stage == "confirm")
        {
            var sizes = freight.Orders.SelectMany(o => o.Crates).Select(c => c.SizeScu)
                .Distinct().OrderDescending().Select(n => n.ToString()).ToList();
            return $"the freight elevator: {contracts} to hand over"
                + (sizes.Count switch
                {
                    0 => "",
                    1 => $", crates of {sizes[0]} SCU",
                    _ => $", crates of {string.Join(", ", sizes[..^1])} and {sizes[^1]} SCU",
                });
        }

        return "the freight elevator"
            + (freight.Elevator is { } e ? $", {e.Used}/{e.Total} SCU on the elevator" : "")
            + (freight.Orders.Count > 0 ? $", cargo for {contracts}" : "");
    }

    private static string RefinerySummary(RefineryReading refinery)
    {
        var at = refinery.Station ?? "a refinery";
        var time = refinery.Seconds is { } s ? RefineryClock(s) : null;

        return refinery.Stage switch
        {
            "processing" =>
                $"a refinery order running at {at}"
                + (refinery.Lots.Sum(l => l.Yield ?? 0) is > 0 and var back ? $", {back} cSCU coming back" : "")
                + (time is null ? "" : $", {time} left"),
            "setup" =>
                $"a refinery quote at {at}"
                + (refinery.Method is { } m ? $": {m}" : refinery.MethodRead is null ? ", no method picked" : $": {refinery.MethodRead}")
                + (refinery.ToRefine is > 0 and var n ? $", {n} cSCU in" : "")
                + (refinery.Cost is { } c ? $", {c:#,0.##} aUEC" : "")
                + (time is null ? "" : $", {time}"),
            _ => $"the refinery at {at}" + (refinery.CapacityPercent is { } p ? $", at {p:#,0}% capacity" : ""),
        };
    }

    /// <summary>The terminal's own way of writing a duration: "6m 35s", "2h 10m".</summary>
    internal static string RefineryClock(int seconds)
    {
        var t = TimeSpan.FromSeconds(seconds);
        return t.TotalHours >= 1
            ? $"{(int)t.TotalHours}h {t.Minutes}m"
            : $"{t.Minutes}m {t.Seconds}s";
    }

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

/// <summary>
/// The target panel, read up close: the patch where it was last seen, or
/// where the whole frame's lines put it.
/// </summary>
/// <remarks>
/// Twice the size, no shear - the treatment the mining panel reads best at,
/// and the panel is the same thin HUD face. Read alone, a patch a tenth of the
/// frame costs a fraction of the whole read, which is what lets the live loop
/// look at a fight every second. Static over the reader so the CLI's
/// <c>--target</c> check runs exactly this against recorded frames.
/// </remarks>
public static class TargetLook
{
    /// <summary>Null panel when there is none to read; the caller looks at the whole frame again before it gives up.</summary>
    public static async Task<(TargetReading? Panel, TargetStance Stance)> ReadAsync(
        IScreenReader reader, string path, IReadOnlyList<ScreenTextLine>? whole, ScreenPatch? known,
        int width, int height, CancellationToken token = default)
    {
        var stance = TargetStance.Unread;
        if (whole is not null && TargetPanel.LockLabel(whole) is { } label)
            stance = await StanceAsync(reader, path, label, token);

        var patch = known ?? (whole is null ? null : TargetPanel.Locate(whole, width, height));
        if (patch is null)
            return (null, stance);

        // The label is thin text over the world, and the whole read - Windows'
        // engine above all - mostly misses it. With a lock held, the middle of
        // the screen is read once more on whole-frame ticks, where a target
        // being fought sits; a label off to the side stays unread.
        if (whole is not null && stance == TargetStance.Unread)
        {
            var middle = new ScreenPatch(width * 0.25, height * 0.15, width * 0.5, height * 0.7);
            try
            {
                var centre = await reader.ReadAsync(path, middle, new ScreenTreatment(Scale: 1, Shear: 0), token);
                if (TargetPanel.LockLabel(centre) is { } found)
                    stance = await StanceAsync(reader, path, found, token);
            }
            catch (Exception e) when (e is not ScreenEngineUnavailableException && !token.IsCancellationRequested)
            {
                // The stance stays unread; the panel is still worth reading.
            }
        }

        IReadOnlyList<ScreenTextLine> close;
        try
        {
            close = await reader.ReadAsync(path, patch, new ScreenTreatment(Scale: 2, Shear: 0), token);
        }
        catch (Exception e) when (e is not ScreenEngineUnavailableException && !token.IsCancellationRequested)
        {
            return (null, stance);
        }

        // Re-centred on what this read found, so a panel that drifts with the
        // pilot's head is followed rather than lost off the patch's edge.
        var panel = TargetPanel.Read(close, height);
        if (panel is not null)
        {
            panel = panel with { Panel = TargetPanel.Locate(close, width, height) ?? patch };
        }
        else if (whole is not null)
        {
            panel = TargetPanel.Read(whole, height, patch);
        }

        // A faction that only ever shoots is hostile whatever colour the label
        // was, or whether it was read at all.
        if (stance == TargetStance.Unread && panel?.Faction is { } faction && TargetPanel.IsHostileFaction(faction))
            stance = TargetStance.Hostile;

        return (panel, stance);
    }

    private static async Task<TargetStance> StanceAsync(IScreenReader reader, string path, ScreenTextLine label, CancellationToken token)
    {
        var box = new ScreenPatch(label.Left, label.Top,
            Math.Max(label.Height * 0.6 * label.Text.Length, label.Height), label.Height);
        return TargetPanel.Stance(await reader.PixelsAsync(path, box, token), label);
    }
}
