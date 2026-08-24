namespace Quantumwake.WebTests;

/// <summary>POIs are local annotations over atlas places, not new map data.</summary>
public class MapNotesTests
{
    [Fact]
    public void A_saved_note_marks_its_place_and_appears_on_its_detail_card()
    {
        var page = new Page();
        page.Serve("/api/map-notes", """
            [{"id":"n1","placeId":"RR_MIC_LEO","place":"Port Tressler",
              "title":"Cargo elevator","note":"Use the rear lift","tags":["cargo","quiet"],
              "createdAt":"2026-08-22T09:00:00Z","updatedAt":"2026-08-22T09:00:00Z"}]
            """);

        page.Do("await loadMapNotes(); mapInfoLocation = { rawId: 'RR_MIC_LEO', name: 'Port Tressler' }; renderMapInfoNotes(mapInfoLocation);");

        Assert.True(page.Truth("mapNoteIds.has('RR_MIC_LEO')"));
        Assert.Contains("Cargo elevator", page.NodeText("#map-info-notes"));
        Assert.Contains("Use the rear lift", page.NodeText("#map-info-notes"));
        Assert.Contains("cargo · quiet", page.NodeText("#map-info-notes"));
    }
}
