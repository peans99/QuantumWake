using System.Text;
using System.Text.RegularExpressions;

namespace Quantumwake.Core.GameData;

/// <summary>One commodity a cargo manifest can load, and its weight among the others.</summary>
public sealed record GameManifestGood(string Commodity, double Probability);

/// <summary>
/// A <c>CargoManifest</c>: how full an NPC ship's hold is spawned, and with what.
/// </summary>
/// <param name="Name">The record's own name, <c>Criminal_25_High_001</c>.</param>
/// <param name="Mode">The game's fill rule, <c>Even</c>, <c>Random</c> or <c>Custom</c>.</param>
/// <param name="MinFill">The emptiest the hold is spawned, as a fraction of it.</param>
/// <param name="MaxFill">The fullest.</param>
public sealed record GameCargoManifest(string Name, string Mode, double MinFill, double MaxFill, List<GameManifestGood> Goods);

/// <summary>One kind of ship in a wave.</summary>
/// <param name="Ship">The NPC ship's class, <c>DRAK_Cutlass_Black_PU_AI_NT</c>; null when the files name only who flies it.</param>
/// <param name="Flown">The faction the member is named for, in English, when the record gives one: "Crusader Security".</param>
/// <param name="Amount">How many, as the record writes it.</param>
/// <param name="MinAmount">The fewest, when the record gives a range rather than a count.</param>
/// <param name="MaxAmount">The most.</param>
/// <param name="Skill">The pilot skill record, <c>HumanPilot60</c>, when the member overrides it.</param>
/// <param name="Cargo">The manifests its hold may be spawned with, by name.</param>
public sealed record GameEncounterShip(
    string? Ship, string? Flown, int Amount, int? MinAmount, int? MaxAmount, string? Skill, List<string> Cargo);

/// <summary>One wave of an encounter: the ships that arrive together.</summary>
public sealed record GameEncounterWave(int Id, List<GameEncounterShip> Ships);

