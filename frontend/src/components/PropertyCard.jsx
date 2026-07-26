import React, { useState } from 'react';
import { MyListingModal } from './MyListingModal';
import { getPlatformMeta, getFaviconUrl } from '../utils/platformMeta';

const orientationStyles = {
  Sul: 'bg-green-100 text-green-800',
  Norte: 'bg-blue-100 text-blue-800',
  Oriente: 'bg-yellow-100 text-yellow-800',
  Poente: 'bg-orange-100 text-orange-800',
  'Not Available': 'bg-gray-100 text-gray-800',
};

const ONE_DAY_MS = 24 * 60 * 60 * 1000;

function isNew(createdAt) {
  if (!createdAt) return false;
  return Date.now() - new Date(createdAt).getTime() < ONE_DAY_MS;
}

function PropertyPhoto({ photos, alt }) {
  const [failed, setFailed] = useState(false);
  const url = photos?.[0];

  if (!url || failed) {
    return (
      <div className="w-full h-40 rounded-md bg-gray-100 flex items-center justify-center text-4xl mb-3">
        🏠
      </div>
    );
  }

  return (
    <img
      src={url}
      alt={alt}
      loading="lazy"
      onError={() => setFailed(true)}
      className="w-full h-40 rounded-md object-cover mb-3 bg-gray-100"
    />
  );
}

export function PropertyCard({ property, onAdded }) {
  const [showModal, setShowModal] = useState(false);
  const platform = getPlatformMeta(property.source);
  const faviconUrl = getFaviconUrl(property.source);

  const pricePerM2 =
    property.price && property.sizeM2 ? Math.round(property.price / property.sizeM2) : null;

  return (
    <div className="bg-white rounded-lg shadow p-4 border border-gray-200 hover:shadow-lg transition-shadow relative">
      {isNew(property.createdAt) && (
        <span className="absolute top-2 left-2 z-10 text-xs font-semibold bg-emerald-500 text-white px-2 py-0.5 rounded shadow">
          NEW
        </span>
      )}

      <PropertyPhoto photos={property.photos} alt={property.location} />

      <div className="flex justify-between items-start mb-3">
        <div>
          <h3 className="font-bold text-lg">€{property.price?.toLocaleString() ?? 'N/A'}</h3>
          {property.previousPrice != null && property.previousPrice !== property.price && (
            <p
              className={`text-xs font-medium ${
                property.price < property.previousPrice ? 'text-green-600' : 'text-red-600'
              }`}
            >
              {property.price < property.previousPrice ? '▼' : '▲'} was €
              {property.previousPrice.toLocaleString()}
            </p>
          )}
          <p className="text-sm text-gray-600">{property.location}</p>
        </div>
        <span
          className={`flex items-center gap-1 text-xs px-2 py-1 rounded whitespace-nowrap ${platform.color}`}
        >
          {faviconUrl && (
            <img src={faviconUrl} alt="" width={14} height={14} className="inline-block" />
          )}
          {platform.label}
        </span>
      </div>

      <div className="grid grid-cols-3 gap-2 mb-3 text-sm">
        <div className="text-center">
          <span className="font-semibold">{property.beds ?? '—'}</span>
          <p className="text-gray-600">Beds</p>
        </div>
        <div className="text-center">
          <span className="font-semibold">{property.baths ?? '—'}</span>
          <p className="text-gray-600">Baths</p>
        </div>
        <div className="text-center">
          <span className="font-semibold">{property.sizeM2 ?? '—'}m²</span>
          <p className="text-gray-600">Size</p>
        </div>
      </div>

      {pricePerM2 && (
        <div className="mb-3 text-sm">
          <p className="text-gray-600">
            Price per m²: <span className="font-semibold">€{pricePerM2.toLocaleString()}</span>
          </p>
        </div>
      )}

      <div className="mb-3">
        <span
          className={`text-xs px-2 py-1 rounded ${
            orientationStyles[property.sunOrientation] ?? orientationStyles['Not Available']
          }`}
        >
          ☀️ {property.sunOrientation}
          {property.orientationSource !== 'extracted' && ' (to confirm)'}
        </span>
      </div>

      <div className="flex gap-2">
        <a
          href={property.url}
          target="_blank"
          rel="noopener noreferrer"
          className="flex-1 bg-gray-200 text-black px-3 py-2 rounded text-sm hover:bg-gray-300 text-center"
        >
          View on Site
        </a>
        <button
          onClick={() => setShowModal(true)}
          className="flex-1 bg-blue-600 text-white px-3 py-2 rounded text-sm hover:bg-blue-700"
        >
          Add to List
        </button>
      </div>

      {showModal && (
        <MyListingModal
          property={property}
          onClose={() => setShowModal(false)}
          onAdded={onAdded}
        />
      )}
    </div>
  );
}
