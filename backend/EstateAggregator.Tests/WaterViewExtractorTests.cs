using EstateAggregator.Utilities;

namespace EstateAggregator.Tests;

public class WaterViewExtractorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Apartamento T2 com vista desafogada sobre a cidade.")]
    public void Extract_NoWaterViewMentioned_ReturnsNull(string? description)
    {
        Assert.Null(WaterViewExtractor.Extract(description));
    }

    [Theory]
    [InlineData("Apartamento com vista mar deslumbrante.")]
    [InlineData("T3 com vista para o rio Douro.")]
    [InlineData("Varanda com vista rio.")]
    [InlineData("Excelente vista Douro a partir da sala.")]
    public void Extract_WaterViewMentioned_ReturnsTrue(string description)
    {
        Assert.True(WaterViewExtractor.Extract(description));
    }
}
