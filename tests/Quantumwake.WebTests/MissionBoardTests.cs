namespace Quantumwake.WebTests;

/// <summary>
/// Which 5.0 board a contract is posted on (1.3.5), on Reference → Missions and
/// in the mission window.
/// </summary>
/// <remarks>
/// Shapes from the 5.0 PTU (5.0.202.46035): 248 released <c>CB_</c> contracts
/// are on Career boards, 63 <c>NB_</c> contracts on Notice boards, the rest on
/// the mobiGlas alone. A URW contract names its guild because it pays URW
/// standing.
/// </remarks>
public class MissionBoardTests
{
    private const string Rows = "__dom.node('#missions-table tbody').querySelectorAll('tr')";

    private const string Catalogue = """
        {"ready":true,"count":3,"missions":[
          {"id":"cb","name":"CB_URW_S_Shubin_RG_SMine_Nyx_MediumQuantity_Ice_Hub","title":"Ice Mining","loggedTitle":null,
           "contractor":"Shubin Interstellar","type":"Mining","areas":["Nyx"],"systems":["Nyx"],"rep":100,"blueprint":null,
           "reward":null,"rank":null,"illegal":false,"unreleased":false,"board":{"kind":"Career","guild":"United Resource Workers"},
           "taken":0,"completed":0},
          {"id":"nb","name":"NB_L_Local_Delivery_1","title":"Local Delivery","loggedTitle":null,
           "contractor":"Nyx I","type":"Delivery","areas":["Nyx"],"systems":["Nyx"],"rep":null,"blueprint":null,
           "reward":null,"rank":null,"illegal":false,"unreleased":false,"board":{"kind":"Notice","guild":null},
           "taken":0,"completed":0},
          {"id":"rw","name":"RedWind_Hauling_1","title":"Haul","loggedTitle":null,
           "contractor":"Red Wind Linehaul","type":"Hauling","areas":["Pyro"],"systems":["Pyro"],"rep":1000,"blueprint":null,
           "reward":null,"rank":null,"illegal":false,"unreleased":false,"board":null,"taken":0,"completed":0}]}
        """;

    private static Page Listed(string catalogue = Catalogue)
    {
        var page = new Page();
        page.Serve("/api/reference/missions", catalogue);
        page.Do("await loadMissionsRef();");
        return page;
    }

    [Fact]
    public void A_career_contract_names_its_guild_s_board_and_a_notice_one_its_board()
    {
        var text = Listed().Text($"Array.from({Rows}).map(r => r.textContent).join('|')");

        Assert.Contains("Mining · Career board · United Resource Workers", text);
        Assert.Contains("Delivery · Notice board", text);
    }

    [Fact]
    public void The_board_filter_narrows_to_one_board_or_the_mobiglas()
    {
        var page = Listed();
        Assert.False(page.Text("String(__dom.node('#missions-board').hidden)") == "true");

        page.Do("__dom.node('#missions-board').value = 'Career'; renderMissionsRef();");
        Assert.Equal(1, page.Count($"{Rows}.length"));

        page.Do("__dom.node('#missions-board').value = 'mobiGlas'; renderMissionsRef();");
        Assert.Equal(1, page.Count($"{Rows}.length"));
        Assert.Contains("Red Wind", page.Text($"{Rows}[0].textContent"));
    }

    [Fact]
    public void Before_five_every_contract_is_on_the_mobiglas_and_the_filter_stays_hidden()
    {
        var page = Listed(Catalogue.Replace("\"board\":{\"kind\":\"Career\",\"guild\":\"United Resource Workers\"}", "\"board\":null")
            .Replace("\"board\":{\"kind\":\"Notice\",\"guild\":null}", "\"board\":null"));

        Assert.Equal("true", page.Text("String(__dom.node('#missions-board').hidden)"));
        Assert.Equal(3, page.Count($"{Rows}.length"));
    }

    [Fact]
    public void The_mission_window_says_whose_board_it_is_posted_on()
    {
        var page = new Page();
        page.Serve("/api/reference/mission?id=cb", MissionReferenceTests.Mission
            .Replace("\"unreleased\":false,", "\"unreleased\":false,\"board\":{\"kind\":\"Career\",\"guild\":\"United Resource Workers\"},"));
        page.Do("await openRefDetail('mission', 'cb');");

        Assert.Contains("Posted ona United Resource Workers Career board", page.NodeText("#ref-modal-body"));
    }
}
