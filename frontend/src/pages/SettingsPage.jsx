import React, { useState, useEffect } from 'react';
import { appSettingsApi } from '../api/appSettingsApi';

const sources = [
  { key: 'scrapeIdealistaEnabled', label: 'Idealista', working: false },
  { key: 'scrapeImoVirtualEnabled', label: 'ImoVirtual', working: true },
  { key: 'scrapeImobiliarioEnabled', label: 'Imobiliário', working: false },
  { key: 'scrapeCasaSapoEnabled', label: 'Casa SAPO', working: true },
  { key: 'scrapeCaixaImobiliarioEnabled', label: 'Caixa Imobiliário (CGD)', working: true },
  { key: 'scrapeSantanderEnabled', label: 'Santander', working: true },
];

export function SettingsPage() {
  const [settings, setSettings] = useState(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const [saved, setSaved] = useState(false);

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

  if (loading) return <div className="text-center py-12 text-gray-600">Loading...</div>;
  if (error && !settings)
    return <div className="text-center py-12 text-red-700">Error loading settings: {error}</div>;
  if (!settings) return null;

  return (
    <div className="p-6 max-w-2xl mx-auto">
      <h1 className="text-3xl font-bold mb-6">Scraper Settings</h1>

      {error && <div className="mb-4 p-3 bg-red-50 text-red-700 text-sm rounded">{error}</div>}
      {saved && (
        <div className="mb-4 p-3 bg-green-50 text-green-700 text-sm rounded">
          Settings saved! Scrapers will use the new configuration.
        </div>
      )}

      <div className="bg-white rounded-lg shadow p-6 space-y-6">
        <div>
          <label className="block text-sm font-medium mb-2">Districts (comma-separated)</label>
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
            className="w-full px-3 py-2 border rounded"
          />
          <p className="text-sm text-gray-600 mt-1">Where to search for properties</p>
        </div>

        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium mb-2">Min Price (€)</label>
            <input
              type="number"
              value={settings.priceMin ?? ''}
              onChange={(e) =>
                setSettings({ ...settings, priceMin: parseInt(e.target.value, 10) || 0 })
              }
              className="w-full px-3 py-2 border rounded"
            />
          </div>
          <div>
            <label className="block text-sm font-medium mb-2">Max Price (€)</label>
            <input
              type="number"
              value={settings.priceMax ?? ''}
              onChange={(e) =>
                setSettings({ ...settings, priceMax: parseInt(e.target.value, 10) || 999999 })
              }
              className="w-full px-3 py-2 border rounded"
            />
          </div>
        </div>

        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium mb-2">Min Rooms</label>
            <input
              type="number"
              value={settings.roomsMin ?? ''}
              onChange={(e) =>
                setSettings({ ...settings, roomsMin: parseInt(e.target.value, 10) || 0 })
              }
              className="w-full px-3 py-2 border rounded"
            />
          </div>
          <div>
            <label className="block text-sm font-medium mb-2">Max Rooms</label>
            <input
              type="number"
              value={settings.roomsMax ?? ''}
              onChange={(e) =>
                setSettings({ ...settings, roomsMax: parseInt(e.target.value, 10) || 10 })
              }
              className="w-full px-3 py-2 border rounded"
            />
          </div>
        </div>

        <div>
          <label className="block text-sm font-medium mb-2">Enabled Sources</label>
          <div className="flex gap-4 flex-wrap">
            {sources.map((source) => (
              <label key={source.key} className="flex items-center gap-2 text-sm">
                <input
                  type="checkbox"
                  checked={settings[source.key] ?? false}
                  onChange={(e) => setSettings({ ...settings, [source.key]: e.target.checked })}
                />
                {source.label}
                {!source.working && (
                  <span className="text-xs text-gray-400" title="Integration not available for this source">
                    (not available)
                  </span>
                )}
              </label>
            ))}
          </div>
          <p className="text-sm text-gray-600 mt-1">
            ImoVirtual, Casa SAPO, Caixa Imobiliário and Santander (the latter two are bank-owned
            property portals) run real scrapes. Idealista is blocked by bot protection and
            Imobiliário.pt is a parked domain — see the Reports page for details.
          </p>
        </div>

        {settings.lastScrapedAt && (
          <div className="p-3 bg-gray-50 rounded">
            <p className="text-sm text-gray-600">
              Last scraped: {new Date(settings.lastScrapedAt).toLocaleString()}
            </p>
          </div>
        )}

        <button
          onClick={handleSave}
          disabled={saving}
          className="w-full bg-blue-600 text-white py-2 rounded hover:bg-blue-700 disabled:opacity-50"
        >
          {saving ? 'Saving...' : 'Save Settings'}
        </button>
      </div>
    </div>
  );
}
