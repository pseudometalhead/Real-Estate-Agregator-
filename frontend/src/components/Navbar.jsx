import React, { useState } from 'react';
import { NavLink } from 'react-router-dom';

const links = [
  { to: '/', label: 'Home', end: true },
  { to: '/search', label: 'Search' },
  { to: '/my-listings', label: 'My Listings' },
  { to: '/reports', label: 'Reports' },
  { to: '/settings', label: 'Settings' },
];

function linkClasses({ isActive }) {
  return `rounded-lg px-3 py-2 text-sm font-medium transition-colors ${
    isActive
      ? 'bg-blue-500 text-white shadow-lg shadow-blue-500/30'
      : 'text-slate-400 hover:bg-white/10 hover:text-white'
  }`;
}

export function Navbar() {
  // 5 links no longer fit one row on a phone — collapse to a toggle below
  // sm:, full row unchanged above it.
  const [mobileOpen, setMobileOpen] = useState(false);

  return (
    <nav className="sticky top-0 z-20 bg-white/[0.06] backdrop-blur-xl border-b border-white/10">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 py-4 flex items-center justify-between">
        <span className="text-xl font-bold text-white">🏠 Estate Aggregator</span>

        <div className="hidden sm:flex gap-1">
          {links.map((link) => (
            <NavLink key={link.to} to={link.to} end={link.end} className={linkClasses}>
              {link.label}
            </NavLink>
          ))}
        </div>

        <button
          type="button"
          onClick={() => setMobileOpen((v) => !v)}
          aria-label={mobileOpen ? 'Close menu' : 'Open menu'}
          aria-expanded={mobileOpen}
          className="sm:hidden rounded-lg p-2 text-xl text-slate-300 hover:bg-white/10"
        >
          {mobileOpen ? '✕' : '☰'}
        </button>
      </div>

      {mobileOpen && (
        <div className="sm:hidden border-t border-white/10 px-4 pb-4 pt-2 flex flex-col gap-1">
          {links.map((link) => (
            <NavLink
              key={link.to}
              to={link.to}
              end={link.end}
              onClick={() => setMobileOpen(false)}
              className={linkClasses}
            >
              {link.label}
            </NavLink>
          ))}
        </div>
      )}
    </nav>
  );
}
