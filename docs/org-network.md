# Org network — a plan

Sharing what a pilot's logs already know with the rest of their org: who holds
which blueprint, what materials sit in whose locker, what a commodity fetched
and where, who needs something crafted, and what members will trade with each
other.

Nothing here is built. This is the shape it should take, the order to build it
in, and — more usefully — the decisions that will be hard to reverse once the
first member has pressed Share.

Companion to [architecture.md](architecture.md), whose constraints all still
apply: the app reads logs, it never writes to the game, and it makes no
outbound request the user did not ask for.

---

## What makes this worth building

Quantum Wake already knows things no market site can know, because they come
from a player's own logs rather than a crowd:

- **Which blueprints they hold.** The game announces each one in a toast, and
  the parser keeps it. Seven on the author's install, with dates.
- **What is in their lockers**, per station, and how stale that reading is.
- **What they actually paid and received**, at a named terminal, at a known
  time — the numbers UEX datarunners type in by hand.
- **What they are working towards**, since jobs are authored rather than
  observed.

An org has all of this scattered across a dozen installs and no way to pool it.
"Who can craft an Omnisky IX?" and "has anyone got Hadanite at Port Tressler?"
are answerable questions that currently require asking in Discord and hoping.

UEX already does org-blind pricing well, and this should not compete with it.
The org network's value is the half UEX cannot see: **inventory, capability and
intent, attributable to a person you actually fly with.**

---

## Principles, in the order they win arguments

1. **Local first, always.** Every existing page works with the network off,
   and stays that way. The network is a fourth opt-in integration alongside the
   community dataset, UEX read, and UEX push — not a mode the app runs in.

2. **Nothing leaves without a click.** The established pattern is
   preview-then-send, and it is not negotiable here. The first share of each
   data class shows exactly what would be uploaded, as rows, before anything
   goes.

3. **Per-class consent, not one switch.** Blueprints, stash contents, prices,
   fleet, jobs and position are six separate decisions. A member sharing their
   recipe list has not agreed to publish where they keep their guns.

4. **Position is special and starts off.** Live location is the one field that
   makes a person trackable by their own org. It gets its own consent, its own
   wording, and a session-scoped default rather than a persistent one.

5. **Pseudonymous by default.** A member is their Star Citizen handle, which is
   already public in game. No email, no real name, no account, nothing that
   could not be read off a chat window.

6. **The server is a courier, not an oracle.** It stores and forwards what
   members share. It does not scrape, does not infer, and holds nothing a
   member could not delete.

7. **Deletable.** Leaving an org removes that member's documents. "Forget me"
   is a button, not a support request.

---

## Shape

```
  Quantum Wake (each member)                    Azure
  ┌──────────────────────────┐        ┌──────────────────────────────┐
  │  local logs → SQLite     │        │  Function App (HTTP, C#)     │
  │  jobs, digests, caches   │        │   /org/join   /share/*       │
  │                          │──HTTPS→│   /search     /requests/*    │
  │  OrgClient (opt-in)      │        │   /market/*                  │
  │   · preview before send  │←──────│                              │
  │   · signed member key    │        │            ↓                 │
  └──────────────────────────┘        │  Cosmos DB (serverless)      │
                                       │   partition key = orgId      │
                                       └──────────────────────────────┘
```

The client keeps its offline-first shape: the network is one more service
alongside `UexData` and `CommunityData`, with the same "enabled means a cache
file exists" honesty, and the same refusal to fetch on a timer.

### Why Azure Functions and Cosmos

Both scale to nothing when nobody is flying, which is most of the day. An org
of thirty writing a few hundred documents a day sits inside the free grants;
the realistic bill is a rounding error, and it must stay that way or this stops
being a fan tool. Cosmos is a document store, which suits payloads that differ
per data class and will change shape as the game does — the same reason the
session cache stores JSON blobs rather than a normalised schema.

Nothing here needs SQL joins. Every read is "give me this org's X", which is a
single-partition query by design.

---

## Identity and trust

The hard part, and worth getting right before any code.

**A member is a handle plus a key.** On joining, the client posts the handle it
reads from the logs and receives a long random member key, stored in local app
data beside the UEX credentials. Every later call carries it. No passwords, no
accounts, nothing to reset.

**An org is a code.** Someone creates an org space and gets an invite code to
paste into Discord. Codes expire and can be revoked; an admin can remove a
member, which deletes their documents.

**Handle ownership is unverified at first, and the UI must say so.** Anyone can
claim to be anyone. That is tolerable inside an invite-only org space where
members already know each other, and intolerable if this ever opens up. The
upgrade path, if it is ever needed, is the pattern other SC tools use: the
member puts a one-time code in their RSI profile bio and the function fetches
the public page to confirm. Worth designing the token to carry a `verified`
flag from day one so the UI can grow a badge later without a migration.

**No moderation fantasy.** An org admin can remove members and delete
documents. That is the whole moderation model, and it is enough for a group of
people who fly together.

---

## Data classes

Each is a separate consent, a separate document type, and separately deletable.

