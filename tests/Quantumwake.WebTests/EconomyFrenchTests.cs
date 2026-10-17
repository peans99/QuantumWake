namespace Quantumwake.WebTests;

/// <summary>
/// The Economy pages, the Map and the Log in French.
/// </summary>
/// <remarks>
/// Commodities, terminals, places, shops and the game's facility names stay as
/// written - they are what UEX and the mobiGlas are keyed on. What the server
/// sends as a fixed word (a ledger kind, a security level) is translated where
/// it is drawn, and stays English wherever the page keys on it.
/// </remarks>
public class EconomyFrenchTests
{
    [Fact]
    public void The_ledger_reads_in_french_and_keeps_filtering_on_the_servers_kinds()
    {
        var page = new Page("fr");
        page.Serve("/api/ledger?days=0", """
            [{"at":"2026-08-20T09:00:00+00:00","kind":"Cargo sold","what":"Agricium","where":"Port Tressler",
              "shop":"TDD","amount":288000,"confirmed":false,"running":288000},
             {"at":"2026-08-19T09:00:00+00:00","kind":"Item bought","what":"MedPen","where":"Lorville",
              "shop":"Cubby Blast","amount":-4000,"confirmed":true,"running":284000},
             {"at":"2026-08-18T09:00:00+00:00","kind":"Contract paid","what":"Contract payout","where":"Lorville",
              "shop":"","amount":12000,"confirmed":true,"running":272000}]
            """);
        page.Serve("/api/ledger/wallet", """{"read":null}""");
        page.Do("__dom.node('#ledger-period').value = '0'; await loadLedger();");

        var summary = page.NodeText("#ledger-summary");
        var table = page.NodeText("#ledger-table tbody");

        Assert.Contains("Entrées", summary);
        Assert.Contains("Gain net", summary);
        Assert.Contains("Cargaison vendue", table);
        Assert.Contains("Objet acheté", table);
        Assert.Contains("Paiement de contrat", table);
        Assert.Contains("Agricium", table);
        Assert.Contains("Cubby Blast", table);

        // The button reads French, the filter it sets is the server's word.
        Assert.Equal("Item bought", page.Text("__dom.node('#ledger-kinds').descendants()"
            + ".find(n => n.tagName === 'button' && n.textContent === 'Objet acheté').dataset.kind"));
        Assert.Contains("Argent disponible", page.NodeText("#ledger-wallet"));
    }

    [Fact]
    public void Cargo_and_spending_tiles_read_in_french()
    {
        var page = new Page("fr");
        page.Do("""
            renderCommodities([
              { at: '2026-08-20T09:00:00Z', isSell: true, amount: 40000, scu: 8, unitPrice: 5000, uexBestSell: 0,
                place: 'Port Tressler', commodity: 'Agricium' }]);
            renderSpending({ net: 36000, income: 40000, spend: 4000, commoditySpend: 0, purchaseCount: 1, tradeCount: 1,
              tradeShops: [], shops: [{ name: 'Cubby Blast', count: 2 }], items: [] });
            """);

        Assert.Contains("Recettes", page.NodeText("#cargo-summary"));
        Assert.Contains("SCU vendus", page.NodeText("#cargo-summary"));
        Assert.Contains("Vendu", page.NodeText("#cargo-table tbody"));
        Assert.Contains("Agricium", page.NodeText("#cargo-table tbody"));
        Assert.Contains("Revenus de cargaison", page.NodeText("#spend-summary"));
        Assert.Contains("Gain net", page.NodeText("#spend-summary"));
        Assert.Contains("2 achats", page.NodeText("#shops-chart"));
        Assert.Contains("Aucune vente de marchandise", page.NodeText("#trade-chart"));
    }

    [Fact]
    public void The_log_files_pane_reads_in_french_and_keeps_the_file_names()
    {
        var page = new Page("fr");
        page.Do("""
            renderScanFiles({ root: 'C:/SC', onDisk: 2, bytes: 4194304, files: [
              { name: 'Game.log', path: 'C:/SC/Game.log', state: 'changed', live: true, bytes: 2097152,
                writtenAt: '2026-08-20T09:00:00Z', startedAt: null, handle: null, scannedAt: null, sessionId: null },
              { name: 'Game Build(1) 19 Aug 26.log', path: 'C:/SC/logbackups/x.log', state: 'current', live: false,
                bytes: 2097152, writtenAt: '2026-08-19T09:00:00Z', startedAt: null, handle: 'Pilot', scannedAt: null, sessionId: 's1' }] });
            """);

        var summary = page.NodeText("#scan-files-summary");
        var table = page.NodeText("#scan-files-table tbody");

        Assert.Contains("2 dans le dossier", summary);
        Assert.Contains("allongé", table);
        Assert.Contains("lu", table);
        Assert.Contains("en direct", table);
        Assert.Contains("avant 0.14.10", table);
        Assert.Contains("Game Build(1) 19 Aug 26.log", table);
    }

    [Fact]
    public void A_refinery_reading_on_the_log_reads_in_french_with_the_station_as_written()
    {
        var page = new Page("fr");
        page.Do("""
            const box = document.createElement('div');
            renderSighting(box, { kind: 'Refinery', refinery: {
              station: 'MIC-L5 Modern Icarus Station', stage: 'setup', method: 'Pyrometric Chromalysis',
              toRefine: 182, cost: 121, seconds: 395, capacityPercent: 54,
              lots: [{ read: 'SILICON (RAW)', mineral: 'Raw Silicon', quality: 510, quantity: 142, yield: 64 }] } });
            globalThis.__qwBox = box;
            """);

        var text = page.Text("__qwBox.textContent");
        Assert.Contains("un devis de raffinerie à MIC-L5 Modern Icarus Station", text);
        Assert.Contains("182 cSCU en entrée", text);
        Assert.Contains("qualité 510", text);
        Assert.Contains("→ 64 cSCU en retour", text);
        Assert.Contains("Pyrometric Chromalysis", text);
        Assert.Contains("Commandes de raffinerie", text);
    }

    [Fact]
    public void The_maps_closest_facility_answer_reads_in_french_and_keeps_the_games_facility_name()
    {
        var page = new Page("fr");
        page.Do("""
            atlas = [
              { rawId: 'here', name: 'Home', system: 'Stanton', body: 'Origin', kind: 'City', visits: 1 },
              { rawId: 'near', name: 'Near refinery', system: 'Stanton', body: 'Near', kind: 'RestStop', visits: 0 }
            ];
            hereId = 'here';
            bodyPositions = { stanton: { Origin: { x: 0, y: 0 }, Near: { x: 3, y: 4 } } };
            amenitiesByPlace.set('near refinery', ['Refinery']);
            """);

        var message = page.Text("closestMapFacilities('Refinery').message");
        Assert.Contains("la plus proche de Home", message);
        Assert.Contains("Refinery", message);
        Assert.Contains("d’après les positions des corps", message.ToLowerInvariant());
        Assert.Equal("Aire de repos", page.Text("placeKindWord('RestStop')"));
        Assert.Equal("Ville", page.Text("placeKindWord('City')"));
    }

    [Fact]
    public void An_empty_flight_plan_panel_reads_in_french()
    {
        var page = new Page("fr");
        page.Do("trips = []; showTripPanel();");

        Assert.Equal("Plan de vol", page.NodeText("#cargo-kicker"));
        Assert.Equal("Pas encore de plan", page.NodeText("#cargo-title"));
        Assert.Contains("Double-cliquez sur un lieu", page.NodeText("#cargo-body"));
    }
}
