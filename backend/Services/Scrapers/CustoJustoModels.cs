namespace EstateAggregator.Services.Scrapers;

// Minimal shape of custojusto.pt's embedded Next.js __NEXT_DATA__ JSON blob —
// only the fields this scraper actually uses. Unknown JSON properties are
// ignored by System.Text.Json by default, so this can stay narrow. Verified
// live against https://www.custojusto.pt/porto/imobiliario/apartamentos.
internal class CustoJustoNextData
{
    public CustoJustoProps? Props { get; set; }
}

internal class CustoJustoProps
{
    public CustoJustoPageProps? PageProps { get; set; }
}

internal class CustoJustoPageProps
{
    public List<CustoJustoListItem>? ListItems { get; set; }
}

internal class CustoJustoListItem
{
    public string? ListID { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }

    // Seller/agency display name — free at search-result level (e.g. "Zome",
    // "HabiPredilecta"), verified live. The phone number is NOT available
    // anywhere in server-rendered data — even the individual listing's own
    // page only carries a `phoneHidden` flag, with the actual number
    // revealed via a client-side action after page load (a separate AJAX
    // call this scraper doesn't replicate — reverse-engineering a
    // click-to-reveal contact gate is out of scope here). AgentPhone stays
    // unset for this source.
    public string? Name { get; set; }

    // "sell" for for-sale listings, "let" for rentals — custojusto's
    // apartamentos category page mixes both, so this scraper filters to
    // "sell" only to match every other scraper's buy-focused results.
    public string? Type { get; set; }

    public decimal? Price { get; set; }

    // Site-relative, e.g. "/porto/imobiliario/apartamentos/t3-...-45101592".
    public string? Url { get; set; }
    public string? ImageFullURL { get; set; }
    public CustoJustoLocationNames? LocationNames { get; set; }
    public CustoJustoParams? Params { get; set; }
}

internal class CustoJustoLocationNames
{
    public string? District { get; set; }
    public string? County { get; set; }
    public string? Parish { get; set; }
}

internal class CustoJustoParams
{
    // Typology, e.g. "T3", "T1+1". Parsed with the same [Tt](\d+) pattern
    // CasaSapoScraper/CaixaImobiliarioScraper already use.
    public string? Rooms { get; set; }

    // e.g. "108m²" — parsed with the same (\d+(?:[.,]\d+)?)\s*m pattern
    // CasaSapoScraper already uses.
    public string? Size { get; set; }
}

// The individual listing page's own __NEXT_DATA__ blob — a different shape
// from the search-results page above (props.pageProps.adData, not
// props.pageProps.listItems). Verified live against
// https://www.custojusto.pt/porto/imobiliario/apartamentos/apartamento-t3-...:
// the search page's own "body" field is a short (~150-char) preview that
// cuts off mid-sentence — the individual page's adData.body carries the
// complete text (1900+ chars on the verified listing) — same class of
// truncation ImoVirtualScraper already works around via its own detail-page
// fetch. Bonus found free in the same JSON: adData.location (exact lat/lon,
// no geocoding needed) and adData.params.energyrating.name (structured, more
// reliable than regex-matching EnergyRatingExtractor over free text).
internal class CustoJustoDetailNextData
{
    public CustoJustoDetailProps? Props { get; set; }
}

internal class CustoJustoDetailProps
{
    public CustoJustoDetailPageProps? PageProps { get; set; }
}

internal class CustoJustoDetailPageProps
{
    public CustoJustoAdData? AdData { get; set; }
}

internal class CustoJustoAdData
{
    public string? Body { get; set; }
    public CustoJustoAdLocation? Location { get; set; }
    public CustoJustoAdParams? Params { get; set; }
}

internal class CustoJustoAdLocation
{
    public double? Lat { get; set; }
    public double? Lon { get; set; }
}

internal class CustoJustoAdParams
{
    public CustoJustoEnergyRating? EnergyRating { get; set; }
}

internal class CustoJustoEnergyRating
{
    // e.g. "C" — already the short display form, no parsing needed.
    public string? Name { get; set; }
}
