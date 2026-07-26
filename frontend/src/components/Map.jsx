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

// Basic placeholder map — no geocoding/exact coordinates in this MVP (see
// spec's non-goals), so this centers on Portugal without per-property pins
// unless callers pass properties with lat/lng in the future.
export function Map({ properties = [] }) {
  return (
    <div className="h-80 rounded-lg overflow-hidden border border-gray-200">
      <MapContainer center={PORTUGAL_CENTER} zoom={7} scrollWheelZoom={false} className="h-full w-full">
        <TileLayer
          attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
        />
        {properties
          .filter((p) => p.lat && p.lng)
          .map((p) => (
            <Marker key={p.id} position={[p.lat, p.lng]}>
              <Popup>
                {p.location} — €{p.price?.toLocaleString()}
              </Popup>
            </Marker>
          ))}
      </MapContainer>
    </div>
  );
}
