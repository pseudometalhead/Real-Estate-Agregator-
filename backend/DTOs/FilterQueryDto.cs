namespace EstateAggregator.DTOs;

public class FilterQueryDto
{
    public decimal? PriceMin { get; set; }
    public decimal? PriceMax { get; set; }
    public int? Beds { get; set; }
    public string? Orientation { get; set; }
    public string? Location { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;

    // "date" (default), "price", "size"
    public string? SortBy { get; set; }
    // "asc" or "desc" (default depends on SortBy: "date" defaults desc, others asc)
    public string? SortDir { get; set; }
}
