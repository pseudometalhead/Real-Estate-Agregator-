import React, { useContext, useState, useEffect } from 'react';
import { FilterContext } from '../context/FilterContext';

const districts = ['All', 'Lisboa', 'Porto', 'Cascais', 'Braga', 'Coimbra', 'Faro', 'Aveiro'];
const sources = ['All', 'ImoVirtual', 'CasaSapo', 'CaixaImobiliario', 'Santander', 'CustoJusto'];
const constructionStatuses = ['All', 'Em Construção', 'Nova Construção', 'Para Recuperar', 'Not Available'];
const addedWithinOptions = [
  { label: 'Any time', value: null },
  { label: 'Last 24 hours', value: 1 },
  { label: 'Last 7 days', value: 7 },
  { label: 'Last 30 days', value: 30 },
];

// true/false/null -> 'Yes'/'No'/'Both' and back, shared by every tri-state
// filter below (open-plan kitchen, elevator, parking).
function triStateToSelect(value) {
  if (value === true) return 'Yes';
  if (value === false) return 'No';
  return 'Both';
}
function selectToTriState(value) {
  if (value === 'Yes') return true;
  if (value === 'No') return false;
  return null;
}

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
    <div className="w-64 shrink-0 rounded-2xl border border-white/10 bg-white/[0.06] backdrop-blur-xl p-6 shadow-xl space-y-6 h-fit">
      <h2 className="text-xl font-bold text-white">Filters</h2>

      <div>
        <label className="block text-sm font-medium text-slate-300 mb-2">Price Range (€)</label>
        <input
          type="number"
          placeholder="Min"
          value={localFilters.priceMin}
          onChange={(e) =>
            setLocalFilters({ ...localFilters, priceMin: parseInt(e.target.value, 10) || 0 })
          }
          className="w-full px-3 py-2 border border-white/10 rounded-lg mb-2 bg-white/5 text-slate-100 placeholder:text-slate-500 focus:outline-none focus:ring-2 focus:ring-blue-500/40"
        />
        <input
          type="number"
          placeholder="Max"
          value={localFilters.priceMax}
          onChange={(e) =>
            setLocalFilters({ ...localFilters, priceMax: parseInt(e.target.value, 10) || 999999 })
          }
          className="w-full px-3 py-2 border border-white/10 rounded-lg bg-white/5 text-slate-100 placeholder:text-slate-500 focus:outline-none focus:ring-2 focus:ring-blue-500/40"
        />
      </div>

      <div>
        <label className="block text-sm font-medium text-slate-300 mb-2">Bedrooms</label>
        <select
          value={localFilters.beds ?? 'All'}
          onChange={(e) =>
            setLocalFilters({
              ...localFilters,
              beds: e.target.value === 'All' ? null : parseInt(e.target.value, 10),
            })
          }
          className="w-full px-3 py-2 border border-white/10 rounded-lg bg-white/5 text-slate-100 placeholder:text-slate-500 focus:outline-none focus:ring-2 focus:ring-blue-500/40"
        >
          <option className="bg-slate-900"  value="All">All</option>
          <option className="bg-slate-900"  value="1">1</option>
          <option className="bg-slate-900"  value="2">2</option>
          <option className="bg-slate-900"  value="3">3</option>
          <option className="bg-slate-900"  value="4">4+</option>
        </select>
      </div>

      <div>
        <label className="block text-sm font-medium text-slate-300 mb-2">Sun Orientation</label>
        <select
          value={localFilters.orientation ?? 'All'}
          onChange={(e) =>
            setLocalFilters({
              ...localFilters,
              orientation: e.target.value === 'All' ? null : e.target.value,
            })
          }
          className="w-full px-3 py-2 border border-white/10 rounded-lg bg-white/5 text-slate-100 placeholder:text-slate-500 focus:outline-none focus:ring-2 focus:ring-blue-500/40"
        >
          <option className="bg-slate-900"  value="All">All</option>
          <option className="bg-slate-900"  value="Sul">South (Sul)</option>
          <option className="bg-slate-900"  value="Norte">North (Norte)</option>
          <option className="bg-slate-900"  value="Oriente">East (Oriente)</option>
          <option className="bg-slate-900"  value="Poente">West (Poente)</option>
          <option className="bg-slate-900"  value="Not Available">Not Available</option>
        </select>
      </div>

      <div>
        <label className="block text-sm font-medium text-slate-300 mb-2">Open-plan kitchen</label>
        <select
          value={triStateToSelect(localFilters.openPlanKitchen)}
          onChange={(e) =>
            setLocalFilters({ ...localFilters, openPlanKitchen: selectToTriState(e.target.value) })
          }
          className="w-full px-3 py-2 border border-white/10 rounded-lg bg-white/5 text-slate-100 placeholder:text-slate-500 focus:outline-none focus:ring-2 focus:ring-blue-500/40"
        >
          <option className="bg-slate-900"  value="Both">Both</option>
          <option className="bg-slate-900"  value="Yes">Yes</option>
          <option className="bg-slate-900"  value="No">No / not mentioned</option>
        </select>
      </div>

      <div>
        <label className="block text-sm font-medium text-slate-300 mb-2">Elevator</label>
        <select
          value={triStateToSelect(localFilters.elevator)}
          onChange={(e) => setLocalFilters({ ...localFilters, elevator: selectToTriState(e.target.value) })}
          className="w-full px-3 py-2 border border-white/10 rounded-lg bg-white/5 text-slate-100 placeholder:text-slate-500 focus:outline-none focus:ring-2 focus:ring-blue-500/40"
        >
          <option className="bg-slate-900"  value="Both">Both</option>
          <option className="bg-slate-900"  value="Yes">Yes</option>
          <option className="bg-slate-900"  value="No">No</option>
        </select>
      </div>

      <div>
        <label className="block text-sm font-medium text-slate-300 mb-2">Parking / garage</label>
        <select
          value={triStateToSelect(localFilters.parking)}
          onChange={(e) => setLocalFilters({ ...localFilters, parking: selectToTriState(e.target.value) })}
          className="w-full px-3 py-2 border border-white/10 rounded-lg bg-white/5 text-slate-100 placeholder:text-slate-500 focus:outline-none focus:ring-2 focus:ring-blue-500/40"
        >
          <option className="bg-slate-900"  value="Both">Both</option>
          <option className="bg-slate-900"  value="Yes">Yes</option>
          <option className="bg-slate-900"  value="No">No</option>
        </select>
      </div>

      <div>
        <label className="block text-sm font-medium text-slate-300 mb-2">Construction Status</label>
        <select
          value={localFilters.constructionStatus ?? 'All'}
          onChange={(e) =>
            setLocalFilters({
              ...localFilters,
              constructionStatus: e.target.value === 'All' ? null : e.target.value,
            })
          }
          className="w-full px-3 py-2 border border-white/10 rounded-lg bg-white/5 text-slate-100 placeholder:text-slate-500 focus:outline-none focus:ring-2 focus:ring-blue-500/40"
        >
          {constructionStatuses.map((c) => (
            <option className="bg-slate-900" key={c} value={c}>
              {c}
            </option>
          ))}
        </select>
      </div>

      <div>
        <label className="block text-sm font-medium text-slate-300 mb-2">Price per m² (€)</label>
        <input
          type="number"
          placeholder="Min"
          value={localFilters.pricePerM2Min ?? ''}
          onChange={(e) =>
            setLocalFilters({
              ...localFilters,
              pricePerM2Min: e.target.value === '' ? null : parseInt(e.target.value, 10),
            })
          }
          className="w-full px-3 py-2 border border-white/10 rounded-lg mb-2 bg-white/5 text-slate-100 placeholder:text-slate-500 focus:outline-none focus:ring-2 focus:ring-blue-500/40"
        />
        <input
          type="number"
          placeholder="Max"
          value={localFilters.pricePerM2Max ?? ''}
          onChange={(e) =>
            setLocalFilters({
              ...localFilters,
              pricePerM2Max: e.target.value === '' ? null : parseInt(e.target.value, 10),
            })
          }
          className="w-full px-3 py-2 border border-white/10 rounded-lg bg-white/5 text-slate-100 placeholder:text-slate-500 focus:outline-none focus:ring-2 focus:ring-blue-500/40"
        />
      </div>

      <div>
        <label className="block text-sm font-medium text-slate-300 mb-2">Added</label>
        <select
          value={localFilters.addedWithinDays ?? ''}
          onChange={(e) =>
            setLocalFilters({
              ...localFilters,
              addedWithinDays: e.target.value === '' ? null : parseInt(e.target.value, 10),
            })
          }
          className="w-full px-3 py-2 border border-white/10 rounded-lg bg-white/5 text-slate-100 placeholder:text-slate-500 focus:outline-none focus:ring-2 focus:ring-blue-500/40"
        >
          {addedWithinOptions.map((opt) => (
            <option className="bg-slate-900" key={opt.label} value={opt.value ?? ''}>
              {opt.label}
            </option>
          ))}
        </select>
      </div>

      <div>
        <label className="block text-sm font-medium text-slate-300 mb-2">District</label>
        <select
          value={
            districts.includes(localFilters.location) ? localFilters.location : 'All'
          }
          onChange={(e) =>
            setLocalFilters({
              ...localFilters,
              location: e.target.value === 'All' ? '' : e.target.value,
            })
          }
          className="w-full px-3 py-2 border border-white/10 rounded-lg bg-white/5 text-slate-100 placeholder:text-slate-500 focus:outline-none focus:ring-2 focus:ring-blue-500/40"
        >
          {districts.map((d) => (
            <option className="bg-slate-900" key={d} value={d}>
              {d}
            </option>
          ))}
        </select>
      </div>

      <div>
        <label className="block text-sm font-medium text-slate-300 mb-2">Location</label>
        <input
          type="text"
          placeholder="Search location..."
          value={localFilters.location ?? ''}
          onChange={(e) => setLocalFilters({ ...localFilters, location: e.target.value })}
          className="w-full px-3 py-2 border border-white/10 rounded-lg bg-white/5 text-slate-100 placeholder:text-slate-500 focus:outline-none focus:ring-2 focus:ring-blue-500/40"
        />
      </div>

      <div>
        <label className="block text-sm font-medium text-slate-300 mb-2">Source</label>
        <select
          value={localFilters.source ?? 'All'}
          onChange={(e) =>
            setLocalFilters({
              ...localFilters,
              source: e.target.value === 'All' ? null : e.target.value,
            })
          }
          className="w-full px-3 py-2 border border-white/10 rounded-lg bg-white/5 text-slate-100 placeholder:text-slate-500 focus:outline-none focus:ring-2 focus:ring-blue-500/40"
        >
          {sources.map((s) => (
            <option className="bg-slate-900" key={s} value={s}>
              {s}
            </option>
          ))}
        </select>
      </div>

      <button
        onClick={handleApplyFilters}
        className="w-full rounded-lg bg-blue-500 px-4 py-2 font-medium text-white shadow-lg shadow-blue-500/30 transition-colors hover:bg-blue-400"
      >
        Apply Filters
      </button>
    </div>
  );
}
