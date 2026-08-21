# Can we name what was sold?

Short answer: no, and not for want of looking. The blocker is real and specific.

## What the log gives

A cargo sale is fully described except for the one field that matters:

```
<CEntityComponentCommodityUIProvider::SendCommoditySellRequest>
  Sending SShopCommoditySellRequest - playerId[204721322607]
  shopId[730090005328] shopName[SCShop_Admin_lt_base_g] kioskId[730090005327]
  amount[146240.000000] resourceGUID[b999ef65-35be-45bf-908a-5eac6e06ba12]
  autoLoading[0] quantity[320] transactionMode[Location]
  Cargo Box Data:  [boxSize[16] | unitAmount[20]]
```

Money, volume, box layout, mode and kiosk — all exact. The commodity appears
only as `resourceGUID`, and that id is never repeated anywhere in any of the
144 backup logs alongside a name. Four distinct ids show up across the whole
history.

Buys carry one extra field, `shopPricePerCentiSCU`, which gives an exact unit
price but still no name.

## Why the DataCore does not solve it

`Data\Game2.dcb` is the game's DataCore: 330 MB, unencrypted, and readable
straight out of `Data.p4k` with the existing `P4kArchive`. It was the obvious
place to look, and it does hold the commodity catalogue —
`libs/foundry/records/entities/commodities/minerals/dolivine.xml`,
`.../natural/sunsetberry.xml`, `.../scrap/scrap.xml` — plus 24,442 guid-shaped
strings.

All four log ids were searched through the entire file three ways:

| Form | Result |
|---|---|
| ASCII text, e.g. `b999ef65-35be-45bf-908a-5eac6e06ba12` | not present |
| 16 bytes, .NET guid ordering (`ToByteArray()`) | not present |
| 16 bytes, big-endian (`ToByteArray(bigEndian: true)`) | not present |

Zero hits on any id in any form. The ids in the log therefore belong to a
different numbering from the DataCore's record guids.

## Where it probably does live

`Data\ShopInventories\*.json` inside the p4k — the shop stock tables. Those
ship deliberately encrypted, which is the item already parked as "come back for
encrypted shop later". Until that is opened, the commodity name is out of
reach, and no amount of DataCore parsing changes that.

## What the app shows instead

The Cargo page reports what is provably known: date, buy or sell, SCU, total,
aUEC per SCU, and — via the place back-track described in `architecture.md` —
where the sale happened. It says nothing about what was in the boxes rather
than guessing from unit price.
