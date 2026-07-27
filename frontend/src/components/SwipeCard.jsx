import React, { useState } from 'react';
import { getPlatformMeta, getFaviconUrl } from '../utils/platformMeta';

const orientationStyles = {
  Sul: 'bg-emerald-500/10 text-emerald-400',
  Norte: 'bg-blue-500/10 text-blue-400',
  Oriente: 'bg-amber-500/10 text-amber-400',
  Poente: 'bg-orange-500/10 text-orange-400',
  'Not Available': 'bg-white/10 text-slate-400',
};

// true -> present, false -> confirmed absent, null/undefined -> not mentioned
// (not shown at all — no badge is more honest than implying "no" from silence).
function triStateBadge(value, presentLabel, absentLabel) {
  if (value === true) return { label: presentLabel, className: 'bg-white/10 text-slate-300' };
  if (value === false) return { label: absentLabel, className: 'bg-red-500/10 text-red-400' };
  return null;
}

// Bigger, photo-forward variant of PropertyCard's visual language, sized for
// a phone-portrait hero card rather than a compact grid tile. Purely
// presentational — SwipePage owns all drag/gesture state and passes down
// `style` (the live transform while dragging) and `dragX` (just the
// horizontal offset, used here only to fade in the LIKE/NOPE stamps).
export function SwipeCard({ property, style, dragX = 0, onPointerDown, onPointerMove, onPointerUp, className = '' }) {
  const [photoIndex, setPhotoIndex] = useState(0);
  const [photoFailed, setPhotoFailed] = useState(false);
  const platform = getPlatformMeta(property.source);
  const faviconUrl = getFaviconUrl(property.source);
  const photos = property.photos?.length > 0 ? property.photos : [];
  const photo = photos[photoIndex];

  const pricePerM2 =
    property.price && property.sizeM2 ? Math.round(property.price / property.sizeM2) : null;

  const elevatorBadge = triStateBadge(property.elevator, '🛗 Elevador', '🛗 Sem elevador');
  const parkingBadge = triStateBadge(property.parking, '🚗 Garagem', '🚗 Sem garagem');

  const likeOpacity = Math.min(Math.max(dragX / 100, 0), 1);
  const nopeOpacity = Math.min(Math.max(-dragX / 100, 0), 1);

  // Tap zones on the photo, not a drag gesture — a second horizontal drag
  // here would fight the card's own accept/reject swipe. stopPropagation
  // keeps these taps from also being read as the start of a card swipe.
  const goToPhoto = (e, index) => {
    e.stopPropagation();
    setPhotoIndex(index);
    setPhotoFailed(false);
  };

  return (
    <div
      className={`absolute inset-0 rounded-3xl border border-white/10 bg-white/[0.06] backdrop-blur-xl shadow-2xl overflow-hidden select-none touch-pan-y ${className}`}
      style={style}
      onPointerDown={onPointerDown}
      onPointerMove={onPointerMove}
      onPointerUp={onPointerUp}
      onPointerCancel={onPointerUp}
    >
      <div
        className="absolute top-6 left-6 z-20 rounded-lg border-4 border-emerald-400 px-3 py-1 text-xl font-black uppercase tracking-wider text-emerald-400 -rotate-12"
        style={{ opacity: likeOpacity }}
      >
        Interested
      </div>
      <div
        className="absolute top-6 right-6 z-20 rounded-lg border-4 border-red-400 px-3 py-1 text-xl font-black uppercase tracking-wider text-red-400 rotate-12"
        style={{ opacity: nopeOpacity }}
      >
        Pass
      </div>

      <div className="relative h-[42%] w-full bg-white/5">
        {photo && !photoFailed ? (
          <img
            src={photo}
            alt={property.location}
            draggable={false}
            onError={() => setPhotoFailed(true)}
            className="h-full w-full object-cover"
          />
        ) : (
          <div className="h-full w-full flex items-center justify-center text-7xl">🏠</div>
        )}
        <div className="absolute inset-x-0 bottom-0 h-28 bg-gradient-to-t from-black/70 to-transparent" />

        {photos.length > 1 && (
          <>
            {/* Invisible left/right tap zones covering most of the photo,
                so you don't need to hit a tiny button precisely. */}
            <button
              type="button"
              aria-label="Previous photo"
              onClick={(e) => goToPhoto(e, photoIndex === 0 ? photos.length - 1 : photoIndex - 1)}
              className="absolute inset-y-0 left-0 w-1/3 z-10"
            />
            <button
              type="button"
              aria-label="Next photo"
              onClick={(e) => goToPhoto(e, photoIndex === photos.length - 1 ? 0 : photoIndex + 1)}
              className="absolute inset-y-0 right-0 w-1/3 z-10"
            />
            <div className="absolute top-3 inset-x-3 flex gap-1 z-10">
              {photos.map((_, i) => (
                <div
                  key={i}
                  className={`h-1 flex-1 rounded-full ${i === photoIndex ? 'bg-white' : 'bg-white/30'}`}
                />
              ))}
            </div>
            <span className="absolute bottom-3 right-3 z-10 rounded-full bg-black/50 px-2 py-0.5 text-xs text-white">
              {photoIndex + 1}/{photos.length}
            </span>
          </>
        )}

        <span
          className={`absolute top-4 ${photos.length > 1 ? 'left-1/2 -translate-x-1/2' : 'right-4'} flex items-center gap-1 text-xs px-2.5 py-1 rounded-full whitespace-nowrap shadow backdrop-blur-sm ${platform.color}`}
        >
          {faviconUrl && <img src={faviconUrl} alt="" width={14} height={14} className="inline-block" />}
          {platform.label}
        </span>
        <div className="absolute bottom-3 left-5 right-5">
          <h3 className="text-3xl font-bold text-white drop-shadow">
            €{property.price?.toLocaleString() ?? 'N/A'}
          </h3>
          <p className="text-sm text-slate-200 drop-shadow">{property.location}</p>
        </div>
      </div>

      <div className="h-[58%] p-5 flex flex-col overflow-y-auto">
        <div className="grid grid-cols-3 gap-2 mb-3 text-sm border-b border-white/10 pb-3">
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
          <p className="text-sm text-slate-400 mb-3">
            Price per m²: <span className="font-semibold text-white">€{pricePerM2.toLocaleString()}</span>
          </p>
        )}

        <div className="flex flex-wrap gap-1.5 mb-3">
          <span
            className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${
              orientationStyles[property.sunOrientation] ?? orientationStyles['Not Available']
            }`}
          >
            ☀️ {property.sunOrientation}
          </span>
          {property.constructionStatus && property.constructionStatus !== 'Not Available' && (
            <span className="inline-flex items-center rounded-full bg-violet-500/10 px-2.5 py-0.5 text-xs font-medium text-violet-400">
              🏗️ {property.constructionStatus}
            </span>
          )}
          {property.openPlanKitchen === true && (
            <span className="inline-flex items-center rounded-full bg-white/10 px-2.5 py-0.5 text-xs font-medium text-slate-300">
              🍳 Open-plan kitchen
            </span>
          )}
          {elevatorBadge && (
            <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${elevatorBadge.className}`}>
              {elevatorBadge.label}
            </span>
          )}
          {parkingBadge && (
            <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${parkingBadge.className}`}>
              {parkingBadge.label}
            </span>
          )}
        </div>

        {property.linkedSources?.length > 0 && (
          <div className="flex flex-wrap items-center gap-1 mb-3">
            <span className="text-xs text-slate-500">Also on:</span>
            {property.linkedSources.map((s) => {
              const meta = getPlatformMeta(s.source);
              return (
                <a
                  key={s.url}
                  href={s.url}
                  target="_blank"
                  rel="noopener noreferrer"
                  onClick={(e) => e.stopPropagation()}
                  className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium hover:opacity-80 transition-opacity ${meta.color}`}
                >
                  {meta.label}
                </a>
              );
            })}
          </div>
        )}

        {property.description && (
          <div className="mb-3">
            <p className="text-xs font-medium text-slate-500 mb-1">Description</p>
            <p className="text-sm text-slate-300 whitespace-pre-wrap">{property.description}</p>
          </div>
        )}

        {property.agentName && (
          <p className="text-xs text-slate-500 mb-3">Listed by {property.agentName}</p>
        )}

        <a
          href={property.url}
          target="_blank"
          rel="noopener noreferrer"
          onClick={(e) => e.stopPropagation()}
          className="mt-auto text-center text-xs text-slate-400 underline hover:text-slate-300"
        >
          View on site
        </a>
      </div>
    </div>
  );
}
