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
