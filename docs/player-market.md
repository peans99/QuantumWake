# What a player will pay

A plan for the second half of "what is this worth": not what a shop pays, but
what another pilot is asking.

Written before any code. Endpoint fields were read from UEX's own documentation
and are quoted; everything the app does today was read from the source on
`dev090` and carries the number that was found.

---

## The gap, in one table

Every price this app knows is a price the game charges.

| Feed the app already pulls | What it answers |
|---|---|
| `commodities_prices_all` | what a terminal pays for cargo |
| `terminals?type=commodity` | where those terminals are |
| `vehicles_purchases_prices_all` | what a ship costs to buy |
| `items_prices_all` | what gear costs at a kiosk |
| `commodities_prices_history` | how terminal prices moved |

None of them is what a person will pay you.

That matters most for exactly the items the app already singles out. The label
overlay marks gear **nothing stocks** — and gear nothing stocks is precisely
where a player market exists. The app currently says "nobody sells this" and
stops, one call short of "and pilots are asking about N for it".

---

## What UEX already offers and the app has never called

`marketplace_prices_averages`, read from
[UEX's documentation](https://uexcorp.space/api/documentation/id/get_marketplace_prices_averages/).

**No authentication.** Covers marketplace items — not ships, not commodities.

Accepts at least one of `id_item` (up to ten, comma separated), `id_category`,
`item_uuid`, or `item_name`; then optionally `operation` (buy or sell),
`quality_tier` (0-7), `currency`, and `game_version`.

Returns, among identifiers and dates:

| Field | UEX's own words |
|---|---|
| `listings_count` | "number of active listings contributing to this average" |
| `price_avg` | "current average from active listings, per unit" |
| `price_avg_week` | 7-day rolling average |
| `price_avg_month` | 30-day rolling average |
| `quality_tier` | quality classification (0-7) |
| `operation` | buy or sell designation |

There is more beyond it — `marketplace_listings`, `marketplace_negotiations`,
`marketplace_negotiations_messages`, `marketplace_advertise` — and none of it is
in scope here. See **Deliberately out of scope** below.

---

## Why this is cheap: the join is exact

The app already resolves a Star Citizen item UUID:

```csharp
public string? ItemUuid(string? itemClass) =>
    GameCommodities.ItemUuid(itemClass) ?? Community.Item(itemClass ?? "")?.Uuid;
```

`marketplace_prices_averages` takes `item_uuid`. So this is a **key lookup, not
a name match** — no fuzzy scoring, no "P4-AR versus P8-AR" problem, no guessing.
Where the install or the community dataset knows an item's UUID, the price that
comes back is unambiguously that item's.

Where neither knows a UUID, `item_name` is the fallback, and a fallback is
exactly what it should be called on screen.

This is the difference between this plan and the OCR one. That feature has to
work out *what an item is*. This one already knows, and only has to ask.

---

## What the numbers are, and are not

The house rule is that a figure carries the thing that makes it fragile. These
have three.

**An average of asking prices is not a sale price.** A terminal price is what
the game will pay, today, at a named till, and it cannot be wrong. A marketplace
average is what strangers are *asking* — nothing here says anybody paid it. The
app already separates "requested" from "confirmed" in the Ledger for the same
reason, and the same wording discipline applies.

**`listings_count` is the number that decides whether the average means
anything.** An average over three listings and an average over three hundred
are different kinds of claim, and only one of them survives one optimist. It is
not a detail to tuck away: it belongs beside the figure, always.

**Three averages are a shape, not three numbers.** `price_avg` far above
`price_avg_month` is a spike or a thin week; far below is a market that has
moved. Showing the current average alone throws away the only trend signal in
the payload.

And one the app already handles well and must keep handling: **prices carry
their age.** `date_modified` comes back with every row, and a stale price shown
without it is the one number on a card that can lose real money.

---

## Where it would surface

Nothing new is needed to display this. Three places already exist and already
carry the right caveats.

**The part card.** `EntityCards.Part` shows type, size, grade and the kiosk
price. A player-market line sits directly under it, labelled as asking prices,
with `listings_count` beside it. This is the main one.

**The Loot page.** It already says what looted gear is typically worth — from
kiosk prices, which for unsold gear is nothing at all. This is where the answer
currently reads as "worthless" and is actually "no shop wants it, and pilots are
asking N".

**"Why this number?"** `Explanations` is the natural home for a market figure:
value, rule, records, and what was left out. `listings_count` and the three
averages are, between them, an explanation that writes itself.

---

## Deliberately out of scope

**`marketplace_advertise` posts as the pilot.** Publishing a listing on
somebody's behalf is a different feature with a different consent conversation,
and it is not this one.

**`marketplace_listings` and the negotiation endpoints are a marketplace
client.** Reading live listings, threading messages, replying — that is an
application, not a figure on a card. If it is ever wanted it deserves its own
plan.

**Ships and commodities are not covered by this endpoint.** It is items. Ship
and cargo prices stay where they are.

---

## Open questions

1. **What is `quality_tier`?** Zero to seven, and the documentation says
   "quality ranges" without saying whose. If it is item condition, it changes
   what an average means — a worn and a pristine example of the same gun are not
   the same offer. Worth one call against a known item before designing the
   display.

2. **How many of this install's items get a hit?** The label overlay knows the
   answer to the parallel question — UEX misses 29 of the 106 items these logs
   prove were bought. The marketplace covers far less than the kiosk catalogue,
   almost certainly, and the honest headline is a count, not a promise.

3. **How often to fetch, and when?** UEX allows 120 requests a minute and the
   endpoint takes ten ids at a time. Batching by UUID on demand is probably
   right; a background sweep of the whole catalogue is probably not, and would
   be the app making outbound requests nobody asked for.

4. **Does an unsold mark change?** The label overlay's mark means "no shop
   stocks this". It should almost certainly stay meaning exactly that, with the
   market as a separate line — but somebody will ask, and the answer should be
   written down rather than argued each time.

---

## Build order

1. **Ask UEX about one known item and print what comes back.** By UUID, from
   the CLI. Answers questions 1 and 2 at once, and tells us whether the coverage
   is worth building on before anything is built on it.
2. **A `UexMarket` reader** beside `UexData`, batching UUIDs ten at a time,
   cached like the other feeds and behind the same enable switch — this is an
   outbound call and the pilot decides.
3. **The line on the part card**, with `listings_count` and the age, worded as
   asking prices.
4. **The Loot page**, where the current answer is most wrong.
5. **An `Explanations` entry**, once there is a figure worth questioning.

Step 1 is the whole decision. If the marketplace only knows two hundred items,
this is a footnote on a card; if it knows the gear players actually fight over,
it is the answer to "is this worth carrying" that the app has never been able to
give.
