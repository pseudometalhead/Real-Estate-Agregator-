import React, { useState } from 'react';
import { myListingsApi } from '../api/myListingsApi';
import { ContactPanel } from './ContactPanel';
import { getPlatformMeta, getFaviconUrl } from '../utils/platformMeta';

const orientationStyles = {
  Sul: 'bg-emerald-500/10 text-emerald-400',
  Norte: 'bg-blue-500/10 text-blue-400',
  Oriente: 'bg-amber-500/10 text-amber-400',
  Poente: 'bg-orange-500/10 text-orange-400',
  'Not Available': 'bg-white/10 text-slate-400',
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
      <div className="w-full aspect-[16/10] rounded-xl bg-white/5 flex items-center justify-center text-5xl mb-4">
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
      className="w-full aspect-[16/10] rounded-xl object-cover mb-4 bg-white/5"
    />
  );
}

export function PropertyCard({ property, onAdded }) {
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState(null);
  // The listing just created by a ❤️ tap — same one-tap send moment as the
  // swipe-to-triage homepage (see SwipePage.jsx), reached here without the
  // extra notes/agent-info modal that used to sit in front of it.
  const [sendFlowListing, setSendFlowListing] = useState(null);
  const platform = getPlatformMeta(property.source);
  const faviconUrl = getFaviconUrl(property.source);

  const pricePerM2 =
    property.price && property.sizeM2 ? Math.round(property.price / property.sizeM2) : null;

  const priceDropped =
    property.previousPrice != null &&
    property.previousPrice !== property.price &&
    property.price < property.previousPrice;
  const priceRose =
    property.previousPrice != null &&
    property.previousPrice !== property.price &&
    property.price > property.previousPrice;
  const priceDelta =
    property.previousPrice != null ? Math.abs(property.price - property.previousPrice) : null;

  const handleInterested = async () => {
    if (submitting) return;
    setSubmitting(true);
    setError(null);
    try {
      const listing = await myListingsApi.addToMyListings({ propertyId: property.id });
      onAdded?.();
      setSendFlowListing(listing);
    } catch (err) {
      setError(err.response?.data ?? err.message ?? 'Failed to add property');
    } finally {
      setSubmitting(false);
    }
  };

  const handlePass = async () => {
    if (submitting) return;
    setSubmitting(true);
    setError(null);
    try {
      await myListingsApi.addToMyListings({ propertyId: property.id, status: 'Rejected' });
      onAdded?.();
    } catch (err) {
      setError(err.response?.data ?? err.message ?? 'Failed to save your choice');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="rounded-2xl border border-white/10 bg-white/[0.06] backdrop-blur-xl p-5 shadow-xl hover:bg-white/[0.09] hover:border-white/20 hover:shadow-2xl hover:shadow-blue-500/10 transition-all flex flex-col">
      <div className="relative">
        {isNew(property.createdAt) && (
          <span className="absolute top-2 left-2 z-10 inline-flex items-center rounded-full bg-emerald-500 px-2.5 py-0.5 text-xs font-medium text-white shadow">
            NEW
          </span>
        )}
        <span
          className={`absolute top-2 right-2 z-10 flex items-center gap-1 text-xs px-2.5 py-1 rounded-full whitespace-nowrap shadow backdrop-blur-sm ${platform.color}`}
        >
          {faviconUrl && (
            <img src={faviconUrl} alt="" width={14} height={14} className="inline-block" />
          )}
          {platform.label}
        </span>

        <PropertyPhoto photos={property.photos} alt={property.location} />
      </div>

      <div className="mb-3">
        <div className="flex items-baseline gap-2 flex-wrap">
          <h3 className="text-3xl font-bold text-white">
            €{property.price?.toLocaleString() ?? 'N/A'}
          </h3>
          {priceDelta != null && (priceDropped || priceRose) && (
            <span
              className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${
                priceDropped ? 'bg-emerald-500/10 text-emerald-400' : 'bg-red-500/10 text-red-400'
              }`}
            >
              {priceDropped ? '↓' : '↑'} €{priceDelta.toLocaleString()}
            </span>
          )}
        </div>
        <p className="text-sm text-slate-400 mt-0.5">{property.location}</p>
        {property.linkedSources?.length > 0 && (
          <div className="flex flex-wrap gap-1 mt-1.5">
            <span className="text-xs text-slate-500">Also on:</span>
            {property.linkedSources.map((s) => {
              const meta = getPlatformMeta(s.source);
              return (
                <a
                  key={s.url}
                  href={s.url}
                  target="_blank"
                  rel="noopener noreferrer"
                  title={s.price ? `€${s.price.toLocaleString()} on ${meta.label}` : meta.label}
                  className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium hover:opacity-80 transition-opacity ${meta.color}`}
                >
                  {meta.label}
                </a>
              );
            })}
          </div>
        )}
      </div>

      <div className="grid grid-cols-3 gap-2 mb-4 text-sm border-y border-white/10 py-3">
        <div className="text-center">
          <span className="block font-semibold text-white">{property.beds ?? '—'}</span>
          <p className="text-slate-500">Beds</p>
        </div>
        <div className="text-center">
          <span className="block font-semibold text-white">{property.baths ?? '—'}</span>
          <p className="text-slate-500">Baths</p>
        </div>
        <div className="text-center">
          <span className="block font-semibold text-white">{property.sizeM2 ?? '—'}m²</span>
          <p className="text-slate-500">Size</p>
        </div>
      </div>

      {pricePerM2 && (
        <p className="mb-3 text-sm text-slate-400">
          Price per m²:{' '}
          <span className="font-semibold text-white">€{pricePerM2.toLocaleString()}</span>
        </p>
      )}

      <div className="mb-4 flex-1 flex flex-wrap gap-1.5">
        <span
          className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${
            orientationStyles[property.sunOrientation] ?? orientationStyles['Not Available']
          }`}
        >
          ☀️ {property.sunOrientation}
          {property.orientationSource !== 'extracted' && ' (to confirm)'}
        </span>
        {property.constructionStatus && property.constructionStatus !== 'Not Available' && (
          <span className="inline-flex items-center rounded-full bg-violet-500/10 px-2.5 py-0.5 text-xs font-medium text-violet-400">
            🏗️ {property.constructionStatus}
          </span>
        )}
        {property.elevator === true && (
          <span className="inline-flex items-center rounded-full bg-white/10 px-2.5 py-0.5 text-xs font-medium text-slate-300">
            🛗 Elevador
          </span>
        )}
        {property.parking === true && (
          <span className="inline-flex items-center rounded-full bg-white/10 px-2.5 py-0.5 text-xs font-medium text-slate-300">
            🚗 Garagem
          </span>
        )}
      </div>

      {error && <p className="mb-2 text-xs text-red-400">{String(error)}</p>}

      <div className="flex gap-2">
        <a
          href={property.url}
          target="_blank"
          rel="noopener noreferrer"
          className="flex-1 rounded-lg bg-white/10 px-4 py-2.5 text-center text-sm font-medium text-slate-200 transition-colors hover:bg-white/20"
        >
          View on Site
        </a>
        <button
          onClick={handlePass}
          disabled={submitting}
          aria-label="Pass"
          className="rounded-lg bg-white/10 px-4 py-2.5 text-lg transition-colors hover:bg-white/20 disabled:opacity-50"
        >
          ❌
        </button>
        <button
          onClick={handleInterested}
          disabled={submitting}
          aria-label="Interested"
          className="rounded-lg bg-white/10 px-4 py-2.5 text-lg transition-colors hover:bg-white/20 disabled:opacity-50"
        >
          ❤️
        </button>
      </div>

      {sendFlowListing && (
        <ContactPanel listing={sendFlowListing} onClose={() => setSendFlowListing(null)} />
      )}
    </div>
  );
}
