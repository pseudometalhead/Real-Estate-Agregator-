import React, { useState, useEffect, useCallback } from 'react';
import { myListingsApi } from '../api/myListingsApi';
import { ContactPanel } from '../components/ContactPanel';
import { getPlatformMeta, getFaviconUrl } from '../utils/platformMeta';

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

  return (
    <div className="p-6 max-w-6xl mx-auto">
      <h1 className="text-3xl font-bold mb-6">My Properties</h1>

      <div className="flex gap-2 mb-6 flex-wrap">
        <button
          onClick={() => setStatusFilter(null)}
          className={`px-4 py-2 rounded ${!statusFilter ? 'bg-blue-600 text-white' : 'bg-gray-300'}`}
        >
          All
        </button>
        {statuses.map((status) => (
          <button
            key={status}
            onClick={() => setStatusFilter(status)}
            className={`px-4 py-2 rounded ${
              statusFilter === status ? 'bg-blue-600 text-white' : 'bg-gray-300'
            }`}
          >
            {status}
          </button>
        ))}
      </div>

      {error && <div className="mb-4 p-3 bg-red-50 text-red-700 text-sm rounded">{error}</div>}

      {loading ? (
        <div className="text-center py-12 text-gray-600">Loading...</div>
      ) : listings.length === 0 ? (
        <div className="text-center py-12 text-gray-600">No listings found.</div>
      ) : (
        <div className="space-y-4">
          {listings.map((listing) => {
            const platform = getPlatformMeta(listing.property?.source);
            const faviconUrl = getFaviconUrl(listing.property?.source);
            const photo = listing.property?.photos?.[0];

            return (
              <div key={listing.id} className="bg-white rounded-lg shadow p-6 border border-gray-200">
                <div className="flex justify-between items-start mb-4 gap-4">
                  <div className="flex gap-4 flex-1 min-w-0">
                    {photo && (
                      <img
                        src={photo}
                        alt=""
                        loading="lazy"
                        onError={(e) => (e.target.style.display = 'none')}
                        className="w-20 h-20 rounded object-cover bg-gray-100 shrink-0"
                      />
                    )}
                    <div className="min-w-0">
                      <div className="flex items-center gap-2 flex-wrap">
                        <h3 className="font-bold text-lg">
                          €{listing.property?.price?.toLocaleString()}
                        </h3>
                        <span
                          className={`flex items-center gap-1 text-xs px-2 py-0.5 rounded ${platform.color}`}
                        >
                          {faviconUrl && <img src={faviconUrl} alt="" width={12} height={12} />}
                          {platform.label}
                        </span>
                      </div>
                      <p className="text-gray-600">{listing.property?.location}</p>
                      <p className="text-sm text-gray-500">
                        Added: {new Date(listing.dateAdded).toLocaleDateString()}
                      </p>
                      {listing.commHistoryCount > 0 && (
                        <p className="text-xs text-gray-500">
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
                    className="px-3 py-2 border rounded bg-white"
                  >
                    {statuses.map((status) => (
                      <option key={status} value={status}>
                        {status}
                      </option>
                    ))}
                  </select>
                </div>

                {listing.notes && (
                  <div className="mb-4 p-3 bg-gray-50 rounded">
                    <p className="text-sm">
                      <strong>Notes:</strong> {listing.notes}
                    </p>
                  </div>
                )}

                {listing.agentName && (
                  <div className="text-sm text-gray-600 mb-4">
                    <p>
                      <strong>Agent:</strong> {listing.agentName}
                    </p>
                    {listing.agentPhone && (
                      <p>
                        <strong>Phone:</strong> {listing.agentPhone}
                      </p>
                    )}
                  </div>
                )}

                <div className="flex gap-2 flex-wrap">
                  <a
                    href={listing.property?.url}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="px-4 py-2 bg-gray-200 rounded hover:bg-gray-300 text-sm"
                  >
                    View on Site
                  </a>
                  <button
                    onClick={() => setContactingListing(listing)}
                    className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700 text-sm"
                  >
                    Contact Agent
                  </button>
                  <button
                    onClick={() => handleDelete(listing.id)}
                    className="px-4 py-2 bg-red-500 text-white rounded hover:bg-red-600 text-sm"
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
