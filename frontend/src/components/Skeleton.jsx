import React from 'react';

export function Skeleton({ className = '' }) {
  return <div className={`animate-pulse rounded-md bg-white/10 ${className}`} />;
}

export function PropertyCardSkeleton() {
  return (
    <div className="rounded-2xl border border-white/10 bg-white/[0.06] backdrop-blur-xl p-5 shadow-xl">
      <Skeleton className="w-full aspect-[16/10] rounded-xl mb-4" />
      <Skeleton className="h-8 w-32 mb-2" />
      <Skeleton className="h-4 w-40 mb-4" />
      <div className="grid grid-cols-3 gap-2 mb-4 border-y border-white/10 py-3">
        <Skeleton className="h-8 w-full" />
        <Skeleton className="h-8 w-full" />
        <Skeleton className="h-8 w-full" />
      </div>
      <Skeleton className="h-6 w-24 mb-4" />
      <div className="flex gap-2">
        <Skeleton className="h-9 flex-1" />
        <Skeleton className="h-9 flex-1" />
      </div>
    </div>
  );
}

export function ListRowSkeleton() {
  return (
    <div className="rounded-2xl border border-white/10 bg-white/[0.06] backdrop-blur-xl p-6 shadow-xl">
      <div className="flex gap-4">
        <Skeleton className="w-20 h-20 rounded-lg shrink-0" />
        <div className="flex-1 space-y-2">
          <Skeleton className="h-5 w-40" />
          <Skeleton className="h-4 w-56" />
          <Skeleton className="h-4 w-32" />
        </div>
      </div>
    </div>
  );
}
