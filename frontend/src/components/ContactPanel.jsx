import React, { useState, useEffect, useCallback, useMemo } from 'react';
import { commHistoryApi } from '../api/commHistoryApi';
import { myListingsApi } from '../api/myListingsApi';
import { appSettingsApi } from '../api/appSettingsApi';
import { draftInquiry, buildMailtoLink, buildWhatsAppLink, buildTelLink, isPortugueseMobileNumber } from '../utils/messageTemplates';

const channelLabels = {
  WhatsApp: '💚 WhatsApp',
  Email: '✉️ Email',
  SMS: '💬 SMS',
  Phone: '📞 Phone',
  Site: '🌐 Site form',
  InPerson: '🤝 In person',
  Other: 'Other',
};

export function ContactPanel({ listing, onClose, onLogged }) {
  const [history, setHistory] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  // null while loading — draftInquiry treats null/undefined the same as ''
  // (no availability line), so the first draft render (before Settings
  // loads) is identical to one with nothing set.
  const [availabilityText, setAvailabilityText] = useState(null);
  const [senderName, setSenderName] = useState(null);
  const [edited, setEdited] = useState(false);

  const draft = useMemo(
    () => draftInquiry(listing, { availabilityText, senderName }),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [listing, availabilityText, senderName]
  );
  const [subject, setSubject] = useState(draft.subject);
  const [body, setBody] = useState(draft.body);
  const [replyText, setReplyText] = useState('');
  const [saving, setSaving] = useState(false);
  // Tracks which "Open in ..." link the user actually clicked, so the log
  // entry records the channel they really used. Falls back to the
  // existing Email/Phone guess when neither was explicitly clicked.
  const [clickedChannel, setClickedChannel] = useState(null);

  const fetchHistory = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await commHistoryApi.getHistory(listing.id);
      setHistory(data);
    } catch (err) {
      setError(err.message ?? 'Failed to load contact history');
    } finally {
      setLoading(false);
    }
  }, [listing.id]);

  useEffect(() => {
    fetchHistory();
  }, [fetchHistory]);

  useEffect(() => {
    appSettingsApi
      .getSettings()
      .then((s) => {
        setAvailabilityText(s.availabilityText ?? '');
        setSenderName(s.senderName ?? '');
      })
      .catch(() => {
        setAvailabilityText('');
        setSenderName('');
      });
  }, []);

  // Re-seeds the editable subject/body once the real availabilityText/
  // senderName come back from Settings — but only if the user hasn't
  // started editing yet, so a fast typist doesn't get their draft silently
  // overwritten.
  useEffect(() => {
    if (availabilityText !== null && senderName !== null && !edited) {
      setSubject(draft.subject);
      setBody(draft.body);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [availabilityText, senderName]);

  const logEntry = async (channel, direction, message, entrySubject) => {
    setSaving(true);
    setError(null);
    try {
      await commHistoryApi.addEntry(listing.id, {
        channel,
        direction,
        subject: entrySubject ?? null,
        message,
      });
      await fetchHistory();
      onLogged?.();
    } catch (err) {
      setError(err.message ?? 'Failed to log contact');
    } finally {
      setSaving(false);
    }
  };

  const handleCopy = async () => {
    await navigator.clipboard.writeText(body);
  };

  const handleLogReply = async () => {
    if (!replyText.trim()) return;
    await logEntry('Email', 'Inbound', replyText.trim());
    setReplyText('');
  };

  // WhatsApp opens in a real chat with this text pre-filled — still one tap
  // away from actually sending (WhatsApp's own Send button), so it's logged
  // the same way as Email/Phone: the user confirms afterward via "I've sent
  // this — log it", not automatically on click.
  const handleOpenWhatsApp = () => setClickedChannel('WhatsApp');
  const handleCall = () => setClickedChannel('Phone');
  const phoneIsMobile = isPortugueseMobileNumber(listing.agentPhone);

  return (
    <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4">
      {/* Modals need to be solidly readable over whatever's behind them —
          the card-level bg-white/[0.06] "glass" treatment is far too
          transparent here and lets background text/badges bleed through. */}
      <div className="rounded-xl border border-white/10 bg-slate-900/95 backdrop-blur-xl shadow-2xl p-6 max-w-lg w-full max-h-[90vh] overflow-y-auto">
        <div className="flex justify-between items-start mb-4">
          <h2 className="text-2xl font-bold text-white">Contact Agent</h2>
          <button
            onClick={onClose}
            className="text-slate-400 hover:text-slate-300 text-xl leading-none"
          >
            ×
          </button>
        </div>

        <p className="text-sm text-slate-400 mb-4">
          {listing.agentPhone
            ? 'This drafts a message for you to review and edit. Nothing sends automatically — tap "Open in WhatsApp" (or Email) below, hit send there yourself, then log it here to keep a record.'
            : 'This drafts a message for you to review — nothing is sent automatically. Send it yourself via your email client, WhatsApp, or by phone, then log it below to keep a record.'}
        </p>

        {error && (
          <div className="mb-4 rounded-xl border border-red-500/20 bg-red-500/10 p-3 text-sm text-red-400">
            {error}
          </div>
        )}

        <div className="mb-4 rounded-xl border border-white/10 bg-white/5 p-4 space-y-3">
          <div>
            <label className="block text-xs font-medium text-slate-400 mb-1">Subject</label>
            <input
              type="text"
              value={subject}
              onChange={(e) => {
                setEdited(true);
                setSubject(e.target.value);
              }}
              className="w-full rounded-lg border border-white/10 bg-white/10 px-3 py-2 text-sm text-white focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-slate-400 mb-1">Message</label>
            <textarea
              value={body}
              onChange={(e) => {
                setEdited(true);
                setBody(e.target.value);
              }}
              rows={7}
              className="w-full rounded-lg border border-white/10 bg-white/10 px-3 py-2 text-sm text-white focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
          </div>

          <div className="flex gap-2 flex-wrap">
            {listing.agentPhone && phoneIsMobile && (
              <a
                href={buildWhatsAppLink(listing.agentPhone, body)}
                target="_blank"
                rel="noopener noreferrer"
                onClick={handleOpenWhatsApp}
                title="Opens WhatsApp with this exact message pre-filled — you still tap Send there."
                className="rounded-lg bg-emerald-600 px-4 py-2 text-sm font-semibold text-white transition-colors hover:bg-emerald-700"
              >
                💚 Open in WhatsApp
              </a>
            )}
            {listing.agentPhone && !phoneIsMobile && (
              <a
                href={buildTelLink(listing.agentPhone)}
                onClick={handleCall}
                title="This is a landline number — it can't have a WhatsApp account, so call it instead."
                className="rounded-lg bg-white/10 px-4 py-2 text-sm font-semibold text-slate-200 transition-colors hover:bg-white/20"
              >
                📞 Call {listing.agentPhone}
              </a>
            )}
            {listing.agentEmail && (
              <a
                href={buildMailtoLink(listing.agentEmail, subject, body)}
                onClick={() => setClickedChannel('Email')}
                className="rounded-lg bg-blue-500 px-3 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-400"
              >
                Open in Email
              </a>
            )}
            <button
              onClick={handleCopy}
              className="rounded-lg bg-white/10 px-3 py-2 text-sm font-medium text-slate-300 transition-colors hover:bg-white/20"
            >
              Copy Message
            </button>
            <button
              onClick={() =>
                logEntry(
                  clickedChannel ??
                    (listing.agentPhone && phoneIsMobile
                      ? 'WhatsApp'
                      : listing.agentEmail
                        ? 'Email'
                        : 'Phone'),
                  'Outbound',
                  body,
                  subject
                )
              }
              disabled={saving}
              className="rounded-lg bg-emerald-600 px-3 py-2 text-sm font-medium text-white transition-colors hover:bg-emerald-700 disabled:opacity-50"
            >
              {saving ? 'Logging...' : "I've sent this — log it"}
            </button>
          </div>
          {!listing.agentEmail && !listing.agentPhone && (
            <p className="text-xs text-slate-400">
              No agent email or phone on file — use the site's own contact form, then log it above.
            </p>
          )}
        </div>

        <div className="mb-4 rounded-xl border border-white/10 bg-white/5 p-4">
          <label className="block text-xs font-medium text-slate-400 mb-1">
            Log a reply or note from the agent
          </label>
          <div className="flex gap-2">
            <input
              type="text"
              value={replyText}
              onChange={(e) => setReplyText(e.target.value)}
              placeholder="e.g. Agent confirmed the apartment faces south..."
              className="flex-1 rounded-lg border border-white/10 bg-white/10 px-3 py-2 text-sm text-white focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
            <button
              onClick={handleLogReply}
              disabled={saving || !replyText.trim()}
              className="rounded-lg bg-white/10 px-3 py-2 text-sm font-medium text-slate-300 transition-colors hover:bg-white/20 disabled:opacity-50"
            >
              Log
            </button>
          </div>
        </div>

        <div>
          <h3 className="text-xl font-bold text-white mb-3">Contact History</h3>
          {loading ? (
            <p className="text-sm text-slate-400">Loading...</p>
          ) : history.length === 0 ? (
            <p className="text-sm text-slate-400">No contact logged yet.</p>
          ) : (
            <div className="space-y-2">
              {history.map((entry) => (
                <div key={entry.id} className="rounded-xl border border-white/10 p-3 text-sm">
                  <div className="flex justify-between text-xs text-slate-400 mb-1">
                    <span>
                      {channelLabels[entry.channel] ?? entry.channel} ·{' '}
                      {entry.direction === 'Outbound' ? 'Sent by you' : 'Received'}
                    </span>
                    <span>{new Date(entry.createdAt).toLocaleString()}</span>
                  </div>
                  {entry.subject && <p className="font-medium text-white">{entry.subject}</p>}
                  <p className="text-slate-300 whitespace-pre-wrap">{entry.message}</p>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
