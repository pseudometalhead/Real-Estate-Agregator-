import React from 'react';
import { useNavigate } from 'react-router-dom';
import { getPlatformMeta, getFaviconUrl } from '../utils/platformMeta';

// Same channel icon convention as ContactPanel.jsx's channelLabels.
const channelIcons = {
  WhatsApp: '💚',
  Email: '✉️',
  SMS: '💬',
  Phone: '📞',
  Site: '🌐',
  InPerson: '🤝',
  Other: '💬',
};

const statusStyles = {
  Interested: 'bg-blue-500/10 text-blue-400',
  Contacted: 'bg-amber-500/10 text-amber-400',
  Waiting: 'bg-violet-500/10 text-violet-400',
  Rejected: 'bg-white/10 text-slate-400',
};
// Every status should always show, even at 0, so the pipeline shape reads
// consistently run to run — StatusBreakdown from the API only includes
// statuses that actually have at least one MyListing.
const allStatuses = ['Interested', 'Contacted', 'Waiting', 'Rejected'];

function relativeTime(iso) {
  if (!iso) return 'Never synced';
  const diffMs = Date.now() - new Date(iso).getTime();
  const mins = Math.round(diffMs / 60000);
  if (mins < 1) return 'Just now';
  if (mins < 60) return `${mins}m ago`;
  const hours = Math.round(mins / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.round(hours / 24);
  return `${days}d ago`;
}

export function DailyReport({ report }) {
  const navigate = useNavigate();

  if (!report) return null;

  const statusCounts = Object.fromEntries(
    (report.statusBreakdown ?? []).map((s) => [s.status, s.count])
  );

  // The homepage ("/") is the swipe-to-triage queue now, which already
  // shows exactly this — pending-action properties, no separate filter to
  // apply. Simpler than the old "open Browse pre-filtered" version.
  const handleReviewPending = () => navigate('/');

  return (
    <div className="space-y-8">
      <div>
        <h2 className="text-xl font-bold text-white mb-3">At a Glance (last 24h)</h2>
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
          <div className="rounded-2xl border border-white/10 bg-white/[0.06] backdrop-blur-xl shadow-xl p-6">
            <p className="text-sm text-slate-400 mb-1">📞 Contacts Made</p>
            <p className="text-4xl font-bold text-white mb-3">{report.contactsMadeLast24h}</p>
            {report.recentContacts?.length > 0 ? (
              <div className="space-y-1.5">
                {report.recentContacts.map((c, i) => (
                  <div key={i} className="flex items-center gap-1.5 text-xs text-slate-400">
                    <span>{channelIcons[c.channel] ?? '💬'}</span>
                    <span className="truncate">
                      {c.propertyLocation ?? 'Unknown location'}
                      {c.propertyPrice ? ` · €${c.propertyPrice.toLocaleString()}` : ''}
                    </span>
                  </div>
                ))}
              </div>
            ) : (
              <p className="text-xs text-slate-500">No contacts logged yet today.</p>
            )}
          </div>

          <div className="rounded-2xl border border-white/10 bg-white/[0.06] backdrop-blur-xl shadow-xl p-6 flex flex-col">
            <p className="text-sm text-slate-400 mb-1">🏠 Pending Action</p>
            <p className="text-4xl font-bold text-white mb-3">{report.newListingsPendingAction}</p>
            <p className="text-xs text-slate-500 mb-3 flex-1">
              New listings from today you haven't reacted to yet.
            </p>
            {report.newListingsPendingAction > 0 && (
              <button
                onClick={handleReviewPending}
                className="self-start rounded-lg bg-blue-500 px-3 py-1.5 text-sm font-medium text-white shadow-lg shadow-blue-500/30 transition-colors hover:bg-blue-400"
              >
                Review Now →
              </button>
            )}
          </div>

          <div className="rounded-2xl border border-white/10 bg-white/[0.06] backdrop-blur-xl shadow-xl p-6">
            <p className="text-sm text-slate-400 mb-3">📊 Pipeline (all time)</p>
            <div className="flex flex-wrap gap-2">
              {allStatuses.map((status) => (
                <span
                  key={status}
                  className={`inline-flex items-center rounded-full px-2.5 py-1 text-xs font-medium ${statusStyles[status]}`}
                >
                  {status}: <strong className="ml-1">{statusCounts[status] ?? 0}</strong>
                </span>
              ))}
            </div>
          </div>
        </div>
      </div>

      <div>
        <h2 className="text-xl font-bold text-white mb-3">Platforms</h2>
        <div className="rounded-2xl border border-white/10 bg-white/[0.06] backdrop-blur-xl shadow-xl divide-y divide-white/10">
          {(report.platforms ?? []).map((p) => {
            const meta = getPlatformMeta(p.source);
            const favicon = getFaviconUrl(p.source);
            return (
              <div key={p.source} className="px-5 py-3">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    {favicon && <img src={favicon} alt="" width={16} height={16} />}
                    <span className="font-medium text-white">{meta.label}</span>
                    {!p.enabled && (
                      <span className="rounded-full bg-white/10 px-2 py-0.5 text-xs text-slate-500">
                        disabled
                      </span>
                    )}
                  </div>
                  <div className="flex items-center gap-2">
                    <span className="text-sm text-slate-400">{relativeTime(p.lastSyncedAt)}</span>
                    <span
                      className={`h-2 w-2 rounded-full ${
                        !p.lastSyncedAt
                          ? 'bg-slate-600'
                          : p.hasErrors
                          ? 'bg-red-500'
                          : 'bg-emerald-500'
                      }`}
                      title={!p.lastSyncedAt ? 'Never synced' : p.hasErrors ? 'Last run had errors' : 'OK'}
                    />
                  </div>
                </div>
                {p.hasErrors && p.lastErrors?.length > 0 && (
                  <div className="mt-2 rounded-lg border border-red-500/20 bg-red-500/10 px-3 py-2 text-xs text-red-400">
                    {p.lastErrors.map((err, i) => (
                      <p key={i} className={i > 0 ? 'mt-1' : ''}>
                        {err}
                      </p>
                    ))}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}
