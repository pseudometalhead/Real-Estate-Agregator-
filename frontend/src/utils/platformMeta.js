// Per-source display metadata. Icons are fetched from a public favicon
// service by domain (URL-based, no image storage needed) rather than
// hotlinking each platform's own logo asset.
const PLATFORMS = {
  Idealista: { domain: 'idealista.pt', label: 'Idealista', color: 'bg-purple-100 text-purple-800' },
  ImoVirtual: { domain: 'imovirtual.com', label: 'ImoVirtual', color: 'bg-blue-100 text-blue-800' },
  Imobiliario: { domain: 'imobiliario.pt', label: 'Imobiliário', color: 'bg-gray-100 text-gray-800' },
  CasaSapo: { domain: 'casa.sapo.pt', label: 'Casa SAPO', color: 'bg-orange-100 text-orange-800' },
  CaixaImobiliario: { domain: 'caixaimobiliario.pt', label: 'Caixa Imobiliário', color: 'bg-teal-100 text-teal-800' },
  Santander: { domain: 'santander.pt', label: 'Santander', color: 'bg-red-100 text-red-800' },
};

const DEFAULT_PLATFORM = { domain: null, label: 'Unknown', color: 'bg-gray-100 text-gray-800' };

export function getPlatformMeta(source) {
  return PLATFORMS[source] ?? { ...DEFAULT_PLATFORM, label: source ?? 'Unknown' };
}

export function getFaviconUrl(source, size = 32) {
  const { domain } = getPlatformMeta(source);
  if (!domain) return null;
  return `https://www.google.com/s2/favicons?domain=${encodeURIComponent(domain)}&sz=${size}`;
}
