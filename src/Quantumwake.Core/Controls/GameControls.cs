using Quantumwake.Core.GameData;

namespace Quantumwake.Core.Controls;

/// <summary>One of the layouts the game ships for a known stick, as read from the archive.</summary>
/// <param name="File">The entry's file name - <c>layout_hotas_warthog.xml</c>.</param>
public sealed record ReferenceLayout(string File, ControlProfile Profile);

/// <summary>The keybinding catalogue and the reference layouts, from the archive.</summary>
public sealed record GameControlsData(ControlCatalogue Catalogue, List<ReferenceLayout> Layouts)
{
    public static readonly GameControlsData Empty = new(ControlCatalogue.Empty, []);

    /// <summary>The layouts that name this product GUID among their devices.</summary>
    public IEnumerable<ReferenceLayout> LayoutsFor(string guid) =>
        Layouts.Where(l => l.Profile.Devices.Any(d => string.Equals(d.Guid, guid, StringComparison.OrdinalIgnoreCase)));
}

/// <summary>
/// Reads the keybinding side of the archive: <c>Data\Libs\Config\defaultProfile.xml</c>
/// and every layout under <c>Data\Libs\Config\Mappings\</c>.
/// </summary>
/// <remarks>
/// 17 layouts on this install (Alpha 4.10): Warthog with and without pedals,
/// X52, X52 Pro, X55, X56, G940, T.16000M single, dual and with TWCS,
/// T.Flight HOTAS X, VKB SCG and GNX, GameGlass, a keyboard mod-swap, an
/// X-roll variant and a blank. The ones that name their devices carry the
/// product GUID, which is how a layout is offered to a pilot whose stick it
/// is for.
/// </remarks>
public static class GameControls
{
    public const string CatalogueEntry = @"Data\Libs\Config\defaultProfile.xml";
    public const string LayoutFolder = @"Data\Libs\Config\Mappings\";

    public static GameControlsData Read(P4kArchive p4k, IReadOnlyDictionary<string, string> text)
    {
        var catalogue = ControlCatalogue.Empty;
        if (p4k.TryRead(CatalogueEntry) is { } profile)
        {
            try { catalogue = ControlCatalogue.Parse(profile, text); }
            catch (Exception e) when (e is InvalidDataException or System.Xml.XmlException) { /* a catalogue that will not read is no catalogue; the page says so */ }
        }

        var layouts = new List<ReferenceLayout>();
        foreach (var (path, _) in p4k.List(LayoutFolder))
        {
            if (!path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) || p4k.TryRead(path) is not { } bytes) continue;
            try { layouts.Add(new ReferenceLayout(P4kArchive.EntryFileName(path), ControlProfile.Parse(bytes))); }
            catch (Exception e) when (e is InvalidDataException or System.Xml.XmlException) { /* one bad layout does not lose the rest */ }
        }
        layouts.Sort((a, b) => string.Compare(a.File, b.File, StringComparison.OrdinalIgnoreCase));

        return new GameControlsData(catalogue, layouts);
    }
}
