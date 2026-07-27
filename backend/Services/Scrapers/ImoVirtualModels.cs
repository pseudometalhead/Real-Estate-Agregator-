namespace EstateAggregator.Services.Scrapers;

// Minimal shape of ImoVirtual.com's embedded Next.js __NEXT_DATA__ JSON blob —
// only the fields this scraper actually uses. Unknown JSON properties are
// ignored by System.Text.Json by default, so this can stay narrow.
internal class ImoVirtualNextData
{
    public ImoVirtualProps? Props { get; set; }
}

internal class ImoVirtualProps
{
    public ImoVirtualPageProps? PageProps { get; set; }
}

internal class ImoVirtualPageProps
{
    public ImoVirtualDataContainer? Data { get; set; }
}

internal class ImoVirtualDataContainer
{
    public ImoVirtualSearchAds? SearchAds { get; set; }
}

internal class ImoVirtualSearchAds
{
    public List<ImoVirtualItem> Items { get; set; } = new();
    public ImoVirtualPagination? Pagination { get; set; }
}

internal class ImoVirtualPagination
{
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
    public int CurrentPage { get; set; }
}

internal class ImoVirtualItem
{
    public long Id { get; set; }
    public string? Slug { get; set; }
    public string? RoomsNumber { get; set; }
    public decimal? AreaInSquareMeters { get; set; }
    public string? ShortDescription { get; set; }
    public ImoVirtualMoney? TotalPrice { get; set; }
    public ImoVirtualLocation? Location { get; set; }
    public List<ImoVirtualImage>? Images { get; set; }

    // Free at search-result level, verified live — the private-seller name
    // ("Susana Borges Barreto de Freitas") or, for agency listings, null
    // here with Agency populated instead ("Terrace Benefit, Lda"). Neither
    // carries a phone number at this level (advertOwner.contacts is always
    // an empty array in search results) — that requires the per-listing
    // detail page, see ImoVirtualScraper.FetchPhoneAsync.
    public ImoVirtualAdvertOwner? AdvertOwner { get; set; }
    public ImoVirtualAgency? Agency { get; set; }
}

internal class ImoVirtualAdvertOwner
{
    public string? Name { get; set; }
}

internal class ImoVirtualAgency
{
    public string? Name { get; set; }
}

// Minimal shape of the detail page's own __NEXT_DATA__ — fetched only for
// genuinely new properties (see ImoVirtualScraper.ScrapeDistrictAsync) to
// resolve the phone number, which isn't present anywhere in the search
// response. Verified live: props.pageProps.unifiedAd.resolvedPhone is a
// plain string, already in the site's own display format.
internal class ImoVirtualDetailNextData
{
    public ImoVirtualDetailProps? Props { get; set; }
}

internal class ImoVirtualDetailProps
{
    public ImoVirtualDetailPageProps? PageProps { get; set; }
}

internal class ImoVirtualDetailPageProps
{
    public ImoVirtualUnifiedAd? UnifiedAd { get; set; }

    // The full-resolution photo gallery (12+ on a typical listing) — only
    // present here, never in the search-result response's own Images list,
    // which is capped to a handful of preview thumbnails. See
    // ImoVirtualScraper.FetchDetailAsync.
    public ImoVirtualAd? Ad { get; set; }
}

internal class ImoVirtualUnifiedAd
{
    public string? ResolvedPhone { get; set; }

    // HTML fragment (e.g. "<p>Apartamento...</p><p>Mais informações...</p>"),
    // unlike the search-result's ShortDescription which is plain-text and
    // genuinely truncated mid-sentence. See ImoVirtualScraper.StripDescriptionHtml.
    public string? Description { get; set; }
}

internal class ImoVirtualAd
{
    public List<ImoVirtualDetailImage>? Images { get; set; }
}

internal class ImoVirtualDetailImage
{
    public string? Medium { get; set; }
}

internal class ImoVirtualMoney
{
    public decimal Value { get; set; }
}

internal class ImoVirtualLocation
{
    public ImoVirtualAddress? Address { get; set; }
}

internal class ImoVirtualAddress
{
    public ImoVirtualNamedEntity? City { get; set; }
    public ImoVirtualNamedEntity? Province { get; set; }
}

internal class ImoVirtualNamedEntity
{
    public string? Name { get; set; }
}

internal class ImoVirtualImage
{
    public string? Medium { get; set; }
}
