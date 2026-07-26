import React, { useState } from 'react';
import { myListingsApi } from '../api/myListingsApi';

export function MyListingModal({ property, onClose, onAdded }) {
  const [notes, setNotes] = useState('');
  const [agentName, setAgentName] = useState('');
  const [agentPhone, setAgentPhone] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setLoading(true);
    setError(null);

    try {
      await myListingsApi.addToMyListings({
        propertyId: property.id,
        notes,
        agentName,
        agentPhone,
      });
      onAdded?.();
      onClose();
    } catch (err) {
      setError(err.response?.data ?? err.message ?? 'Failed to add property');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-lg p-6 max-w-md w-full">
        <h2 className="text-2xl font-bold mb-4">Add to My List</h2>

        <div className="mb-4 p-3 bg-gray-100 rounded">
          <p className="font-semibold">€{property.price?.toLocaleString()}</p>
          <p className="text-sm text-gray-600">{property.location}</p>
        </div>

        {error && (
          <div className="mb-4 p-3 bg-red-50 text-red-700 text-sm rounded">{String(error)}</div>
        )}

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="block text-sm font-medium mb-1">Notes</label>
            <textarea
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              placeholder="Why are you interested? Any concerns?"
              rows={3}
              className="w-full px-3 py-2 border rounded"
            />
          </div>

          <div>
            <label className="block text-sm font-medium mb-1">Agent Name (optional)</label>
            <input
              type="text"
              value={agentName}
              onChange={(e) => setAgentName(e.target.value)}
              className="w-full px-3 py-2 border rounded"
            />
          </div>

          <div>
            <label className="block text-sm font-medium mb-1">Agent Phone (optional)</label>
            <input
              type="tel"
              value={agentPhone}
              onChange={(e) => setAgentPhone(e.target.value)}
              className="w-full px-3 py-2 border rounded"
            />
          </div>

          <div className="flex gap-2">
            <button
              type="submit"
              disabled={loading}
              className="flex-1 bg-blue-600 text-white py-2 rounded hover:bg-blue-700 disabled:opacity-50"
            >
              {loading ? 'Adding...' : 'Add to List'}
            </button>
            <button
              type="button"
              onClick={onClose}
              className="flex-1 bg-gray-300 text-black py-2 rounded hover:bg-gray-400"
            >
              Cancel
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
