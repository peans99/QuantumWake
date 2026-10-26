namespace Quantumwake.Core.GameData;

/// <summary>
/// A readable name for a shop the game's brand table does not cover.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="GameNames.Shop"/> answers from <c>shop_name_</c> keys, and 22 of
/// the 50 shop ids logged on this install match none of them: food carts, Pyro's
/// rest-stop stalls, the commodity terminals. Each used to be shown as its id
/// with the underscores taken out - "Pyro RStop stall med 001 rund weaponsmith".
/// </para>
/// <para>
/// The food carts do have names in the game, filed as places rather than shops:
/// <c>area_name_fdcrt_gen_sml_htdg</c> is "Hot Dogs", <c>area_name_fdcrt_ellroys</c>
/// "Ellroy's Coffee", <c>area_name_ChuanCart</c> "Chuan Cart". Their ids share no
/// token with those keys, so the pairing is written down here, in the game's
/// words. The stalls have no name anywhere in the English text - no key holds
/// "weaponsmith" - so they are called what the id says they sell.
/// </para>
/// </remarks>
public static class ShopNames
{
    /// <summary>The game's food-cart names, by the token the shop id uses.</summary>
    private static readonly Dictionary<string, string> FoodCarts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["HotDogBar"] = "Hot Dogs",
        ["BurritoBar"] = "Burritos",
        ["burrito"] = "Burritos",
        ["PizzaBar"] = "Pizza",
        ["JuiceBar"] = "Juice",
        ["Ellroys"] = "Ellroy's Coffee",
        ["Chuan"] = "Chuan Cart"
    };

    /// <summary>What a stall's last word says it sells, where the id runs words together or misspells them.</summary>
    private static readonly Dictionary<string, string> StallGoods = new(StringComparer.OrdinalIgnoreCase)
    {
        ["friedgrub"] = "Fried Grub",
        ["conveniance"] = "Convenience",
        ["weaponsmith"] = "Weaponsmith"
    };

    /// <summary>
    /// Layout and size tokens: where a stall sits and how big it is, never
    /// what it sells.
    /// </summary>
    private static readonly HashSet<string> Layout = new(StringComparer.OrdinalIgnoreCase)
    {
        "SCShop", "Pyro", "Stanton", "Nyx", "RStop", "RestStop", "base", "stall", "sml", "small", "med",
        "rund", "rdwn", "Food", "Store", "a", "b"
    };

    /// <summary>The name, or null when the id says nothing a pilot could use.</summary>
    /// <param name="shop">The id, raw or with its underscores already spaced out.</param>
    public static string? Describe(string shop)
    {
        if (string.IsNullOrWhiteSpace(shop))
            return null;

        var tokens = shop.Split(['_', '-', ' '], StringSplitOptions.RemoveEmptyEntries);

        // Every Admin shop on this install appears only in commodity-kiosk
        // lines: SCShop_Admin_lt_base_g 1,786 times, AdminOffice_NewBabbage and
        // Pyro_RestStop_Rund_Admin too.
        if (tokens.Any(t => t.Equals("Admin", StringComparison.OrdinalIgnoreCase)
                || t.StartsWith("AdminOffice", StringComparison.OrdinalIgnoreCase)))
        {
            return "Cargo terminal";
        }

        // Noodle carts come in two: _a is the stall, _b the shop.
        var noodle = Array.FindIndex(tokens, t => t.Equals("NoodleBar", StringComparison.OrdinalIgnoreCase));
        if (noodle >= 0)
        {
            return noodle + 1 < tokens.Length && tokens[noodle + 1].Equals("B", StringComparison.OrdinalIgnoreCase)
                ? "Noodle Shop"
                : "Noodle Stall";
        }

        foreach (var token in tokens)
        {
            if (FoodCarts.TryGetValue(token, out var cart))
                return cart;
        }

        // Crates are sold here, as at SCShop_Cargo_Office, which the game names.
        if (tokens.Any(t => t.StartsWith("CargoOffice", StringComparison.OrdinalIgnoreCase)))
            return "Cargo Services";

        if (tokens.Any(t => t.Equals("BlackMarket", StringComparison.OrdinalIgnoreCase)))
            return "Black Market";

        if (tokens.Any(t => t.Equals("Refinery", StringComparison.OrdinalIgnoreCase)))
            return "Refinery Store";

        if (tokens.Any(t => t.Equals("MiningStall", StringComparison.OrdinalIgnoreCase)))
            return "Mining Stall";

        if (tokens.Any(t => t.Equals("Rentals", StringComparison.OrdinalIgnoreCase)))
            return "Ship Rentals";

        // SCShop_ht_delta_rayari_m_store: an outpost's store, named by operator
        // the way ht_delta_shubin_m_store already is through the brand table.
        if (tokens.Any(t => t.Equals("rayari", StringComparison.OrdinalIgnoreCase)))
            return "Rayari Store";

        if (tokens.Any(t => t.Equals("stall", StringComparison.OrdinalIgnoreCase)))
        {
            var goods = tokens.LastOrDefault(t => !Layout.Contains(t) && !t.All(char.IsDigit));
            if (goods is null)
                return "Stall";

            return StallGoods.TryGetValue(goods, out var known)
                ? $"{known} Stall"
                : $"{char.ToUpperInvariant(goods[0])}{goods[1..]} Stall";
        }

        return null;
    }
}
