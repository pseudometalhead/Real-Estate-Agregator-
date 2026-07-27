import client from './client';

// The client's default axios timeout is unset (no cutoff), but these two
// endpoints are explicitly slow — run-now executes the full scraper
// pipeline across all 6 sources sequentially (verified live: ~11 minutes
// end to end, some individual sources alone take 3-5 minutes) and
// geocode-now can walk up to a few hundred locations at ~1 req/sec against
// Nominatim (worst case several minutes). This used to be 6 minutes, which
// a real run regularly exceeded — the browser would abort the request
// mid-scrape, and since RunScrapersAsync didn't check for cancellation
// between sources, that abort cascaded into every remaining scraper failing
// and the endpoint returning a 500, even though nothing was actually wrong.
const SLOW_REQUEST_TIMEOUT_MS = 20 * 60 * 1000; // 20 minutes

export const scrapersApi = {
  runNow: async () => {
    const { data } = await client.post(
      '/scrapers/run-now',
      null,
      { timeout: SLOW_REQUEST_TIMEOUT_MS }
    );
    return data;
  },

  geocodeNow: async () => {
    const { data } = await client.post(
      '/scrapers/geocode-now',
      null,
      { timeout: SLOW_REQUEST_TIMEOUT_MS }
    );
    return data;
  },
};
