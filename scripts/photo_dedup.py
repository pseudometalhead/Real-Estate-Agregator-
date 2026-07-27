"""
Cross-portal duplicate detection by comparing listing photos directly,
instead of relying only on location/price/beds/size text matching (see
DedupHashGenerator.cs). The text-based hash requires near-exact numeric
agreement across portals; two listings for the same real apartment often
differ slightly (a price cut reflected on one portal but not the other yet,
a size reported as "59m²" on one site and "58.5m²" on another). Photos don't
have that problem — the same physical apartment listed on two portals
usually reuses at least one identical (or near-identical) photo, since the
seller/agent uploads the same images everywhere.

Uses perceptual hashing (imagehash's phash — a classic, well-established
algorithm, not a machine-learning model), not an LLM/AI API: free, no
account, no per-request cost, runs in milliseconds per image. This is a
deliberately lighter-weight alternative to a CLIP-embedding pipeline for the
same reason every paid-service decision this project has made went the free
route first — see docs/ for the WhatsApp-over-Twilio precedent.

This is a standalone script, not wired into the scraper pipeline
automatically: it downloads real images from live listing pages, which is
worth doing deliberately (and rate-limited) rather than on every scrape run.
Re-run it manually whenever cross-portal linking seems to be missing things
the text-hash didn't catch.

Usage: python scripts/photo_dedup.py [--dry-run] [--distance N] [--db PATH]
"""

import argparse
import io
import sqlite3
import sys
import time
import unicodedata
import urllib.request
from collections import defaultdict
from itertools import combinations

try:
    from PIL import Image
    import imagehash
except ImportError:
    print("Missing dependencies. Install with: pip install Pillow imagehash", file=sys.stderr)
    sys.exit(1)

DEFAULT_DB_PATH = r"C:\Users\santo\Documents\Real-Estate-Agregator-\data\estate.db"
# Verified live with a real dry run: distance <=4 matches were all clean 1:1
# pairs with exact or near-exact prices. At distance 6, some new-development
# clusters (e.g. several distinct units in the same Rio Tinto building)
# started matching EACH OTHER — one ImoVirtual listing hit three different
# CustoJusto listings at prices 20%+ apart — almost certainly shared
# template/stock photography between distinct units, not the same listing.
# 4 is the boundary actually supported by evidence, not a guess.
DEFAULT_HASH_DISTANCE = 4  # out of 64 bits
PRICE_SANITY_RATIO = 0.30  # candidate pair's prices must be within 30% of each other
REQUEST_TIMEOUT = 15
USER_AGENT = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36"


def normalize_location(location):
    if not location:
        return ""
    text = location.strip().lower()
    text = " ".join(text.split())
    decomposed = unicodedata.normalize("NFD", text)
    return "".join(c for c in decomposed if unicodedata.category(c) != "Mn")


