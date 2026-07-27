using System.Text.RegularExpressions;

namespace EstateAggregator.Utilities;

// Strips agency self-promotion / sales boilerplate from a full description
// before it's stored for display — see MapToProperty in every scraper: this
// runs AFTER Orientation/Elevator/Parking/etc. extraction, never before, so
// a boilerplate sentence that happens to contain a real signal word doesn't
// cost those extractors any recall.
//
// Patterns below are built from real listings, not guessed — see the
// "Zome" example (a large real-estate franchise) that appends a "3 razões
// para comprar com a Zome" testimonial section plus a broker-share notice
// after the actual property description, and several one-line CTAs
// ("Contacte hoje mesmo...", "Entre em contacto...") seen verbatim across
// unrelated agencies. Does NOT attempt to detect/strip duplicate bilingual
// content (a PT description followed by its own English translation, also
// observed live) — reliably telling "a translation of the same listing"
// apart from "more distinct information" without a translation/NLP
// dependency was judged too failure-prone to ship; flagged as a known gap.
public static class DescriptionCleaner
{
    // Once this appears, everything from that point to the end is agency
    // marketing copy about the AGENCY, not information about this specific
    // property — so it's a truncation point, not a line to individually
    // filter out.
    private static readonly Regex TruncateFromPattern = new(
        @"\d+\s*raz[õo]es\s*para\s*comprar\s*com\s*a\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Broker-to-broker business-sharing disclosure ("if you're a real
    // estate consultant, this property is available for shared business")
    // — near-universal Portuguese industry boilerplate seen verbatim across
    // unrelated agencies, never actual information about the property.
    private static readonly Regex BrokerShareNoticePattern = new(
        @"caso seja um consultor imobili[áa]rio.*?partilha de neg[óo]cio[^\n]*\.?",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    // Whole-line call-to-action / contact-prompt patterns — anchored to the
    // full line (^...$) so a factual sentence that merely mentions
    // "contacte" alongside real details isn't discarded, only a line that
    // IS the CTA.
    private static readonly Regex[] CtaLinePatterns =
    {
        new(@"^\W*(agende|marque)\s+(j[áa]\s+)?a\s+sua\s+visita\W*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^\W*contacte(-nos)?\b[^\n]*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^\W*entre em contacto\b[^\n]*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^\W*n[ãa]o hesite em (contact|apresent)\w*[^\n]*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^\W*venha conhecer\W*.{0,30}$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        // A bare reference-code line, e.g. "ref:APA_1765." — an internal
        // agency code, not something a house hunter reads for information.
        new(@"^\W*ref\.?\s*:\s*\S+\.?\W*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
    };

    // Listings decorate section headers with an emoji bullet ("📍
    // Localização central...", "🔧 Conclusão da obra...") encoded as an HTML
    // numeric character reference (e.g. "&#128205;"). HtmlAgilityPack's
    // HtmlEntity.DeEntitize — called by every scraper before Clean() ever
    // sees the text — has a known gap for numeric references above the
    // Basic Multilingual Plane (i.e. U+10000+, exactly where every emoji
    // lives): it consumes the leading "&" while failing to actually decode
    // the codepoint, leaving the bare "#128205;" behind verbatim. Verified
    // live on a CustoJusto listing (four separate instances in one
    // description, always at the start of a line). Reconstructed here from
    // the leftover digits — a plain string.Replace("&#", ...) can't fix this
    // upstream since the "&" is already gone by the time any scraper's text
    // reaches DeEntitize's output.
    private static readonly Regex BrokenAstralEntityPattern = new(
        @"^#(\d{4,7});[ \t]*", RegexOptions.Multiline | RegexOptions.Compiled);

    // Defensive net for literal HTML markup surviving into the stored
    // description as plain text — verified live on an ImoVirtual listing
    // where a detail-page description came through with raw "<br>ACABAMENTOS
    // EXTERIORES<br>..." instead of line breaks. ImoVirtualScraper's own
    // StripDescriptionHtml handles this at scrape time for the one field it
    // wraps, but it can't retroactively fix rows already stored before that
    // existed, and doesn't cover every other scraper/field — running here
    // means every source and every already-stored row gets the same
    // guarantee. Block tags become a newline (so paragraphs don't get mashed
    // together); anything else still shaped like a tag is just dropped.
    private static readonly Regex HtmlBreakPattern = new(
        @"<\s*(br|/p|/div|/li)\s*/?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex HtmlTagPattern = new(
        @"<[a-zA-Z/][^<>]*>", RegexOptions.Compiled);

    // U+FFFD (the Unicode replacement character) shows up verbatim in a
    // small number of listings' raw description text — verified live by
    // fetching the source directly (RapidAPI's Idealista feed, and
    // separately ImoVirtual's own served HTML): the literal "�" bytes are
    // already present in what the source sends, before this app's HTTP
    // client or JSON/HTML parsing ever touches it. That means the original
    // character (almost always one accented Portuguese letter) was lost
    // upstream — most likely an agent's listing text was entered in one
    // encoding and mis-transcoded by the portal's own backend before
    // publishing — and there is no way to recover what it originally said.
    // Rather than show the ugly "�" glyph, this drops it; a slightly
    // misspelled word ("servios" instead of "serviços") reads better than
    // visible corruption. Collapses any doubled space left behind so words
    // don't end up run together or with a stray gap.
    private static readonly Regex ReplacementCharPattern = new(
        "�", RegexOptions.Compiled);

    public static string? Clean(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return description;

        var withoutHtml = HtmlBreakPattern.Replace(description, "\n");
        withoutHtml = HtmlTagPattern.Replace(withoutHtml, "");
        withoutHtml = ReplacementCharPattern.Replace(withoutHtml, "");
        withoutHtml = Regex.Replace(withoutHtml, "[ \t]{2,}", " ");

        var text = BrokenAstralEntityPattern.Replace(withoutHtml, match =>
        {
            var codepoint = int.Parse(match.Groups[1].Value);
            try
            {
                return char.ConvertFromUtf32(codepoint) + " ";
            }
            catch (ArgumentOutOfRangeException)
            {
                // Not a valid Unicode codepoint after all — leave it alone
                // rather than guess.
                return match.Value;
            }
        });

        var truncateMatch = TruncateFromPattern.Match(text);
        if (truncateMatch.Success)
            text = text[..truncateMatch.Index];

        text = BrokerShareNoticePattern.Replace(text, string.Empty);

        var lines = text.Split('\n');
        var keptLines = lines.Where(line => !CtaLinePatterns.Any(p => p.IsMatch(line.Trim())));
        text = string.Join('\n', keptLines);

        // Collapse the blank-line gaps left behind by removed lines/the
        // truncation above.
        text = Regex.Replace(text, @"\n{3,}", "\n\n").Trim();

        // Defensive fallback: if cleaning somehow stripped everything (e.g.
        // a description that was pure boilerplate to begin with), showing
        // the original uncleaned text beats showing nothing.
        return string.IsNullOrWhiteSpace(text) ? description : text;
    }
}
