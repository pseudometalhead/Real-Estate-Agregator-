// Generates a draft inquiry message the user can review, edit, and send
// themselves (via their own email client, SMS, or by phone) — never sent
// automatically by the app; even the one-tap SMS flow (see ContactPanel)
// requires an explicit tap on this exact drafted text.
//
// Deliberately does NOT ask to schedule a viewing until the sun orientation
// is actually known (either extracted from the listing, or the agent has
// already answered a prior question about it) — no point proposing a visit
// to a property that might turn out to face the wrong way. The open-plan-
// kitchen question that used to run alongside this was removed; orientation
// is the one thing worth blocking a viewing request on.
export function draftInquiry(listing, { availabilityText, senderName } = {}) {
  const property = listing.property;
  const location = property?.location ?? 'o imóvel';
  const price = property?.price ? `€${property.price.toLocaleString()}` : '';
  const agentName = listing.agentName?.trim();
  const url = property?.url?.trim();
  const externalId = property?.externalId?.trim();

  const greeting = agentName ? `Olá ${agentName},` : 'Olá,';
  // The link and reference matter here beyond just courtesy: an agency
  // agent often has dozens of active listings, so naming the exact property
  // (not just "the apartment in Porto") is what lets them actually identify
  // which one without a back-and-forth — the reference is the same ID the
  // site itself uses for this listing, so it's something the agent can
  // search their own system for even without opening the link.
  const linkLine = url ? `\n\nAnúncio: ${url}${externalId ? ` (Ref. ${externalId})` : ''}` : '';
  const intro =
    `${greeting}\n\n` +
    `Tenho interesse no imóvel em ${location}${price ? ` (${price})` : ''} e gostaria de saber mais detalhes.${linkLine}`;
  const trimmedSenderName = senderName?.trim();
  const signature = trimmedSenderName ? `\n\nCumprimentos,\n${trimmedSenderName}` : '\n\nCumprimentos.';
  const closing = `\n\nFico a aguardar a sua resposta.${signature}`;

  const orientationKnown = !!property?.sunOrientation && property.sunOrientation !== 'Not Available';

  let middle;
  if (!orientationKnown && !listing.askedAboutOrientation) {
    // Ask first, hold off on requesting a visit.
    middle =
      ' Antes de agendar uma visita, gostaria de perceber qual é a orientação solar do imóvel ' +
      '(a sul, norte, nascente ou poente), já que não consegui encontrar essa informação no anúncio.';
  } else if (orientationKnown) {
    const trimmedAvailability = availabilityText?.trim();
    const availabilityLine = trimmedAvailability ? ` Estou disponível: ${trimmedAvailability}.` : '';
    middle = ` Se possível, gostaria de agendar uma visita.${availabilityLine}`;
  } else {
    // Already asked about orientation but still don't know it — don't
    // repeat the question, but still don't propose a viewing yet either.
    middle = '';
  }

  return {
    subject: `Interesse no imóvel - ${location}`,
    body: `${intro}${middle}${closing}`,
  };
}

export function buildMailtoLink(email, subject, body) {
  const params = new URLSearchParams({ subject, body });
  return `mailto:${email}?${params.toString().replace(/\+/g, '%20')}`;
}

// SMS deep links have no universally-agreed syntax for the body param:
// iOS uses `sms:NUMBER&body=...`, Android/most others use
// `sms:NUMBER?body=...`. We use `?body=` as the primary since it has
// broader support. There is no subject line for SMS.
export function buildSmsLink(phone, body) {
  const cleanPhone = phone.replace(/[^\d+]/g, '');
  const params = new URLSearchParams({ body });
  return `sms:${cleanPhone}?${params.toString().replace(/\+/g, '%20')}`;
}

// wa.me click-to-chat: free, no account/API key/business verification —
// opens WhatsApp (app or web) directly in a chat with the agent, with this
// exact text pre-filled in the compose box. Same "one-tap send, never
// silent auto-send" rule as every other channel here: it still takes an
// explicit tap on WhatsApp's own Send button, nothing goes out from this
// app's code. Replaces the Twilio SMS integration, which needed a paid
// account and was never actually configured.
//
// wa.me requires E.164 digits with no "+" or spaces. Agent numbers captured
// by the scrapers are Portuguese local format (e.g. "912 345 678"), so a
// bare number is assumed local and given a "351" prefix — every listing
// this app tracks is in Portugal (see every scraper's country=pt/pt-PT
// assumptions).
export function buildWhatsAppLink(phone, body) {
  const digits = phone.replace(/[^\d+]/g, '');
  const e164Digits = digits.startsWith('+') ? digits.slice(1) : `351${digits}`;
  const params = new URLSearchParams({ text: body });
  return `https://wa.me/${e164Digits}?${params.toString()}`;
}

// Portuguese mobile numbers are always 9 digits starting with "9"; landline
// (geographic) numbers start with "2" (21 Lisboa, 22 Porto, etc.) — verified
// live: a real agent landline like "22 327 9548" produces a wa.me link that
// WhatsApp itself rejects with "isn't on WhatsApp", since a landline can
// never have a WhatsApp account, on any client. Used to hide the "Open in
// WhatsApp" button for numbers that could never work rather than let the
// user hit that dead end.
export function isPortugueseMobileNumber(phone) {
  if (!phone) return false;
  const digits = phone.replace(/[^\d]/g, '');
  const local = digits.startsWith('351') ? digits.slice(3) : digits;
  return /^9\d{8}$/.test(local);
}

export function buildTelLink(phone) {
  const digits = phone.replace(/[^\d+]/g, '');
  const e164Digits = digits.startsWith('+') ? digits : `+351${digits}`;
  return `tel:${e164Digits}`;
}
