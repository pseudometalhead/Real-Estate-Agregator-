import React from 'react';
import { PropertyCard } from './PropertyCard';

export function PropertyGrid({ properties, onAdded }) {
  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
      {properties.map((property) => (
        <PropertyCard key={property.id} property={property} onAdded={onAdded} />
      ))}
    </div>
  );
}
