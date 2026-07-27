# Idealista Integration Plan

## Current state: implemented and verified live

`IdealistaScraper.cs` integrates with a real, documented third-party API
instead of scraping idealista.pt directly. It's fully wired into the
existing pipeline (dedup, sun-orientation/open-plan-kitchen extraction,
`ScraperRun` reporting), and — as of 2026-07-26 — verified against a real
RapidAPI key: a live run against Porto (€50K–300K) added 194 new listings
in 6 seconds with zero errors, alongside all 5 other sources also running
clean in the same pass. Live testing caught and fixed three things a spec
alone couldn't reveal (see code comments in `IdealistaScraper.cs` for
detail): the API defaults to Spanish descriptions unless `&locale=pt` is
passed, `autocomplete` returns a municipality before the district for an
ambiguous query like "Porto", and the API's own `district`/`province`
field names are the reverse of what they suggest (`district` is
neighborhood-level, `province` is the district-level field this app
means by "district").

Idealista.pt itself sits behind [DataDome](https://datadome.co/) bot
protection: confirmed live that even the homepage and `robots.txt`-adjacent
paths return 403 to a plain HTTP request. No RSS/saved-search feed path
could be found either (every guessed path 403'd too, and Idealista's own
public docs don't mention one). Options A (RSS) and C (browser automation)
from the original version of this doc were dropped as a result — see
"Options considered" below for the full reasoning.

## What was built: Option D — third-party wrapper API

**API**: [rapidapi.com/kiwimaker/api/idealista-real-estate](https://rapidapi.com/kiwimaker/api/idealista-real-estate),
backed by apidea.es. **Not an official Idealista API** — explicitly
disclaimed by its own provider as an independent wrapper, not affiliated
with or endorsed by Idealista S.A.U. We'd be a paying customer of a
commercial data reseller, not running any scraping/bypass code ourselves —
a meaningfully different risk profile than Option C, but still a
dependency on a business that could change or disappear if Idealista acts
against resellers.

- Covers Portugal (`country=pt`), alongside Spain and Italy.
- Full OpenAPI 3.1 spec at `https://apidea.es/openapi.json` — every field
  and parameter name in `IdealistaScraper.cs`/`IdealistaModels.cs` was
  checked directly against the raw spec, not guessed.
- Search results include lat/lng directly, so Idealista listings skip
  `GeocodingService` entirely — one less Nominatim call per listing.
- Search results also include a full `description` field, so sun
  orientation and open-plan-kitchen extraction work exactly like every
  other source, with no extra per-listing API calls needed.
- One documented gap: `/v1/search/filters` (dynamic filter discovery)
  returns a server error for `pt`/`it` — doesn't matter here, since this
  scraper passes `priceFrom`/`priceTo`/`bedrooms` directly rather than
  discovering filters at runtime.
- District → Idealista location-id resolution happens at request time via
  `/v1/locations/autocomplete`, since no Portugal location-id list exists
  anywhere in the spec (only Spain has one, via `listSpanishRegions`).

**Verified live** — the code still distinguishes a 401 (bad/missing key),
422 (a parameter this scraper sent wrong — full response body goes to the
logs), and 502/503 (Idealista itself unreachable upstream of the wrapper,
not our bug), useful if the wrapper's API ever changes shape.

### Activated

`IDEALISTA_RAPIDAPI_KEY` is set locally in `.env.local` (gitignored, never
committed — the tracked `.env` only has an empty placeholder). When
starting the stack from a fresh shell, either export the key before
`docker compose up`, or merge it in explicitly — **avoid `source .env` in
Git Bash on Windows**, which mangles the POSIX-style `DATABASE_PATH=/data/...`
value into a Windows path (`C:/Program Files/Git/data/...`) via MSYS
path-conversion, silently pointing the app at an empty database. Safer:
```
export MSYS_NO_PATHCONV=1
export IDEALISTA_RAPIDAPI_KEY=<your key>
docker compose up -d --build
```
(docker-compose reads the rest of `.env` itself, correctly, without going
through bash at all).

## Also available: manual import (Option E)

Independent of the API key: `POST /api/properties/import`, with a form on
the Settings page, lets you paste a listing's URL/price/location/etc. by
hand and runs it through the same dedup/extraction pipeline. Works today,
no key required, zero automation risk — useful as a fallback or for
one-off listings regardless of what happens with the API above.

## Options considered and dropped

- **RSS/saved-search feed** — every guessed feed path 403'd the same as
  the rest of the site; no evidence Idealista.pt currently offers one.
- **Official Idealista partner API** — real and free to apply for
  ([developers.idealista.com/access-request](https://developers.idealista.com/access-request),
  just a name/email/project-description form), but no visible timeline,
  cost, or approval odds from the page itself. Worth submitting in
  parallel if you want the "official" path eventually — it doesn't
  conflict with using the RapidAPI wrapper in the meantime.
- **propertium.io** — covers Portugal, but API access is on their
  **Enterprise plan at €999/month**; their only affordable tier (€19.99/mo)
  is watch-zone email alerts, not API access. Not worth it for personal use.
- **Headless browser + anti-detection (old Option C)** — still not
  recommended: DataDome is built to detect exactly this, it's an ongoing
  arms race rather than a one-time integration, and it likely crosses
  Idealista's ToS. Out of scope unless explicitly requested.
- **Draft text for the official access-request form** (at
  [developers.idealista.com/access-request](https://developers.idealista.com/access-request) —
  submitting it requires your own name/email, so this is prepared for you
  to paste in, not submitted automatically):

  > **Project description:** Estate Aggregator is a personal-use real
  > estate search tool that aggregates apartment listings for sale across
  > several Portuguese portals (ImoVirtual, Casa SAPO, Caixa Imobiliário,
  > Santander, CustoJusto) into a single searchable interface with map
  > view, price-history tracking, and filtering by district, price,
  > rooms, and sun orientation. It's a single-user local application, not
  > a public site or commercial product. We'd like to add Idealista as an
  > additional source via the official API rather than any form of
  > scraping.

- **Other RapidAPI/GitHub scrapers mentioned in community discussion**
  (`rapidapi.com/apidojo/api/idealista2`, `github.com/seralexger/idealista-data`,
  `github.com/David-Carrasco/Scrapy-Idealista`, `github.com/hmeleiro/idealisto`)
  — not evaluated in depth; `idealisto` was reported broken by a commenter.
  The kiwimaker/apidea.es wrapper was picked for its openly-published,
  verifiable OpenAPI spec — most of the alternatives above have no
  equivalent public documentation to check claims against.
