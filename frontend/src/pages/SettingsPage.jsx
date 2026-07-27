import React, { useState, useEffect } from 'react';
import { appSettingsApi } from '../api/appSettingsApi';
import { scrapersApi } from '../api/scrapersApi';

const sources = [
  { key: 'scrapeIdealistaEnabled', label: 'Idealista', working: true },
  { key: 'scrapeImoVirtualEnabled', label: 'ImoVirtual', working: true },
  { key: 'scrapeImobiliarioEnabled', label: 'CustoJusto', working: true },
  { key: 'scrapeCasaSapoEnabled', label: 'Casa SAPO', working: true },
  { key: 'scrapeCaixaImobiliarioEnabled', label: 'Caixa Imobiliário (CGD)', working: true },
  { key: 'scrapeSantanderEnabled', label: 'Santander', working: true },
];

function Spinner() {
  return (
    <svg className="h-4 w-4 animate-spin" viewBox="0 0 24 24" fill="none">
      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
      <path
        className="opacity-75"
        fill="currentColor"
        d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"
      />
    </svg>
  );
}

function formatScraperSummary(report) {
  if (!report) return null;
  return `Found ${report.propertiesFound ?? 0} · Added ${report.propertiesAdded ?? 0} · Updated ${
    report.propertiesUpdated ?? 0
  } · Linked ${report.propertiesLinked ?? 0} · Skipped ${report.propertiesSkipped ?? 0}`;
}

function formatGeocodeSummary(result) {
  if (!result) return 'Backfill complete.';
  const geocoded =
    result.geocoded ?? result.propertiesGeocoded ?? result.updated ?? result.count ?? null;
  const remaining = result.remaining ?? result.propertiesRemaining ?? null;
  if (geocoded == null) return 'Backfill complete.';
  let msg = `Geocoded ${geocoded} location${geocoded === 1 ? '' : 's'}`;
  if (remaining != null) msg += ` · ${remaining} remaining`;
  return msg;
}

