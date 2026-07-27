// Per-source display metadata. Icons are fetched from a public favicon
// service by domain (URL-based, no image storage needed) rather than
// hotlinking each platform's own logo asset.
// Translucent color-on-dark chips (bg-*-500/15 text-*-300) rather than the
// pastel bg-*-100/text-*-800 light-mode style — reads cleanly against the
// slate-900/950 dark surfaces used throughout the app.
const PLATFORMS = {
  Idealista: { domain: 'idealista.pt', label: 'Idealista', color: 'bg-purple-500/15 text-purple-300' },
  ImoVirtual: { domain: 'imovirtual.com', label: 'ImoVirtual', color: 'bg-blue-500/15 text-blue-300' },
  CustoJusto: { domain: 'custojusto.pt', label: 'CustoJusto', color: 'bg-slate-500/15 text-slate-300' },
  CasaSapo: { domain: 'casa.sapo.pt', label: 'Casa SAPO', color: 'bg-orange-500/15 text-orange-300' },
  CaixaImobiliario: { domain: 'caixaimobiliario.pt', label: 'Caixa Imobiliário', color: 'bg-teal-500/15 text-teal-300' },
  Santander: { domain: 'santander.pt', label: 'Santander', color: 'bg-red-500/15 text-red-300' },
};

const DEFAULT_PLATFORM = { domain: null, label: 'Unknown', color: 'bg-slate-500/15 text-slate-300' };

export function getPlatformMeta(source) {
  return PLATFORMS[source] ?? { ...DEFAULT_PLATFORM, label: source ?? 'Unknown' };
}

export function getFaviconUrl(source, size = 32) {
  const { domain } = getPlatformMeta(source);
  if (!domain) return null;
  return `https://www.google.com/s2/favicons?domain=${encodeURIComponent(domain)}&sz=${size}`;
}