/// <summary>
/// An <c>AIWaveCollection</c>: a group of NPC ships the game spawns in waves.
/// </summary>
/// <param name="Name">The record's name, <c>Bounty_Wave_Hard_Stanton2</c>.</param>
/// <param name="Folder">
/// Where the game files the record, below <c>aiwavecollection/</c>:
/// <c>pu/mission/ai_bounty</c>. The game's own filing, not a link to a mission.
/// </param>
/// <param name="Scripts">
/// The mission scripts that name this collection by its id, as paths below
/// <c>Libs/Subsumption/</c>. Empty for most: see <see cref="GameEncounters"/>.
/// </param>
public sealed record GameEncounter(string Name, string Folder, List<string> Scripts, List<GameEncounterWave> Waves)
{
    /// <summary>Arena Commander's swarm modes, filed under <c>ea/</c>: not the universe.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool ArenaCommander => Folder.Equals("ea", StringComparison.OrdinalIgnoreCase)
        || Folder.StartsWith("ea/", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Every encounter, every manifest they load, and the manifests nothing loads.</summary>
public sealed class GameEncounterData
{
    public List<GameEncounter> Encounters { get; init; } = [];

    /// <summary>Every <c>CargoManifest</c> in the install, used or not, by name.</summary>
    public Dictionary<string, GameCargoManifest> Manifests { get; init; } = new(StringComparer.Ordinal);

    /// <summary>How many mission scripts were searched for an encounter's id; 0 when the archive was not read.</summary>
    public int ScriptsRead { get; init; }

    public static GameEncounterData Empty { get; } = new();

    /// <summary>The manifests no encounter loads, by name.</summary>
    public IEnumerable<GameCargoManifest> Unused()
    {
        var used = Encounters.SelectMany(e => e.Waves).SelectMany(w => w.Ships).SelectMany(s => s.Cargo).ToHashSet(StringComparer.Ordinal);
        return Manifests.Values.Where(m => !used.Contains(m.Name)).OrderBy(m => m.Name, StringComparer.Ordinal);
    }
}

/// <summary>
/// Reads the NPC encounters the install describes, what each ship in them is
/// spawned carrying, and which mission scripts name them.
/// </summary>
/// <remarks>
/// <para>
/// An <c>AIWaveCollection</c> is a list of waves, each a list of members: a ship
/// class, an amount, a pilot skill and the <c>CargoManifest</c>s its hold may be
/// spawned with. Measured on 4.10.2: 184 collections; 370 members in 88 of them
/// carry a manifest, and only 17 of the 82 manifests are carried at all, every
/// one at 1 to 25% of the hold - food and medical supplies on civilians,
/// distilled spirits on criminals and Nine Tails, nothing on security.
/// </para>
/// <para>
/// Nothing in the DataCore points at a collection: a raw scan of its bytes finds
/// each id once, as its own record, and no field's type is the collection's.
/// What does name them is the mission scripts - the Subsumption CryXml files
/// under <c>Libs/Subsumption</c> - by id. 48 of the 184 are named there: 32 by
/// the Stanton law missions' wave spawning (wanted policing, security patrols,
/// bounty hunters), 4 by the service-beacon combat assist, 1 by the derelict
/// freighter, and 11 by Arena Commander's swarm. The other 136 - the bounty,
/// steal, recovery and assassination waves among them - are named by no script
/// the client ships, and a search for the prefixes a script would build their
/// names from ("Bounty_Wave") finds none either: they are picked on the server.
/// For those the record's own folder and name are all the files say, and the
/// page labels them that way.
/// </para>
/// <para>
/// 65 manifests are carried by nothing the client has - the blockade runners'
/// 45 to 95% loads, the legal, illegal and mixed value tiers, the ship heist.
/// The one other field that can name a manifest, <c>CargoControllerParams.cargoManifestOverride</c>,
/// is empty on all 1,144 entities that have one.
/// </para>
/// </remarks>
public static partial class GameEncounters
{
    /// <summary>Where the mission scripts are in the archive.</summary>
    public const string ScriptFolder = @"Data\Libs\Subsumption\";

    private const string RecordFolder = "aiwavecollection/";

    [GeneratedRegex(@"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", RegexOptions.CultureInvariant)]
    private static partial Regex GuidText();

    /// <param name="commodities">ResourceType id to English name, as <see cref="GameCommodities"/> reads them.</param>
    /// <param name="p4k">The archive, for the mission scripts; null leaves every encounter unlinked.</param>
    public static GameEncounterData Read(
        DataCore core, IReadOnlyDictionary<string, string> text, IReadOnlyDictionary<string, string> commodities, P4kArchive? p4k)
    {
        var collection = core.StructIndexOf("AIWaveCollection");
        var manifest = core.StructIndexOf("CargoManifest");
        if (collection < 0) return GameEncounterData.Empty;

        var names = new Dictionary<Guid, string>();
        var collections = new List<DataRecord>();
        var manifests = new Dictionary<string, GameCargoManifest>(StringComparer.Ordinal);

        foreach (var record in core.Records())
        {
            if (record.StructIndex == collection) collections.Add(record);
            else if (record.StructIndex == manifest)
            {
                names.TryAdd(record.Hash, Bare(record.Name));
                if (Manifest(core, commodities, record) is { } m) manifests[m.Name] = m;
            }
            else if (record.Name.StartsWith("EntityClassDefinition.", StringComparison.Ordinal)
                || record.Name.StartsWith("SkillDefinitions.", StringComparison.Ordinal))
                names.TryAdd(record.Hash, Bare(record.Name));
        }

        var (scripts, read) = p4k is null
            ? (new Dictionary<Guid, HashSet<string>>(), 0)
            : Scripts(p4k, collections.Select(c => c.Hash).ToHashSet());

        var encounters = new List<GameEncounter>();
        foreach (var record in collections)
        {
            var at = core.InstanceAt(record, record.VariantIndex);
            var waves = new List<GameEncounterWave>();
            foreach (var wave in core.ClassArrayAt(at, record.StructIndex, "waves"))
            {
                var wa = core.InstanceAt(wave);
                var ships = new List<GameEncounterShip>();
                foreach (var member in core.ClassArrayAt(wa, wave.StructIndex, "members"))
                    ships.Add(Member(core, text, names, member));
                waves.Add(new GameEncounterWave(core.Int32At(wa, wave.StructIndex, "id") ?? waves.Count + 1, ships));
            }

            encounters.Add(new GameEncounter(
                Bare(record.Name),
                Folder(record.FileName),
                scripts.TryGetValue(record.Hash, out var by) ? [.. by.Order(StringComparer.Ordinal)] : [],
                waves));
        }

        return new GameEncounterData
        {
            Encounters = [.. encounters.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)],
            Manifests = manifests,
            ScriptsRead = read,
        };
    }

    private static GameEncounterShip Member(
        DataCore core, IReadOnlyDictionary<string, string> text, IReadOnlyDictionary<Guid, string> names, DataCore.Pointer member)
    {
        var at = core.InstanceAt(member);
        var si = member.StructIndex;

        string? Named(string field) =>
            core.ReferenceAt(at, si, field) is { } id && id != Guid.Empty && names.TryGetValue(id, out var n) ? n : null;

        // A member that names no ship carries only who flies it - "Crusader
        // Security" - and the mission picks the hull; the name is a text key.
        var flown = core.StringAt(at, si, "name") is { Length: > 1 } key && text.TryGetValue(key.TrimStart('@'), out var english)
            && english.Length > 0 ? english.Trim() : null;

        // -1 is the record's "not set": most members give a plain amount.
        static int? Set(int? value) => value is >= 0 ? value : null;

        return new GameEncounterShip(
            Named("entityClassDefinition"),
            flown,
            Math.Max(0, core.Int32At(at, si, "amount") ?? 0),
            Set(core.Int32At(at, si, "minAmount")),
            Set(core.Int32At(at, si, "maxAmount")),
            Named("skillsetOverride"),
            [.. core.ReferenceArrayAt(at, si, "cargoManifests").Select(g => names.TryGetValue(g, out var n) ? n : null).OfType<string>()]);
    }

    private static GameCargoManifest? Manifest(DataCore core, IReadOnlyDictionary<string, string> commodities, DataRecord record)
    {
        var at = core.InstanceAt(record, record.VariantIndex);
        if (core.PointerAt(at, record.StructIndex, "cargoFillCapacity") is not { StructIndex: >= 0 } fill) return null;

        var fa = core.InstanceAt(fill);
        var goods = core.ClassArrayAt(fa, fill.StructIndex, "resources")
            .Select(r =>
            {
                var ra = core.InstanceAt(r);
                var id = core.ReferenceAt(ra, r.StructIndex, "resource");
                // One legal-cargo manifest names a commodity whose English is the
                // table's "<= PLACEHOLDER =>", which is no commodity at all.
                return id is { } g && commodities.TryGetValue(g.ToString(), out var name) && !name.StartsWith("<=", StringComparison.Ordinal)
                    ? new GameManifestGood(name, Math.Round(core.SingleAt(ra, r.StructIndex, "probability") ?? 0, 3))
                    : null;
            })
            .OfType<GameManifestGood>()
            .ToList();

        const string prefix = "CargoFillCapacityValue_";
        var mode = core.StructName(fill.StructIndex);
        return new GameCargoManifest(
            Bare(record.Name),
            mode.StartsWith(prefix, StringComparison.Ordinal) ? mode[prefix.Length..] : mode,
            Math.Round(core.SingleAt(fa, fill.StructIndex, "minCapacityRange") ?? 0, 3),
            Math.Round(core.SingleAt(fa, fill.StructIndex, "maxCapacityRange") ?? 0, 3),
            goods);
    }

    /// <summary>
    /// Which mission scripts carry each collection's id. The scripts are CryXml
    /// whose string table holds ids as text, so every id-shaped run is collected
    /// once per file and looked up, rather than searching each file 184 times.
    /// </summary>
    private static (Dictionary<Guid, HashSet<string>> ById, int Read) Scripts(P4kArchive p4k, HashSet<Guid> wanted)
    {
        var byId = new Dictionary<Guid, HashSet<string>>();
        var read = 0;
        foreach (var entry in p4k.Locate(ScriptFolder))
        {
            if (p4k.TryRead(entry) is not { } bytes) continue;
            read++;
            var path = entry.Path[ScriptFolder.Length..].Replace('\\', '/');
            foreach (Match m in GuidText().Matches(Encoding.Latin1.GetString(bytes)))
                if (Guid.TryParse(m.Value, out var id) && wanted.Contains(id))
                    (byId.TryGetValue(id, out var set) ? set : byId[id] = new(StringComparer.Ordinal)).Add(path);
        }
        return (byId, read);
    }

    /// <summary>
    /// The hold an NPC ship is spawned with: its own grids when the install
    /// gives it some, else those of the longest hull class it is built on -
    /// <c>DRAK_Cutlass_Black_PU_AI_NT</c> on <c>DRAK_Cutlass_Black</c>, never on
    /// <c>DRAK_Cutlass</c> when the longer one exists. Null when neither has a hold.
    /// </summary>
    public static (string Hull, double Scu)? Hold(string? ship, IReadOnlyDictionary<string, InstallCargoHull> hulls)
    {
        if (ship is not { Length: > 0 }) return null;
        if (hulls.TryGetValue(ship, out var own) && own.Scu > 0) return (ship, own.Scu);

        var hull = hulls.Keys
            .Where(h => ship.StartsWith(h + "_", StringComparison.OrdinalIgnoreCase))
            .MaxBy(h => h.Length);
        return hull is not null && hulls[hull].Scu > 0 ? (hull, hulls[hull].Scu) : null;
    }

    /// <summary><c>libs/foundry/records/aiwavecollection/pu/mission/ai_bounty/x.xml</c> to <c>pu/mission/ai_bounty</c>.</summary>
    internal static string Folder(string fileName)
    {
        var path = fileName.Replace('\\', '/');
        var at = path.IndexOf(RecordFolder, StringComparison.OrdinalIgnoreCase);
        if (at >= 0) path = path[(at + RecordFolder.Length)..];
        var slash = path.LastIndexOf('/');
        return slash > 0 ? path[..slash].ToLowerInvariant() : "";
    }

    private static string Bare(string name) => name.Contains('.') ? name[(name.IndexOf('.') + 1)..] : name;
}
