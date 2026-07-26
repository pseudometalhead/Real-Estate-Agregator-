import React, { createContext, useState, useMemo } from 'react';

export const FilterContext = createContext();

const defaultFilters = {
  priceMin: 0,
  priceMax: 999999,
  beds: null,
  orientation: null,
  location: '',
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
