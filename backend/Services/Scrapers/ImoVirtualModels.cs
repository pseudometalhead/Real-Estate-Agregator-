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
