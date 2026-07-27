import React, { useState, useEffect, useContext, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { FilterSidebar } from '../components/FilterSidebar';
import { PropertyGrid } from '../components/PropertyGrid';
import { Map } from '../components/Map';
import { FilterContext } from '../context/FilterContext';
import { propertiesApi } from '../api/propertiesApi';
import { PropertyCardSkeleton } from '../components/Skeleton';

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
  const navigate = useNavigate();
  const { filters, setFilters, setPage } = useContext(FilterContext);
  const [properties, setProperties] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [total, setTotal] = useState(0);
  const [showMap, setShowMap] = useState(false);
  const [mapProperties, setMapProperties] = useState([]);
  const [mapLoading, setMapLoading] = useState(false);
  const pageSize = 12;
  // Well above the current property count so a single request covers every
  // match — the map should plot every result, not just the current grid page.
  const MAP_PAGE_SIZE = 2000;

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

  useEffect(() => {
    if (!showMap) return;
    let cancelled = false;
    setMapLoading(true);
    propertiesApi
      .getProperties({ ...filters, page: 1, pageSize: MAP_PAGE_SIZE })
      .then((result) => {
        if (!cancelled) setMapProperties(result.items);
      })
      .catch(() => {
        if (!cancelled) setMapProperties([]);
      })
      .finally(() => {
        if (!cancelled) setMapLoading(false);
      });
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [showMap, filters.priceMin, filters.priceMax, filters.beds, filters.orientation, filters.location, filters.source]);

  const totalPages = Math.max(1, Math.ceil(total / pageSize));

  const handleSortChange = (value) => {
    const [sortBy, sortDir] = value === 'date' ? ['date', 'desc'] : value.split('-');
    setFilters({ ...filters, sortBy, sortDir });
  };

  const mappable = mapProperties.filter((p) => p.lat != null && p.lng != null);

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 py-6">
      <div className="flex gap-6">
        <FilterSidebar />
        <div className="flex-1 min-w-0">
          <div className="mb-6 flex items-end justify-between flex-wrap gap-4">
            <div>
              <h1 className="text-3xl font-bold text-white mb-2">Properties</h1>
              <p className="text-slate-400">
                Showing {properties.length} of {total} properties
              </p>
            </div>
            <div className="flex items-center gap-2">
              <select
                value={sortValueFor(filters)}
                onChange={(e) => handleSortChange(e.target.value)}
                className="px-3 py-2 border border-white/10 rounded-lg bg-white/[0.06] backdrop-blur-xl text-sm text-slate-100 focus:outline-none focus:ring-2 focus:ring-blue-500/40"
              >
                {sortOptions.map((opt) => (
                  <option key={opt.value} value={opt.value} className="bg-slate-900">
                    {opt.label}
                  </option>
                ))}
              </select>
              <button
                onClick={() => setShowMap((v) => !v)}
                className={`rounded-lg px-4 py-2 text-sm font-medium transition-colors ${
                  showMap
                    ? 'bg-blue-500 text-white shadow-lg shadow-blue-500/30'
                    : 'bg-white/10 text-slate-200 hover:bg-white/20'
                }`}
              >
                🗺️ Map
              </button>
            </div>
          </div>

          {error && (
            <div className="mb-4 p-3 rounded-xl border border-red-500/20 bg-red-500/10 text-red-400 text-sm">
              {error}
            </div>
          )}

          {showMap && (
            <div className="mb-6">
              {mapLoading ? (
                <div className="h-80 rounded-2xl border border-white/10 bg-white/[0.06] backdrop-blur-xl flex items-center justify-center text-slate-400 text-sm">
                  Loading map pins...
                </div>
              ) : (
                <>
                  <Map properties={mappable} onSelectProperty={(p) => navigate(`/property/${p.id}`)} />
                  {mappable.length < mapProperties.length && (
                    <p className="text-xs text-slate-500 mt-1">
                      {mapProperties.length - mappable.length} of {mapProperties.length} matching
                      properties aren't geocoded yet — they'll appear after the next scraper run.
                    </p>
                  )}
                </>
              )}
            </div>
          )}

          {loading ? (
            <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-3 gap-6">
              {Array.from({ length: 6 }).map((_, i) => (
                <PropertyCardSkeleton key={i} />
              ))}
            </div>
          ) : properties.length === 0 ? (
            <div className="text-center py-16 text-slate-400">
              <p className="text-4xl mb-3">🏚️</p>
              <p className="font-medium text-slate-200">No properties found</p>
              <p className="text-sm text-slate-500 mt-1">Try adjusting your filters.</p>
            </div>
          ) : (
            <>
              <PropertyGrid properties={properties} onAdded={fetchProperties} />

              <div className="flex justify-center items-center gap-2 mt-8">
                <button
                  onClick={() => setPage(Math.max(1, filters.page - 1))}
                  disabled={filters.page === 1}
                  className="rounded-lg bg-white/10 px-4 py-2 font-medium text-slate-200 transition-colors hover:bg-white/20 disabled:opacity-40"
                >
                  Previous
                </button>
                <span className="px-4 py-2 text-slate-300">
                  Page {filters.page} of {totalPages}
                </span>
                <button
                  onClick={() => setPage(filters.page + 1)}
                  disabled={filters.page >= totalPages}
                  className="rounded-lg bg-white/10 px-4 py-2 font-medium text-slate-200 transition-colors hover:bg-white/20 disabled:opacity-40"
                >
                  Next
                </button>
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  );
}
