import React, { createContext, useState, useMemo } from 'react';

export const FilterContext = createContext();

const defaultFilters = {
  priceMin: 0,
  priceMax: 999999,
  beds: null,
  orientation: null,
  // Tri-state (true/false/null="Both") — see FilterSidebar's selects.
  openPlanKitchen: null,
  constructionStatus: null,
  elevator: null,
  parking: null,
  pricePerM2Min: null,
  pricePerM2Max: null,
  addedWithinDays: null,
  // Only properties with no MyListing at all — set via the Report page's
  // "Review Now" click-through, not exposed in FilterSidebar itself yet.
  // Name must match backend's FilterQueryDto.PendingActionOnly exactly
  // (case-insensitively) for ASP.NET Core's query-string model binding to
  // pick it up — there's no [FromQuery(Name=...)] alias on that property.
  pendingActionOnly: null,
  location: '',
  source: null,
  sortBy: 'date',
  sortDir: 'desc',
  page: 1,
};

export function FilterProvider({ children }) {
  const [filters, setFiltersState] = useState(defaultFilters);

  // Any change other than a direct page change should reset pagination back
  // to page 1, otherwise users can land on an out-of-range page after
  // narrowing their filters.
  const setFilters = (updater) => {
    setFiltersState((prev) => {
      const next = typeof updater === 'function' ? updater(prev) : updater;
      const filtersChanged = Object.keys(next).some(
        (key) => key !== 'page' && next[key] !== prev[key]
      );
      return filtersChanged ? { ...next, page: 1 } : next;
    });
  };

  const setPage = (page) => setFiltersState((prev) => ({ ...prev, page }));

  const value = useMemo(() => ({ filters, setFilters, setPage }), [filters]);

  return <FilterContext.Provider value={value}>{children}</FilterContext.Provider>;
}