def fetch_photo_hash(url):
    req = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    with urllib.request.urlopen(req, timeout=REQUEST_TIMEOUT) as resp:
        data = resp.read()
    img = Image.open(io.BytesIO(data))
    return imagehash.phash(img)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dry-run", action="store_true", help="Report matches without writing to the database")
    parser.add_argument("--distance", type=int, default=DEFAULT_HASH_DISTANCE, help="Max Hamming distance to count as a match (default 6/64)")
    parser.add_argument("--db", default=DEFAULT_DB_PATH, help="Path to estate.db")
    args = parser.parse_args()

    conn = sqlite3.connect(args.db)
    conn.execute("PRAGMA journal_mode=WAL;")
    cur = conn.cursor()

    cur.execute("SELECT Id, Url, Source, Price, LocationString, PhotosJson FROM Properties")
    rows = cur.fetchall()
    print(f"Loaded {len(rows)} properties.")

    import json
    properties = []
    for pid, url, source, price, location, photos_json in rows:
        try:
            photos = json.loads(photos_json) if photos_json else []
        except json.JSONDecodeError:
            photos = []
        if not photos or price is None:
            continue
        properties.append({
            "id": pid, "url": url, "source": source, "price": price,
            "location": location, "photo": photos[0],
        })

    # Existing links, so we don't propose (or recreate) ones that already exist.
    cur.execute("SELECT PropertyId, Url FROM PropertySources")
    existing_links = set(cur.fetchall())

    # Bucket by normalized location only (not price/size — the whole point
    # here is to catch pairs the exact-numeric-match hash misses). Only
    # buckets with 2+ DIFFERENT sources are worth comparing at all.
    buckets = defaultdict(list)
    for p in properties:
        buckets[normalize_location(p["location"])].append(p)

    candidate_pairs = []
    for loc, group in buckets.items():
        by_source = defaultdict(list)
        for p in group:
            by_source[p["source"]].append(p)
        sources = list(by_source.keys())
        if len(sources) < 2:
            continue
        for s1, s2 in combinations(sources, 2):
            for p1 in by_source[s1]:
                for p2 in by_source[s2]:
                    lo, hi = min(p1["price"], p2["price"]), max(p1["price"], p2["price"])
                    if lo == 0 or (hi - lo) / lo > PRICE_SANITY_RATIO:
                        continue
                    candidate_pairs.append((p1, p2))

    print(f"{len(candidate_pairs)} cross-source candidate pairs within price sanity range (same normalized location).")

    # Hash each distinct photo URL once, not once per pair it appears in.
    photo_urls = {p["photo"] for pair in candidate_pairs for p in pair}
    print(f"Downloading and hashing {len(photo_urls)} distinct photos...")
    hash_cache = {}
    failed = 0
    for i, url in enumerate(photo_urls, 1):
        try:
            hash_cache[url] = fetch_photo_hash(url)
        except Exception as ex:
            failed += 1
        if i % 50 == 0:
            print(f"  ...{i}/{len(photo_urls)}")
        time.sleep(0.1)  # light throttling — these are real third-party image CDNs

    if failed:
        print(f"  {failed} photo(s) failed to download/decode (skipped, not fatal).")

    matches = []
    for p1, p2 in candidate_pairs:
        h1, h2 = hash_cache.get(p1["photo"]), hash_cache.get(p2["photo"])
        if h1 is None or h2 is None:
            continue
        distance = h1 - h2
        if distance <= args.distance:
            matches.append((p1, p2, distance))

    matches.sort(key=lambda m: m[2])

    # A genuine cross-portal duplicate is a clean 1:1 pair. A property
    # appearing in 2+ matches is more likely a new development where several
    # distinct units share template/stock photography — verified live (see
    # DEFAULT_HASH_DISTANCE comment): exactly this pattern showed up as
    # one-to-many clusters at looser distances. Those get reported, not
    # auto-linked.
    match_count = defaultdict(int)
    for p1, p2, _ in matches:
        match_count[p1["id"]] += 1
        match_count[p2["id"]] += 1
    ambiguous_ids = {pid for pid, count in match_count.items() if count > 1}

    print(f"\n{len(matches)} photo-matched pairs (Hamming distance <= {args.distance}):\n")

    to_link = []
    for p1, p2, distance in matches:
        canonical, other = (p1, p2) if p1["id"] < p2["id"] else (p2, p1)
        ambiguous = p1["id"] in ambiguous_ids or p2["id"] in ambiguous_ids
        already_linked = (canonical["id"], other["url"]) in existing_links
        status = "already linked" if already_linked else ("AMBIGUOUS - skipped" if ambiguous else "NEW")
        print(f"  [{status}] dist={distance}  #{canonical['id']} ({canonical['source']}) <-> "
              f"#{other['id']} ({other['source']})  {canonical['location']}  "
              f"€{canonical['price']:,.0f} vs €{other['price']:,.0f}")
        if not already_linked and not ambiguous:
            to_link.append((canonical, other))

    if ambiguous_ids:
        print(f"\n{len(ambiguous_ids)} propert(y/ies) involved in ambiguous (one-to-many) matches — "
              f"skipped, not auto-linked. Review manually if needed.")

    if args.dry_run:
        print(f"\nDry run — {len(to_link)} new link(s) would be created. Re-run without --dry-run to apply.")
        conn.close()
        return

    if to_link:
        now = time.strftime("%Y-%m-%d %H:%M:%S")
        for canonical, other in to_link:
            cur.execute(
                "INSERT INTO PropertySources (PropertyId, Source, Url, Price, FirstSeenAt, LastSeenAt) "
                "VALUES (?, ?, ?, ?, ?, ?)",
                (canonical["id"], other["source"], other["url"], other["price"], now, now),
            )
        conn.commit()
        cur.execute("PRAGMA wal_checkpoint(FULL);")
        print(f"\nCreated {len(to_link)} new PropertySource link(s).")
    else:
        print("\nNo new links to create.")

    conn.close()


if __name__ == "__main__":
    main()
