# Reporting a problem

How "it works for you and not for me" gets answered without anybody sending
their `Game.log` anywhere.

Settings → **Report a problem** saves a small JSON file. This is what is in it,
what is deliberately not, and the one thing it cannot promise.

## Why not just send the log

Three reasons, in order of how quickly they bite.

**Size.** A quiet session here is 0.2 MB. A real one is **8.1 MB and 29,269
lines**. The whole corpus on this machine is **158 files, 435 MB**. Pastebin
refuses a paste over 512 KB on a free account, so almost every gameplay log is
too big before privacy is even discussed. GitHub accepts a 25 MB attachment,
which fits one log and not a set.

**Other people.** A log names the pilots you flew with. Party notifications and
ship comms channels are the only lines in a 4.x log that name another player —
they are the entire basis of the Crew page — so publishing your log publishes
their handles too. That is not yours to give away.

**Yours.** The header carries your handle, character GEID, account id and
session GUIDs, and `Executable:` names a user folder on many installs.

## What the report holds instead

The parser already records what it could not read: a count per unrecognised tag
and one example line each (`LogEventParser.RecordUnmatched`). That is the whole
diagnosis for an empty page, and it is **kilobytes** — a real report from this
install is about 1 KB.

| Field | Why it is in there |
|---|---|
| `producer` | App version and build, so the answer is about the right code |
| `install` | Whether an install was found, its channel, whether `Game.log` is present, how many backups — **never the path** |
| `library` | Sessions stored and counted, first and last dates, and every game build seen with a count |
| `parser` | How many lines went unread this run, and under which tags |
| `views` | The counts behind each page — ships, places, contracts, purchases, trades, fleet, loadout, stash |
| `data` | Whether the community dataset and UEX are on, and which dump the dataset came from |
| `wipe` | The line, its scope, and how many sessions sit before it |

`views` and `wipe` are there because most "this page is empty" reports are not
parser bugs at all. A Crew page with nothing on it and a `wipe.hidden` of 108 is
a wipe line drawn too late, not a defect.

## Allow-list, not deny-list

Every field above is one the code chose to put in. The report is **built up**
from facts the app can name, rather than **built down** from a log with the
private parts stripped out.

That direction is the whole safety argument. A deny-list leaks the pattern
nobody thought of, and the only thing worse than no report is one that promises
to be clean and is not. It is the same reasoning as `LanGuard`, which whitelists
read methods rather than listing forbidden endpoints.

Consequences worth stating:

- **The install path is absent.** It reads `C:\Users\<name>\...` on plenty of
  machines, and it has never been the answer to a parser question.
- **UEX keys are absent.** Whether keys are *stored* is a boolean, because "UEX
  is on but has no keys" explains a page of blanks.
- **No handle, character or account id appears anywhere.**

A test reads the whole document back and fails if any field is so much as
*named* for one of those, so a field added later that carries one arrives as a
red test rather than a quiet leak.

## The one thing it cannot promise

Example lines are **off unless you ask**, and this is why.

A sample exists only because a known tag stopped parsing — which means the
game changed that line's format. A changed format is free to write your name in
a shape nothing here has ever seen. Scrubbing replaces the identifiers this
install has already read, and the shapes the game has always used
(`Handle[...]`, `nickname="..."`, `- name X -`, GEIDs, account ids, session
GUIDs). It cannot replace a shape that has just been invented.

That is not a hypothetical. A synthetic 4.11-shaped log whose login line was
reshaped to `Pilot{TestPilot42}` came through the scrubber **still naming its
pilot**:

```
2  Legacy login response   [CIG-net] User Login OK - Pilot{TestPilot42} - Time[177332566]
2  AccountLoginCharacterStatus_Character   Character: createdAt 1784476187540 - geid <id> - accountId <id> - name <pilot> - status CURRENT
2  Context Establisher Done   establisher="Game" nonsense session=<session>
```

Two of the three were scrubbed because their shapes were known. The one that
was not is the one whose format had changed — and a format change is the only
reason any of them is in the list.

There is a second, quieter failure in the same case: when the line that broke is
the *login* line, the handle never reaches the session store, so there is no
value to search for either. Shape-based scrubbing is what covers that, and it
covers only shapes it knows.

So the report always carries the **counts**, which are safe by construction and
enough to see that something broke and where. The **lines** are a separate yes,
and the Settings page says plainly what it cannot promise about them.

## Sending one

Nothing is uploaded. The file is built in the page and handed to the browser,
so it lands in your downloads and goes no further until you send it. Read it
first — it is a kilobyte of JSON and that is the point of it being small.

Attach it to a GitHub issue with what you expected to see and what you saw.

## Reading one, as a maintainer

In roughly this order:

1. **`parser.unread`.** Anything above zero with a tag list is a format change:
   the game moved a line the app depends on. Ask for the example lines if they
   are not attached.
2. **`library.builds`.** Which patch they are on, and whether the sessions are
   spread across several. A build nobody else has reported is a strong lead.
3. **`wipe.hidden`.** A large number here explains an empty page with no bug
   attached.
4. **`views`.** Zero on one page with sessions in the library narrows it to that
   page's resolver rather than the parser.
5. **`data.communityDump`.** Missing names for new ships or items usually means
   the dump predates their patch, which the dataset now reports itself.

`install.backups` at zero with `install.found` true means the app is reading a
live `Game.log` and nothing else — a fresh install, or a channel with its
backups cleared. That install cannot show history yet, and no amount of parser
work will change it.
