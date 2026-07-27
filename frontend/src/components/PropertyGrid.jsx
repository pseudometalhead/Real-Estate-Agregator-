import React from 'react';
import { PropertyCard } from './PropertyCard';

export function PropertyGrid({ properties, onAdded }) {
  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-3 gap-6 items-stretch">
      {properties.map((property) => (
        <PropertyCard key={property.id} property={property} onAdded={onAdded} />
      ))}
    </div>
  );
}
