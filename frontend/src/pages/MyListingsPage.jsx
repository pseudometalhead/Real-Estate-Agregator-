import React, { useState, useEffect, useCallback } from 'react';
import { myListingsApi } from '../api/myListingsApi';
import { ContactPanel } from '../components/ContactPanel';
import { getPlatformMeta, getFaviconUrl } from '../utils/platformMeta';
import { toCsv, downloadCsv } from '../utils/csv';
import { ListRowSkeleton } from '../components/Skeleton';

const csvColumns = [
  { header: 'Status', get: (l) => l.status },
  { header: 'Price', get: (l) => l.property?.price },
  { header: 'Location', get: (l) => l.property?.location },
  { header: 'Beds', get: (l) => l.property?.beds },
  { header: 'Size (m2)', get: (l) => l.property?.sizeM2 },
  { header: 'Sun Orientation', get: (l) => l.property?.sunOrientation },
  { header: 'Source', get: (l) => l.property?.source },
  { header: 'Agent Name', get: (l) => l.agentName },
  { header: 'Agent Phone', get: (l) => l.agentPhone },
  { header: 'Agent Email', get: (l) => l.agentEmail },
  { header: 'Notes', get: (l) => l.notes },
  { header: 'Date Added', get: (l) => new Date(l.dateAdded).toLocaleDateString() },
  { header: 'Contacts Logged', get: (l) => l.commHistoryCount },
  { header: 'Last Contacted', get: (l) => (l.lastContactedAt ? new Date(l.lastContactedAt).toLocaleDateString() : '') },
  { header: 'URL', get: (l) => l.property?.url },
];

const statuses = ['Interested', 'Contacted', 'Waiting', 'Rejected'];

