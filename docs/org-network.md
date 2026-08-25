# Org network

Pooling what members' own installs already know — who holds which blueprint,
what a commodity really fetched at a named terminal, who needs what — across
an org that chooses to run a small server.

As of 0.9 the **spine is built and shipping**: the server exists, people sign
in, orgs form and get approved, apps link, and the dashboard has an Org tab.
The data classes arrive module by module on top of it. An earlier version of
this document designed the network around Azure Functions, Cosmos DB and
per-member keys; what was actually built supersedes all three, and this
document describes the thing that exists plus the parts still to come.

Companion to [architecture.md](architecture.md), whose constraints all still
apply: the app reads logs, it never writes to the game, and it makes no
outbound request the user did not ask for.

## Principles, in the order they win arguments

These survived the redesign untouched, because they are the product.

1. **Local first, always.** Every existing page works with the network off and
   stays that way. The network is the fourth opt-in integration, beside the
   community dataset and the two UEX directions — not a mode the app runs in.
2. **Nothing leaves without a click.** Preview-then-send governs every data
   class. The first share of each class shows exactly what would be uploaded,
   as rows, before anything goes.
3. **Per-class consent, not one switch.** A member sharing their recipe list
   has not agreed to publish where they keep their guns.
4. **Position is special and starts off.** Live location is the one field that
   makes a person trackable by their own org. It gets its own session-scoped
   consent that resets on every restart — enforced by never writing it to
   disk, not by remembering to clear a flag.
5. **The handle is self-declared and the wire says so.** Sign-in proves a
   person (their Discord account); nothing verifies that `nekron` in the
   member list is the `nekron` you fly with. `handleVerified: false` travels
   on every account so no client can forget to admit it. A later RSI-bio
   verification can flip the flag without a migration.
6. **The server is a courier, not an oracle.** It stores and forwards what
   members share. It does not scrape, does not infer, and holds nothing a
   member could not delete.
7. **Deletable.** Leaving an org removes that member's documents; "forget me"
   is a button on the server's account page, and `ON DELETE CASCADE` from the
   accounts table is its implementation, not a batch job that might miss.

## The shape that was built

One ASP.NET Core app, `src/Quantumwake.OrgServer`, that is its own three
deployments: a self-contained executable on a spare box, a Docker container,
and that container on Azure App Service. No Functions runtime, no Cosmos —
anything that only works because Azure provides it would be a dependency the
self-hosted build has to fake, so there is exactly one codebase behaving one
way everywhere.

Storage is a single SQLite file, `org.db`, raw ADO.NET in the house style of
`SessionStore` — with the versioning rule deliberately inverted. SessionStore
drops tables on a schema mismatch because logs can be re-read; this database
is the *only copy* of what members shared, and the sources are on other
people's machines. Migrations are additive only, and a database written by a
newer build refuses to open rather than being half-read.

The wire format lives in `src/Quantumwake.OrgShared`, a dependency-free
project compiled into both sides so a cap or a field cannot drift. It grew
from `ExportDocument`'s doctrine: camelCase, an explicit `formatVersion`
refused whole when newer, stable caveat keys rather than prose, and every
shared row carrying when it was *observed*, never when it was uploaded.
`Sanitise` moved there, because the org wire faces text somebody else wrote on
both ends.

### Identity

- **A person is a provider identity.** Accounts hang off an `identities`
  table keyed by provider and subject — Discord first (scope `identify` only:
  the snowflake and a display name, never the email), Google or Microsoft
  later as new rows, not a migration.
- **A desktop app is a device token.** The app cannot receive an OAuth
  redirect, so linking is a device code: the app asks the org server for a
  code, the person approves it in a signed-in browser, and the app polls its
  way to a long-lived `qwo_` token, stored beside the UEX credentials. The
  code is visible in a URL; the token is only released to the holder of the
  device secret, which never leaves the machine that asked. Tokens are
  SHA-256-hashed at rest, listed by device on the account page, revocable and
  each revocation immediate.
- **A server admin is configuration.** `OrgServer__Admins` lists Discord ids,
  checked live. When nothing is configured and the database is empty, the
  first account to sign in becomes admin — loudly logged, because on a public
  server that is the window someone forgot to close.

### Tenancy

Orgs register pending and wait for a server admin to approve them, unless an
admin created one — there is nobody above them to ask. Members join by invite
code pasted from Discord (expiring, optionally use-limited, revocable). Roles
are owner, manager, member; one owner always, ownership transfers rather than
multiplying.

The wall between orgs is built into the shape of every query: the org id
comes from the route, the membership from the credential, and every store
method takes the org id as its first parameter — a forgotten filter is
impossible rather than unlikely. A non-member gets **404** from everything
under an org, never 403, because a wrong guess must not confirm the org
exists. There is no browsable org directory for the same reason.

### What the client grew

An **Org** tab, always in the strip — a switch that vanishes reads as a
feature that does not exist — rendering an offer when unconfigured, a
waiting-room card while an org is pending, a named unreachable state with a
time on it, and the member roster with its floor stated: handles are
self-declared, and the list only shows members who linked the app.

