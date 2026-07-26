import React, { useCallback, useEffect, useState } from 'react';
import { reportsApi } from '../api/reportsApi';
import { DailyReport } from '../components/DailyReport';

export function ReportPage() {
  const [report, setReport] = useState(null);
  const [loading, setLoading] = useState(true);
  const [running, setRunning] = useState(false);
  const [error, setError] = useState(null);

  const fetchReport = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await reportsApi.getDailyReport();
      setReport(data);
    } catch (err) {
      setError(err.message ?? 'Failed to fetch report');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchReport();
  }, [fetchReport]);

  const handleRunNow = async () => {
    setRunning(true);
    setError(null);
    try {
      await reportsApi.runScrapersNow();
      await fetchReport();
    } catch (err) {
      setError(err.message ?? 'Failed to run scrapers');
    } finally {
      setRunning(false);
    }
  };

  return (
    <div className="p-6 max-w-4xl mx-auto">
      <div className="flex justify-between items-center mb-6 flex-wrap gap-4">
        <h1 className="text-3xl font-bold">Daily Report</h1>
        <button
          onClick={handleRunNow}
          disabled={running}
          className="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700 disabled:opacity-50 text-sm"
        >
          {running ? 'Running scrapers...' : 'Run scrapers now'}
        </button>
      </div>

      {error && <div className="mb-4 p-3 bg-red-50 text-red-700 text-sm rounded">{error}</div>}

      {loading ? (
        <div className="text-center py-12 text-gray-600">Loading...</div>
      ) : (
        <DailyReport report={report} />
      )}
    </div>
  );
}
