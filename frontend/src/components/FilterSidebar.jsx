import React, { useContext, useState, useEffect } from 'react';
import { FilterContext } from '../context/FilterContext';

export function FilterSidebar() {
  const { filters, setFilters } = useContext(FilterContext);
  const [localFilters, setLocalFilters] = useState(filters);

  useEffect(() => {
    setLocalFilters(filters);
  }, [filters]);

  const handleApplyFilters = () => {
    setFilters(localFilters);
  };

  return (
    <div className="w-64 shrink-0 bg-gray-100 p-6 rounded-lg space-y-6 h-fit">
      <h2 className="text-2xl font-bold mb-6">Filters</h2>

      <div>
        <label className="block text-sm font-medium mb-2">Price Range (€)</label>
        <input
          type="number"
          placeholder="Min"
          value={localFilters.priceMin}
          onChange={(e) =>
            setLocalFilters({ ...localFilters, priceMin: parseInt(e.target.value, 10) || 0 })
          }
          className="w-full px-3 py-2 border rounded mb-2"
        />
        <input
          type="number"
          placeholder="Max"
          value={localFilters.priceMax}
          onChange={(e) =>
            setLocalFilters({ ...localFilters, priceMax: parseInt(e.target.value, 10) || 999999 })
          }
          className="w-full px-3 py-2 border rounded"
        />
      </div>

      <div>
        <label className="block text-sm font-medium mb-2">Bedrooms</label>
        <select
          value={localFilters.beds ?? 'All'}
          onChange={(e) =>
            setLocalFilters({
              ...localFilters,
              beds: e.target.value === 'All' ? null : parseInt(e.target.value, 10),
            })
          }
          className="w-full px-3 py-2 border rounded bg-white"
        >
          <option value="All">All</option>
          <option value="1">1</option>
          <option value="2">2</option>
          <option value="3">3</option>
          <option value="4">4+</option>
        </select>
      </div>

      <div>
        <label className="block text-sm font-medium mb-2">Sun Orientation</label>
        <select
          value={localFilters.orientation ?? 'All'}
          onChange={(e) =>
            setLocalFilters({
              ...localFilters,
              orientation: e.target.value === 'All' ? null : e.target.value,
            })
          }
          className="w-full px-3 py-2 border rounded bg-white"
        >
          <option value="All">All</option>
          <option value="Sul">South (Sul)</option>
          <option value="Norte">North (Norte)</option>
          <option value="Oriente">East (Oriente)</option>
          <option value="Poente">West (Poente)</option>
          <option value="Not Available">Not Available</option>
        </select>
      </div>

      <div>
        <label className="block text-sm font-medium mb-2">Location</label>
        <input
          type="text"
          placeholder="Search location..."
          value={localFilters.location ?? ''}
          onChange={(e) => setLocalFilters({ ...localFilters, location: e.target.value })}
          className="w-full px-3 py-2 border rounded"
        />
      </div>

      <button
        onClick={handleApplyFilters}
        className="w-full bg-blue-600 text-white py-2 rounded hover:bg-blue-700"
      >
        Apply Filters
      </button>
    </div>
  );
}