A Settings block holds the doorway: server address (nothing is contacted
until Link is pressed), the link-code flow, and the invite-code join. The
dashboard **never talks to the org server** — everything proxies through the
local server's `/api/org/*` endpoints. Three reasons, each sufficient: the
token would otherwise reach every LAN viewer; every org mutation is then a
POST to the local server, which LanGuard already refuses off-machine; and
rows can be decorated with what this install knows before the page sees them.

The link flow's poll is the one sanctioned poll in the app: user-started,
only while a code is pending, at the interval the server stated, never past
the code's ten-minute expiry. Everything else remains click-driven, and the
status line reports "last heard from the server", never "online".

## Running a server

### Standalone

```
Quantumwake.OrgServer.exe --Data C:\orgdata --Port 8321
```

Binds loopback by default; `--Bind 0.0.0.0` opens it up, the same
safe-by-default posture as the app's `-Lan`. Configuration is arguments, then
`OrgServer__` environment variables, then defaults in code — no config file
ships. Sign-in needs `--PublicBaseUrl` plus `--Discord:ClientId` and
`--Discord:ClientSecret` from a (free) Discord application whose redirect URI
is `<PublicBaseUrl>/auth/callback`.

The one thing self-hosting cannot solve is reachability: a server on a home
network needs a forwarded port or a tunnel, and TLS in front. Said here
rather than discovered after setting one up.

### Docker

The `Dockerfile` beside the project builds a chiseled image — no shell,
non-root, one volume on `/data`, port 8080. Releases push it to
`ghcr.io/peans99/quantumwake-orgserver`. TLS terminates in front; set
`OrgServer__BehindProxy=true` there so redirects and cookies know.

### Azure App Service

The GHCR image on a Linux plan. **B1 is the honest minimum** (~US$13/month):
the free tier cold-starts and idles out, which breaks link-code polling and
makes the org page feel dead. App settings: the Discord pair,
`OrgServer__PublicBaseUrl`, `OrgServer__Admins`, `OrgServer__BehindProxy=true`,
`OrgServer__Data=/home/data` with `WEBSITES_ENABLE_APP_SERVICE_STORAGE=true`,
and — **this one is load-bearing** — `OrgServer__Journal=delete`. App
Service's `/home` is SMB-backed Azure Files, and SQLite's WAL mode depends on
shared-memory files that are not safe there; `delete` journal mode is slower
and correct. Everywhere else the default `wal` stands.

**Scale-out stays at exactly one instance, forever.** SQLite is the reason.
At the scale this serves — orgs of tens to a few hundred, human-paced writes —
one instance has an order of magnitude of headroom, and the day that stops
being true is the day this grows a storage interface, not a second instance.

## The data classes still to come

Unchanged in intent from the original plan; each is a separate org-side
module toggle, a separate client-side consent, and separately deletable — all
off by default. In build order:

| Module | What is shared | The honesty rule it must keep |
|---|---|---|
| `blueprints` | Names and dates held | Name and date only — never a recipe |
| `prices` | Own trades: commodity, terminal, price, time | Carries the export caveats: place inferred, requested not confirmed |
| `requests` | "I need this", with org-set expiry | Claims are advisory — this coordinates people, it does not police them |
| `commissionOrders` / `commissionServices` | Craft orders and offered services, for a fee | Two separate toggles; a noticeboard, never an escrow |
| `events` | An org schedule | Stored UTC, rendered local |
| `pois` | Named places with notes | Carries both the place id and the author's wording, resolved locally where possible |
| `checklists` | Snapshots of chosen lists | Snapshot, not collaboration — replacing your copy is the whole model |
| `location` | Live position, in memory only | Session-scoped consent that a restart resets; the server never writes it to disk, so a crash forgets it — which is correct |

Requests, commissions, events and POIs will be **authored on the org server**
rather than synced from local stores: they are inherently shared — a request
nobody can see is not a request — and their lifecycles (claims, expiry,
forget-me) belong with the single copy.

Between orgs on the same server, `prices` and `commissions` can eventually be
shared — paired by short-lived codes exchanged out-of-band, with directional
consent on both sides, because offering data must not force it into an org
that never asked.

What position sharing can honestly mean is constrained by the logs:
`Party.cs` documents that there is no roster event, only HUD toasts naming
whoever the game mentioned, so "share while in a party" cannot be auto-gated.
It will ship as a manual session toggle whose own wording admits that.

## Deliberately absent, so it stays absent

Real money, escrow, reputation scores, automated matching, cross-org trading
beyond the paired classes above, collaborative editing, and any moderation
model beyond "an admin can remove members and delete orgs". Every one of
those turns a noticeboard into a much heavier product.

## Risks, honestly

- **Trust is the whole product.** One surprise upload and nobody in the org
  turns it on again — which is why consent is per class, previews are rows,
  and the client's quiet state sends nothing at all.
- **A hosted instance is a commitment.** It can leak, it can go down, and
  someone pays for it. Self-hosting is first-class precisely so no org has to
  trust an instance it does not run.
- **Cost drift.** Nothing polls: the client calls on clicks, the server
  expires data lazily on reads and writes, and there is no background sweeper
  to keep a bill warm.
