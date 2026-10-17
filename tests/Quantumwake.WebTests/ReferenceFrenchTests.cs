namespace Quantumwake.WebTests;

/// <summary>
/// The Reference pages in French: Ships, Parts, Mining and Crafting.
/// </summary>
/// <remarks>
/// Ship, item, mineral and blueprint names are the game's, and so are careers,
/// roles and item types - they are what the mobiGlas and UEX call them. The
/// app's own words around them translate, including the server's fixed words
/// the page keys on: a mining run's stage, a crack verdict, a resource kind.
/// </remarks>
public class ReferenceFrenchTests
{
    [Fact]
    public void The_ship_catalogue_reads_in_french_and_keeps_careers_and_names()
    {
        var page = new Page("fr");
        page.Serve("/api/reference/ships", """
            [{"name":"Drake Cutlass Black","career":"Combat","role":"Medium Fighter","crew":2,"isSpaceship":true,
              "expeditedCost":5000,"standardClaimTime":10,"cargoScu":46,"scmSpeed":200,"maxSpeed":1100,"shieldHp":10000,"health":20000,
              "price":null,"shops":[],"rental":null,"rentals":[]},
             {"name":"Greycat ROC","career":"Industrial","role":"Mining","crew":1,"isSpaceship":false,
              "expeditedCost":0,"standardClaimTime":0,"cargoScu":0,"scmSpeed":0,"maxSpeed":0,"shieldHp":0,"health":0,
              "price":null,"shops":[],"rental":null,"rentals":[]}]
            """);
        page.Do("await loadShipsRef();");
        var table = page.NodeText("#ships-table tbody");

        Assert.Equal("2 coques", page.NodeText("#ships-count"));
        Assert.Contains("non vendu", table);
        Assert.Contains("terrestre", table);
        Assert.Contains("Combat", table);
        Assert.Contains("Medium Fighter", table);
        Assert.Contains("Drake Cutlass Black", table);
    }

    [Fact]
    public void A_blueprint_reads_in_french_with_its_name_and_materials_as_written()
    {
        var page = new Page("fr");
        page.Serve("/api/reference/blueprints", """
            [{"output":"Metamaterial Test #146","type":"WeaponGun","grade":1,"kind":"creation",
              "craftSeconds":70,"materials":["Titanium 2 SCU"],"default":false,"rewardPools":["InterSec ResourceGathering"],
              "shopPrice":null,"owned":false,"receivedAt":null,"source":"install"}]
            """);
        page.Do("await loadCraftingRef();");
        var table = page.NodeText("#crafting-table tbody");

        Assert.Contains("Lu dans votre installation du jeu", page.NodeText("#crafting-source"));
        Assert.Contains("1 réserve de récompenses", table);
        Assert.Contains("non vendu", table);
        Assert.Contains("Metamaterial Test #146", table);
        Assert.Contains("Titanium 2 SCU", table);
    }

    [Fact]
    public void A_mining_run_reads_its_stage_and_next_step_in_french()
    {
        var page = new Page("fr");
        page.Serve("/api/mining/log", """
            [{"id":"m1","at":"2026-08-29T10:00:00Z","place":"Aberdeen","resource":"Hadanite","scu":12,
              "quality":740,"revenue":null,"stage":"Extracted","refinery":null}]
            """);
        page.Serve("/api/mining/pending", "[]");
        page.Do("await loadMiningLog();");

        var body = page.NodeText("#mining-log tbody");
        Assert.Contains("Dans votre soute", body);
        Assert.Contains("Envoyer en raffinerie", body);
        Assert.Contains("Hadanite", body);
        Assert.Contains("1 sortie", page.NodeText("#mining-log-note"));
    }

    [Fact]
    public void The_crack_verdict_reads_in_french_with_the_games_figures_kept()
    {
        var page = new Page("fr");
        page.Serve("/api/mining/model", """
            {"ready":true,
             "lasers":[{"class":"L1","name":"Arbor MH1 Mining Laser","size":1,"power":2340,"slots":1,"modifiers":{"resistance":25}}],
             "modules":[],"gadgets":[],"itemPricesKnown":true,"compositions":[],
             "minerals":[{"class":"Q","name":"Quantainium (Raw)","resistance":0.95,"instability":1000,"windowMidpoint":0.5,"windowRandomness":0.2,"explosionMultiplier":260}],
             "ships":[{"class":"MISC_Prospector","name":"MISC Prospector","heads":[{"portId":"p","size":1,"stock":"L1"}],"flown":true}],
             "rule":{"requiredWattsPerKg":0.36,"soloRatio":1.15,"gadgetRatio":0.7,"source":"scminer.rocks"}}
            """);
        page.Serve("/api/mining/crack", """
            {"verdict":{"powerDelivered":2340,"powerRequired":1800,"ratio":1.3,"verdict":"solo",
                        "effectiveResistancePercent":0,"effectiveInstabilityPercent":9.8,"windowPercent":14,"maxCrackableMassKg":6500,
                        "energyCapacity":0,"energyDecayPerSecond":0,"notes":[]},
             "matrix":[{"laser":"L1","name":"Arbor MH1 Mining Laser","power":2340,"powerDelivered":2340,"ratio":1.3,"verdict":"solo","maxCrackableMassKg":6500}]}
            """);
        page.Do("__dom.node('#crack-mass').value = '5000'; await loadCrackModel(); __dom.node('#crack-mineral').value = 'Q'; await assessCrack();");

        var verdict = page.NodeText("#crack-verdict");
        Assert.Contains("Se brise avec cet équipement", verdict);
        Assert.Contains("Résistance après équipement", verdict);
        Assert.Contains("Quantainium (Raw)", verdict);
        Assert.Contains("ici, une surcharge, c’est le rocher perdu", verdict);
        Assert.Contains("Se brise avec cet équipement", page.NodeText("#crack-matrix tbody"));
        Assert.Contains("MISC Prospector", page.NodeText("#crack-ship"));
        Assert.Contains("piloté", page.NodeText("#crack-ship"));
    }

    [Fact]
    public void A_price_age_on_an_item_card_reads_in_french()
    {
        var page = new Page("fr");
        Assert.Equal("relevé aujourd’hui", page.Text("ageWord(new Date().toISOString())"));
        Assert.Equal("relevé il y a 3 jours", page.Text("ageWord(new Date(Date.now() - 3 * 86400000 - 3600000).toISOString())"));
    }
}
