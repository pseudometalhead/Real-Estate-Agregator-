import React from 'react';

const statusColor = (hasErrors) =>
  hasErrors ? 'bg-red-100 text-red-800' : 'bg-green-100 text-green-800';

export function DailyReport({ report }) {
  if (!report) return null;

  return (
    <div className="space-y-6">
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
        <div className="bg-white rounded-lg shadow p-4 border border-gray-200">
          <p className="text-sm text-gray-600">Added (last 24h)</p>
          <p className="text-3xl font-bold">{report.propertiesAddedLast24h}</p>
        </div>
        <div className="bg-white rounded-lg shadow p-4 border border-gray-200">
          <p className="text-sm text-gray-600">Updated (last 24h)</p>
          <p className="text-3xl font-bold">{report.propertiesUpdatedLast24h}</p>
        </div>
        <div className="bg-white rounded-lg shadow p-4 border border-gray-200">
          <p className="text-sm text-gray-600">Average Price</p>
          <p className="text-3xl font-bold">
            {report.averagePrice ? `€${Math.round(report.averagePrice).toLocaleString()}` : '—'}
          </p>
        </div>
      </div>

      <div>
        <h3 className="font-semibold mb-2">Sun Orientation Breakdown</h3>
        <div className="flex flex-wrap gap-2">
          {report.topOrientations.map((o) => (
            <span key={o.orientation} className="px-3 py-1 bg-gray-100 rounded text-sm">
              {o.orientation}: <strong>{o.count}</strong>
            </span>
          ))}
        </div>
      </div>

      <div>
        <h3 className="font-semibold mb-2">Scraper Runs (last 24h)</h3>
        {report.scraperRuns.length === 0 ? (
          <p className="text-gray-600 text-sm">No scraper runs in the last 24 hours.</p>
        ) : (
          <div className="space-y-2">
            {report.scraperRuns.map((run, idx) => (
              <div
                key={`${run.source}-${idx}`}
                className="flex items-center justify-between bg-white border border-gray-200 rounded p-3"
              >
                <div>
                  <p className="font-medium">{run.source}</p>
                  <p className="text-xs text-gray-500">
                    Found {run.propertiesFound} · Added {run.propertiesAdded} · Updated{' '}
                    {run.propertiesUpdated} · Skipped {run.propertiesSkipped}
                  </p>
                  {run.errors?.length > 0 && (
                    <p className="text-xs text-red-600 mt-1">{run.errors.join(', ')}</p>
                  )}
                </div>
                <span className={`text-xs px-2 py-1 rounded ${statusColor(run.hasErrors)}`}>
                  {run.hasErrors ? 'Error' : 'OK'}
                </span>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
