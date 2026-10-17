namespace Quantumwake.WebTests;

/// <summary>
/// What a screen reading says, in French.
/// </summary>
/// <remarks>
/// A reading's summary and checks are written in English when the screenshot
/// is read and stored that way, so the page translates them by shape. These
/// are the shapes as the server writes them; a change to its wording shows up
/// here as English.
/// </remarks>
public class ScreenFrenchTests
{
    [Theory]
    [InlineData("1,912,941 aUEC from the ledger", "1,912,941 aUEC d’après les comptes")]
    [InlineData("948,628 aUEC left without a line in the log since 8 Oct 03:02", "948,628 aUEC partis sans ligne dans le journal depuis 8 Oct 03:02")]
    [InlineData("nothing - no session covers that moment", "rien - aucune session ne couvre ce moment")]
    [InlineData("the Origin 315p as it ships", "Origin 315p tel que livré")]
    [InlineData("1 part named, 2 ports empty, 1 port with nothing read under it, 6 not matched", "1 pièce nommée, 2 emplacements vides, 1 emplacement sans rien de lu dessous, 6 non associées")]
    [InlineData("3 ships listed: Corsair at Hurston, Hermes (1 name did not read)", "3 vaisseaux listés : Corsair à Hurston, Hermes (1 nom illisible)")]
    [InlineData("4 accepted of 10: MEMBER I STELLAR MEDIUM HAUL", "acceptés : 4 sur 10 : MEMBER I STELLAR MEDIUM HAUL")]
    [InlineData("on screen but not in the logs: A, B; the tab says 5, the logs say 4", "à l’écran mais pas dans les journaux : A, B; l’onglet dit 5, les journaux disent 4")]
    [InlineData("Corsair, 6 parts named", "Corsair, 6 pièces nommées")]
    [InlineData("a refinery order running at a refinery, 40 cSCU coming back, 6m 35s left", "une commande de raffinage en cours à une raffinerie, 40 cSCU en retour, reste 6m 35s")]
    [InlineData("The loadout estimate at the Fleet Manager: 24 items, replacement fee 22,506 aUEC.", "L’estimation d’équipement au Fleet Manager : 24 objets, frais de remplacement 22,506 aUEC.")]
    [InlineData("Only showing ships and equipment located in Hurston.", "Only showing ships and equipment located in Hurston.")]
    public void A_stored_reading_is_translated_by_shape(string english, string french)
    {
        var page = new Page("fr");
        Assert.Equal(french, page.Text($"screenText({System.Text.Json.JsonSerializer.Serialize(english)})").Replace(' ', ' ').Replace(' ', ' '));
    }
}
