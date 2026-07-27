import React, { useCallback, useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { propertiesApi } from '../api/propertiesApi';
import { myListingsApi } from '../api/myListingsApi';
import { ContactPanel } from '../components/ContactPanel';
import { SwipeCard } from '../components/SwipeCard';

const PAGE_SIZE = 30;
const SWIPE_THRESHOLD = 100; // px of horizontal drag to commit a swipe
const FLING_OUT_DISTANCE = 600;
const ANIMATION_MS = 280;

export function SwipePage() {
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
      // whole untriaged backlog, not just today's arrivals.
      const result = await propertiesApi.getProperties({
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
  }, []);

  useEffect(() => {
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
      <h1 className="text-2xl font-bold text-white mb-4 self-start">Discover</h1>

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
        <div className="flex items-center gap-6 mt-6">
          <button
            onClick={() => commitSwipe('left')}
            aria-label="Pass"
            className="h-16 w-16 rounded-full border border-white/10 bg-white/[0.06] backdrop-blur-xl shadow-xl text-3xl transition-transform hover:scale-105 active:scale-95"
          >
            ❌
          </button>
          <button
            onClick={() => commitSwipe('right')}
            aria-label="Interested"
            className="h-16 w-16 rounded-full border border-white/10 bg-white/[0.06] backdrop-blur-xl shadow-xl text-3xl transition-transform hover:scale-105 active:scale-95"
          >
            ❤️
          </button>
        </div>
      )}

      {sendFlowListing && (
        <ContactPanel listing={sendFlowListing} onClose={() => setSendFlowListing(null)} />
      )}
    </div>
  );
}
