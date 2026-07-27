# SMS Contact Plan

> **Superseded**: this plan's Twilio SMS integration was removed in favor of
> free WhatsApp click-to-chat links (`wa.me`) — no paid account needed. See
> `frontend/src/utils/messageTemplates.js` (`buildWhatsAppLink`) and
> `ContactPanel.jsx`. Kept below for historical context only.

## Current state

`ContactPanel.jsx` already has a clear philosophy, stated directly in the
code (`CommHistoryController.cs`): **the app never sends anything to an
agent/seller automatically.** It drafts a message, hands off to *your own*
email client via a `mailto:` link, and you log what you actually sent
afterward. There's no "SMS" channel yet, but `CommHistoryEntry.Channel` is
a free string, so adding one costs nothing schema-wise.

Two ways to add SMS, with very different scope:

## Option A — `sms:` deep link (recommended, matches existing design)

Exactly the same pattern as the existing `mailto:` button:

- Add a `buildSmsLink(phone, body)` helper next to `buildMailtoLink()` in
  `messageTemplates.js`, producing an `sms:+351912345678?body=...` link.
- Add an "Open in Messages" button in `ContactPanel.jsx` next to "Open in
  Email", shown when `listing.agentPhone` is set.
- Clicking it opens *your own* phone/SMS app (or Windows' Phone Link, or
  macOS Messages, depending on what's registered as the `sms:` handler on
  your device) pre-filled with the draft. You review and hit send
  yourself, then log it as `channel: "SMS"` through the comm-history
  endpoint that already exists.

**Effort:** tiny — one helper function, one button, no backend change.
**Cost:** none.
**Risk:** none — no third party involved, no automated sending, fits the
app's existing "never send on our own" design exactly.
**Caveat:** only as good as whatever `sms:` handler is registered on the
device you're using the app from. Great on a phone; on a bare desktop
browser with nothing configured, the link may not do anything.

## Option B — real automated SMS sending (e.g. Twilio)

A genuinely different feature: the backend calls a provider's API and the
message goes out without you touching your phone.

- New `POST /api/mylistings/{id}/sms` endpoint that calls Twilio (or a
  similar provider) server-side.
- Needs: a Twilio account, a provisioned phone number (recurring cost +
  per-message cost, international/PT pricing to check), API credentials
  stored as a new secret (`.env`, never committed), error handling for
  failed sends, and — since it's now real automated outbound contact —
  probably a confirmation step before it fires, plus the comm-history log
  entry gets written automatically on send instead of manually.
- This is a real shift from the app's current design principle (the
  `CommHistoryController.cs` comment literally says the app never sends
  on its own) — worth doing deliberately, not as a side effect of "add
  SMS."

**Effort:** medium — new provider account/credentials, a new backend
integration, a bit of UX work around confirmation and failure states.
**Cost:** real, ongoing (Twilio number + per-SMS fees).
**Risk:** low for genuine 1:1 personal outreach to a business number
(this isn't bulk/marketing messaging), but it's a new operational
responsibility — rate-limiting to avoid accidental double-sends, cost
monitoring, credential handling.

## Recommendation

Build **Option A** by default — it's small, free, and consistent with
how the rest of the contact flow already works. Only reach for **Option
B** if manual sending becomes an actual bottleneck (e.g. you're
contacting enough agents that opening your phone each time is the
friction point) and you're fine adding a paid third-party dependency.