export function SettingsPage() {
  const [settings, setSettings] = useState(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const [saved, setSaved] = useState(false);

  const [scraperRunning, setScraperRunning] = useState(false);
  const [scraperResult, setScraperResult] = useState(null);
  const [scraperError, setScraperError] = useState(null);

  const [geocodeRunning, setGeocodeRunning] = useState(false);
  const [geocodeResult, setGeocodeResult] = useState(null);
  const [geocodeError, setGeocodeError] = useState(null);

  useEffect(() => {
    const fetchSettings = async () => {
      try {
        const data = await appSettingsApi.getSettings();
        setSettings(data);
      } catch (err) {
        setError(err.message ?? 'Failed to fetch settings');
      } finally {
        setLoading(false);
      }
    };

    fetchSettings();
  }, []);

  const handleSave = async () => {
    setSaving(true);
    setError(null);
    setSaved(false);
    try {
      const updated = await appSettingsApi.updateSettings(settings);
      setSettings(updated);
      setSaved(true);
    } catch (err) {
      setError(err.message ?? 'Failed to save settings');
    } finally {
      setSaving(false);
    }
  };

  const handleRunScraperNow = async () => {
    setScraperRunning(true);
    setScraperError(null);
    setScraperResult(null);
    try {
      const report = await scrapersApi.runNow();
      setScraperResult(report);
    } catch (err) {
      setScraperError(err.message ?? 'Failed to run scrapers');
    } finally {
      setScraperRunning(false);
    }
  };

  const handleGeocodeNow = async () => {
    setGeocodeRunning(true);
    setGeocodeError(null);
    setGeocodeResult(null);
    try {
      const result = await scrapersApi.geocodeNow();
      setGeocodeResult(result);
    } catch (err) {
      setGeocodeError(err.message ?? 'Failed to backfill map pins');
    } finally {
      setGeocodeRunning(false);
    }
  };

  if (loading) return <div className="py-12 text-center text-slate-400">Loading...</div>;
  if (error && !settings)
    return <div className="py-12 text-center text-red-400">Error loading settings: {error}</div>;
  if (!settings) return null;

  return (
    <div className="p-6 max-w-2xl mx-auto">
      <h1 className="text-3xl font-bold text-white mb-6">Scraper Settings</h1>

      {error && (
        <div className="mb-4 rounded-xl border border-red-500/20 bg-red-500/10 p-3 text-sm text-red-400">
          {error}
        </div>
      )}
      {saved && (
        <div className="mb-4 rounded-xl border border-emerald-500/20 bg-emerald-500/10 p-3 text-sm text-emerald-400">
          Settings saved! Scrapers will use the new configuration.
        </div>
      )}

      <div className="rounded-xl border border-white/10 bg-white/[0.06] backdrop-blur-xl shadow-sm p-6 space-y-6">
        <div>
          <label className="block text-sm font-medium text-slate-300 mb-2">
            Districts (comma-separated)
          </label>
          <input
            type="text"
            value={settings.districts?.join(', ') ?? ''}
            onChange={(e) =>
              setSettings({
                ...settings,
                districts: e.target.value.split(',').map((d) => d.trim()).filter(Boolean),
              })
            }
            placeholder="e.g., Lisbon, Porto, Cascais"
            className="w-full rounded-lg border border-white/10 bg-white/10 px-3 py-2 text-sm text-white focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
          />
          <p className="text-sm text-slate-400 mt-1">Where to search for properties</p>
        </div>

        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-slate-300 mb-2">Min Price (€)</label>
            <input
              type="number"
              value={settings.priceMin ?? ''}
              onChange={(e) =>
                setSettings({ ...settings, priceMin: parseInt(e.target.value, 10) || 0 })
              }
              className="w-full rounded-lg border border-white/10 bg-white/10 px-3 py-2 text-sm text-white focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-300 mb-2">Max Price (€)</label>
            <input
              type="number"
              value={settings.priceMax ?? ''}
              onChange={(e) =>
                setSettings({ ...settings, priceMax: parseInt(e.target.value, 10) || 999999 })
              }
              className="w-full rounded-lg border border-white/10 bg-white/10 px-3 py-2 text-sm text-white focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
          </div>
        </div>

        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-slate-300 mb-2">Min Rooms</label>
            <input
              type="number"
              value={settings.roomsMin ?? ''}
              onChange={(e) =>
                setSettings({ ...settings, roomsMin: parseInt(e.target.value, 10) || 0 })
              }
              className="w-full rounded-lg border border-white/10 bg-white/10 px-3 py-2 text-sm text-white focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-300 mb-2">Max Rooms</label>
            <input
              type="number"
              value={settings.roomsMax ?? ''}
              onChange={(e) =>
                setSettings({ ...settings, roomsMax: parseInt(e.target.value, 10) || 10 })
              }
              className="w-full rounded-lg border border-white/10 bg-white/10 px-3 py-2 text-sm text-white focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
          </div>
        </div>

        <div>
          <label className="block text-sm font-medium text-slate-300 mb-2">Max Pages Per Source</label>
          <input
            type="number"
            min="1"
            value={settings.maxPagesPerSource ?? ''}
            onChange={(e) =>
              setSettings({ ...settings, maxPagesPerSource: parseInt(e.target.value, 10) || 1 })
            }
            className="w-full rounded-lg border border-white/10 bg-white/10 px-3 py-2 text-sm text-white focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
          />
          <p className="text-sm text-slate-400 mt-1">
            How many pages each source fetches per district before stopping. Higher values pull in
            more listings per run but take longer and risk hitting a site's own rate limits — some
            scrapers already log warnings and back off automatically (via Polly retry policies), but
            this setting controls how far each run is allowed to go in the first place.
          </p>
        </div>

        <div>
          <label className="block text-sm font-medium text-slate-300 mb-2">Your name</label>
          <input
            type="text"
            value={settings.senderName ?? ''}
            onChange={(e) => setSettings({ ...settings, senderName: e.target.value })}
            placeholder="e.g. Diana Barros"
            className="w-full rounded-lg border border-white/10 bg-white/10 px-3 py-2 text-sm text-white focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
          />
          <p className="text-sm text-slate-400 mt-1">
            Signs every drafted inquiry message ("Cumprimentos, ...").
          </p>
        </div>

        <div>
          <label className="block text-sm font-medium text-slate-300 mb-2">My availability</label>
          <textarea
            value={settings.availabilityText ?? ''}
            onChange={(e) => setSettings({ ...settings, availabilityText: e.target.value })}
            placeholder="e.g. Dias de semana após as 18h, ou fins-de-semana"
            rows={2}
            className="w-full rounded-lg border border-white/10 bg-white/10 px-3 py-2 text-sm text-white focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
          />
          <p className="text-sm text-slate-400 mt-1">
            When you're free for a viewing — reused as-is in every drafted inquiry message so the
            agent can propose a time in their first reply.
          </p>
        </div>

        <div>
          <label className="block text-sm font-medium text-slate-300 mb-2">Enabled Sources</label>
          <div className="flex gap-4 flex-wrap">
            {sources.map((source) => (
              <label key={source.key} className="flex items-center gap-2 text-sm text-slate-300">
                <input
                  type="checkbox"
                  checked={settings[source.key] ?? false}
                  onChange={(e) => setSettings({ ...settings, [source.key]: e.target.checked })}
                  className="accent-blue-600"
                />
                {source.label}
                {!source.working && (
                  <span
                    className="inline-flex items-center rounded-full bg-white/10 px-2.5 py-0.5 text-xs font-medium text-slate-400"
                    title={source.note ?? 'Integration not available for this source'}
                  >
                    {source.note ?? 'not available'}
                  </span>
                )}
              </label>
            ))}
          </div>
          <p className="text-sm text-slate-400 mt-1">
            ImoVirtual, CustoJusto, Casa SAPO, Caixa Imobiliário and Santander (the latter two are
            bank-owned property portals) run real scrapes. Idealista.pt itself blocks scraping, so
            Idealista goes through a third-party API instead — set{' '}
            <code className="text-xs bg-white/10 px-1 py-0.5 rounded">IDEALISTA_RAPIDAPI_KEY</code>{' '}
            in <code className="text-xs bg-white/10 px-1 py-0.5 rounded">.env</code> to enable it
            (see docs/idealista-integration-plan.md).
          </p>
        </div>

        {settings.lastScrapedAt && (
          <div className="rounded-xl border border-white/10 bg-white/5 p-3">
            <p className="text-sm text-slate-400">
              Last scraped: {new Date(settings.lastScrapedAt).toLocaleString()}
            </p>
          </div>
        )}

        <div className="rounded-xl border border-white/10 bg-white/5 p-4 space-y-3">
          <div className="flex flex-wrap items-center gap-3">
            <button
              type="button"
              onClick={handleRunScraperNow}
              disabled={scraperRunning}
              className="inline-flex items-center gap-2 rounded-lg bg-blue-500 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-400 disabled:opacity-50"
            >
              {scraperRunning && <Spinner />}
              {scraperRunning ? 'Running scrapers…' : 'Run Scraper Now'}
            </button>
            <button
              type="button"
              onClick={handleGeocodeNow}
              disabled={geocodeRunning}
              className="inline-flex items-center gap-2 rounded-lg bg-white/10 px-4 py-2 text-sm font-medium text-slate-300 transition-colors hover:bg-white/20 disabled:opacity-50"
            >
              {geocodeRunning && <Spinner />}
              {geocodeRunning ? 'Backfilling…' : 'Backfill Map Pins'}
            </button>
          </div>
          <p className="text-xs text-slate-400">
            Running the full scraper can take up to a minute. Backfilling map pins only fetches
            coordinates for listings that are missing them, and is usually faster.
          </p>

          {scraperError && <p className="text-sm text-red-400">{scraperError}</p>}
          {scraperResult && !scraperError && (
            <p className={`text-sm ${scraperResult.hasErrors ? 'text-red-400' : 'text-emerald-400'}`}>
              {formatScraperSummary(scraperResult)}
              {scraperResult.hasErrors &&
                scraperResult.errors?.length > 0 &&
                ` — ${scraperResult.errors.join(', ')}`}
            </p>
          )}

          {geocodeError && <p className="text-sm text-red-400">{geocodeError}</p>}
          {geocodeResult && !geocodeError && (
            <p className="text-sm text-emerald-400">{formatGeocodeSummary(geocodeResult)}</p>
          )}
        </div>

        <button
          onClick={handleSave}
          disabled={saving}
          className="w-full rounded-lg bg-blue-500 px-4 py-2 font-medium text-white transition-colors hover:bg-blue-400 disabled:opacity-50"
        >
          {saving ? 'Saving...' : 'Save Settings'}
        </button>
      </div>
    </div>
  );
}
