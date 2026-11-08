using System.IO.Compression;
using System.Text.Json;
using Quantumwake.Core.Controls;
using Quantumwake.Server;
namespace Quantumwake.Tests;
public class StreamDeckTests
{
    private static ControlCatalogue Catalogue => new([new("ship", "Ship", "FLIGHT")], [new("ship", "gear", "Landing Gear", "", "n", "", "", "", "", "")]);
    [Fact] public void Rebound_keys_replace_defaults_and_explicitly_cleared_keys_stay_unbound()
    {
        var rebound = new ControlProfile("default", null, [], [new("ship", "gear", ControlInput.Parse("kb1_lalt+g"), null, null)]);
        Assert.Equal("lalt+g", StreamDeckProfiles.Commands(Catalogue, rebound)[0].Key);
        var cleared = rebound with { Bindings = [new("ship", "gear", ControlInput.Parse("kb1_ "), null, null)] };
        Assert.NotNull(StreamDeckProfiles.Commands(Catalogue, cleared)[0].Problem);
        Assert.Equal("n", StreamDeckProfiles.Commands(Catalogue, null)[0].Key);
    }
    [Theory] [InlineData("15",5,3)] [InlineData("xl",8,4)]
    public void Every_page_fits_the_device_and_every_folder_points_to_a_page(string model, int columns, int rows)
    {
        var commands = Enumerable.Range(0,80).Select(n => new DeckCommand("ship/"+n, "Ship", "Action " + n, "n", "", null)).ToList();
        using var bytes = new MemoryStream(StreamDeckProfiles.Build(model, commands, "C:/SC/PTU/Bin64/StarCitizen.exe"));
        using var zip = new ZipArchive(bytes);
        var sent = new List<string>(); var folders = new List<string>();
        foreach (var entry in zip.Entries.Where(e => e.FullName.Contains("/Profiles/") && e.FullName.EndsWith("/manifest.json")))
        {
            using var doc = JsonDocument.Parse(entry.Open());
            var actions = doc.RootElement.GetProperty("Controllers")[0].GetProperty("Actions");
            foreach(var action in actions.EnumerateObject())
            {
                var slot=action.Name.Split(',').Select(int.Parse).ToArray(); Assert.InRange(slot[0],0,columns-1); Assert.InRange(slot[1],0,rows-1);
                var value=action.Value; var kind=value.GetProperty("UUID").GetString();
                if(kind=="com.nlebel.starcitizen.command") sent.Add(value.GetProperty("Settings").GetProperty("label").GetString()!);
                if(kind=="com.elgato.streamdeck.profile.openchild") folders.Add(value.GetProperty("Settings").GetProperty("ProfileUUID").GetString()!);
            }
        }
        Assert.Equal(80,sent.Distinct().Count());
        foreach(var folder in folders) Assert.Contains(zip.Entries,e => e.FullName.EndsWith("/Profiles/"+folder+"/manifest.json"));
    }
    [Fact] public void Mining_wheel_presets_use_the_games_keyboard_field_but_respect_removal()
    {
        var action = new ActionInfo("spaceship_mining", "v_increase_mining_throttle", "Mining power", "", "maxis_z", "", "", "", "press", "");
        var catalogue = new ControlCatalogue([], [action]);
        var wheel = StreamDeckProfiles.Commands(catalogue,null).Single(c => c.Id == "preset/Mining/mining_power_up");
        Assert.Equal("mwheel_up", wheel.Key); Assert.Null(wheel.Problem);
        var profile = new ControlProfile("default",null,[],[new(action.ActionMap,action.Name,ControlInput.Parse("kb1_ "),null,null)]);
        Assert.NotNull(StreamDeckProfiles.Commands(catalogue,profile).Single(c => c.Id == wheel.Id).Problem);
    }
    [Fact] public void Unsupported_inputs_and_activation_modes_are_explained()
    {
        Assert.Null(StreamDeckProfiles.Key(ControlInput.Parse("js1_button2")));
        Assert.Null(StreamDeckProfiles.Key(ControlInput.Parse("kb1_unknown")));
        var catalogue = Catalogue with { Actions = [Catalogue.Actions[0] with { ActivationMode = "double_tap" }] };
        Assert.NotNull(StreamDeckProfiles.Commands(catalogue,null)[0].Problem);
    }
}
