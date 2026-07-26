// Generates a draft inquiry message the user can review, edit, and send
// themselves (via their own email client or by phone) — never sent
// automatically by the app.
export function draftInquiry(listing) {
  const property = listing.property;
  const location = property?.location ?? 'o imóvel';
  const price = property?.price ? `€${property.price.toLocaleString()}` : '';
  const agentName = listing.agentName?.trim();

  const greeting = agentName ? `Olá ${agentName},` : 'Olá,';

  const orientationLine =
    property?.sunOrientation === 'Not Available' && !listing.askedAboutOrientation
      ? '\n\nTambém gostaria de saber qual é a orientação solar do imóvel (a sul, norte, nascente ou poente), já que não consegui encontrar essa informação no anúncio.'
      : '';

  const body =
    `${greeting}\n\n` +
    `Tenho interesse no imóvel em ${location}${price ? ` (${price})` : ''} e gostaria de saber mais detalhes ` +
    `e, se possível, agendar uma visita.${orientationLine}\n\n` +
    `Fico a aguardar a sua resposta.\n\nCom os melhores cumprimentos.`;

  const subject = `Interesse no imóvel - ${location}`;

  return { subject, body };
}

export function buildMailtoLink(email, subject, body) {
  const params = new URLSearchParams({ subject, body });
  return `mailto:${email}?${params.toString().replace(/\+/g, '%20')}`;
}