export function MyListingsPage() {
  const [listings, setListings] = useState([]);
  const [statusFilter, setStatusFilter] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [contactingListing, setContactingListing] = useState(null);

  const fetchListings = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await myListingsApi.getMyListings({ status: statusFilter });
      setListings(result.items ?? []);
    } catch (err) {
      setError(err.message ?? 'Failed to fetch listings');
    } finally {
      setLoading(false);
    }
  }, [statusFilter]);

  useEffect(() => {
    fetchListings();
  }, [fetchListings]);

  const handleStatusChange = async (listing, newStatus) => {
    try {
      // The update endpoint replaces the whole record, so the existing
      // fields are sent back alongside the new status to avoid wiping out
      // notes/agent info with an inline status-only change.
      await myListingsApi.updateMyListing(listing.id, {
        status: newStatus,
        notes: listing.notes,
        agentName: listing.agentName,
        agentPhone: listing.agentPhone,
        agentEmail: listing.agentEmail,
        askedAboutOrientation: listing.askedAboutOrientation,
        followUpDate: listing.followUpDate,
      });
      setListings((prev) =>
        prev.map((l) => (l.id === listing.id ? { ...l, status: newStatus } : l))
      );
      // Re-marking Interested gets the same one-tap send moment as first
      // adding a listing — see PropertyCard's post-add flow.
      if (newStatus === 'Interested') {
        setContactingListing({ ...listing, status: newStatus });
      }
    } catch (err) {
      setError(err.message ?? 'Failed to update status');
    }
  };

  const handleDelete = async (listingId) => {
    if (!window.confirm('Delete this listing?')) return;
    try {
      await myListingsApi.deleteMyListing(listingId);
      setListings((prev) => prev.filter((l) => l.id !== listingId));
    } catch (err) {
      setError(err.message ?? 'Failed to delete listing');
    }
  };

  const handleExportCsv = () => {
    const csv = toCsv(listings, csvColumns);
    const suffix = statusFilter ? `-${statusFilter.toLowerCase()}` : '';
    downloadCsv(`my-properties${suffix}-${new Date().toISOString().slice(0, 10)}.csv`, csv);
  };

  return (
    <div className="p-6 max-w-6xl mx-auto">
      <div className="flex justify-between items-center mb-6 flex-wrap gap-4">
        <h1 className="text-3xl font-bold text-white">My Properties</h1>
        <button
          onClick={handleExportCsv}
          disabled={listings.length === 0}
          className="rounded-lg bg-white/10 px-4 py-2 text-sm font-medium text-slate-300 transition-colors hover:bg-white/20 disabled:opacity-50"
        >
          ⬇ Export CSV
        </button>
      </div>

      <div className="flex gap-2 mb-6 flex-wrap">
        <button
          onClick={() => setStatusFilter(null)}
          className={`rounded-lg px-4 py-2 font-medium transition-colors ${
            !statusFilter ? 'bg-blue-500 text-white' : 'bg-white/10 text-slate-300 hover:bg-white/20'
          }`}
        >
          All
        </button>
        {statuses.map((status) => (
          <button
            key={status}
            onClick={() => setStatusFilter(status)}
            className={`rounded-lg px-4 py-2 font-medium transition-colors ${
              statusFilter === status
                ? 'bg-blue-500 text-white'
                : 'bg-white/10 text-slate-300 hover:bg-white/20'
            }`}
          >
            {status}
          </button>
        ))}
      </div>

      {error && (
        <div className="mb-4 rounded-xl border border-red-500/20 bg-red-500/10 p-3 text-sm text-red-400">
          {error}
        </div>
      )}

      {loading ? (
        <div className="space-y-4">
          {Array.from({ length: 3 }).map((_, i) => (
            <ListRowSkeleton key={i} />
          ))}
        </div>
      ) : listings.length === 0 ? (
        <div className="py-16 text-center text-slate-400">
          <p className="text-4xl mb-3">📋</p>
          <p className="font-medium text-slate-300">No listings found</p>
          <p className="text-sm text-slate-400 mt-1">
            Add properties from Browse to start tracking them here.
          </p>
        </div>
      ) : (
        <div className="space-y-4">
          {listings.map((listing) => {
            const platform = getPlatformMeta(listing.property?.source);
            const faviconUrl = getFaviconUrl(listing.property?.source);
            const photo = listing.property?.photos?.[0];

            return (
              <div
                key={listing.id}
                className="rounded-xl border border-white/10 bg-white/[0.06] backdrop-blur-xl shadow-sm hover:shadow-lg hover:border-white/10 transition-all p-6"
              >
                <div className="flex flex-col sm:flex-row sm:justify-between sm:items-start mb-4 gap-4">
                  <div className="flex gap-4 flex-1 min-w-0">
                    {photo && (
                      <img
                        src={photo}
                        alt=""
                        loading="lazy"
                        onError={(e) => (e.target.style.display = 'none')}
                        className="w-20 h-20 rounded-lg object-cover bg-white/10 shrink-0"
                      />
                    )}
                    <div className="min-w-0">
                      <div className="flex items-center gap-2 flex-wrap">
                        <h3 className="font-bold text-lg text-white">
                          €{listing.property?.price?.toLocaleString()}
                        </h3>
                        <span
                          className={`inline-flex items-center gap-1 rounded-full px-2.5 py-0.5 text-xs font-medium ${platform.color}`}
                        >
                          {faviconUrl && <img src={faviconUrl} alt="" width={12} height={12} />}
                          {platform.label}
                        </span>
                      </div>
                      <p className="text-slate-400">{listing.property?.location}</p>
                      <p className="text-sm text-slate-400">
                        Added: {new Date(listing.dateAdded).toLocaleDateString()}
                      </p>
                      {listing.commHistoryCount > 0 && (
                        <p className="text-xs text-slate-400">
                          {listing.commHistoryCount} contact
                          {listing.commHistoryCount === 1 ? '' : 's'} logged
                          {listing.lastContactedAt &&
                            ` · last ${new Date(listing.lastContactedAt).toLocaleDateString()}`}
                        </p>
                      )}
                    </div>
                  </div>
                  <select
                    value={listing.status}
                    onChange={(e) => handleStatusChange(listing, e.target.value)}
                    className="rounded-lg border border-white/10 bg-white/10 px-3 py-2 text-sm text-slate-300 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
                  >
                    {statuses.map((status) => (
                      <option key={status} value={status} className="bg-slate-900">
                        {status}
                      </option>
                    ))}
                  </select>
                </div>

                {listing.notes && (
                  <div className="mb-4 rounded-xl border border-white/10 bg-white/5 p-3">
                    <p className="text-sm text-slate-300">
                      <strong className="text-white">Notes:</strong> {listing.notes}
                    </p>
                  </div>
                )}

                {listing.agentName && (
                  <div className="text-sm text-slate-400 mb-4">
                    <p>
                      <strong className="text-white">Agent:</strong> {listing.agentName}
                    </p>
                    {listing.agentPhone && (
                      <p>
                        <strong className="text-white">Phone:</strong> {listing.agentPhone}
                      </p>
                    )}
                  </div>
                )}

                <div className="flex gap-2 flex-wrap">
                  <a
                    href={listing.property?.url}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="rounded-lg bg-white/10 px-4 py-2 text-sm font-medium text-slate-300 transition-colors hover:bg-white/20"
                  >
                    View on Site
                  </a>
                  <button
                    onClick={() => setContactingListing(listing)}
                    className="rounded-lg bg-blue-500 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-400"
                  >
                    Contact Agent
                  </button>
                  <button
                    onClick={() => handleDelete(listing.id)}
                    className="rounded-lg bg-red-500/100 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-red-400"
                  >
                    Delete
                  </button>
                </div>
              </div>
            );
          })}
        </div>
      )}

      {contactingListing && (
        <ContactPanel
          listing={contactingListing}
          onClose={() => setContactingListing(null)}
          onLogged={fetchListings}
        />
      )}
    </div>
  );
}
