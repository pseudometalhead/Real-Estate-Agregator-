namespace EstateAggregator.Services.Scrapers;

// Response shapes for the third-party "Idealista Real Estate" API on RapidAPI
// (https://rapidapi.com/kiwimaker/api/idealista-real-estate, backed by
// apidea.es — NOT an official Idealista API). Field names verified directly
// against the provider's published OpenAPI 3.1 spec (https://apidea.es/openapi.json,
// components.schemas.SearchResponse / SearchElement / LocationsResponse /
// LocationNode), not guessed — only the fields this scraper actually uses
// are declared here; System.Text.Json ignores the rest.
internal class IdealistaSearchResponse
{
    public int Total { get; set; }
    public int TotalPages { get; set; }
    public int ActualPage { get; set; }
    public List<IdealistaSearchElement> ElementList { get; set; } = new();
}

internal class IdealistaSearchElement
{
    public string PropertyCode { get; set; } = string.Empty;
    public double? Price { get; set; }
    public string? Thumbnail { get; set; }
    public double? Size { get; set; }
    public int? Rooms { get; set; }
    public int? Bathrooms { get; set; }
    public string? Address { get; set; }
    public string? Province { get; set; }
    public string? Municipality { get; set; }
    public string? District { get; set; }
    public string? Neighborhood { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Url { get; set; }
    public string? Description { get; set; }

    // Verified live against a real search response: not in the published
    // OpenAPI spec's documented SearchElement fields, but present on real
    // results —
    // "contactInfo":{"commercialName":"...","phone1":{"phoneNumber":"...",...},"contactName":"..."}.
    // System.Text.Json ignores any of this shape's fields not declared here.
    public IdealistaContactInfo? ContactInfo { get; set; }
}

internal class IdealistaContactInfo
{
    public string? CommercialName { get; set; }
    public string? ContactName { get; set; }
    public IdealistaPhone? Phone1 { get; set; }
}

internal class IdealistaPhone
{
    public string? PhoneNumber { get; set; }
}

internal class IdealistaLocationsResponse
{
    public List<IdealistaLocationNode> Locations { get; set; } = new();
}

internal class IdealistaLocationNode
{
    public string? LocationId { get; set; }
    public string? Name { get; set; }

    // Observed live values: "Distrito" (district, e.g. "Porto" — matches
    // this app's notion of "district"), "Concelho" (municipality, e.g.
    // "Porto, Porto"), "Zona" (neighborhood/parish). Autocomplete returns
    // the municipality before the district for an ambiguous query like
    // "Porto" — see ResolveLocationIdAsync, which prefers "Distrito".
    public string? SubTypeText { get; set; }
}
