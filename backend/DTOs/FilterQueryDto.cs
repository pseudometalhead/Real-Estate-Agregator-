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
}
