import client from './client';

// The client's default axios timeout is unset (no cutoff), but these two
// endpoints are explicitly slow — run-now executes the full scraper
// pipeline (up to ~30-60s) and geocode-now can walk up to a few hundred
// locations at ~1 req/sec against Nominatim (worst case several minutes).
// Passing an explicit per-request timeout keeps that intent obvious even if
// the shared client's default ever changes.
const SLOW_REQUEST_TIMEOUT_MS = 6 * 60 * 1000; // 6 minutes

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
