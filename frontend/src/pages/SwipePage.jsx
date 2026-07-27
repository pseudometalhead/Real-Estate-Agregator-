import React, { useCallback, useContext, useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { propertiesApi } from '../api/propertiesApi';
import { myListingsApi } from '../api/myListingsApi';
import { ContactPanel } from '../components/ContactPanel';
import { SwipeCard } from '../components/SwipeCard';
import { FilterSidebar } from '../components/FilterSidebar';
import { FilterContext } from '../context/FilterContext';

const PAGE_SIZE = 30;
const SWIPE_THRESHOLD = 100; // px of horizontal drag to commit a swipe
const FLING_OUT_DISTANCE = 600;
const ANIMATION_MS = 280;

export function SwipePage() {
  const { filters } = useContext(FilterContext);
  const [showFilters, setShowFilters] = useState(false);
  const [queue, setQueue] = useState([]);
  const [currentIndex, setCurrentIndex] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [exhausted, setExhausted] = useState(false);

  const [dragging, setDragging] = useState(false);
  const [dragX, setDragX] = useState(0);
  const [animatingOut, setAnimatingOut] = useState(null); // 'left' | 'right' | null
  const dragStartXRef = useRef(0);
  const fetchingMoreRef = useRef(false);

  // The listing just created by a right-swipe — opens the same drafted-
  // message review used everywhere else in the app (PropertyCard, the
  // status-change flow in MyListingsPage). Swiping right never sends
  // anything by itself; this panel still requires an explicit tap to send.
  const [sendFlowListing, setSendFlowListing] = useState(null);

  const fetchBatch = useCallback(async (isInitial) => {
    if (isInitial) setLoading(true);
    try {
      // Deliberately not scoped to addedWithinDays like the Report page's
      // "last 24h" dashboard — this is the homepage now, meant to clear the
      // whole untriaged backlog, not just today's arrivals (unless the user
      // set that filter explicitly via the Filters panel). pendingActionOnly/
      // sortBy/sortDir/pageSize come after the spread so they always win over
      // whatever FilterContext happens to hold (its own `page` is dropped
      // entirely — this page manages its own queue/batching, not pagination).
      const result = await propertiesApi.getProperties({
        ...filters,
        page: undefined,
        pendingActionOnly: true,
        sortBy: 'date',
        sortDir: 'desc',
        pageSize: PAGE_SIZE,
      });
      setQueue((prev) => {
        const existingIds = new Set(prev.map((p) => p.id));
        const fresh = (result.items ?? []).filter((p) => !existingIds.has(p.id));
        return isInitial ? result.items ?? [] : [...prev, ...fresh];
      });
      if ((result.items ?? []).length === 0 && isInitial) setExhausted(true);
    } catch (err) {
      setError(err.message ?? 'Failed to load properties');
    } finally {
      if (isInitial) setLoading(false);
      fetchingMoreRef.current = false;
    }
  }, [filters]);

  // Re-runs whenever `filters` changes too (fetchBatch's identity depends on
  // it), resetting the queue and starting a fresh batch under the new
  // filters rather than mixing old and newly-filtered cards.
  useEffect(() => {
    setCurrentIndex(0);
    setExhausted(false);
    fetchBatch(true);
  }, [fetchBatch]);

  // Top up the queue before the user actually runs out, so swiping never
  // visibly stalls waiting on a network request.
  useEffect(() => {
    const remaining = queue.length - currentIndex;
    if (!loading && remaining <= 3 && remaining >= 0 && !fetchingMoreRef.current) {
      fetchingMoreRef.current = true;
      fetchBatch(false);
    }
    if (!loading && remaining <= 0 && queue.length > 0) {
      setExhausted(true);
    }
  }, [currentIndex, queue.length, loading, fetchBatch]);

  const current = queue[currentIndex];

  const commitSwipe = async (direction) => {
    if (!current || animatingOut) return;
    setAnimatingOut(direction);
    setDragging(false);

    try {
      const listing = await myListingsApi.addToMyListings({
        propertyId: current.id,
        ...(direction === 'left' ? { status: 'Rejected' } : {}),
      });
      if (direction === 'right') setSendFlowListing(listing);
    } catch (err) {
      // A 409 here would mean the property got tracked another way between
      // fetch and swipe (e.g. added from Search in another tab) — rare, and
      // not worth blocking the queue over; just move on.
      setError(err.response?.data ?? err.message ?? 'Failed to save your choice');
    }

    setTimeout(() => {
      setCurrentIndex((i) => i + 1);
      setAnimatingOut(null);
      setDragX(0);
    }, ANIMATION_MS);
  };

  const handlePointerDown = (e) => {
    if (animatingOut) return;
    // Photo-nav tap zones, "View on site", and "Also on:" links are all
    // interactive children inside the card. Capturing the pointer here
    // unconditionally (the old behavior) redirects every subsequent
    // pointer/click event to this outer div regardless of where the finger
    // actually is — which silently ate every tap on those children instead
    // of letting them handle their own click. Bail out before capturing so
    // taps on an interactive element reach it normally.
    if (e.target.closest('button, a')) return;
    setDragging(true);
    dragStartXRef.current = e.clientX;
    e.currentTarget.setPointerCapture(e.pointerId);
  };

  const handlePointerMove = (e) => {
    if (!dragging) return;
    setDragX(e.clientX - dragStartXRef.current);
  };

  const handlePointerUp = () => {
    if (!dragging) return;
    setDragging(false);
    if (Math.abs(dragX) > SWIPE_THRESHOLD) {
      commitSwipe(dragX > 0 ? 'right' : 'left');
    } else {
      setDragX(0);
    }
  };

  const topCardStyle = animatingOut
    ? {
        transform: `translateX(${animatingOut === 'right' ? FLING_OUT_DISTANCE : -FLING_OUT_DISTANCE}px) rotate(${
          animatingOut === 'right' ? 20 : -20
        }deg)`,
        opacity: 0,
        transition: `transform ${ANIMATION_MS}ms ease-out, opacity ${ANIMATION_MS}ms ease-out`,
      }
    : {
        transform: `translateX(${dragX}px) rotate(${dragX / 20}deg)`,
        transition: dragging ? 'none' : 'transform 200ms ease-out',
      };

  return (
    <div className="max-w-md mx-auto px-4 py-6 flex flex-col items-center">
      <div className="w-full flex items-center justify-between mb-4">
        <h1 className="text-2xl font-bold text-white">Discover</h1>
        <button
          onClick={() => setShowFilters(true)}
          className="rounded-lg border border-white/10 bg-white/[0.06] px-3 py-2 text-sm font-medium text-slate-300 backdrop-blur-xl transition-colors hover:bg-white/10"
        >
          ⚙️ Filters
        </button>
      </div>

      {error && (
        <div className="mb-4 w-full rounded-xl border border-red-500/20 bg-red-500/10 p-3 text-sm text-red-400">
          {error}
        </div>
      )}

      <div className="relative w-full" style={{ height: 'min(70vh, 560px)' }}>
        {loading ? (
          <div className="absolute inset-0 rounded-3xl border border-white/10 bg-white/[0.06] backdrop-blur-xl animate-pulse" />
        ) : exhausted ? (
          <div className="absolute inset-0 rounded-3xl border border-white/10 bg-white/[0.06] backdrop-blur-xl flex flex-col items-center justify-center text-center p-8">
            <p className="text-5xl mb-4">🎉</p>
            <p className="text-xl font-bold text-white mb-2">You're all caught up!</p>
            <p className="text-sm text-slate-400 mb-6">
              No new listings pending action right now — check back after the next scrape.
            </p>
            <Link
              to="/search"
              className="rounded-lg bg-blue-500 px-4 py-2 text-sm font-medium text-white shadow-lg shadow-blue-500/30 transition-colors hover:bg-blue-400"
            >
              Browse everything →
            </Link>
          </div>
        ) : (
          <>
            {queue[currentIndex + 1] && (
              <SwipeCard
                key={queue[currentIndex + 1].id}
                property={queue[currentIndex + 1]}
                className="scale-95 opacity-60"
                style={{}}
              />
            )}
            {current && (
              <SwipeCard
                key={current.id}
                property={current}
                dragX={dragX}
                style={topCardStyle}
                onPointerDown={handlePointerDown}
                onPointerMove={handlePointerMove}
                onPointerUp={handlePointerUp}
              />
            )}
          </>
        )}
      </div>

      {!loading && !exhausted && current && (
        <div className="flex items-center gap-8 mt-6">
          <button
            onClick={() => commitSwipe('left')}
            aria-label="Pass"
            className="group flex h-16 w-16 items-center justify-center rounded-full border border-red-500/30 bg-gradient-to-b from-red-500/15 to-red-600/5 shadow-lg shadow-red-500/10 backdrop-blur-xl transition-all hover:scale-110 hover:border-red-500/50 hover:from-red-500/25 hover:shadow-red-500/20 active:scale-95"
          >
            <svg
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2.5"
              strokeLinecap="round"
              className="h-7 w-7 text-red-400 transition-colors group-hover:text-red-300"
            >
              <path d="M18 6 6 18M6 6l12 12" />
            </svg>
          </button>
          <button
            onClick={() => commitSwipe('right')}
            aria-label="Interested"
            className="group flex h-16 w-16 items-center justify-center rounded-full border border-emerald-500/30 bg-gradient-to-b from-emerald-500/15 to-emerald-600/5 shadow-lg shadow-emerald-500/10 backdrop-blur-xl transition-all hover:scale-110 hover:border-emerald-500/50 hover:from-emerald-500/25 hover:shadow-emerald-500/20 active:scale-95"
          >
            <svg
              viewBox="0 0 24 24"
              fill="currentColor"
              className="h-7 w-7 text-emerald-400 transition-colors group-hover:text-emerald-300"
            >
              <path d="M12 21s-6.716-4.35-9.428-8.09C.86 10.31 1.02 7.14 3.34 5.24c2.02-1.65 4.85-1.32 6.66.63L12 7.94l2-2.07c1.81-1.95 4.64-2.28 6.66-.63 2.32 1.9 2.48 5.07.77 7.67C18.716 16.65 12 21 12 21z" />
            </svg>
          </button>
        </div>
      )}

      {sendFlowListing && (
        <ContactPanel listing={sendFlowListing} onClose={() => setSendFlowListing(null)} />
      )}

      {showFilters && (
        <div
          className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-start justify-center z-50 p-4 overflow-y-auto"
          onClick={() => setShowFilters(false)}
        >
          <div className="mt-6 mb-6" onClick={(e) => e.stopPropagation()}>
            <div className="flex justify-end mb-2">
              <button
                onClick={() => setShowFilters(false)}
                aria-label="Close filters"
                className="text-slate-300 hover:text-white text-2xl leading-none"
              >
                ×
              </button>
            </div>
            <FilterSidebar />
          </div>
        </div>
      )}
    </div>
  );
}
