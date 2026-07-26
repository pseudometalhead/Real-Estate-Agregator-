import React from 'react';
import { MapContainer, TileLayer, Marker, Popup } from 'react-leaflet';
import L from 'leaflet';
import markerIcon2x from 'leaflet/dist/images/marker-icon-2x.png';
import markerIcon from 'leaflet/dist/images/marker-icon.png';
import markerShadow from 'leaflet/dist/images/marker-shadow.png';

// Leaflet's default marker icon paths break under bundlers like Vite because
// they reference relative asset URLs that don't survive bundling. Resetting
// the prototype and pointing it at the bundler-resolved imports fixes the
// broken-icon issue.
delete L.Icon.Default.prototype._getIconUrl;
L.Icon.Default.mergeOptions({
  iconRetinaUrl: markerIcon2x,
  iconUrl: markerIcon,
  shadowUrl: markerShadow,
});

const PORTUGAL_CENTER = [39.5, -8.0];

function averageCenter(properties) {
  if (properties.length === 0) return PORTUGAL_CENTER;
  const sum = properties.reduce(
    (acc, p) => [acc[0] + p.lat, acc[1] + p.lng],
    [0, 0]
  );
  return [sum[0] / properties.length, sum[1] / properties.length];
}

// Pins are geocoded via the backend's Nominatim integration (see
// GeocodingService), so properties only appear here once a scraper run has
// resolved their location string to coordinates.
export function Map({ properties = [] }) {
  const geocoded = properties.filter((p) => p.lat != null && p.lng != null);
  const center = averageCenter(geocoded);
  const zoom = geocoded.length > 0 ? 11 : 7;

  return (
    <div className="h-80 rounded-lg overflow-hidden border border-gray-200">
      <MapContainer key={`${center[0]},${center[1]}`} center={center} zoom={zoom} scrollWheelZoom={false} className="h-full w-full">
        <TileLayer
          attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
        />
        {geocoded.map((p) => (
          <Marker key={p.id} position={[p.lat, p.lng]}>
            <Popup>
              <div className="text-sm">
                <p className="font-semibold">€{p.price?.toLocaleString()}</p>
                <p>{p.location}</p>
                <a href={p.url} target="_blank" rel="noopener noreferrer" className="text-blue-600 underline">
                  View listing
                </a>
              </div>
            </Popup>
          </Marker>
        ))}
      </MapContainer>
    </div>
  );
}