| Class | What is shared | Why anyone wants it | Default |
|---|---|---|---|
| `blueprints` | Names and dates of blueprints held | "Who can craft this?" | off |
| `stash` | Item name, station, last-seen date | "Has anyone got Hadanite at Tressler?" | off |
| `prices` | Commodity, terminal, unit price, timestamp | Org-local price truth, fresher than crowd data | off |
| `fleet` | Ship names and cargo capacity | "Who has a hauler free?" | off |
| `jobs` | Open job titles and missing materials | Turns a shopping list into a request | off |
| `position` | Current system/place | Coordination during ops | off, session-scoped |

Notes that will matter later:

- **Stash is presence, not quantity.** The logs never record removals or
  counts, so a shared stash row means "this was seen here on this date" and the
  UI must render it that way. Shipping this as "Bob has 40 Hadanite" would be a
  lie the data cannot support.
- **Everything carries its age.** The Market page already learned this lesson:
  a price without a timestamp is a rumour. Every shared document carries when
  it was observed, not when it was uploaded.
- **Documents expire.** Cosmos TTL on stash and position (days and hours
  respectively) means stale claims disappear on their own rather than
  misleading someone in a month.

---

## Requests and the market

Two features, one mechanism: a document with a state machine.

**Requests** are "I need this". A member posts what they want — often straight
from a job, since a job already knows the missing materials — and other members
see it against what they hold. The org page can answer *"you have three of the
five things Bob needs, and two of them are at the station you are docked at"*,
which is the whole point of pooling inventory.

States: `open → claimed → delivered → closed`, with `cancelled` from any of
them. Claiming is advisory, not a lock: this coordinates people, it does not
police them.

**The market** is the same document with a price and the opposite direction: "I
have this, and I want this much for it". Listings expire. There is no escrow,
no reputation score, and no attempt to hold anyone to anything — trades happen
in game, between people who know each other, and the app's job ends at making
the offer visible.

Deliberately absent, and worth writing down so it stays absent: real money,
cross-org trading, and any automated matching that would make this a market
maker rather than a noticeboard.

---

## What the client grows

- **A Settings block** listing the six data classes with their own switches,
  mirroring the UEX feeds pattern, plus join/leave and a "delete everything I
  have shared" button that actually deletes it.
- **A share preview**, per class, before the first upload of that class.
- **An Org page** under a new nav group: members and when they were last seen,
  a search across pooled blueprints and stashes, the request board, and the
  market listings.
- **Existing pages gain org answers where they are already asking the
  question.** The Blueprints page can say who else holds one; a job line that
  reads "missing, buy at X" can also read "or ask Bob, who had one at
  Seraphim"; the Market page can show org prices beside UEX prices, fresher and
  attributable.

That last point is the real design goal: the network should not be a separate
destination people remember to visit. It should make the pages they already use
answer better.

---

## Build order

Each phase is useful alone and shippable alone.

**Phase A — the pipe.** Function app, Cosmos, join/leave, member keys, one data
class (`blueprints`, the smallest and least sensitive), the Settings block, the
share preview, and an Org page that lists members and who holds what. Proves
identity, consent and deletion end to end on the least dangerous data.

**Phase B — inventory and prices.** `stash` and `prices`, with TTLs and the
staleness wording. This is where the pooled search becomes genuinely useful,
and where "presence, not quantity" has to hold the line.

**Phase C — requests.** The board, the state machine, and the join from a
member's own jobs to what the org can supply. Most of the value with the least
new infrastructure, because the documents already exist.

**Phase D — market.** Listings, expiry, and the org-price column on the Market
page.

**Phase E — position, if wanted at all.** Session-scoped, loud consent, and
easy to leave unbuilt if the org does not want it. Being able to build a thing
is not a reason to.

---

## Risks, honestly

- **Trust is the whole product.** One surprise upload and nobody in the org
  turns it on again. Every default is off, every first share is previewed, and
  when in doubt the app asks rather than assumes.
- **Shared data can be wrong**, because some of it is inferred: stash contents
  are the last listing seen and a listing is only a page; respawn points are
  deduced. Anything inferred must be labelled as such when it reaches someone
  else's screen, where the original caveats are not visible.
- **Cost drift.** Serverless is cheap until something polls. No background
  sync, no timers, no chatty clients — the same rule that already governs UEX.
- **A hosted service is a commitment** in a way a local exe is not: it can
  leak, it can go down, and someone pays for it. It should be possible for an
  org to run their own instance from the repo, and the client should let them
  point at it.
- **Scope.** Every idea here that is not a noticeboard — escrow, reputation,
  cross-org markets, live tracking — makes it a different and much heavier
  product. The plan is deliberately small.

---

## Open questions

1. Who hosts the shared instance, and who pays — one person, or does each org
   deploy their own? This decides whether the function needs multi-tenancy
   hardening or merely org partitioning.
2. Is unverified handle ownership acceptable for the first release? (Probably
   yes inside invite-only spaces; certainly not beyond them.)
3. Should the org share pooled data with the *user's own* pages by default once
   joined, or should reading also be opt-in? Reading is harmless, but surprise
   is not.
4. Does the org want position sharing at all, or is Phase E a solution looking
   for a problem?
