namespace Quantumwake.WebTests;

/// <summary>
/// Sentences the server writes outside screen readings, in French.
/// </summary>
/// <remarks>
/// The Garage's notes, the cargo fit's reasons, a fire mode's condition, a
/// recipe's choice of materials and the server's messages. Some are cached
/// from the game files and some computed per request; all go through the
/// page's <c>serverText()</c>, so these rows are the server's wording as it
/// stands, and a change to it shows up here as English.
/// </remarks>
public class ServerTextFrenchTests
{
    [Theory]
    [InlineData("Draws 26 power segments with shields up; the plant provides 20. The game will brown out something.",
        "Consomme 26 segments de puissance boucliers levés ; la centrale en fournit 20. Le jeu va couper l’alimentation de quelque chose.")]
    [InlineData("The pilot's guns fire at 2 speeds - CF-337 Panther Repeater ×4 at 1,480 m/s, Revenant Gatling ×2 at 1,296 m/s - so the HUD shows 2 pips, and a lead that lands one gun misses the other.",
        "Les armes du pilote tirent à 2 vitesses - CF-337 Panther Repeater ×4 à 1,480 m/s, Revenant Gatling ×2 à 1,296 m/s - donc le HUD affiche 2 repères, et une visée qui fait mouche avec une arme rate avec l’autre.")]
    [InlineData("A 32 SCU crate is bigger than any box this hull's grids accept - the largest they take is 2.5 × 2.5 × 2.5 m.",
        "Une caisse de 32 SCU est plus grande que tout ce qu’acceptent les grilles de cette coque - la plus grande qu’elles prennent fait 2.5 × 2.5 × 2.5 m.")]
    [InlineData("heat at or above 40%", "chaleur à 40 % ou plus")]
    [InlineData("any 2 of: Taranite 0.03 SCU, Aslarite 0.02 SCU", "2 au choix parmi : Taranite 0.03 SCU, Aslarite 0.02 SCU")]
    [InlineData("The dashboard is running under the bare server; the sticks are read by QuantumWake.exe.",
        "Le tableau de bord tourne sous le serveur seul ; les manches sont lus par QuantumWake.exe.")]
    [InlineData("after its pickups, which are not on this plan", "après ses chargements, qui ne sont pas sur ce plan")]
    [InlineData("Titanium 2 SCU", "Titanium 2 SCU")]
    public void A_server_sentence_reads_in_french(string english, string french)
    {
        var page = new Page("fr");
        var said = page.Text($"serverText({System.Text.Json.JsonSerializer.Serialize(english)})");
        Assert.Equal(french, said.Replace(' ', ' ').Replace(' ', ' '));
    }
}
