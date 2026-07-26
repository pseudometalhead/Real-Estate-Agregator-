import React, { useState, useEffect, useContext, useCallback } from 'react';
import { FilterSidebar } from '../components/FilterSidebar';
import { PropertyGrid } from '../components/PropertyGrid';
import { Map } from '../components/Map';
import { FilterContext } from '../context/FilterContext';
import { propertiesApi } from '../api/propertiesApi';

const sortOptions = [
  { value: 'date', label: 'Newest first' },
  { value: 'price-asc', label: 'Price: low to high' },
  { value: 'price-desc', label: 'Price: high to low' },
  { value: 'size-desc', label: 'Size: largest first' },
];

function sortValueFor(filters) {
  if (filters.sortBy === 'date') return 'date';
  return `${filters.sortBy}-${filters.sortDir}`;
}

export function BrowsePage() {
  const { filters, setFilters, setPage } = useContext(FilterContext);
  const [properties, setProperties] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [total, setTotal] = useState(0);
  const [showMap, setShowMap] = useState(false);
  const pageSize = 12;

  const fetchProperties = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await propertiesApi.getProperties({ ...filters, pageSize });
      setProperties(result.items);
      setTotal(result.total);
    } catch (err) {
      setError(err.message ?? 'Failed to fetch properties');
    } finally {
      setLoading(false);
    }
  }, [filters]);

  useEffect(() => {
    fetchProperties();
  }, [fetchProperties]);

  const totalPages = Math.max(1, Math.ceil(total / pageSize));

  const handleSortChange = (value) => {
    const [sortBy, sortDir] = value === 'date' ? ['date', 'desc'] : value.split('-');
    setFilters({ ...filters, sortBy, sortDir });
  };

  const mappable = properties.filter((p) => p.lat != null && p.lng != null);

  return (
    <div className="flex gap-6 p-6 max-w-6xl mx-auto">
      <FilterSidebar />
      <div className="flex-1 min-w-0">
        <div className="mb-6 flex items-end justify-between flex-wrap gap-4">
          <div>
            <h1 className="text-3xl font-bold mb-2">Properties</h1>
            <p className="text-gray-600">
              Showing {properties.length} of {total} properties
            </p>
          </div>
          <div className="flex items-center gap-2">
            <select
              value={sortValueFor(filters)}
              onChange={(e) => handleSortChange(e.target.value)}
              className="px-3 py-2 border rounded bg-white text-sm"
            >
              {sortOptions.map((opt) => (
                <option key={opt.value} value={opt.value}>
                  {opt.label}
                </option>
              ))}
            </select>
            <button
              onClick={() => setShowMap((v) => !v)}
              className={`px-3 py-2 rounded text-sm border ${
                showMap ? 'bg-blue-600 text-white border-blue-600' : 'bg-white border-gray-300'
              }`}
            >
              🗺️ Map
            </button>
          </div>
        </div>

        {error && <div className="mb-4 p-3 bg-red-50 text-red-700 text-sm rounded">{error}</div>}

        {showMap && !loading && (
          <div className="mb-6">
            <Map properties={mappable} />
            {mappable.length < properties.length && (
              <p className="text-xs text-gray-500 mt-1">
                {properties.length - mappable.length} of {properties.length} properties on this
                page aren't geocoded yet — they'll appear after the next scraper run.
              </p>
            )}
          </div>
        )}

        {loading ? (
          <div className="text-center py-12 text-gray-600">Loading...</div>
        ) : properties.length === 0 ? (
          <div className="text-center py-12 text-gray-600">
            No properties found. Try adjusting your filters.
          </div>
        ) : (
          <>
            <PropertyGrid properties={properties} onAdded={fetchProperties} />

            <div className="flex justify-center items-center gap-2 mt-8">
              <button
                onClick={() => setPage(Math.max(1, filters.page - 1))}
                disabled={filters.page === 1}
                className="px-4 py-2 bg-gray-300 rounded disabled:opacity-50"
              >
                Previous
              </button>
              <span className="px-4 py-2">
                Page {filters.page} of {totalPages}
              </span>
              <button
                onClick={() => setPage(filters.page + 1)}
                disabled={filters.page >= totalPages}
                className="px-4 py-2 bg-gray-300 rounded disabled:opacity-50"
              >
                Next
              </button>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
