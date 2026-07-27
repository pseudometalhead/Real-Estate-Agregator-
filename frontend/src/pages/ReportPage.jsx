import React, { useCallback, useEffect, useState } from 'react';
import { reportsApi } from '../api/reportsApi';
import { DailyReport } from '../components/DailyReport';
import { Skeleton } from '../components/Skeleton';

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
        <h1 className="text-3xl font-bold text-white">Daily Report</h1>
        <button
          onClick={handleRunNow}
          disabled={running}
          className="rounded-lg bg-blue-500 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-400 disabled:opacity-50"
        >
          {running ? 'Running scrapers...' : 'Run scrapers now'}
        </button>
      </div>

      {error && (
        <div className="mb-4 rounded-xl border border-red-500/20 bg-red-500/10 p-3 text-sm text-red-400">
          {error}
        </div>
      )}

      {loading ? (
        <div className="space-y-6">
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
            <Skeleton className="h-24" />
            <Skeleton className="h-24" />
            <Skeleton className="h-24" />
          </div>
          <Skeleton className="h-40" />
        </div>
      ) : (
        <DailyReport report={report} />
      )}
    </div>
  );
}
