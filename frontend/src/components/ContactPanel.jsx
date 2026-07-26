import React, { useState, useEffect, useCallback } from 'react';
import { commHistoryApi } from '../api/commHistoryApi';
import { draftInquiry, buildMailtoLink } from '../utils/messageTemplates';

const channelLabels = {
  Email: '✉️ Email',
  Phone: '📞 Phone',
  Site: '🌐 Site form',
  InPerson: '🤝 In person',
  Other: 'Other',
};

export function ContactPanel({ listing, onClose, onLogged }) {
  const [history, setHistory] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const draft = draftInquiry(listing);
  const [subject, setSubject] = useState(draft.subject);
  const [body, setBody] = useState(draft.body);
  const [replyText, setReplyText] = useState('');
  const [saving, setSaving] = useState(false);

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

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-lg p-6 max-w-lg w-full max-h-[90vh] overflow-y-auto">
        <div className="flex justify-between items-start mb-4">
          <h2 className="text-2xl font-bold">Contact Agent</h2>
          <button onClick={onClose} className="text-gray-500 hover:text-gray-700 text-xl leading-none">
            ×
          </button>
        </div>

        <p className="text-sm text-gray-600 mb-4">
          This drafts a message for you to review — nothing is sent automatically. Send it yourself
          via your email client (or by phone), then log it below to keep a record.
        </p>

        {error && <div className="mb-4 p-3 bg-red-50 text-red-700 text-sm rounded">{error}</div>}

        <div className="mb-4 p-4 bg-gray-50 rounded space-y-3">
          <div>
            <label className="block text-xs font-medium text-gray-600 mb-1">Subject</label>
            <input
              type="text"
              value={subject}
              onChange={(e) => setSubject(e.target.value)}
              className="w-full px-3 py-2 border rounded text-sm"
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-600 mb-1">Message</label>
            <textarea
              value={body}
              onChange={(e) => setBody(e.target.value)}
              rows={7}
              className="w-full px-3 py-2 border rounded text-sm"
            />
          </div>

          <div className="flex gap-2 flex-wrap">
            {listing.agentEmail && (
              <a
                href={buildMailtoLink(listing.agentEmail, subject, body)}
                className="px-3 py-2 bg-blue-600 text-white rounded text-sm hover:bg-blue-700"
              >
                Open in Email
              </a>
            )}
            <button
              onClick={handleCopy}
              className="px-3 py-2 bg-gray-200 rounded text-sm hover:bg-gray-300"
            >
              Copy Message
            </button>
            <button
              onClick={() => logEntry(listing.agentEmail ? 'Email' : 'Phone', 'Outbound', body, subject)}
              disabled={saving}
              className="px-3 py-2 bg-emerald-600 text-white rounded text-sm hover:bg-emerald-700 disabled:opacity-50"
            >
              {saving ? 'Logging...' : "I've sent this — log it"}
            </button>
          </div>
          {!listing.agentEmail && (
            <p className="text-xs text-gray-500">
              No agent email on file — call {listing.agentPhone || 'the agent'} and read this, or use
              the site's own contact form, then log it above.
            </p>
          )}
        </div>

        <div className="mb-4 p-4 bg-gray-50 rounded">
          <label className="block text-xs font-medium text-gray-600 mb-1">
            Log a reply or note from the agent
          </label>
          <div className="flex gap-2">
            <input
              type="text"
              value={replyText}
              onChange={(e) => setReplyText(e.target.value)}
              placeholder="e.g. Agent confirmed the apartment faces south..."
              className="flex-1 px-3 py-2 border rounded text-sm"
            />
            <button
              onClick={handleLogReply}
              disabled={saving || !replyText.trim()}
              className="px-3 py-2 bg-gray-700 text-white rounded text-sm hover:bg-gray-800 disabled:opacity-50"
            >
              Log
            </button>
          </div>
        </div>

        <div>
          <h3 className="font-semibold mb-2">Contact History</h3>
          {loading ? (
            <p className="text-sm text-gray-500">Loading...</p>
          ) : history.length === 0 ? (
            <p className="text-sm text-gray-500">No contact logged yet.</p>
          ) : (
            <div className="space-y-2">
              {history.map((entry) => (
                <div key={entry.id} className="border border-gray-200 rounded p-3 text-sm">
                  <div className="flex justify-between text-xs text-gray-500 mb-1">
                    <span>
                      {channelLabels[entry.channel] ?? entry.channel} ·{' '}
                      {entry.direction === 'Outbound' ? 'Sent by you' : 'Received'}
                    </span>
                    <span>{new Date(entry.createdAt).toLocaleString()}</span>
                  </div>
                  {entry.subject && <p className="font-medium">{entry.subject}</p>}
                  <p className="text-gray-700 whitespace-pre-wrap">{entry.message}</p>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
