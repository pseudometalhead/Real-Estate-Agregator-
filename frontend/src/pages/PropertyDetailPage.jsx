import React, { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { propertiesApi } from '../api/propertiesApi';
import { getPlatformMeta, getFaviconUrl } from '../utils/platformMeta';
import { formatLocation } from '../utils/formatLocation';

const orientationStyles = {
  Sul: 'bg-emerald-500/10 text-emerald-400',
  Norte: 'bg-blue-500/10 text-blue-400',
  Oriente: 'bg-amber-500/10 text-amber-400',
  Poente: 'bg-orange-500/10 text-orange-400',
  'Norte/Nascente': 'bg-cyan-500/10 text-cyan-400',
  'Norte/Poente': 'bg-indigo-500/10 text-indigo-400',
  'Sul/Nascente': 'bg-lime-500/10 text-lime-400',
  'Sul/Poente': 'bg-rose-500/10 text-rose-400',
  'Not Available': 'bg-white/10 text-slate-400',
};

const constructionIcons = {
  'Em Construção': '🏗️',
  'Nova Construção': '🏗️',
  'Concluída': '✅',
  'Para Recuperar': '🔧',
};

// true -> present, false -> confirmed absent, null/undefined -> not mentioned
function triStateBadge(value, presentLabel, absentLabel) {
  if (value === true) return { label: presentLabel, className: 'bg-white/10 text-slate-300' };
  if (value === false) return { label: absentLabel, className: 'bg-red-500/10 text-red-400' };
  return null;
}

// Full-detail read-only view of a property, at its own /property/:id route
// (linked from a Browse grid card or a map pin) rather than an overlay modal
// — a real URL means back/forward and refresh behave like a normal page,
// and there's no cramped max-height/overflow scroll area to fight with.
export function PropertyDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [property, setProperty] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [photoIndex, setPhotoIndex] = useState(0);
  const [photoFailed, setPhotoFailed] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    propertiesApi
      .getProperty(id)
      .then((data) => {
        if (!cancelled) setProperty(data);
      })
      .catch((err) => {
        if (!cancelled) setError(err.response?.data ?? err.message ?? 'Failed to load property');
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [id]);

  if (loading) {
    return (
      <div className="max-w-3xl mx-auto px-4 sm:px-6 py-6">
        <div className="h-96 rounded-2xl border border-white/10 bg-white/[0.06] backdrop-blur-xl animate-pulse" />
      </div>
    );
  }

  if (error || !property) {
    return (
      <div className="max-w-3xl mx-auto px-4 sm:px-6 py-6 text-center">
        <p className="text-4xl mb-3">🏚️</p>
        <p className="font-medium text-slate-200">{error ?? 'Property not found'}</p>
        <button
          onClick={() => navigate(-1)}
          className="mt-4 rounded-lg bg-white/10 px-4 py-2 text-sm font-medium text-slate-200 hover:bg-white/20"
        >
          ← Back
        </button>
      </div>
    );
  }

  const platform = getPlatformMeta(property.source);
  const faviconUrl = getFaviconUrl(property.source);
  const photos = property.photos?.length > 0 ? property.photos : [];
  const photo = photos[photoIndex];

  const pricePerM2 =
    property.price && property.sizeM2 ? Math.round(property.price / property.sizeM2) : null;

  const elevatorBadge = triStateBadge(property.elevator, '🛗 Elevador', '🛗 Sem elevador');
  const parkingBadge = triStateBadge(property.parking, '🚗 Garagem', '🚗 Sem garagem');
  const furnishedBadge = triStateBadge(property.furnished, '🛋️ Mobilado', '🛋️ Não mobilado');
  const acBadge = triStateBadge(property.airConditioning, '❄️ Ar condicionado', '❄️ Sem ar condicionado');
  const balconyBadge = triStateBadge(property.balcony, '🌤️ Varanda/Terraço', '🌤️ Sem varanda/terraço');
  const storageBadge = triStateBadge(property.storage, '📦 Arrecadação', '📦 Sem arrecadação');
  const licenseBadge = triStateBadge(property.hasUsageLicense, '📋 Com licença de habitação', '📋 Sem licença de habitação');

  const goToPhoto = (index) => {
    setPhotoIndex(index);
    setPhotoFailed(false);
  };

  return (
    <div className="max-w-3xl mx-auto px-4 sm:px-6 py-6">
      <button
        onClick={() => navigate(-1)}
        className="mb-4 inline-flex items-center gap-1 text-sm text-slate-400 hover:text-slate-200"
      >
        ← Back
      </button>

      <div className="rounded-2xl border border-white/10 bg-slate-900/95 backdrop-blur-xl shadow-2xl overflow-hidden">
        <div className="relative">
          {photo && !photoFailed ? (
            <img
              src={photo}
              alt={formatLocation(property)}
              className="w-full aspect-[16/9] object-cover bg-white/5"
              onError={() => setPhotoFailed(true)}
            />
          ) : (
            <div className="w-full aspect-[16/9] bg-white/5 flex items-center justify-center text-6xl">
              🏠
            </div>
          )}
          <span
            className={`absolute top-3 left-3 flex items-center gap-1 text-xs px-2.5 py-1 rounded-full whitespace-nowrap shadow backdrop-blur-sm ${platform.color}`}
          >
            {faviconUrl && <img src={faviconUrl} alt="" width={14} height={14} className="inline-block" />}
            {platform.label}
          </span>
          {photos.length > 1 && (
            <>
              {/* Invisible left/right tap zones covering most of the photo,
                  so you don't need to hit a tiny button precisely. */}
              <button
                type="button"
                aria-label="Previous photo"
                onClick={() => goToPhoto(photoIndex === 0 ? photos.length - 1 : photoIndex - 1)}
                className="absolute inset-y-0 left-0 w-1/3 z-10"
              />
              <button
                type="button"
                aria-label="Next photo"
                onClick={() => goToPhoto(photoIndex === photos.length - 1 ? 0 : photoIndex + 1)}
                className="absolute inset-y-0 right-0 w-1/3 z-10"
              />
              <div className="absolute bottom-3 inset-x-3 flex gap-1 z-10 pointer-events-none">
                {photos.map((_, i) => (
                  <div
                    key={i}
                    className={`h-1.5 flex-1 rounded-full ${i === photoIndex ? 'bg-white' : 'bg-white/30'}`}
                  />
                ))}
              </div>
              <span className="absolute bottom-6 right-3 z-10 rounded-full bg-black/50 px-2 py-0.5 text-xs text-white">
                {photoIndex + 1}/{photos.length}
              </span>
            </>
          )}
        </div>

        <div className="p-6">
          <div className="flex items-baseline gap-2 flex-wrap mb-1">
            <h2 className="text-3xl font-bold text-white">€{property.price?.toLocaleString() ?? 'N/A'}</h2>
            {property.previousPrice != null && property.previousPrice !== property.price && (
              <span
                className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${
                  property.price < property.previousPrice
                    ? 'bg-emerald-500/10 text-emerald-400'
                    : 'bg-red-500/10 text-red-400'
                }`}
              >
                {property.price < property.previousPrice ? '↓' : '↑'} €
                {Math.abs(property.previousPrice - property.price).toLocaleString()}
              </span>
            )}
          </div>
          <p className="text-slate-400 mb-1">{formatLocation(property)}</p>
          {property.externalId && <p className="text-xs text-slate-500 mb-3">Ref. {property.externalId}</p>}

          {property.linkedSources?.length > 0 && (
            <div className="flex flex-wrap items-center gap-1 mb-4">
              <span className="text-xs text-slate-500">Also on:</span>
              {property.linkedSources.map((s) => {
                const meta = getPlatformMeta(s.source);
                return (
                  <a
                    key={s.url}
                    href={s.url}
                    target="_blank"
                    rel="noopener noreferrer"
                    className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium hover:opacity-80 transition-opacity ${meta.color}`}
                  >
                    {meta.label}
                  </a>
                );
              })}
            </div>
          )}

          <div className="grid grid-cols-2 gap-2 mb-4 text-sm border-y border-white/10 py-3">
            <div className="text-center">
              <span className="block font-semibold text-white">{property.beds ?? '—'}</span>
              <p className="text-slate-500">Beds</p>
            </div>
            <div className="text-center">
              <span className="block font-semibold text-white">{property.sizeM2 ?? '—'}m²</span>
              <p className="text-slate-500">Size</p>
            </div>
          </div>

          {pricePerM2 && (
            <p className="mb-3 text-sm text-slate-400">
              Price per m²: <span className="font-semibold text-white">€{pricePerM2.toLocaleString()}</span>
            </p>
          )}

          <div className="flex flex-wrap gap-1.5 mb-4">
            <span
              className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${
                orientationStyles[property.sunOrientation] ?? orientationStyles['Not Available']
              }`}
            >
              ☀️ {property.sunOrientation}
            </span>
            {property.constructionStatus && property.constructionStatus !== 'Not Available' && (
              <span className="inline-flex items-center rounded-full bg-violet-500/10 px-2.5 py-0.5 text-xs font-medium text-violet-400">
                {constructionIcons[property.constructionStatus] ?? '🏗️'} {property.constructionStatus}
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
            {furnishedBadge && (
              <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${furnishedBadge.className}`}>
                {furnishedBadge.label}
              </span>
            )}
            {acBadge && (
              <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${acBadge.className}`}>
                {acBadge.label}
              </span>
            )}
            {balconyBadge && (
              <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${balconyBadge.className}`}>
                {balconyBadge.label}
              </span>
            )}
            {storageBadge && (
              <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${storageBadge.className}`}>
                {storageBadge.label}
              </span>
            )}
            {licenseBadge && (
              <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${licenseBadge.className}`}>
                {licenseBadge.label}
              </span>
            )}
            {property.renovated === true && (
              <span className="inline-flex items-center rounded-full bg-white/10 px-2.5 py-0.5 text-xs font-medium text-slate-300">
                🔨 Renovated
              </span>
            )}
            {property.waterView === true && (
              <span className="inline-flex items-center rounded-full bg-sky-500/10 px-2.5 py-0.5 text-xs font-medium text-sky-400">
                🌊 River/Sea View
              </span>
            )}
            {property.nearMetro === true && (
              <span className="inline-flex items-center rounded-full bg-white/10 px-2.5 py-0.5 text-xs font-medium text-slate-300">
                🚇 Near Metro
              </span>
            )}
            {property.energyRating && (
              <span className="inline-flex items-center rounded-full bg-white/10 px-2.5 py-0.5 text-xs font-medium text-slate-300">
                🔋 Energy {property.energyRating}
              </span>
            )}
            {(property.floor || property.totalFloors) && (
              <span className="inline-flex items-center rounded-full bg-white/10 px-2.5 py-0.5 text-xs font-medium text-slate-300">
                🏢 {property.floor ? `Piso ${property.floor}` : 'Piso —'}
                {property.totalFloors ? ` de ${property.totalFloors}` : ''}
              </span>
            )}
            {property.condoFeeMonthly != null && (
              <span className="inline-flex items-center rounded-full bg-white/10 px-2.5 py-0.5 text-xs font-medium text-slate-300">
                💶 Condomínio €{property.condoFeeMonthly.toLocaleString()}/mês
              </span>
            )}
            {property.hasPool === true && (
              <span className="inline-flex items-center rounded-full bg-sky-500/10 px-2.5 py-0.5 text-xs font-medium text-sky-400">
                🏊 Piscina
              </span>
            )}
            {property.hasGarden === true && (
              <span className="inline-flex items-center rounded-full bg-emerald-500/10 px-2.5 py-0.5 text-xs font-medium text-emerald-400">
                🌳 Jardim
              </span>
            )}
            {property.yearBuilt && (
              <span className="inline-flex items-center rounded-full bg-white/10 px-2.5 py-0.5 text-xs font-medium text-slate-300">
                📅 Construído em {property.yearBuilt}
              </span>
            )}
          </div>

          {property.description && (
            <div className="mb-4">
              <p className="text-xs font-medium text-slate-500 mb-1">
                {property.aiEnrichedAt ? 'Additional details' : 'Description'}
              </p>
              <p className="text-sm text-slate-300 whitespace-pre-wrap">{property.description}</p>
            </div>
          )}

          {(property.agentName || property.agentPhone || property.agentEmail) && (
            <p className="text-xs text-slate-500 mb-4">
              {property.agentName && <>Listed by {property.agentName}</>}
              {property.agentPhone && <> · {property.agentPhone}</>}
              {property.agentEmail && <> · {property.agentEmail}</>}
            </p>
          )}

          <a
            href={property.url}
            target="_blank"
            rel="noopener noreferrer"
            className="block w-full rounded-lg bg-white/10 px-4 py-2.5 text-center text-sm font-medium text-slate-200 transition-colors hover:bg-white/20"
          >
            View on Site
          </a>
        </div>
      </div>
    </div>
  );
}
