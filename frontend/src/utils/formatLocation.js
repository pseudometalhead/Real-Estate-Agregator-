// Portugal's administrative hierarchy (Distrito > Concelho > Freguesia), as
// far as each source's own data actually distinguishes them — see
// LocationHierarchyParser.cs / each scraper's MapToProperty on the backend.
// Falls back to the flat `location` string for rows scraped before this
// existed (or from a source that only ever gives one usable level), so nothing
// renders blank while a backfill or the next scrape catches up.
export function formatLocation(property) {
  const parts = [property.distrito, property.concelho, property.freguesia].filter(Boolean);
  return parts.length > 0 ? parts.join(', ') : property.location ?? '';
}
